using dvbapiNet.Dvb;
using dvbapiNet.Dvb.Crypto;
using dvbapiNet.Log;
using dvbapiNet.Log.Locale;
using dvbapiNet.Oscam.InterCom;
using dvbapiNet.Utils;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Runtime.InteropServices;

namespace dvbapiNet.Oscam
{
    /// <summary>
    /// Stellt den Plugin-Client dar, der auf den internen Dvbapi-Server verbindet, der wiederrum die Daten an Oscam sendet.
    /// </summary>
    public class DvbApiAdapter : IDisposable
    {
        private const string cLogSection = "dvbapiadapter";
        private const string cConfigSection = "dvbapi";
        private static DvbApiClient _ApiClient;
        private int _AdapterIndex;
        private int _ServiceId;

        private bool _DumpStream;

        /// <summary>
        /// array mit 0x2000 bytes, als Kenner ob für descrambling freigeschaltet.
        /// </summary>
        private byte[] _DescramblePids;

        /// <summary>
        /// Gesamtliste zum Direktzugriff über Index von Oscam
        /// </summary>
        private Descrambler[] _Descramblers;

        /// <summary>
        /// Liste mit allen Descramblern zwecks durchiterieren und Abschließen der Decoding-Batches.
        /// </summary>
        private List<Descrambler> _DescramblersList;

        private byte[] _FilteredPids;
        private List<DemuxFilter> _Filters;
        private InterComEndPoint _IcEp;
        private PatSection _Pat;
        private PmtSection _Pmt;
        private SdtSection _Sdt;
        private int _PmtPid;
        private int _NetworkId;
        private int _TransportStreamId;

        private PatSection _TmpPat;
        private PmtSection _TmpPmt;
        private SdtSection _TmpSdt;

        private FileStream _DumpFileStream;

        public delegate void PidRequest(DvbApiAdapter sender, ushort pid);

        public delegate void GotCw(byte[] cw, DescramblerParity parity);

        public event PidRequest AddPidRequested;

        public event PidRequest DelPidRequested;

        public event GotCw GotControlWord;

        private bool _UseMdApi;

        public DvbApiClient ApiClient
        {
            get
            {
                return _ApiClient;
            }
        }

        public bool HasDvbApiClient
        {
            get
            {
                return _ApiClient != null;
            }
        }

        public bool IsTuned
        {
            get
            {
                return _ServiceId > 0;
            }
        }

        public int CurrentService
        {
            get
            {
                if (IsTuned)
                    return _ServiceId;

                return -1;
            }
        }

        public int CurrentNetwork
        {
            get
            {
                if (IsTuned)
                    return _NetworkId;

                return -1;
            }
        }

        public int CurrentTransponder
        {
            get
            {
                if (IsTuned)
                    return _TransportStreamId;

                return -1;
            }
        }

        public int CurrentPmtPid
        {
            get
            {
                if (IsTuned)
                    return _PmtPid;

                return -1;
            }
        }

        /// <summary>
        /// Instantiiert einen neuen Adapter
        /// </summary>
        /// <param name="pipeName">Pipename über die der Adapter die Einstellungen zum ApiClient bezieht.</param>
        /// <param name="isMdapi">Wenn der Adapter selbst keine Entschlüsselung vornehmen soll, true, anderenfalls false.</param>
        public DvbApiAdapter(string pipeName, bool isMdapi)
        {
            _FilteredPids = new byte[0x2000]; // index = pid 0 = nicht filtern, 1 = filtern.
            _DescramblePids = new byte[0x2000]; // gleiche hier, wert jedoch descrambler-Index.

            _Descramblers = new Descrambler[256];
            _DescramblersList = new List<Descrambler>();

            _Filters = new List<DemuxFilter>();
            _ServiceId = -1;
            _PmtPid = -1;
            _AdapterIndex = -1;

            _UseMdApi = isMdapi;
            _DumpStream = false;

            Globals.Config.Get("debug", "streamdump", ref _DumpStream);

            try
            {
                string srv = "";
                int port = 0;
                bool old = false;

                int adapterOffset = 0;

                Globals.Config.Get(cConfigSection, "server", ref srv);
                Globals.Config.Get(cConfigSection, "oldproto", ref old);

                if (Globals.Config.Get(cConfigSection, "offset", 0, 128, ref adapterOffset) != Configuration.ConfigRes.Ok)
                    LogProvider.Add(DebugLevel.Error, cLogSection, Message.AdapterConfigInvalidOffset);

                if (Globals.Config.Get(cConfigSection, "port", 1, 65535, ref port) != Configuration.ConfigRes.Ok)
                    LogProvider.Add(DebugLevel.Error, cLogSection, Message.AdapterConfigInvalidPort);

                _ApiClient = new DvbApiClient(srv, port, pipeName, Globals.Info, old, adapterOffset);
                _ApiClient.Start();
            }
            catch (AlreadyRunningException)
            {
                // ...wenn fehlgeschlagen, weil pipe schon existent,
                // dann war eine andere Plugin-Instanz schneller, wird weiter nicht benötigt.
                LogProvider.Add(DebugLevel.Info, cLogSection, Message.AdapterClientRunning);
            }

            _IcEp = new InterComClient(pipeName);

            _IcEp.GotCommand += IcCommand;
            _IcEp.Disconnected += IcDisconnected;

            _IcEp.Start();
        }

