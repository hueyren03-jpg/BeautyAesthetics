using Beauty_Aesthetics_WebPos.Components.Services.Printing;

namespace Beauty_Aesthetics_Hybrid.Web.Services;

public sealed class WebReceiptPrinterService : IReceiptPrinterService
{
    public Task<(bool Success, string Error)> PrintAsync(PrinterOption printer, ReceiptData data) =>
        Task.FromResult((false, "Receipt printing is only available in the mobile app."));
}
