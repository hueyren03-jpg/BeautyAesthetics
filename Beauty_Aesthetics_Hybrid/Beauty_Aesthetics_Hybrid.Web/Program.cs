using Beauty_Aesthetics_WebPos.Components.Services.Feedback;
using Beauty_Aesthetics_Hybrid.Shared.Services;
using Beauty_Aesthetics_Hybrid.Web.Components;
using Beauty_Aesthetics_Hybrid.Web.Services;
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

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

// Configure SignalR for larger message sizes (needed for base64 images)
builder.Services.AddSignalR(options =>
{
    options.MaximumReceiveMessageSize = 10 * 1024 * 1024; // 10 MB
    options.StreamBufferCapacity = 15;
    options.ClientTimeoutInterval = TimeSpan.FromSeconds(60);
    options.HandshakeTimeout = TimeSpan.FromSeconds(30);
});

// Add device-specific services used by the Beauty_Aesthetics_Hybrid.Shared project
builder.Services.AddSingleton<IFormFactor, FormFactor>();
builder.Services.AddSingleton<IPathProvider, PathProvider>();
builder.Services.AddScoped<ICultureSettings, CultureSettings>();
builder.Services.AddScoped<Microsoft.AspNetCore.Components.Server.ProtectedBrowserStorage.ProtectedLocalStorage>();
builder.Services.AddScoped<ITokenStore, WebProtectedTokenStore>();
builder.Services.AddScoped<IBranchSessionStore, WebBranchSessionStore>();
builder.Services.AddScoped<IBranchSessionService, BranchSessionService>();
builder.Services.AddScoped<AppState>();
builder.Services.AddScoped<AppFeedbackService>();
builder.Services.AddSingleton(new AuthApiOptions
{
    BaseUrl = builder.Configuration["Api:BaseUrl"] ?? AuthApiOptions.DefaultBaseUrl
});
// The API transport is shared for the application lifetime. Scoped feature services may
// finish work during navigation, so they must not inherit a client disposed with a UI scope.
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


// Ported Pos registrations
builder.Services.AddLocalization(options => options.ResourcesPath = "Resources");
builder.Services.AddHttpContextAccessor();

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

var app = builder.Build();

var supportedCultureNames = new[] { "en", "zh-Hans", "ms" };
var supportedCultures = supportedCultureNames.Select(c => new System.Globalization.CultureInfo(c)).ToList();

app.UseRequestLocalization(new RequestLocalizationOptions
{
    DefaultRequestCulture = new Microsoft.AspNetCore.Localization.RequestCulture("en"),
    SupportedCultures = supportedCultures,
    SupportedUICultures = supportedCultures
});

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}
app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
app.UseHttpsRedirection();

app.MapGet("/culture/set", (HttpContext http, string culture, string? redirectUri) =>
{
    if (!supportedCultureNames.Contains(culture, StringComparer.OrdinalIgnoreCase))
    {
        culture = "en";
    }

    var requestCulture = new Microsoft.AspNetCore.Localization.RequestCulture(culture);
    http.Response.Cookies.Append(
        Microsoft.AspNetCore.Localization.CookieRequestCultureProvider.DefaultCookieName,
        Microsoft.AspNetCore.Localization.CookieRequestCultureProvider.MakeCookieValue(requestCulture),
        new CookieOptions { Expires = DateTimeOffset.UtcNow.AddYears(1), IsEssential = true });

    var target = string.IsNullOrWhiteSpace(redirectUri) ? "/" : redirectUri;
    return Results.Redirect(target);
});

app.UseAntiforgery();

app.MapStaticAssets();

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode()
    .AddAdditionalAssemblies(
        typeof(Beauty_Aesthetics_WebPos.Components._Imports).Assembly);

app.Run();
