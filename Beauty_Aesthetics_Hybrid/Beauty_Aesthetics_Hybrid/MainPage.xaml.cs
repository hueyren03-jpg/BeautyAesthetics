using System.Diagnostics;

namespace Beauty_Aesthetics_Hybrid
{
    public partial class MainPage : ContentPage
    {
        public MainPage()
        {
            InitializeComponent();

#if ANDROID
            blazorWebView.BlazorWebViewInitialized += (_, args) =>
            {
                if (args.WebView is not Android.Webkit.WebView webView)
                {
                    return;
                }

                // Some Android/.NET 10 combinations can fail while creating a
                // custom Android.Webkit.WebChromeClient. Do not let an optional
                // camera integration prevent the entire POS from starting.
                // If construction fails, leave BlazorWebView's built-in chrome
                // client in place and continue loading the app normally.
                try
                {
                    var cameraChromeClient = new CameraEnabledBlazorWebChromeClient();
                    webView.SetWebChromeClient(cameraChromeClient);
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Camera WebChromeClient unavailable; using the default BlazorWebView client. {ex}");
                }
            };

            var context = Android.App.Application.Context;
            var resources = context?.Resources;
            var resourceId = resources?.GetIdentifier("status_bar_height", "dimen", "android") ?? 0;
            if (resourceId > 0 && resources != null)
            {
                var statusBarHeight = resources.GetDimensionPixelSize(resourceId);
                var density = Microsoft.Maui.Devices.DeviceDisplay.Current?.MainDisplayInfo.Density ?? 1.0;
                var statusBarHeightDp = statusBarHeight / density;
                this.Padding = new Thickness(0, statusBarHeightDp, 0, 0);
            }
            else
            {
                this.Padding = new Thickness(0, 36, 0, 0);
            }
#endif
        }
    }
}