        public void Dispose()
        {
            try
            {
                if (_ApiClient != null)
                    _ApiClient.Dispose();
            }
            catch { }

            _IcEp.Dispose();
            ClearDescramblers();
        }

        private void ClearDescramblers()
        {
            Array.Clear(_DescramblePids, 0, _DescramblePids.Length);

            lock (_DescramblersList)
            {
                foreach (Descrambler d in _DescramblersList)
                {
                    _Descramblers[d.Index] = null;
                    d.Dispose();
                }

                _DescramblersList.Clear();
            }
        }

        /// <summary>
        /// Verarbeitet den Rohen TS-Datenstrom
        /// </summary>
        /// <param name="buf">Pointer auf das Syncbyte des ersten Packets</param>
        /// <param name="len">Länge des Puffers, muss ein Vielfaches von 188 sein.</param>
        public void ProcessRawTs(IntPtr buf, int len)
        {
            int count = len / 188;
            int i, offset, pid;
            bool scr;

            if (_DumpFileStream != null)
            {
                try
                {
                    byte[] b = new byte[len];
                    Marshal.Copy(buf, b, 0, len);

                    lock (_DumpFileStream)
                    {
                        _DumpFileStream.Write(b, 0, len);
                    }
                }
                catch (Exception ex)
                {
                    LogProvider.Exception(cLogSection, Message.AdapterDumpFailed, ex);
                }
            }

            for (i = 0, offset = 0; i < count; i++, offset += 188)
            {
                pid = Marshal.ReadInt32(buf, offset); // int in Host-Byte-Order gelesen, pid & 0xff gibt sync-byte
                scr = (pid & 0xc0000000) != 0; // scrambled ?
                pid = (pid >> 16 & 0x00ff) | (pid & 0x1f00);

                // Aus Performance-Gründen wird hier mit Array-Direktzugriff gearbeitet.
                // über die PID wird der Descrambler-Index geholt und damit der descrambler der die Pid
                // entschlüsseln kann. Dies veringert die CPU-Last zum Teil erheblich - sofern mehrere Packets pro Aufruf entschlüsselt werden.
                if (_DescramblePids[pid] > 0 && scr)
                {
                    int di = _DescramblePids[pid];
                    Descrambler descr = _Descramblers[di - 1];
                    if (descr != null)
                    {
                        descr.AddToBatch(buf + offset);
                    }
                }

                // Gleiche hier mit der Verarbeitung der anderen Types, PMT, PAT, etc.
                if (_FilteredPids[pid] > 0)
                {
                    try
                    {
                        ProcessTsPacket(buf + offset, pid);
                    }
                    catch (Exception ex)
                    {
                        LogProvider.Exception(cLogSection, Message.AdapterProcessFailed, ex);
                    }
                }
            }

            // Alle descrambler durchfahren und "Decrypt-Button" drücken.
            lock (_DescramblersList)
            {
                for (i = 0; i < _DescramblersList.Count; i++)
                {
                    Descrambler descr = _DescramblersList[i];
                    descr.DescrambleBatch();
                }
            }
        }

