using Microsoft.AspNetCore.Components.Server.ProtectedBrowserStorage;
using SenangRetails.Shared.Services.FilePickerService;
using SenangRetails.Shared.ApiClient;
using SenangRetails.Shared.Services;
using SenangRetails.Shared.Services.AccountService;
using SenangRetails.Shared.Services.AuthService;
using SenangRetails.Shared.Services.BluetoothPrinterService;
using SenangRetails.Shared.Services.BranchService;
using SenangRetails.Shared.Services.CashSalesService;
using SenangRetails.Shared.Services.EmailSettingService;
using SenangRetails.Shared.Services.CustomerService;
using SenangRetails.Shared.Services.DashboardService;
using SenangRetails.Shared.Services.EInvoiceService;
using SenangRetails.Shared.Services.FileDownloadService;
using SenangRetails.Shared.Services.InventoryService;
using SenangRetails.Shared.Services.MembersCreditService;
using SenangRetails.Shared.Services.ARReceiptService;
using SenangRetails.Shared.Services.MembershipTypeService;
using SenangRetails.Shared.Services.MembersPackageService;
using SenangRetails.Shared.Services.NotificationService;
using SenangRetails.Shared.Services.PaymentService;
using SenangRetails.Shared.Services.PriceGroupService;
using SenangRetails.Shared.Services.PurchaseOrderService;
using SenangRetails.Shared.Services.CashDrawerService;
using SenangRetails.Shared.Services.BarcodeSetupService;
using SenangRetails.Shared.Services.CommissionSetupService;
using SenangRetails.Shared.Services.MaintainDocumentNumberService;
using SenangRetails.Shared.Services.PromotionSetupService;
using SenangRetails.Shared.Services.ThemeService;
using SenangRetails.Shared.Services.PointConversionService;
using SenangRetails.Shared.Services.ReceiptPrinterService;
using SenangRetails.Shared.Services.SupportingTableService;
using SenangRetails.Shared.Services.TaxRateService;
using SenangRetails.Shared.Services.WhatsappService;
using SenangRetails.Shared.Data;
using SenangRetails.Shared.Services.DataLayer.Offline;
using SenangRetails.Shared.Services.Sync;
using SenangRetails.Web.Components;
using SenangRetails.Web.Services;
using SenangRetails.Web.Services.BluetoothPrinterService;
using SenangRetails.Web.Services.FileDownloadService;
using SenangRetails.Web.Services.FilePickerService;
using SenangRetails.Web.Services.ReceiptPrinterService;
using SenangRetails.Web.Services.TokenSessionStorage;
using SenangRetails.Shared.Services.UserControlService;
using SenangRetails.Shared.Services.EmployeeService;
using SenangRetails.Shared.Services.CashDiscountService;
using SenangRetails.Shared.Services.Connectivity;

var builder = WebApplication.CreateBuilder(args);

// Disable glyph checking for Chinese characters
QuestPDF.Settings.CheckIfAllTextGlyphsAreAvailable = false;

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents()
    .AddHubOptions(options =>
    {
        options.MaximumReceiveMessageSize = 10 * 1024 * 1024; // 10MB limit to accommodate larger local storage payloads
    });

// Add device-specific services used by the SenangRetails.Shared project
builder.Services.AddSingleton<IFormFactor, FormFactor>();
builder.Services.AddScoped<AppState>(); 
builder.Services.AddBlazorBootstrap();
builder.Services.AddScoped<EInvoiceSetupGuideService>();

//Auth DI
builder.Services.AddHttpClient<AuthAC>(client =>
{
    client.BaseAddress = new Uri("https://ebisoftware.com.my:5000/");
});

builder.Services.AddHttpClient<StaffAC>(client =>
{
    client.BaseAddress = new Uri("https://ebisoftware.com.my:5000/");
});

builder.Services.AddScoped(sp => new HttpClient
{
    BaseAddress = new Uri("https://ebisoftware.com.my:5000")
});

//Service DI
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<IPriceGroupService, PriceGroupService>();
builder.Services.AddScoped<IPurchaseOrderService, PurchaseOrderService>();
builder.Services.AddScoped<ICashDrawerService, CashDrawerService>();
builder.Services.AddScoped<IBarcodeSetupService, BarcodeSetupService>();
builder.Services.AddScoped<ICommissionSetupService, CommissionSetupService>();
builder.Services.AddScoped<IMaintainDocumentNumberService, MaintainDocumentNumberService>();
builder.Services.AddScoped<IPromotionSetupService, PromotionSetupService>();
builder.Services.AddScoped<IThemeService, ThemeService>();
builder.Services.AddScoped<ProtectedSessionStorage>();
builder.Services.AddScoped<IStoreTokenService, StoreTokenService>();
builder.Services.AddSingleton<INetworkStatusService, DefaultNetworkStatusService>();
builder.Services.AddScoped<IEInvoiceService, EInvoiceService>();
builder.Services.AddScoped<IMembershipTypeService, MembershipTypeService>();
builder.Services.AddScoped<StaffService>();
builder.Services.AddScoped<MembershipTypeAC>();
// User Control (permissions)
builder.Services.AddScoped<SecurityUserAC>();
builder.Services.AddScoped<SecurityUserService>();

