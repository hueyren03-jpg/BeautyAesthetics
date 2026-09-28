using Beauty_Aesthetics_WebPos.Components.Services.Files;
using Microsoft.JSInterop;

namespace Beauty_Aesthetics_Hybrid.Web.Services;

public sealed class WebFileDownloadService(IJSRuntime jsRuntime) : IFileDownloadService
{
    public async Task DownloadBinaryFileAsync(
        string fileName,
        string base64Content,
        string mimeType,
        CancellationToken cancellationToken = default)
    {
        var normalized = NormalizeBase64(base64Content);
        var dataUrl = $"data:{mimeType};base64,{normalized}";

        var downloaded = await jsRuntime.InvokeAsync<bool>(
            "downloadDocumentFile",
            cancellationToken,
            fileName,
            dataUrl);

        if (!downloaded)
        {
            throw new InvalidOperationException("The browser could not download the file.");
        }
    }

    private static string NormalizeBase64(string value)
    {
        var trimmed = value.Trim();
        var commaIndex = trimmed.IndexOf(',');
        return trimmed.StartsWith("data:", StringComparison.OrdinalIgnoreCase) && commaIndex >= 0
            ? trimmed[(commaIndex + 1)..]
            : trimmed;
    }
}
