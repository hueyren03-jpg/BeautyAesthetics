using Beauty_Aesthetics_WebPos.APIClient.ResultPattern;
using Beauty_Aesthetics_WebPos.Components.Services.Auth;
using Beauty_Aesthetics_WebPos.Models.DTOs;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace Beauty_Aesthetics_WebPos.APIClient;

public sealed class CustomerFollowUpAC
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private static readonly MediaTypeHeaderValue JsonPatchMediaType =
        MediaTypeHeaderValue.Parse("application/json-patch+json");

    private readonly IAuthService authService;

    public CustomerFollowUpAC(IAuthService authService)
    {
        this.authService = authService;
    }

    public async Task<ApiCallResult<List<CustomerFollowUpDTO>>> LoadByCustomerAsync(
        string customerId,
        CancellationToken cancellationToken = default)
    {
        var url = $"/api/CustomerFollowUp/LoadProxyByCustomerID?id={Uri.EscapeDataString(customerId ?? string.Empty)}";
        Console.WriteLine($"[API GET api/CustomerFollowUp/LoadProxyByCustomerID] CustomerID={customerId}");

        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        using var response = await authService.SendAuthorizedAsync(request, cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        Console.WriteLine(
            $"[API GET api/CustomerFollowUp/LoadProxyByCustomerID] HTTP {(int)response.StatusCode} | Body: {body}");

        return ReadResponseBody<List<CustomerFollowUpDTO>>(
            response.StatusCode,
            response.IsSuccessStatusCode,
            body,
            "Unable to load customer follow-up records.");
    }

    public async Task<ApiCallResult<List<CustomerFollowUpDTO>>> LoadByBranchAsync(
        DateTime startDate,
        DateTime endDate,
        string branchId,
        CancellationToken cancellationToken = default)
    {
        var payload = new CustomerFollowUpBranchRequestDTO
        {
            StartDate = startDate,
            EndDate = endDate,
            BranchID = branchId ?? string.Empty
        };

        Console.WriteLine(
            $"[API POST api/CustomerFollowUp/LoadProxyByBranchID] " +
            $"BranchID={payload.BranchID} | Start={payload.StartDate:O} | End={payload.EndDate:O}");

        using var request = new HttpRequestMessage(
            HttpMethod.Post,
            "/api/CustomerFollowUp/LoadProxyByBranchID")
        {
            Content = JsonContent.Create(
                payload,
                mediaType: JsonPatchMediaType,
                options: JsonOptions)
        };

        using var response = await authService.SendAuthorizedAsync(request, cancellationToken);
        Console.WriteLine($"[API POST api/CustomerFollowUp/LoadProxyByBranchID] HTTP {(int)response.StatusCode}");

        return await ReadResponseAsync<List<CustomerFollowUpDTO>>(
            response,
            "Unable to load branch follow-up records.",
            cancellationToken);
    }

    public async Task<ApiCallResult<CustomerFollowUpDTO>> LoadRecordAsync(
        string recordId,
        CancellationToken cancellationToken = default)
    {
        var url = $"/api/CustomerFollowUp/LoadRecord?id={Uri.EscapeDataString(recordId ?? string.Empty)}";
        Console.WriteLine($"[API POST api/CustomerFollowUp/LoadRecord] RecordID={recordId}");

        using var request = new HttpRequestMessage(HttpMethod.Post, url);
        using var response = await authService.SendAuthorizedAsync(request, cancellationToken);
        Console.WriteLine($"[API POST api/CustomerFollowUp/LoadRecord] HTTP {(int)response.StatusCode}");

        return await ReadResponseAsync<CustomerFollowUpDTO>(
            response,
            "Unable to load the follow-up record.",
            cancellationToken);
    }

    public Task<ApiCallResult<JsonElement>> CreateAsync(
        CustomerFollowUpDTO record,
        CancellationToken cancellationToken = default) =>
        SendRecordAsync(
            HttpMethod.Post,
            "/api/CustomerFollowUp/CreateRecord",
            record,
            cancellationToken);

    public Task<ApiCallResult<JsonElement>> UpdateAsync(
        CustomerFollowUpDTO record,
        CancellationToken cancellationToken = default) =>
        SendRecordAsync(
            HttpMethod.Put,
            "/api/CustomerFollowUp/UpdateRecord",
            record,
            cancellationToken);

    public async Task<ApiCallResult<bool>> DeleteAsync(
        string recordId,
        CancellationToken cancellationToken = default)
    {
        var url = $"/api/CustomerFollowUp/Delete?id={Uri.EscapeDataString(recordId ?? string.Empty)}";
        Console.WriteLine($"[API DELETE api/CustomerFollowUp/Delete] RecordID={recordId}");

        using var request = new HttpRequestMessage(HttpMethod.Delete, url);
        using var response = await authService.SendAuthorizedAsync(request, cancellationToken);
        Console.WriteLine($"[API DELETE api/CustomerFollowUp/Delete] HTTP {(int)response.StatusCode}");

        if (response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
        {
            return ApiCallResult<bool>.Unauthorized(response.StatusCode);
        }

        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        return response.IsSuccessStatusCode
            ? ApiCallResult<bool>.Ok(response.StatusCode, true)
            : ApiCallResult<bool>.Failure(
                response.StatusCode,
                ReadError(body, "Unable to delete customer follow-up."));
    }

    private async Task<ApiCallResult<JsonElement>> SendRecordAsync(
        HttpMethod method,
        string url,
        CustomerFollowUpDTO record,
        CancellationToken cancellationToken)
    {
        Console.WriteLine(
            $"[API {method.Method} {url.TrimStart('/')}] " +
            $"CustomerID={record.CustomerID} | RecordID={record.CustomerVisitNoteID} | " +
            $"BranchID={record.BranchID} | GroupID={record.GroupID} | " +
            $"RtfLength={record.RtfMessage?.Length ?? 0}");

        using var request = new HttpRequestMessage(method, url)
        {
            Content = JsonContent.Create(
                record,
                mediaType: JsonPatchMediaType,
                options: JsonOptions)
        };

        using var response = await authService.SendAuthorizedAsync(request, cancellationToken);
        Console.WriteLine($"[API {method.Method} {url.TrimStart('/')}] HTTP {(int)response.StatusCode}");

        if (response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
        {
            return ApiCallResult<JsonElement>.Unauthorized(response.StatusCode);
        }

        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            return ApiCallResult<JsonElement>.Failure(
                response.StatusCode,
                ReadError(body, "Unable to save customer follow-up."));
        }

        if (string.IsNullOrWhiteSpace(body))
        {
            return ApiCallResult<JsonElement>.Ok(response.StatusCode, default);
        }

        try
        {
            var wrapped = JsonSerializer.Deserialize<ApiResponse<JsonElement>>(body, JsonOptions);
            if (wrapped?.StatusCode > 0)
            {
                if (!wrapped.IsSuccess)
                {
                    return ApiCallResult<JsonElement>.Failure(
                        response.StatusCode,
                        string.IsNullOrWhiteSpace(wrapped.Message)
                            ? "Unable to save customer follow-up."
                            : wrapped.Message);
                }

                return ApiCallResult<JsonElement>.Ok(response.StatusCode, wrapped.Result);
            }

            using var doc = JsonDocument.Parse(body);
            return ApiCallResult<JsonElement>.Ok(
                response.StatusCode,
                doc.RootElement.Clone());
        }
        catch (JsonException)
        {
            return ApiCallResult<JsonElement>.Ok(response.StatusCode, default);
        }
    }

    private static ApiCallResult<T> ReadResponseBody<T>(
        HttpStatusCode statusCode,
        bool isSuccessStatusCode,
        string body,
        string fallback)
    {
        if (statusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
        {
            return ApiCallResult<T>.Unauthorized(statusCode);
        }

        if (string.IsNullOrWhiteSpace(body))
        {
            return ApiCallResult<T>.Failure(statusCode, fallback);
        }

        try
        {
            // CustomerFollowUp/LoadProxyByCustomerID currently returns HTTP 404
            // while its JSON envelope contains StatusCode=200 and a valid Result.
            // Treat the API envelope as the source of truth before transport status.
            var wrapped = JsonSerializer.Deserialize<ApiResponse<T>>(body, JsonOptions);
            if (wrapped?.StatusCode > 0)
            {
                var logicalStatus = Enum.IsDefined(typeof(HttpStatusCode), wrapped.StatusCode)
                    ? (HttpStatusCode)wrapped.StatusCode
                    : statusCode;

                if (!wrapped.IsSuccess)
                {
                    return ApiCallResult<T>.Failure(
                        logicalStatus,
                        string.IsNullOrWhiteSpace(wrapped.Message)
                            ? fallback
                            : wrapped.Message);
                }

                if (wrapped.Result is null)
                {
                    return ApiCallResult<T>.Failure(logicalStatus, fallback);
                }

                if (!isSuccessStatusCode)
                {
                    Console.WriteLine(
                        $"[CustomerFollowUp] LEGACY ENVELOPE ACCEPTED | " +
                        $"Transport={(int)statusCode} | ApiStatus={wrapped.StatusCode} | Message={wrapped.Message}");
                }

                return ApiCallResult<T>.Ok(logicalStatus, wrapped.Result);
            }

            if (!isSuccessStatusCode)
            {
                return ApiCallResult<T>.Failure(
                    statusCode,
                    ReadError(body, fallback));
            }

            var direct = JsonSerializer.Deserialize<T>(body, JsonOptions);
            return direct is null
                ? ApiCallResult<T>.Failure(statusCode, fallback)
                : ApiCallResult<T>.Ok(statusCode, direct);
        }
        catch (JsonException ex)
        {
            Console.WriteLine($"[CustomerFollowUp] HISTORY PARSE FAILED | {ex.Message}");
            return ApiCallResult<T>.Failure(
                statusCode,
                $"{fallback} Response was invalid.");
        }
    }

    private static async Task<ApiCallResult<T>> ReadResponseAsync<T>(
        HttpResponseMessage response,
        string fallback,
        CancellationToken cancellationToken)
    {
        if (response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
        {
            return ApiCallResult<T>.Unauthorized(response.StatusCode);
        }

        var body = await response.Content.ReadAsStringAsync(cancellationToken);

        return ReadResponseBody<T>(
            response.StatusCode,
            response.IsSuccessStatusCode,
            body,
            fallback);
    }

    private static string ReadError(string body, string fallback)
    {
        try
        {
            using var doc = JsonDocument.Parse(body);
            var root = doc.RootElement;

            foreach (var name in new[] { "detail", "Detail", "message", "Message", "title", "Title" })
            {
                if (root.TryGetProperty(name, out var value) &&
                    value.ValueKind == JsonValueKind.String &&
                    !string.IsNullOrWhiteSpace(value.GetString()))
                {
                    return value.GetString()!;
                }
            }
        }
        catch (JsonException)
        {
        }

        return fallback;
    }
}
