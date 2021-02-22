using Networking.RFC_Foundational;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media;

// The User Control item template is documented at https://go.microsoft.com/fwlink/?LinkId=234236

namespace Networking.RFC_UI_UWP
{
    public sealed partial class FingerClient_Rfc_1288_Control : UserControl
    {
        public FingerClient_Rfc_1288_Control()
        {
            this.InitializeComponent();

            var serviceList = LittleTcpService_Rfc_848.ServiceList;
            foreach (var serviceItem in serviceList)
            {
                if (serviceItem.ServiceName == "finger")
                {
                    WellKnownHosts.Add(new HostService(serviceItem.HostAddress.CanonicalName, serviceItem.Service));
                }
            }

            this.DataContext = this; // Set up the DataContext so the data binding to the WellKnownHosts list works
        }

        /// <summary>
        /// List of well know hosts/services that the user can try. These aren't guaranteed to work!
        /// </summary>
        public List<HostService> WellKnownHosts { get; } = new List<HostService>()
        {
            new HostService("coke@cs.cmu.edu"),

            new HostService("phlog@1436.ninja"),
            new HostService("redacted@1436.ninja"),
            new HostService("twitpher@1436.ninja"),

            new HostService("@finger.farm"),
            new HostService("about@finger.farm"),
            new HostService("help@finger.farm"),
            new HostService("finger@finger.farm"),
            new HostService("info@finger.farm"),

            new HostService("@telehack.com"),
        };
        public class HostService
        {
            public HostService(string host, string service = null)
            {
                Host = host;
                if (service != null) Service = service;
            }
            public string Host { get; set; } = "example.com";
            public string Service { get; set; } = FingerServer_Rfc_1288.ServerOptions.RfcService;
            public override string ToString()
            {
                return Host;
            }
        }

        FingerClient_Rfc_1288 client;

        private void DoWait (bool doWait)
        {
            uiWait.Visibility = doWait ? Visibility.Visible : Visibility.Collapsed;
        }

        private async void OnSend(object sender, RoutedEventArgs e)
        {
            try
            {
                DoWait(true);

                // Might be user@example.com
                // OR just @example.com OR example.com
                // OR finger://user@example.com//W
                // OR finger://example.com/user

                ParsedFingerCommand request = ParsedFingerCommand.ParseFromUxString(uiAddress.Text.Trim(), uiService.Text, uiWSwitch.IsOn);

                if(request == null)
                {
                    Client_LogEvent(this, $"ERROR: Client: can't parse finger {uiAddress.Text}. Should be person@example.com (for example)");
                }
                else
                {
                    uiService.Text = request.SendToPort;
                    uiWSwitch.IsOn = request.HasWSwitch;

                    if (client == null)
                    {
                        client = new FingerClient_Rfc_1288();
                        client.LogEvent += Client_LogEvent;
                    }

                    await client.WriteTcpAsync(request);
                }
            }
            catch (Exception ex)
            {
                Client_LogEvent(this, $"ERROR: Client: Send exception {ex.Message} for host {uiAddress.Text}");
            }
            DoWait(false);
        }

        /// <summary>
        /// Used when there's a URI to send seperate from the normal UI.
        /// </summary>
        /// <param name="uri"></param>
        /// <returns></returns>
        public async Task DoSendUri(Uri uri)
        {
            try
            {
                DoWait(true);

                // finger://user@example.com//W
                // OR finger://example.com/user
                // OR finger://example.com//W%20user

                var request = ParsedFingerCommand.ParseFromUri(uri);
                if (request == null)
                {
                    Client_LogEvent(this, $"ERROR: Client: can't parse finger {uri}. Should be finger://user@example.com (for example)");
                }
                else
                {
                    // Update UI from the URI via the request
                    uiAddress.Text = request.OriginalCommand;
                    uiService.Text = request.SendToPort;
                    uiWSwitch.IsOn = request.HasWSwitch;

                    if (client == null)
                    {
                        client = new FingerClient_Rfc_1288();
                        client.LogEvent += Client_LogEvent;
                    }

                    await client.WriteTcpAsync(request);
                }
            }
            catch (Exception ex)
            {
                Client_LogEvent(this, $"ERROR: Client: Send exception {ex.Message} for host {uri.Host}");
            }
            DoWait(false);
        }

        FontFamily FixedWidth = new FontFamily("Consolas");

        private async void Client_LogEvent(object sender, string str)
        {
            var ncr = str.Count((c) => { return c == '\n'; });
            if (ncr > 5)
            {
                // old-fashioned plan file deserves a courier-type font!
                uiLog.FontFamily = FixedWidth;
            }
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
            DoWait(false);
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
            var cmd = ParsedFingerCommand.ParseFromUxString(uiAddress.Text, uiService.Text, uiWSwitch.IsOn);
            uiService.Text = cmd.SendToPort;
            uiWSwitch.IsOn = cmd.HasWSwitch;
        }

        private void OnClear(object sender, RoutedEventArgs e)
        {
            uiLog.Text = "";
        }

        private void OnUserKeyDown(object sender, Windows.UI.Xaml.Input.KeyRoutedEventArgs e)
        {
            if (e.Key == Windows.System.VirtualKey.Enter)
            {
                e.Handled = true;
                OnSend(null, null);
            }
        }
    }
}
