using Android.App;
using Android.Content;
using Android.Content.PM;
using Android.OS;
using AndroidX.Activity;

namespace SenangRetails
{
    [Activity(Theme = "@style/Maui.SplashTheme", MainLauncher = true, ConfigurationChanges = ConfigChanges.ScreenSize | ConfigChanges.Orientation | ConfigChanges.UiMode | ConfigChanges.ScreenLayout | ConfigChanges.SmallestScreenSize | ConfigChanges.Density)]
    public class MainActivity : MauiAppCompatActivity
    {
        private OnBackPressedCallback? _backPressedCallback;

        protected override void OnCreate(Bundle? savedInstanceState)
        {
            ApplyPreferredLocale();
            base.OnCreate(savedInstanceState);

            // Log device information FIRST
            LogDeviceInformation();

            // Initialize Imin Printer
            Platforms.Android.IminPrinterInitializer.Initialize(this);

            // Register broadcast receiver for printer status
            RegisterPrinterStatusReceiver();

            // Run a printer test after 3 seconds
            Task.Run(async () =>
            {
                await Task.Delay(3000);
                await TestPrinterQRCapability();
            });

        }

        // This method to log device info
        private void LogDeviceInformation()
        {
            try
            {
                System.Console.WriteLine("========================================");
                System.Console.WriteLine("[MainActivity] DEVICE INFORMATION");
                System.Console.WriteLine("========================================");
                System.Console.WriteLine($"Manufacturer: {Build.Manufacturer}");
                System.Console.WriteLine($"Brand: {Build.Brand}");
                System.Console.WriteLine($"Model: {Build.Model}");
                System.Console.WriteLine($"Device: {Build.Device}");
                System.Console.WriteLine($"Product: {Build.Product}");
                System.Console.WriteLine($"Board: {Build.Board}");
                System.Console.WriteLine($"Hardware: {Build.Hardware}");
                System.Console.WriteLine($"Fingerprint: {Build.Fingerprint}");
                System.Console.WriteLine($"SDK Version: {Build.VERSION.SdkInt}");
                System.Console.WriteLine("========================================");

                // Run the detection logic here
                bool isImin = Platforms.Android.IminPrinterInitializer.IsRunningOnIminDevice();
                System.Console.WriteLine($"[MainActivity] Is Imin Device: {isImin}");

                bool isPrinterAvailable = Platforms.Android.IminPrinterInitializer.IsIminPrinterAvailable();
                System.Console.WriteLine($"[MainActivity] Is Printer Available: {isPrinterAvailable}");

                System.Console.WriteLine("========================================");
            }
            catch (System.Exception ex)
            {
                System.Console.WriteLine($"[MainActivity] Error logging device info: {ex.Message}");
            }
        }

        protected override void OnResume()
        {
            base.OnResume();
            ApplyPreferredLocale();
        }

       

        private async Task TestPrinterQRCapability()
        {
            try
            {
                System.Console.WriteLine("[MainActivity] ============================================");
                System.Console.WriteLine("[MainActivity] Testing printer capabilities...");
                System.Console.WriteLine("[MainActivity] ============================================");

                // Check if on Imin device
                if (!Platforms.Android.IminPrinterInitializer.IsRunningOnIminDevice())
                {
                    System.Console.WriteLine("[MainActivity] Not running on Imin device - skipping printer test");
                    return;
                }

                // Check if printer available
                if (!Platforms.Android.IminPrinterInitializer.IsIminPrinterAvailable())
                {
                    System.Console.WriteLine("[MainActivity] Printer not available - skipping printer test");
                    return;
                }

                System.Console.WriteLine("[MainActivity] Priming QR code module...");

                // Ensure warm-up
                await Platforms.Android.IminPrinterInitializer.WarmUpPrinter();
                await Task.Delay(500);

                System.Console.WriteLine("[MainActivity] Printer initialization completed");
            }
            catch (System.Exception ex)
            {
                System.Console.WriteLine($"[MainActivity] Printer test error: {ex.Message}");
            }
        }

        private PrinterStatusReceiver? _printerStatusReceiver;

        private void RegisterPrinterStatusReceiver()
        {
            try
            {
                _printerStatusReceiver = new PrinterStatusReceiver();
                var filter = new IntentFilter();
                filter.AddAction("com.imin.printerservice.PRITER_STATUS_CHANGE");

                if (Build.VERSION.SdkInt >= BuildVersionCodes.Tiramisu)
                    RegisterReceiver(_printerStatusReceiver, filter, ReceiverFlags.NotExported);
                else
                    RegisterReceiver(_printerStatusReceiver, filter);

                System.Console.WriteLine("[MainActivity] Printer status receiver registered");
            }
            catch (System.Exception ex)
            {
                System.Console.WriteLine($"[MainActivity] Failed to register receiver: {ex.Message}");
            }
        }

     
        private void ApplyPreferredLocale()
        {
            try
            {
                var savedCulture = Preferences.Get("AppCulture", Java.Util.Locale.Default?.ToLanguageTag() ?? "en");
                var localeTag = savedCulture.Replace('-', '_');
                var locale = new Java.Util.Locale(localeTag);

                Java.Util.Locale.Default = locale;

                var resources = Resources;
                if (resources == null) return;

                var config = new Android.Content.Res.Configuration(resources.Configuration);
                config.SetLocale(locale);

                resources.UpdateConfiguration(config, resources.DisplayMetrics);
            }
            catch { }
        }

        private class PrinterStatusReceiver : BroadcastReceiver
        {
            public override void OnReceive(Context? context, Intent? intent)
            {
                if (intent?.Action == "com.imin.printerservice.PRITER_STATUS_CHANGE")
                {
                    int status = intent.GetIntExtra("status", -1);
                    System.Console.WriteLine($"[IminPrinter] Status changed: {status}");

                    // Reset warm-up state on status change
                    if (status != 1)
                    {
                        Platforms.Android.IminPrinterInitializer.ResetWarmUpState();
                    }
                }
            }
        }

        
        

        
    }
}
