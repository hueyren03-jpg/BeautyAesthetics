using Beauty_Aesthetics_WebPos.Components.Services.Printing;

namespace Beauty_Aesthetics_Hybrid.Services;

public sealed class IminPrinterHelperService
{
    public Task<(bool Success, string Error)> PrintReceiptAsync(ReceiptData data) =>
        Task.FromResult((
            false,
            "iMin printer support requires the iMin printer SDK/JAR. BeautyAesthetics does not currently include that SDK."));
}
