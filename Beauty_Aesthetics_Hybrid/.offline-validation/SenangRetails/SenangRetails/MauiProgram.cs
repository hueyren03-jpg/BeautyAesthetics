using System.Reflection;
using Microsoft.Extensions.Logging;
using QuestPDF.Infrastructure;
using SenangRetails.Services;
using SenangRetails.Services.BluetoothPrinterService;
using SenangRetails.Services.FileDownloadService;
using SenangRetails.Services.FilePickerService;
using SenangRetails.Shared.Services.FilePickerService;
using SenangRetails.Services.ReceiptPrinterService;
using SenangRetails.Services.TokenSecureStorage;
using SenangRetails.Shared.ApiClient;
using SenangRetails.Shared.Services;
using SenangRetails.Shared.Services.AccountService;
using SenangRetails.Shared.Services.AuthService;
using SenangRetails.Shared.Services.BluetoothPrinterService;
using SenangRetails.Shared.Services.BranchService;
using SenangRetails.Shared.Services.CashDiscountService;
using SenangRetails.Shared.Services.CashSalesService;
using SenangRetails.Shared.Services.CustomerService;
using SenangRetails.Shared.Services.DashboardService;
using SenangRetails.Shared.Services.EInvoiceService;
using SenangRetails.Shared.Services.EmailSettingService;
using SenangRetails.Shared.Services.EmployeeService;
using SenangRetails.Shared.Services.FileDownloadService;
using SenangRetails.Shared.Services.InventoryService;
using SenangRetails.Shared.Services.MembersCreditService;
using SenangRetails.Shared.Services.ARReceiptService;
using SenangRetails.Shared.Services.MembershipTypeService;
using SenangRetails.Shared.Services.MembersPackageService;
using SenangRetails.Shared.Services.NotificationService;
using SenangRetails.Shared.Services.PriceGroupService;
using SenangRetails.Shared.Services.PurchaseOrderService;
using SenangRetails.Shared.Services.CashDrawerService;
using SenangRetails.Shared.Services.BarcodeSetupService;
using SenangRetails.Shared.Services.CommissionSetupService;
using SenangRetails.Shared.Services.MaintainDocumentNumberService;
using SenangRetails.Shared.Services.PromotionSetupService;
using SenangRetails.Shared.Services.ThemeService;
using SenangRetails.Shared.Services.PaymentService;
using SenangRetails.Shared.Services.PointConversionService;
using SenangRetails.Shared.Services.ReceiptPrinterService;
using SenangRetails.Shared.Services.SupportingTableService;
using SenangRetails.Shared.Services.TaxRateService;
using SenangRetails.Shared.Services.UserControlService;
using SenangRetails.Shared.Services.WhatsappService;
using SenangRetails.Shared.Data;
using SenangRetails.Shared.Services.DataLayer.Offline;
using SenangRetails.Shared.Services.Sync;
using SenangRetails.Shared.Services.Connectivity;


