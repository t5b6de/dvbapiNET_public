namespace dvbapiNet.Oscam.InterCom
{
    /// <summary>
    /// Ereignis wenn ein Befehl empfangen wurde.
    /// </summary>
    /// <param name="cmd"></param>
    /// <param name="data"></param>
    internal delegate void Command(InterComEndPoint sender, InterComCommand cmd, byte[] data);

    /// <summary>
    /// einfacher Generischer Aufruf ohne weitere Parameter.
    /// </summary>
    /// <param name="sender"></param>
    internal delegate void Generic(InterComEndPoint sender);
}
