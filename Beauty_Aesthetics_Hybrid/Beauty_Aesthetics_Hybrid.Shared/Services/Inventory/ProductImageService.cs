using Beauty_Aesthetics_WebPos.Components.Services.Auth;
using Beauty_Aesthetics_WebPos.Components.ViewModels;

namespace Beauty_Aesthetics_WebPos.Components.Services.Inventory;

/// <summary>
/// Resolves product images the same way as Senang Apps: prefer a locally cached
/// image, otherwise build an absolute URL from the API image path and file name.
/// </summary>
public sealed class ProductImageService
{
    private readonly string apiBaseUrl;
    private readonly Dictionary<string, string> imageCache = new(StringComparer.OrdinalIgnoreCase);

    public ProductImageService(AuthApiOptions apiOptions)
    {
        apiBaseUrl = string.IsNullOrWhiteSpace(apiOptions.BaseUrl)
            ? AuthApiOptions.DefaultBaseUrl
            : apiOptions.BaseUrl.Trim();
    }

    public string ResolveImageUrl(InventoryViewModel.InventoryItem? item) => item is null
        ? string.Empty
        : ResolveImageUrl(item.MasterAccountId, item.ImagePath, item.ImageFileName);

    public string ResolveImageUrl(
        string? masterAccountId,
        string? imagePath,
        string? imageFileName)
    {
        var cached = GetImage(masterAccountId);
        return !string.IsNullOrWhiteSpace(cached)
            ? cached
            : ResolveServerImageUrl(imagePath, imageFileName);
    }

    public string ResolveServerImageUrl(string? imagePath, string? imageFileName)
    {
        var rawPath = imagePath?.Trim() ?? string.Empty;
        var rawFile = imageFileName?.Trim() ?? string.Empty;

        string rawUrl;
        if (rawPath.StartsWith("data:", StringComparison.OrdinalIgnoreCase) ||
            rawPath.StartsWith("blob:", StringComparison.OrdinalIgnoreCase))
        {
            rawUrl = rawPath;
        }
        else if (!string.IsNullOrEmpty(rawPath) && !string.IsNullOrEmpty(rawFile))
        {
            var normalizedPath = rawPath.Replace('\\', '/');
            rawUrl = normalizedPath.EndsWith($"/{rawFile}", StringComparison.OrdinalIgnoreCase) ||
                     string.Equals(normalizedPath, rawFile, StringComparison.OrdinalIgnoreCase)
                ? normalizedPath
                : $"{normalizedPath.TrimEnd('/')}/{rawFile.TrimStart('/', '\\')}";
        }
        else
        {
            rawUrl = !string.IsNullOrEmpty(rawPath) ? rawPath : rawFile;
        }

        if (string.IsNullOrWhiteSpace(rawUrl)) return string.Empty;

        rawUrl = rawUrl.Replace('\\', '/');
        if (rawUrl.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
            rawUrl.StartsWith("https://", StringComparison.OrdinalIgnoreCase) ||
            rawUrl.StartsWith("data:", StringComparison.OrdinalIgnoreCase) ||
            rawUrl.StartsWith("blob:", StringComparison.OrdinalIgnoreCase))
        {
            return rawUrl;
        }

        var baseUrl = apiBaseUrl.TrimEnd('/') + "/";
        var relativeUrl = rawUrl.TrimStart('~', '/');
        return $"{baseUrl}{relativeUrl}";
    }

    public void StoreImage(string? masterAccountId, string? imageDataUrl)
    {
        if (!string.IsNullOrWhiteSpace(masterAccountId) && !string.IsNullOrWhiteSpace(imageDataUrl))
        {
            imageCache[masterAccountId.Trim()] = imageDataUrl;
        }
    }

    public string? GetImage(string? masterAccountId)
    {
        if (string.IsNullOrWhiteSpace(masterAccountId)) return null;
        return imageCache.TryGetValue(masterAccountId.Trim(), out var image) ? image : null;
    }

    public void RemoveImage(string? masterAccountId)
    {
        if (!string.IsNullOrWhiteSpace(masterAccountId))
        {
            imageCache.Remove(masterAccountId.Trim());
        }
    }
}
