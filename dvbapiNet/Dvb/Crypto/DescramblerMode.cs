namespace dvbapiNet.Dvb.Crypto
{
    /// <summary>
    /// Auflistung verfügbarer Entschlüsselungsmodi für DES und AES
    /// </summary>
    public enum DescramblerMode : int
    {
        Ecb = 0,
        Cbc = 1,
    }
}
