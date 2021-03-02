using Networking.Utilities;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Windows.Networking;
using Windows.Networking.Sockets;
using Windows.Storage.Streams;

namespace Networking.RFC_Foundational
{
    public class CharGenClient_Rfc_864 : IDisposable
    {
        /// <summary>
        /// The network value returned by the WriteAsync() calls
        /// </summary>
        public class CharGenResult
        {
            public static CharGenResult MakeInProgress(double time)
            {
                var retval = new CharGenResult()
                {
                    Succeeded = State.InProgress,
                    ExtendedError = null,
                    Value = "",
                    TimeInSeconds = time
                };
                return retval;
            }
            public static CharGenResult MakeSucceeded(string value, double time)
            {
                var retval = new CharGenResult()
                {
                    Succeeded = State.Succeeded,
                    ExtendedError = null,
                    Value = value,
                    TimeInSeconds = time
                };
                return retval;
            }
            public static CharGenResult MakeFailed(Exception e, double time, string value = "")
            {
                var retval = new CharGenResult()
                {
                    Succeeded = State.Failed,
                    ExtendedError = e,
                    Value = value,
                    TimeInSeconds = time
                };
                return retval;
            }
            public static CharGenResult MakeFailed(SocketErrorStatus status, double time, string value = "")
            {
                var retval = new CharGenResult()
                {
                    Succeeded = State.Failed,
                    ExtendedError = null,
                    _ManuallySetStatus = status,
                    Value = value,
                    TimeInSeconds = time
                };
                return retval;
            }
            public enum State { NotStarted, Failed, InProgress, Succeeded };
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
            public Verbosity LoggingLevel { get; set; } = ClientOptions.Verbosity.Normal;
        }
        public ClientOptions Options { get; internal set; } = new ClientOptions();

        public class ClientStats
        {
            public int NWrites { get; set; } = 0;
            public int NExceptions { get; set; } = 0;
        }
        public ClientStats Stats { get; internal set; } = new ClientStats();

        // TODO: create a comment here and copy to Echo
        class ClientUdpResults
        {
            public void Add(string service, CharGenResult result)
            {
                UdpResults.TryAdd(service, new ConcurrentQueue<CharGenResult>());
                var queue = UdpResults[service];
                queue.Enqueue(result);
            }
            public bool TryRemove(string service, out CharGenResult echoResult)
            {
                echoResult = null;
                if (!UdpResults.ContainsKey(service)) return false;
                var queue = UdpResults[service]; // We never remove a list from the collection, so this is safe
                var didremove = queue.TryDequeue(out echoResult);
                return didremove;
            }
            ConcurrentDictionary<string, ConcurrentQueue<CharGenResult>> UdpResults = new ConcurrentDictionary<string, ConcurrentQueue<CharGenResult>>();
        }

        DatagramSocket udpSocket;
        StreamSocket tcpSocket;
        DataWriter tcpDw;
        ClientUdpResults UdpResults = new ClientUdpResults();

        public delegate void LogEventHandler(object sender, string str);
        public event LogEventHandler LogEvent;

        public CharGenClient_Rfc_864(ClientOptions options = null)
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

        public async Task<CharGenResult> CloseAsync()
        {
            if (tcpDw != null)
            {
                await tcpDw.FlushAsync();
                tcpDw.Dispose();
                tcpDw = null;
            }
            if (tcpSocket != null)
            {
                if (TcpCancellationTokenSource != null)
                {
                    Log($"Character Generator: CLIENT: About to cancel... TODO remove this message....");
                    TcpCancellationTokenSource.Cancel();
                }
                // // // TODO: this will Throw an exception???
                try
                {
                    await tcpSocket.CancelIOAsync(); //TODO: cancel the read (if any)
                }
                catch(Exception cancelEx)
                {
                    Log($"Character Generator: CLIENT: EXCEPTION: unable to cancel IO {cancelEx.Message}");
                }
                tcpSocket.Dispose();
                tcpSocket = null;

                // Wait for the TcpReadTask to finish
                if (TcpReadTask != null && !TcpReadTask.IsCompleted)
                {
                    await TcpReadTask;
                }
            }
            if (udpSocket != null)
            {
                udpSocket.Dispose();
                udpSocket = null;
            }
            return TcpReadEchoResult; // Not really fully correct.
        }

        Task TcpReadTask = null;
        CharGenResult TcpReadEchoResult = null;
        CancellationTokenSource TcpCancellationTokenSource = null;
        DateTime SocketStartTime;

