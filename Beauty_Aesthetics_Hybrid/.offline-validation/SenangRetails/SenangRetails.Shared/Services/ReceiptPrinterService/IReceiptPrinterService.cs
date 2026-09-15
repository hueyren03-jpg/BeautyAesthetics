using SenangRetails.Shared.Services.BluetoothPrinterService;

namespace SenangRetails.Shared.Services.ReceiptPrinterService
{
    public interface IReceiptPrinterService
    {
        Task<(bool Success, string Error)> PrintAsync(PrinterOption printer, ReceiptData data);
    }
}
