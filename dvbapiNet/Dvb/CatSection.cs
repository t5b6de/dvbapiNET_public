using dvbapiNet.Dvb.Descriptors;
using System;
using System.Collections;
using System.Collections.Generic;

namespace dvbapiNet.Dvb
{
    public sealed class CatSection : SectionBase, IEnumerable<DescriptorBase>
    {
        private List<DescriptorBase> _Descs;

        public CatSection()
            : base(0x0001, true)
        {
            _Descs = new List<DescriptorBase>();
        }

        public CatSection(byte[] source, int offset)
            : base(source, offset, 0x0001, true)
        {
            _Descs = new List<DescriptorBase>();

            if (Finished)
                Parse();
        }

        private bool PreCheck()
        {
            if (TableId != 0x01) // Dann keine Cat-Tabelle.
            {
                Reset();
                return false;
            }

            Parse();
            return true;
        }

        public override bool AddPacket(IntPtr tsPacket)
        {
            if (base.AddPacket(tsPacket))
                return PreCheck();

            return false;
        }

        public IEnumerator<DescriptorBase> GetEnumerator()
        {
            return ((IEnumerable<DescriptorBase>)_Descs).GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return ((IEnumerable<DescriptorBase>)_Descs).GetEnumerator();
        }

        public override void Reset()
        {
            base.Reset();

            if (_Descs != null)
                _Descs.Clear();
        }

        private void Parse()
        {
            _Descs.Clear();

            int len = SectionSize - 4; // 4 byte CRC am Ende abziehen.
            int i = 8; // start der Daten.

            while (i < len)
            {
                DescriptorBase desc = DescriptorFactory.CreateDescriptor(SectionData, i);

                _Descs.Add(desc);

                i += desc.Length;
            }
        }
    }
}
