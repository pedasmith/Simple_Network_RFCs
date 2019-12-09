using RFC_Foundational;
using System;
using Windows.Networking;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

// The User Control item template is documented at https://go.microsoft.com/fwlink/?LinkId=234236

namespace RFC_UI_UWP
{
    public sealed partial class DaytimeClient_Rfc_867_Control : UserControl
    {
        public DaytimeClient_Rfc_867_Control()
        {
            this.InitializeComponent();
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

    }
}
