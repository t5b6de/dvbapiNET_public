using System.Net.Sockets;

namespace dvbapiNet.Oscam
{
    /// <summary>
    /// Enthält statische ggf. nützliche Methoden für Netzwerkdatenverarbeitung
    /// </summary>
    internal class NetUtils
    {
        /// <summary>
        /// Empfängt daten aus einem Socket, so viele in buffer passen, oder über len angegeben.
        /// Hintergrund: bei großen Datenmengen werden nicht alle Daten auf einmal empfangen.
        /// Hier kleine Hilfsfunktion um große Datenmengen zu empfangen.
        /// </summary>
        /// <param name="buffer">ziel array</param>
        /// <param name="s">socket</param>
        /// <param name="len">max anzahl bytes</param>
        public static bool ReceiveAll(byte[] buffer, Socket s, int len, int offset)
        {
            if (len > buffer.Length - offset)
                len = buffer.Length - offset;

            int p = s.Receive(buffer, offset, len, SocketFlags.None);
            int r;

            while (p < len)
            {
                r = s.Receive(buffer, p + offset, len - p, SocketFlags.None);

                if (r == 0) // socket dicht.
                    return false;

                p += p;
            }

            return true;
        }

        public static bool ReceiveAll(byte[] buffer, Socket s, int len)
        {
            return ReceiveAll(buffer, s, len, 0);
        }

        public static bool ReceiveAll(byte[] buffer, Socket s)
        {
            return ReceiveAll(buffer, s, buffer.Length, 0);
        }
    }
}
