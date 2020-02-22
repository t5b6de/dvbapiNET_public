using dvbapiNet.Dvb.Crypto;
using dvbapiNet.Log;
using dvbapiNet.Log.Locale;
using dvbapiNet.Oscam.InterCom;
using dvbapiNet.Oscam.Packets;
using dvbapiNet.Utils;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Threading;
using static dvbapiNet.Oscam.CaPmtSection;

namespace dvbapiNet.Oscam
{
    /// <summary>
    /// Stellt den Client zum Oscam-Server dar, und den Internen Server auf dem sich die Plugin-Instanzen verbinden.
    /// Diese Klasse kann nur ein einziges Mal Instantiiert werden, da diese eine named-Pipe auf macht.
    /// Erfordert mindestens oscam svn r11520 (Implementierung DemuxDevice Descriptor in CAPMT)
    /// </summary>
    public class DvbApiClient : IDisposable
    {
        private const string cLogSection = "dvbapiclient";
        private const byte cMsgStart = 0xa5;

        private const int cOldProto = 2;

        private const int cSuppProto = 3;

        private Socket _ApiSckt;

        private ChannelSession[] _ChanSessions;

        private DvbApiClientInfo _ClientInfo;

        private Thread _ConnectionWatcher;

        /// <summary>
        /// Hostname des Oscam DVBApi-Servers
        /// </summary>
        private string _Host;

        private InterComServer _IcServer;
        private bool _IsConnected;
        private bool _IsRunning;

        // DVBAPI Protocol Version
        // DVBAPI Alte Protocol Version
        private bool _LimitOldProtocol;

        private uint _MsgId = 0;
        private string _PipeName;
        private int _Port;
        private byte[] _RecvBuff;

        private DvbApiServerInfo _ServerInfo;
        private int _UseProto;
        private int _AdapterOffset;

        public DvbApiClient(string host, int port, string pipeName, string clientInfo, bool useOldProcol, int adapterOffset)
        {
            _UseProto = 0;
            _IsRunning = true;
            _LimitOldProtocol = useOldProcol;
            _PipeName = pipeName;
            _AdapterOffset = adapterOffset;

            _IsConnected = false;

            _Host = host;
            _Port = port;

            try
            {
                _IcServer = new InterComServer(_PipeName);
                _IcServer.NewInstanceGenerated += AddChannelSession;
            }
            catch (IOException ex)
            {
                uint res = (uint)Marshal.GetHRForException(ex);
                if (res == 0x800700e7) // -> Pipe-Fehler bereits belegt, also schon gestartet.
                {
                    // Dient als Blocker, einmal angelegt, damit kein weiterer dvbapi client gestartet wird
                    throw new AlreadyRunningException("Oscam DVBAPI Client bereits gestartet. Plugin läuft als Demux-Instanz.", ex);
                }

                throw ex;
            }

            if (string.IsNullOrWhiteSpace(_Host))
                throw new ArgumentNullException("host");

            if (port < 1 || port > 65535)
                throw new ArgumentOutOfRangeException("Wert außerhalb gültigen Bereichs (1-65535)", "port");

            _ClientInfo = new DvbApiClientInfo(_LimitOldProtocol ? cOldProto : cSuppProto, clientInfo);
            _RecvBuff = new byte[4096];

            _ChanSessions = new ChannelSession[32]; // max 32 instanzen.
        }

        public void Dispose()
        {
            _IsRunning = false;
            Reset();
            try
            {
                _ApiSckt.Dispose();
            }
            catch { }

            _IcServer.Dispose();
        }

        public void Start()
        {
            if (_ConnectionWatcher == null)
            {
                _ConnectionWatcher = new Thread(WatchConnection);
                _ConnectionWatcher.Start();
            }
        }

        private ChannelSession[] ActiveChannels()
        {
            List<ChannelSession> active = new List<ChannelSession>();
            int i;

            for (i = 0; i < _ChanSessions.Length; i++)
            {
                if (_ChanSessions[i] != null && _ChanSessions[i].CurrentSid > 0)
                    active.Add(_ChanSessions[i]);
            }

            return active.ToArray();
        }

