using Beauty_Aesthetics_WebPos.Components.Services.Printing;

namespace Beauty_Aesthetics_Hybrid.Web.Services;

public sealed class WebReceiptPrinterService : IReceiptPrinterService
{
    public Task<(bool Success, string Error)> PrintAsync(
        PrinterOption printer,
        ReceiptData data,
        CancellationToken cancellationToken = default) =>
        Task.FromResult((false, "Direct receipt printing is only available in the MAUI mobile app. Use Download PDF Receipt on web."));
}
