using System;

namespace dvbapiNet.Dvb.Descriptors
{
    /// <summary>
    /// Implementiert den DVB Conditional Access Descriptor
    /// </summary>
    public sealed class CaDescriptor : DescriptorBase
    {
        private int _CaId;
        private int _CaPid;
        private byte[] _PrivateData;

        /// <summary>
        /// Gibt die Conditional Access System ID an, die in diesem Descriptor repräsentiert wird.
        /// </summary>
        public int CaId
        {
            get
            {
                return _CaId;
            }
        }

        /// <summary>
        /// Gibt die Stream PID an, über die ECM oder EMM für dieses CA-System zu finden sind.
        /// </summary>
        public int CaPid
        {
            get
            {
                return _CaPid;
            }
        }

        /// <summary>
        /// Gibt die privat definierten Daten für dieses CA-System zurück.
        /// </summary>
        public byte[] PrivateData
        {
            get
            {
                return _PrivateData;
            }
        }

        /// <summary>
        /// Instanziiert eine neue Instanz des CaDescriptors aus den gegebenen Quelldaten
        /// </summary>
        /// <param name="data">Byte[] aus dem die Daten für diesen CA-Descriptor bezogen werden sollen.</param>
        /// <param name="offset">Nullbasierter Quelloffset von ab dem die Daten bezogen werden sollen</param>
        public CaDescriptor(byte[] data, int offset)
            : base(data, offset)
        {
            _CaId = data[2 + offset] << 8;
            _CaId |= data[3 + offset];

            _CaPid = data[4 + offset] << 8;
            _CaPid |= data[5 + offset];
            _CaPid &= 0x1fff;

            if (Length > 6)
            {
                _PrivateData = new byte[Length - 6]; // 6 bytes abziehen.

                Array.Copy(data, 6 + offset, _PrivateData, 0, _PrivateData.Length);
            }
            else
            {
                _PrivateData = null;
            }
        }
    }
}
