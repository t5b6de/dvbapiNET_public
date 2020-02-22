namespace dvbapiNet.Dvb.Types
{
    public class ProgramAssociation
    {
        private int _Pid;
        private int _Sid;

        public bool IsNetworkPid
        {
            get
            {
                return _Sid == 0;
            }
        }

        public int NetworkPid
        {
            get
            {
                if (IsNetworkPid)
                    return _Pid;

                return -1;
            }
        }

        public int ProgramPid
        {
            get
            {
                if (!IsNetworkPid)
                    return _Pid;

                return -1;
            }
        }

        public int Sid
        {
            get
            {
                return _Sid;
            }
        }

        public ProgramAssociation(int num, int pid)
        {
            _Sid = num;
            _Pid = pid;
        }

        /// <summary>
        /// Erstellt eine neue Instanz von ProgramAssociation anhand der Section-Daten
        /// Dieses Element benötigt 4 Byte in der Section.
        /// </summary>
        /// <param name="data">Section-Daten-Array</param>
        /// <param name="offset">offset im Array.</param>
        public ProgramAssociation(byte[] data, int offset)
        {
            _Sid = data[0 + offset] << 8;
            _Sid |= data[1 + offset];

            _Pid = data[2 + offset] << 8;
            _Pid |= data[3 + offset];
            _Pid &= 0x1fff;
        }
    }
}
