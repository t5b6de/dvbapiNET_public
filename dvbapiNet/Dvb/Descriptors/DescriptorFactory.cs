namespace dvbapiNet.Dvb.Descriptors
{
    public static class DescriptorFactory
    {
        public static DescriptorBase CreateDescriptor(byte[] data, int offset)
        {
            DescriptorTag tag = (DescriptorTag)data[offset];

            switch (tag)
            {
                case DescriptorTag.ConditionalAccess:
                    return new CaDescriptor(data, offset);

                default:
                    return new DescriptorBase(data, offset);
            }
        }
    }
}
