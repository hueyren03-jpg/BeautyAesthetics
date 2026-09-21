using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Beauty_Aesthetics_WebPos.APIClient.ResultPattern;
using Beauty_Aesthetics_WebPos.Components.Services.Auth;
using Beauty_Aesthetics_WebPos.Models.DTOs;

namespace Beauty_Aesthetics_WebPos.APIClient;

public sealed class BranchAC
{
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };
    private static readonly MediaTypeHeaderValue JsonPatchMediaType =
        MediaTypeHeaderValue.Parse("application/json-patch+json");
    private readonly IAuthService authService;

    public BranchAC(IAuthService authService)
    {
        this.authService = authService;
    }

    public async Task<ApiCallResult<List<BranchLookupDTO>>> LoadBranchesAsync(
        CancellationToken cancellationToken = default)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/Branch/LoadProxy")
        {
            Content = JsonContent.Create(new { }, mediaType: JsonPatchMediaType, options: JsonOptions)
        };
        using var response = await authService.SendAuthorizedAsync(request, cancellationToken);

        if (response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
        {
            return ApiCallResult<List<BranchLookupDTO>>.Unauthorized(response.StatusCode);
        }

        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            return ApiCallResult<List<BranchLookupDTO>>.Failure(
                response.StatusCode,
                $"Unable to load branches ({(int)response.StatusCode}).");
        }

        try
        {
            var wrapped = JsonSerializer.Deserialize<ApiResponse<List<BranchLookupDTO>>>(body, JsonOptions);
            if (wrapped?.StatusCode > 0)
            {
                if (!wrapped.IsSuccess)
                {
                    return ApiCallResult<List<BranchLookupDTO>>.Failure(
                        response.StatusCode,
                        string.IsNullOrWhiteSpace(wrapped.Message) ? "Unable to load branches." : wrapped.Message);
                }

                return ApiCallResult<List<BranchLookupDTO>>.Ok(
                    response.StatusCode,
                    wrapped.Result ?? []);
            }

            var direct = JsonSerializer.Deserialize<List<BranchLookupDTO>>(body, JsonOptions);
            return direct is null
                ? ApiCallResult<List<BranchLookupDTO>>.Failure(response.StatusCode, "Branch list response was invalid.")
                : ApiCallResult<List<BranchLookupDTO>>.Ok(response.StatusCode, direct);
        }
        catch (JsonException)
        {
            return ApiCallResult<List<BranchLookupDTO>>.Failure(response.StatusCode, "Branch list response was invalid.");
        }
    }
}