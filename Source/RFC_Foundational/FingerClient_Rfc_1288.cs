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
        public class FingerRequest
        {
            public FingerRequest(HostName host, string user, bool whoIsMode)
            {
                Host = host;
                User = user;
                WhoIsMode = whoIsMode;
            }
            public HostName Host { get; set; }
            public string User { get; set; }
            public bool WhoIsMode { get; set; } = false; // the /W switch aka the -l long view mode
            public string WSwitch {  get { return WhoIsMode ? "/W" : null; } }
            public string Port { get; set; } = DefaultService;
            const string DefaultService = "79";

            public override string ToString()
            {
                var wswitchSpace = WhoIsMode && !string.IsNullOrEmpty(User) ? " " : "";
                var data = WSwitch + wswitchSpace + User + "\r\n";
                return data;
            }

            public string ToStringAtFormat()
            {
                string retval = string.IsNullOrEmpty(User) ? "" : User;
                retval += "@";
                retval += Host.CanonicalName;
                return retval;
            }

            /// <summary>
            /// Converts input like user@example.com or @example.com or example.com into a request.
            /// Does not handle uri (see FromUri for that)
            /// </summary>
            /// <param name="address"></param>
            /// <param name="wswitch"></param>
            /// <returns></returns>
            public static FingerRequest FromString(String address, bool wswitch=false)
            {
                FingerClient_Rfc_1288.FingerRequest request = null;
                var userSplit = address.Split(new char[] { '@' }, 2);
                switch (userSplit.Length)
                {
                    case 1:
                        {
                            var host = new HostName(userSplit[0]);
                            request = new FingerClient_Rfc_1288.FingerRequest(host, "", wswitch);
                            break;
                        }
                    case 2:
                        {
                            var user = userSplit[0];
                            var host = new HostName(userSplit[1]);
                            request = new FingerClient_Rfc_1288.FingerRequest(host, user, wswitch);
                            break;
                        }
                }
                return request;
            }
            public static FingerRequest FromUri (Uri uri)
            {
                // See: https://tools.ietf.org/html/draft-ietf-uri-url-finger-03
                // finger://host[:port][/<request>]
                // examples from spec: finger://space.mit.edu/nasanews finger://status.nlak.net
                if (uri.Scheme.ToLowerInvariant() != "finger")
                {
                    throw new Exception($"ERROR: FingerRequest requires URI that starts with finger://");
                }
                // The LocalPath will be //W user for urls like finger://example.com//W%20user
                bool isW = false;
                string user = uri.LocalPath;
                string port = DefaultService;
                if (uri.LocalPath.StartsWith ("//W"))
                {
                    user = uri.LocalPath.Substring(3).Trim();
                    isW = true;
                }
                else if (uri.LocalPath.StartsWith("/"))
                {
                    user = uri.LocalPath.Substring(1);
                }
                // technical violation of the url spec: I allow finger://user@example.com and finger://user@example.com//W
                // In both cases, the user will be the one before the @sign/
                // If there's both a real path AND an user@ given, the user@ will take priority.
                if (!string.IsNullOrEmpty(uri.UserInfo))
                {
                    user = uri.UserInfo;
                }
                if (!uri.IsDefaultPort)
                {
                    port = uri.Port.ToString(); // is -1 by default for finger://
                }
                var retval = new FingerRequest(new HostName (uri.Host), user, isW);
                retval.Port = port;
                return retval;
            }
        }

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
        public async Task<FingerResult> WriteAsync(FingerRequest request)
        {
            var data = request.ToString();
            var datanice = data.Replace("\r\n", "");
            datanice = string.IsNullOrEmpty(datanice) ? "<blank string>" : datanice;

            var startTime = DateTime.UtcNow;
            try
            {
                var tcpSocket = new StreamSocket();
                await tcpSocket.ConnectAsync(request.Host, request.Port);
                // Everything that's sent will be ignored.
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
            catch (Exception ex)
            {
                Stats.NExceptions++;
                Log($"ERROR: Client: Writing {datanice} to {request.Host} exception {ex.Message}");
                var delta = DateTime.UtcNow.Subtract(startTime).TotalSeconds;
                return FingerResult.MakeFailed(ex, delta);
            }
        }
    }
}

