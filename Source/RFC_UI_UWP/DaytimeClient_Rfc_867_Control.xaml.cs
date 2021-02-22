using Networking.RFC_Foundational;
using System;
using System.Collections.Generic;
using Windows.Networking;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

// The User Control item template is documented at https://go.microsoft.com/fwlink/?LinkId=234236

namespace Networking.RFC_UI_UWP
{

    public sealed partial class DaytimeClient_Rfc_867_Control : UserControl
    {
        public DaytimeClient_Rfc_867_Control()
        {
            this.InitializeComponent();

            var serviceList = LittleTcpService_Rfc_848.ServiceList;
            foreach (var serviceItem in serviceList)
            {
                if (serviceItem.ServiceName == "daytime")
                {
                    WellKnownHosts.Add(new HostService(serviceItem.HostAddress.CanonicalName, serviceItem.Service));
                }
            }

            this.DataContext = this; // Set up the DataContext so the data binding to the WellKnownHosts list works
        }


        /// <summary>
        /// List of well know hosts/services that the user can try. These aren't guaranteed to work!
        /// As of 2019-12-14, the TCP services seem to work but not the UDP ones.
        /// See https://www.nist.gov/pml/time-and-frequency-division/services/internet-time-service-its for servers
        /// </summary>
        public List<HostService> WellKnownHosts { get; } = new List<HostService>()
        {
            new HostService("localhost", "10013"),
            new HostService("time.nist.gov"), // NIST format is like JJJJJ YR-MO-DA HH:MM:SS TT L H msADV UTC(NIST) OTM
            new HostService("time-a-g.nist.gov"),
            new HostService("time-a-b.nist.gov"),
            new HostService("time-a-wwv.nist.gov"),
            new HostService("utcnist.colorado.edu"),
            new HostService("utcnist2.colorado.edu"),
        };
        public class HostService
        {
            public HostService(string host, string service = null)
            {
                Host = host;
                if (service != null) Service = service;
            }
            public string Host { get; set; } = "example.com";
            public string Service { get; set; } = DaytimeServer_Rfc_867.ServerOptions.RfcService;
            public override string ToString()
            {
                return Host;
            }
        }

        DaytimeClient_Rfc_867 client;
        private async void OnSend(object sender, RoutedEventArgs e)
        {
            try
            {
                var host = new HostName(uiAddress.Text);
                var service = uiService.Text;
                var data = uiData.Text;
                var ptype = uiProtocolType.IsOn ? DaytimeClient_Rfc_867.ProtocolType.Udp : DaytimeClient_Rfc_867.ProtocolType.Tcp; // double-checked; off is TCP.

                if (client == null)
                {
                    client = new DaytimeClient_Rfc_867();
                    client.LogEvent += Client_LogEvent;
                }
                await client.WriteAsync(host, service, ptype, data);
            }
            catch (Exception ex)
            {
                Client_LogEvent(this, $"ERROR: Client: Write exception {ex.Message} for host {uiAddress.Text}");
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
                await Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, () =>
                {
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
