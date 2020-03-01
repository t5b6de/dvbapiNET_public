using System.IO;
using System.Net;
using System.Text;

namespace dvbapiNet.Oscam.Packets
{
    /// <summary>
    /// ECM-Info mit informationen über letzte erfolgreiche ecm
    /// </summary>
    internal class EcmInfo
    {
        public int CaId
        {
            get; private set;
        }

        public string CardSystem
        {
            get; private set;
        }

        public int EcmTime
        {
            get; private set;
        }

        public int HopsCount
        {
            get; private set;
        }

        public int Pid
        {
            get; private set;
        }

        public string ProtocolName
        {
            get; private set;
        }

        public int ProviderId
        {
            get; private set;
        }

        public string ReaderName
        {
            get; private set;
        }

        public int ServiceId
        {
            get; private set;
        }

        public string SourceName
        {
            get; private set;
        }

        public EcmInfo(int serviceId, int caid,
            int pid, int provId, int ecmTime, int hops,
            string cardSystem, string readerName, string sourceName, string protocol
            )
        {
            ServiceId = serviceId;
            CaId = caid;
            Pid = pid;
            ProviderId = provId;
            EcmTime = ecmTime;
            HopsCount = hops;
            CardSystem = cardSystem;
            ReaderName = readerName;
            SourceName = sourceName;
            ProtocolName = protocol;
        }

        public static EcmInfo GetFromBinaryReader(BinaryReader rdr)
        {
            int serviceId;
            int caid;
            int pid;
            int provId;
            int ecmTime;
            int hops;
            string cardSystem;
            string readerName;
            string sourceName;
            string protocol;

            serviceId = IPAddress.NetworkToHostOrder(rdr.ReadInt16()) & 0xffff;
            caid = IPAddress.NetworkToHostOrder(rdr.ReadInt16()) & 0xffff;
            pid = IPAddress.NetworkToHostOrder(rdr.ReadInt16()) & 0xffff;
            provId = IPAddress.NetworkToHostOrder(rdr.ReadInt32());
            ecmTime = IPAddress.NetworkToHostOrder(rdr.ReadInt32());

            cardSystem = Encoding.UTF8.GetString(rdr.ReadBytes(rdr.ReadByte()));
            readerName = Encoding.UTF8.GetString(rdr.ReadBytes(rdr.ReadByte()));
            sourceName = Encoding.UTF8.GetString(rdr.ReadBytes(rdr.ReadByte()));
            protocol = Encoding.UTF8.GetString(rdr.ReadBytes(rdr.ReadByte()));

            hops = rdr.ReadByte();

            return new EcmInfo(
                serviceId, caid, pid,
                provId, ecmTime, hops,
                cardSystem, readerName, sourceName, protocol
             );
        }

        public override string ToString()
        {
            StringBuilder sb = new StringBuilder();

            sb.AppendLine("----------- ECM INFO -----------");
            sb.Append("Service ID: 0x");
            sb.AppendLine(ServiceId.ToString("x4"));
            sb.Append("PID:        0x");
            sb.AppendLine(Pid.ToString("x4"));
            sb.Append("Caid:       0x");
            sb.AppendLine(CaId.ToString("x4"));
            sb.Append("ProvId:     0x");
            sb.AppendLine(ProviderId.ToString("x6"));
            sb.Append("Cardsystem: ");
            sb.AppendLine(CardSystem);
            sb.Append("Reader:     ");
            sb.AppendLine(ReaderName);
            sb.Append("Source:     ");
            sb.AppendLine(SourceName);
            sb.Append("Protocol:   ");
            sb.AppendLine(ProtocolName);
            sb.Append("Hops:       ");
            sb.AppendLine(HopsCount.ToString());
            sb.Append("Time:       ");
            sb.Append(EcmTime.ToString());
            sb.AppendLine("ms");
            sb.AppendLine("----------- ECM INFO -----------");

            return sb.ToString();
        }
    }
}
