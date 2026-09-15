using SenangRetails.Shared.Services.BluetoothPrinterService;

namespace SenangRetails.Web.Services.BluetoothPrinterService
{
    /// <summary>Web stub — printer configuration is display-only on the browser.</summary>
    public class WebBluetoothPrinterService : IBluetoothPrinterService
    {
        private string _selectedKey = "";
        private string _net58Ip = "192.168.1.200";
        private string _net80Ip = "192.168.1.200";

        public List<PrinterOption> GetPrinterOptions() => new()
        {
            new() { Name = "Imin Printer (58mm/80mm)", IsIminPrinter     = true,  ReceiptMM = 100, IsSelected = _selectedKey == "Imin_100" },
            new() { Name = "Bluetooth Printer (58mm)", IsBluetoothPrinter = true,  ReceiptMM = 58,  IsSelected = _selectedKey == "BT_58"   },
            new() { Name = "Bluetooth Printer (80mm)", IsBluetoothPrinter = true,  ReceiptMM = 80,  IsSelected = _selectedKey == "BT_80"   },
            new() { Name = "Network Printer (58mm)",   IsWifiPrinter      = true,  ReceiptMM = 58,  IsSelected = _selectedKey == "Net_58",  IpAddress = _net58Ip },
            new() { Name = "Network Printer (80mm)",   IsWifiPrinter      = true,  ReceiptMM = 80,  IsSelected = _selectedKey == "Net_80",  IpAddress = _net80Ip },
        };

        public PrinterOption? GetSelectedPrinter() =>
            GetPrinterOptions().FirstOrDefault(p => p.IsSelected);

        public void SelectPrinter(PrinterOption printer) =>
            _selectedKey = printer.Key;

        public void UpdateNetworkPrinterIp(PrinterOption printer, string ipAddress)
        {
            if (printer.ReceiptMM == 58) _net58Ip = ipAddress;
            else _net80Ip = ipAddress;
        }
    }
}
