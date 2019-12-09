using RFC_Foundational;
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

namespace RFC_UI_UWP
{
    public sealed partial class TimeClient_Rfc_868_Control : UserControl
    {
        public TimeClient_Rfc_868_Control()
        {
            this.InitializeComponent();
        }
        TimeClient_Rfc_868 client;
        private async void OnSend(object sender, RoutedEventArgs e)
        {
            try
            {
                var host = new HostName(uiAddress.Text);
                var service = uiService.Text;
                var data = uiData.Text;
                var ptype = uiProtocolType.IsOn ? TimeClient_Rfc_868.ProtocolType.Udp : TimeClient_Rfc_868.ProtocolType.Tcp; // double-checked; off is TCP.

                if (client == null)
                {
                    client = new TimeClient_Rfc_868();
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
