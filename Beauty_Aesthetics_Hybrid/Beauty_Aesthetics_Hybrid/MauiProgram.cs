using Beauty_Aesthetics_WebPos.Components.Services.Feedback;
using Beauty_Aesthetics_Hybrid.Services;
using Beauty_Aesthetics_Hybrid.Shared.Services;
using Microsoft.Extensions.Logging;
using Beauty_Aesthetics_WebPos.APIClient;
using Beauty_Aesthetics_WebPos.Components.Services;
using Beauty_Aesthetics_WebPos.Components.Services.Auth;
using Beauty_Aesthetics_WebPos.Components.Services.Customers;
using Beauty_Aesthetics_WebPos.Components.Services.Dashboard;
using Beauty_Aesthetics_WebPos.Components.Services.Employees;
using Beauty_Aesthetics_WebPos.Components.Services.Inventory;
using Beauty_Aesthetics_WebPos.Components.Services.Branches;
using Beauty_Aesthetics_WebPos.Components.Services.MembershipTypes;
using Beauty_Aesthetics_WebPos.Components.Services.PointConversions;
using Beauty_Aesthetics_WebPos.Components.Services.Sales;
using Beauty_Aesthetics_WebPos.Components.ViewModels;
using Beauty_Aesthetics_WebPos.Components.Pages;
using Beauty_Aesthetics_WebPos.Components.Pages.Voucher;
using Beauty_Aesthetics_WebPos.Data;

using Microsoft.Maui.Storage;
using System.Globalization;

namespace Beauty_Aesthetics_Hybrid
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

            // Add device-specific services used by the Beauty_Aesthetics_Hybrid.Shared project
            builder.Services.AddSingleton<IFormFactor, FormFactor>();
            builder.Services.AddSingleton<IPathProvider, PathProvider>();
            builder.Services.AddScoped<ICultureSettings, CultureSettings>();
		builder.Services.AddScoped<ITokenStore, MauiSecureTokenStore>();
		builder.Services.AddScoped<IBranchSessionStore, MauiBranchSessionStore>();
		builder.Services.AddScoped<IBranchSessionService, BranchSessionService>();
            builder.Services.AddScoped<AppState>();
            builder.Services.AddScoped<AppFeedbackService>();
            builder.Services.AddSingleton(new AuthApiOptions
            {
                BaseUrl = Environment.GetEnvironmentVariable("BEAUTY_AESTHETICS_API_BASE_URL") ?? AuthApiOptions.DefaultBaseUrl
            });
            // Keep the API transport alive for the MAUI application lifetime. Blazor page
            // scopes can be replaced while an awaited API operation is still completing.
            builder.Services.AddSingleton(sp => new HttpClient
            {
                BaseAddress = new Uri(sp.GetRequiredService<AuthApiOptions>().BaseUrl),
                Timeout = TimeSpan.FromSeconds(120)
            });
            builder.Services.AddScoped<AuthAC>();
            builder.Services.AddScoped<CashSalesAC>();
            builder.Services.AddScoped<ICashSalesService, CashSalesService>();
            builder.Services.AddSingleton<IPendingOrderService, PendingOrderService>();
            builder.Services.AddScoped<AuthService>();
            builder.Services.AddScoped<IAuthService>(sp => sp.GetRequiredService<AuthService>());
            builder.Services.AddScoped<CustomerAC>();
            builder.Services.AddScoped<CustomerRatingAC>();
            builder.Services.AddScoped<CustomerService>();
            builder.Services.AddScoped<ICustomerService>(sp => sp.GetRequiredService<CustomerService>());
            builder.Services.AddScoped<WebDashboardAC>();
            builder.Services.AddScoped<IDashboardService, DashboardService>();
            builder.Services.AddScoped<EmployeeAC>();
            builder.Services.AddScoped<FileUploadAC>();
            builder.Services.AddScoped<EmployeeService>();
            builder.Services.AddScoped<IEmployeeService>(sp => sp.GetRequiredService<EmployeeService>());
            builder.Services.AddScoped<InventoryAC>();
            builder.Services.AddScoped<ServiceInventoryAC>();
            builder.Services.AddScoped<SupportingTableAC>();
            builder.Services.AddScoped<StockGrnAC>();
            builder.Services.AddScoped<StockGinAC>();
            builder.Services.AddScoped<StockTransferAC>();
            builder.Services.AddScoped<InventoryPendingAcceptAC>();
            builder.Services.AddScoped<BranchAC>();
            builder.Services.AddScoped<IProductInventoryService, ProductInventoryService>();
builder.Services.AddScoped<IProductSetupAssignmentService, ProductSetupAssignmentService>();
            builder.Services.AddSingleton<ProductImageService>();
            builder.Services.AddScoped<IInventoryOptionService, InventoryOptionService>();
            builder.Services.AddScoped<IStockGrnService, StockGrnService>();
            builder.Services.AddScoped<IStockGinService, StockGinService>();
            builder.Services.AddScoped<IStockTransferService, StockTransferService>();
            builder.Services.AddScoped<IInventoryPendingAcceptService, InventoryPendingAcceptService>();
            builder.Services.AddScoped<IBranchLookupService, BranchLookupService>();
            
            builder.Services.AddScoped<IServiceItemService, ServiceItemService>();
            builder.Services.AddScoped<IMemberCreditService, MemberCreditService>();
            builder.Services.AddScoped<IPackageService, PackageService>();
            builder.Services.AddScoped<MembershipTypeAC>();
            builder.Services.AddScoped<IMembershipTypeService, MembershipTypeService>();
            builder.Services.AddScoped<PointConversionAC>();
            builder.Services.AddScoped<IPointConversionService, PointConversionService>();
builder.Services.AddScoped<AppointmentAC>();
builder.Services.AddScoped<AppointmentService>();


            // Load persisted culture and apply to thread on startup
            var savedCulture = Preferences.Get("selected_culture", "en");
            try
            {
                var cultureInfo = new CultureInfo(savedCulture);
                CultureInfo.DefaultThreadCurrentCulture = cultureInfo;
                CultureInfo.DefaultThreadCurrentUICulture = cultureInfo;
            }
            catch {}

            builder.Services.AddMauiBlazorWebView();

            // Ported Pos registrations
            builder.Services.AddLocalization(options => options.ResourcesPath = "Resources");

            builder.Services.AddScoped<IRatingService, RatingService>();

            
            builder.Services.AddSingleton<MockDataService>();
            builder.Services.AddSingleton<ImageUploadService>();
            builder.Services.AddSingleton<RosterService>();
            builder.Services.AddScoped<SalesService>();

            builder.Services.AddTransient<VoucherPage>();
            builder.Services.AddTransient<VoucherPageViewModel>();
            builder.Services.AddTransient<VoucherDetails>();
            builder.Services.AddTransient<VoucherDetailsViewModel>();

            builder.Services.AddScoped<DocumentTemplateViewModel>();
            builder.Services.AddScoped<CaseNotePageViewModel>();
            builder.Services.AddScoped<FollowUpPageViewModel>();

#if DEBUG
            builder.Services.AddBlazorWebViewDeveloperTools();
            builder.Logging.AddDebug();
#endif

            return builder.Build();
        }
    }
}
