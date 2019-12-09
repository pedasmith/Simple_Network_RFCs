using System;
using System.Text;
using System.Threading.Tasks;
using Windows.Networking.Sockets;
using Windows.Storage.Streams;

namespace RFC_Foundational
{
    public class EchoServer_Rfc_862
    {
        public delegate void LogEventHandler(object sender, string str);
        public event LogEventHandler LogEvent;

        public EchoServer_Rfc_862(string service = "10007") // actually, the real service is on port 7, but that's restricted
        {
            Service = service;
        }
        public enum State {  NotStarted, Error, Running};
        public int NConnections = 0;

        /// <summary>
        /// Service is the fancy name for "port"
        /// </summary>
        public string Service;

        StreamSocketListener TcpListener = null;
        DatagramSocket UdpListener = null;
        private void Log(string str)
        {
            LogEvent?.Invoke(this, str);
            System.Diagnostics.Debug.WriteLine(str);
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
            var dr = args.GetDataReader();
            var remoteHost = args.RemoteAddress;
            var remotePort = args.RemotePort;
            var os = await sender.GetOutputStreamAsync(remoteHost, remotePort);

            //var dw = new DataWriter(sender.OutputStream);
            var dw = new DataWriter(os);
            Task t = EchoAsync("UDP", dr, DataReaderType.Buffer, dw);
            await t;
        }

        private async void Listener_ConnectionReceived(StreamSocketListener sender, StreamSocketListenerConnectionReceivedEventArgs args)
        {
            NConnections++;
            var socket = args.Socket;
            var dr = new DataReader(socket.InputStream);
            dr.InputStreamOptions = InputStreamOptions.Partial; // | InputStreamOptions.ReadAhead;
            var dw = new DataWriter(socket.OutputStream);
            Task t = EchoAsync("TCP", dr, DataReaderType.Stream, dw);
            await t;
        }
        public enum DataReaderType { Stream, Buffer };

        private async Task EchoAsync (string type, DataReader dr, DataReaderType drt, DataWriter dw)
        {
            try
            {
                uint count = 0;
                do
                {
                    if (drt == DataReaderType.Stream)
                    {
                        await dr.LoadAsync(2048);
                    }
                    count = dr.UnconsumedBufferLength;
                    if (count > 0)
                    {
                        byte[] buffer = new byte[count];
                        dr.ReadBytes(buffer);
                        LogEchoBuffer(buffer);
                        try
                        {
                            dw.WriteBytes(buffer);
                            await dw.StoreAsync();
                        }
                        catch (Exception ex)
                        {
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
                Log($"SERVER: ECHO: {type} exception while reading {ex.Message}");
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
