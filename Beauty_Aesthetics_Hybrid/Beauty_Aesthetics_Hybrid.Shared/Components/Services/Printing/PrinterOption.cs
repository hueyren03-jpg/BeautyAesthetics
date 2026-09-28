namespace Beauty_Aesthetics_WebPos.Components.Services.Printing;

public sealed class PrinterOption
{
    public string Name { get; set; } = string.Empty;
    public bool IsBluetoothPrinter { get; set; }
    public bool IsNetworkPrinter { get; set; }
    public bool IsBrowserPrinter { get; set; }
    public decimal ReceiptMM { get; set; } = 58;
    public string IpAddress { get; set; } = "192.168.1.200";
    public bool IsSelected { get; set; }

    public string Key =>
        IsBrowserPrinter ? "BROWSER" :
        IsBluetoothPrinter ? $"BT_{ReceiptMM:0}" :
        IsNetworkPrinter ? $"NET_{ReceiptMM:0}" :
        $"PRINTER_{ReceiptMM:0}";
}
