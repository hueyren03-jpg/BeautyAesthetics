using Microsoft.JSInterop;
using Microsoft.Maui.Storage;
using System;
using System.IO;

namespace Beauty_Aesthetics_Hybrid.Services
{
    public static class ImageResolver
    {
        [JSInvokable("ResolveImage")]
        public static string ResolveImage(string path)
        {
            if (string.IsNullOrEmpty(path))
                return string.Empty;

            // Normalize path separators
            var normalizedPath = path.Replace('\\', '/');

            // Default fallback image
            if (normalizedPath == "/images/customers/default.png" || normalizedPath == "images/customers/default.png")
            {
                return "_content/Beauty_Aesthetics_Hybrid.Shared/images/customers/default.png";
            }

            if (normalizedPath.StartsWith("/images/") || normalizedPath.StartsWith("images/"))
            {
                try
                {
                    var cleanPath = normalizedPath.TrimStart('/');
                    // Target directory: FileSystem.AppDataDirectory/wwwroot/images/...
                    var root = Path.Combine(FileSystem.AppDataDirectory, "wwwroot");
                    var fullPath = Path.Combine(root, cleanPath.Replace('/', Path.DirectorySeparatorChar));

                    if (File.Exists(fullPath))
                    {
                        var bytes = File.ReadAllBytes(fullPath);
                        var ext = Path.GetExtension(fullPath).TrimStart('.').ToLower();
                        var mime = ext == "png" ? "image/png" : "image/jpeg";
                        return $"data:{mime};base64,{Convert.ToBase64String(bytes)}";
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Error resolving image path: {ex.Message}");
                }

                // If file doesn't exist on disk, assume it's static in the shared project
                return "_content/Beauty_Aesthetics_Hybrid.Shared/" + normalizedPath.TrimStart('/');
            }

            return path;
        }
    }
}
