using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace dvbapiNet.Oscam
{
    /// <summary>
    /// Ausnahme, die geworfen wird, wenn auf dem System bereits ein OScam Dvbapi-Client läuft.
    /// </summary>
    public class AlreadyRunningException : Exception
    {
        public AlreadyRunningException(string message, Exception inner)
            : base(message, inner) { }
    }
}
