namespace SenangRetails.Shared.Services.BluetoothPrinterService
{
    public class PrinterOption
    {
        public string Name { get; set; } = "";
        public bool IsIminPrinter { get; set; }
        public bool IsBluetoothPrinter { get; set; }
        public bool IsWifiPrinter { get; set; }
        public decimal ReceiptMM { get; set; }
        public string IpAddress { get; set; } = "192.168.1.200";
        public bool IsSelected { get; set; }

        // Unique key used for Preferences storage
        public string Key =>
            IsIminPrinter ? "Imin_100" :
            IsBluetoothPrinter ? $"BT_{ReceiptMM}" :
            $"Net_{ReceiptMM}";
    }
}
