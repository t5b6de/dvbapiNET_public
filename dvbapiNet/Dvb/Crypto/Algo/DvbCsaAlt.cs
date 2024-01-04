﻿using dvbapiNet.Log;
using dvbapiNet.Log.Locale;
using System;
using System.Runtime.InteropServices;

namespace dvbapiNet.Dvb.Crypto.Algo
{
    /// <summary>
    /// Implementiert den Alternativen DVB CSA Algorithmus
    /// Hier über Einbindung einer externen FFDecsa.dll
    /// </summary>
    public sealed class DvbCsaAlt : IDescramblerAlgo
    {
        private const string cLogSection = "descr dvbcsaalt";

        private IntPtr[] _Cluster;

        private int _CurrentBatchIndex;

        private byte[] _CwEven;

        private byte[] _CwOdd;

        private int _MaxPacketsAtOnce;

        private IntPtr _PtrKeySet;

        public DvbCsaAlt()
        {
            _MaxPacketsAtOnce = 256;
            _CwEven = new byte[8];
            _CwOdd = new byte[8];
            _PtrKeySet = GetKeySet();
            _CurrentBatchIndex = 0;

            _Cluster = new IntPtr[(_MaxPacketsAtOnce << 1) + 2]; // shl = 2 Pointers per packet, +2 weil letzter ptr nullptr sein muss.
        }

        /// <summary>
        /// Fügt dem Batch neues packet hinzu.
        /// Bei überschreiten der Maximalmenge an möglichen Pakets wird Descramble ausgelöst.
        /// </summary>
        /// <param name="tsPacket"></param>
        public void AddToBatch(IntPtr tsPacket)
        {
            int pos = _CurrentBatchIndex << 1;
            _Cluster[pos] = tsPacket;
            _Cluster[pos + 1] = tsPacket + 188;
            _CurrentBatchIndex++;

            if (_CurrentBatchIndex >= _MaxPacketsAtOnce)
                DescrambleBatch();
        }

        public void DescrambleBatch()
        {
            if (_CurrentBatchIndex == 0)
                return;

            int pos = _CurrentBatchIndex << 1;

            // Nullptr setzen, damit ffdecsa weiß dass der "buffer" hier zuende ist.
            _Cluster[pos] = IntPtr.Zero;
            _Cluster[pos + 1] = IntPtr.Zero;

            // Entschlüsseln bis alles verarbeitet ist
            while (Decrypt(_PtrKeySet, _Cluster) > 0) ;

            _CurrentBatchIndex = 0;
        }

        public void DescrambleSingle(IntPtr tsPacket)
        {
            IntPtr[] cl = new IntPtr[3];

            cl[0] = tsPacket;
            cl[1] = tsPacket + 188;
            cl[2] = IntPtr.Zero;

            Decrypt(_PtrKeySet, cl);
        }

        public void Dispose()
        {
            FreeKeySet(_PtrKeySet);
        }

        public void SetDescramblerData(DescramblerParity parity, DescramblerDataType type, byte[] data)
        {
            if (type == DescramblerDataType.Key)
            {
                switch (parity)
                {
                    case DescramblerParity.Even:
                        Array.Copy(data, _CwEven, _CwEven.Length);
                        SetEvenControlWord(_PtrKeySet, _CwEven);
                        break;

                    case DescramblerParity.Odd:
                        Array.Copy(data, _CwOdd, _CwOdd.Length);
                        SetOddControlWord(_PtrKeySet, _CwOdd);
                        break;

                    default:
                        LogProvider.Add(DebugLevel.Warning, cLogSection, Message.CsaUnknownParity, parity);
                        return;
                }
            }
            else
            {
                LogProvider.Add(DebugLevel.Warning, cLogSection, Message.CsaInvalidDescrDataType, type);
            }
        }

        public void SetDescramblerMode(DescramblerMode mode)
        {
            // in DvbAltCsa nicht unterstützt, daher ignorieren.
        }

        /// <summary>
        /// Entschlüsselt die Pakete im Cluster, implementierung entschlüsselt nicht alle Packets, gibt die Anzahl der entschlüsselten Packets zurück.
        /// </summary>
        /// <param name="keySet">Zeiger auf Structure mit dem Keyset</param>
        /// <param name="cluster">Zeiger auf Array mit Zeiger auf TS-Packets</param>
        /// <returns>Anzahl der Entschlüsselten Pakete</returns>
        [DllImport("FFDecsa.dll", EntryPoint = "decrypt_packets", CallingConvention = CallingConvention.Cdecl)]
        private static extern int Decrypt(IntPtr keySet, IntPtr[] cluster);

        /// <summary>
        /// Gibt den für den Keyset verwendeten Speicher wieder frei
        /// </summary>
        /// <param name="keySet">Zeiger auf Structure mit dem Keyset</param>
        [DllImport("FFDecsa.dll", EntryPoint = "free_key_struct", CallingConvention = CallingConvention.Cdecl)]
        private static extern void FreeKeySet(IntPtr keySet);

        /// <summary>
        /// Allokiert Speicher für den Keyset und gibt den Zeiger hierfür zurück
        /// </summary>
        /// <returns>Zeiger auf den Speicherbereich für den Keyset</returns>
        [DllImport("FFDecsa.dll", EntryPoint = "get_key_struct", CallingConvention = CallingConvention.Cdecl)]
        private static extern IntPtr GetKeySet();

        /// <summary>
        /// Setzt das even (gerade) Kontrollwort/Schlüssel
        /// </summary>
        /// <param name="keySet">Zeiger auf Structure mit dem Keyset</param>
        /// <param name="cw">byte[] mit dem Kontrollwort</param>
        [DllImport("FFDecsa.dll", EntryPoint = "set_even_control_word_alt", CallingConvention = CallingConvention.Cdecl)]
        private static extern void SetEvenControlWord(IntPtr keySet, byte[] cw);

        /// <summary>
        /// Setzt das odd (ungerade) Kontrollwort/Schlüssel
        /// </summary>
        /// <param name="keySet">Zeiger auf Structure mit dem Keyset</param>
        /// <param name="cw">byte[] mit dem Kontrollwort</param>
        [DllImport("FFDecsa.dll", EntryPoint = "set_odd_control_word_alt", CallingConvention = CallingConvention.Cdecl)]
        private static extern void SetOddControlWord(IntPtr keySet, byte[] cw);
    }
}
