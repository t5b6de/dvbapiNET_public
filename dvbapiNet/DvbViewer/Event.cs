namespace dvbapiNet.DvbViewer
{
    public enum Event : uint
    {
        Unload = 0,
        InitComplete = 3,
        KeyPressed = 4,
        Action = 5,
        Remote = 6,
        Mouse = 9,
        FinishSetChannel = 10,
        MouseMove = 11,
        TuneChannel = 999,
        RemoveChannel = 998,
        EnableTuner = 1000,
        DisableTuner = 1001
    }
}
