using dvbapiNet.Dvb.Descriptors;
using dvbapiNet.Dvb.Types;
using System;
using System.Collections.Generic;

namespace dvbapiNet.Dvb
{
    public sealed class PmtSection : SectionBase
    {
        private List<DescriptorBase> _Descs;
        private int _PcrPid;
        private List<ElementaryStream> _Streams;

        public int ServiceId
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

        public PmtSection(byte[] source, int offset, int pmtPid)
            : base(source, offset, pmtPid, true)
        {
            _Descs = new List<DescriptorBase>();
            _Streams = new List<ElementaryStream>();

            if (Finished)
                Parse();
        }

        public PmtSection(int pid)
            : base(pid, true)
        {
            _Descs = new List<DescriptorBase>();
            _Streams = new List<ElementaryStream>();
        }

        private bool PreCheck()
        {
            if (TableId != 0x02) // Dann keine Pmt-Tabelle.
            {
                Reset();
                return false;
            }

            Parse();
            return true;
        }

        public override bool AddPacket(byte[] tsPacket)
        {
            if (base.AddPacket(tsPacket))
                return PreCheck();

            return false;
        }

        public override bool AddPacket(IntPtr tsPacket)
        {
            if (base.AddPacket(tsPacket))
                return PreCheck();

            return false;
        }

        public DescriptorBase GetDescriptor(int index)
        {
            if (index >= _Descs.Count)
                return null;

            return _Descs[index];
        }

        public ElementaryStream GetEsInfo(int index)
        {
            if (index >= _Streams.Count)
                return null;

            return _Streams[index];
        }

        public override void Reset()
        {
            base.Reset();

            if (_Descs != null)
                _Descs.Clear();

            if (_Streams != null)
                _Streams.Clear();

            _PcrPid = -1;
        }

        private void Parse()
        {
            // ISO IEC 13818 seite 58 (pdf)

            _Descs.Clear();
            _Streams.Clear();
            _PcrPid = 0;

            int len = SectionSize - 4; // 4 byte CRC am Ende abziehen.
            int i = 8; // start der Daten.

            _PcrPid |= SectionData[i++] << 8;
            _PcrPid |= SectionData[i++];
            _PcrPid &= 0x1fff;

            int piLen = 0;

            piLen |= SectionData[i++] << 8;
            piLen |= SectionData[i++];
            piLen &= 0x0fff;
            // Ende der Daten:
            piLen += i;

            while (i < piLen)
            {
                DescriptorBase desc = DescriptorFactory.CreateDescriptor(SectionData, i);
                _Descs.Add(desc);

                i += desc.Length;
            }

            while (i < len)
            {
                ElementaryStream es = new ElementaryStream(SectionData, i);
                _Streams.Add(es);

                i += es.Length;
            }
        }
    }
}
