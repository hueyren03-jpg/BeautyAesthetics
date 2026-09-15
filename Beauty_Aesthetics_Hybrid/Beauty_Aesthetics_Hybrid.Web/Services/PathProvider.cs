using Microsoft.AspNetCore.Hosting;
using Beauty_Aesthetics_WebPos.Components.Services;

namespace Beauty_Aesthetics_Hybrid.Web.Services
{
    public class PathProvider : IPathProvider
    {
        private readonly IWebHostEnvironment _environment;

        public PathProvider(IWebHostEnvironment environment)
        {
            _environment = environment;
        }

        public string GetWebRootPath()
        {
            return _environment.WebRootPath;
        }
    }
}
