using RFC_Foundational;
using System;
using System.Threading.Tasks;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

// The User Control item template is documented at https://go.microsoft.com/fwlink/?LinkId=234236

namespace RFC_UI_UWP
{
    public sealed partial class EchoServer_Rfc_862_Control : UserControl
    {
        EchoServer_Rfc_862 Server;
        Task ServerTask;

        public EchoServer_Rfc_862_Control()
        {
            this.InitializeComponent();
            this.Loaded += EchoServer_Rfc_862_Control_Loaded;
        }

        private void EchoServer_Rfc_862_Control_Loaded(object sender, RoutedEventArgs e)
        {
        }

        private void OnStartServers(object sender, RoutedEventArgs e)
        {
            Server = new EchoServer_Rfc_862(uiService.Text);
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
