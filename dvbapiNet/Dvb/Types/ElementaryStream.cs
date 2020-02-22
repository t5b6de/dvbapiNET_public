using dvbapiNet.Dvb.Descriptors;
using System.Collections;
using System.Collections.Generic;

namespace dvbapiNet.Dvb.Types
{
    public class ElementaryStream : IEnumerable<DescriptorBase>
    {
        private List<DescriptorBase> _Descs;
        private int _Len;
        private int _Pid;
        private int _Type;

        public int Length
        {
            get
            {
                return _Len + 5; // inkl StreamType (1), Elementary Pid (2), EsInfoLength (2)
            }
        }

        public int Pid
        {
            get
            {
                return _Pid;
            }
        }

        public int StreamType
        {
            get
            {
                return _Type;
            }
        }

        public ElementaryStream(byte[] data, int offset)
        {
            _Descs = new List<DescriptorBase>();

            int i = 0;

            _Type = data[(i++) + offset];
            _Pid = data[(i++) + offset] << 8;
            _Pid |= data[(i++) + offset];
            _Pid &= 0x1fff;

            _Len = data[(i++) + offset] << 8;
            _Len = data[(i++) + offset];

            while (i < Length)
            {
                DescriptorBase desc = DescriptorFactory.CreateDescriptor(data, i + offset);
                _Descs.Add(desc);

                i += desc.Length;
            }
        }

        public IEnumerator<DescriptorBase> GetEnumerator()
        {
            return ((IEnumerable<DescriptorBase>)_Descs).GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return ((IEnumerable<DescriptorBase>)_Descs).GetEnumerator();
        }
    }
}
