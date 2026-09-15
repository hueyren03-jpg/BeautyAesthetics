using SenangRetails.Shared.Services.BluetoothPrinterService;

namespace SenangRetails.Services.BluetoothPrinterService
{
    public class MauiBluetoothPrinterService : IBluetoothPrinterService
    {
        private const string PrefSelectedKey  = "PrinterSelectedKey";
        private const string PrefNet58Ip      = "PrinterNet58Ip";
        private const string PrefNet80Ip      = "PrinterNet80Ip";

        public List<PrinterOption> GetPrinterOptions()
        {
            var selectedKey = Preferences.Get(PrefSelectedKey, "");
            var net58Ip     = Preferences.Get(PrefNet58Ip, "192.168.1.200");
            var net80Ip     = Preferences.Get(PrefNet80Ip, "192.168.1.200");

            return new List<PrinterOption>
            {
                new() { Name = "Imin Printer (58mm/80mm)", IsIminPrinter     = true,  ReceiptMM = 100, IsSelected = selectedKey == "Imin_100" },
                new() { Name = "Bluetooth Printer (58mm)", IsBluetoothPrinter = true,  ReceiptMM = 58,  IsSelected = selectedKey == "BT_58"   },
                new() { Name = "Bluetooth Printer (80mm)", IsBluetoothPrinter = true,  ReceiptMM = 80,  IsSelected = selectedKey == "BT_80"   },
                new() { Name = "Network Printer (58mm)",   IsWifiPrinter      = true,  ReceiptMM = 58,  IsSelected = selectedKey == "Net_58",  IpAddress = net58Ip },
                new() { Name = "Network Printer (80mm)",   IsWifiPrinter      = true,  ReceiptMM = 80,  IsSelected = selectedKey == "Net_80",  IpAddress = net80Ip },
            };
        }

        public PrinterOption? GetSelectedPrinter() =>
            GetPrinterOptions().FirstOrDefault(p => p.IsSelected);

        public void SelectPrinter(PrinterOption printer) =>
            Preferences.Set(PrefSelectedKey, printer.Key);

        public void UpdateNetworkPrinterIp(PrinterOption printer, string ipAddress)
        {
            if (printer.ReceiptMM == 58)
                Preferences.Set(PrefNet58Ip, ipAddress);
            else
                Preferences.Set(PrefNet80Ip, ipAddress);
        }
    }
}