        private void AddChannelSession(InterComEndPoint ep)
        {
            ChannelSession sess = null;
            lock (this)
            {
                for (int i = 0; i < _ChanSessions.Length; i++)
                {
                    if (_ChanSessions[i] == null)
                    {
                        sess = new ChannelSession(i + _AdapterOffset, ep);
                        _ChanSessions[i] = sess;
                        break;
                    }
                }
            }

            if (sess != null)
            {
                sess.ChannelUpdated += ChannelUpdated;
                sess.FilterData += SendFilterdata;
                sess.Disconnected += ChannelDisconnected;
                sess.Start();
            }
            else // keine ID mehr frei, max instanzen erreicht, trennen.
            {
                ep.DisconnectIntercom();
            }
        }

        /// <summary>
        /// Bearbeitet Disconnect-Event von Channel Session, und gibt ID wieder frei.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="na"></param>
        private void ChannelDisconnected(ChannelSession sender, bool na)
        {
            if (sender.AdapterId == -1) // dann bereits getrennt aber möglicherweise noch aktionen aktiv.
                return;

            _ChanSessions[sender.AdapterId - _AdapterOffset] = null;
        }

        private void ChannelUpdated(ChannelSession sender, bool add)
        {
            CaPmtSection capmt = null;

            if (!add)
            {
                if (sender.CurrentSid > 0) // dann Update.
                {
                    capmt = sender.GetCaPmt(ListManagement.Update);
                }
                else
                {
                    // dann ist raus, also remove. hier bietet Oscam dvbapi ein stop command
                    StopDmx((byte)sender.AdapterId);
                }
            }
            else
            {
                // wenn noch keiner läuft dann als only senden. ansonsten add
                int c = ActiveChannels().Length;

                if (c > 1) // eigenen abziehen
                {
                    capmt = sender.GetCaPmt(ListManagement.Add);
                }
                else
                {
                    capmt = sender.GetCaPmt(ListManagement.Only);
                }
            }

            if (capmt != null)
                SendCaPmt(capmt);
        }

        private void CmdCaSetCaDescr(BinaryReader r)
        {
            int adapter = r.ReadByte();
            int index = IPAddress.NetworkToHostOrder(r.ReadInt32());
            DescramblerParity parity = (DescramblerParity)IPAddress.NetworkToHostOrder(r.ReadInt32());
            byte[] cw = r.ReadBytes(8);

            LogProvider.Add(DebugLevel.DvbApi, cLogSection, Message.DvbapiSetCaDescr, adapter, index, parity);
            LogProvider.Add(DebugLevel.DvbApi | DebugLevel.HexDump, cLogSection, Message.DvbapiHexCw, cw, 0, cw.Length);

            ChannelSession c = _ChanSessions[adapter - _AdapterOffset];

            if (c != null)
            {
                c.SetDecramblerData(index, parity, DescramblerDataType.Key, cw);
            }
            else
            {
                LogProvider.Add(DebugLevel.Warning, cLogSection, Message.DvbapiCwAdapterNotFound, adapter);
            }
        }

        private void CmdCaSetDescrData(BinaryReader r)
        {
            int adapter = r.ReadByte();
            int index = IPAddress.NetworkToHostOrder(r.ReadInt32());
            DescramblerParity parity = (DescramblerParity)IPAddress.NetworkToHostOrder(r.ReadInt32());
            DescramblerDataType dType = (DescramblerDataType)IPAddress.NetworkToHostOrder(r.ReadInt32());
            int dLen = IPAddress.NetworkToHostOrder(r.ReadInt32());

            byte[] data = r.ReadBytes(dLen);

            ChannelSession c = _ChanSessions[adapter - _AdapterOffset];

            if (c != null)
            {
                c.SetDecramblerData(index, parity, dType, data);
            }
            else
            {
                LogProvider.Add(DebugLevel.Warning, cLogSection, Message.DvbapiDataAdapterNotFound, adapter);
            }
        }

