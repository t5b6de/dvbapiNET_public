using dvbapiNet.Dvb.Types;
using System;
using System.Collections;
using System.Collections.Generic;

namespace dvbapiNet.Dvb
{
    public sealed class SdtSection : SectionBase, IEnumerable<ServiceDescriptionTable>
    {
        private int _OrigNid;
        private List<ServiceDescriptionTable> _Sdts;

        public int OriginalNetworkId
        {
            get
            {
                return _OrigNid;
            }
        }

        private bool Actual
        {
            get;
        }

        public int TransportStreamId
        {
            get
            {
                if (Finished)
                {
                    int u = SectionData[3];
                    int l = SectionData[4];

                    l |= u << 8;

                    return l;
                }
                else
                {
                    return -1;
                }
            }
        }

        /// <summary>
        /// Instantiiert neue SdtSection
        /// </summary>
        /// <param name="actual">True wenn die Liste vom aktuellen Transponder genutzt werden soll. Anderenfalls false</param>
        public SdtSection(bool actual)
            : base(0x0011, true)
        {
            _Sdts = new List<ServiceDescriptionTable>();
            Actual = actual;
        }

        public SdtSection(byte[] source, int offset, bool actual)
            : base(source, offset, 0x0011, true)
        {
            _Sdts = new List<ServiceDescriptionTable>();
            Actual = actual;

            if (Finished)
                Parse();
        }

        private bool PreCheck()
        {
            if (TableId != (Actual ? 0x42 : 0x44)) // Dann keine Sdt-Tabelle.
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

        public override bool AddPacket(byte[] tsPacket)
        {
            if (base.AddPacket(tsPacket))
                return PreCheck();

            return false;
        }

        public IEnumerator<ServiceDescriptionTable> GetEnumerator()
        {
            return ((IEnumerable<ServiceDescriptionTable>)_Sdts).GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return ((IEnumerable<ServiceDescriptionTable>)_Sdts).GetEnumerator();
        }

        public override void Reset()
        {
            base.Reset();

            if (_Sdts != null)
                _Sdts.Clear();
        }

        private void Parse()
        {
            _Sdts.Clear();

            int len = SectionSize - 4; // 4 byte CRC am Ende abziehen.
            int i = 8; // start der Daten.

            _OrigNid |= SectionData[i++] << 8;
            _OrigNid |= SectionData[i++];

            i++; // 1 byte reserved for future.

            while (i < len)
            {
                ServiceDescriptionTable sdt = new ServiceDescriptionTable(SectionData, i);

                _Sdts.Add(sdt);

                i += sdt.Length;
            }
        }
    }
}
