using System;
using System.Collections.Generic;
using System.Text;
using SenangRetails.Shared.Services.FilePickerService;

namespace SenangRetails.Services.FilePickerService
{
    public class MauiFilePickerService : IFilePickerService
    {
        public async Task<FilePickerResult?> PickFileAsync(string[] allowedExtensions)
        {
            try
            {
                var result = await FilePicker.Default.PickAsync(new PickOptions
                {
                    PickerTitle = "Select CSV File",
                    FileTypes = new FilePickerFileType(
                        new Dictionary<DevicePlatform, IEnumerable<string>>
                        {
                    { DevicePlatform.Android, new[] { "text/csv", "text/comma-separated-values" } },
                    { DevicePlatform.iOS, new[] { "public.comma-separated-values-text" } },
                    { DevicePlatform.WinUI, new[] { ".csv" } },
                        })
                });

                if (result == null) return null;

                var ext = Path.GetExtension(result.FileName).ToLower();

                // Strict check — reject anything that is not .csv
                if (ext != ".csv")
                {
                    await MainThread.InvokeOnMainThreadAsync(async () =>
                    {
                        var window = Application.Current?.Windows[0];
                        if (window?.Page != null)
                        {
                            await window.Page.DisplayAlertAsync(
                                "Invalid File",
                                "Only CSV files are accepted. Please select a .csv file.",
                                "OK");
                        }
                    });
                    return null;
                }

                var ms = new MemoryStream();
                using var stream = await result.OpenReadAsync();
                await stream.CopyToAsync(ms);
                ms.Position = 0;

                return new FilePickerResult
                {
                    FileName = result.FileName,
                    Extension = ext,
                    Stream = ms
                };
            }
            catch (Exception ex)
            {
                Console.WriteLine($"File picker error: {ex.Message}");
                return null;
            }
        }
    }
}
