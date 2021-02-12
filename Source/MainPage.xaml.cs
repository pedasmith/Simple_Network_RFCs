using Networking.RFC_Foundational;
using Networking.RFC_Foundational_Tests;
using Networking.Simplest_Possible_Versions;
using System;
using System.Threading.Tasks;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;


namespace Networking
{
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class MainPage : Page
    {
        public static MainPage CurrMainPage = null;
        public MainPage()
        {
            this.InitializeComponent();
            this.Loaded += MainPage_Loaded;
            CurrMainPage = this;
        }

        private void MainPage_Loaded(object sender, RoutedEventArgs e)
        {
            DisplayRequestRequestActive(true); // Windows will turn this off automatically.
            uiNetworkInfo.Text = "\n";

            var hosts = Windows.Networking.Connectivity.NetworkInformation.GetHostNames();
            uiNetworkInfo.Text += "HOST NAMES\n================\n";
            foreach (var host in hosts)
            {
                uiNetworkInfo.Text += $"{host.CanonicalName}\n";
            }

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
            await DoSelectByTag(select);
        }
        public async Task<Grid> DoSelectByTag(string select)
        {
            Grid retval = null;
            foreach (var item in uiControls.Children)
            {
                var fe = item as FrameworkElement;
                if (fe?.Tag as string == select)
                {
                    fe.Visibility = Visibility.Visible;
                    retval = fe as Grid;
                }
                else
                {
                    fe.Visibility = Visibility.Collapsed;
                }
            }

            // Set up the RFC viewer. This should be rewritten so that new items
            // don't require hand editing to make this work.
            switch (select)
            {
                case "Rfc_862":
                    await uiRfcViewerControl.SetContentsTitle("ECHO (RFC 862)", "rfc862.txt");
                    break;
                case "Rfc_864":
                    await uiRfcViewerControl.SetContentsTitle("Character Generator (RFC 864)", "rfc864.txt");
                    break;
                case "Rfc_867":
                    await uiRfcViewerControl.SetContentsTitle("Daytime (RFC 867)", "rfc867.txt");
                    break;
                case "Rfc_868":
                    await uiRfcViewerControl.SetContentsTitle("Time (RFC 868)", "rfc868.txt");
                    break;
                case "Rfc_1288":
                    await uiRfcViewerControl.SetContentsTitle("Finger (RFC 1288)", "rfc1288.txt");
                    break;
            }

            return retval;
        }

        public void LogTestError(string error)
        {
            uiSystemTestResults.Text += $"{error}\n";
        }

        public void LogTestMessage(string error)
        {
            // Logging message are just ignored for now. Later it might make sense
            // to optionally log these messages.
            // uiSystemTestResults.Text += $"{error}\n";
        }

        private async void OnSystemTestClick(object sender, RoutedEventArgs e)
        {
            uiSystemTestResults.Text = "Starting test run\n\n";
            int nerror = 0;

            nerror += await DoFullTest();
        }

        private async Task<int> DoFullTest()
        { 
            int nerror = 0;

            Infrastructure.LogError += LogTestError;
            Infrastructure.LogMessage += LogTestMessage;


            uiSystemTestResults.Text += "Simplest_Daytime_Sample_Rfc_867.RunAsync: ";
            var result867simple = await Simplest_Daytime_Sample_Rfc_867.RunAsync();
            if (!result867simple.Succeeded) nerror++;
            uiSystemTestResults.Text += $" {nerror}\n";


            uiSystemTestResults.Text += "Simplest_Time_Sample_Rfc_868.RunAsync: ";
            var result868simple = await Simplest_Time_Sample_Rfc_868.RunAsync();
            if (!result868simple.Succeeded) nerror++;
            uiSystemTestResults.Text += $" {nerror}\n";


            uiSystemTestResults.Text += "TimeServer_Rfc_868.TimeConversion.TestCalendar:";
            nerror += TimeServer_Rfc_868.TimeConversion.TestCalendar();
            uiSystemTestResults.Text += $" {nerror}\n";

            //
            // Tests for each protocol.
            //
            uiSystemTestResults.Text += "CharGenTest_Rfc_864.Test: ";
            Infrastructure.NError = 0;
            await CharGenTest_Rfc_864.Test();
            nerror += Infrastructure.NError;
            uiSystemTestResults.Text += $" {nerror}\n";

            uiSystemTestResults.Text += "DaytimeTest_Rfc_867.Test: ";
            Infrastructure.NError = 0;
            await DaytimeTest_Rfc_867.Test();
            nerror += Infrastructure.NError;
            uiSystemTestResults.Text += $" {nerror}\n";

            uiSystemTestResults.Text += "EchoTest_Rfc_862.Test: ";
            Infrastructure.NError = 0;
            await EchoTest_Rfc_862.Test();
            nerror += Infrastructure.NError;
            uiSystemTestResults.Text += $" {nerror}\n";

            uiSystemTestResults.Text += "TimeTest_Rfc_868.Test: ";
            Infrastructure.NError = 0;
            await TimeTest_Rfc_868.Test();
            nerror += Infrastructure.NError;
            uiSystemTestResults.Text += $" {nerror}\n";

            uiSystemTestResults.Text += $"\n\n\nTotal errors: {nerror}";
            Infrastructure.LogError -= LogTestError;
            Infrastructure.LogMessage -= LogTestMessage;

            return nerror;
        }

        private async void OnSystemTestUriClick(object sender, RoutedEventArgs e)
        {
            var uri = new Uri("finger://coke@cs.cmu.edu");
            var success = await Windows.System.Launcher.LaunchUriAsync(uri);
        }
    }
}
