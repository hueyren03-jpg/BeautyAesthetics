using Beauty_Aesthetics_WebPos.Components.Services.Printing;

namespace Beauty_Aesthetics_Hybrid.Web.Services;

public sealed class WebPrinterSelectionService : IPrinterSelectionService
{
    private string selectedKey = string.Empty;
    private string network58Ip = "192.168.1.200";
    private string network80Ip = "192.168.1.200";

    public IReadOnlyList<PrinterOption> GetPrinterOptions() =>
    [
        new() { Name = "Bluetooth Printer (58mm)", IsBluetoothPrinter = true, ReceiptMM = 58, IsSelected = selectedKey == "BT_58" },
        new() { Name = "Bluetooth Printer (80mm)", IsBluetoothPrinter = true, ReceiptMM = 80, IsSelected = selectedKey == "BT_80" },
        new() { Name = "Network Printer (58mm)", IsNetworkPrinter = true, ReceiptMM = 58, IpAddress = network58Ip, IsSelected = selectedKey == "NET_58" },
        new() { Name = "Network Printer (80mm)", IsNetworkPrinter = true, ReceiptMM = 80, IpAddress = network80Ip, IsSelected = selectedKey == "NET_80" }
    ];

    public PrinterOption? GetSelectedPrinter() =>
        GetPrinterOptions().FirstOrDefault(item => item.IsSelected);

    public void SelectPrinter(PrinterOption printer) =>
        selectedKey = printer.Key;

    public void UpdateNetworkPrinterIp(PrinterOption printer, string ipAddress)
    {
        if (printer.ReceiptMM == 80)
        {
            network80Ip = ipAddress;
        }
        else
        {
            network58Ip = ipAddress;
        }
    }
}
