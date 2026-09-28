using Beauty_Aesthetics_WebPos.Components.Services.Printing;

namespace Beauty_Aesthetics_Hybrid.Web.Services;

public sealed class WebPrinterSelectionService : IPrinterSelectionService
{
    private string selectedKey = "BROWSER";

    public IReadOnlyList<PrinterOption> GetPrinterOptions() =>
    [
        new()
        {
            Name = "System / Browser Printer",
            IsBrowserPrinter = true,
            ReceiptMM = 80,
            IsSelected = selectedKey == "BROWSER"
        }
    ];

    public PrinterOption? GetSelectedPrinter() =>
        GetPrinterOptions().FirstOrDefault(item => item.IsSelected);

    public void SelectPrinter(PrinterOption printer) =>
        selectedKey = printer.Key;

    public void UpdateNetworkPrinterIp(PrinterOption printer, string ipAddress)
    {
    }
}
