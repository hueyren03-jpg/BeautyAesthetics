namespace Beauty_Aesthetics_WebPos.Components.Services.Printing;

public interface IPrinterSelectionService
{
    IReadOnlyList<PrinterOption> GetPrinterOptions();
    PrinterOption? GetSelectedPrinter();
    void SelectPrinter(PrinterOption printer);
    void UpdateNetworkPrinterIp(PrinterOption printer, string ipAddress);
}
