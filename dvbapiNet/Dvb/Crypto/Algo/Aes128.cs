using dvbapiNet.Log;
using dvbapiNet.Log.Locale;
using System;
using System.Runtime.InteropServices;
using System.Security.Cryptography;

namespace dvbapiNet.Dvb.Crypto.Algo
{
    /// <summary>
    /// Implementirt den Aes128 Modus
    /// </summary>
    public sealed class Aes128 : IDescramblerAlgo
    {
        private const string cLogSection = "descr aes128";

        private IntPtr[] _Cluster;
        private int _CurrentBatchIndex;
        private AesBase _EvenAes;
        private int _MaxPacketsAtOnce = 256;
        private AesBase _OddAes;

        public Aes128()
        {
            _EvenAes = new AesBase();
            _OddAes = new AesBase();
            _Cluster = new IntPtr[_MaxPacketsAtOnce + 1];
            _CurrentBatchIndex = 0;
        }

        public void AddToBatch(IntPtr tsPacket)
        {
            _Cluster[_CurrentBatchIndex++] = tsPacket;

            if (_CurrentBatchIndex >= _MaxPacketsAtOnce)
            {
                DescrambleBatch();
            }
        }

        public void DescrambleBatch()
        {
            if (_CurrentBatchIndex == 0)
                return;

            _Cluster[_CurrentBatchIndex++] = IntPtr.Zero;
            DescrambleBatch(_Cluster);
            _CurrentBatchIndex = 0;
        }

        public void DescrambleSingle(IntPtr tsPacket)
        {
            IntPtr[] clust = new IntPtr[2];
            clust[0] = tsPacket;
            clust[1] = IntPtr.Zero;

            DescrambleBatch(clust);
        }

        public void Dispose()
        {
            _EvenAes.Dispose();
            _OddAes.Dispose();
        }

        public void SetDescramblerData(DescramblerParity parity, DescramblerDataType type, byte[] data)
        {
            AesBase aes;
            switch (parity)
            {
                case DescramblerParity.Even:
                    aes = _EvenAes;
                    break;

                case DescramblerParity.Odd:
                    aes = _OddAes;
                    break;

                default:
                    LogProvider.Add(DebugLevel.Warning, cLogSection, Message.AesUnknownParity, parity);
                    return;
            }

            switch (type)
            {
                case DescramblerDataType.InitializationVector:
                    aes.SetIv(data);
                    break;

                case DescramblerDataType.Key:
                    if (data.Length != 16)
                    {
                        LogProvider.Add(DebugLevel.Warning, cLogSection, Message.AesInvalidKeyLength);
                        return;
                    }

                    aes.SetKey(data);
                    break;

                default:
                    LogProvider.Add(DebugLevel.Warning, cLogSection, Message.AesInvalidDescrDataType, type);
                    return;
            }
        }

        public void SetDescramblerMode(DescramblerMode mode)
        {
            switch (mode)
            {
                case DescramblerMode.Cbc:
                    _EvenAes.SetMode(CipherMode.CBC);
                    _OddAes.SetMode(CipherMode.CBC);
                    break;

                case DescramblerMode.Ecb:
                    _EvenAes.SetMode(CipherMode.ECB);
                    _OddAes.SetMode(CipherMode.ECB);
                    break;

                default:
                    LogProvider.Add(DebugLevel.Warning, cLogSection, Message.AesInvalidDescrMode, mode);
                    return;
            }
        }

        private void DescrambleBatch(IntPtr[] cluster)
        {
            byte[] packetBuffer = new byte[188];
            int adapt;
            int scram;
            int plPtr;

            int dStart; // Daten start
            int dLen; // Daten Länge.
            AesBase aes = null;

            for (int i = 0; i < cluster.Length; i++)
            {
                if (cluster[i] == IntPtr.Zero)
                    break;

                dStart = 4;

                // sind hier im Managed, heißt also kopieren in byte[] und wieder zurück. später performantere Lösung finden,
                // z.B. native DLL
                Marshal.Copy(cluster[i], packetBuffer, 0, packetBuffer.Length);

                scram = packetBuffer[3] & 0xc0; // 0 = unscrambled, 0x80 = even CW, 0xc0 = odd cw, 0x40 = ?
                adapt = packetBuffer[3] & 0x30; // 0 = invalid, 1 = payload only, 2 = a. only, 3 = a folow pl
                plPtr = packetBuffer[4]; // wenn Adaptionfield vorhanden Pointer auf Payload-Beginn (Adaption-Field Länge ohne Ptr selbst)

                if (scram == 0) // nicht verschlüsselt.
                    continue;
                // Normalerweise hier auch Check ob PES Level Descrambling an dieser Stelle.
                // Allerdings sieht der Standard vor, dass PES Level scrambling nicht in
                // "Consumer Electronic applications" verwendet werden soll,
                // daher wird das erstmal nicht implementiert.
                // ETSI TS 103 127 V1.1.1 (2013-05) 6.2.2 Letzter Satz.

                if (scram == 0x80) // even
                {
                    aes = _EvenAes;
                }
                else if (scram == 0xc0) // odd
                {
                    aes = _OddAes;
                } // else 0x40 unbekannt

                if (aes == null || !aes.CanDescramble)
                    continue;

                if (adapt == 0x20) // nur adaptionfield-Packet.
                    continue;

                if (adapt == 0x30) // Adaption field vorhanden, zusammen mit Payload.
                {
                    dStart += 1 + plPtr;
                }

                dLen = (188 - dStart) & 0xf0; // verschlüsselte Länge ist vielfaches von 16, also & ~0x0f;

                if (dLen > 0)
                    aes.Decrypt(packetBuffer, dStart, dLen);

                // Daten zurückkopieren:
                Marshal.Copy(packetBuffer, dStart, cluster[i] + dStart, dLen);
                Marshal.WriteByte(cluster[i], 3, (byte)(packetBuffer[3] & 0x3f)); // scrambled Flag entfernen.
            }
        }
    }
}
