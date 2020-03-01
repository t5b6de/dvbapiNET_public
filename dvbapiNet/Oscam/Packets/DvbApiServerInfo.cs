namespace dvbapiNet.Oscam.Packets
{
    /// <summary>
    /// Dvbapiserver info von oscam an diesen Client gesendet.
    /// </summary>
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
