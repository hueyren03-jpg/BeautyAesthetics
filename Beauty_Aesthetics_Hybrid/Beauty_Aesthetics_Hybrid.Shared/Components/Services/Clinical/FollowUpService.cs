using Beauty_Aesthetics_WebPos.APIClient;
using Beauty_Aesthetics_WebPos.APIClient.ResultPattern;
using Beauty_Aesthetics_WebPos.Components.Services;
using Beauty_Aesthetics_WebPos.Models.DTOs;
using System.Net;
using System.Text.Json;

namespace Beauty_Aesthetics_WebPos.Components.Services.Clinical;

public sealed class FollowUpService : IFollowUpService
{
    private readonly CustomerFollowUpAC followUpAC;
    private readonly AppState appState;

    public FollowUpService(CustomerFollowUpAC followUpAC, AppState appState)
    {
        this.followUpAC = followUpAC;
        this.appState = appState;
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

        var result = await followUpAC.LoadByCustomerAsync(
            customerId.Trim(),
            cancellationToken);

        if (!result.Success || result.Value is null)
        {
            return ApiCallResult<IReadOnlyList<FollowUpRecordDTO>>.Failure(
                result.StatusCode,
                result.ErrorMessage ?? "Unable to load follow-up history.");
        }

        var records = new List<FollowUpRecordDTO>();

        foreach (var row in result.Value
                     .Where(row => !string.IsNullOrWhiteSpace(row.CustomerVisitNoteID)))
        {
            var source = row;

            // Some proxy responses can omit the medical detail columns.
            // Hydrate the row through LoadRecord so history can restore
            // FinancialDate + Symptoms + Diagnoses + RtfMessage completely.
            if (string.IsNullOrWhiteSpace(row.Symptoms) ||
                string.IsNullOrWhiteSpace(row.Diagnoses) ||
                string.IsNullOrWhiteSpace(row.RtfMessage))
            {
                var fullRecord = await followUpAC.LoadRecordAsync(
                    row.CustomerVisitNoteID,
                    cancellationToken);

                if (fullRecord.Success && fullRecord.Value is not null)
                {
                    source = fullRecord.Value;
                    Console.WriteLine(
                        $"[CustomerFollowUp] HISTORY HYDRATE PASS | Record={row.CustomerVisitNoteID}");
                }
                else
                {
                    Console.WriteLine(
                        $"[CustomerFollowUp] HISTORY HYDRATE FALLBACK | Record={row.CustomerVisitNoteID} | " +
                        $"Status={(int)fullRecord.StatusCode} | Error={fullRecord.ErrorMessage}");
                }
            }

            var mapped = ToFollowUpRecord(source, customerId);
            if (!string.IsNullOrWhiteSpace(mapped.Content))
            {
                records.Add(mapped);
            }
        }

        records = records
            .OrderByDescending(record => record.Date)
            .ToList();

        Console.WriteLine(
            $"[CustomerFollowUp] LOAD CUSTOMER PASS | Customer={customerId} | Records={records.Count}");

        return ApiCallResult<IReadOnlyList<FollowUpRecordDTO>>.Ok(
            result.StatusCode,
            records);
    }

    public async Task<ApiCallResult<IReadOnlyList<FollowUpRecordDTO>>> LoadByBranchAsync(
        DateTime startDate,
        DateTime endDate,
        string branchId,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(branchId))
        {
            return ApiCallResult<IReadOnlyList<FollowUpRecordDTO>>.Failure(
                HttpStatusCode.BadRequest,
                "Branch ID is required before loading follow-up history.");
        }

        var result = await followUpAC.LoadByBranchAsync(
            startDate,
            endDate,
            branchId.Trim(),
            cancellationToken);

        if (!result.Success || result.Value is null)
        {
            return ApiCallResult<IReadOnlyList<FollowUpRecordDTO>>.Failure(
                result.StatusCode,
                result.ErrorMessage ?? "Unable to load branch follow-up history.");
        }

        var records = result.Value
            .Where(row => !string.IsNullOrWhiteSpace(row.CustomerVisitNoteID))
            .Select(row => ToFollowUpRecord(row, row.CustomerID))
            .Where(record => !string.IsNullOrWhiteSpace(record.Content))
            .OrderByDescending(record => record.Date)
            .ToList();

