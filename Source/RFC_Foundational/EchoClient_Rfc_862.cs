using Networking.Utilities;
using System;
using System.Collections.Concurrent;
using System.Text;
using System.Threading.Tasks;
using Windows.Networking;
using Windows.Networking.Sockets;
using Windows.Storage.Streams;

namespace Networking.RFC_Foundational
{
    public class EchoClient_Rfc_862: IDisposable
    {
        /// <summary>
        /// The network value returned by the WriteAsync() calls
        /// </summary>
        public class EchoResult
        {
            public static EchoResult MakeInProgress(double time)
            {
                var retval = new EchoResult()
                {
                    Succeeded = State.InProgress,
                    ExtendedError = null,
                    Value = "",
                    TimeInSeconds = time
                };
                return retval;
            }
            public static EchoResult MakeSucceeded(string value, double time)
            {
                var retval = new EchoResult()
                {
                    Succeeded = State.Succeeded,
                    ExtendedError = null,
                    Value = value,
                    TimeInSeconds = time
                };
                return retval;
            }
            public static EchoResult MakeFailed(Exception e, double time, string value = "")
            {
                var retval = new EchoResult()
                {
                    Succeeded = State.Failed,
                    ExtendedError = e,
                    Value = value,
                    TimeInSeconds = time
                };
                return retval;
            }
            public static EchoResult MakeFailed(SocketErrorStatus status, double time, string value = "")
            {
                var retval = new EchoResult()
                {
                    Succeeded = State.Failed,
                    ExtendedError = null,
                    _ManuallySetStatus = status,
                    Value = value,
                    TimeInSeconds = time
                };
                return retval;
            }
            public enum State { NotStarted, Failed, InProgress, Succeeded};
            public State Succeeded { get; set; } = State.NotStarted;
            public Exception ExtendedError { get; set; }
            private SocketErrorStatus _ManuallySetStatus = SocketErrorStatus.Unknown;
            public SocketErrorStatus Error { get { if (ExtendedError == null) return _ManuallySetStatus; return SocketError.GetStatus(ExtendedError.HResult); } }
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

        DatagramSocket udpSocket;
        StreamSocket tcpSocket;
        DataWriter tcpDw;
        DataWriter udpDw;
        public delegate void LogEventHandler(object sender, string str);
        public event LogEventHandler LogEvent;

        public EchoClient_Rfc_862(ClientOptions options = null)
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

        public async void Dispose()
        {
            await CloseAsync();
        }

        public async Task<EchoResult> CloseAsync()
        {
            if (tcpSocket != null)
            {
                await tcpDw.FlushAsync();
                tcpDw.Dispose();
                tcpSocket.Dispose();
                tcpSocket = null;
                tcpDw = null;

                // Wait for the TcpReadTask to finish
                if (TcpReadTask != null)
                {
                    await TcpReadTask;
                }
            }
            return TcpReadEchoResult; // Not really fully correct.
        }

        Task TcpReadTask = null;
        EchoResult TcpReadEchoResult = null;
        DateTime TcpStartTime;

        public enum ProtocolType {  Tcp, Udp }
        public async Task<EchoResult> WriteAsync(HostName address, string service, ProtocolType protocolType, string data)
        {
            switch (protocolType)
            {
                case ProtocolType.Tcp:
                    return await WriteTcpAsync(address, service, data);
                case ProtocolType.Udp:
                    return await WriteUdpAsync(address, service, data);
            }
            return EchoResult.MakeFailed(SocketErrorStatus.SocketTypeNotSupported, 0.0);
        }


        private async Task<EchoResult> WriteTcpAsync(HostName address, string service, string data)
        {
            var startTime = DateTime.UtcNow;
            try
            {
                if (tcpSocket == null)
                {
                    tcpSocket = new StreamSocket();
                    await tcpSocket.ConnectAsync(address, service);
                    tcpDw = new DataWriter(tcpSocket.OutputStream);
                    TcpStartTime = startTime;

                    // Now read everything
                    TcpReadEchoResult = EchoResult.MakeInProgress(0); // Taken no time at all so far :-)
                    var dr = new DataReader(tcpSocket.InputStream);
                    dr.InputStreamOptions = InputStreamOptions.Partial;
                    TcpReadTask = ReadTcpAsync(dr);
                }

                tcpDw.WriteString(data);
                await tcpDw.StoreAsync();
                Stats.NWrites++;

                var delta = DateTime.UtcNow.Subtract(UdpStartTime).TotalSeconds;
                return EchoResult.MakeInProgress(delta);
            }
            catch (Exception ex)
            {
                Stats.NExceptions++;
                Log($"ERROR: Client: Writing {data} to {address} exception {ex.Message}");
                var delta = DateTime.UtcNow.Subtract(UdpStartTime).TotalSeconds;
                return EchoResult.MakeFailed(ex, delta);
            }
        }

        ConcurrentDictionary<string, EchoResult> UdpResults = new ConcurrentDictionary<string, EchoResult>();
        DateTime UdpStartTime;

