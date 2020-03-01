using dvbapiNet.Dvb;
using dvbapiNet.Dvb.Crypto;
using dvbapiNet.Oscam.InterCom;
using dvbapiNet.Oscam.Packets;
using System.IO;

namespace dvbapiNet.Oscam
{
    /// <summary>
    /// Ereignis für Kanalaktualisierung
    /// </summary>
    /// <param name="add">true wenn vorher kein Sender aktiv war, false wenn aktualisierung oder Stop.</param>
    internal delegate void ChannelUpdated(ChannelSession sender, bool add);

    /// <summary>
    /// Ereignis für Filterdaten erhalten
    /// </summary>
    /// <param name="filterId">Filter-ID von Oscam festgelegt.</param>
    /// <param name="data">Filterdaten, neu erstelltes Array, verweis kann gesichert werden.</param>
    internal delegate void FilterEvent(ChannelSession sender, int filterId, byte[] data);

    /// <summary>
    /// Stellt aus Sicht des DVBAPI-Clients eine Plugin-Instanz dar.
    /// </summary>
    internal class ChannelSession
    {
        private PmtSection _CurrentPmt;
        private InterComEndPoint _IcEp;
        private bool _IsRunning;

        public event ChannelUpdated ChannelUpdated;

        //public event Action Connected;

        public event ChannelUpdated Disconnected;

        public event FilterEvent FilterData;

        public int AdapterId { get; private set; }

        public int CurrentPid { get; private set; }
        public int CurrentNId { get; private set; }
        public int CurrentTsId { get; private set; }

        public int CurrentSid
        {
            get
            {
                if (_CurrentPmt != null)
                    return _CurrentPmt.ServiceId;

                return -1;
            }
        }

        /// <summary>
        /// Dvbapi Demux-Index, stellt den Demux-Index in Oscam selbst dar. Wird gesetzt sobald Filter angefordert werden.
        /// Dies stellt einen Workaround dar, oscam sollte keine eigenen Index führen.
        /// </summary>
        public int DvbApiDemuxIndex { get; private set; }

        /// <summary>
        /// Gibt letzte ECM-Info von Oscam zurück
        /// </summary>
        public EcmInfo LastEcmInfo // TODO Nutzen finden, ggf. Webinterface?
        {
            get; private set;
        }

        public ChannelSession(int index, InterComEndPoint ep)
        {
            AdapterId = index;
            _IcEp = ep;

            _IsRunning = false;

            _IcEp.GotCommand += CommandReceived;
            _IcEp.Disconnected += IcEpDisconnected;

            CurrentPid = -1;
            CurrentTsId = -1;
            CurrentNId = -1;
            DvbApiDemuxIndex = -1;
        }

        public void ClearFilter()
        {
            _IcEp.SendCommand(InterComCommand.ClearFilter);
        }

        public CaPmtSection GetCaPmt(CaPmtSection.ListManagement entryType)
        {
            return new CaPmtSection(_CurrentPmt, CurrentPid, CurrentTsId, CurrentNId, (byte)AdapterId, (byte)AdapterId, (byte)AdapterId, entryType);
        }

        public void SetDecramblerData(int caIndex, DescramblerParity parity, DescramblerDataType dType, byte[] data)
        {
            using (MemoryStream ms = new MemoryStream())
            using (BinaryWriter wr = new BinaryWriter(ms))
            {
                wr.Write(caIndex);
                wr.Write((int)parity);
                wr.Write((int)dType);
                wr.Write(data);

                wr.Flush();
                _IcEp.SendCommand(InterComCommand.SetDescramblerData, ms.ToArray());
            }
        }

        public void SetDescramblerMode(int caIndex, DescramblerAlgo algo, DescramblerMode mode)
        {
            using (MemoryStream ms = new MemoryStream())
            using (BinaryWriter wr = new BinaryWriter(ms))
            {
                wr.Write(caIndex);
                wr.Write((int)algo);
                wr.Write((int)mode);
                wr.Flush();
                _IcEp.SendCommand(InterComCommand.SetDescramblerMode, ms.ToArray());
            }
        }

        public void SetEcmInfo(EcmInfo info)
        {
            LastEcmInfo = info;
        }