        public enum ProtocolType { Tcp, Udp }
        public async Task<CharGenResult> WriteAsync(HostName address, string service, ProtocolType protocolType, string data="")
        {
            switch (protocolType)
            {
                case ProtocolType.Tcp:
                    return await WriteTcpAsync(address, service, data);
                case ProtocolType.Udp:
                    return await WriteUdpAsync(address, service, data);
            }
            return CharGenResult.MakeFailed(SocketErrorStatus.SocketTypeNotSupported, 0.0);
        }
        public async Task<CharGenResult> WriteTcpAsync(HostName address, string service, string data)
        {
            var startTime = DateTime.UtcNow;
            try
            {
                TcpCancellationTokenSource = new CancellationTokenSource();
                tcpSocket = new StreamSocket();
                var connectTask = tcpSocket.ConnectAsync(address, service);
                var taskList = new Task[]
                {
                        connectTask.AsTask(),
                        Task.Delay (Options.MaxConnectTimeInMilliseconds)
                };
                var waitResult = await Task.WhenAny(taskList);
                if (waitResult == taskList[1])
                {
                    tcpSocket = null;
                    Stats.NExceptions++; // mark it as an exception -- it would have failed if we didn't time out
                    Log($"TIMEOUT while connecting to {address} {service}");

                    var faildelta = DateTime.UtcNow.Subtract(startTime).TotalSeconds;
                    return CharGenResult.MakeFailed(SocketErrorStatus.ConnectionTimedOut, faildelta);
                }


                Log(ClientOptions.Verbosity.Verbose, $"CLIENT: TCP Connection using local port {tcpSocket.Information.LocalPort}");
                tcpDw = new DataWriter(tcpSocket.OutputStream);
                SocketStartTime = startTime;

                // Now read everything
                TcpReadEchoResult = CharGenResult.MakeInProgress(0); // Taken no time at all so far :-)
                var dr = new DataReader(tcpSocket.InputStream);
                dr.InputStreamOptions = InputStreamOptions.Partial;
                TcpReadTask = ReadTcpAsync(dr, TcpCancellationTokenSource.Token);

                tcpDw.WriteString(data);
                await tcpDw.StoreAsync();
                Stats.NWrites++;

                var delta = DateTime.UtcNow.Subtract(SocketStartTime).TotalSeconds;
                return CharGenResult.MakeInProgress(delta);
            }
            catch (Exception ex)
            {
                await CloseAsync();
                Stats.NExceptions++;
                Log($"ERROR: Client: Writing {data} to {address} exception {ex.Message}");
                var delta = DateTime.UtcNow.Subtract(SocketStartTime).TotalSeconds;
                return CharGenResult.MakeFailed(ex, delta);
            }
        }
        /// <summary>
        /// Ensures that the socket is made. Will return a failure EchoResult on failure
        /// and will return NULL for a success.
        /// </summary>
        /// <param name="address"></param>
        /// <param name="service"></param>
        /// <returns></returns>
        private CharGenResult EnsureUdpSocket()
        {
            var startTime = DateTime.UtcNow;
            lock (this)
            {
                if (udpSocket == null)
                {
                    udpSocket = new DatagramSocket();
                    SocketStartTime = startTime;
                    udpSocket.MessageReceived += UdpSocket_MessageReceived;
                }
            }

            // We made a socket and then making the datawriter somehow failed.
            // Just give up; there's no path that will get us into a better place.
            if (udpSocket == null)
            {
                return CharGenResult.MakeFailed(SocketErrorStatus.CannotAssignRequestedAddress, 0);
            }

            return null;
        }