        private void CmdCaSetPid(BinaryReader r)
        {
            int adapter = r.ReadByte();
            int pid = IPAddress.NetworkToHostOrder(r.ReadInt32());
            int idx = IPAddress.NetworkToHostOrder(r.ReadInt32());

            LogProvider.Add(DebugLevel.DvbApi, cLogSection, Message.DvbapiCaSetPid, adapter, pid, idx);

            ChannelSession c = _ChanSessions[adapter - _AdapterOffset];

            if (c != null)
                c.SetPid(pid, (byte)(idx + 1));
        }

        private void CmdDmxSetFilter(BinaryReader r)
        {
            int adapter = r.ReadByte();
            int demux = r.ReadByte();
            int filter = r.ReadByte();
            int pid = IPAddress.NetworkToHostOrder(r.ReadInt16());
            byte[] fData = r.ReadBytes(3 * 16);
            int timeout = IPAddress.NetworkToHostOrder(r.ReadInt32());
            int flags = IPAddress.NetworkToHostOrder(r.ReadInt32());

            ChannelSession c = _ChanSessions[adapter - _AdapterOffset];

            if (c != null)
            {
                c.SetFilter(demux, filter, pid, timeout, flags, fData);
                LogProvider.Add(DebugLevel.DvbApi, cLogSection, Message.DvbapiSetFilter, demux, filter, pid);
            }
            else
            {
                LogProvider.Add(DebugLevel.Warning, cLogSection, Message.DvbapiSetFilterFailed, adapter);
            }
        }

        private void CmdDmxStop(BinaryReader r)
        {
            int adapter = r.ReadByte();
            int demux = r.ReadByte();
            int filter = r.ReadByte();
            int pid = IPAddress.NetworkToHostOrder(r.ReadInt16());
            ChannelSession c;

            if (demux == 255)
            {
                LogProvider.Add(DebugLevel.DvbApi, cLogSection, Message.DvbapiStopFilterAll);
                for (int i = 0; i < _ChanSessions.Length; i++)
                {
                    c = _ChanSessions[i];

                    if (c != null)
                    {
                        c.ClearFilter();
                    }
                }
            }
            else
            {
                LogProvider.Add(DebugLevel.DvbApi, cLogSection, Message.DvbapiStopFilter, adapter, demux, filter, pid);

                c = _ChanSessions[adapter - _AdapterOffset];

                if (c != null && c.DvbApiDemuxIndex == demux)
                {
                    c.StopFilter(filter, pid);
                }
                else
                {
                    LogProvider.Add(DebugLevel.Warning, cLogSection, Message.DvbapiStopFilterFailed, adapter, demux);
                }
            }
        }

        private void CmdEcmInfo(BinaryReader rdr)
        {
            int adapter = rdr.ReadByte();
            EcmInfo info = EcmInfo.GetFromBinaryReader(rdr);

            LogProvider.Add(DebugLevel.EcmInfo, cLogSection, info);

            ChannelSession c = _ChanSessions[adapter - _AdapterOffset];

            if (c != null)
            {
                c.SetEcmInfo(info);
            }
            else
            {
                LogProvider.Add(DebugLevel.Warning, cLogSection, Message.DvbapiEcmInfoFailed, adapter);
            }
        }

        private void CmdServerInfo(BinaryReader r)
        {
            int proto = IPAddress.NetworkToHostOrder(r.ReadInt16());
            string info = Statics.GetStringFromC(3, _RecvBuff, _RecvBuff[2]);

            LogProvider.Add(DebugLevel.Info, cLogSection, Message.DvbapiServerInfo, info, proto);

            _ServerInfo = new DvbApiServerInfo(proto, info);
            _UseProto = _ServerInfo.ProtocolVersion; // hier wissen wir sicher.

            // Oscams eigenartiges, über die Zeit inkonsistentes Vorgehen zwingt uns zu dieser Prüfung:
            if (_UseProto > (_LimitOldProtocol ? cOldProto : cSuppProto))
            {
                _UseProto = (_LimitOldProtocol ? cOldProto : cSuppProto);
            }

            SendCaPmtList();
        }