        public void Tune(int sid, int pmt, int tsId, int nId)
        {
            if (sid == -1)
            {
                if (_ServiceId == -1) // dann bereits untuned.
                    return;

                LogProvider.Add(DebugLevel.Info, cLogSection, Message.AdapterEvUntune);

                lock (_Filters)
                    _Filters.Clear();

                DelAllPids();
                IcStop();
                _Pmt = null;
                _Pat = null;
                _Sdt = null;
                _TmpPmt = null;
                _TmpPat = null;
                _TmpSdt = null;

                try
                {
                    if (_DumpFileStream != null)
                    {
                        lock (_DumpFileStream)
                        {
                            _DumpFileStream.Close();
                            _DumpFileStream = null;
                        }
                    }
                }
                catch (Exception ex)
                {
                    LogProvider.Exception(cLogSection, Message.AdapterDumpStopFailed, ex);
                }

                //Pids zurücksetzen:
                ClearDescramblers();
            }
            else
            {
                if (_ServiceId != -1 || _PmtPid != -1) // dann fehlte wohl untune/disable tuner
                    Tune(-1, -1, -1, -1); // reset.

                LogProvider.Add(DebugLevel.Info, cLogSection, Message.AdapterEvTune, sid, pmt, tsId, nId);

                try
                {
                    if (_DumpStream)
                    {
                        string filename = DateTime.Now.ToString("yyyyMMddHHmmss");
                        filename = $"tsdump-{_AdapterIndex}-{filename}-{sid}-{tsId}.ts";

                        filename = Path.Combine(Globals.HomeDirectory.FullName, filename);

                        _DumpFileStream = new FileStream(filename, FileMode.Create);
                    }
                }
                catch (Exception ex)
                {
                    LogProvider.Exception(cLogSection, Message.AdapterDumpStartFailed, ex);
                }

                _TmpPat = new PatSection();
                AddPid(0x0000); // PAT holen um PMT Pid zu bestimmen über sid falls PMT fehlt oder falsch.

                if (pmt > 0)
                {
                    _TmpPmt = new PmtSection(pmt);
                    AddPid(pmt);
                }

                if (nId < 0)
                {
                    _TmpSdt = new SdtSection(true);
                    AddPid(0x0011);
                }
            }

            _ServiceId = sid;
            _PmtPid = pmt;
            _TransportStreamId = tsId;
            _NetworkId = nId;
        }

        private void AddPid(int pid)
        {
            if (pid < 0 || pid > 0x1fff)
                return;

            if (_FilteredPids[pid] == 0)
            {
                AddPidRequested?.Invoke(this, (ushort)pid);
                _FilteredPids[pid] = 1;
            }
        }

        private void DelAllPids()
        {
            for (int i = 0; i < _FilteredPids.Length; i++)
            {
                if (_FilteredPids[i] != 0)
                    DelPidRequested?.Invoke(this, (ushort)i);

                _FilteredPids[i] = 0;
            }
        }

        private bool DelFilter(int num, int pid)
        {
            DemuxFilter f = null;
            lock (_Filters)
            {
                foreach (DemuxFilter flt in _Filters)
                {
                    if (flt.Number == num && flt.Pid == pid)
                    {
                        f = flt;

                        break;
                    }
                }

                if (f != null)
                {
                    _Filters.Remove(f);
                    return true;
                }
                return false;
            }
        }

        private void DelPid(int pid)
        {
            if (pid < 0 || pid > 0x1fff)
                return;

            lock (_Filters)
            {
                // prüfen ob noch im filter:
                foreach (DemuxFilter f in _Filters)
                {
                    if (f.Pid == pid)
                        return;
                }
            }

            if (_FilteredPids[pid] != 0)
            {
                DelPidRequested?.Invoke(this, (ushort)pid);
                _FilteredPids[pid] = 0;
            }
        }

        private Descrambler GetDescrambler(int index)
        {
            Descrambler descr = _Descramblers[index];

            if (descr == null)
            {
                descr = new Descrambler();
                descr.Index = index;

                _Descramblers[index] = descr;

                lock (_DescramblersList)
                    _DescramblersList.Add(descr);
            }

            return descr;
        }

