using System;
using System.IO;
using System.Text;

namespace dvbapiNet.Oscam.Packets
{
    /// <summary>
    /// Dvbapiclientinfo, packet für die Richtung vom Dvbapiclient zum Server
    /// </summary>
    internal class DvbApiClientInfo
    {
        private string _Info;
        private int _Proto;

        public DvbApiClientInfo(int version, string info)
        {
            _Info = info;
            _Proto = version;
        }

        public byte[] GetData()
        {
            byte[] data;

            using (MemoryStream ms = new MemoryStream())
            {
                uint cmd = (uint)DvbApiCommand.ClientInfo;
                byte[] info = Encoding.UTF8.GetBytes(_Info);

                ms.WriteByte((byte)(cmd >> 24));
                ms.WriteByte((byte)(cmd >> 16));
                ms.WriteByte((byte)(cmd >> 8));
                ms.WriteByte((byte)(cmd));

                ms.WriteByte((byte)(_Proto >> 8));
                ms.WriteByte((byte)(_Proto));

                ms.WriteByte((byte)Math.Min(info.Length, 255));
                ms.Write(info, 0, Math.Min(info.Length, 255));

                data = ms.ToArray();
            }

            return data;
        }
    }
}
