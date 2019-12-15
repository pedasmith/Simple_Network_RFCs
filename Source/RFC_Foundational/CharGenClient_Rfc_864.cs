﻿using System;
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
    /// TODO: this is really similar to the echo client -- what should happen to merge them into
    /// one complete class that handles both cases?
    /// </summary>
    class CharGenClient_Rfc_864
    {
        DatagramSocket udpSocket;
        StreamSocket tcpSocket;
        DataWriter tcpDw;
        DataWriter udpDw;
        public delegate void LogEventHandler(object sender, string str);
        public event LogEventHandler LogEvent;

        public async Task CloseAsync()
        {
            if (tcpSocket != null)
            {
                await tcpDw.FlushAsync();
                tcpDw.Dispose();
                tcpSocket.Dispose();
                tcpSocket = null;
                tcpDw = null;
            }
        }
        private void Log(string str)
        {
            LogEvent?.Invoke(this, str);
            System.Diagnostics.Debug.WriteLine(str);
        }
        Task ReadTask = null;
        public enum ProtocolType { Tcp, Udp }
        public async Task SendAsync(HostName address, string service, ProtocolType protocolType, string data)
        {

            try
            {
                if (protocolType == ProtocolType.Tcp && tcpSocket == null)
                {
                    tcpSocket = new StreamSocket();
                    await tcpSocket.ConnectAsync(address, service);
                    tcpDw = new DataWriter(tcpSocket.OutputStream);

                    // Now read everything
                    var dr = new DataReader(tcpSocket.InputStream);
                    dr.InputStreamOptions = InputStreamOptions.Partial;
                    ReadTask = ReadAsync(dr, DataReaderType.Stream);
                }
                if (protocolType == ProtocolType.Udp && udpSocket == null)
                {
                    udpSocket = new DatagramSocket();
                    await udpSocket.ConnectAsync(address, service);
                    udpDw = new DataWriter(udpSocket.OutputStream);

                    // Now read everything
                    udpSocket.MessageReceived += UdpSocket_MessageReceived;
                }
                var dw = protocolType == ProtocolType.Tcp ? tcpDw : udpDw;

                dw.WriteString(data);
                await dw.StoreAsync();

            }
            catch (Exception ex)
            {
                Log($"ERROR: Client: sending {data} to {address} exception {ex.Message}");
            }
        }

        private void UdpSocket_MessageReceived(DatagramSocket sender, DatagramSocketMessageReceivedEventArgs args)
        {
            var dr = args.GetDataReader();
            dr.InputStreamOptions = InputStreamOptions.Partial; // | InputStreamOptions.ReadAhead;
            ReadTask = ReadAsync(dr, DataReaderType.Buffer);
        }

        public enum DataReaderType { Stream, Buffer };

        private async Task ReadAsync(DataReader dr, DataReaderType drt)
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
                        byte[] buffer = new byte[dr.UnconsumedBufferLength];
                        dr.ReadBytes(buffer);
                        LogEchoBuffer(buffer);
                    }
                    else // socket is done
                    {
                    }
                }
                while (count > 0);
            }
            catch (Exception ex)
            {
                Log($"ECHO: CLIENT: Exception {ex.Message}");
            }
        }

        private void LogEchoBuffer(byte[] buffer)
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
            Log($"ECHO: CLIENT: got buffer length={buffer.Length} {sb.ToString()}");
        }
    }
}