using Beauty_Aesthetics_WebPos.APIClient;
using Beauty_Aesthetics_WebPos.APIClient.ResultPattern;
using Beauty_Aesthetics_WebPos.Models.DTOs;
using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Beauty_Aesthetics_WebPos.Components.Services.Clinical;

public sealed class FollowUpService : IFollowUpService
{
    private const string FollowUpMarker = "data-beauty-record=\"follow-up\"";
    private const string MedicalCertificateMarker = "data-beauty-record=\"medical-certificate\"";
    private const string PayloadMarker = "data-follow-up-html=\"";

    private readonly CustomerVisitNoteAC visitNoteAC;

    public FollowUpService(CustomerVisitNoteAC visitNoteAC)
    {
        this.visitNoteAC = visitNoteAC;
    }

    public async Task<ApiCallResult<IReadOnlyList<FollowUpRecordDTO>>> LoadByCustomerAsync(
        string customerId,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(customerId))
        {
            return ApiCallResult<IReadOnlyList<FollowUpRecordDTO>>.Failure(
                HttpStatusCode.BadRequest,
                "Customer ID is required before loading follow-up history.");
        }

        var result = await visitNoteAC.LoadByCustomerAsync(customerId, cancellationToken);
        if (!result.Success || result.Value is null)
        {
            return ApiCallResult<IReadOnlyList<FollowUpRecordDTO>>.Failure(
                result.StatusCode,
                result.ErrorMessage ?? "Unable to load follow-up history.");
        }

        var records = new List<FollowUpRecordDTO>();
        foreach (var row in result.Value)
        {
            if (string.IsNullOrWhiteSpace(row.CustomerVisitNoteID))
            {
                continue;
            }

            if (!string.IsNullOrWhiteSpace(row.RtfMessage) &&
                row.RtfMessage.Contains(MedicalCertificateMarker, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var content = ExtractFollowUpContent(row.RtfMessage);
            if (string.IsNullOrWhiteSpace(content))
            {
                continue;
            }

            var date = row.ModifiedDateTime != default && row.ModifiedDateTime != DateTime.MinValue
                ? row.ModifiedDateTime
                : row.CreatedDateTime != default && row.CreatedDateTime != DateTime.MinValue
                    ? row.CreatedDateTime
                    : row.FinancialDate != default && row.FinancialDate != DateTime.MinValue
                        ? row.FinancialDate
                        : DateTime.Now;

            records.Add(new FollowUpRecordDTO
            {
                RecordId = row.CustomerVisitNoteID,
                CustomerId = string.IsNullOrWhiteSpace(row.CustomerID) ? customerId : row.CustomerID,
                Date = date,
                Content = content
            });
        }

        return ApiCallResult<IReadOnlyList<FollowUpRecordDTO>>.Ok(
            result.StatusCode,
            records.OrderByDescending(record => record.Date).ToList());
    }

    public async Task<ApiCallResult<FollowUpRecordDTO>> SaveAsync(
        FollowUpRecordDTO record,
        string branchId,
        string groupId,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(record.CustomerId))
        {
            return ApiCallResult<FollowUpRecordDTO>.Failure(
                HttpStatusCode.BadRequest,
                "Customer ID is required before saving a follow-up.");
        }

        if (string.IsNullOrWhiteSpace(record.Content))
        {
            return ApiCallResult<FollowUpRecordDTO>.Failure(
                HttpStatusCode.BadRequest,
                "Follow-up content is required.");
        }

        var isUpdate = !string.IsNullOrWhiteSpace(record.RecordId);
        var payload = new CustomerVisitNoteWriteDTO
        {
            CustomerVisitNoteID = record.RecordId,
            FinancialDate = record.Date == default ? DateTime.Now : record.Date,
            RtfMessage = BuildRecordHtml(record.Content),
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
        {
            return ApiCallResult<FollowUpRecordDTO>.Failure(
                response.StatusCode,
                response.ErrorMessage ?? "Unable to save follow-up.");
        }

        if (!isUpdate)
        {
            record.RecordId = ExtractId(response.Value);
            if (string.IsNullOrWhiteSpace(record.RecordId))
            {
                var reload = await LoadByCustomerAsync(record.CustomerId, cancellationToken);
                if (reload.Success && reload.Value is not null)
                {
                    var match = reload.Value
                        .Where(item => string.Equals(item.Content, record.Content, StringComparison.Ordinal))
                        .OrderByDescending(item => item.Date)
                        .FirstOrDefault();
                    if (match is not null)
                    {
                        record.RecordId = match.RecordId;
                        record.Date = match.Date;
                    }
                }
            }
        }

        return ApiCallResult<FollowUpRecordDTO>.Ok(response.StatusCode, record);
    }

    public Task<ApiCallResult<bool>> DeleteAsync(
        string recordId,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(recordId))
        {
            return Task.FromResult(
                ApiCallResult<bool>.Failure(
                    HttpStatusCode.BadRequest,
                    "Follow-up record ID is missing."));
        }

        return visitNoteAC.DeleteAsync(recordId, cancellationToken);
    }

    private static string BuildRecordHtml(string content)
    {
        var encoded = Convert.ToBase64String(Encoding.UTF8.GetBytes(content));
        var preview = Regex.Replace(content, "<[^>]+>", " ");
        preview = System.Net.WebUtility.HtmlDecode(preview);
        preview = Regex.Replace(preview, @"\s+", " ").Trim();
        if (preview.Length > 240)
        {
            preview = preview[..240] + "...";
        }

        var safePreview = System.Net.WebUtility.HtmlEncode(preview);
        return $"<div class=\"follow-up-record\" {FollowUpMarker} {PayloadMarker}{encoded}\">" +
               $"<div class=\"visit-note-text\">{safePreview}</div></div>";
    }

    private static string ExtractFollowUpContent(string? html)
    {
        if (string.IsNullOrWhiteSpace(html))
        {
            return string.Empty;
        }

        if (!html.Contains(FollowUpMarker, StringComparison.OrdinalIgnoreCase))
        {
            // Backward compatibility: Senang/older Beauty customer visit notes
            // did not include a Beauty record marker. Treat any non-medical
            // visit-note record as a legacy follow-up.
            return html;
        }

        var markerIndex = html.IndexOf(PayloadMarker, StringComparison.OrdinalIgnoreCase);
        if (markerIndex < 0)
        {
            return html;
        }

        var payloadStart = markerIndex + PayloadMarker.Length;
        var payloadEnd = html.IndexOf('"', payloadStart);
        if (payloadEnd <= payloadStart)
        {
            return html;
        }

        try
        {
            var encoded = html[payloadStart..payloadEnd];
            return Encoding.UTF8.GetString(Convert.FromBase64String(encoded));
        }
        catch
        {
            return html;
        }
    }

    private static string ExtractId(JsonElement response)
    {
        if (response.ValueKind == JsonValueKind.String)
        {
            return response.GetString() ?? string.Empty;
        }

        if (response.ValueKind != JsonValueKind.Object)
        {
            return string.Empty;
        }

        foreach (var key in new[] { "Id", "id", "ID", "CustomerVisitNoteID", "customerVisitNoteID" })
        {
            if (!response.TryGetProperty(key, out var value))
            {
                continue;
            }

            if (value.ValueKind == JsonValueKind.String)
            {
                return value.GetString() ?? string.Empty;
            }

            if (value.ValueKind == JsonValueKind.Number)
            {
                return value.ToString();
            }
        }

        return string.Empty;
    }
}
