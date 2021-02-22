using Networking.Utilities;
using System;
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
    /// <summary>
    /// Implement this and then set the FingerServer_Rfc_1288.FingerHandler. This allows you
    /// to create your own replies to the Finger requests. The finger requests are split into the
    /// four kinds of requests (listuser and listall are the "Q1" type and listremote is the "Q2" type).
    /// </summary>
    public interface IFingerServerHandler
    {
        string OnError(ParsedFingerCommand command);
        string OnListAll(ParsedFingerCommand command);
        string OnListUser(ParsedFingerCommand command);
        string OnListRemote(ParsedFingerCommand command);
    }

    /// <summary>
    /// Default implementation of the server handler. Just dumps out default information.
    /// </summary>
    public class SimpleFingerServerHandler : IFingerServerHandler
    {
        public string OnError(ParsedFingerCommand command)
        {
            return $"ERROR: unknown command from {command.OriginalCommand}";
        }
        public string OnListAll(ParsedFingerCommand command)
        {
            return "List all users is not allowed on this server";
        }
        public string OnListUser(ParsedFingerCommand command)
        {
            return $"Unable to get information for user {command.User}";
        }
        public string OnListRemote(ParsedFingerCommand command)
        {
            return $"Request to forward finger is not allowed";
        }
    }



    class FingerServer_Rfc_1288 : IDisposable
    {
        public class ServerOptions
        {
            public enum Verbosity { None, Normal, Verbose }
            public Verbosity LoggingLevel { get; set; } = ServerOptions.Verbosity.Normal;
            /// <summary>
            /// Service is the fancy name for "port". Set to 10079 to work with WinRT; RFC compliant value is 79.
            /// </summary>
            public string Service { get; set; } = "79";

            /// <summary>
            /// The RFC compatible service (port 79).
            /// </summary>
            public static string RfcService = "79";
            /// <summary>
            /// How long should we wait to drain the incoming stream? Setting this large will always result in a slower
            /// client because we can only close the outgoing stream when the incoming stream is drained (or we give up
            /// on draining). Setting this to zero is OK.
            /// 
            /// Default is 100 milliseconds
            /// </summary>
            public int TcpReadTimeInMilliseconds { get; set; } = 100;

            /// <summary>
            /// Says how long to pause before closing the outgoing stream. There isn't actually a way
            /// to close a Tcp stream "nicely" at the guaranteed correct time. When negative, won't pause at all.
            /// Default is -1.
            /// </summary>
            public int TcpPauseBeforeCloseTimeInMilliseconds { get; set; } = -1;

            /// <summary>
            /// How long to wait while waiting for the socket write to have a complete progress.
            /// Often, the progress callback will never be called, so waiting for "real" data is
            /// not smart; this timer will set a maximum wait time. When set to a negative number,
            /// the socket won't wait for the write to be complete.
            /// 
            /// Default is -1.
            /// </summary>
            public int TcpWriteTimeInMilliseconds { get; set; } = -1;
        }
        public ServerOptions Options { get; internal set; } = new ServerOptions();

        public class ServerStats
        {
            // _NConnection and _NResponses must be ordinary number to be interlock-incremented.
            private int _NConnections = 0;
            private int _NResponses = 0;
            public void IncrementNConnections()
            {
                Interlocked.Increment(ref _NConnections);
            }
            public void IncrementNResponses()
            {
                Interlocked.Increment(ref _NResponses);
            }
            public int NConnections { get { return _NConnections; } }
            public int NResponses { get { return _NResponses; } }
            public uint NBytesRead { get; set; } = 0;
            public int NExceptions { get; set; } = 0;
        };

        public ServerStats Stats { get; internal set; } = new ServerStats();


        public delegate void LogEventHandler(object sender, string str);
        public event LogEventHandler LogEvent;
        public IFingerServerHandler FingerRequestHandler = new SimpleFingerServerHandler(); // ready to be overridden!

        public FingerServer_Rfc_1288(ServerOptions options = null)
        {
            if (options != null)
            {
                Options = options;
            }
        }

        public void Dispose()
        {
            if (TcpListener != null)
            {
                var task = TcpListener.CancelIOAsync();
                task.AsTask().Wait();
                TcpListener.Dispose();
                TcpListener = null;
            }
        }



        StreamSocketListener TcpListener = null;

        /// <summary>
        /// Central routine for all logging; will log as normal
        /// </summary>
        /// <param name="str"></param>
        private void Log(string str)
        {
            Log(ServerOptions.Verbosity.Normal, str);
        }

        /// <summary>
        /// Central routine for all logging. Will trigger the log event and will write to the
        /// system.diagnostics.debug output.
        /// </summary>
        /// <param name="level"></param>
        /// <param name="str"></param>
        private void Log(ServerOptions.Verbosity level, string str)
        {
            // e.g. level is Normal (1) and LoggingLevel is None (0), then should not log
            if (level <= Options.LoggingLevel)
            {
                LogEvent?.Invoke(this, str);
                System.Diagnostics.Debug.WriteLine(str);
            }
        }

        /// <summary>
        /// Returns true if all the servers (really just TCP) could be started. Writes to the log on both failure and success.
        /// </summary>
        /// <returns></returns>
        public async Task<bool> StartAsync()
        {
            var retval = true;
            TcpListener = new StreamSocketListener();
            TcpListener.ConnectionReceived += Listener_ConnectionReceived;
            try
            {
                await TcpListener.BindServiceNameAsync(Options.Service);
                {
                    Log($"Finger Connected on Tcp {TcpListener.Information.LocalPort}");
                }
            }
            catch (Exception e)
            {
                Stats.NExceptions++;
                Log($"ERROR: unable to start TCP server {e.Message}");
                retval = false;
            }
            return retval;
        }


        private async void Listener_ConnectionReceived(StreamSocketListener sender, StreamSocketListenerConnectionReceivedEventArgs args)
        {
            Stats.IncrementNConnections();
            var socket = args.Socket;
            Task t = FingerTcpAsync(socket);
            await t;
        }

        private async Task FingerTcpAsync(StreamSocket tcpSocket)
        {
            var time = DateTime.Now;
            string reply = ""; 

            var incomingText = await ReadIncomingCommandLineAsync(tcpSocket);
            var command = ParsedFingerCommand.ParseFromNetwork(incomingText);

            switch (command.FingerCommand)
            {
                case ParsedFingerCommand.CommandType.Error:
                    reply = FingerRequestHandler.OnError(command);
                    break;
                case ParsedFingerCommand.CommandType.ListAll:
                    reply = FingerRequestHandler.OnListAll(command);
                    break;
                case ParsedFingerCommand.CommandType.ListUser:
                    switch (command.User)
                    {
                        case "stats":
                            reply = $"NConnection={Stats.NConnections}\nNBytesRead={Stats.NBytesRead}\nNExceptions={Stats.NExceptions}\n";
                            break;
                        default:
                            reply = FingerRequestHandler.OnListUser(command);
                            break;
                    }
                    break;
                case ParsedFingerCommand.CommandType.ListRemote:
                    reply = FingerRequestHandler.OnListRemote(command);
                    break;
            }
            reply += "\n" + command.ToDebugString() + "\n" + command.ToReceivedNetworkCommand() + "****\r\n";


            //NOTE: here's how to write data using a DataWriter
            //var dw = new DataWriter(tcpSocket.OutputStream);
            //dw.WriteString(str);
            //await dw.StoreAsync();
            //await dw.FlushAsync(); // NOTE: this flush doesn't actually do anything useful.

            Stats.IncrementNResponses();
            uint totalBytesWrite = 0;
            var writeBuffer = Windows.Security.Cryptography.CryptographicBuffer.ConvertStringToBinary(reply, Windows.Security.Cryptography.BinaryStringEncoding.Utf8);
            var writeTask = tcpSocket.OutputStream.WriteAsync(writeBuffer);
            var bytesToWrite = writeBuffer.Length;
            writeTask.Progress += (operation, progress) =>
            {
                totalBytesWrite = progress;
            };
            await tcpSocket.OutputStream.FlushAsync();


            var drainResult = await NetworkStreamUtilities.DrainStream(tcpSocket, Options.TcpReadTimeInMilliseconds);
            Stats.NBytesRead += drainResult.NBytesRead;
            Stats.NExceptions += drainResult.NExceptions;
            if (drainResult.LogText != "")
            {
                Log(drainResult.LogText);
            }
            Log(ServerOptions.Verbosity.Verbose, $"SERVER: TCP Stream closing down the current writing socket");

            // Wait for the write buffer to be completely written, but only wait a short while.
            // The actual progress is limited because not all writes will trigger the progress indicator.
            // Works fine with no waiting (WriteTimeInMilliseconds set to -1)
            int currWait = 0;
            while (totalBytesWrite != bytesToWrite && currWait < Options.TcpWriteTimeInMilliseconds)
            {
                await Task.Delay(10);
                currWait += 10;
            }
            if (totalBytesWrite != bytesToWrite && Options.TcpWriteTimeInMilliseconds >= 0)
            {
                Log(ServerOptions.Verbosity.Verbose, $"SERVER: incomplete write {totalBytesWrite} of {bytesToWrite} wait time {Options.TcpWriteTimeInMilliseconds}");
            }

            if (Options.TcpPauseBeforeCloseTimeInMilliseconds >= 0)
            {
                await Task.Delay(Options.TcpPauseBeforeCloseTimeInMilliseconds);
            }
            tcpSocket.Dispose(); // The dispose is critical; without it the client won't ever finish reading our output
        }


        /// <summary>
        /// Reads the incoming socket, returning just the first line (based on CRLF). The rest of the data is ignored.
        /// </summary>
        /// <param name="tcpSocket"></param>
        /// <returns></returns>
        private async Task<String> ReadIncomingCommandLineAsync(StreamSocket tcpSocket)
        {
            var s = tcpSocket.InputStream;
            var buffer = new Windows.Storage.Streams.Buffer(2048);

            string stringresult = "";
            var keepGoing = true;
            while (keepGoing)
            {
                try
                {
                    var readTask = s.ReadAsync(buffer, buffer.Capacity, InputStreamOptions.Partial);
                    var taskList = new Task[]
                    {
                        readTask.AsTask(),
                        Task.Delay (Options.TcpReadTimeInMilliseconds),
                    };
                    var waitResult = await Task.WhenAny(taskList);
                    if (waitResult == taskList[0])
                    {
                        var result = readTask.GetResults();
                        Stats.NBytesRead += result.Length;
                        var options = BufferToString.ToStringOptions.ProcessCrLf | BufferToString.ToStringOptions.ProcessTab;
                        var partialresult = BufferToString.ToString(result, options);
                        stringresult += partialresult;
                        Log($"Got data from client: {stringresult} Length={result.Length}");
                    }
                    else
                    {
                        keepGoing = false; // Timed out
                    }
                }
                catch (Exception ex2)
                {
                    Stats.NExceptions++;
                    keepGoing = false;
                    Log($"EXCEPTION while reading: {ex2.Message} {ex2.HResult:X}");
                }
            }
            var split = stringresult.Split("\r\n", 2); //NOTE: alternative is to split at either CR or LF
            return split[0];
        }

    }
}

