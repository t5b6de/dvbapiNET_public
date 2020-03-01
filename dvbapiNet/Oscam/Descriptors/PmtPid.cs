using dvbapiNet.Dvb.Descriptors;
using System;
using System.IO;

namespace dvbapiNet.Oscam.Descriptors
{
    /// <summary>
    /// OScam-spezifischer Descriptor für CaPMT zwecks Übergabe des der PMT-Pid
    /// </summary>
    internal sealed class PmtPid : DescriptorBase
    {
        public override int Length
        {
            get
            {
                return 4;
            }
        }

        public ushort Pid { get; }

        public PmtPid(ushort pid)
            : base(DescriptorTag.PmtPid)
        {
            if (pid >= 0x1fff)
                throw new ArgumentOutOfRangeException("pid", "Pid außerhalb Wertebereich 0 - 1fff");

            Pid = pid;
        }

        public override void Write(Stream s)
        {
            s.WriteByte((byte)_DescTag);
            s.WriteByte(2); // anzahl nachfolgender Bytes
            s.WriteByte((byte)((Pid >> 8) & 0xff));
            s.WriteByte((byte)((Pid >> 0) & 0xff));
        }
    }
}
