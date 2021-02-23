﻿using Networking.RFC_Foundational;
using System;
using System.Collections.Generic;
using Windows.Networking;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

// The User Control item template is documented at https://go.microsoft.com/fwlink/?LinkId=234236

namespace Networking.RFC_UI_UWP
{
    public sealed partial class EchoClient_Rfc_862_Control : UserControl
    {
        public EchoClient_Rfc_862_Control()
        {
            this.InitializeComponent();

            var serviceList = LittleTcpService_Rfc_848.ServiceList;
            foreach (var serviceItem in serviceList)
            {
                if (serviceItem.ServiceName == "echo")
                {
                    WellKnownHosts.Add(new HostService(serviceItem.HostAddress.CanonicalName, serviceItem.Service));
                }
            }

            this.DataContext = this; // Set up the DataContext so the data binding to the WellKnownHosts list works
        }

        /// <summary>
        /// TODO: correct list. Are there any public echo servers?
        /// List of well know hosts/services that the user can try. These aren't guaranteed to work!
        /// </summary>
        public List<HostService> WellKnownHosts { get; } = new List<HostService>()
        {
            new HostService("localhost", "10007"),
        };
        public class HostService
        {
            public HostService(string host, string service = null)
            {
                Host = host;
                if (service != null) Service = service;
            }
            public string Host { get; set; } = "example.com";
            public string Service { get; set; } = EchoServer_Rfc_862.ServerOptions.RfcService;
            public override string ToString()
            {
                return Host;
            }
        }

        EchoClient_Rfc_862 client;
        private async void OnSend(object sender, RoutedEventArgs e)
        {
            try
            {
                var host = new HostName(uiAddress.Text);
                var service = uiService.Text;
                var data = uiData.Text;
                var ptype = uiProtocolType.IsOn ? EchoClient_Rfc_862.ProtocolType.Udp : EchoClient_Rfc_862.ProtocolType.Tcp; // double-checked; off is TCP.

                if (client == null)
                {
                    client = new EchoClient_Rfc_862();
                    client.LogEvent += Client_LogEvent;
                }
                await client.WriteAsync(host, service, ptype, data);
            }
            catch (Exception ex)
            {
                Client_LogEvent (this, $"ERROR: Client: Write exception {ex.Message} for host {uiAddress.Text}");
            }
        }

        private async void Client_LogEvent(object sender, string str)
        {
            if (Dispatcher.HasThreadAccess)
            {
                uiLog.Text += str + "\n";
            }
            else
            {
                await Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, () => {
                    uiLog.Text += str + "\n";
                });
            }
        }
        private async void OnClose(object sender, RoutedEventArgs e)
        {
            if (client != null)
            {
                await client.CloseAsync();
            }
            client = null;
        }

        private void OnHostsListSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (e.AddedItems.Count != 1) return;
            var item = e.AddedItems[0] as HostService;
            if (item == null) return;
            uiAddress.Text = item.Host;
            uiService.Text = item.Service;
        }
    }
}
