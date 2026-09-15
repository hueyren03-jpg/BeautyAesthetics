using Android.Content;
using Android.OS;
using Android.Runtime;
using Com.Imin.Printer;
using System;
using System.Threading.Tasks;

namespace SenangRetails.Platforms.Android
{
    public static class IminPrinterInitializer
    {
        private static bool _isInitialized = false;
        private static bool _isPrinterWarmedUp = false;
        private static readonly object _lockObject = new object();  
        private static DateTime _lastWarmUpTime = DateTime.MinValue;

        public static void Initialize(Context? context)
        {
            if (_isInitialized || context == null)
                return;

            try
            {
                // Initialize printer service
                PrinterHelper.Instance?.InitPrinterService(context, new PrinterInitCallback());
                _isInitialized = true;
                System.Console.WriteLine("[IminPrinter] Printer service initialized successfully");

                // Start background warm-up after initialization
                Task.Run(async () =>
                {
                    await Task.Delay(1500); // Give SDK time to initialize
                    await PerformInitialWarmUp();
                });
            }
            catch (Exception ex)
            {
                System.Console.WriteLine($"[IminPrinter] Failed to initialize: {ex.Message}");
            }
        }

        // Check if device is actually an Imin
        public static bool IsRunningOnIminDevice()
        {
            try
            {
                string manufacturer = Build.Manufacturer?.ToLower() ?? "";
                string brand = Build.Brand?.ToLower() ?? "";
                string model = Build.Model?.ToLower() ?? "";
                string fingerprint = Build.Fingerprint?.ToLower() ?? "";
                string hardware = Build.Hardware?.ToLower() ?? "";

                // ADD DETAILED LOGGING
                Console.WriteLine($"[IminPrinter] Device Info:");
                Console.WriteLine($"  Manufacturer: {manufacturer}");
                Console.WriteLine($"  Brand: {brand}");
                Console.WriteLine($"  Model: {model}");
                Console.WriteLine($"  Fingerprint: {fingerprint}");
                Console.WriteLine($"  Hardware: {hardware}");

                bool isImin = manufacturer.Contains("imin") || brand.Contains("imin");
                Console.WriteLine($"[IminPrinter] Is Imin Device: {isImin}");

                bool isEmulator =
                    Build.Fingerprint.StartsWith("generic") ||
                    Build.Fingerprint.Contains("vbox") ||
                    Build.Fingerprint.Contains("test-keys") ||
                    Build.Product.Contains("sdk") ||           
                    Build.Product.Contains("emulator") ||     
                    model.Contains("emulator") ||
                    model.Contains("android sdk built for") ||
                    model.Contains("sdk_gphone") ||
                    manufacturer.Contains("google") ||
                    manufacturer.Contains("unknown") ||
                    brand.Contains("generic") ||
                    hardware.Contains("ranchu") ||
                    hardware.Contains("goldfish") ||
                    (Build.Device.StartsWith("generic"));

                Console.WriteLine($"[IminPrinter] Is Emulator: {isEmulator}");

                if (isEmulator)
                {
                    Console.WriteLine("[IminPrinter] Emulator detected — forcing virtual printer mode.");
                    return false;
                }

                return isImin;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[IminPrinter] Device check failed: {ex.Message}");
                return false;
            }
        }

        // Check if printer is connected properly
        public static bool IsPrinterConnected()
        {
            try
            {
                var printer = PrinterHelper.Instance;
                if (printer == null)
                    return false;

                int status = printer.PrinterStatus;
                Console.WriteLine($"[IminPrinter] Printer status: {status}");

                // Common connected states: 0–4
                return status >= 0 && status <= 4;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[IminPrinter] Failed to get printer status: {ex.Message}");
                return false;
            }
        }

        // Combined availability check
        public static bool IsIminPrinterAvailable()
        {
            // Prevent any SDK calls on emulator or non-Imin devices
            if (!IsRunningOnIminDevice())
            {
                Console.WriteLine("[IminPrinter] Not running on Imin device (or emulator detected).");
                return false;
            }

            try
            {
                var printer = PrinterHelper.Instance;
                if (printer == null)
                {
                    Console.WriteLine("[IminPrinter] PrinterHelper instance is null.");
                    return false;
                }

                int status = printer.PrinterStatus;
                Console.WriteLine($"[IminPrinter] Printer status: {status}");

                // Typical OK range: 0–4
                bool connected = status >= 0 && status <= 4;
                if (!connected)
                    Console.WriteLine("[IminPrinter] Printer not connected.");

                return connected;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[IminPrinter] Printer check failed: {ex.Message}");
                return false;
            }
        }

        private static async Task PerformInitialWarmUp()
        {
            try
            {
                var printer = PrinterHelper.Instance;
                if (printer == null)
                {
                    System.Console.WriteLine("[IminPrinter] Cannot warm up - printer not initialized");
                    return;
                }

                // Configure QR settings
                try
                {
                    printer.SetQrCodeSize(8);
                    printer.SetQrCodeErrorCorrectionLev(1);
                    System.Console.WriteLine("[IminPrinter] QR code configured (size: 8, error correction: 1)");
                }
                catch (Exception ex)
                {
                    System.Console.WriteLine($"[IminPrinter] QR config error: {ex.Message}");
                }

                lock (_lockObject)
                {
                    _isPrinterWarmedUp = true;
                    _lastWarmUpTime = DateTime.Now;
                }

                System.Console.WriteLine("[IminPrinter] Initial warm-up completed (time-based)");
            }
            catch (Exception ex)
            {
                System.Console.WriteLine($"[IminPrinter] Initial warm-up error: {ex.Message}");
            }
        }

