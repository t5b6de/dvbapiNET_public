namespace dvbapiNet.Dvb.Crypto
{
    /// <summary>
    /// Auflistung Verfügbarer Descrambler-Algorithmen
    /// </summary>
    public enum DescramblerAlgo : int
    {
        DvbCsa = 0,
        Des = 1,
        Aes128 = 2,
        DvbCsaAlt = 3,
    }
}
