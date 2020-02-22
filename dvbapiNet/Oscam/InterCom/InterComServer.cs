using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Pipes;
using System.Net;
using System.Net.Sockets;
using System.Threading;

namespace dvbapiNet.Oscam.InterCom
{
    internal class InterComServer : InterComEndPoint
    {
        private NamedPipeServerStream _MainPipe;

        private List<InterComEndPoint> _OpenConns;
        private byte[] _PipeData;
        private Thread _TListener;

        /// <summary>
        /// Wird gefeuert, wenn ein Client sich verbunden und erfolgreich authentifiziert hat.
        /// </summary>
        public event Generic NewInstanceGenerated;

        public InterComServer(string pipeName)
            : base(null)
        {
            _PipeName = pipeName;
            // Nur für übertragung der IP und Port für die interne Plugin-Kommunikation.
            _MainPipe = new NamedPipeServerStream(_PipeName, PipeDirection.Out, 1, PipeTransmissionMode.Byte, PipeOptions.Asynchronous);

            Random rnd = new Random();
            byte[] ipData = new byte[4];
            int port = 0;
            int retry = 0;
            _AuthToken = (ulong)(rnd.Next(int.MinValue, int.MaxValue) << 32) | (ulong)rnd.Next(int.MinValue, int.MaxValue);

            _OpenConns = new List<InterComEndPoint>();

            _IsRunning = true;

            while (true)
            {
                try
                {
                    rnd.NextBytes(ipData);
                    port = rnd.Next(32000, 65000);
                    ipData[0] = 127; // soll eine Local IP im bereich 127.0.0.0/8 sein.
                    //ipData[1] = 0; // soll eine Local IP im bereich 127.0.0.0/8 sein.
                    //ipData[2] = 0; // soll eine Local IP im bereich 127.0.0.0/8 sein.
                    //ipData[3] = 1; // soll eine Local IP im bereich 127.0.0.0/8 sein.

                    IPAddress addr = new IPAddress(ipData);
                    _IpEp = new IPEndPoint(addr, port);

                    _IcSckt.Bind(_IpEp);
                    _IcSckt.Listen(32);

                    using (MemoryStream ms = new MemoryStream())
                    using (BinaryWriter w = new BinaryWriter(ms))
                    {
                        w.Write(_IpEp.Address.GetAddressBytes());
                        w.Write(_IpEp.Port);
                        w.Write(_AuthToken);
                        w.Flush();
                        _PipeData = ms.ToArray();
                    }

                    _TListener = new Thread(Listener);
                    _TListener.Start();

                    break;
                }
                catch (Exception ex)
                {
                    if (retry++ > 25)
                    {
                        throw ex;
                    }
                }
            }

            _MainPipe.BeginWaitForConnection(PipeConnect, null);
        }

        public override void Dispose()
        {
            base.Dispose();
            try
            {
                _MainPipe.Dispose();
            }
            catch { }

            InterComEndPoint[] tmp = _OpenConns.ToArray();
            for (int i = 0; i < tmp.Length; i++)
            {
                try
                {
                    tmp[i].Dispose();
                }
                catch { }
            }

            try
            {
                _IcSckt.Close();
                _IcSckt.Dispose();
            }
            catch { }
        }

        private void InstanceAuthenticated(InterComEndPoint ep)
        {
            NewInstanceGenerated?.Invoke(ep);
        }

        private void Listener(object state)
        {
            try
            {
                while (_IcSckt.IsBound && _IsRunning)
                {
                    Socket cl = _IcSckt.Accept();
                    if (cl == null)
                        return;

                    cl.NoDelay = true;
                    InterComServerConnection ep = new InterComServerConnection(cl, _AuthToken);
                    _OpenConns.Add(ep);

                    ep.Disconnected += RemoveConn;
                    ep.Authenticated += InstanceAuthenticated;

                    ep.BeginAuth();
                }
            }
            catch { }
        }

        private void PipeConnect(IAsyncResult iar)
        {
            if (!_IsRunning)
                return;

            _MainPipe.EndWaitForConnection(iar);

            _MainPipe.Write(_PipeData, 0, _PipeData.Length);

            _MainPipe.WaitForPipeDrain();
            _MainPipe.Disconnect();

            // neue Pipe erstellen, einmal geschlossen, wars das
            _MainPipe.BeginWaitForConnection(PipeConnect, null);
        }

        private void RemoveConn(InterComEndPoint ep)
        {
            _OpenConns.Remove(ep);
        }
    }
}
