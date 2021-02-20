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
    public class CharGenServer_Rfc_864 : IDisposable
    {
        public class ServerOptions
        {
            public enum Verbosity { None, Normal, Verbose }
            public Verbosity LoggingLevel { get; set; } = ServerOptions.Verbosity.Normal;

            public enum PatternType {  Fixed, Classic72 };
            public PatternType OutputPattern { get; set; } = PatternType.Classic72;
            public static string Ascii95 = " !\"#$%&'()*+,-./0123456789:;<=>?@ABCDEFGHIJKLMNOPQRSTUVWXYZ[\\]^_`abcdefghijklmnopqrstuvwxyz{|}~";
            
            /// <summary>
            /// Says how quickly the server will send data to the client. This isn't a speed competition,
            /// so by default I keep the speed low.
            /// </summary>
            public int TimeBetweenWritesInMilliseconds { get; set; } = 500; 

            /// <summary>
            /// Service is the fancy name for "port". Set to 10019 to work with WinRT; RFC compliant value is 19.
            /// </summary>
            public string Service { get; set; } = "10019";

            /// <summary>
            /// The RFC compatible service.
            /// </summary>
            public static string RfcService = "19";
            /// <summary>
            /// Unlike the ECHO protocol, CharGen will keep producing output until the input is "done". 
            /// Normally we can expect that zero bytes will be sent; we're simply waiting for the socket
            /// to either be closed gracefully or to be reset.
            /// 
            /// Default is 500 milliseconds. 
            /// </summary>
            public int TcpReadPollTimeInMilliseconds { get; set; } = 500;

        }
        public ServerOptions Options { get; internal set; } = new ServerOptions();

        public class ServerStats
        {
            public int NConnections = 0;
            public int NResponses = 0;
            public uint NBytesRead { get; set; } = 0;
            public uint NBytesSent { get; set; } = 0;
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

            return retval;
        }

        private async void UdpListener_MessageReceived(DatagramSocket sender, DatagramSocketMessageReceivedEventArgs args)
        {
            Log(ServerOptions.Verbosity.Verbose, $"SERVER: UDP: Got incoming message");
            Interlocked.Increment(ref Stats.NConnections);

            HostName remoteHost;
            string remotePort = "not set";
            try
            {
                remoteHost = args.RemoteAddress;
                remotePort = args.RemotePort;
                var dr = args.GetDataReader();
                var len = dr.UnconsumedBufferLength;
                Stats.NBytesRead += len;
                var os = await sender.GetOutputStreamAsync(remoteHost, remotePort);
                var dw = new DataWriter(os);

                Task t = CharGenUdpAsync(dw, remotePort);
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
            Log(ServerOptions.Verbosity.Verbose, $"SERVER: TCP Connection to remote port {args.Socket.Information.RemotePort}");
            Interlocked.Increment(ref Stats.NConnections);
            var socket = args.Socket;
            Task t = CharGenTcpAsync(socket);
            await t;
        }

        private async Task CharGenReadTcpAsync(StreamSocket tcpSocket, CancellationToken ct)
        {
            // Continuously read in all information
            var s = tcpSocket.InputStream;
            var buffer = new Windows.Storage.Streams.Buffer(2048);

            var readOk = true;
            while (readOk && !ct.IsCancellationRequested)
            {
                try
                {
                    var readTask = s.ReadAsync(buffer, buffer.Capacity, InputStreamOptions.Partial);
                    var taskList = new Task[]
                    {
                        readTask.AsTask(),
                        Task.Delay (Options.TcpReadPollTimeInMilliseconds)
                    };
                    var waitResult = await Task.WhenAny(taskList);
                    if (waitResult == taskList[0])
                    {
                        var result = readTask.GetResults();
                        Stats.NBytesRead += result.Length;
                        if (result.Length == 0)
                        {
                            readOk = false; // Client must have closed the socket; stop the server
                        }
                        var partialresult = BufferToString.ToString(result);
                    }
                    else
                    {
                        // Done with this polling loop
                    }
                }
                catch (Exception ex2)
                {
                    Stats.NExceptions++;
                    readOk = false;
                    Log($"EXCEPTION while reading: {ex2.Message} {ex2.HResult:X}");
                }
            }
        }

        /// <summary>
        /// Continuously writes data over a streamsocket until the cancellation token is reset
        /// </summary>
        /// <param name="tcpSocket"></param>
        /// <returns></returns>
        private async Task CharGenWriteTcpAsync(StreamSocket tcpSocket, CancellationToken ct)
        {
            int start = 0;
            bool writeOk = true;
            while (writeOk && !ct.IsCancellationRequested)
            {
                var str = MakePattern (start, 72);
                var writeBuffer = Windows.Security.Cryptography.CryptographicBuffer.ConvertStringToBinary(str, Windows.Security.Cryptography.BinaryStringEncoding.Utf8);
                try
                {
                    var nbytes = await tcpSocket.OutputStream.WriteAsync(writeBuffer);
                    Stats.NBytesSent += nbytes;
                    start++;
                }
                catch (Exception ex2)
                {
                    Stats.NExceptions++;
                    writeOk = false;
                    Log($"EXCEPTION while writing: {ex2.Message} {ex2.HResult:X}");
                }
                if (!ct.IsCancellationRequested)
                {
                    // I'm not a fan of how a Task.Delay with a CancellationToken will throw
                    // an exception when the cancellation token is cancelled. 
                    try
                    {
                        await Task.Delay(Options.TimeBetweenWritesInMilliseconds, ct);
                    }
                    catch (Exception ex)
                    {
                        if ((uint)ex.HResult != 0x8013153B)
                        {
                            Log($"EXCEPTION while delay-writing: {ex.Message} {ex.HResult:X}");
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Main TCP loop; has two tasks, one for reading (which triggers when the client
        /// closes their side of the socket) and one for writing (which must be cancelled when
        /// the socket is closed).
        /// </summary>
        /// <param name="tcpSocket"></param>
        /// <returns></returns>
        private async Task CharGenTcpAsync(StreamSocket tcpSocket)
        {
            CancellationTokenSource cts = new CancellationTokenSource();
            Interlocked.Increment(ref Stats.NResponses);

            Task[] tasks = new Task[]
            {
                CharGenWriteTcpAsync(tcpSocket, cts.Token),
                CharGenReadTcpAsync(tcpSocket, cts.Token),
            };
            // Wait for one task to finish -- mostly likely the reader which will stop
            // when the client closes their side of the socket.
            var done = await Task.WhenAny(tasks);
            // If you don't cancel here, the writer will just keep on writing which in turn
            // will cause the socket to be forcefully closed when we try to write to a socket
            // which is in the process of being closed.
            cts.Cancel();
            await Task.WhenAll(tasks);

            Log(ServerOptions.Verbosity.Verbose, $"SERVER: TCP Stream closing down the current writing socket");

            tcpSocket.Dispose(); 
        }


        private async Task CharGenUdpAsync(DataWriter dw, string remotePort)
        {
            var start = rnd.Next(0, 95);
            var str = MakePattern(start, 72);

            Log(ServerOptions.Verbosity.Verbose, $"SERVER: UDP: reply with CharGen <<{str}>> to remote port {remotePort}");
            dw.WriteString(str);
            await dw.StoreAsync();
            await dw.FlushAsync(); // NOTE: this flush doesn't actually do anything useful
            Interlocked.Increment(ref Stats.NResponses);

            dw.Dispose();
            Log(ServerOptions.Verbosity.Verbose, $"SERVER: UDP closing down the current writing socket for {str}");
        }

        private string MakePattern(int start, int length=72)
        {
            switch (Options.OutputPattern)
            {
                case ServerOptions.PatternType.Fixed:
                    return MakeAscii(0, 72);
                case ServerOptions.PatternType.Classic72:
                    return MakeAscii(start, 72);
            }
            return MakeAscii(0, 72);
        }

        /// <summary>
        /// Generates an ascii string which is a subset of the original.
        /// </summary>
        /// <param name="start"></param>
        /// <param name="length"></param>
        /// <returns></returns>
        private static string MakeAscii(int start, int length=72)
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

        public static string GetSampleReturn(int start = 0)
        {
            return MakeAscii(start);
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

        /// <summary>
        /// Helper routine with radically simplified parameters for testing the Ascii lines.
        /// </summary>
        /// <param name="start"></param>
        /// <param name="length"></param>
        /// <param name="expected"></param>
        private static void TestMakeAsciiOne(int start, int length, string expected)
        {
            var actual = MakeAscii(start, length);
            Infrastructure.IfTrueError(actual != expected, $"MakeAscii({start},{length}) should be [{expected}] but actually got [{actual}]");
        }
    }
}