// Email Settings
builder.Services.AddScoped<SettingAC>();
builder.Services.AddScoped<IEmailSettingService, EmailSettingService>();

// Inventory & SupportingTable API clients
builder.Services.AddScoped<InventoryAC>();
builder.Services.AddScoped<SupportingTableAC>();

// Inventory & SupportingTable services
builder.Services.AddScoped<IInventoryService, InventoryService>();
builder.Services.AddScoped<ISupportingTableService, SupportingTableService>();

// In-memory cache per user session (Scoped = per Blazor Server circuit).
builder.Services.AddScoped<ProductCacheService>();
builder.Services.AddScoped<WorkspaceService>();

// Local image cache per user session.
builder.Services.AddScoped<LocalImageCacheService>();

// Language service: in-memory per session (localStorage handled by components via JS interop).
builder.Services.AddScoped<LanguageService>();

// Customer services and API client
builder.Services.AddScoped<CustomerAC>();
builder.Services.AddScoped<ICustomerService, CustomerService>();

// Dashboard service and API client
builder.Services.AddScoped<DashboardAC>();
builder.Services.AddScoped<IDashboardService, DashboardService>();
// Notification service (Scoped so the same instance is shared across all components in a circuit)
builder.Services.AddScoped<INotificationService, NotificationService>();

// File Download Services
builder.Services.AddScoped<IFileDownloadService, WebFileDownloadService>();

// File Picker Services
builder.Services.AddScoped<IFilePickerService, WebFilePickerService>();

// Branch Services
builder.Services.AddScoped<BranchAC>();
builder.Services.AddScoped<IBranchService, BranchService>();

builder.Services.AddScoped<GSTTaxCodeAC>();
builder.Services.AddScoped<IGstTaxRateService, GstTaxRateService>();

//Payment Services
builder.Services.AddScoped<POSPaymentLineTypeAC>();
builder.Services.AddScoped<IPaymentService, PaymentService>();

// Cash Sales & Offline Storage
builder.Services.AddDbContext<LocalAppDbContext>();
builder.Services.AddScoped<IOfflineCashSalesStorage, OfflineCashSalesStorage>();
builder.Services.AddScoped<IOrderSyncService, OrderSyncService>();
builder.Services.AddScoped<CashSalesAC>();
builder.Services.AddScoped<ICashSalesService, CashSalesService>();
// Account Service
builder.Services.AddHttpClient<AccountAC>(client =>
{
    client.BaseAddress = new Uri("https://ebisoftware.com.my:5000/");
});
builder.Services.AddScoped<IAccountService, AccountService>();

// Bluetooth Printer Service (not supported on web)
builder.Services.AddScoped<IBluetoothPrinterService, WebBluetoothPrinterService>();

// Receipt Printer Service (not supported on web)
builder.Services.AddScoped<IReceiptPrinterService, WebReceiptPrinterService>();
// Whatsapp Service
builder.Services.AddScoped<WhatsAppService>();


// Report Service
builder.Services.AddScoped<IReportService, ReportService>();

// Point Conversion Formula Service
builder.Services.AddScoped<PointConversionAC>();
builder.Services.AddScoped<IPointConversionService, PointConversionService>();

// Members Package Service
builder.Services.AddScoped<MembersPackageAC>();
builder.Services.AddScoped<IMembersPackageService, MembersPackageService>();

// Members Credit Service
builder.Services.AddScoped<MembersCreditAC>();
builder.Services.AddScoped<IMembersCreditService, MembersCreditService>();

// AR Receipt Service
builder.Services.AddScoped<ARReceiptAC>();
builder.Services.AddScoped<IARReceiptService, ARReceiptService>();

// User Control Service
builder.Services.AddScoped<UserControlAC>();
builder.Services.AddScoped<IUserControlService, UserControlService>();

// Tax Service
builder.Services.AddScoped<ITaxService, TaxService>();

// Employee Service
builder.Services.AddScoped<EmployeeAC>();
builder.Services.AddScoped<IEmployeeService, EmployeeService>();

// Cash Discount Service
builder.Services.AddScoped<CashDiscountAC>();
builder.Services.AddScoped<ICashDiscountService, CashDiscountService>();


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

//http clientfor API calls
builder.Services.AddHttpClient("EbiApi", client =>
{
    client.BaseAddress = new Uri("https://ebisoftware.com.my:5000/");
});


var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}
app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
app.UseHttpsRedirection();

app.UseAntiforgery();

app.MapStaticAssets();

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode()
    .AddAdditionalAssemblies(
        typeof(SenangRetails.Shared._Imports).Assembly);

app.Run();
