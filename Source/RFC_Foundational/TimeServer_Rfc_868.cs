using Echo_Rfc_862.Utilities;
using System;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Windows.Foundation;
using Windows.Networking;
using Windows.Networking.Sockets;
using Windows.Storage.Streams;

namespace RFC_Foundational
{
    public class TimeServer_Rfc_868 : IDisposable
    {
        public static class TimeConversion
        {
            private static DateTimeOffset StartTime = new DateTimeOffset(1900, 1, 1, 0, 0, 0, new TimeSpan(0));
            public static UInt32 GetNow(DateTimeOffset? time = null)
            {
                if (!time.HasValue) time = DateTimeOffset.UtcNow;
                var delta = time.Value.Subtract(StartTime);
                return (UInt32)delta.TotalSeconds;
            }

            public static DateTimeOffset Convert (UInt32 data)
            {
                var convert = StartTime.AddSeconds(data);
                return convert;
            }

            public static int TestCalendar()
            {
                int nerror = 0;
                // These samples are straight from the RTC
                nerror += TestCalendarOne("0:00 1-Jan-1970 GMT", 2208988800);
                nerror += TestCalendarOne("0:00 1-Jan-1976 GMT", 2398291200);
                nerror += TestCalendarOne("0:00 1-Jan-1980 GMT", 2524521600);
                nerror += TestCalendarOne("0:00 1-May-1983 GMT", 2629584000);

                return nerror;
            }
            private static int TestCalendarOne(string date, UInt32 expected)
            {
                int nerror = 0;
                var dt = DateTimeOffset.Parse(date);
                var actual = GetNow(dt);
                if (actual != expected)
                {
                    nerror++;
                    System.Diagnostics.Debug.WriteLine($"TimeServer: Test Calendar: ERROR: ({date}) expected {expected} actual {actual} delta {actual - expected}");
                }

                return nerror;
            }
        }


        public class ServerOptions
        {
            public enum Verbosity { None, Normal, Verbose }
            public Verbosity LoggingLevel { get; set; } = ServerOptions.Verbosity.Normal;
            /// <summary>
            /// Service is the fancy name for "port". Set to 10037 to work with WinRT; RFC compliant value is 37.
            /// </summary>
            public string Service { get; set; } = "10037";

            /// <summary>
            /// The RFC compatible service.
            /// </summary>
            public static string RfcService = "37";
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

        public TimeServer_Rfc_868(ServerOptions options = null)
        {
            if (options != null)
            {
                Options = options;
            }
        }

        /// <summary>
        /// The Dispose means that when you create the server in a using() block,
        /// the server sockets will be automatically closed at the end.
        /// </summary>
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
                    Log($"Time Connected on Tcp {TcpListener.Information.LocalPort}");
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
                    Log($"Time Connected on Udp {UdpListener.Information.LocalPort}");
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

                Task t = TimeAsyncUdp(dr, dw, remotePort);
                await t;
            }
            catch (Exception)
            {
                //TODO: say when this can happen and add to DaytimeServer
                Log(ServerOptions.Verbosity.Verbose, $"SERVER: UDP EXCEPTION when processing message remote {remotePort} ");
                Stats.NExceptions++;
            }
        }

        /// <summary>
        /// Called whenever a new TCP connection is made to the server.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="args"></param>
        private async void Listener_ConnectionReceived(StreamSocketListener sender, StreamSocketListenerConnectionReceivedEventArgs args)
        {
            Stats.NConnections++;
            var socket = args.Socket;
            Task t = TimeAsyncTcp(socket);
            await t;
        }


        private async Task TimeAsyncTcp(StreamSocket tcpSocket)
        {
            // Step 1 is to send the reply.
            // CHANGE: fix this comment Step 2 is to read (and discard) all incoming data 


            //NOTE: here's how to write data using a DataWriter
            //CHANGE: use datawriter
            var dw = new DataWriter(tcpSocket.OutputStream);
            var now = TimeConversion.GetNow();
            dw.WriteUInt32(now);
            await dw.StoreAsync();

            Interlocked.Increment(ref Stats.NResponses);
            //await dw.FlushAsync(); // NOTE: this flush doesn't actually do anything useful.

            // CHANGE: REMOVE THE DIRECT STREAM WRITE
            await tcpSocket.OutputStream.FlushAsync();


            //CHANGE: no reading!

            Log(ServerOptions.Verbosity.Verbose, $"SERVER: TCP Stream closing down the current writing socket");

            if (Options.TcpPauseBeforeCloseTimeInMilliseconds >= 0)
            {
                await Task.Delay(Options.TcpPauseBeforeCloseTimeInMilliseconds);
            }
            tcpSocket.Dispose(); // The dispose is critical; without it the client won't ever finish reading our output
        }


        private async Task TimeAsyncUdp(DataReader dr, DataWriter dw, string remotePort)
        {
            var now = TimeConversion.GetNow();
            dw.WriteUInt32(now);
            await dw.StoreAsync();
            Interlocked.Increment(ref Stats.NResponses);
            Log(ServerOptions.Verbosity.Verbose, $"SERVER: UDP: reply with time {now} to remote port {remotePort}");

            dw.Dispose();
            Log(ServerOptions.Verbosity.Verbose, $"SERVER: UDP closing down the current writing socket for {now}");
        }
    }
}
