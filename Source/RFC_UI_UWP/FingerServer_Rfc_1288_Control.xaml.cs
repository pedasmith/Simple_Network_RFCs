using Networking.RFC_Foundational;
using System;
using System.Threading.Tasks;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

// The User Control item template is documented at https://go.microsoft.com/fwlink/?LinkId=234236

namespace Networking.RFC_UI_UWP
{
    public sealed partial class FingerServer_Rfc_1288_Control : UserControl
    {
        FingerServer_Rfc_1288 Server;
        Task ServerTask;

        public FingerServer_Rfc_1288_Control()
        {
            this.InitializeComponent();
            this.Loaded += FingerServer_Rfc_1288_Control_Loaded;
        }

        private void FingerServer_Rfc_1288_Control_Loaded(object sender, RoutedEventArgs e)
        {
        }

        private void OnStartServers(object sender, RoutedEventArgs e)
        {
            var options = new FingerServer_Rfc_1288.ServerOptions()
            {
                Service = uiService.Text,
            };

            Server = new FingerServer_Rfc_1288(options);
            Server.LogEvent += Server_LogEvent;
            ServerTask = Server.StartAsync();
        }

        private async void Server_LogEvent(object sender, string str)
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
    }
}
