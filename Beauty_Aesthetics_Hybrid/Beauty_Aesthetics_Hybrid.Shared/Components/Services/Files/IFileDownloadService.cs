namespace Beauty_Aesthetics_WebPos.Components.Services.Files;

public interface IFileDownloadService
{
    Task DownloadBinaryFileAsync(
        string fileName,
        string base64Content,
        string mimeType,
        CancellationToken cancellationToken = default);
}
