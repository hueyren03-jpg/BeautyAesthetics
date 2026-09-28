namespace Beauty_Aesthetics_WebPos.Components.Services.Printing;

public interface IReceiptPrinterService
{
    Task<(bool Success, string Error)> PrintAsync(
        PrinterOption printer,
        ReceiptData data,
        CancellationToken cancellationToken = default);
}
