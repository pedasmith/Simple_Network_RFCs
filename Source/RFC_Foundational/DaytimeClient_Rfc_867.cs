using Networking.Utilities;
using System;
using System.Collections.Concurrent;
using System.Threading.Tasks;
using Windows.Networking;
using Windows.Networking.Sockets;
using Windows.Storage.Streams;

namespace Networking.RFC_Foundational
{
    public class DaytimeClient_Rfc_867
    {
        /// <summary>
        /// The network value returned by the WriteAsync() calls
        /// </summary>
        public class DaytimeResult
        {
            public static DaytimeResult MakeSucceeded(string value, double time)
            {
                var retval = new DaytimeResult()
                {
                    Succeeded = true,
                    ExtendedError = null,
                    Value = value,
                    TimeInSeconds = time
                };
                return retval;
            }
            public static DaytimeResult MakeFailed(Exception e, double time, string value = "")
            {
                var retval = new DaytimeResult()
                {
                    Succeeded = false,
                    ExtendedError = e,
                    Value = value,
                    TimeInSeconds = time
                };
                return retval;
            }
            public static DaytimeResult MakeFailed(SocketErrorStatus status, double time, string value = "")
            {
                var retval = new DaytimeResult()
                {
                    Succeeded = false,
                    ExtendedError = null,
                    _ManuallySetStatus = status,
                    Value = value,
                    TimeInSeconds = time
                };
                return retval;
            }

            public bool Succeeded { get; set; }
            public Exception ExtendedError { get; set; }
            private SocketErrorStatus _ManuallySetStatus = SocketErrorStatus.Unknown; 
            public SocketErrorStatus Error {  get { if (ExtendedError == null) return _ManuallySetStatus; return SocketError.GetStatus(ExtendedError.HResult); } }
            public string Value { get; set; }
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

        public DaytimeClient_Rfc_867(ClientOptions options =  null)
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
        public async Task<DaytimeResult> WriteAsync(HostName address, string service = "10013", ProtocolType protocolType=ProtocolType.Udp, string data="")
        {
            switch (protocolType)
            {
                case ProtocolType.Tcp:
                    return await WriteAsyncTcp(address, service, data); //TODO: rename to WriteTcpAsync everywhere and WriteUdpAsync, too
                case ProtocolType.Udp:
                    return await WriteAsyncUdp(address, service, data);
            }
            return DaytimeResult.MakeFailed (SocketErrorStatus.SocketTypeNotSupported, 0.0);
        }

        private async Task<DaytimeResult> WriteAsyncTcp(HostName address, string service, string data)
        {
            var startTime = DateTime.UtcNow;
            try
            {
                var tcpSocket = new StreamSocket();
                await tcpSocket.ConnectAsync(address, service);
                // Everything that's sent will be ignored.
                if (!string.IsNullOrEmpty(data))
                {
                    var dw = new DataWriter(tcpSocket.OutputStream);
                    dw.WriteString(data);
                    await dw.StoreAsync();
                }
                Stats.NWrites++;

                // Now read everything
                var s = tcpSocket.InputStream;
                var buffer = new Windows.Storage.Streams.Buffer(2048);

                string stringresult = "";
                var keepGoing = true;
                while (keepGoing)
                {
                    try
                    {
                        var read = s.ReadAsync(buffer, buffer.Capacity, InputStreamOptions.Partial);
                        /* This is the syntax that the editor will suggest. There's a 
                         * much simpler syntax (below) that's syntactic sugar over this.
                        read.Progress = new AsyncOperationProgressHandler<IBuffer, uint>(
                            (operation, progress) =>
                            {
                                var err = operation.ErrorCode == null ? "null" : operation.ErrorCode.ToString();
                                Log(ClientOptions.Verbosity.Verbose, $"DBG: Daytime Progress count={progress} status={operation.Status} errorcode={err}");
                            });
                        */
                        read.Progress = (operation, progress) =>
                            {
                                var err = operation.ErrorCode == null ? "null" : operation.ErrorCode.ToString();
                                Log(ClientOptions.Verbosity.Verbose, $"DBG: Daytime Progress count={progress} status={operation.Status} errorcode={err}");
                            };
                        var result = await read;
                        if (result.Length != 0)
                        {
                            var partialresult = BufferToString.ToString(result);
                            stringresult += partialresult;
                            Log($"{stringresult}"); // This will be printed on the user's screen.
                        }
                        else
                        {
                            keepGoing = false;
                            Log(ClientOptions.Verbosity.Verbose, $"Read completed with zero bytes; closing");
                        }
                    }
                    catch (Exception ex2)
                    {
                        keepGoing = false;
                        Stats.NExceptions++;
                        Log($"EXCEPTION while reading: {ex2.Message} {ex2.HResult:X}");

                        var faildelta = DateTime.UtcNow.Subtract(startTime).TotalSeconds;
                        return DaytimeResult.MakeFailed (ex2, faildelta);
                    }
                }

                var delta = DateTime.UtcNow.Subtract(startTime).TotalSeconds;
                return DaytimeResult.MakeSucceeded(stringresult, delta);

                //var dr = new DataReader(tcpSocket.InputStream);
                //dr.InputStreamOptions = InputStreamOptions.Partial;
                //ReadTask = ReadAsync(dr, DataReaderType.Stream);
                //tcpSocket.Dispose();
                //tcpSocket = null;
            }
            catch (Exception ex)
            {
                Stats.NExceptions++;
                Log($"ERROR: Client: Writing {data} to {address} exception {ex.Message}");
                var delta = DateTime.UtcNow.Subtract(startTime).TotalSeconds;
                return DaytimeResult.MakeFailed(ex, delta);
            }
        }

