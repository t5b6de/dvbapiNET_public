using dvbapiNet.Log;
using dvbapiNet.Log.Locale;
using dvbapiNet.Oscam;
using System;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace dvbapiNet.DvbViewer
{
    /// <summary>
    /// Plugin-Schnittstelle zum DVBViewer, hier sind die exports drin.
    /// </summary>
    public static class Plugin
    {
        public static TransponderCallback _Cb;
        private const string cLogSection = "dvbv plg";

        private static TTransCallData _CallbackData;
        private static IntPtr _CopyrightString;
        private static DvbApiAdapter _DvbAdapter;
        private static IntPtr _DvbViewerHwnd;
        private static IntPtr _NameString;
        private static IntPtr _NativeTsCallback;
        private static IntPtr _TypeString;
        private static IntPtr _VersionString;

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        public delegate byte TransponderCallback(IntPtr buf, int len);

        /// <summary>
        /// DVBViewer Plugin-Interface.
        /// </summary>
        static Plugin()
        {
            FileVersionInfo fvi = Globals.PluginInfo;

            _CopyrightString = Marshal.StringToHGlobalAnsi(fvi.LegalCopyright);
            _TypeString = Marshal.StringToHGlobalAnsi("Plugin");
            _NameString = Marshal.StringToHGlobalAnsi(fvi.ProductName);
            _VersionString = Marshal.StringToHGlobalAnsi(Globals.Info);

            _DvbViewerHwnd = IntPtr.Zero;

            _CallbackData = new TTransCallData();
            _Cb = new TransponderCallback(ProcessRawTs);
            _CallbackData.TransCall = Marshal.GetFunctionPointerForDelegate(_Cb);

            _NativeTsCallback = Marshal.AllocHGlobal(Marshal.SizeOf(typeof(TTransCallData)));
            Marshal.StructureToPtr(_CallbackData, _NativeTsCallback, false);

            // Adapter anlegen, macht auch direkt Verbindung zum Server auf
            try
            {
                _DvbAdapter = new DvbApiAdapter(Globals.PipeName, false);
                _DvbAdapter.AddPidRequested += AddPid;
                _DvbAdapter.DelPidRequested += DelPid;
            }
            catch (Exception ex)
            {
                LogProvider.Exception(cLogSection, Message.DvbvInitFailed, ex);
            }
        }

        private static void AddPid(DvbApiAdapter sender, ushort pid)
        {
            PostMessage(_DvbViewerHwnd, (uint)WMessage.DvbViewer, new UIntPtr((uint)ParamMessages.StartFilter), new IntPtr(pid));
        }

        [DllExport(ExportName = "Copyright", CallingConvention = CallingConvention.StdCall)]
        public static IntPtr Copyright()
        {
            return _CopyrightString;
        }

        private static void DelPid(DvbApiAdapter sender, ushort pid)
        {
            PostMessage(_DvbViewerHwnd, (uint)WMessage.DvbViewer, new UIntPtr((uint)ParamMessages.StopFilter), new IntPtr(pid));
        }

        [DllExport(ExportName = "EventMsg", CallingConvention = CallingConvention.StdCall)]
        public static int EventMsg(Event ev, IntPtr data)
        {
            LogProvider.Add(DebugLevel.DvbViewerPluginEvent, cLogSection, Message.DvbvEvent, ev);
            try
            {
                switch (ev)
                {
                    case Event.InitComplete:
                        break;

                    case Event.TuneChannel:

                        if (data != IntPtr.Zero)
                        {
                            TChannel chan = (TChannel)Marshal.PtrToStructure(data, typeof(TChannel));
                            PostMessage(_DvbViewerHwnd, (uint)WMessage.DvbViewer, new UIntPtr((uint)ParamMessages.AddTsCall), _NativeTsCallback);
                            _DvbAdapter.Tune(chan.Sid, chan.PmtPid, chan.TransportStreamId, chan.NetworkId);
                        }
                        else if (_DvbAdapter.IsTuned) // bei einigen Versionen kommt hier null (nil in pascal)
                        {
                            PostMessage(_DvbViewerHwnd, (uint)WMessage.DvbViewer, new UIntPtr((uint)ParamMessages.DelTsCall), _NativeTsCallback);
                            _DvbAdapter.Tune(-1, -1, -1, -1);
                        }

                        break;

                    case Event.RemoveChannel: // DVBViewer
                    case Event.DisableTuner: // Recording-Service

                        if (_DvbAdapter.IsTuned)
                        {
                            PostMessage(_DvbViewerHwnd, (uint)WMessage.DvbViewer, new UIntPtr((uint)ParamMessages.DelTsCall), _NativeTsCallback);
                            _DvbAdapter.Tune(-1, -1, -1, -1);
                        }
                        break;

                    case Event.Unload:
                        Dispose();
                        break;
                }
            }
            catch (Exception ex)
            {
                LogProvider.Exception(cLogSection, Message.DvbvEventFailed, ex);
            }

            return 0;
        }

        [DllExport(ExportName = "SetAppHandle", CallingConvention = CallingConvention.StdCall)]
        public static void SetAppHandle(IntPtr wnd, IntPtr RebuildFunc)
        {
            _DvbViewerHwnd = wnd;
        }

        [DllExport(ExportName = "Version", CallingConvention = CallingConvention.StdCall)]
        public static IntPtr Version()
        {
            return _VersionString;
        }

        [DllExport(ExportName = "Execute", CallingConvention = CallingConvention.StdCall)]
        public static byte Execute(IntPtr Tuner, IntPtr pids)
        {
            // Alles hier entfernt. Alles wird nur noch über die Event Messages gesteuert.
            return 0;
        }

        [DllExport(ExportName = "LibTyp", CallingConvention = CallingConvention.StdCall)]
        public static IntPtr LibTyp()
        {
            return _TypeString;
        }

        [DllExport(ExportName = "PidCallback", CallingConvention = CallingConvention.StdCall)]
        public static void PidCallback(IntPtr tsPacket)
        {
            // Alles hier entfernt. Alles wird nur noch über RawTsCallback erledigt.
            // Grund: Fehler im Dvbviewer Media Server -> Dieser vermischt die Streams und gibt Packets des falschen Transponders aus.
        }

        [DllExport(ExportName = "PluginName", CallingConvention = CallingConvention.StdCall)]
        public static IntPtr PluginName()
        {
            return _NameString;
        }

        [DllImport("user32.dll")]
        private static extern IntPtr PostMessage(IntPtr hWnd, uint wMsg,
                                UIntPtr wParam, IntPtr lParam);

        /// <summary>
        /// Pakete sind ungefiltert aber sauber ausgerichtet und fehlerfrei.
        /// </summary>
        /// <param name="buffer">Ts-Packet</param>
        /// <param name="size">sollte immer vielfaches von 188 byte sein.</param>
        /// <returns></returns>
        public static byte ProcessRawTs(IntPtr buffer, int size)
        {
            try
            {
                _DvbAdapter.ProcessRawTs(buffer, size);
            }
            catch (Exception ex)
            {
                LogProvider.Exception(cLogSection, Message.DvbvProcessFailed, ex);
            }

            return 0;
        }

        private static void Dispose()
        {
            _DvbAdapter.Dispose();

            Marshal.FreeHGlobal(_CopyrightString);
            Marshal.FreeHGlobal(_TypeString);
            Marshal.FreeHGlobal(_NameString);
            Marshal.FreeHGlobal(_VersionString);
            Marshal.FreeHGlobal(_NativeTsCallback);
        }
    }
}
