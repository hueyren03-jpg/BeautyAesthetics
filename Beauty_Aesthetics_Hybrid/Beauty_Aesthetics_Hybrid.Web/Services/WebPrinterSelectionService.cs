using Beauty_Aesthetics_WebPos.Components.Services.Printing;

namespace Beauty_Aesthetics_Hybrid.Web.Services;

public sealed class WebPrinterSelectionService : IBluetoothPrinterService, IPrinterSelectionService
{
    private string selectedKey = string.Empty;
    private string net58Ip = "192.168.1.200";
    private string net80Ip = "192.168.1.200";

    public List<PrinterOption> GetPrinterOptions() => new List<PrinterOption>
    {
        new() { Name = "iMin Printer (58mm/80mm)", IsIminPrinter = true, ReceiptMM = 100, IsSelected = selectedKey == "Imin_100" },
        new() { Name = "Bluetooth Printer (58mm)", IsBluetoothPrinter = true, ReceiptMM = 58, IsSelected = selectedKey == "BT_58" },
        new() { Name = "Bluetooth Printer (80mm)", IsBluetoothPrinter = true, ReceiptMM = 80, IsSelected = selectedKey == "BT_80" },
        new() { Name = "Network Printer (58mm)", IsWifiPrinter = true, ReceiptMM = 58, IsSelected = selectedKey == "Net_58", IpAddress = net58Ip },
        new() { Name = "Network Printer (80mm)", IsWifiPrinter = true, ReceiptMM = 80, IsSelected = selectedKey == "Net_80", IpAddress = net80Ip }
    };

    public PrinterOption? GetSelectedPrinter() =>
        GetPrinterOptions().FirstOrDefault(p => p.IsSelected);

    public void SelectPrinter(PrinterOption printer) =>
        selectedKey = printer.Key;

    public void UpdateNetworkPrinterIp(PrinterOption printer, string ipAddress)
    {
        if (printer.ReceiptMM == 58)
            net58Ip = ipAddress;
        else
            net80Ip = ipAddress;
    }
}
