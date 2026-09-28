using Beauty_Aesthetics_WebPos.Components.Services.Printing;
using Microsoft.Maui.Storage;

namespace Beauty_Aesthetics_Hybrid.Services;

public sealed class MauiPrinterSelectionService : IPrinterSelectionService
{
    private const string SelectedPrinterKey = "ReceiptPrinterSelectedKey";
    private const string Network58IpKey = "ReceiptPrinterNetwork58Ip";
    private const string Network80IpKey = "ReceiptPrinterNetwork80Ip";

    public IReadOnlyList<PrinterOption> GetPrinterOptions()
    {
        var selectedKey = Preferences.Get(SelectedPrinterKey, string.Empty);
        var network58Ip = Preferences.Get(Network58IpKey, "192.168.1.200");
        var network80Ip = Preferences.Get(Network80IpKey, "192.168.1.200");

        return
        [
            new() { Name = "Bluetooth Printer (58mm)", IsBluetoothPrinter = true, ReceiptMM = 58, IsSelected = selectedKey == "BT_58" },
            new() { Name = "Bluetooth Printer (80mm)", IsBluetoothPrinter = true, ReceiptMM = 80, IsSelected = selectedKey == "BT_80" },
            new() { Name = "Network Printer (58mm)", IsNetworkPrinter = true, ReceiptMM = 58, IpAddress = network58Ip, IsSelected = selectedKey == "NET_58" },
            new() { Name = "Network Printer (80mm)", IsNetworkPrinter = true, ReceiptMM = 80, IpAddress = network80Ip, IsSelected = selectedKey == "NET_80" }
        ];
    }

    public PrinterOption? GetSelectedPrinter() =>
        GetPrinterOptions().FirstOrDefault(item => item.IsSelected);

    public void SelectPrinter(PrinterOption printer) =>
        Preferences.Set(SelectedPrinterKey, printer.Key);

    public void UpdateNetworkPrinterIp(PrinterOption printer, string ipAddress)
    {
        Preferences.Set(
            printer.ReceiptMM == 80 ? Network80IpKey : Network58IpKey,
            ipAddress?.Trim() ?? string.Empty);
    }
}
