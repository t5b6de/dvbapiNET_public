using System;
using System.Security.Cryptography;

namespace dvbapiNet.Dvb.Crypto.Algo
{
    /// <summary>
    /// Implementiert Schnittstelle zum DES
    /// </summary>
    public class DesBase
    {
        private DESCryptoServiceProvider _Des;
        private CipherMode _Cipher;
        private ICryptoTransform _Decryptor;
        private byte[] _Iv;
        private byte[] _Key;

        /// <summary>
        /// True wenn diese Instanz Daten entschlüsseln kann (Mindestens ein Schlüssel gesetzt) anderenfalls false.
        /// </summary>
        public bool CanDescramble
        {
            get; private set;
        }

        public DesBase()
        {
            CanDescramble = false;

            _Iv = new byte[16];
            _Key = new byte[16];

            // Standardwerte festlegen:
            _Des = new DESCryptoServiceProvider
            {
                IV = _Iv,
                Key = _Key,
                Mode = CipherMode.CBC,
                Padding = PaddingMode.None,
                BlockSize = 64,
                FeedbackSize = 64
            };
            _Decryptor = _Des.CreateDecryptor();
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
                _Decryptor = _Des.CreateDecryptor();
            }
        }

        public void Dispose()
        {
            CanDescramble = false;
            _Decryptor.Dispose();
            _Des.Dispose();
            _Decryptor = null;
            _Des = null;
        }

        /// <summary>
        /// Setzt den Initialisierungsvektor für die Entschlüsselung
        /// </summary>
        /// <param name="iv"></param>
        public void SetIv(byte[] iv)
        {
            Array.Copy(iv, _Iv, _Iv.Length);

            _Des.IV = _Iv;

            _Decryptor.Dispose();
            _Decryptor = _Des.CreateDecryptor();
        }

        /// <summary>
        /// Setzt den Schlüssel für die Entschlüsselung
        /// </summary>
        /// <param name="key"></param>
        public void SetKey(byte[] key)
        {
            Array.Copy(key, _Key, _Key.Length);

            _Des.Key = _Key;

            _Decryptor.Dispose();
            _Decryptor = _Des.CreateDecryptor();

            CanDescramble = true; // minimum Key muss gesetzt sein.
        }

        /// <summary>
        /// Setzt den Modus für die Entschlüsselung
        /// </summary>
        /// <param name="mode"></param>
        public void SetMode(CipherMode mode)
        {
            _Cipher = mode;

            _Des.Mode = _Cipher;

            _Decryptor.Dispose();
            _Decryptor = _Des.CreateDecryptor();
        }
    }
}
