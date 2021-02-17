using Networking.Utilities;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.Networking;
using Windows.Networking.Sockets;
using Windows.Storage.Streams;

namespace Networking.RFC_Foundational
{
    /// <summary>
    /// Finger specified in https://tools.ietf.org/html/rfc1288
    /// See also https://tools.ietf.org/html/draft-ietf-uri-url-finger-03 for finger://url
    /// </summary>
    class FingerClient_Rfc_1288
    {

        /// <summary>
        /// The network value returned by the WriteAsync() calls
        /// </summary>
        public class FingerResult
        {
            public static FingerResult MakeSucceeded(string value, double time)
            {
                var retval = new FingerResult()
                {
                    Succeeded = true,
                    ExtendedError = null,
                    Value = value,
                    TimeInSeconds = time
                };
                return retval;
            }
            public static FingerResult MakeFailed(Exception e, double time, string value = "")
            {
                var retval = new FingerResult()
                {
                    Succeeded = false,
                    ExtendedError = e,
                    Value = value,
                    TimeInSeconds = time
                };
                return retval;
            }
            public static FingerResult MakeFailed(SocketErrorStatus status, double time, string value = "")
            {
                var retval = new FingerResult()
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
            public SocketErrorStatus Error { get { if (ExtendedError == null) return _ManuallySetStatus; return SocketError.GetStatus(ExtendedError.HResult); } }
            public string Value { get; set; }
            public double TimeInSeconds { get; set; }
        }


        public class ClientOptions
        {
            public enum Verbosity { None, Normal, Verbose }
            public Verbosity LoggingLevel { get; set; } = ClientOptions.Verbosity.Normal;

            /// <summary>
            /// Maximum total wait time for a connection. Keep it short: good servers are generally very fast to connect.
            /// </summary>
            public int MaxConnectTimeInMilliseconds { get; set; } = 1_000;
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

        public FingerClient_Rfc_1288(ClientOptions options = null)
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

        public enum ProtocolType { Tcp, }
        public async Task<FingerResult> WriteAsync(ParsedFingerCommand request)
        {
            var data = request.ToString();
            var datanice = data.Replace("\r\n", "");
            datanice = string.IsNullOrEmpty(datanice) ? "<blank string>" : datanice;

            var startTime = DateTime.UtcNow;
            try
            {
                var tcpSocket = new StreamSocket();
                var connectTask = tcpSocket.ConnectAsync(request.SendToHost, request.SendToPort);

                var taskList = new Task[]
                {
                        connectTask.AsTask(),
                        Task.Delay (Options.MaxConnectTimeInMilliseconds)
                };
                var waitResult = await Task.WhenAny(taskList);
                if (waitResult == taskList[1])
                {
                    Stats.NExceptions++; // mark it as an exception -- it would have failed if we didn't time out
                    Log($"TIMEOUT while connecting to {request.SendToHost} {request.SendToPort}");
                    Log($"Unable to send command {datanice}\n");

                    var faildelta = DateTime.UtcNow.Subtract(startTime).TotalSeconds;
                    return FingerResult.MakeFailed(SocketErrorStatus.ConnectionTimedOut, faildelta);
                }
                else
                {
                    // Connect is OK
                    if (!string.IsNullOrEmpty(data))
                    {
                        var dw = new DataWriter(tcpSocket.OutputStream);
                        dw.WriteString(data);
                        await dw.StoreAsync();
                        Log(ClientOptions.Verbosity.Normal, $"Finger sending command {datanice}\n");
                    }
                    Stats.NWrites++;

                    // Now read everything
                    var s = tcpSocket.InputStream;
                    var buffer = new Windows.Storage.Streams.Buffer(1024*64); // read in lots of the data

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
                                    Log(ClientOptions.Verbosity.Verbose, $"Daytime Progress count={progress} status={operation.Status} errorcode={err}");
                                });
                            */
                            read.Progress = (operation, progress) =>
                            {
                                var err = operation.ErrorCode == null ? "null" : operation.ErrorCode.ToString();
                                Log(ClientOptions.Verbosity.Verbose, $"Finger Progress count={progress} status={operation.Status} errorcode={err}");
                            };
                            var result = await read;
                            if (result.Length != 0)
                            {
                                var options = BufferToString.ToStringOptions.ProcessCrLf | BufferToString.ToStringOptions.ProcessTab;
                                var partialresult = BufferToString.ToString(result, options);
                                stringresult += partialresult;
                                Log($"{partialresult}"); // This will be printed on the user's screen.
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
                            return FingerResult.MakeFailed(ex2, faildelta);
                        }
                    }

                    var delta = DateTime.UtcNow.Subtract(startTime).TotalSeconds;
                    return FingerResult.MakeSucceeded(stringresult, delta);
                }
            }
            catch (Exception ex)
            {
                Stats.NExceptions++;
                Log($"ERROR: Client: Writing {datanice} to {request.SendToHost} exception {ex.Message}");
                var delta = DateTime.UtcNow.Subtract(startTime).TotalSeconds;
                return FingerResult.MakeFailed(ex, delta);
            }
        }
    }
}

