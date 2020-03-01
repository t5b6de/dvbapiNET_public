using System;
using System.Net.Sockets;

namespace dvbapiNet.Oscam.InterCom
{
    /// <summary>
    /// Intercom Endpoint Variante für die Verbindung auf Seite des DvbapiClient
    /// </summary>
    internal class InterComServerConnection : InterComEndPoint
    {
        /// <summary>
        /// Wird gefeuert, wenn der Client sich syntaktisch korrekt mit Token gemeldet hat.
        /// </summary>
        public event Generic Authenticated;

        public InterComServerConnection(Socket s, ulong auth)
            : base(null)
        {
            _IcSckt = s;
            _AuthToken = auth;
        }

        /// <summary>
        /// Ruft einmalig den Abruf einer Message bestehend aus Länge Befehl und Daten auf
        /// Sobald die LÄngenInfos empfangen wurden wird firstcommand aufgerufen.
        /// </summary>
        public void BeginAuth()
        {
            _IcSckt.BeginReceive(_InBuffer, 0, sizeof(int), SocketFlags.None, FirstCommand, null);
        }

        /// <summary>
        /// Prüft ob der CLient auch wirklich der ist, der er zu sein scheint.
        /// </summary>
        /// <param name="iar"></param>
        private void FirstCommand(IAsyncResult iar)
        {
            int len = _IcSckt.EndReceive(iar);

            if (len != sizeof(int))
            {
                Disconnect();
                return;
            }

            int packetLen = _Br.ReadInt32();

            if (packetLen > _InBuffer.Length)
            {
                Disconnect();
                return;
            }

            if (!NetUtils.ReceiveAll(_InBuffer, _IcSckt, packetLen, (int)_MsIn.Position))
            {
                Disconnect();
                return;
            }

            InterComCommand cmd = (InterComCommand)_Br.ReadInt32();

            if (cmd != InterComCommand.Initiate)
            {
                Disconnect();
                return;
            }

            ulong auth = _Br.ReadUInt64();

            if (auth != _AuthToken)
            {
                Disconnect();
                return;
            }

            if (Authenticated == null)
            {
                Disconnect();
                return;
            }

            Authenticated.Invoke(this);
        }
    }
}