        private void IcCommand(InterComEndPoint sender, InterComCommand cmd, byte[] data)
        {
            LogProvider.Add(DebugLevel.InterComClientCommand, cLogSection, Message.AdapterIcCommand, cmd);

            try
            {
                using (MemoryStream ms = new MemoryStream(data))
                using (BinaryReader rdr = new BinaryReader(ms))
                {
                    switch (cmd)
                    {
                        case InterComCommand.SetInstanceNumber:
                            IcCommandSetInstanceNumber(rdr);
                            break;

                        case InterComCommand.Stop:
                            // entfernt. Soll einfach weiterlaufen, z.b. bei oscam Restart
                            break;

                        case InterComCommand.SetFilter:
                            IcCommandSetFilter(rdr);
                            break;

                        case InterComCommand.DelFilter:
                            IcCommandDelFilter(rdr);
                            break;

                        case InterComCommand.SetPid:
                            SetRawPid(rdr.ReadInt32(), rdr.ReadByte());
                            break;

                        case InterComCommand.ClearFilter:
                            IcCommandClearFilter(rdr);
                            break;

                        case InterComCommand.SetDescramblerMode:
                            IcCommandSetDescramblerMode(rdr);
                            break;

                        case InterComCommand.SetDescramblerData:
                            IcCommandSetDescrambleData(rdr, data.Length);
                            break;

                        default:
                            LogProvider.Add(DebugLevel.Warning, cLogSection, Message.AdapterIcCommandUnknown, cmd);
                            // unbekannter befehl. Sollte nicht vorkommen.
                            // Da aber bereits alle Bytes gelesen, ignorieren und weiter im Programm.
                            break;
                    }
                }
            }
            catch (Exception ex)
            {
                LogProvider.Exception(cLogSection, Message.AdapterIcCommandFailed, ex);
            }
        }

        private void IcCommandClearFilter(BinaryReader rdr)
        {
            lock (_Filters)
                _Filters.Clear();

            DelAllPids();
        }

        private void IcCommandDelFilter(BinaryReader rdr)
        {
            int num = rdr.ReadInt32();
            int pid = rdr.ReadInt32();

            LogProvider.Add(DebugLevel.InterComClientCommand, cLogSection, Message.AdapterDelFilter, num, pid);

            DelFilter(num, pid);

            // muss _nach_ Remove ausgeführt werden, prüft nämlich ob noch filter vorhanden sind.
            if (pid != _PmtPid)
                DelPid(pid);
        }

        private void IcCommandSetDescrambleData(BinaryReader rdr, int dLen)
        {
            int index = rdr.ReadInt32();
            DescramblerParity parity = (DescramblerParity)rdr.ReadInt32();
            DescramblerDataType dType = (DescramblerDataType)rdr.ReadInt32();

            byte[] data = rdr.ReadBytes((int)(dLen - rdr.BaseStream.Position));

            if (dType == DescramblerDataType.Key)
                GotControlWord?.Invoke(data, parity);

            if (_UseMdApi)
                return;

            Descrambler descr = GetDescrambler(index);

            descr.SetDescramblerData(parity, dType, data);
        }

        private void IcCommandSetDescramblerMode(BinaryReader rdr)
        {
            int index = rdr.ReadInt32();
            DescramblerAlgo algo = (DescramblerAlgo)rdr.ReadInt32();
            DescramblerMode mode = (DescramblerMode)rdr.ReadInt32();

            if (_UseMdApi)
            {
                if (algo != DescramblerAlgo.DvbCsa)
                {
                    LogProvider.Add(DebugLevel.Warning, cLogSection, Message.AdapterMdapiMode);
                }
                return;
            }

            Descrambler descr = GetDescrambler(index);

            descr.SetDescramblerMode(algo, mode);
        }

