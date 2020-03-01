using System;
using System.Runtime.InteropServices;

namespace dvbapiNet.DvbViewer
{
    /// <summary>
    /// Callback-Structure für DVBViewer Transponder-Callback.
    /// </summary>
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct TTransCallData
    {
        /// <summary>
        /// Zeiger auf Delegat im Nichtverwaltetem Speicher
        /// </summary>
        public IntPtr TransCall;

        /// <summary>
        /// Parameter für den Delegaten
        /// </summary>
        public int Param;
    }
}