        private void CmdSetCaDescrMode(BinaryReader r)
        {
            int adapter = r.ReadByte();
            int idx = IPAddress.NetworkToHostOrder(r.ReadInt32());
            DescramblerAlgo algo = (DescramblerAlgo)IPAddress.NetworkToHostOrder(r.ReadInt32());
            DescramblerMode mode = (DescramblerMode)IPAddress.NetworkToHostOrder(r.ReadInt32());

            LogProvider.Add(DebugLevel.DvbApi, cLogSection, Message.DvbapiSetMode, adapter, idx, algo, mode);

            ChannelSession c = _ChanSessions[adapter - _AdapterOffset];

            if (c != null)
                c.SetDescramblerMode(idx, algo, mode);
        }

        private void Connect()
        {
            if (_IsConnected || !_IsRunning)
                return;

            _IsConnected = true;

            LogProvider.Add(DebugLevel.DvbApi, cLogSection, Message.DvbapiConnect);

            IPAddress ip = null;

            if (!IPAddress.TryParse(_Host, out ip))
            {
                try
                {
                    IPHostEntry ihe = Dns.GetHostEntry(_Host); // wirft Ausnahme wenn Host nicht gefunden.

                    if (ihe.AddressList.Length > 0)
                    {
                        ip = ihe.AddressList[0];
                    }
                }
                catch { }
            }

            if (ip == null)
            {
                _IsConnected = false;
                LogProvider.Add(DebugLevel.Error, cLogSection, Message.DvbapiInvalidHost, _Host);
                return;
            }

            EndPoint srv = new IPEndPoint(ip, _Port);

            _ApiSckt = new Socket(ip.AddressFamily, SocketType.Stream, ProtocolType.Tcp);
            _ApiSckt.Connect(srv);
            _ApiSckt.NoDelay = true; // im Hinterkopf halten falls aussetzer

            LogProvider.Add(DebugLevel.DvbApi, cLogSection, Message.DvbapiConnected);

            SendMessage(_ClientInfo.GetData(), true);

            _ApiSckt.BeginReceive(_RecvBuff, 0, 1, SocketFlags.None, Receive, _ApiSckt); // msgstart empfangen.
        }

