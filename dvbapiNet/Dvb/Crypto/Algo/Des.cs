using dvbapiNet.Log;
using dvbapiNet.Log.Locale;
using System;
using System.Runtime.InteropServices;
using System.Security.Cryptography;

namespace dvbapiNet.Dvb.Crypto.Algo
{
    public sealed class Des : IDescramblerAlgo
    {
        private const string cLogSection = "descr des";

        private IntPtr[] _Cluster;
        private int _CurrentBatchIndex;
        private DesBase _EvenDes;
        private int _MaxPacketsAtOnce = 256;
        private DesBase _OddDes;

        public Des()
        {
            // TODO: Entfernen, wenn fertig:
            LogProvider.Add(DebugLevel.Warning, cLogSection, Message.DesDescramblingNotAvail);
            _EvenDes = new DesBase();
            _OddDes = new DesBase();
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
            _EvenDes.Dispose();
            _OddDes.Dispose();
        }

        public void SetDescramblerData(DescramblerParity parity, DescramblerDataType type, byte[] data)
        {
            DesBase des;
            switch (parity)
            {
                case DescramblerParity.Even:
                    des = _EvenDes;
                    break;

                case DescramblerParity.Odd:
                    des = _OddDes;
                    break;

                default:
                    LogProvider.Add(DebugLevel.Warning, cLogSection, Message.DesUnknownParity, parity);
                    return;
            }

            switch (type)
            {
                case DescramblerDataType.InitializationVector:
                    des.SetIv(data);
                    break;

                case DescramblerDataType.Key:
                    if (data.Length != 8)
                    {
                        LogProvider.Add(DebugLevel.Warning, cLogSection, Message.DesInvalidKeyLength);
                        return;
                    }

                    des.SetKey(data);
                    break;

                default:
                    LogProvider.Add(DebugLevel.Warning, cLogSection, Message.DesInvalidDescrDataType, type);
                    return;
            }
        }

        public void SetDescramblerMode(DescramblerMode mode)
        {
            switch (mode)
            {
                case DescramblerMode.Cbc:
                    _EvenDes.SetMode(CipherMode.CBC);
                    _OddDes.SetMode(CipherMode.CBC);
                    break;

                case DescramblerMode.Ecb:
                    _EvenDes.SetMode(CipherMode.ECB);
                    _OddDes.SetMode(CipherMode.ECB);
                    break;

                default:
                    LogProvider.Add(DebugLevel.Warning, cLogSection, Message.DesInvalidDescrMode, mode);
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
            DesBase des = null;

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

                if (scram == 0x80) // even
                {
                    des = _EvenDes;
                }
                else if (scram == 0xc0) // odd
                {
                    des = _OddDes;
                } // else 0x40 unbekannt

                if (des == null || !des.CanDescramble)
                    continue;

                if (adapt == 0x20) // nur adaptionfield-Packet.
                    continue;

                if (adapt == 0x30) // Adaption field vorhanden, zusammen mit Payload.
                {
                    dStart += 1 + plPtr;
                }

                dLen = (188 - dStart) & 0xf8; // verschlüsselte Länge ist vielfaches von 8 (64 bit), also & ~0x07;

                if (dLen > 0)
                    des.Decrypt(packetBuffer, dStart, dLen);

                // Daten zurückkopieren:
                Marshal.Copy(packetBuffer, dStart, cluster[i] + dStart, dLen);
                Marshal.WriteByte(cluster[i], 3, (byte)(packetBuffer[3] & 0x3f)); // scrambled Flag entfernen.
            }
        }
    }
}
