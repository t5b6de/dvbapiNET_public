namespace dvbapiNet.Oscam.Packets
{
    internal class DvbApiServerInfo
    {
        private string _Info;
        private int _Proto;

        public int ProtocolVersion
        {
            get
            {
                return _Proto;
            }
        }

        public string Server
        {
            get
            {
                return _Info;
            }
        }

        public DvbApiServerInfo(int proto, string server)
        {
            _Info = server;
            _Proto = proto;
        }
    }
}
