using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Networking.RFC_PortScan
{
    class PortScanOptions
    {
        public int MaxOutstandingRequests { get; set; } = 3;


        [FlagsAttribute]
        public enum Protocol {  Daytime, Echo, Finger, Time };
        public Protocol ScanProtocol { get; set; } = Protocol.Daytime | Protocol.Time;

        /// <summary>
        /// Maximum total wait time for a connection. Keep it short: good servers are generally very fast to connect.
        /// </summary>
        public int MaxConnectTimeInMilliseconds { get; set; } = 1_000;

        /// <summary>
        /// Maximum total wait time for an answer
        /// </summary>
        public int MaxWaitInMilliseconds { get; set; } = 10_000;
        /// <summary>
        /// Maximum single-loop poll time for an answer; the client does an exponential backoff
        /// up to MaxPollLoopInMilliseconds. Once the client has waited a total of MaxWaitInMilliseconds,
        /// the client will give up.
        /// </summary>
        public int MaxPollLoopInMilliseconds { get; set; } = 1_000;
        public enum Verbosity { None, Normal, Verbose }
        public Verbosity LoggingLevel { get; set; } = PortScanOptions.Verbosity.Normal;
    }
}
