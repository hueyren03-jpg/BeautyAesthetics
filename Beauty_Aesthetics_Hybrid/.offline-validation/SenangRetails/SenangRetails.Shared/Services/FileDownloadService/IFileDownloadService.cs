using System;
using System.Collections.Generic;
using System.Text;

namespace SenangRetails.Shared.Services.FileDownloadService
{
    public interface IFileDownloadService
    {
        Task DownloadCsvTemplateAsync(string fileName, string[] headers, string? content = null);
        Task DownloadBinaryFileAsync(string fileName, string base64Content, string mimeType);
    }
}
