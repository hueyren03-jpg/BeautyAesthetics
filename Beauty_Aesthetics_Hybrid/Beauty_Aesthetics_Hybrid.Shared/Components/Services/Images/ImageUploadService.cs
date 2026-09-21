using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Components.Forms;

namespace Beauty_Aesthetics_WebPos.Components.Services
{
    public class ImageUploadService
    {
        private readonly IPathProvider _pathProvider;

        public ImageUploadService(IPathProvider pathProvider)
        {
            _pathProvider = pathProvider;
        }

        public async Task<string> SaveCustomerImageAsync(IBrowserFile file)
        {
            if (file == null)
                return string.Empty;

            // Create upload directory if not exist
            var uploadsFolder = Path.Combine(_pathProvider.GetWebRootPath(), "images/customers");
            if (!Directory.Exists(uploadsFolder))
                Directory.CreateDirectory(uploadsFolder);

            // Generate unique file name
            var uniqueFileName = Guid.NewGuid().ToString() + Path.GetExtension(file.Name);
            var filePath = Path.Combine(uploadsFolder, uniqueFileName);

            // Limit file size to 5 MB
            await using var stream = new FileStream(filePath, FileMode.Create);
            await file.OpenReadStream(maxAllowedSize: 5 * 1024 * 1024).CopyToAsync(stream);

            // Return the relative URL for display
            return $"/images/customers/{uniqueFileName}";
        }
    }
}