        private void IcCommandSetFilter(BinaryReader rdr)
        {
            int filter = rdr.ReadInt32();
            int pid = rdr.ReadInt32();
            int timeout = rdr.ReadInt32();
            DemuxFilter.Flags flags = (DemuxFilter.Flags)rdr.ReadInt32();
            byte[] fData = rdr.ReadBytes(16);
            byte[] fMask = rdr.ReadBytes(16);
            byte[] fMode = rdr.ReadBytes(16);

            // falls es den schon gibt rauskicken:
            DelFilter(filter, pid);

            LogProvider.Add(DebugLevel.InterComClientCommand, cLogSection, Message.AdapterSetFilter, filter, pid);
            LogProvider.Add(DebugLevel.InterComClientCommand | DebugLevel.HexDump, cLogSection, Message.AdapterSetFilterDump, fData, 0, fData.Length);
            LogProvider.Add(DebugLevel.InterComClientCommand | DebugLevel.HexDump, cLogSection, Message.Empty, fMask, 0, fMask.Length);
            LogProvider.Add(DebugLevel.InterComClientCommand | DebugLevel.HexDump, cLogSection, Message.Empty, fMode, 0, fMode.Length);

            lock (_Filters)
                _Filters.Add(new DemuxFilter(filter, pid, fData, fMask, fMode, timeout, flags));

            // prüfen ob PMT-Filter, dann PMT direkt rausschicken:
            if (pid == _PmtPid && _Pmt != null)
            {
                IcFilterData(filter, _Pmt.Data, _Pmt.SectionSize);
            }

            AddPid(pid);
        }

        private void IcCommandSetInstanceNumber(BinaryReader rdr)
        {
            _AdapterIndex = rdr.ReadInt32();

            LogProvider.SetInstanceNumber(_AdapterIndex);
        }

        private void IcDisconnected(InterComEndPoint sender)
        {
            _AdapterIndex = -1; // verhindert, dass noch etwas durchgeschickt wird.
        }

        /// <summary>
        /// Sendet aus dem TS gefilterte Sections an den DVBApi-Client.
        /// </summary>
        /// <param name="filterNumber">Filternummer</param>
        /// <param name="data">Section-Daten</param>
        private void IcFilterData(int filterNumber, byte[] data, int len)
        {
            if (_AdapterIndex < 0)
                return;

            using (MemoryStream ms = new MemoryStream())
            using (BinaryWriter w = new BinaryWriter(ms))
            {
                w.Write(filterNumber);
                w.Write(data, 0, len);

                w.Flush();
                _IcEp.SendCommand(InterComCommand.FilterData, ms.ToArray());
            }
        }

        /// <summary>
        /// Sendet gegebene Section durch die Pipe.
        /// </summary>
        /// <param name="sec"></param>
        private void IcPmt(PmtSection pmt)
        {
            if (_AdapterIndex < 0)
                return;

            using (MemoryStream ms = new MemoryStream())
            using (BinaryWriter w = new BinaryWriter(ms))
            {
                w.Write(_NetworkId);
                w.Write(_TransportStreamId);
                w.Write(_PmtPid);
                w.Flush();

                pmt.WriteToStream(ms);

                _IcEp.SendCommand(InterComCommand.Pmt, ms.ToArray());
            }
        }

        private void IcStop()
        {
            if (_AdapterIndex < 0)
                return;

            _IcEp.SendCommand(InterComCommand.Stop);
        }

