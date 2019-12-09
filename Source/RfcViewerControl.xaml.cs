using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.Storage;
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
    public sealed partial class RfcViewerControl : UserControl
    {
        public string RfcName { get; set; } = "rfc862.txt";
        public RfcViewerControl()
        {
            this.InitializeComponent();
            this.Loaded += RfcViewerControl_Loaded;
        }

        private async void RfcViewerControl_Loaded(object sender, RoutedEventArgs e)
        {
            try
            {
                string dname = @"Assets\RFC\";

                // Read in the full set of devices
                StorageFolder InstallationFolder = Windows.ApplicationModel.Package.Current.InstalledLocation;
                var dir = await InstallationFolder.GetFolderAsync(dname);
                var files = await dir.GetFilesAsync();
                foreach (var file in files)
                {
                    if (file.Name.Contains (RfcName))
                    {
                        var contents = File.ReadAllText(file.Path);
                        uiContent.Text = contents;
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"ERROR: Unable to looad RFC: {ex.Message}");
            }
        }
    }
}