        private async Task<EchoResult> WriteUdpAsync(HostName address, string service, string data)
        {
            UdpStartTime = DateTime.UtcNow;
            try
            {
                if (udpSocket == null)
                {
                    udpSocket = new DatagramSocket();
                    await udpSocket.ConnectAsync(address, service);
                    udpDw = new DataWriter(udpSocket.OutputStream);

                    // Now read everything
                    udpSocket.MessageReceived += UdpSocket_MessageReceived;
                }
                var dw = udpDw;

                if (!string.IsNullOrEmpty(data))
                {
                    // A blank string, when written to a data writer, won't actually result in a 
                    // UDP packet being sent. For the special case of not sending any data,
                    // use the WriteAsync on the socket's OutputStream directly.
                    udpDw.WriteString(data);
                    await udpDw.StoreAsync();
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

                EchoResult udpResult = null;
                while (!UdpResults.TryRemove(udpSocket.Information.LocalPort, out udpResult))
                {
                    await Task.Delay(currDelay);
                    currTotalDelay += currDelay;
                    currDelay = Math.Min(currDelay * 2, Options.MaxPollLoopInMilliseconds); // Do an exponential backup up to max (10 seconds)
                    if (currTotalDelay >= Options.MaxWaitInMilliseconds)
                    {
                        Log($"ERROR: Client: reply from {address} took too long (outgoing data={data})");
                        var delta = DateTime.UtcNow.Subtract(UdpStartTime).TotalSeconds;
                        udpResult = EchoResult.MakeFailed(SocketErrorStatus.ConnectionTimedOut, delta);
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
                return EchoResult.MakeFailed(ex, delta);
            }
        }

        private void UdpSocket_MessageReceived(DatagramSocket sender, DatagramSocketMessageReceivedEventArgs args)
        {
            try
            {
                var dr = args.GetDataReader();
                dr.InputStreamOptions = InputStreamOptions.Partial; // | InputStreamOptions.ReadAhead;
                var udpResult = ReadUdp(dr);
                UdpResults.TryAdd(sender.Information.LocalPort, udpResult);
            }
            catch (Exception ex)
            {
                // This can happen when we send a packet to a correct host (like localhost) but with an
                // incorrect service. The packet will "bounce", resuluting in a MessageReceived event
                // but with an args with no real data.
                var delta = DateTime.UtcNow.Subtract(UdpStartTime).TotalSeconds;
                var udpResult = EchoResult.MakeFailed(ex, delta);
                UdpResults.TryAdd(sender.Information.LocalPort, udpResult);
            }
        }
        private EchoResult ReadUdp(DataReader dr)
        {
            uint count = dr.UnconsumedBufferLength;
            if (count > 0)
            {
                byte[] buffer = new byte[dr.UnconsumedBufferLength];
                dr.ReadBytes(buffer);
                var stringresult = BufferToString.ToString(buffer);
                LogEchoBuffer(buffer);
                var delta = DateTime.UtcNow.Subtract(UdpStartTime).TotalSeconds;
                return EchoResult.MakeSucceeded(stringresult, delta);
            }
            else // socket is done
            {
                var delta = DateTime.UtcNow.Subtract(UdpStartTime).TotalSeconds;
                return EchoResult.MakeFailed(SocketErrorStatus.HttpInvalidServerResponse, delta);
            }
        }

        private async Task ReadTcpAsync(DataReader dr)
        {
            try
            {
                uint count = 0;
                do
                {
                    await dr.LoadAsync(2048); // Will throw 'thread exit or app request' when the socket is Disposed
                    count = dr.UnconsumedBufferLength;
                    if (count > 0)
                    {
                        byte[] buffer = new byte[dr.UnconsumedBufferLength];
                        dr.ReadBytes(buffer);
                        string str = Utilities.BufferToString.ToString(buffer);
                        TcpReadEchoResult.Value += str;
                        LogEchoBuffer(buffer);
                    }
                    else // socket is done
                    {
                    }
                }
                while (count > 0);
            }
            catch (Exception ex)
            {
                Log($"ECHO: CLIENT: Exception while reading {ex.Message} 0x{ex.HResult:X}");
                Stats.NExceptions++;
            }
            var delta = DateTime.UtcNow.Subtract(TcpStartTime).TotalSeconds;
            TcpReadEchoResult.TimeInSeconds = delta;
            if (TcpReadEchoResult.Succeeded == EchoResult.State.InProgress)
            {
                TcpReadEchoResult.Succeeded = EchoResult.State.Succeeded; // TODO: Really succeeded? Or are we just kind of faking it?
            }
        }

        private void LogEchoBuffer(byte[] buffer)
        {
            var sb = new StringBuilder();
            foreach (var b in buffer)
            {
                if (b >= 32 && b < 128)
                {
                    sb.Append((char)b);
                }
                else
                {
                    sb.Append($"0x{b:X2}");
                }
            }
            Log($"ECHO: CLIENT: got {sb.ToString()}");
        }
    }
}
