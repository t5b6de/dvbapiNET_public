using dvbapiNet.Dvb.Descriptors;
using System.IO;

namespace dvbapiNet.Oscam.Descriptors
{
    /// <summary>
    /// OScam-spezifischer Descriptor für CaPMT zwecks Übergabe des verwendeten Adapters
    /// </summary>
    internal sealed class AdapterDevice : DescriptorBase
    {
        public byte Adapter { get; }

        public override int Length
        {
            get
            {
                return 3;
            }
        }

        public AdapterDevice(byte adapter)
            : base(DescriptorTag.AdapterDevice)
        {
            Adapter = adapter;
        }

        public override void Write(Stream s)
        {
            s.WriteByte((byte)_DescTag);
            s.WriteByte(1); // anzahl nachfolgender Bytes
            s.WriteByte(Adapter);
        }
    }
}
