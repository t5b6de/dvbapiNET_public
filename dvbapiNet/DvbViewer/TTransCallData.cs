using System;
using System.Runtime.InteropServices;

namespace dvbapiNet.DvbViewer
{
    // Struct zur Kopie in Nichtverwaltetem Speicher mit Zeiger auf Delegat.
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct TTransCallData
    {
        public IntPtr TransCall;
        public int Param;
    }
}
