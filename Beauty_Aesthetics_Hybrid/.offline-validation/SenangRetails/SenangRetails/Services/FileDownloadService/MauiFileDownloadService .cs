using SenangRetails.Shared.Services.FileDownloadService;
using System.Text;

namespace SenangRetails.Services.FileDownloadService
{
    public class MauiFileDownloadService : IFileDownloadService
    {
        public async Task DownloadCsvTemplateAsync(string fileName, string[] headers, string? content = null)
        {
            try
            {
                var csvContent = content ?? string.Join(",", headers);

#if ANDROID
                await SaveFileAndroid(fileName, csvContent);
#elif IOS || MACCATALYST
                await SaveFileGeneric(fileName, csvContent);
#else
                await SaveFileGeneric(fileName, csvContent);
#endif
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error downloading file: {ex.Message}");
                throw;
            }
        }

#if ANDROID
        private async Task SaveFileAndroid(string fileName, string content)
        {
            try
            {
                // 2. IMPORTANT: Add UTF-8 Preamble (BOM) for Chinese Support
                var preamble = Encoding.UTF8.GetPreamble();
                var dataBytes = Encoding.UTF8.GetBytes(content);
                var combinedBytes = preamble.Concat(dataBytes).ToArray();

                var values = new Android.Content.ContentValues();
                values.Put(Android.Provider.MediaStore.IMediaColumns.DisplayName, fileName);
                values.Put(Android.Provider.MediaStore.IMediaColumns.MimeType, "text/csv");
                values.Put(Android.Provider.MediaStore.IMediaColumns.RelativePath, Android.OS.Environment.DirectoryDownloads);

                var resolver = Android.App.Application.Context.ContentResolver;
                var uri = resolver?.Insert(Android.Provider.MediaStore.Downloads.ExternalContentUri, values);

                if (uri != null)
                {
                    using var stream = resolver?.OpenOutputStream(uri);
                    if (stream != null)
                    {
                        await stream.WriteAsync(combinedBytes, 0, combinedBytes.Length);
                        await stream.FlushAsync();
                    }

                    MainThread.BeginInvokeOnMainThread(() =>
                    {
                        Android.Widget.Toast.MakeText(Android.App.Application.Context, $"Saved to Downloads/{fileName}", Android.Widget.ToastLength.Long)?.Show();
                    });
                }
                else
                {
                    await SaveFileGeneric(fileName, content);
                }
            }
            catch (Exception)
            {
                await SaveFileGeneric(fileName, content);
            }
        }
#endif

        private async Task SaveFileGeneric(string fileName, string content)
        {
            var filePath = Path.Combine(FileSystem.Current.CacheDirectory, fileName);

            var preamble = Encoding.UTF8.GetPreamble();
            var dataBytes = Encoding.UTF8.GetBytes(content);
            var combinedBytes = preamble.Concat(dataBytes).ToArray();

            await File.WriteAllBytesAsync(filePath, combinedBytes);

            await Share.Default.RequestAsync(new ShareFileRequest
            {
                Title = "Save Member Data",
                File = new ShareFile(filePath, "text/csv")
            });
        }

        public async Task DownloadBinaryFileAsync(string fileName, string base64Content, string mimeType)
        {
            var dataBytes = Convert.FromBase64String(base64Content);

#if ANDROID
            var values = new Android.Content.ContentValues();
            values.Put(Android.Provider.MediaStore.IMediaColumns.DisplayName, fileName);
            values.Put(Android.Provider.MediaStore.IMediaColumns.MimeType, mimeType);
            values.Put(Android.Provider.MediaStore.IMediaColumns.RelativePath, Android.OS.Environment.DirectoryDownloads);

            var resolver = Android.App.Application.Context.ContentResolver;
            var uri = resolver?.Insert(Android.Provider.MediaStore.Downloads.ExternalContentUri, values);

            if (uri != null)
            {
                using var stream = resolver?.OpenOutputStream(uri);
                await stream.WriteAsync(dataBytes, 0, dataBytes.Length);

                MainThread.BeginInvokeOnMainThread(() => {
                    Android.Widget.Toast.MakeText(Android.App.Application.Context, $"Saved to Downloads", Android.Widget.ToastLength.Long)?.Show();
                });
            }
#else
    // iOS / Windows Logic
    var filePath = Path.Combine(FileSystem.Current.CacheDirectory, fileName);
    await File.WriteAllBytesAsync(filePath, dataBytes);
    await Share.Default.RequestAsync(new ShareFileRequest { Title = fileName, File = new ShareFile(filePath, mimeType) });
#endif
        }
    }
}