        private void TcpReadProgress (uint count)
        {

        }

        ConcurrentDictionary<string, DaytimeResult> UdpResults = new ConcurrentDictionary<string, DaytimeResult>();
        DateTime UdpStartTime;

        /// <summary>
        /// Sends out a query and then waits for the reply. Waiting on a UDP involves waiting for a message to come back in.
        /// </summary>
        private async Task<DaytimeResult> WriteAsyncUdp(HostName address, string service, string data)
        {
            UdpStartTime = DateTime.UtcNow;
            try
            {
                var udpSocket = new DatagramSocket(); //TODO: really use the same socket each time?
                await udpSocket.ConnectAsync(address, service);
                udpSocket.MessageReceived += UdpSocket_MessageReceived;

                if (!string.IsNullOrEmpty(data))
                {
                    // A blank string, when written to a data writer, won't actually result in a 
                    // UDP packet being sent. For the special case of not sending any data,
                    // use the WriteAsync on the socket's OutputStream directly.
                    var dw = new DataWriter(udpSocket.OutputStream);
                    dw.WriteString(data); 
                    await dw.StoreAsync();
                    Stats.NWrites++;
                }
                else
                {
                    var b = new Windows.Storage.Streams.Buffer(0);
                    await udpSocket.OutputStream.WriteAsync(b);
                    Stats.NWrites++;
                }
                Log(ClientOptions.Verbosity.Verbose, $"Client: UDP: Sent request on local port {udpSocket.Information.LocalPort} request {data}");

                // Wait for an answer

                const int START_DELAY_MS = 10;
                int currTotalDelay = 0;
                int currDelay = START_DELAY_MS;

                DaytimeResult udpResult = null;
                while (!UdpResults.TryRemove(udpSocket.Information.LocalPort, out udpResult))
                {
                    await Task.Delay(currDelay);
                    currTotalDelay += currDelay;
                    currDelay = Math.Min(currDelay * 2, Options.MaxPollLoopInMilliseconds); // Do an exponential backup up to max (10 seconds)
                    if (currTotalDelay >= Options.MaxWaitInMilliseconds)
                    {
                        Log($"ERROR: Client: reply from {address} took too long (outgoing data={data})");
                        var delta = DateTime.UtcNow.Subtract(UdpStartTime).TotalSeconds;
                        udpResult = DaytimeResult.MakeFailed(SocketErrorStatus.ConnectionTimedOut, delta);
                        break;
                    }
                }
                return udpResult;
            }
            catch (Exception ex)
            {
                Stats.NExceptions++;
                Log($"ERROR: Client: Writing {data} to {address} exception {ex.Message}");
                var delta = DateTime.UtcNow.Subtract(UdpStartTime).TotalSeconds;
                return DaytimeResult.MakeFailed(ex, delta);
            }
        }


        private void UdpSocket_MessageReceived(DatagramSocket sender, DatagramSocketMessageReceivedEventArgs args)
        {
            try
            {
                var dr = args.GetDataReader();
                var udpResult = ReadUdp(dr);
                UdpResults.TryAdd(sender.Information.LocalPort, udpResult);
            }
            catch (Exception ex)
            {
                // This should not every actually happen
                Stats.NExceptions++;
                Log($"DAYTIME: CLIENT: ERROR {ex.Message}");
                var delta = DateTime.UtcNow.Subtract(UdpStartTime).TotalSeconds;
                var udpResult = DaytimeResult.MakeFailed(ex, delta);
                UdpResults.TryAdd(sender.Information.LocalPort, udpResult);
            }

            // Don't need the socket after we get the first message
            sender.MessageReceived -= UdpSocket_MessageReceived;
            sender.Dispose();
        }

        private DaytimeResult ReadUdp(DataReader dr) //TODO: directly read buffer instead of weirding out the DataReader?
        {
            uint count = dr.UnconsumedBufferLength;
            if (count > 0)
            {
                byte[] buffer = new byte[dr.UnconsumedBufferLength];
                dr.ReadBytes(buffer);
                var stringresult = BufferToString.ToString(buffer);
                Log($"{stringresult}"); // Will be printed on the screen.
                var delta = DateTime.UtcNow.Subtract(UdpStartTime).TotalSeconds;
                return DaytimeResult.MakeSucceeded(stringresult, delta);
            }
            else
            {
                var stringresult = "ERROR:No results!";
                Log($"DAYTIME: CLIENT:{stringresult}");
                var delta = DateTime.UtcNow.Subtract(UdpStartTime).TotalSeconds;
                return DaytimeResult.MakeFailed(SocketErrorStatus.HttpInvalidServerResponse, delta);
            }
        }
    }
}
