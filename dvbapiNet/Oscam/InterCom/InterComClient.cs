using System.IO;
using System.IO.Pipes;
using System.Net;

namespace dvbapiNet.Oscam.InterCom
{
    /// <summary>
    /// Stellt einen Intercom-Client dar (Verbindung zwischen DvbapiAdapter und DvbapiClient
    /// </summary>
    internal class InterComClient : InterComEndPoint
    {
        public InterComClient(string pipeName)
            : base(pipeName)
        {
            NamedPipeClientStream cl = new NamedPipeClientStream(".", _PipeName, PipeDirection.In);

            cl.Connect(1000);

            IPAddress ip;
            byte[] ipData;
            int port;

            using (BinaryReader r = new BinaryReader(cl))
            {
                ipData = r.ReadBytes(4);
                port = r.ReadInt32();
                _AuthToken = r.ReadUInt64();
            }

            ip = new IPAddress(ipData);
            _IpEp = new IPEndPoint(ip, port);

            _IcSckt.Connect(_IpEp);
            IsConnected = true; // muss true gesetzt werden, sonst funktioniert send command nicht.
            Authenticate();
        }

        private void Authenticate()
        {
            using (MemoryStream ms = new MemoryStream())
            using (BinaryWriter bw = new BinaryWriter(ms))
            {
                bw.Write(_AuthToken);
                bw.Flush();
                SendCommand(InterComCommand.Initiate, ms.ToArray());
            }
        }
    }
}
