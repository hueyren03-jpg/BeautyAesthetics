using Beauty_Aesthetics_WebPos.APIClient.ResultPattern;
using Beauty_Aesthetics_WebPos.Components.Services.Auth;
using Beauty_Aesthetics_WebPos.Models.DTOs;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace Beauty_Aesthetics_WebPos.APIClient;

public sealed class CustomerVisitNoteAC
{
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };
    private readonly IAuthService authService;

    public CustomerVisitNoteAC(IAuthService authService) => this.authService = authService;

    public async Task<ApiCallResult<List<CustomerVisitNoteRecordDTO>>> LoadByCustomerAsync(
        string customerId,
        CancellationToken cancellationToken = default)
    {
        var url = $"/api/CustomerVisitNote/LoadProxyByCustomerID?id={Uri.EscapeDataString(customerId ?? string.Empty)}";
        Console.WriteLine($"[API GET api/CustomerVisitNote/LoadProxyByCustomerID] CustomerID={customerId}");
        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        using var response = await authService.SendAuthorizedAsync(request, cancellationToken);
        Console.WriteLine($"[API GET api/CustomerVisitNote/LoadProxyByCustomerID] HTTP {(int)response.StatusCode}");

        if (response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
            return ApiCallResult<List<CustomerVisitNoteRecordDTO>>.Unauthorized(response.StatusCode);

        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode)
            return ApiCallResult<List<CustomerVisitNoteRecordDTO>>.Failure(
                response.StatusCode, ReadError(body, "Unable to load customer visit-note records."));

        try
        {
            var wrapped = JsonSerializer.Deserialize<ApiResponse<List<CustomerVisitNoteRecordDTO>>>(body, JsonOptions);
            if (wrapped?.StatusCode > 0)
            {
                if (!wrapped.IsSuccess)
                    return ApiCallResult<List<CustomerVisitNoteRecordDTO>>.Failure(
                        response.StatusCode,
                        string.IsNullOrWhiteSpace(wrapped.Message)
                            ? "Unable to load customer visit-note records."
                            : wrapped.Message);

                return ApiCallResult<List<CustomerVisitNoteRecordDTO>>.Ok(
                    response.StatusCode,
                    wrapped.Result ?? new List<CustomerVisitNoteRecordDTO>());
            }

            var direct = JsonSerializer.Deserialize<List<CustomerVisitNoteRecordDTO>>(body, JsonOptions);
            return ApiCallResult<List<CustomerVisitNoteRecordDTO>>.Ok(
                response.StatusCode,
                direct ?? new List<CustomerVisitNoteRecordDTO>());
        }
        catch (JsonException)
        {
            return ApiCallResult<List<CustomerVisitNoteRecordDTO>>.Failure(
                response.StatusCode,
                "Customer visit-note response was invalid.");
        }
    }

    public Task<ApiCallResult<JsonElement>> CreateAsync(
        CustomerVisitNoteWriteDTO record,
        CancellationToken cancellationToken = default) =>
        SendRecordAsync(HttpMethod.Post, "/api/CustomerVisitNote/CreateRecord", record, cancellationToken);

    public Task<ApiCallResult<JsonElement>> UpdateAsync(
        CustomerVisitNoteWriteDTO record,
        CancellationToken cancellationToken = default) =>
        SendRecordAsync(HttpMethod.Put, "/api/CustomerVisitNote/UpdateRecord", record, cancellationToken);

    public async Task<ApiCallResult<bool>> DeleteAsync(
        string recordId,
        CancellationToken cancellationToken = default)
    {
        var url = $"/api/CustomerVisitNote/Delete?id={Uri.EscapeDataString(recordId ?? string.Empty)}";
        Console.WriteLine($"[API DELETE api/CustomerVisitNote/Delete] RecordID={recordId}");
        using var request = new HttpRequestMessage(HttpMethod.Delete, url);
        using var response = await authService.SendAuthorizedAsync(request, cancellationToken);
        Console.WriteLine($"[API DELETE api/CustomerVisitNote/Delete] HTTP {(int)response.StatusCode}");

        if (response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
            return ApiCallResult<bool>.Unauthorized(response.StatusCode);

        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        return response.IsSuccessStatusCode
            ? ApiCallResult<bool>.Ok(response.StatusCode, true)
            : ApiCallResult<bool>.Failure(response.StatusCode, ReadError(body, "Unable to delete customer visit note."));
    }

    private async Task<ApiCallResult<JsonElement>> SendRecordAsync(
        HttpMethod method,
        string url,
        CustomerVisitNoteWriteDTO record,
        CancellationToken cancellationToken)
    {
        Console.WriteLine(
            $"[API {method.Method} {url.TrimStart('/')}] " +
            $"CustomerID={record.CustomerID} | RecordID={record.CustomerVisitNoteID} | " +
            $"BranchID={record.BranchID} | GroupID={record.GroupID} | RtfLength={record.RtfMessage?.Length ?? 0}");

        using var request = new HttpRequestMessage(method, url)
        {
            Content = JsonContent.Create(record, options: JsonOptions)
        };
        using var response = await authService.SendAuthorizedAsync(request, cancellationToken);
        Console.WriteLine($"[API {method.Method} {url.TrimStart('/')}] HTTP {(int)response.StatusCode}");

        if (response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
            return ApiCallResult<JsonElement>.Unauthorized(response.StatusCode);

        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode)
            return ApiCallResult<JsonElement>.Failure(
                response.StatusCode, ReadError(body, "Unable to save customer visit note."));

        try
        {
            var wrapped = JsonSerializer.Deserialize<ApiResponse<JsonElement>>(body, JsonOptions);
            if (wrapped?.StatusCode > 0)
            {
                if (!wrapped.IsSuccess)
                    return ApiCallResult<JsonElement>.Failure(
                        response.StatusCode,
                        string.IsNullOrWhiteSpace(wrapped.Message) ? "Unable to save customer visit note." : wrapped.Message);

                return ApiCallResult<JsonElement>.Ok(response.StatusCode, wrapped.Result);
            }

            using var doc = JsonDocument.Parse(string.IsNullOrWhiteSpace(body) ? "{}" : body);
            return ApiCallResult<JsonElement>.Ok(response.StatusCode, doc.RootElement.Clone());
        }
        catch (JsonException)
        {
            return ApiCallResult<JsonElement>.Ok(response.StatusCode, default);
        }
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
                    return value.GetString()!;
            }
        }
        catch (JsonException) { }

        return fallback;
    }
}
