using Networking.RFC_Foundational;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Networking.RFC_PortScan
{
    /// <summary>
    /// Converts a PortScanOption into the specific type of option for a protocol. Has deep knowledge of both the port scan option and the individual options
    /// </summary>
    static class ClientOptionFactory
    {
        public static DaytimeClient_Rfc_867.ClientOptions MakeDaytime (PortScanOptions option)
        {
            var retval = new DaytimeClient_Rfc_867.ClientOptions();
            retval.LoggingLevel = (DaytimeClient_Rfc_867.ClientOptions.Verbosity)option.LoggingLevel;
            return retval;
        }
    }
}
