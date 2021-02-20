using System;
using System.Collections.Concurrent;
using System.Threading.Tasks;
using Windows.Networking;
using Windows.Networking.Sockets;
using Windows.Storage.Streams;

namespace Networking.RFC_Foundational
{
    public class TimeClient_Rfc_868
    {
        /// <summary>
        /// The network value returned by the WriteAsync() calls
        /// </summary>
        public class TimeResult
        {
            public static TimeResult MakeSucceeded(long unixTimeSeconds, string value, double time)
            {
                var retval = new TimeResult()
                {
                    Succeeded = true,
                    ExtendedError = null,
                    UnixTimeSeconds = unixTimeSeconds,
                    Value = value,
                    TimeInSeconds = time
                };
                if (time > 1000)
                {
                    ;
                }
                return retval;
            }
            public static TimeResult MakeFailed(Exception e, double time, string value = "")
            {
                var retval = new TimeResult()
                {
                    Succeeded = false,
                    ExtendedError = e,
                    Value = value,
                    TimeInSeconds = time
                };
                if (time > 1000)
                {
                    ;
                }
                return retval;
            }
            public static TimeResult MakeFailed(SocketErrorStatus status, double time, string value = "")
            {
                var retval = new TimeResult()
                {
                    Succeeded = false,
                    ExtendedError = null,
                    _ManuallySetStatus = status,
                    Value = value,
                    TimeInSeconds = time
                };
                if (time > 1000)
                {
                    ;
                }
                return retval;
            }

            public bool Succeeded { get; set; }
            public Exception ExtendedError { get; set; }
            private SocketErrorStatus _ManuallySetStatus = SocketErrorStatus.Unknown;
            public SocketErrorStatus Error { get { if (ExtendedError == null) return _ManuallySetStatus; return SocketError.GetStatus(ExtendedError.HResult); } }
            public string Value { get; set; }
            public long UnixTimeSeconds { get; set; } = 0;
            public double TimeInSeconds { get; set; }
        }


        public class ClientOptions
        {
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
            public Verbosity LoggingLevel { get; set; } = ClientOptions.Verbosity.Normal;
        }
        public ClientOptions Options { get; internal set; } = new ClientOptions();

        public class ClientStats
        {
            public int NWrites { get; set; } = 0;
            public int NExceptions { get; set; } = 0;
        }
        public ClientStats Stats { get; internal set; } = new ClientStats();

        public delegate void LogEventHandler(object sender, string str);
        public event LogEventHandler LogEvent;

        public TimeClient_Rfc_868(ClientOptions options = null)
        {
            if (options != null)
            {
                Options = options;
            }
        }

        private void Log(string str)
        {
            Log(ClientOptions.Verbosity.Normal, str);
        }
        private void Log(ClientOptions.Verbosity level, string str)
        {
            // e.g. level is Normal (1) and LoggingLevel is None (0), then should not log
            if (level <= Options.LoggingLevel)
            {
                LogEvent?.Invoke(this, str);
                System.Diagnostics.Debug.WriteLine(str);
            }
        }

        public async Task CloseAsync()
        {
            await Task.Delay(0);
        }

        public enum ProtocolType { Tcp, Udp }
        public async Task<TimeResult> WriteAsync(HostName address, string service = "10037", ProtocolType protocolType = ProtocolType.Udp)
        {
            switch (protocolType)
            {
                case ProtocolType.Tcp:
                    return await WriteTcpAsync(address, service);
                case ProtocolType.Udp:
                    return await WriteUdpAsync(address, service);
            }
            return TimeResult.MakeFailed(SocketErrorStatus.SocketTypeNotSupported, 0.0);
        }

        private async Task<TimeResult> WriteTcpAsync(HostName address, string service)
        {
            var startTime = DateTime.UtcNow;
            try
            {
                var tcpSocket = new StreamSocket();
                await tcpSocket.ConnectAsync(address, service);
                Stats.NWrites++;

                // Now read everything
                var s = tcpSocket.InputStream;
                var dr = new DataReader(s);
                var nbytes = await dr.LoadAsync(4); // Always gets 4 bytes
                if (nbytes >= 4)
                {
                    var retval = ReadDataReader(startTime, dr);
                    return retval;
                }

                var delta = DateTime.UtcNow.Subtract(startTime).TotalSeconds;
                return TimeResult.MakeFailed(SocketErrorStatus.NoDataRecordOfRequestedType, delta);
            }
            catch (Exception ex)
            {
                Stats.NExceptions++;
                Log($"ERROR: Client: Writing to {address} exception {ex.Message}");
                var delta = DateTime.UtcNow.Subtract(startTime).TotalSeconds;
                return TimeResult.MakeFailed(ex, delta);
            }
        }


