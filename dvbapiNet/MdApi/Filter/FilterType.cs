namespace dvbapiNet.MdApi.Filter
{
    internal enum FilterType : byte
    {
        Packet184 = 0,
        Packet920 = 1,
        PsiSection = 2,
        PesSection = 3,
        Ts = 4,
        RawTs = 5
    }
}