        public void SetFilter(int demux, int filter, int pid, int timeout, int flags, byte[] fSource)
        {
            DvbApiDemuxIndex = demux;

            using (MemoryStream ms = new MemoryStream())
            using (BinaryWriter wr = new BinaryWriter(ms))
            {
                wr.Write(filter);
                wr.Write(pid);
                wr.Write(timeout);
                wr.Write(flags);
                wr.Write(fSource);
                wr.Flush();
                _IcEp.SendCommand(InterComCommand.SetFilter, ms.ToArray());
            }
        }

        public void SetPid(int pid, byte idx)
        {
            using (MemoryStream ms = new MemoryStream())
            using (BinaryWriter wr = new BinaryWriter(ms))
            {
                wr.Write(pid);
                wr.Write(idx);
                wr.Flush();
                _IcEp.SendCommand(InterComCommand.SetPid, ms.ToArray());
            }
        }

        public void Start()
        {
            if (_IsRunning)
                return;

            _IcEp.Start();
            SendIndex();

            _IsRunning = true;
        }

        public void StopFilter(int filter, int pid)
        {
            using (MemoryStream ms = new MemoryStream())
            using (BinaryWriter wr = new BinaryWriter(ms))
            {
                wr.Write(filter);
                wr.Write(pid);
                wr.Flush();
                _IcEp.SendCommand(InterComCommand.DelFilter, ms.ToArray());
            }
        }

        private void CommandReceived(InterComEndPoint sender, InterComCommand cmd, byte[] data)
        {
            using (MemoryStream ms = new MemoryStream(data))
            using (BinaryReader rdr = new BinaryReader(ms))
            {
                switch (cmd)
                {
                    case InterComCommand.Stop:

                        Stop(false);

                        break;

                    case InterComCommand.Pmt:
                        // 1 int pmt pid, 1 int sid;
                        bool update = false;
                        bool add = CurrentSid == -1;

                        int tmp = rdr.ReadInt32(); // 4 byte nid

                        if (tmp != CurrentNId)
                        {
                            update = true;
                            CurrentNId = tmp;
                        }

                        tmp = rdr.ReadInt32(); // 4 byte tsid

                        if (tmp != CurrentTsId)
                        {
                            update = true;
                            CurrentTsId = tmp;
                        }

                        tmp = rdr.ReadInt32(); // 4 byte Pid

                        if (tmp != CurrentPid)
                        {
                            update = true;
                            CurrentPid = tmp;
                        }

                        PmtSection pmt = new PmtSection(data, (int)rdr.BaseStream.Position, CurrentPid);

                        if (_CurrentPmt == null || _CurrentPmt.Version != pmt.Version)
                        {
                            update = true;
                            _CurrentPmt = pmt;
                        }

                        if (update)
                            ChannelUpdated?.Invoke(this, add);

                        break;

                    case InterComCommand.FilterData:

                        int fltId = rdr.ReadInt32();

                        byte[] fData = rdr.ReadBytes(data.Length - sizeof(int));

                        FilterData?.Invoke(this, fltId, fData);
                        break;

                    default:
                        // unbekannter befehl.  oder anderswo abgefangen Sollte nicht vorkommen
                        // Da aber bereits alle Bytes gelesen, ignorieren und weiter im Programm.
                        break;
                }
            }
        }

        private void IcEpDisconnected(InterComEndPoint sender)
        {
            Stop(false);
            Disconnected?.Invoke(this, false);

            // Adapter-ID erst nach invoke zurücksetzen, zwecks Identifikation im Disconnect.
            AdapterId = -1;
        }

        /// <summary>
        ///
        /// </summary>
        private void SendIndex()
        {
            using (MemoryStream ms = new MemoryStream())
            using (BinaryWriter wr = new BinaryWriter(ms))
            {
                wr.Write(AdapterId);
                wr.Flush();
                _IcEp.SendCommand(InterComCommand.SetInstanceNumber, ms.ToArray());
            }
        }

        private void Stop(bool fromApi)
        {
            _CurrentPmt = null;

            CurrentPid = -1;
            CurrentNId = -1;
            CurrentTsId = -1;
            DvbApiDemuxIndex = -1;
            LastEcmInfo = null;

            if (!fromApi)
            {
                ChannelUpdated?.Invoke(this, false);
            }
            else
            {
                _IcEp.SendCommand(InterComCommand.Stop);
            }
        }
    }
}