        private void HandleApiCommand(DvbApiCommand cmd, Socket s)
        {
            LogProvider.Add(DebugLevel.DvbApi, cLogSection, Message.DvbapiCommand, cmd);

            using (MemoryStream ms = new MemoryStream(_RecvBuff))
            using (BinaryReader rdr = new BinaryReader(ms))
            {
                switch (cmd)
                {
                    case DvbApiCommand.ServerInfo:
                        if (!NetUtils.ReceiveAll(_RecvBuff, s, 3))
                        {
                            Reset();
                            return;
                        }

                        if (!NetUtils.ReceiveAll(_RecvBuff, s, _RecvBuff[2], 3))
                        {
                            Reset();
                            return;
                        }

                        CmdServerInfo(rdr);

                        break;

                    case DvbApiCommand.DmxSetFilter:
                        if (!NetUtils.ReceiveAll(_RecvBuff, s, 61)) // immer 61 byte, 65 mit opcode.
                        {
                            Reset();
                            return;
                        }

                        CmdDmxSetFilter(rdr);

                        break;

                    case DvbApiCommand.DmxStop:

                        if (!NetUtils.ReceiveAll(_RecvBuff, s, 5))
                        {
                            Reset();
                            return;
                        }

                        CmdDmxStop(rdr);

                        break;

                    case DvbApiCommand.CaSetPid:
                        if (!NetUtils.ReceiveAll(_RecvBuff, s, 9))
                        {
                            Reset();
                            return;
                        }

                        CmdCaSetPid(rdr);
                        break;

                    case DvbApiCommand.CaSetDescrMode:
                        if (!NetUtils.ReceiveAll(_RecvBuff, s, 13)) // 1 byte adapter index, dann 12 Byte ca_descr_mode struct
                        {
                            Reset();
                            return;
                        }
                        CmdSetCaDescrMode(rdr);
                        break;

                    case DvbApiCommand.CaSetDescrData:
                        /*
                            adapter 1
                            typedef struct ca_descr_data {
                                uint32_t index; 4
                                enum ca_descr_parity parity; 4
                                enum ca_descr_data_type data_type;4
                                uint32_t length; 4
                                uint8_t *data;
                            } ca_descr_data_t;
                        */

                        if (!NetUtils.ReceiveAll(_RecvBuff, s, 17)) // erste 17 byte (1 byte adapter + 16 byte teil Struct)
                        {
                            Reset();
                            return;
                        }

                        ms.Position = 13;

                        int l = IPAddress.NetworkToHostOrder(rdr.ReadInt32());

                        if (!NetUtils.ReceiveAll(_RecvBuff, s, l, (int)ms.Position))
                        {
                            Reset();
                            return;
                        }
                        rdr.BaseStream.Position = 0;

                        CmdCaSetDescrData(rdr);

                        break;

                    case DvbApiCommand.CaSetDescr:
                        if (!NetUtils.ReceiveAll(_RecvBuff, s, 17))
                        {
                            Reset();
                            return;
                        }
                        CmdCaSetCaDescr(rdr);

                        break;

                    case DvbApiCommand.EcmInfo:
                        // erste 15 byte nach Command reinholen, sind reine zahlenwerte
                        if (!NetUtils.ReceiveAll(_RecvBuff, s, 15))
                        {
                            Reset();
                            return;
                        }
                        // dann folgen 4 strings mit 1 byte länge vorangestellt.
                        int ptr = 15;
                        for (int i = 0; i < 4; i++)
                        {
                            if (!NetUtils.ReceiveAll(_RecvBuff, s, 1, ptr))
                            {
                                Reset();
                                return;
                            }

                            int strlen = _RecvBuff[ptr++];
                            if (!NetUtils.ReceiveAll(_RecvBuff, s, strlen, ptr))
                            {
                                Reset();
                                return;
                            }
                            ptr += strlen;
                        }
                        // hops empfangen:
                        if (!NetUtils.ReceiveAll(_RecvBuff, s, 1, ptr))
                        {
                            Reset();
                            return;
                        }

                        CmdEcmInfo(rdr);
                        break;

                    default:
                        LogProvider.Add(DebugLevel.Error, cLogSection, Message.DvbapiUnkownCmd);
                        break;
                }
            }
        }

        private void Receive(IAsyncResult iar)
        {
            try
            {
                if (!_IsRunning)
                    return;

                Socket s = iar.AsyncState as Socket;
                int len = s.EndReceive(iar);

                if (len < 1)
                {
                    Reset();
                    return;
                }

                if (_UseProto == 0)
                {
                    if (_RecvBuff[0] == cMsgStart) // dann wohl v3.
                    {
                        _UseProto = 3;
                    }
                    else
                    {
                        _UseProto = 2;
                        // dann rest vom Befehl empfangen:

                        len += _ApiSckt.Receive(_RecvBuff, 1, 3, SocketFlags.None);
                    }
                }

                if (_UseProto == 3)
                {
                    if (_RecvBuff[0] != cMsgStart) // dann "out of sync" resync.
                    {
                        _ApiSckt.BeginReceive(_RecvBuff, 0, 1, SocketFlags.None, Receive, s); // message start empfangen.
                        return;
                    }

                    if (!NetUtils.ReceiveAll(_RecvBuff, _ApiSckt, 8)) // 4 byte msgid und 4byte command
                    {
                        Reset();
                        return;
                    }
                }
                else
                {
                    if (len < 4)
                    {
                        Reset();
                        return;
                    }
                }

                using (MemoryStream ms = new MemoryStream(_RecvBuff))
                using (BinaryReader r = new BinaryReader(ms))
                {
                    int msgid = 0;

                    if (_UseProto == 3)
                        msgid = IPAddress.NetworkToHostOrder(r.ReadInt32());

                    uint cmd = (uint)IPAddress.NetworkToHostOrder(r.ReadInt32());

                    HandleApiCommand((DvbApiCommand)cmd, s);
                }

                _ApiSckt.BeginReceive(_RecvBuff, 0, _UseProto == 3 ? 1 : 4, SocketFlags.None, Receive, s); // message start empfangen.
            }
            catch (Exception ex)
            {
                LogProvider.Exception(cLogSection, Message.DvbapiClientError, ex);
                Reset();
            }
        }

