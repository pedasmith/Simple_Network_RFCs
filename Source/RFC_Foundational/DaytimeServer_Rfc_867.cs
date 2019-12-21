using Networking.Utilities;
using System;
using System.Threading;
using System.Threading.Tasks;
using Windows.Networking;
using Windows.Networking.Sockets;
using Windows.Storage.Streams;

namespace Networking.RFC_Foundational
{
    public class DaytimeServer_Rfc_867: IDisposable
    {
        public class ServerOptions
        {
            public enum Verbosity {  None, Normal, Verbose}
            public Verbosity LoggingLevel { get; set; } = ServerOptions.Verbosity.Normal;
            /// <summary>
            /// DateTime format to use when writing back data. O=round trip format  F=common format.
            /// </summary>
            public string DateTimeFormat { get; internal set; } = "O";
            /// <summary>
            /// Service is the fancy name for "port". Set to 10013 to work with WinRT; RFC compliant value is 13.
            /// </summary>
            public string Service { get; set; }  = "10013";

            /// <summary>
            /// The RFC compatible service.
            /// </summary>
            public static string RfcService = "13";
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

        public DaytimeServer_Rfc_867(ServerOptions options = null) 
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
                    Log($"Daytime Connected on Tcp {TcpListener.Information.LocalPort}");
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
                    Log($"Daytime Connected on Udp {UdpListener.Information.LocalPort}");
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
            Stats.IncrementNConnections();

            HostName remoteHost;
            string remotePort = "not set";
            try
            {
                remoteHost = args.RemoteAddress;
                remotePort = args.RemotePort;
                var dr = args.GetDataReader();
                var os = await sender.GetOutputStreamAsync(remoteHost, remotePort);
                var dw = new DataWriter(os);

                Task t = DaytimeUdpAsync(dr, dw, remotePort);
                await t;
            }
            catch (Exception)
            {
                Stats.NExceptions++;
                Log(ServerOptions.Verbosity.Verbose, $"SERVER: UDP EXCEPTION when processing message remote {remotePort} ");
            }
        }

        private async void Listener_ConnectionReceived(StreamSocketListener sender, StreamSocketListenerConnectionReceivedEventArgs args)
        {
            Stats.IncrementNConnections();
            var socket = args.Socket;
            Task t = DaytimeTcpAsync(socket);
            await t;
        }
        private async Task DaytimeTcpAsync(StreamSocket tcpSocket)
        {
            // Step 1 is to write the reply.
            // Step 2 is to read (and discard) all incoming data
            var time = DateTime.Now;
            var str = time.ToString(Options.DateTimeFormat); // "F" is the default

            //NOTE: here's how to write data using a DataWriter
            //var dw = new DataWriter(tcpSocket.OutputStream);
            //dw.WriteString(str);
            //await dw.StoreAsync();
            Stats.IncrementNResponses();
            //await dw.FlushAsync(); // NOTE: this flush doesn't actually do anything useful.

            uint totalBytesWrite = 0;
            var writeBuffer = Windows.Security.Cryptography.CryptographicBuffer.ConvertStringToBinary(str, Windows.Security.Cryptography.BinaryStringEncoding.Utf8);
            var writeTask = tcpSocket.OutputStream.WriteAsync(writeBuffer);
            var bytesToWrite = writeBuffer.Length;
            writeTask.Progress += (operation, progress) => 
            {
                totalBytesWrite = progress;
            };
            await tcpSocket.OutputStream.FlushAsync();


            // Now read in all of the data that might have been passed but only for a little while.
            // Daytime doesn't use this information at all.
            var s = tcpSocket.InputStream;
            var buffer = new Windows.Storage.Streams.Buffer(2048);

            string stringresult = "";
            var keepGoing = Options.TcpReadTimeInMilliseconds >= 0; // Read time is negative? Then don't read at all!
            while (keepGoing)
            {
                try
                {
                    var read = s.ReadAsync(buffer, buffer.Capacity, InputStreamOptions.Partial);
                    //TODO: switch to WhenAny to use the awaitable values. See the CharGen server for details.
                    var waitResult = Task.WaitAny(new Task[] { read.AsTask() }, Options.TcpReadTimeInMilliseconds);
                    if (waitResult == 0)
                    {
                        var result = read.GetResults();
                        Stats.NBytesRead += result.Length;
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


        private async Task DaytimeUdpAsync(DataReader dr, DataWriter dw, string remotePort)
        {
            // Step 1 is to write the reply.
            // Step 2 is to read (and discard) all incoming data from the single UDP packet
            var time = DateTime.Now;
            var str = time.ToString(Options.DateTimeFormat); // "F" is the default


            string suffix = "";
            if (dr != null) // Will always be present for normal packets
            {
                uint count = dr.UnconsumedBufferLength;
                if (count > 0)
                {
                    Stats.NBytesRead += count;
                    byte[] buffer = new byte[count];
                    dr.ReadBytes(buffer);
                    var stringresult = BufferToString.ToString(buffer);
                    suffix = stringresult;
                    Log(ServerOptions.Verbosity.Verbose, $"SERVER: UDP read {count} bytes {stringresult}");
                }
            }

            str = str + suffix;
            Log(ServerOptions.Verbosity.Verbose, $"SERVER: UDP: reply with daytime <<{str}>> to remote port {remotePort}");
            dw.WriteString(str);
            await dw.StoreAsync();
            await dw.FlushAsync(); // NOTE: this flush doesn't actually do anything useful
            Stats.IncrementNResponses();

            dw.Dispose();
            Log(ServerOptions.Verbosity.Verbose, $"SERVER: UDP closing down the current writing socket for {str}");
        }
    }
}
