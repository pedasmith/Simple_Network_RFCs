using Networking.RFC_Foundational;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading.Tasks;
using Windows.Foundation;
using Windows.Foundation.Collections;
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
    public sealed partial class CharGenServer_Rfc_864_Control : UserControl
    {
        CharGenServer_Rfc_864 Server;
        Task ServerTask;

        public CharGenServer_Rfc_864_Control()
        {
            this.InitializeComponent();
            this.Loaded += CharGenServer_Rfc_864_Control_Loaded;
        }

        private void CharGenServer_Rfc_864_Control_Loaded(object sender, RoutedEventArgs e)
        {
        }

        private void OnStartServers(object sender, RoutedEventArgs e)
        {
            var serverOptions = new CharGenServer_Rfc_864.ServerOptions()
            {
                Service = uiService.Text,
                LoggingLevel = CharGenServer_Rfc_864.ServerOptions.Verbosity.Verbose,
            };
            Server = new CharGenServer_Rfc_864(serverOptions);
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
