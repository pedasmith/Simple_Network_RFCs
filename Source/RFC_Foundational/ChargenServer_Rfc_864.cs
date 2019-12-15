using Networking.RFC_Foundational_Tests;
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
    class CharGenServer_Rfc_864
    {
        public class ServerOptions
        {
            public enum Verbosity { None, Normal, Verbose }
            public Verbosity LoggingLevel { get; set; } = ServerOptions.Verbosity.Normal;

            public string DateTimeFormat { get; set; } = "O"; //TODO: datetimeformat is junk now!
            public enum PatternType {  Classic72 };
            public PatternType OutputPattern { get; set; } = PatternType.Classic72;

            public static string Ascii95 = " !\"#$%&'()*+,-./0123456789:;<=>?@ABCDEFGHIJKLMNOPQRSTUVWXYZ[\\]^_`abcdefghijklmnopqrstuvwxyz{|}~";

            /// <summary>
            /// Service is the fancy name for "port". Set to 10013 to work with WinRT; RFC compliant value is 13.
            /// </summary>
            public string Service { get; set; } = "10019";

            /// <summary>
            /// The RFC compatible service.
            /// </summary>
            public static string RfcService = "19";
            /// <summary>
            /// How long should we wait to drain the incoming stream? Setting this large will always result in a slower
            /// client because we can only close the outgoing stream when the incoming stream is drained (or we give up
            /// on draining). Setting this to zero is OK.
            /// 
            /// Default is 10 milliseconds
            /// </summary>
            public int TcpReadTimeInMilliseconds { get; set; } = 10;

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
            public int NConnections = 0;
            public int NResponses = 0;
            public uint NBytes { get; set; } = 0;
            public int NExceptions { get; set; } = 0;
        };
        public ServerStats Stats { get; internal set; } = new ServerStats();

        public delegate void LogEventHandler(object sender, string str);
        public event LogEventHandler LogEvent;
        private Random rnd = new Random();

        public CharGenServer_Rfc_864(ServerOptions options = null)
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
            if (UdpListener != null)
            {
                var task = UdpListener.CancelIOAsync();
                task.AsTask().Wait();
                UdpListener.Dispose();
                UdpListener = null;
            }
        }
        public enum State { NotStarted, Error, Running };


        StreamSocketListener TcpListener = null;
        DatagramSocket UdpListener = null;
        private void Log(string str)
        {
            Log(ServerOptions.Verbosity.Normal, str);
        }
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
        /// Returns true if all the servers (TCP and UDP) could be started. Writes to the log on both failure and success.
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
                    Log($"CharGen Connected on Tcp {TcpListener.Information.LocalPort}");
                }
            }
            catch (Exception e)
            {
                Stats.NExceptions++;
                Log($"ERROR: unable to start TCP server {e.Message}");
                retval = false;
            }

            UdpListener = new DatagramSocket();
            UdpListener.MessageReceived += UdpListener_MessageReceived;
            try
            {
                await UdpListener.BindServiceNameAsync(Options.Service);
                {
                    Log($"CharGen Connected on Udp {UdpListener.Information.LocalPort}");
                }
            }
            catch (Exception e)
            {
                Stats.NExceptions++;
                Log($"ERROR: unable to start UDP server {e.Message}");
                retval = false;
            }

            var hosts = Windows.Networking.Connectivity.NetworkInformation.GetHostNames();
            foreach (var host in hosts)
            {
                Log($"Host: {host.CanonicalName}");
            }
            return retval;
        }

        private async void UdpListener_MessageReceived(DatagramSocket sender, DatagramSocketMessageReceivedEventArgs args)
        {
            Log(ServerOptions.Verbosity.Verbose, $"SERVER: UDP: message recv sender remote port <<{sender.Information.RemotePort}>>");
            Interlocked.Increment(ref Stats.NConnections);

            HostName remoteHost;
            string remotePort = "not set";
            try
            {
                remoteHost = args.RemoteAddress;
                remotePort = args.RemotePort;
                var dr = args.GetDataReader();
                var os = await sender.GetOutputStreamAsync(remoteHost, remotePort);
                var dw = new DataWriter(os);

                Task t = CharGenAsyncUdp(dr, dw, remotePort);
                await t;
            }
            catch (Exception)
            {
                Log(ServerOptions.Verbosity.Verbose, $"SERVER: UDP EXCEPTION when processing message remote {remotePort} ");
                Stats.NExceptions++;
            }
        }

        private async void Listener_ConnectionReceived(StreamSocketListener sender, StreamSocketListenerConnectionReceivedEventArgs args)
        {
            Stats.NConnections++;
            var socket = args.Socket;
            Task t = CharGenAsyncTcp(socket);
            await t;
        }
        private async Task CharGenAsyncTcp(StreamSocket tcpSocket)
        {
            // Step 1 is to send the reply.
            // Step 2 is to read (and discard) all incoming data
            var time = DateTime.Now;
            var str = time.ToString(Options.DateTimeFormat); // "F" is the default

            //NOTE: here's how to write data using a DataWriter
            //var dw = new DataWriter(tcpSocket.OutputStream);
            //dw.WriteString(str);
            //await dw.StoreAsync();
            Interlocked.Increment(ref Stats.NResponses);
            //await dw.FlushAsync(); // NOTE: this flush doesn't actually do anything useful.

            uint totalBytesSent = 0;
            var writeBuffer = Windows.Security.Cryptography.CryptographicBuffer.ConvertStringToBinary(str, Windows.Security.Cryptography.BinaryStringEncoding.Utf8);
            var writeTask = tcpSocket.OutputStream.WriteAsync(writeBuffer);
            var bytesToSend = writeBuffer.Length;
            writeTask.Progress += (operation, progress) =>
            {
                totalBytesSent = progress;
            };
            await tcpSocket.OutputStream.FlushAsync();


            // Now read in all of the data that might have been passed but only for a little while.
            // CharGen doesn't use this information at all.
            var s = tcpSocket.InputStream;
            var buffer = new Windows.Storage.Streams.Buffer(2048);

            string stringresult = "";
            var keepGoing = Options.TcpReadTimeInMilliseconds >= 0; // Read time is negative? Then don't read at all!
            while (keepGoing)
            {
                try
                {
                    var read = s.ReadAsync(buffer, buffer.Capacity, InputStreamOptions.Partial);
                    var waitResult = Task.WaitAny(new Task[] { read.AsTask() }, Options.TcpReadTimeInMilliseconds);
                    if (waitResult == 0)
                    {
                        var result = read.GetResults();
                        Stats.NBytes += result.Length;
                        var partialresult = BufferToString.ToString(result);
                        stringresult += partialresult;
                        Log($"Got data from client: {stringresult} Length={result.Length}");
                    }
                    else
                    {
                        keepGoing = false;
                    }
                }
                catch (Exception ex2)
                {
                    Stats.NExceptions++;
                    keepGoing = false;
                    Log($"EXCEPTION while reading: {ex2.Message} {ex2.HResult:X}");
                }
            }

            Log(ServerOptions.Verbosity.Verbose, $"SERVER: TCP Stream closing down the current writing socket");

            // Wait for the write buffer to be completely written, but only wait a short while.
            // The actual progress is limited because not all writes will trigger the progress indicator.
            // Works fine with no waiting (WriteTimeInMilliseconds set to -1)
            int currWait = 0;
            while (totalBytesSent != bytesToSend && currWait < Options.TcpWriteTimeInMilliseconds)
            {
                await Task.Delay(10);
                currWait += 10;
            }
            if (totalBytesSent != bytesToSend && Options.TcpWriteTimeInMilliseconds >= 0)
            {
                Log(ServerOptions.Verbosity.Verbose, $"SERVER: incomplete send {totalBytesSent} of {bytesToSend} wait time {Options.TcpWriteTimeInMilliseconds}");
            }

            if (Options.TcpPauseBeforeCloseTimeInMilliseconds >= 0)
            {
                await Task.Delay(Options.TcpPauseBeforeCloseTimeInMilliseconds);
            }
            tcpSocket.Dispose(); // The dispose is critical; without it the client won't ever finish reading our output

        }


        private async Task CharGenAsyncUdp(DataReader dr, DataWriter dw, string remotePort)
        {
            var start = rnd.Next(0, 95);
            var str = MakeAscii(start, 72);

            Log(ServerOptions.Verbosity.Verbose, $"SERVER: UDP: reply with CharGen <<{str}>> to remote port {remotePort}");
            dw.WriteString(str);
            await dw.StoreAsync();
            await dw.FlushAsync(); // NOTE: this flush doesn't actually do anything useful
            Interlocked.Increment(ref Stats.NResponses);

            dw.Dispose();
            Log(ServerOptions.Verbosity.Verbose, $"SERVER: UDP closing down the current writing socket for {str}");
        }

        /// <summary>
        /// Generates an ascii string which is a subset of the original.
        /// </summary>
        /// <param name="start"></param>
        /// <param name="length"></param>
        /// <returns></returns>
        public static string MakeAscii(int start, int length)
        {
            string proto = ServerOptions.Ascii95;
            var sb = new StringBuilder();
            if (start < 0) start = 0;
            start = start % proto.Length;
            if (length < 0) length = 0;
            if (length > proto.Length) length = proto.Length;
            for (int i=0; i<length; i++)
            {
                var index = (start + i) % proto.Length;
                sb.Append(proto[index]);
            }
            return sb.ToString();
        }

        public static void TestAscii95()
        {
            // The Ascii95 string should be 95 chars long and in order
            Infrastructure.IfTrueError(ServerOptions.Ascii95.Length != 95, $"Ascii95 should be 95 chars long; is actually {ServerOptions.Ascii95.Length}");
            char lastChar = '\0';
            for (int i=0; i<ServerOptions.Ascii95.Length; i++)
            {
                var ch = ServerOptions.Ascii95[i];
                Infrastructure.IfTrueError(ch <= lastChar, $"Ascii95 [{i}] is {ch} which isn't > lastChar {lastChar}");
                Infrastructure.IfTrueError(char.IsControl(ch), $"Ascii95 [{i}] is {ch} which is a control character");

                lastChar = ch;
            }

            TestMakeAsciiOne(0, 5, " !\"#$");
            TestMakeAsciiOne(1, 5, "!\"#$%");
            TestMakeAsciiOne(95, 5, " !\"#$");
            TestMakeAsciiOne(0, -1, "");
            TestMakeAsciiOne(-1, 5, " !\"#$");

        }

        private static void TestMakeAsciiOne(int start, int length, string expected)
        {
            var actual = MakeAscii(start, length);
            Infrastructure.IfTrueError(actual != expected, $"MakeAscii({start},{length}) should be [{expected}] but actually got [{actual}]");

        }
    }
}
