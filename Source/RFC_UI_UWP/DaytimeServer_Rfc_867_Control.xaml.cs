﻿using Networking.RFC_Foundational;
using System;
using System.Threading.Tasks;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

// The User Control item template is documented at https://go.microsoft.com/fwlink/?LinkId=234236

namespace Networking.RFC_UI_UWP
{
    public sealed partial class DaytimeServer_Rfc_867_Control : UserControl
    {
        DaytimeServer_Rfc_867 Server;
        Task ServerTask;

        public DaytimeServer_Rfc_867_Control()
        {
            this.InitializeComponent();
            this.Loaded += DaytimeServer_Rfc_867_Control_Loaded;
        }

        private void DaytimeServer_Rfc_867_Control_Loaded(object sender, RoutedEventArgs e)
        {
        }

        private void OnStartServers(object sender, RoutedEventArgs e)
        {
            var serverOptions = new DaytimeServer_Rfc_867.ServerOptions()
            {
                Service = uiService.Text
            };
            Server = new DaytimeServer_Rfc_867(serverOptions);
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
