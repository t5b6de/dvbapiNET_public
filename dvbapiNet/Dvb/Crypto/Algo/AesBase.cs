﻿using System;
using System.Security.Cryptography;

namespace dvbapiNet.Dvb.Crypto.Algo
{
    /// <summary>
    /// Implementiert Schnittstelle zum AES
    /// </summary>
    public class AesBase : IDisposable
    {
        private AesManaged _Aes;
        private CipherMode _Cipher;
        private ICryptoTransform _Decryptor;
        private byte[] _Iv;
        private byte[] _Key;

        /// <summary>
        /// True, wenn eine Entschlüsselung möglich (mind. Key ist gesetzt), anderenfalls false.
        /// </summary>
        public bool CanDescramble
        {
            get; private set;
        }

        public AesBase()
        {
            CanDescramble = false;
            // Default IV, ETSI TS 103127 V1.1.1 (2013-05) Punkt 6.3.1.2 Initialization Vector (DVBTMCPTAESCISSA):
            _Iv = new byte[16] { 0x44, 0x56, 0x42, 0x54, 0x4d, 0x43, 0x50, 0x54, 0x41, 0x45, 0x53, 0x43, 0x49, 0x53, 0x53, 0x41 };
            _Key = new byte[16];

            // Standardwerte festlegen:
            _Aes = new AesManaged
            {
                IV = _Iv,
                Key = _Key,
                Mode = CipherMode.CBC,
                Padding = PaddingMode.None,
                BlockSize = 128,
                FeedbackSize = 128
            };
            _Decryptor = _Aes.CreateDecryptor();
        }

        /// <summary>
        /// Entschlüsselt einen Block mit gegebener Länge
        /// </summary>
        /// <param name="block">Byte-Array, welches die Daten enthält</param>
        /// <param name="offset">start-Offset im Byte-Array</param>
        /// <param name="len">Menge der zu entschlüsselnen Bytes</param>
        public void Decrypt(byte[] block, int offset, int len)
        {
            _Decryptor.TransformBlock(block, offset, len, block, offset);

            if (_Cipher == CipherMode.CBC)
            {
                _Decryptor.Dispose();
                _Decryptor = _Aes.CreateDecryptor();
            }
        }

        public void Dispose()
        {
            CanDescramble = false;
            _Decryptor.Dispose();
            _Aes.Dispose();
            _Decryptor = null;
            _Aes = null;
        }

        /// <summary>
        /// Setzt den Initialisierungsvektor für die Entschlüsselung
        /// </summary>
        /// <param name="iv"></param>
        public void SetIv(byte[] iv)
        {
            Array.Copy(iv, _Iv, _Iv.Length);

            _Aes.IV = _Iv;

            _Decryptor.Dispose();
            _Decryptor = _Aes.CreateDecryptor();
        }

        /// <summary>
        /// Setzt den Schlüssel für die Entschlüsselung
        /// </summary>
        /// <param name="key"></param>
        public void SetKey(byte[] key)
        {
            Array.Copy(key, _Key, _Key.Length);

            _Aes.Key = _Key;

            _Decryptor.Dispose();
            _Decryptor = _Aes.CreateDecryptor();

            CanDescramble = true; // minimum Key muss gesetzt sein.
        }

        /// <summary>
        /// Setzt den Betriebsmodus für die Verschlüsselung
        /// </summary>
        /// <param name="mode"></param>
        public void SetMode(CipherMode mode)
        {
            if (_Cipher == mode)
                return;

            _Cipher = mode;

            _Aes.Mode = _Cipher;

            _Decryptor.Dispose();
            _Decryptor = _Aes.CreateDecryptor();
        }
    }
}