        return ApiCallResult<IReadOnlyList<FollowUpRecordDTO>>.Ok(
            result.StatusCode,
            records);
    }

    public async Task<ApiCallResult<FollowUpRecordDTO>> LoadRecordAsync(
        string recordId,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(recordId))
        {
            return ApiCallResult<FollowUpRecordDTO>.Failure(
                HttpStatusCode.BadRequest,
                "Follow-up record ID is required.");
        }

        var result = await followUpAC.LoadRecordAsync(
            recordId.Trim(),
            cancellationToken);

        if (!result.Success || result.Value is null)
        {
            return ApiCallResult<FollowUpRecordDTO>.Failure(
                result.StatusCode,
                result.ErrorMessage ?? "Unable to load the follow-up record.");
        }

        return ApiCallResult<FollowUpRecordDTO>.Ok(
            result.StatusCode,
            ToFollowUpRecord(result.Value, result.Value.CustomerID));
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

        if (string.IsNullOrWhiteSpace(record.Symptoms))
        {
            return ApiCallResult<FollowUpRecordDTO>.Failure(
                HttpStatusCode.BadRequest,
                "Symptoms are required before saving a follow-up.");
        }

        if (string.IsNullOrWhiteSpace(record.Diagnoses))
        {
            return ApiCallResult<FollowUpRecordDTO>.Failure(
                HttpStatusCode.BadRequest,
                "Diagnoses are required before saving a follow-up.");
        }

        if (string.IsNullOrWhiteSpace(branchId))
        {
            return ApiCallResult<FollowUpRecordDTO>.Failure(
                HttpStatusCode.BadRequest,
                "Branch ID is required before saving a follow-up.");
        }

        var isUpdate = !string.IsNullOrWhiteSpace(record.RecordId);
        CustomerFollowUpDTO? existing = null;

        if (isUpdate)
        {
            var loadResult = await followUpAC.LoadRecordAsync(
                record.RecordId,
                cancellationToken);

            if (!loadResult.Success || loadResult.Value is null)
            {
                return ApiCallResult<FollowUpRecordDTO>.Failure(
                    loadResult.StatusCode,
                    loadResult.ErrorMessage ??
                    "Unable to load the follow-up before updating it.");
            }

            existing = loadResult.Value;
        }

        var now = DateTime.Now;
        var actor = ResolveAuditUser();

        var payload = new CustomerFollowUpDTO
        {
            IsLoading = false,
            CustomerVisitNoteID = isUpdate ? record.RecordId.Trim() : string.Empty,
            FinancialDate = record.Date == default ? DateTime.Today : record.Date,
            CreatedBy = isUpdate && existing is not null && !string.IsNullOrWhiteSpace(existing.CreatedBy)
                ? existing.CreatedBy
                : actor,
            CreatedDateTime = isUpdate && existing is not null && existing.CreatedDateTime != default
                ? existing.CreatedDateTime
                : now,
            ModifiedBy = actor,
            ModifiedDateTime = now,
            RtfMessage = record.Content,
            CustomerID = record.CustomerId.Trim(),
            BranchID = branchId.Trim(),
            GroupID = string.IsNullOrWhiteSpace(groupId)
                ? branchId.Trim()
                : groupId.Trim(),
            Symptoms = record.Symptoms.Trim(),
            Diagnoses = record.Diagnoses.Trim(),
            SaveAction = isUpdate ? "Changed" : 1,
            IsDirty = true
        };

        Console.WriteLine(
            $"[CustomerFollowUp Beauty] {(isUpdate ? "UPDATE" : "CREATE")} | " +
            $"Customer={payload.CustomerID} | Record={payload.CustomerVisitNoteID} | " +
            $"FinancialDate={payload.FinancialDate:O} | Branch={payload.BranchID} | Group={payload.GroupID} | " +
            $"SaveAction={payload.SaveAction} | SymptomsLength={payload.Symptoms.Length} | " +
            $"DiagnosesLength={payload.Diagnoses.Length} | RtfLength={payload.RtfMessage.Length}");

        var response = isUpdate
            ? await followUpAC.UpdateAsync(payload, cancellationToken)
            : await followUpAC.CreateAsync(payload, cancellationToken);

        if (!response.Success)
        {
            return ApiCallResult<FollowUpRecordDTO>.Failure(
                response.StatusCode,
                response.ErrorMessage ?? "Unable to save follow-up.");
        }

        record.BranchId = payload.BranchID;
        record.GroupId = payload.GroupID;
        record.Symptoms = payload.Symptoms;
        record.Diagnoses = payload.Diagnoses;

        if (!isUpdate)
        {
            record.RecordId = ExtractId(response.Value);

            if (string.IsNullOrWhiteSpace(record.RecordId))
            {
                var reload = await LoadByCustomerAsync(
                    record.CustomerId,
                    cancellationToken);

                if (reload.Success && reload.Value is not null)
                {
                    var match = reload.Value
                        .Where(item =>
                            string.Equals(item.Content, record.Content, StringComparison.Ordinal) &&
                            string.Equals(item.Symptoms, record.Symptoms, StringComparison.Ordinal) &&
                            string.Equals(item.Diagnoses, record.Diagnoses, StringComparison.Ordinal))
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

        Console.WriteLine(
            $"[CustomerFollowUp Beauty] {(isUpdate ? "UPDATE" : "CREATE")} PASS | " +
            $"Customer={record.CustomerId} | Record={record.RecordId}");

        return ApiCallResult<FollowUpRecordDTO>.Ok(
            response.StatusCode,
            record);
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

        return followUpAC.DeleteAsync(
            recordId.Trim(),
            cancellationToken);
    }

    private static FollowUpRecordDTO ToFollowUpRecord(
        CustomerFollowUpDTO row,
        string fallbackCustomerId)
    {
        var date =
            row.FinancialDate != default &&
            row.FinancialDate != DateTime.MinValue
                ? row.FinancialDate
                : row.ModifiedDateTime != default &&
                  row.ModifiedDateTime != DateTime.MinValue
                    ? row.ModifiedDateTime
                    : row.CreatedDateTime != default &&
                      row.CreatedDateTime != DateTime.MinValue
                        ? row.CreatedDateTime
                        : DateTime.Today;

        return new FollowUpRecordDTO
        {
            RecordId = row.CustomerVisitNoteID ?? string.Empty,
            CustomerId = string.IsNullOrWhiteSpace(row.CustomerID)
                ? fallbackCustomerId ?? string.Empty
                : row.CustomerID,
            Date = date,
            Content = row.RtfMessage ?? string.Empty,
            BranchId = row.BranchID ?? string.Empty,
            GroupId = row.GroupID ?? string.Empty,
            Symptoms = row.Symptoms ?? string.Empty,
            Diagnoses = row.Diagnoses ?? string.Empty
        };
    }

    private string ResolveAuditUser()
    {
        if (!string.IsNullOrWhiteSpace(appState.UserEmail))
        {
            return appState.UserEmail.Trim();
        }

        return "POS";
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

        foreach (var key in new[]
        {
            "Id",
            "id",
            "ID",
            "CustomerVisitNoteID",
            "customerVisitNoteID"
        })
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