        public static async Task<bool> WarmUpPrinter()
        {
            lock (_lockObject)
            {
                // If warmed up recently (within 5 minutes), skip
                if (_isPrinterWarmedUp && (DateTime.Now - _lastWarmUpTime).TotalMinutes < 5)
                {
                    System.Console.WriteLine("[IminPrinter] Printer already warmed up (recent)");
                    return true;
                }
            }

            try
            {
                var printer = PrinterHelper.Instance;
                if (printer == null)
                {
                    System.Console.WriteLine("[IminPrinter] Printer not initialized");
                    return false;
                }

                System.Console.WriteLine("[IminPrinter] Starting on-demand printer warm-up (time-based)...");

                // Perform aggressive warm-up sequence - IGNORE STATUS
                for (int attempt = 0; attempt < 3; attempt++)
                {
                    try
                    {
                        printer.PrintText(" ", null);
                        printer.PrintAndLineFeed();
                        await Task.Delay(350);
                    }
                    catch (Exception ex)
                    {
                        System.Console.WriteLine($"[IminPrinter] Warm-up attempt {attempt + 1} error: {ex.Message}");
                    }
                }

                // Configure QR settings
                try
                {
                    printer.SetQrCodeSize(8);
                    printer.SetQrCodeErrorCorrectionLev(1);
                }
                catch (Exception ex)
                {
                    System.Console.WriteLine($"[IminPrinter] QR config error: {ex.Message}");
                }

                lock (_lockObject)
                {
                    _isPrinterWarmedUp = true;
                    _lastWarmUpTime = DateTime.Now;
                }

                System.Console.WriteLine("[IminPrinter] ✓ Printer warmed up successfully (time-based)");
                return true;
            }
            catch (Exception ex)
            {
                System.Console.WriteLine($"[IminPrinter] Warm-up failed: {ex.Message}");
                return false;
            }
        }

        public static void ResetWarmUpState()
        {
            lock (_lockObject)
            {
                _isPrinterWarmedUp = false;
                _lastWarmUpTime = DateTime.MinValue;
            }
            System.Console.WriteLine("[IminPrinter] Warm-up state reset");
        }

        public static void Shutdown(Context? context)
        {
            if (!_isInitialized || context == null)
                return;

            try
            {
                PrinterHelper.Instance?.DeInitPrinterService(context);
                _isInitialized = false;
                lock (_lockObject)
                {
                    _isPrinterWarmedUp = false;
                    _lastWarmUpTime = DateTime.MinValue;
                }
                System.Console.WriteLine("[IminPrinter] Printer service shut down");
            }
            catch (Exception ex)
            {
                System.Console.WriteLine($"[IminPrinter] Failed to shutdown: {ex.Message}");
            }
        }

        public static int GetPrinterStatus()
        {
            try
            {
                return PrinterHelper.Instance?.PrinterStatus ?? -1;
            }
            catch (Exception ex)
            {
                System.Console.WriteLine($"[IminPrinter] Failed to get status: {ex.Message}");
                return -1;
            }
        }

        // Add this method to IminPrinterInitializer.cs
        public static int CheckPrinterHardwareStatus()
        {
            try
            {
                var printer = PrinterHelper.Instance;
                if (printer == null)
                    return -1;

                int status = printer.PrinterStatus;
                Console.WriteLine($"[IminPrinter] Hardware Status Check: {status}");

                // Status codes (may vary by model):
                // -1 –> The printer is not connected or powered on
                // 0 –> The printer is normal
                // 1 –> The printer is not connected or powered on
                // 3 –> Print head open
                // 7 –> No Paper Feed
                // 8 –> Paper Running Out
                // 99 –> Other errors

                return status;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[IminPrinter] Status check failed: {ex.Message}");
                return -1;
            }
        }

        // Callback for printer initialization
        private sealed class PrinterInitCallback : Java.Lang.Object, IInitPrinterCallback
        {
            public void OnConnected()
            {
                System.Console.WriteLine("[IminPrinter] Printer connected");

                // Reset warm-up state when printer reconnects
                lock (_lockObject)
                {
                    _isPrinterWarmedUp = false;
                    _lastWarmUpTime = DateTime.MinValue;
                }

                try
                {
                    var status = PrinterHelper.Instance?.PrinterStatus ?? -1;
                    System.Console.WriteLine($"[IminPrinter] Connection status: {status}");

                    // Trigger warm-up after connection
                    Task.Run(async () =>
                    {
                        await Task.Delay(800);
                        await PerformInitialWarmUp();
                    });
                }
                catch (Exception ex)
                {
                    System.Console.WriteLine($"[IminPrinter] Error on connection: {ex.Message}");
                }
            }

            public void OnDisconnected()
            {
                System.Console.WriteLine("[IminPrinter] Printer disconnected");
                lock (_lockObject)
                {
                    _isPrinterWarmedUp = false;
                    _lastWarmUpTime = DateTime.MinValue;
                }
            }
        }
    }
}