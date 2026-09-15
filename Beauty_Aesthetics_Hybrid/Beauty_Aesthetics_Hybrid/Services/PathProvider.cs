using Microsoft.Maui.Storage;
using System.IO;
using Beauty_Aesthetics_WebPos.Components.Services;

namespace Beauty_Aesthetics_Hybrid.Services
{
    public class PathProvider : IPathProvider
    {
        public string GetWebRootPath()
        {
            // Use local app data directory for storing uploads in Blazor Hybrid
            var path = Path.Combine(FileSystem.AppDataDirectory, "wwwroot");
            if (!Directory.Exists(path))
            {
                Directory.CreateDirectory(path);
            }
            return path;
        }
    }
}
