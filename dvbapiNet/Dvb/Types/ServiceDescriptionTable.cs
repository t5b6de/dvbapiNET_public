using dvbapiNet.Dvb.Descriptors;
using System.Collections.Generic;

namespace dvbapiNet.Dvb.Types
{
    public class ServiceDescriptionTable
    {
        private List<DescriptorBase> _Descs;
        private bool _EitPresentFollowing;
        private bool _EitSched;
        private bool _FreeCA;
        private int _Len;
        private int _RunningStatus;
        private int _Sid;

        public bool EitPresentFollowing
        {
            get
            {
                return _EitPresentFollowing;
            }
        }

        public bool EitScheduleFlag
        {
            get
            {
                return _EitSched;
            }
        }

        public bool FreeCA
        {
            get
            {
                return _FreeCA;
            }
        }

        public int Length
        {
            get
            {
                return _Len + 5;
                // Gesamtlänge inkl service_id (2), reserved future+ Flags (1) + length(2)
            }
        }

        public int RunningStatus
        {
            get
            {
                return _RunningStatus;
            }
        }

        public int ServiceId
        {
            get
            {
                return _Sid;
            }
        }

        public ServiceDescriptionTable(byte[] data, int offset)
        {
            _Descs = new List<DescriptorBase>();

            int i = 0;
            int tmp = 0;
            _Sid = data[(i++) + offset] << 8;
            _Sid |= data[(i++) + offset];

            tmp = data[(i++) + offset];

            _EitSched = (tmp & 0x02) != 0;

            _EitPresentFollowing = (tmp & 0x01) != 0;

            _Len = data[(i++) + offset] << 8;
            _Len |= data[(i++) + offset];

            _RunningStatus = _Len >> 13;
            _FreeCA = (_Len & 0x1000) != 0;

            _Len &= 0x0fff;

            while (i < Length)
            {
                DescriptorBase desc = DescriptorFactory.CreateDescriptor(data, i + offset);
                _Descs.Add(desc);

                i += desc.Length;
            }
        }
    }
}
