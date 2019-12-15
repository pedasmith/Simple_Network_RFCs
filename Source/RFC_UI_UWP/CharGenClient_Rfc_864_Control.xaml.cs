﻿using Networking.RFC_Foundational;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.Networking;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;

// The User Control item template is documented at https://go.microsoft.com/fwlink/?LinkId=234236

namespace Networking.RFC_UI_UWP
{
    public sealed partial class CharGenClient_Rfc_864_Control : UserControl
    {
        public CharGenClient_Rfc_864_Control()
        {
            this.InitializeComponent();
            this.DataContext = this; // Set up the DataContext so the data binding to the WellKnownHosts list works
        }


        /// <summary>
        /// TODO: correct list. Are there any public chargen servers?
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
            public string Service { get; set; } = "13";
            public override string ToString()
            {
                return Host;
            }
        }

        CharGenClient_Rfc_864 client;
        private async void OnSend(object sender, RoutedEventArgs e)
        {
            try
            {
                var host = new HostName(uiAddress.Text);
                var service = uiService.Text;
                var data = uiData.Text;
                var ptype = uiProtocolType.IsOn ? CharGenClient_Rfc_864.ProtocolType.Udp : CharGenClient_Rfc_864.ProtocolType.Tcp; // double-checked; off is TCP.

                if (client == null)
                {
                    client = new CharGenClient_Rfc_864();
                    client.LogEvent += Client_LogEvent;
                }
                await client.SendAsync(host, service, ptype, data);
            }
            catch (Exception ex)
            {
                Client_LogEvent(this, $"ERROR: Client: Send exception {ex.Message} for host {uiAddress.Text}");
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
            await client.CloseAsync();
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
