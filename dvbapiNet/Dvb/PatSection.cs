using dvbapiNet.Dvb.Types;
using System;
using System.Collections;
using System.Collections.Generic;

namespace dvbapiNet.Dvb
{
    public sealed class PatSection : SectionBase, IEnumerable<ProgramAssociation>
    {
        private List<ProgramAssociation> _PAssocs;

        public PatSection()
            : base(0, true)
        {
            _PAssocs = new List<ProgramAssociation>();
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

        private bool Parse()
        {
            if (TableId != 0x00) // Dann keine Pat-Tabelle.
            {
                Reset();
                return false;
            }

            _PAssocs.Clear();

            int len = SectionSize - 4; // 4 byte CRC am Ende abziehen.
            int i = 8; // start der ProgrammDaten.

            while (i < len)
            {
                _PAssocs.Add(new ProgramAssociation(SectionData, i));

                i += 4; // eine ProgramAssociation benötigt 4 byte.
            }

            return true;
        }

        public override bool AddPacket(byte[] tsPacket)
        {
            if (base.AddPacket(tsPacket))
                return Parse();

            return false;
        }

        public override bool AddPacket(IntPtr tsPacket)
        {
            if (base.AddPacket(tsPacket))
                return Parse();

            return false;
        }

        public IEnumerator<ProgramAssociation> GetEnumerator()
        {
            return ((IEnumerable<ProgramAssociation>)_PAssocs).GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return ((IEnumerable<ProgramAssociation>)_PAssocs).GetEnumerator();
        }

        public int GetNetworkPid()
        {
            if (Finished)
            {
                foreach (ProgramAssociation pa in _PAssocs)
                {
                    if (pa.IsNetworkPid)
                        return pa.NetworkPid;
                }
            }

            return -1;
        }

        public int GetPmtPidBySid(int sid)
        {
            if (Finished)
            {
                foreach (ProgramAssociation pa in _PAssocs)
                {
                    if (pa.Sid == sid)
                        return pa.ProgramPid;
                }
            }

            return -1;
        }

        public override void Reset()
        {
            base.Reset();

            if (_PAssocs != null) // bei Initialisierung wichtig.
                _PAssocs.Clear();
        }
    }
}