        private void Reset()
        {
            try
            {
                _ApiSckt.Close();
            }
            catch { }

            LogProvider.Add(DebugLevel.DvbApi, cLogSection, Message.DvbapiDisconnected);

            _ServerInfo = null;
            _IsConnected = false;
            _UseProto = 0;
            // Reconnect erfolgt bei nächstem Send-Message.
        }

        private void SendCaPmt(CaPmtSection capmt)
        {
            if (_ServerInfo == null)
                return;

            byte[] data = capmt.Create();

            using (MemoryStream ms = new MemoryStream())
            {
                int len = data.Length;

                int cmd = unchecked((int)DvbApiCommand.AotCaPmt);

                if (!_IsConnected)
                    return;

                byte[] header;
                // Längengenerierung nach ASN.1:
                if (len < 128)
                {
                    header = new byte[4];
                    header[3] = (byte)len;
                }
                else if (len < 256)
                {
                    header = new byte[5];
                    header[3] = 0x81;
                    header[4] = (byte)len;
                }
                else if (len < 65536)
                {
                    header = new byte[6];
                    header[3] = 0x82;
                    header[4] = (byte)(len >> 8);
                    header[5] = (byte)(len);
                }
                else // bis 16MiB
                {
                    header = new byte[7];
                    header[3] = 0x83;
                    header[4] = (byte)(len >> 16);
                    header[5] = (byte)(len >> 8);
                    header[6] = (byte)(len);
                }

                header[0] = (byte)(cmd >> 24);
                header[1] = (byte)(cmd >> 16);
                header[2] = (byte)(cmd >> 8);

                ms.Write(header, 0, header.Length);
                ms.Write(data, 0, data.Length);
                byte[] d = ms.ToArray();
                SendMessage(ms.ToArray());
            }
        }

        /// <summary>
        /// Sendet vollständige CaPmt-Liste erneut, z.b. bei Reconnect.
        /// </summary>
        private void SendCaPmtList()
        {
            lock (this) // niemals gleichzeitig senden!
            {
                // count für Anzahl sender, bestimmt ob only oder first ... more ... last
                ChannelSession[] chans = ActiveChannels();
                CaPmtSection capmt;
                if (chans.Length == 0)
                    return;

                if (chans.Length == 1)
                {
                    capmt = chans[0].GetCaPmt(ListManagement.Only);
                    SendCaPmt(capmt);
                }
                else
                {
                    ListManagement t;
                    for (int i = 0; i < chans.Length; i++)
                    {
                        if (i == 0)
                            t = ListManagement.First;
                        else if (i == chans.Length - 1)
                            t = ListManagement.Last;
                        else
                            t = ListManagement.More;

                        capmt = chans[i].GetCaPmt(t);

                        SendCaPmt(capmt);
                    }
                }
            }
        }

        private void SendFilterdata(ChannelSession sender, int filterId, byte[] data)
        {
            if (sender.DvbApiDemuxIndex == -1)
                return;

            using (MemoryStream ms = new MemoryStream())
            using (BinaryWriter w = new BinaryWriter(ms))
            {
                w.Write(IPAddress.HostToNetworkOrder(unchecked((int)DvbApiCommand.FilterData)));
                w.Write((byte)sender.DvbApiDemuxIndex);
                w.Write((byte)filterId);
                w.Write(data);
                w.Flush();

                SendMessage(ms.ToArray());
            }
        }

