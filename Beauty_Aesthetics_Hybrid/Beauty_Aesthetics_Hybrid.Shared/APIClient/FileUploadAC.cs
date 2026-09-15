using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using Beauty_Aesthetics_WebPos.APIClient.ResultPattern;
using Beauty_Aesthetics_WebPos.Components.Services.Auth;
using Beauty_Aesthetics_WebPos.Models.DTOs;
using Microsoft.AspNetCore.Components.Forms;

namespace Beauty_Aesthetics_WebPos.APIClient;

public sealed class FileUploadAC
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly IAuthService authService;

    public FileUploadAC(IAuthService authService)
    {
        this.authService = authService;
    }

    public async Task<ApiCallResult<string>> UploadImageAsync(
        IBrowserFile file,
        string subFolderName,
        CancellationToken cancellationToken = default)
    {
        if (file is null)
        {
            return ApiCallResult<string>.Failure(HttpStatusCode.BadRequest, "Select an image first.");
        }

        if (!file.ContentType.StartsWith("image/", StringComparison.OrdinalIgnoreCase))
        {
            return ApiCallResult<string>.Failure(HttpStatusCode.BadRequest, "Select a valid image file.");
        }

        const long maxFileSize = 5 * 1024 * 1024;
        if (file.Size > maxFileSize)
        {
            return ApiCallResult<string>.Failure(HttpStatusCode.BadRequest, "Image must be 5 MB or smaller.");
        }

        await using var stream = file.OpenReadStream(maxFileSize, cancellationToken);
        using var form = new MultipartFormDataContent();
        using var fileContent = new StreamContent(stream);
        fileContent.Headers.ContentType = MediaTypeHeaderValue.Parse(file.ContentType);
        form.Add(fileContent, "File", file.Name);
        form.Add(new StringContent(subFolderName), "SubFolderName");
        form.Add(new StringContent("true"), "FileRenameAllowed");

        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/FileUpload/UploadImageFile")
        {
            Content = form
        };
        using var response = await authService.SendAuthorizedAsync(request, cancellationToken);

        if (response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
        {
            return ApiCallResult<string>.Unauthorized(response.StatusCode);
        }

        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            return ApiCallResult<string>.Failure(response.StatusCode, "Image upload failed.");
        }

        try
        {
            var envelope = JsonSerializer.Deserialize<ApiResponse<FileUploadResponseDTO>>(body, JsonOptions);
            var uri = envelope?.Result?.FileUri;
            return !string.IsNullOrWhiteSpace(uri)
                ? ApiCallResult<string>.Ok(response.StatusCode, uri)
                : ApiCallResult<string>.Failure(response.StatusCode, "The image upload did not return a file path.");
        }
        catch (JsonException)
        {
            return ApiCallResult<string>.Failure(response.StatusCode, "Image upload response was invalid.");
        }
    }
}