        public async Task<CharGenResult> WriteUdpAsync(HostName address, string service, string data)
        {
            try
            {
                var haveUdpSocket = EnsureUdpSocket();
                if (haveUdpSocket != null)
                {
                    // Was unable to create the socket.
                    return haveUdpSocket;
                }

                var stream = await udpSocket.GetOutputStreamAsync(address, service);
                if (string.IsNullOrEmpty(data))
                {
                    // A blank string, when written to a data writer, won't actually result in a 
                    // UDP packet being sent. For the special case of not sending any data,
                    // use the WriteAsync on the socket's OutputStream directly.
                    var b = new Windows.Storage.Streams.Buffer(0);
                    await stream.WriteAsync(b);
                    Stats.NWrites++;
                }
                else
                {
                    var dw = new DataWriter(stream);
                    dw.WriteString(data);
                    await dw.StoreAsync();
                    Stats.NWrites++;
                }
                Log(ClientOptions.Verbosity.Verbose, $"Client: UDP: Sent request on local port {udpSocket.Information.LocalPort} request {data}");


                //
                // Wait for an answer
                //
                const int START_DELAY_MS = 10;
                int currTotalDelay = 0;
                int currDelay = START_DELAY_MS;

                CharGenResult udpResult = null;
                while (!UdpResults.TryRemove(udpSocket.Information.LocalPort, out udpResult))
                {
                    await Task.Delay(currDelay);
                    currTotalDelay += currDelay;
                    currDelay = Math.Min(currDelay * 2, Options.MaxPollLoopInMilliseconds); // Do an exponential backup up to max (10 seconds)
                    if (currTotalDelay >= Options.MaxWaitInMilliseconds)
                    {
                        Log($"ERROR: Client: reply from {address} took too long (outgoing data={data})");
                        var delta = DateTime.UtcNow.Subtract(SocketStartTime).TotalSeconds;
                        udpResult = CharGenResult.MakeFailed(SocketErrorStatus.ConnectionTimedOut, delta);
                        break;
                    }
                }
                return udpResult;
            }
            catch (Exception ex)
            {
                Stats.NExceptions++;
                Log($"ERROR: Client: Writing {data} to {address} exception {ex.Message}");
                var delta = DateTime.UtcNow.Subtract(SocketStartTime).TotalSeconds;
                return CharGenResult.MakeFailed(ex, delta);
            }
        }

        private void UdpSocket_MessageReceived(DatagramSocket sender, DatagramSocketMessageReceivedEventArgs args)
        {
            try
            {
                var dr = args.GetDataReader();
                dr.InputStreamOptions = InputStreamOptions.Partial; // | InputStreamOptions.ReadAhead;
                var udpResult = ReadUdp(dr);
                UdpResults.Add(sender.Information.LocalPort, udpResult);
            }
            catch (Exception ex)
            {
                // This can happen when we send a packet to a correct host (like localhost) but with an
                // incorrect service. The packet will "bounce", resuluting in a MessageReceived event TODO: resuluting is spelled wrong
                // but with an args with no real data.
                Stats.NExceptions++;
                var delta = DateTime.UtcNow.Subtract(SocketStartTime).TotalSeconds;
                var udpResult = CharGenResult.MakeFailed(ex, delta);
                UdpResults.Add(sender.Information.LocalPort, udpResult);
            }
        }

        private CharGenResult ReadUdp(DataReader dr)
        {
            uint count = dr.UnconsumedBufferLength;
            if (count > 0)
            {
                byte[] buffer = new byte[dr.UnconsumedBufferLength];
                dr.ReadBytes(buffer);
                var stringresult = BufferToString.ToString(buffer);
                LogCharGenBuffer(buffer);
                var delta = DateTime.UtcNow.Subtract(SocketStartTime).TotalSeconds;
                return CharGenResult.MakeSucceeded(stringresult, delta);
            }
            else // socket is done
            {
                var delta = DateTime.UtcNow.Subtract(SocketStartTime).TotalSeconds;
                return CharGenResult.MakeFailed(SocketErrorStatus.HttpInvalidServerResponse, delta);
            }
        }

        private async Task ReadTcpAsync(DataReader dr, CancellationToken ct)
        {
            try
            {
                uint count = 0;
                do
                {
                    Log($"Character Generator: CLIENT: FYI: about to try reading! TODO: remove this message....");

                    await dr.LoadAsync(2048).AsTask(ct); // Will throw 'thread exit or app request' when the socket is Disposed
                    count = dr.UnconsumedBufferLength;
                    if (count > 0)
                    {
                        byte[] buffer = new byte[dr.UnconsumedBufferLength];
                        dr.ReadBytes(buffer);
                        LogCharGenBuffer(buffer);
                        string str = Utilities.BufferToString.ToString(buffer);
                        TcpReadEchoResult.Value += str;
                        LogCharGenBuffer(buffer);
                    }
                    else // socket is done
                    {
                    }
                }
                while (count > 0);
            }
            catch (Exception ex)
            {
                Stats.NExceptions++;
                if (ct.IsCancellationRequested)
                {
                    Log($"Character Generator: CLIENT: cancellation with exception {ex.Message}");
                }
                else
                {
                    Log($"Character Generator: CLIENT: Exception {ex.Message}");
                }
            }
            var delta = DateTime.UtcNow.Subtract(SocketStartTime).TotalSeconds;
            TcpReadEchoResult.TimeInSeconds = delta;
            if (TcpReadEchoResult.Succeeded == CharGenResult.State.InProgress)
            {
                TcpReadEchoResult.Succeeded = CharGenResult.State.Succeeded;
            }
        }

        private void LogCharGenBuffer(byte[] buffer)
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
            Log($"Character Generator: CLIENT: {sb.ToString()}");
        }
    }
}
