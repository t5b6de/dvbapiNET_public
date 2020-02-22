using dvbapiNet.Dvb.Descriptors;
using System.IO;

namespace dvbapiNet.Oscam.Descriptors
{
    internal sealed class DemuxDevice : DescriptorBase
    {
        public byte Device { get; }

        public override int Length
        {
            get
            {
                return 3;
            }
        }

        public DemuxDevice(byte device)
            : base(DescriptorTag.DemuxDevice)
        {
            Device = device;
        }

        public override void Write(Stream s)
        {
            s.WriteByte((byte)_DescTag);
            s.WriteByte(1); // anzahl nachfolgender Bytes
            s.WriteByte(Device);
        }
    }
}
