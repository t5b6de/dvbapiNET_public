using System;

namespace dvbapiNet.Dvb.Crypto.Algo
{
    /// <summary>
    /// Schnittstelle für DVB Descrambler auf MPEG-TS Ebene.
    /// PES-Level Encryption wird nicht unterstützt.
    /// </summary>
    internal interface IDescramblerAlgo : IDisposable
    {
        /// <summary>
        /// Fügt ein TS-Packet dem Batch zur Massenentschlüsselung hinzu.
        /// Wird die Maximalmenge gleichzeitig zur entschlüsselden Packets überschritten,
        /// wird automatisch DescrambleBatch() ausgeführt.
        /// </summary>
        /// <param name="tsPacket"></param>
        void AddToBatch(IntPtr tsPacket);

        /// <summary>
        /// Veranlasst die Entschlüsselung der hinzugefügten Packets.
        /// </summary>
        void DescrambleBatch();

        /// <summary>
        /// Entschlüsselt einzelnes MPEG-TS-Packet
        /// </summary>
        /// <param name="tsPacket"></param>
        void DescrambleSingle(IntPtr tsPacket);

        /// <summary>
        /// Setzt die Daten für den Descrambler, z.B. Initialisierungsvektor und Schlüssel / Kontrollwörter
        /// </summary>
        /// <param name="parity"></param>
        /// <param name="type"></param>
        /// <param name="data"></param>
        void SetDescramblerData(DescramblerParity parity, DescramblerDataType type, byte[] data);

        /// <summary>
        /// Setzt den Verschlüsselungsmodus
        /// </summary>
        /// <param name="mode"></param>
        void SetDescramblerMode(DescramblerMode mode);
    }
}
