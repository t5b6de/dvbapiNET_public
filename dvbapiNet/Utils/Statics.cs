using System.Text;

namespace dvbapiNet.Utils
{
    /// <summary>
    /// Stellt einfache statische Funktionen bereit.
    /// </summary>
    internal static class Statics
    {
        /// <summary>
        /// Liest eine Zeichenfolge aus einem Byte-Array welche nach C-Standard erstellt wurde.
        /// </summary>
        /// <param name="offset">Beginn der Zeichenfolge im Quellarray</param>
        /// <param name="data">Quellarray</param>
        /// <param name="maxLen">Maximale länge der Zeichenfolge</param>
        /// <returns></returns>
        public static string GetStringFromC(int offset, byte[] data, int maxLen)
        {
            int i = 0;

            for (; i < data.Length && i < maxLen; i++)
            {
                if (data[i + offset] == 0)
                    break;
            }

            return Encoding.UTF8.GetString(data, offset, i);
        }

        /// <summary>
        /// Liest eine Zeichenfolge aus einem Byte-Array welche nach C-Standard erstellt wurde.
        /// </summary>
        /// <param name="offset">Beginn der Zeichenfolge im Quellarray</param>
        /// <param name="data">Quellarray</param>
        /// <returns></returns>
        public static string GetStringFromC(int offset, byte[] data)
        {
            return GetStringFromC(offset, data, data.Length);
        }

        /// <summary>
        /// Liest eine Zeichenfolge aus einem Byte-Array welche nach C-Standard erstellt wurde.
        /// </summary>
        /// <param name="data">Quellarray</param>
        /// <returns></returns>
        public static string GetStringFromC(byte[] data)
        {
            return GetStringFromC(0, data, data.Length);
        }
    }
}
