using Beauty_Aesthetics_WebPos.APIClient;
using Beauty_Aesthetics_WebPos.APIClient.ResultPattern;
using Beauty_Aesthetics_WebPos.Models.DTOs;
using System.Net;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;

namespace Beauty_Aesthetics_WebPos.Components.Services.Clinical;

public sealed class MedicalCertificateService : IMedicalCertificateService
{
    private const string RecordMarker = "data-beauty-record=\"medical-certificate\"";
    private const string PayloadMarker = "data-mc-json=\"";
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    private readonly CustomerVisitNoteAC visitNoteAC;

    public MedicalCertificateService(CustomerVisitNoteAC visitNoteAC) => this.visitNoteAC = visitNoteAC;

    public async Task<ApiCallResult<IReadOnlyList<MedicalCertificateRecordDTO>>> LoadByCustomerAsync(
        string customerId,
        CancellationToken cancellationToken = default)
    {
        var result = await visitNoteAC.LoadByCustomerAsync(customerId, cancellationToken);
        if (!result.Success || result.Value is null)
            return ApiCallResult<IReadOnlyList<MedicalCertificateRecordDTO>>.Failure(
                result.StatusCode,
                result.ErrorMessage ?? "Unable to load medical certificate history.");

        var certificates = new List<MedicalCertificateRecordDTO>();
        foreach (var row in result.Value)
        {
            if (!TryParseRecord(row.RtfMessage, out var record) || record is null) continue;

            record.RecordId = row.CustomerVisitNoteID;
            if (string.IsNullOrWhiteSpace(record.CustomerId)) record.CustomerId = row.CustomerID;
            if (record.IssuedAt == default)
                record.IssuedAt = row.FinancialDate != default ? row.FinancialDate : row.CreatedDateTime;
            certificates.Add(record);
        }

        return ApiCallResult<IReadOnlyList<MedicalCertificateRecordDTO>>.Ok(
            result.StatusCode,
            certificates.OrderByDescending(item => item.IssuedAt).ToList());
    }

    public async Task<ApiCallResult<MedicalCertificateRecordDTO>> SaveAsync(
        MedicalCertificateRecordDTO record,
        string branchId,
        string groupId,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(record.CustomerId))
            return ApiCallResult<MedicalCertificateRecordDTO>.Failure(
                HttpStatusCode.BadRequest,
                "Customer ID is required before issuing a medical certificate.");

        var isUpdate = !string.IsNullOrWhiteSpace(record.RecordId);
        var payload = new CustomerVisitNoteWriteDTO
        {
            CustomerVisitNoteID = record.RecordId,
            FinancialDate = record.IssuedAt == default ? DateTime.Now : record.IssuedAt,
            RtfMessage = BuildRecordHtml(record),
            CustomerID = record.CustomerId,
            BranchID = branchId ?? string.Empty,
            GroupID = groupId ?? string.Empty,
            Symptoms = null,
            Diagnoses = null,
            SaveAction = isUpdate ? 2 : 1,
            IsDirty = true,
            IsLoading = false
        };

        var response = isUpdate
            ? await visitNoteAC.UpdateAsync(payload, cancellationToken)
            : await visitNoteAC.CreateAsync(payload, cancellationToken);

        if (!response.Success)
            return ApiCallResult<MedicalCertificateRecordDTO>.Failure(
                response.StatusCode,
                response.ErrorMessage ?? "Unable to save medical certificate.");

        if (!isUpdate)
        {
            record.RecordId = ExtractId(response.Value);
            if (string.IsNullOrWhiteSpace(record.RecordId))
            {
                var reload = await LoadByCustomerAsync(record.CustomerId, cancellationToken);
                var match = reload.Value?.FirstOrDefault(item =>
                    item.CertificateNumber.Equals(record.CertificateNumber, StringComparison.OrdinalIgnoreCase));
                if (match is not null) record.RecordId = match.RecordId;
            }
        }

        return ApiCallResult<MedicalCertificateRecordDTO>.Ok(response.StatusCode, record);
    }

    public Task<ApiCallResult<bool>> DeleteAsync(
        string recordId,
        CancellationToken cancellationToken = default) =>
        visitNoteAC.DeleteAsync(recordId, cancellationToken);

    private static string BuildRecordHtml(MedicalCertificateRecordDTO record)
    {
        var json = JsonSerializer.Serialize(record, JsonOptions);
        var encodedPayload = Convert.ToBase64String(Encoding.UTF8.GetBytes(json));
        var summary = System.Net.WebUtility.HtmlEncode(
            $"Medical Certificate {record.CertificateNumber} | {TypeLabel(record.CertificateType)} | {record.DurationText} | {record.Reason}");

        return $"<div class=\"medical-certificate-record\" {RecordMarker} {PayloadMarker}{encodedPayload}\">" +
               $"<div class=\"visit-note-text\">{summary}</div></div>";
    }

    private static bool TryParseRecord(string? html, out MedicalCertificateRecordDTO? record)
    {
        record = null;
        if (string.IsNullOrWhiteSpace(html) ||
            !html.Contains(RecordMarker, StringComparison.OrdinalIgnoreCase))
            return false;

        var markerIndex = html.IndexOf(PayloadMarker, StringComparison.OrdinalIgnoreCase);
        if (markerIndex < 0) return false;

        var payloadStart = markerIndex + PayloadMarker.Length;
        var payloadEnd = html.IndexOf('"', payloadStart);
        if (payloadEnd <= payloadStart) return false;

        try
        {
            var encoded = html[payloadStart..payloadEnd];
            var json = Encoding.UTF8.GetString(Convert.FromBase64String(encoded));
            record = JsonSerializer.Deserialize<MedicalCertificateRecordDTO>(json, JsonOptions);
            return record is not null;
        }
        catch
        {
            return false;
        }
    }

    private static string ExtractId(JsonElement response)
    {
        if (response.ValueKind == JsonValueKind.String) return response.GetString() ?? string.Empty;
        if (response.ValueKind != JsonValueKind.Object) return string.Empty;

        foreach (var key in new[] { "Id", "id", "ID", "CustomerVisitNoteID", "customerVisitNoteID" })
        {
            if (!response.TryGetProperty(key, out var value)) continue;
            if (value.ValueKind == JsonValueKind.String) return value.GetString() ?? string.Empty;
            if (value.ValueKind == JsonValueKind.Number) return value.ToString();
        }

        return string.Empty;
    }

    private static string TypeLabel(string? type) =>
        string.Equals(type, "time-off", StringComparison.OrdinalIgnoreCase) ? "Time-Off" : "Day-Off";
}
