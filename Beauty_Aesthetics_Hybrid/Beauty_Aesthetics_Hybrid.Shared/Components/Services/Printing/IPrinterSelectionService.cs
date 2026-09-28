namespace Beauty_Aesthetics_WebPos.Components.Services.Printing;

public interface IBluetoothPrinterService
{
    List<PrinterOption> GetPrinterOptions();
    PrinterOption? GetSelectedPrinter();
    void SelectPrinter(PrinterOption printer);
    void UpdateNetworkPrinterIp(PrinterOption printer, string ipAddress);
}

// Compatibility alias for older Beauty code. New printing workflow uses IBluetoothPrinterService.
public interface IPrinterSelectionService : IBluetoothPrinterService
{
}
