namespace dvbapiNet.Dvb.Descriptors
{
    /// <summary>
    /// Stellt eine Factory für Descriptoren bereit
    /// </summary>
    public static class DescriptorFactory
    {
        /// <summary>
        /// Instantiiert einen Descriptor abhängig vom Inhalt.
        /// </summary>
        /// <param name="data">Descriptor-Daten</param>
        /// <param name="offset">Offset im Quellarray</param>
        /// <returns>Descriptor anhand des Inhalts</returns>
        public static DescriptorBase CreateDescriptor(byte[] data, int offset)
        {
            DescriptorTag tag = (DescriptorTag)data[offset];

            switch (tag)
            {
                case DescriptorTag.ConditionalAccess:
                    return new CaDescriptor(data, offset);
                // TODO: bei Bedarf weitere Descriptoren hier einfügen
                default:
                    return new DescriptorBase(data, offset);
            }
        }
    }
}
