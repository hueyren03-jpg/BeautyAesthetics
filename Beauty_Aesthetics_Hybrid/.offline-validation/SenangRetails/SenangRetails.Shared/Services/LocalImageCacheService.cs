namespace SenangRetails.Shared.Services
{
    /// <summary>
    /// In-memory store mapping MasterAccountID → base64 image data URI.
    /// Persists for the lifetime of the process.
    /// MAUI override (MauiLocalImageCacheService) additionally persists images to disk.
    /// </summary>
    public class LocalImageCacheService
    {
        private readonly Dictionary<string, string> _images = new();

        public virtual void StoreImage(string masterAccountId, string base64DataUri)
        {
            if (!string.IsNullOrEmpty(masterAccountId) && !string.IsNullOrEmpty(base64DataUri))
                _images[masterAccountId] = base64DataUri;
        }

        public virtual string? GetImage(string masterAccountId)
        {
            if (string.IsNullOrEmpty(masterAccountId)) return null;
            return _images.TryGetValue(masterAccountId, out var v) ? v : null;
        }

        public virtual void RemoveImage(string masterAccountId)
        {
            if (!string.IsNullOrEmpty(masterAccountId))
                _images.Remove(masterAccountId);
        }
    }
}
