using Beauty_Aesthetics_WebPos.Components.Services.Printing;
using Microsoft.JSInterop;

namespace Beauty_Aesthetics_Hybrid.Web.Services;

public sealed class WebReceiptPrinterService(IJSRuntime js) : IReceiptPrinterService
{
    public async Task<(bool Success, string Error)> PrintAsync(PrinterOption printer, ReceiptData data)
    {
        try
        {
            if (printer is not null && printer.IsBluetoothPrinter)
            {
                var columns = printer.ReceiptMM == 80m ? 48 : 32;
                var escPosBytes = EscPosReceiptBuilder.BuildEscPosReceipt(data, columns);
                var base64Data = Convert.ToBase64String(escPosBytes);
                var bluetoothResult = await js.InvokeAsync<WebPrintResult>(
                    "webPrinter.sendBluetoothEscPos",
                    base64Data);

                if (bluetoothResult is { Success: true })
                {
                    return (true, string.Empty);
                }

                Console.WriteLine(
                    $"[WebReceiptPrinterService] Bluetooth direct print notice: {bluetoothResult?.Error}. Falling back to browser print.");
            }

            var paperMm = printer?.ReceiptMM > 0m ? printer.ReceiptMM : 80m;
            var html = EscPosReceiptBuilder.BuildHtmlReceipt(data, paperMm);
            var htmlResult = await js.InvokeAsync<WebPrintResult>(
                "webPrinter.printHtmlReceipt",
                html,
                (int)paperMm);

            return htmlResult is { Success: true }
                ? (true, string.Empty)
                : (false, htmlResult?.Error ?? "Printing failed.");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[WebReceiptPrinterService] Error: {ex.Message}");
            return (false, ex.Message);
        }
    }

    private sealed class WebPrintResult
    {
        public bool Success { get; set; }
        public string? Error { get; set; }
    }
}
