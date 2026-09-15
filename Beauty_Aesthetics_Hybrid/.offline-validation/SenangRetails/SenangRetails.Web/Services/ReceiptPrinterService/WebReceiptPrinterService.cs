using SenangRetails.Shared.Services.BluetoothPrinterService;
using SenangRetails.Shared.Services.ReceiptPrinterService;

namespace SenangRetails.Web.Services.ReceiptPrinterService
{
    public class WebReceiptPrinterService : IReceiptPrinterService
    {
        public Task<(bool Success, string Error)> PrintAsync(PrinterOption printer, ReceiptData data) =>
            Task.FromResult((false, "Receipt printing is only available in the mobile app."));
    }
}