namespace SenangRetails
{
    public static class MauiProgram
    {
        public static MauiApp CreateMauiApp()
        {
            var builder = MauiApp.CreateBuilder();
            builder
                .UseMauiApp<App>()
                .ConfigureFonts(fonts =>
                {
                    fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                });

            // ==========================================
            // 1. SET QUESTPDF SETTINGS FIRST
            // ==========================================
            QuestPDF.Settings.License = LicenseType.Community;
            QuestPDF.Settings.CheckIfAllTextGlyphsAreAvailable = false;

            // ==========================================
            // 2. REGISTER FONT WITH ERROR HANDLING
            // ==========================================
            try
            {
                // Method 1: Try to load from embedded resource
                var assembly = Assembly.GetExecutingAssembly();
                var resourceName = assembly.GetManifestResourceNames()
                    .FirstOrDefault(r => r.Contains("NotoSansSC") && r.EndsWith(".ttf"));

                if (resourceName != null)
                {
                    using var stream = assembly.GetManifestResourceStream(resourceName);
                    if (stream != null)
                    {
                        QuestPDF.Drawing.FontManager.RegisterFontWithCustomName("Noto Sans SC", stream);
                        Console.WriteLine($"✅ Font registered from embedded resource: {resourceName}");
                    }
                }
                else
                {
                    // Method 2: Try to load from file system
                    var fontPath = Path.Combine(FileSystem.AppDataDirectory, "..", "..", "Resources", "Fonts", "NotoSansSC-Regular.ttf");

                    if (!File.Exists(fontPath))
                    {
                        // Try alternative paths
                        var altPaths = new[]
                        {
                            Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Resources", "Fonts", "NotoSansSC-Regular.ttf"),
                            Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Fonts", "NotoSansSC-Regular.ttf"),
                            Path.Combine(FileSystem.AppDataDirectory, "Fonts", "NotoSansSC-Regular.ttf"),
                        };

                        foreach (var altPath in altPaths)
                        {
                            if (File.Exists(altPath))
                            {
                                fontPath = altPath;
                                break;
                            }
                        }
                    }

                    if (File.Exists(fontPath))
                    {
                        using var stream = File.OpenRead(fontPath);
                        QuestPDF.Drawing.FontManager.RegisterFontWithCustomName("Noto Sans SC", stream);
                        Console.WriteLine($"✅ Font registered from file: {fontPath}");
                    }
                    else
                    {
                        Console.WriteLine("⚠️ Font file not found. Using system fonts as fallback.");
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"⚠️ Font registration failed: {ex.Message}");
                Console.WriteLine("Using system fonts as fallback.");
                // App continues - font registration failure shouldn't crash the app
            }

            // Register AppState with platform detection
            builder.Services.AddSingleton<AppState>(sp =>
            {
                var appState = new AppState();

                // Detect platform using MAUI's DeviceInfo
                if (DeviceInfo.Current.Platform == DevicePlatform.Android)
                    appState.Platform = "Android";
                else if (DeviceInfo.Current.Platform == DevicePlatform.iOS)
                    appState.Platform = "iOS";
                else if (DeviceInfo.Current.Platform == DevicePlatform.MacCatalyst)
                    appState.Platform = "MacCatalyst";
                else if (DeviceInfo.Current.Platform == DevicePlatform.WinUI)
                    appState.Platform = "Windows";
                else
                    appState.Platform = "Unknown";

                return appState;
            });

            // Add device-specific services used by the SenangRetails.Shared project
            builder.Services.AddSingleton<IFormFactor, FormFactor>();
            builder.Services.AddBlazorBootstrap();
            builder.Services.AddScoped<EInvoiceSetupGuideService>();
            builder.Services.AddScoped(sp => new HttpClient
            {
                BaseAddress = new Uri("https://ebisoftware.com.my:5000") // Set base address
            });

            // Auth — AuthAC needs a pre-configured HttpClient to reach the API.
            builder.Services.AddHttpClient<AuthAC>(client =>
            {
                client.BaseAddress = new Uri("https://ebisoftware.com.my:5000/");
            });
            builder.Services.AddScoped<IAuthService, AuthService>();
            builder.Services.AddSingleton<IMauiTokenService, MauiTokenService>();
            builder.Services.AddScoped<IEInvoiceService, EInvoiceService>();

            // Adapter: exposes IMauiTokenService as IStoreTokenService for shared AC classes.
            // Singleton matches InventoryAC / SupportingTableAC lifetime.
            builder.Services.AddSingleton<IStoreTokenService, MauiStoreTokenService>();
            builder.Services.AddSingleton<INetworkStatusService, MauiNetworkStatusService>();

            // Inventory & SupportingTable API clients — Singleton so the HttpClient
            // and its TCP/TLS connection pool are reused across page navigations.
            builder.Services.AddSingleton<InventoryAC>();
            builder.Services.AddSingleton<SupportingTableAC>();

            builder.Services.AddScoped<SettingAC>();
            builder.Services.AddScoped<IEmailSettingService, EmailSettingService>();

            // Inventory & SupportingTable services — Singleton so ProductCacheService (Singleton) can depend on them.
            builder.Services.AddSingleton<IInventoryService, InventoryService>();
            builder.Services.AddSingleton<ISupportingTableService, SupportingTableService>();

            // In-memory cache: first visit fetches from API, every return visit loads instantly.
            builder.Services.AddSingleton<ProductCacheService>();
            builder.Services.AddSingleton<WorkspaceService>();

            // Local image cache: persists uploaded images to the app data directory so they
            // survive logout/login and app restarts. Registered as the base type so shared
            // components (Product.razor.cs) can inject LocalImageCacheService directly.
            builder.Services.AddSingleton<LocalImageCacheService, MauiLocalImageCacheService>();

            // Language service: persists chosen language via Preferences across restarts.
            builder.Services.AddSingleton<LanguageService, MauiLanguageService>();

            // Customer service and API client
            builder.Services.AddSingleton<CustomerAC>();
            builder.Services.AddSingleton<ICustomerService, CustomerService>();

            // Dashboard service and API client
            builder.Services.AddSingleton<DashboardAC>();
            builder.Services.AddSingleton<IDashboardService, DashboardService>();

            // Notification service (Singleton so the same instance is shared across all pages)
            builder.Services.AddSingleton<INotificationService, NotificationService>();

            // File download services
            builder.Services.AddSingleton<IFileDownloadService, MauiFileDownloadService>();

            // File Picker Services
            builder.Services.AddSingleton<IFilePickerService, MauiFilePickerService>();

            // Branch Services
            builder.Services.AddSingleton<BranchAC>();
            builder.Services.AddSingleton<IBranchService, BranchService>();

            builder.Services.AddSingleton<GSTTaxCodeAC>();
            builder.Services.AddSingleton<IGstTaxRateService, GstTaxRateService>();

            //Payment Services
            builder.Services.AddSingleton<POSPaymentLineTypeAC>();
            builder.Services.AddSingleton<IPaymentService, PaymentService>();

            // Cash Sales & Android Local SQLite Storage
            LocalAppDbContext.CustomDbPath = Path.Combine(FileSystem.AppDataDirectory, "senang_local_pos.db");
            builder.Services.AddDbContext<LocalAppDbContext>();
            builder.Services.AddSingleton<IOfflineCashSalesStorage, OfflineCashSalesStorage>();
            builder.Services.AddSingleton<IOrderSyncService, OrderSyncService>();
            builder.Services.AddSingleton<CashSalesAC>();
            builder.Services.AddSingleton<ICashSalesService, CashSalesService>();
            // Account Service
            builder.Services.AddHttpClient<AccountAC>(client =>
            {
                client.BaseAddress = new Uri("https://ebisoftware.com.my:5000/");
            });
            builder.Services.AddHttpClient<StaffAC>(client =>
            {
                client.BaseAddress = new Uri("https://ebisoftware.com.my:5000/");
            });
            builder.Services.AddScoped<StaffService>();

            builder.Services.AddScoped<IAccountService, AccountService>();
            // These services use IJSRuntime/localStorage and must be resolved inside
            // the active BlazorWebView scope on MAUI.
            builder.Services.AddScoped<IPriceGroupService, PriceGroupService>();
            builder.Services.AddScoped<IPurchaseOrderService, PurchaseOrderService>();
            builder.Services.AddScoped<ICashDrawerService, CashDrawerService>();
            builder.Services.AddScoped<IBarcodeSetupService, BarcodeSetupService>();
            builder.Services.AddScoped<ICommissionSetupService, CommissionSetupService>();
            builder.Services.AddScoped<IMaintainDocumentNumberService, MaintainDocumentNumberService>();
            builder.Services.AddScoped<IPromotionSetupService, PromotionSetupService>();
            builder.Services.AddScoped<IThemeService, ThemeService>();

            // Bluetooth Printer Service
            builder.Services.AddSingleton<IBluetoothPrinterService, MauiBluetoothPrinterService>();

            // Receipt Printer Service
            builder.Services.AddSingleton<IReceiptPrinterService, MauiReceiptPrinterService>();
            // Whatsapp Service
            builder.Services.AddSingleton<WhatsAppService>(sp =>
            {
                var js = sp.GetRequiredService<Microsoft.JSInterop.IJSRuntime>();
                var service = new WhatsAppService(js);
                service.NativeOpenUrlHandler = async (url) =>
                {
                    try
                    {
                        return await Microsoft.Maui.ApplicationModel.Launcher.Default.OpenAsync(url);
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"[MAUI] Native open URL failed: {ex.Message}");
                        return false;
                    }
                };
                return service;
            });


            // Report Service
            builder.Services.AddSingleton<IReportService, ReportService>();

            // Point Conversion Formula Service
            builder.Services.AddSingleton<PointConversionAC>();
            builder.Services.AddSingleton<IPointConversionService, PointConversionService>();
            builder.Services.AddSingleton<MembershipTypeAC>();
            builder.Services.AddSingleton<IMembershipTypeService, MembershipTypeService>();

            // User Control (permissions)
            builder.Services.AddSingleton<SecurityUserAC>();
            builder.Services.AddSingleton<SecurityUserService>();

            // Members Package Service
            builder.Services.AddSingleton<MembersPackageAC>();
            builder.Services.AddSingleton<IMembersPackageService, MembersPackageService>();

            // Members Credit Service
            builder.Services.AddSingleton<MembersCreditAC>();
            builder.Services.AddSingleton<IMembersCreditService, MembersCreditService>();

            // AR Receipt Service
            builder.Services.AddSingleton<ARReceiptAC>();
            builder.Services.AddSingleton<IARReceiptService, ARReceiptService>();

            // User Control Service
            builder.Services.AddSingleton<UserControlAC>();
            builder.Services.AddSingleton<IUserControlService, UserControlService>();

            // Tax Service
            builder.Services.AddSingleton<ITaxService, TaxService>();

            // Employee Service
            builder.Services.AddSingleton<EmployeeAC>();
            builder.Services.AddSingleton<IEmployeeService, EmployeeService>();

            // Cash Discount Service
            builder.Services.AddSingleton<CashDiscountAC>();
            builder.Services.AddSingleton<ICashDiscountService, CashDiscountService>();


            builder.Services.AddScoped(sp =>
            {
                var handler = new System.Net.Http.HttpClientHandler
                {
                    ServerCertificateCustomValidationCallback = (message, cert, chain, errors) => true
                };

                return new HttpClient(handler)
                {
                    BaseAddress = new Uri("https://ebisoftware.com.my:5000")
                };
            });

            builder.Services.AddMauiBlazorWebView();

#if DEBUG
            builder.Services.AddBlazorWebViewDeveloperTools();
            builder.Logging.AddDebug();
#endif

            var app = builder.Build();

            // Native connectivity listener: automatically sync when internet reconnects
            try
            {
                Connectivity.Current.ConnectivityChanged += (s, e) =>
                {
                    if (e.NetworkAccess == NetworkAccess.Internet)
                    {
                        Task.Run(async () =>
                        {
                            try
                            {
                                var syncService = app.Services.GetService<IOrderSyncService>();
                                if (syncService != null)
                                {
                                    await syncService.SyncPendingOrdersAsync();
                                }
                            }
                            catch (Exception ex)
                            {
                                Console.WriteLine($"[AutoSync] ConnectivityChanged trigger error: {ex.Message}");
                            }
                        });
                    }
                };
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[AutoSync] Could not register connectivity listener: {ex.Message}");
            }

            return app;
        }
    }
}
