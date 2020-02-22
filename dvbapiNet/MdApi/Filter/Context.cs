using dvbapiNet.Dvb;
using dvbapiNet.Log;
using dvbapiNet.Log.Locale;
using dvbapiNet.Oscam;
using System;
using System.Runtime.InteropServices;

namespace dvbapiNet.MdApi.Filter
{
    internal class Context : IDisposable
    {
        private IntPtr _StructPtr;
        private const string cLogSection = "mdapi filter";
        private const int cTsPacketSize = 188;
        private const int cTsHeaderSize = 4;
        private const byte cTsSyncByte = 0x47;

        private DvbApiAdapter _Adapter;

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate void FilterDataArrivalCallback(int filter, int len, IntPtr data);

        private FilterDataArrivalCallback _Cb;
        private IntPtr _CbPtr;

        private TStartFilter _FilterStruct;

        private uint _ContCounter;

        private IntPtr _MdapiWindow;

        private SectionBase _Section;

        private byte[] _FilterTables;

        private bool _Disposed;

        public ushort FilterPid
        {
            get;
        }

        public ushort FilterId
        {
            get;
        }

        public Context(DvbApiAdapter adapter, byte[] dllName, IntPtr window, bool ts188Support, int dllId, ushort pid, ushort filterNum)
        {
            LogProvider.Add(DebugLevel.Info, cLogSection, Message.MdapiCtxStartFilter, pid, filterNum);

            _Disposed = false;
            _ContCounter = 0;

            _Cb = new FilterDataArrivalCallback(FilterDataArrival);
            _CbPtr = Marshal.GetFunctionPointerForDelegate(_Cb);
            _Adapter = adapter;

            _MdapiWindow = window;

            FilterPid = pid;
            FilterId = filterNum;

            switch (pid)
            {
                case 0x00: // PAT
                    _FilterTables = new byte[] { 0x00 };
                    break;

                case 0x01: // CAT
                    _FilterTables = new byte[] { 0x01 };
                    break;

                case 0x11: // SDT (nur actual)
                    _FilterTables = new byte[] { 0x42 };
                    break;

                default:
                    _FilterTables = new byte[] {
                        0x02, 0x80, 0x81, 0x82, 0x83, 0x84, 0x85, 0x86, 0x87, 0x88, 0x89, 0x8a, 0x8b, 0x8c, 0x8d, 0x8e, 0x8f
                    }; // PMT, ECM (0x80, 0x81) & EMM (0x82 - 0x8f)
                    break;
            }

            _Section = new SectionBase(FilterPid, _FilterTables);
            // theoretisch könnte man über PMT-Pid feststellen, dass es PMT ist, dann über
            // die PMT selbst die einzelnen Table-IDs ermitteln um so noch sicherere Daten zu erhalten.

            _FilterStruct = new TStartFilter();
            _FilterStruct.Name = new byte[32];
            _FilterStruct.FilterId = filterNum;
            _FilterStruct.Pid = FilterPid;
            _FilterStruct.DllId = (ushort)dllId;
            _FilterStruct.FilterCallback = _CbPtr;
            Array.Copy(dllName, 0, _FilterStruct.Name, 0, Math.Min(dllName.Length, _FilterStruct.Name.Length));
            _FilterStruct.Name[_FilterStruct.Name.Length - 1] = 0;

            if (ts188Support)
            {
                _FilterStruct.Pid |= 0x8000; // aktiviert im regulären MDAPI+ die 188byte
                // Nachfolgend für ProgDVB weitere Angaben
                _FilterStruct.Type = FilterType.Ts;
                _FilterStruct.ExtSize = 4;
            }

            _StructPtr = Marshal.AllocHGlobal(Marshal.SizeOf(_FilterStruct));
            Marshal.StructureToPtr(_FilterStruct, _StructPtr, false);

            int res = (int)Plugin.SendMessage(_MdapiWindow, (uint)WMessage.User, (UIntPtr)MdApiMessages.StartFilter, _StructPtr);

            if (res == 0)
            {
                uint err = Plugin.GetLastError();
                LogProvider.Add(DebugLevel.Warning, cLogSection, Message.MdapiCtxStartFilterFailed, err);
            }
        }

        public void Dispose()
        {
            if (!_Disposed)
            {
                // Struct zurücklesen, Host passt RunningID an zwecks identifikation. Die wird für Stopfilter benötigt.
                TStartFilter str = (TStartFilter)Marshal.PtrToStructure(_StructPtr, typeof(TStartFilter));

                int res = (int)Plugin.SendMessage(_MdapiWindow, (uint)WMessage.User, (UIntPtr)MdApiMessages.StopFilter, new IntPtr(str.RunningId));
                LogProvider.Add(DebugLevel.Info, cLogSection, Message.MdapiCtxDelFilter, str.Pid, str.RunningId);
                if (res == 0)
                {
                    uint err = Plugin.GetLastError();
                    LogProvider.Add(DebugLevel.Warning, cLogSection, Message.MdapiCtxDelFilterFailed, err);
                }

                Marshal.FreeHGlobal(_StructPtr);
                _Disposed = true;
            }
        }

        private void FilterDataArrival(int filter, int len, IntPtr data)
        {
            if (_Disposed)
            {
                LogProvider.Add(DebugLevel.Error, cLogSection, Message.MdapiCtxFilterDisposed, filter, FilterPid);
                return;
            }

            try
            {
                int offset = 0;

                if (len % cTsPacketSize == 0)
                {
                    _Adapter.ProcessRawTs(data, len);
                }
                else
                {
                    // ggf. Header versteckt.
                    if (len == (cTsPacketSize - cTsHeaderSize) && Marshal.ReadByte(data, -4) == cTsSyncByte)
                    {
                        _Adapter.ProcessRawTs(data - 4, cTsPacketSize);
                    }
                    else // Header rekonstruieren
                    {
                        if (len % (cTsPacketSize - cTsHeaderSize) == 0)
                        {
                            while (offset < len)
                            {
                                bool c = _Section.AddPacket(data + offset);

                                if (c)
                                {
                                    SectionBase t = _Section;

                                    while (t != null)
                                    {
                                        if (t.Finished)
                                        {
                                            byte[] packets = t.Packetize(ref _ContCounter);
                                            if (packets != null)
                                            {
                                                IntPtr p = Marshal.AllocHGlobal(packets.Length);
                                                Marshal.Copy(packets, 0, p, packets.Length);
                                                _Adapter.ProcessRawTs(p, packets.Length);
                                                Marshal.FreeHGlobal(p);
                                            }
                                        }
                                        else
                                        {
                                            _Section = t;
                                            break;
                                        }

                                        t = t.NextSection;
                                    }
                                }

                                if (_Section.Finished)
                                    _Section = new SectionBase(FilterPid, _FilterTables);

                                offset += cTsPacketSize - cTsHeaderSize;
                            }
                        } // ansonsten nicht parsbar...
                    }
                }
            }
            catch (Exception ex)
            {
                LogProvider.Exception(cLogSection, Message.MdapiCtxProcessFailed, ex);
            }
        }
    }
}
