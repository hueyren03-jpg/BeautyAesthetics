using Beauty_Aesthetics_WebPos.Components.Services.Files;

namespace Beauty_Aesthetics_Hybrid.Services;

public sealed class MauiFileDownloadService : IFileDownloadService
{
    public async Task DownloadBinaryFileAsync(
        string fileName,
        string base64Content,
        string mimeType,
        CancellationToken cancellationToken = default)
    {
        var bytes = Convert.FromBase64String(NormalizeBase64(base64Content));

#if ANDROID
        await SaveToAndroidDownloadsAsync(fileName, bytes, mimeType, cancellationToken);
#else
        await SaveAndShareAsync(fileName, bytes, mimeType, cancellationToken);
#endif
    }

#if ANDROID
    private static async Task SaveToAndroidDownloadsAsync(
        string fileName,
        byte[] bytes,
        string mimeType,
        CancellationToken cancellationToken)
    {
        try
        {
            var values = new Android.Content.ContentValues();
            values.Put(Android.Provider.MediaStore.IMediaColumns.DisplayName, fileName);
            values.Put(Android.Provider.MediaStore.IMediaColumns.MimeType, mimeType);
            values.Put(
                Android.Provider.MediaStore.IMediaColumns.RelativePath,
                Android.OS.Environment.DirectoryDownloads);

            var resolver = Android.App.Application.Context.ContentResolver;
            var uri = resolver?.Insert(
                Android.Provider.MediaStore.Downloads.ExternalContentUri,
                values);

            if (uri is null)
            {
                await SaveAndShareAsync(fileName, bytes, mimeType, cancellationToken);
                return;
            }

            await using var stream = resolver!.OpenOutputStream(uri);
            if (stream is null)
            {
                await SaveAndShareAsync(fileName, bytes, mimeType, cancellationToken);
                return;
            }

            await stream.WriteAsync(bytes, cancellationToken);
            await stream.FlushAsync(cancellationToken);

            MainThread.BeginInvokeOnMainThread(() =>
            {
                Android.Widget.Toast.MakeText(
                    Android.App.Application.Context,
                    $"Saved to Downloads/{fileName}",
                    Android.Widget.ToastLength.Long)?.Show();
            });
        }
        catch
        {
            await SaveAndShareAsync(fileName, bytes, mimeType, cancellationToken);
        }
    }
#endif

    private static async Task SaveAndShareAsync(
        string fileName,
        byte[] bytes,
        string mimeType,
        CancellationToken cancellationToken)
    {
        var filePath = Path.Combine(FileSystem.Current.CacheDirectory, fileName);
        await File.WriteAllBytesAsync(filePath, bytes, cancellationToken);

        await Share.Default.RequestAsync(new ShareFileRequest
        {
            Title = fileName,
            File = new ShareFile(filePath, mimeType)
        });
    }

    private static string NormalizeBase64(string value)
    {
        var trimmed = value.Trim();
        var commaIndex = trimmed.IndexOf(',');
        return trimmed.StartsWith("data:", StringComparison.OrdinalIgnoreCase) && commaIndex >= 0
            ? trimmed[(commaIndex + 1)..]
            : trimmed;
    }
}