        /// <summary>
        /// Bearbeitet TS-Packets für PAT, CAT, SDT, PMT, ECM und EMM
        /// </summary>
        /// <param name="tsPacket"></param>
        private void ProcessTsPacket(IntPtr tsPacket, int pid)
        {
            if (pid == 0x0000) // PAT
            {
                if (_TmpPat != null && _TmpPat.AddPacket(tsPacket))
                {
                    _Pat = _TmpPat;
                    _TmpPat = null;

                    int pmtPid = _Pat.GetPmtPidBySid(_ServiceId);

                    if (_TransportStreamId != -1 && _TransportStreamId != 0xffff && _Pat.TransportStreamId != _TransportStreamId)
                        LogProvider.Add(DebugLevel.Warning, cLogSection, Message.AdapterWrongPat, _Pat.TransportStreamId, _TransportStreamId);

                    if (pmtPid == -1)
                    {
                        LogProvider.Add(DebugLevel.Warning, cLogSection, Message.AdapterServiceNotFound, _Pat.TransportStreamId, _ServiceId);
                        _TmpPat = new PatSection();
                    }
                    else if (_PmtPid != pmtPid) // PmtPid beim Tune Falsch
                    {
                        LogProvider.Add(DebugLevel.Info, cLogSection, Message.AdapterRetuneOnPat);
                        Tune(_ServiceId, pmtPid, _TransportStreamId, _NetworkId); // Retune mit richtigen Daten.
                    }
                    else
                    {
                        LogProvider.Add(DebugLevel.Info, cLogSection, Message.AdapterPatDone);
                        DelPid(0);
                    }
                }
            }
            else if (pid == 0x0011) // SDT
            {
                if (_TmpSdt != null && _TmpSdt.AddPacket(tsPacket))
                {
                    _Sdt = _TmpSdt;
                    _TmpSdt = null;

                    if (_TransportStreamId != -1 && _TransportStreamId != 0xffff)
                    {
                        if (_Sdt.TransportStreamId != _TransportStreamId)
                        {
                            LogProvider.Add(DebugLevel.Warning, cLogSection, Message.AdapterWrongSdt, _Sdt.TransportStreamId, _TransportStreamId);
                            _TmpSdt = new SdtSection(true);
                        }
                        else if (_NetworkId != _Sdt.OriginalNetworkId)
                        {
                            LogProvider.Add(DebugLevel.Info, cLogSection, Message.AdapterRetuneOnSdt);
                            Tune(_ServiceId, _PmtPid, _TransportStreamId, _Sdt.OriginalNetworkId); // Retune mit richtigen Daten.
                        }
                        else
                        {
                            LogProvider.Add(DebugLevel.Info, cLogSection, Message.AdapterSdtDone);
                            DelPid(0x0011);
                        }
                    }
                    else
                    {
                        LogProvider.Add(DebugLevel.Warning, cLogSection, Message.AdapterSdtDoneNoTsId);
                    }
                }
            }
            else if (pid == _PmtPid)
            {
                if (_TmpPmt != null && _TmpPmt.AddPacket(tsPacket))
                {
                    if (_TmpPmt.ServiceId != _ServiceId)
                    {
                        LogProvider.Add(DebugLevel.Warning, cLogSection, Message.AdapterWrongPmt, _TmpPmt.ServiceId);
                        if (_Pmt != null)
                        {
                            LogProvider.Add(DebugLevel.Warning, cLogSection, Message.AdapterStopPmtTracking);
                            DelPid(_PmtPid);
                            _TmpPmt = null;
                        }
                    }
                    else
                    {
                        if (_AdapterIndex != -1 && (_Pmt == null || (_Pmt != null && _Pmt.Version != _TmpPmt.Version))) // dann pipe verbunden.
                        {
                            IcPmt(_TmpPmt);

                            LogProvider.Add(DebugLevel.Info, cLogSection, Message.AdapterPmtDone, _PmtPid, _ServiceId, _TmpPmt.Version);
                            if (_Pmt != null)
                            {
                                LogProvider.Add(DebugLevel.Info, cLogSection, Message.AdapterPmtVer, _Pmt.Version);
                            }
                        }
                        _Pmt = _TmpPmt;
                        _TmpPmt = new PmtSection(_PmtPid);
                    }
                }
            }

            // Filter durchlaufen, und checken ob hier was bei ist, was an oscam geht.
            lock (_Filters)
            {
                foreach (DemuxFilter f in _Filters)
                {
                    if (f.Pid == pid && f.AddPacket(tsPacket))
                    {
                        byte[][] filtered = f.GetFilteredSections();

                        foreach (byte[] data in filtered)
                        {
                            IcFilterData(f.Number, data, data.Length);
                            LogProvider.Add(DebugLevel.Info, cLogSection, Message.AdapterDataFiltered, f.Number, f.Pid);
                            LogProvider.Add(DebugLevel.Info | DebugLevel.HexDump, cLogSection, Message.Empty, data, 0, data.Length < 18 ? data.Length : 18);
                        }
                        f.Reset();
                    }
                }
            }
        }

        private void SetRawPid(int pid, byte index)
        {
            if (_UseMdApi)
                return;

            if (index > 0 && _Descramblers[index - 1] == null)
            {
                GetDescrambler(index - 1); // legt zugleich Descrambler an, falls nicht vorhanden.
            }

            _DescramblePids[pid] = index;
        }
    }
}
