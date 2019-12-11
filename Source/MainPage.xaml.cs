using RFC_Foundational_Tests;
using System;
using Windows.Networking;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;


namespace RFC_Foundational
{
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class MainPage : Page
    {
        public MainPage()
        {
            this.InitializeComponent();
            this.Loaded += MainPage_Loaded;
        }

        private void MainPage_Loaded(object sender, RoutedEventArgs e)
        {
            DisplayRequestRequestActive(true); // Windows will turn this off automatically.

            //var testTask = DaytimeTest_Rfc_867.Test();
            //await testTask;

            int nerror = 0;
            nerror += TimeServer_Rfc_868.TimeConversion.TestCalendar();
        }

        static Windows.System.Display.DisplayRequest CurrDisplayRequest = null;

        private void DisplayRequestRequestActive (bool screenStaysOn)
        {
            if (CurrDisplayRequest == null)
            {
                CurrDisplayRequest = new Windows.System.Display.DisplayRequest();
            }
            if (screenStaysOn)
            {
                CurrDisplayRequest.RequestActive();
            }
            else
            {
                CurrDisplayRequest.RequestRelease();
            }
        }

        private async void OnShowRfc(object sender, RoutedEventArgs e)
        {
            await uiRfcViewerControl.ReloadRfcAsync();
            uiRfcViewer.Visibility = (uiRfcViewer.Visibility == Visibility.Collapsed) ? Visibility.Visible : Visibility.Collapsed;
        }

        private async void OnSelectMenu(NavigationView sender, NavigationViewSelectionChangedEventArgs args)
        {
            var select = (args.SelectedItem as FrameworkElement).Tag as string;
            foreach (var item in uiControls.Children)
            {
                var fe = item as FrameworkElement;
                var visibility = ((fe?.Tag as string) == select) ? Visibility.Visible : Visibility.Collapsed;
                fe.Visibility = visibility;
            }

            // Set up the RFC viewer. This should be rewritten so that new items
            // don't require hand editing to make this work.
            switch (select)
            {
                case "Rfc_862":
                    await uiRfcViewerControl.SetContentsTitle("ECHO (RFC 862)", "rfc862.txt");
                    break;
                case "Rfc_867":
                    await uiRfcViewerControl.SetContentsTitle("Daytime (RFC 867)", "rfc867.txt");
                    break;
                case "Rfc_868":
                    await uiRfcViewerControl.SetContentsTitle("Time (RFC 868)", "rfc868.txt");
                    break;
            }
        }
    }
}
