using Beauty_Aesthetics_WebPos.APIClient.ResultPattern;
using Beauty_Aesthetics_WebPos.Components.Services.Auth;
using Beauty_Aesthetics_WebPos.Models.DTOs;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace Beauty_Aesthetics_WebPos.APIClient;

public sealed class GSTTaxCodeAC
{
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };
    private static readonly MediaTypeHeaderValue JsonPatchMediaType =
        MediaTypeHeaderValue.Parse("application/json-patch+json");
    private readonly IAuthService authService;

    public GSTTaxCodeAC(IAuthService authService) => this.authService = authService;

    public async Task<ApiCallResult<List<GstTaxCodeDTO>>> LoadProxyByParentIdAsync(
        string taxTypeId,
        CancellationToken cancellationToken = default)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/GSTTaxCode/LoadProxyByParentID")
        {
            Content = JsonContent.Create(new { id = taxTypeId }, mediaType: JsonPatchMediaType, options: JsonOptions)
        };
        using var response = await authService.SendAuthorizedAsync(request, cancellationToken);

        if (response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
            return ApiCallResult<List<GstTaxCodeDTO>>.Unauthorized(response.StatusCode);

        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode)
            return ApiCallResult<List<GstTaxCodeDTO>>.Failure(response.StatusCode, ReadError(body));

        try
        {
            var wrapped = JsonSerializer.Deserialize<ApiResponse<List<GstTaxCodeDTO>>>(body, JsonOptions);
            if (wrapped?.StatusCode > 0)
            {
                if (!wrapped.IsSuccess)
                    return ApiCallResult<List<GstTaxCodeDTO>>.Failure(
                        response.StatusCode,
                        string.IsNullOrWhiteSpace(wrapped.Message) ? "GST tax code API request failed." : wrapped.Message);

                return ApiCallResult<List<GstTaxCodeDTO>>.Ok(
                    response.StatusCode,
                    wrapped.Result ?? new List<GstTaxCodeDTO>());
            }

            var direct = JsonSerializer.Deserialize<List<GstTaxCodeDTO>>(body, JsonOptions);
            return ApiCallResult<List<GstTaxCodeDTO>>.Ok(
                response.StatusCode,
                direct ?? new List<GstTaxCodeDTO>());
        }
        catch (JsonException)
        {
            return ApiCallResult<List<GstTaxCodeDTO>>.Failure(response.StatusCode, "GST tax code response was invalid.");
        }
    }

    private static string ReadError(string body)
    {
        try
        {
            using var json = JsonDocument.Parse(body);
            var root = json.RootElement;
            foreach (var name in new[] { "detail", "Detail", "message", "Message", "title", "Title" })
                if (root.TryGetProperty(name, out var value) &&
                    value.ValueKind == JsonValueKind.String &&
                    !string.IsNullOrWhiteSpace(value.GetString()))
                    return value.GetString()!;
        }
        catch (JsonException) { }

        return "GST tax code API request failed.";
    }
}
