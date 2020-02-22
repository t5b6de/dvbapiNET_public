using dvbapiNet.Log;
using dvbapiNet.Log.Locale;
using System;
using System.IO;
using System.Net;
using System.Net.Sockets;

namespace dvbapiNet.Oscam.InterCom
{
    internal abstract class InterComEndPoint : IDisposable
    {
        protected ulong _AuthToken;
        protected BinaryReader _Br;
        protected BinaryWriter _Bw;
        protected Socket _IcSckt;
        protected byte[] _InBuffer;
        protected IPEndPoint _IpEp;
        protected MemoryStream _MsIn;
        protected MemoryStream _MsOut;
        protected byte[] _OutBuffer;
        protected string _PipeName;
        private const string cLogSection = "intercom ep";

        public event Generic Disconnected;

        public event Command GotCommand;

        protected bool _IsRunning;

        public bool IsConnected { get; protected set; }

        /// <summary>
        /// Instanziiert neuen InterComEndpoint
        /// </summary>
        /// <param name="pipe">Pipe-Name oder null, wenn Client vom Intercomserver.</param>
        protected InterComEndPoint(string pipe)
        {
            _InBuffer = new byte[16384];
            _OutBuffer = new byte[16384];
            _MsIn = new MemoryStream(_InBuffer);
            _MsOut = new MemoryStream(_OutBuffer);
            _Br = new BinaryReader(_MsIn);
            _Bw = new BinaryWriter(_MsOut);
            _IsRunning = true;

            _IcSckt = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            _IcSckt.NoDelay = true;

            // Dann ist das ein Client vom IntercomServer
            if (pipe == null)
                return;

            _PipeName = pipe;
        }

        /// <summary>
        /// Trennt die InterCom-Verbindung ohne ein Ereignis zu feuern.
        /// </summary>
        public void DisconnectIntercom()
        {
            Disconnect(false);
        }

        public virtual void Dispose()
        {
            try
            {
                _IsRunning = false;
                DisconnectIntercom();
                _IcSckt.Dispose();
            }
            catch { }
        }

        public void SendCommand(InterComCommand cmd, byte[] data, int offset, int len)
        {
            if (!IsConnected)
                return;

            lock (this) // nur einer gleichzeitig senden!
            {
                _MsOut.Position = 0;
                _Bw.Write(len + sizeof(int));

                _Bw.Write((int)cmd);

                if (len != 0)
                    _Bw.Write(data, offset, len);

                _Bw.Flush();

                try
                {
                    _IcSckt.Send(_OutBuffer, (int)_MsOut.Position, SocketFlags.None);
                }
                catch (Exception ex)
                {
                    this.Disconnect();
                }
            }
        }

        public void SendCommand(InterComCommand cmd, byte[] data)
        {
            int len = data == null ? 0 : data.Length;
            SendCommand(cmd, data, 0, len);
        }

        public void SendCommand(InterComCommand cmd)
        {
            SendCommand(cmd, null, 0, 0);
        }

        public void Start()
        {
            if (!_IsRunning)
                return;

            IsConnected = true;
            _IcSckt.BeginReceive(_InBuffer, 0, sizeof(int), SocketFlags.None, OnRead, null);
        }

        protected void Disconnect()
        {
            Disconnect(true);
        }

        /// <summary>
        ///
        /// </summary>
        /// <param name="doEvent"></param>
        protected void Disconnect(bool invokeEvent)
        {
            IsConnected = false;
            try
            {
                _IcSckt.Close();
            }
            catch { }

            if (invokeEvent)
                Disconnected?.Invoke(this);
        }

        protected void OnRead(IAsyncResult iar)
        {
            if (!_IsRunning)
                return;

            if (!IsConnected || !_IcSckt.Connected)
                return;

            try
            {
                int len = _IcSckt.EndReceive(iar);
                _MsIn.Position = 0;

                if (len < 4)
                {
                    Disconnect();
                    return;
                }

                int packetLen = _Br.ReadInt32();

                if (!NetUtils.ReceiveAll(_InBuffer, _IcSckt, packetLen, (int)_MsIn.Position))
                {
                    Disconnect();
                    return;
                }

                InterComCommand cmd = (InterComCommand)_Br.ReadInt32();

                byte[] data = new byte[packetLen - sizeof(int)];
                Array.Copy(_InBuffer, _MsIn.Position, data, 0, data.Length);

                LogProvider.Add(DebugLevel.InterComEndpoint, cLogSection, Message.IntercomCmd, (int)cmd, cmd);
                LogProvider.Add(DebugLevel.InterComEndpoint | DebugLevel.HexDump, cLogSection, Message.IntercomCmdData, data, 0, data.Length);

                GotCommand?.Invoke(this, cmd, data);

                // Nächste Daten:
                Start();
            }
            catch (Exception ex)
            {
                Disconnect();

                LogProvider.Exception(cLogSection, Message.IntercomEx, ex);
            }
        }
    }
}
