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

        private async void MainPage_Loaded(object sender, RoutedEventArgs e)
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

        private void OnShowRfc(object sender, RoutedEventArgs e)
        {
            uiRfcViewer.Visibility = (uiRfcViewer.Visibility == Visibility.Collapsed) ? Visibility.Visible : Visibility.Collapsed;
        }
    }
}
