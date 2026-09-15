using Microsoft.JSInterop;
using SenangRetails.Shared.Services.FileDownloadService;
using System.Text;

namespace SenangRetails.Web.Services.FileDownloadService
{
    public class WebFileDownloadService : IFileDownloadService
    {
        private readonly IJSRuntime _jsRuntime;

        public WebFileDownloadService(IJSRuntime jsRuntime)
        {
            _jsRuntime = jsRuntime;
        }

        public async Task DownloadCsvTemplateAsync(string fileName, string[] headers, string? content = null)
        {
            try
            {
                string csvData = content ?? string.Join(",", headers);

                var preamble = Encoding.UTF8.GetPreamble();
                var dataBytes = Encoding.UTF8.GetBytes(csvData);

                var combinedBytes = preamble.Concat(dataBytes).ToArray();

                var base64 = Convert.ToBase64String(combinedBytes);
                var dataUrl = $"data:text/csv;base64,{base64}";

                await _jsRuntime.InvokeVoidAsync("downloadFile", dataUrl, fileName);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error downloading file: {ex.Message}");
                throw;
            }
        }

        public async Task DownloadBinaryFileAsync(string fileName, string base64Content, string mimeType)
        {
            try
            {
                await _jsRuntime.InvokeVoidAsync("downloadFileFromBase64", fileName, base64Content);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Web Download Error: {ex.Message}");
            }
        }
    }
}