namespace dvbapiNet.Dvb
{
    /// <summary>
    /// CRC Checksummen-Berechnung
    /// </summary>
    internal static class SectionCrc
    {
        private const uint cCrc32Poly = 0x04C11DB7;
        private static uint[] _CrcTable = new uint[256];

        static SectionCrc()
        {
            uint i, j;

            uint crc;

            // CRC Tabelle erstellen
            for (i = 0; i < 256; i++)
            {
                crc = i << 24;
                for (j = 0; j < 8; j++)
                {
                    if ((crc & 0x80000000) != 0)
                        crc = (crc << 1) ^ cCrc32Poly;
                    else
                        crc = (crc << 1);
                }
                _CrcTable[i] = crc;
            }
        }

        /// <summary>
        /// CRC32 Checksummen Berechnung mit Gegenprüfung
        /// </summary>
        /// <param name="data">ByteArray des Befehls</param>
        /// <param name="len">Länge der zu berücksichtigen Bytes</param>
        /// <param name="crc">CRC zur Gegenprüfung</param>
        /// <returns>Bool true wenn CRC stimmt</returns>
        public static bool Compare(byte[] data, int len, uint crc)
        {
            uint ccrc = ComputeInt(data, len);

            return crc == ccrc;
        }

        /// <summary>
        /// CRC32 Checksummen Berechnung
        /// </summary>
        /// <param name="data">ByteArray des Befehls</param>
        /// <param name="len">Länge der zu berücksichtigen Bytes</param>
        /// <returns>4 byte CRC Checksumme</returns>
        public static byte[] Compute(byte[] data, int len)
        {
            uint crc = ComputeInt(data, len);

            byte[] output = new byte[4];

            output[0] = (byte)(crc >> 24);
            output[1] = (byte)(crc >> 16);
            output[2] = (byte)(crc >> 8);
            output[3] = (byte)(crc);

            return output;
        }

        /// <summary>
        /// CRC32 Checksummen Berechnung
        /// </summary>
        /// <param name="data">ByteArray des Befehls</param>
        /// <param name="len">Länge der zu berücksichtigen Bytes</param>
        /// <returns>4 byte CRC Checksumme</returns>
        public static uint ComputeInt(byte[] data, int len)
        {
            uint i, j;
            uint p = 0;

            uint crc = 0xffffffff;

            for (j = 0; j < len; j++)
            {
                i = ((crc >> 24) ^ data[p++]) & 0xff;
                crc = (crc << 8) ^ _CrcTable[i];
            }

            return crc;
        }
    }
}