        /// <summary>
        /// Sendet eine Nachricht an Oscam DVBAPI Serverr
        /// </summary>
        /// <param name="data">die zu sendenen Daten inkl. Command Bytes</param>
        /// <param name="force">true, wenn auch dann gesendet werden soll, wenn noch kein Serverinfo gekommen ist. Erforderlich bei ClientInfo.</param>
        private void SendMessage(byte[] data, bool force = false)
        {
            if (!_IsRunning)
                return;

            if (_ServerInfo == null && !force)
                return;

            lock (this) // nichts darf parallel senden.
            {
                if (_UseProto == 3)
                {
                    byte[] send;

                    using (MemoryStream ms = new MemoryStream())
                    using (BinaryWriter w = new BinaryWriter(ms))
                    {
                        w.Write(cMsgStart);
                        w.Write(IPAddress.HostToNetworkOrder((int)_MsgId++));
                        w.Write(data);
                        w.Flush();

                        send = ms.ToArray();
                    }

                    _ApiSckt.Send(send);
                }
                else
                {
                    _ApiSckt.Send(data);
                }
            }
        }

        /// <summary>
        /// Stoppt demuxer
        /// </summary>
        /// <param name="index">Geräteindex, 0xff für alle.</param>
        private void StopDmx(byte index)
        {
            /* DVBApi bietet einen Stop Demux, das ist sinnvoller als die ganze Liste neu zu senden, was auch ginge:
             * Alternatively when there is a need to stop channel decoding, while having the connection still open you can send a
             * special '3f' packed to OSCam. To stop decoding the specified demux, the following CA_PMT data should be sent to OSCam:
             * 9F 80 3f 04 83 02 00 <demux index>
             * If <demux index> is 0xff, then it is parsed as a wildcard and all demuxers associated with the connection are stopped.
             */

            // wird zuammengesetzt aus AotCaStop befehl + AdapterDevice Descriptor mit 2 byte Adapter device.
            // Da das zu viel wird, hier nun noch sonderfälle einzubauen, beispiel wie folgt im Kommentar, wird das ganz simpel gemacht:
            /*
            int cmd = IPAddress.HostToNetworkOrder(unchecked((int)DvbApiCommand.AotCaStop));
            DescriptorBase cb = new AdapterDevice(index);

            using (NetworkStream ns = new NetworkStream(_ApiSckt, false))
            using (BinaryWriter w = new BinaryWriter(ns))
            {
                w.Write(cmd);
                cb.Write(ns);
            }
            */

            int cmd = unchecked((int)DvbApiCommand.AotCaStop);
            byte[] data = new byte[] {
                (byte)(cmd >> 24),
                (byte)(cmd >> 16),
                (byte)(cmd >> 8),
                (byte)(cmd),
                0x83,
                0x02,
                0x00,
                index
            };

            SendMessage(data);
        }

        private void WatchConnection(object o)
        {
            Stopwatch measure = new Stopwatch();
            while (_IsRunning)
            {
                try
                {
                    if (!_IsConnected)
                    {
                        measure.Start();
                        Connect();
                    }
                    else if (_ServerInfo == null) // verbunden aber noch keine Infos.
                    {
                        if (measure.ElapsedMilliseconds > 30000)
                        {
                            LogProvider.Add(DebugLevel.Error, cLogSection, Message.DvbapiTimeout);
                            measure.Reset();
                            Reset();
                        }
                    }
                    else if (measure.IsRunning)
                    {
                        measure.Reset();
                    }
                }
                catch (Exception ex)
                {
                    LogProvider.Exception(cLogSection, Message.DvbapiConnFailed, ex);
                    Reset();
                }

                Thread.Sleep(100);
            }
        }
    }
}
