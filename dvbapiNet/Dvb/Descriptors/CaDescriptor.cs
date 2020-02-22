using System;

namespace dvbapiNet.Dvb.Descriptors
{
    public sealed class CaDescriptor : DescriptorBase
    {
        private int _CaId;
        private int _CaPid;
        private byte[] _PrivateData;

        public int CaId
        {
            get
            {
                return _CaId;
            }
        }

        public int CaPid
        {
            get
            {
                return _CaPid;
            }
        }

        public byte[] PrivateData
        {
            get
            {
                return _PrivateData;
            }
        }

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
