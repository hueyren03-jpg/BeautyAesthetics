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
                if (args.WebView is Android.Webkit.WebView webView)
                {
                    webView.SetWebChromeClient(new CameraEnabledBlazorWebChromeClient());
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
