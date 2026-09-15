using SenangRetails.Shared.Services;

namespace SenangRetails.Services
{
    /// <summary>
    /// MAUI-specific image cache that persists uploaded images to the app data directory.
    /// On startup, all previously cached images are loaded back into memory so they survive
    /// app restarts (and logout/login cycles that restart the process).
    /// </summary>
    public class MauiLocalImageCacheService : LocalImageCacheService
    {
        private readonly string _cacheDir;

        public MauiLocalImageCacheService()
        {
            _cacheDir = Path.Combine(FileSystem.AppDataDirectory, "product_images");
            try { Directory.CreateDirectory(_cacheDir); } catch { }
            LoadAllFromDisk();
        }

        /// <summary>Pre-load all persisted images into memory on startup.</summary>
        private void LoadAllFromDisk()
        {
            try
            {
                foreach (var file in Directory.GetFiles(_cacheDir, "*.imgcache"))
                {
                    var key = Path.GetFileNameWithoutExtension(file);
                    var data = File.ReadAllText(file);
                    if (!string.IsNullOrEmpty(data))
                        base.StoreImage(key, data);
                }
            }
            catch { /* non-critical startup error — continue without persisted images */ }
        }

        public override void StoreImage(string masterAccountId, string base64DataUri)
        {
            base.StoreImage(masterAccountId, base64DataUri);
            // Write synchronously so the file is guaranteed on disk before any logout/reload.
            try
            {
                var file = Path.Combine(_cacheDir, masterAccountId + ".imgcache");
                File.WriteAllText(file, base64DataUri);
            }
            catch { }
        }

        public override string? GetImage(string masterAccountId)
        {
            // Check in-memory first (fast path).
            var cached = base.GetImage(masterAccountId);
            if (cached != null) return cached;

            // Fall back to disk in case the in-memory cache is empty
            // (e.g. after a service restart or forceLoad navigation).
            try
            {
                var file = Path.Combine(_cacheDir, masterAccountId + ".imgcache");
                if (File.Exists(file))
                {
                    var data = File.ReadAllText(file);
                    if (!string.IsNullOrEmpty(data))
                    {
                        base.StoreImage(masterAccountId, data); // warm the memory cache
                        return data;
                    }
                }
            }
            catch { }
            return null;
        }

        public override void RemoveImage(string masterAccountId)
        {
            base.RemoveImage(masterAccountId);
            try
            {
                var file = Path.Combine(_cacheDir, masterAccountId + ".imgcache");
                if (File.Exists(file)) File.Delete(file);
            }
            catch { }
        }
    }
}
