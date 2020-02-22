using dvbapiNet.Dvb.Descriptors;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;

namespace dvbapiNet.Oscam.Descriptors
{
    /// <summary>
    /// Stellt einen EnigmaNamespaceDescriptor dar, hier eine unvollständige minimalimplementierung
    /// zwecks CAPMT Update bei Oscam.
    /// </summary>
    internal class EnigmaNamespace : DescriptorBase
    {
        public int TransportStreamId { get; }
        public int NetworkId { get; }

        public override int Length
        {
            get
            {
                return 10;
            }
        }

        public EnigmaNamespace(int nId, int tsId)
            : base(DescriptorTag.EnigmaNamespace)
        {
            TransportStreamId = tsId;
            NetworkId = nId;
        }

        public override void Write(Stream s)
        {
            using (MemoryStream ms = new MemoryStream())
            using (BinaryWriter wr = new BinaryWriter(ms))
            {
                wr.Write((byte)_DescTag);
                wr.Write((byte)8);
                wr.Write((uint)0xffffffff); // ens, nicht verfügbar also alles 1
                wr.Write(IPAddress.HostToNetworkOrder((short)TransportStreamId));
                wr.Write(IPAddress.HostToNetworkOrder((short)NetworkId));
                wr.Flush();

                s.Write(ms.ToArray(), 0, (int)ms.Position);
            }
        }
    }
}
