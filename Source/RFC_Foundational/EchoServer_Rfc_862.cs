using System;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Windows.Networking.Sockets;
using Windows.Storage.Streams;

namespace Networking.RFC_Foundational
{
    public class EchoServer_Rfc_862 : IDisposable
    {
        public class ServerOptions
        {
            public enum Verbosity { None, Normal, Verbose }
            public Verbosity LoggingLevel { get; set; } = ServerOptions.Verbosity.Normal;
            /// <summary>
            /// DateTime format to use when writing back data. O=round trip format  F=common format.
            /// </summary>
            public string DateTimeFormat { get; internal set; } = "O";
            /// <summary>
            /// Service is the fancy name for "port". Set to 10013 to work with WinRT; RFC compliant value is 13.
            /// </summary>
            public string Service { get; set; } = "10013";

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

        public EchoServer_Rfc_862(string service = "10007") // actually, the real service is on port 7, but that's restricted
        {
            Service = service;
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


        /// <summary>
        /// Service is the fancy name for "port"
        /// </summary>
        public string Service;

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

        public async Task StartAsync()
        {
            TcpListener = new StreamSocketListener();
            TcpListener.ConnectionReceived += Listener_ConnectionReceived;
            try
            {
                await TcpListener.BindServiceNameAsync(Service);
                {
                    Log ($"Echo Connected on Tcp {TcpListener.Information.LocalPort}");
                }
            }
            catch (Exception e)
            {
                Stats.NExceptions++;
                Log ($"ERROR: unable to start TCP server {e.Message}");
            }

            UdpListener = new DatagramSocket();
            UdpListener.MessageReceived += UdpListener_MessageReceived;
            try
            {
                await UdpListener.BindServiceNameAsync(Service);
                {
                    Log($"Echo Connected on Udp {UdpListener.Information.LocalPort}");
                }
            }
            catch (Exception e)
            {
                Log($"ERROR: unable to start TCP server {e.Message}");
            }

            var hosts = Windows.Networking.Connectivity.NetworkInformation.GetHostNames();
            foreach (var host in hosts)
            {
                Log($"Host: {host.CanonicalName}");
            }

        }

        private async void UdpListener_MessageReceived(DatagramSocket sender, DatagramSocketMessageReceivedEventArgs args)
        {
            Log(ServerOptions.Verbosity.Verbose, $"SERVER: UDP: message recv sender remote port <<{sender.Information.RemotePort}>>");
            Stats.IncrementNConnections();
            
            var dr = args.GetDataReader();
            var remoteHost = args.RemoteAddress;
            var remotePort = args.RemotePort;
            var os = await sender.GetOutputStreamAsync(remoteHost, remotePort);

            //var dw = new DataWriter(sender.OutputStream);
            var dw = new DataWriter(os);
            Task t = EchoUdpAsync("UDP", dr, dw);
            await t;
        }

        private async void Listener_ConnectionReceived(StreamSocketListener sender, StreamSocketListenerConnectionReceivedEventArgs args)
        {
            Stats.IncrementNConnections();
            var socket = args.Socket;
            var dr = new DataReader(socket.InputStream);
            dr.InputStreamOptions = InputStreamOptions.Partial; // | InputStreamOptions.ReadAhead;
            var dw = new DataWriter(socket.OutputStream);
            Task t = EchoTcpAsync("TCP", dr, dw);
            await t;
        }


        private async Task EchoTcpAsync (string type, DataReader dr, DataWriter dw)
        {
            try
            {
                uint count = 0;
                do
                {
                    await dr.LoadAsync(2048); // Will load nothing when the client closes gracefully.
                    count = dr.UnconsumedBufferLength;
                    if (count > 0)
                    {
                        byte[] buffer = new byte[count];
                        dr.ReadBytes(buffer);
                        Stats.NBytesRead += count;
                        LogEchoBuffer(buffer);
                        try
                        {
                            dw.WriteBytes(buffer);
                            await dw.StoreAsync();
                            Stats.IncrementNResponses();
                        }
                        catch (Exception ex)
                        {
                            Stats.NExceptions++;
                            Log($"SERVER: ECHO: {type} exception while writing {ex.Message}");
                        }
                    }
                    else // socket is done
                    {
                        Log($"SERVER: {type} closing down the current reading socket");
                        await dw.FlushAsync();
                        dw.Dispose();
                    }
                }
                while (count > 0);
            }
            catch (Exception ex)
            {
                Stats.NExceptions++;
                Log($"SERVER: ECHO: {type} exception while reading {ex.Message}");
            }
        }

        private async Task EchoUdpAsync(string type, DataReader dr, DataWriter dw)
        {
            uint count = dr.UnconsumedBufferLength;
            if (count > 0)
            {
                byte[] buffer = new byte[count];
                dr.ReadBytes(buffer);
                Stats.NBytesRead += count;
                LogEchoBuffer(buffer);
                try
                {
                    dw.WriteBytes(buffer);
                    await dw.StoreAsync();
                    Stats.IncrementNResponses();
                }
                catch (Exception ex)
                {
                    Stats.NExceptions++;
                    Log($"SERVER: ECHO: {type} exception while writing {ex.Message}");
                }
            }
            else // socket is done
            {
                Log($"SERVER: {type} closing down the current reading socket");
                await dw.FlushAsync();
                dw.Dispose();
            }
        }


        private void LogEchoBuffer (byte[] buffer)
        {
            var sb = new StringBuilder();
            foreach (var b in buffer)
            {
                if (b >= 32 && b< 128)
                {
                    sb.Append((char)b);
                }
                else
                {
                    sb.Append($"0x{b:X2}");
                }
            }
            Log ( $"ECHO: SERVER: got buffer length={buffer.Length} {sb.ToString()}");
        }
    }
}