        ConcurrentDictionary<string, TimeResult> UdpResults = new ConcurrentDictionary<string, TimeResult>();
        DateTime UdpStartTime;

        /// <summary>
        /// Sends out a query and then waits for the reply. Waiting on a UDP involves waiting for a message to come back in.
        /// </summary>
        private async Task<TimeResult> WriteUdpAsync(HostName address, string service)
        {
            UdpStartTime = DateTime.UtcNow;
            try
            {
                var udpSocket = new DatagramSocket(); //TODO: really use the same socket each time?
                await udpSocket.ConnectAsync(address, service);
                udpSocket.MessageReceived += UdpSocket_MessageReceived;

                // this is how to write an empty (blank) UDP datagram
                var b = new Windows.Storage.Streams.Buffer(0);
                await udpSocket.OutputStream.WriteAsync(b);
                Stats.NWrites++;

                Log(ClientOptions.Verbosity.Verbose, $"Client: UDP: Sent request on local port {udpSocket.Information.LocalPort}");

                // Wait for an answer

                const int START_DELAY_MS = 10;
                int currTotalDelay = 0;
                int currDelay = START_DELAY_MS;

                TimeResult udpResult = null;
                while (!UdpResults.TryRemove(udpSocket.Information.LocalPort, out udpResult))
                {
                    await Task.Delay(currDelay);
                    currTotalDelay += currDelay;
                    currDelay = Math.Min(currDelay * 2, Options.MaxPollLoopInMilliseconds); // Do an exponential backup up to max (10 seconds)
                    if (currTotalDelay >= Options.MaxWaitInMilliseconds)
                    {
                        Log($"ERROR: Client: reply from {address} took too long");
                        var delta = DateTime.UtcNow.Subtract(UdpStartTime).TotalSeconds;
                        udpResult = TimeResult.MakeFailed(SocketErrorStatus.ConnectionTimedOut, delta);
                        break;
                    }
                }
                return udpResult;
            }
            catch (Exception ex)
            {
                Stats.NExceptions++;
                Log($"ERROR: Client: Writing to {address} exception {ex.Message}");
                var delta = DateTime.UtcNow.Subtract(UdpStartTime).TotalSeconds;
                return TimeResult.MakeFailed(ex, delta);
            }
        }


        private void UdpSocket_MessageReceived(DatagramSocket sender, DatagramSocketMessageReceivedEventArgs args)
        {
            try
            {
                var dr = args.GetDataReader();
                var udpResult = ReadDataReader(UdpStartTime, dr);
                UdpResults.TryAdd(sender.Information.LocalPort, udpResult);
            }
            catch (Exception ex)
            {
                // This should not every actually happen
                Stats.NExceptions++;
                Log($"TIME: CLIENT: ERROR {ex.Message}");
                var delta = DateTime.UtcNow.Subtract(UdpStartTime).TotalSeconds;
                var udpResult = TimeResult.MakeFailed(ex, delta);
                UdpResults.TryAdd(sender.Information.LocalPort, udpResult);
            }

            // Don't need the socket after we get the first message
            sender.MessageReceived -= UdpSocket_MessageReceived;
            sender.Dispose();
        }

        private TimeResult ReadDataReader(DateTime startTime, DataReader dr)
        {
            uint count = dr.UnconsumedBufferLength;
            if (count >= 4)
            {
                var rawdata = dr.ReadUInt32();
                var result = TimeServer_Rfc_868.TimeConversion.Convert(rawdata);

                // Convert to now

                var stringresult = $"{result.ToString()} raw={rawdata} {rawdata:X}";
                Log($"{stringresult}"); // Will be printed on the screen.
                var delta = DateTime.UtcNow.Subtract(startTime).TotalSeconds;
                return TimeResult.MakeSucceeded(result.ToUnixTimeSeconds(), stringresult, delta);
            }
            else // socket is done
            {
                var stringresult = "ERROR:No results!";
                Log($"TIME: CLIENT:{stringresult}");
                var delta = DateTime.UtcNow.Subtract(startTime).TotalSeconds;
                return TimeResult.MakeFailed(SocketErrorStatus.HttpInvalidServerResponse, delta);
            }
        }
    }
}
