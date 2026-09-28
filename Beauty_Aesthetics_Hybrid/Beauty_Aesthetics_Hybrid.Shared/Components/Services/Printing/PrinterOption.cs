namespace Beauty_Aesthetics_WebPos.Components.Services.Printing;

public sealed class PrinterOption
{
    public string Name { get; set; } = string.Empty;
    public bool IsIminPrinter { get; set; }
    public bool IsBluetoothPrinter { get; set; }
    public bool IsWifiPrinter { get; set; }
    public decimal ReceiptMM { get; set; }
    public string IpAddress { get; set; } = "192.168.1.200";
    public bool IsSelected { get; set; }

    public string Key =>
        IsIminPrinter ? "Imin_100" :
        IsBluetoothPrinter ? $"BT_{ReceiptMM:0}" :
        $"Net_{ReceiptMM:0}";
}
