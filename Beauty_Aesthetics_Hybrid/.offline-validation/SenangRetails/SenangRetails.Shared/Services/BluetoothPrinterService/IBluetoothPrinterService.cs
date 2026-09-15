namespace SenangRetails.Shared.Services.BluetoothPrinterService
{
    public interface IBluetoothPrinterService
    {
        /// <summary>Returns the fixed list of 5 printer options with current selection state.</summary>
        List<PrinterOption> GetPrinterOptions();

        /// <summary>Returns the currently selected printer, or null if none selected.</summary>
        PrinterOption? GetSelectedPrinter();

        /// <summary>Marks the given printer as selected and persists the choice.</summary>
        void SelectPrinter(PrinterOption printer);

        /// <summary>Updates the IP address for a network printer and persists it.</summary>
        void UpdateNetworkPrinterIp(PrinterOption printer, string ipAddress);
    }
}
