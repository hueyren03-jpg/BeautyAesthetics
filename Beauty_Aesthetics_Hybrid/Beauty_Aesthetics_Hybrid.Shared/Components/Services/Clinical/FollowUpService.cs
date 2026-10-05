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

    public FollowUpService(
        CustomerFollowUpAC followUpAC,
        AppState appState)
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

        var records = result.Value
            .Where(row => !string.IsNullOrWhiteSpace(row.CustomerVisitNoteID))
            .Select(row => ToFollowUpRecord(row, customerId))
            .Where(record => !string.IsNullOrWhiteSpace(record.Content))
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
            CustomerVisitNoteID = isUpdate
                ? record.RecordId.Trim()
                : string.Empty,
            FinancialDate = record.Date == default
                ? existing?.FinancialDate ?? now
                : record.Date,
            CreatedBy = isUpdate
                ? existing?.CreatedBy ?? actor
                : actor,
            CreatedDateTime = isUpdate && existing is not null &&
                              existing.CreatedDateTime != default &&
                              existing.CreatedDateTime != DateTime.MinValue
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
            Symptoms = NonNullBackendText(
                string.IsNullOrWhiteSpace(record.Symptoms)
                    ? existing?.Symptoms
                    : record.Symptoms),
            Diagnoses = NonNullBackendText(
                string.IsNullOrWhiteSpace(record.Diagnoses)
                    ? existing?.Diagnoses
                    : record.Diagnoses),
            SaveAction = "Changed",
            IsDirty = true
        };

        HashSet<string> preCreateIds = new(StringComparer.OrdinalIgnoreCase);
        if (!isUpdate)
        {
            var beforeCreate = await followUpAC.LoadByCustomerAsync(
                payload.CustomerID,
                cancellationToken);

            if (beforeCreate.Success && beforeCreate.Value is not null)
            {
                preCreateIds = beforeCreate.Value
                    .Where(item => !string.IsNullOrWhiteSpace(item.CustomerVisitNoteID))
                    .Select(item => item.CustomerVisitNoteID)
                    .ToHashSet(StringComparer.OrdinalIgnoreCase);

                var existingSameContent = beforeCreate.Value
                    .Where(item =>
                        string.Equals(item.RtfMessage, payload.RtfMessage, StringComparison.Ordinal) &&
                        string.Equals(item.BranchID, payload.BranchID, StringComparison.OrdinalIgnoreCase))
                    .OrderByDescending(item => ResolveRecordDate(item))
                    .FirstOrDefault();

                if (existingSameContent is not null &&
                    ResolveRecordDate(existingSameContent) >= now.AddMinutes(-10))
                {
                    Console.WriteLine(
                        $"[CustomerFollowUp] CREATE DUPLICATE PREVENTED | Customer={payload.CustomerID} | " +
                        $"Record={existingSameContent.CustomerVisitNoteID} | Branch={payload.BranchID}");

                    return ApiCallResult<FollowUpRecordDTO>.Ok(
                        HttpStatusCode.OK,
                        ToFollowUpRecord(existingSameContent, payload.CustomerID));
                }
            }
        }

        Console.WriteLine(
            $"[CustomerFollowUp] {(isUpdate ? "UPDATE" : "CREATE")} REQUEST | " +
            $"Customer={payload.CustomerID} | Record={payload.CustomerVisitNoteID} | " +
            $"Branch={payload.BranchID} | Group={payload.GroupID} | " +
            $"SymptomsLength={payload.Symptoms.Length} | DiagnosesLength={payload.Diagnoses.Length} | " +
            $"RtfLength={payload.RtfMessage.Length}");

        var response = isUpdate
            ? await followUpAC.UpdateAsync(payload, cancellationToken)
            : await followUpAC.CreateAsync(payload, cancellationToken);

        if (!response.Success)
        {
            var isDbNullServerBug =
                response.StatusCode == HttpStatusCode.InternalServerError &&
                !string.IsNullOrWhiteSpace(response.ErrorMessage) &&
                response.ErrorMessage.Contains(
                    "System.DBNull",
                    StringComparison.OrdinalIgnoreCase);

            if (isDbNullServerBug)
            {
                if (!isUpdate)
                {
                    var recovered = await RecoverCreatedRecordAfterDbNullAsync(
                        payload,
                        preCreateIds,
                        cancellationToken);

                    if (recovered is not null)
                    {
                        Console.WriteLine(
                            $"[CustomerFollowUp] CREATE RECOVERED AFTER SERVER 500 | " +
                            $"Customer={payload.CustomerID} | Record={recovered.RecordId}");

                        return ApiCallResult<FollowUpRecordDTO>.Ok(
                            HttpStatusCode.OK,
                            recovered);
                    }
                }
                else
                {
                    var verifyUpdate = await followUpAC.LoadRecordAsync(
                        payload.CustomerVisitNoteID,
                        cancellationToken);

                    if (verifyUpdate.Success &&
                        verifyUpdate.Value is not null &&
                        string.Equals(
                            verifyUpdate.Value.RtfMessage,
                            payload.RtfMessage,
                            StringComparison.Ordinal))
                    {
                        Console.WriteLine(
                            $"[CustomerFollowUp] UPDATE RECOVERED AFTER SERVER 500 | " +
                            $"Customer={payload.CustomerID} | Record={payload.CustomerVisitNoteID}");

                        return ApiCallResult<FollowUpRecordDTO>.Ok(
                            HttpStatusCode.OK,
                            ToFollowUpRecord(verifyUpdate.Value, payload.CustomerID));
                    }
                }

                return ApiCallResult<FollowUpRecordDTO>.Failure(
                    response.StatusCode,
                    "CustomerFollowUp API returned a server-side DBNull mapping error and the save could not be verified. The backend API needs to handle nullable database string columns.");
            }

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

            // Some CustomerFollowUp deployments return only HTTP 200 for CreateRecord.
            // Resolve the generated ID from the customer proxy so Edit/Delete immediately
            // operate on the actual backend record.
            if (string.IsNullOrWhiteSpace(record.RecordId))
            {
                var reload = await LoadByCustomerAsync(
                    record.CustomerId,
                    cancellationToken);

                if (reload.Success && reload.Value is not null)
                {
                    var match = reload.Value
                        .Where(item =>
                            string.Equals(
                                item.Content,
                                record.Content,
                                StringComparison.Ordinal))
                        .OrderByDescending(item => item.Date)
                        .FirstOrDefault();

                    if (match is not null)
                    {
                        record.RecordId = match.RecordId;
                        record.Date = match.Date;
                        record.BranchId = match.BranchId;
                        record.GroupId = match.GroupId;
                        record.Symptoms = match.Symptoms;
                        record.Diagnoses = match.Diagnoses;
                    }
                }
            }
        }

        Console.WriteLine(
            $"[CustomerFollowUp] {(isUpdate ? "UPDATE" : "CREATE")} PASS | " +
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
            row.ModifiedDateTime != default &&
            row.ModifiedDateTime != DateTime.MinValue
                ? row.ModifiedDateTime
                : row.CreatedDateTime != default &&
                  row.CreatedDateTime != DateTime.MinValue
                    ? row.CreatedDateTime
                    : row.FinancialDate != default &&
                      row.FinancialDate != DateTime.MinValue
                        ? row.FinancialDate
                        : DateTime.Now;

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

    private async Task<FollowUpRecordDTO?> RecoverCreatedRecordAfterDbNullAsync(
        CustomerFollowUpDTO payload,
        HashSet<string> preCreateIds,
        CancellationToken cancellationToken)
    {
        var reload = await followUpAC.LoadByCustomerAsync(
            payload.CustomerID,
            cancellationToken);

        if (!reload.Success || reload.Value is null)
        {
            Console.WriteLine(
                $"[CustomerFollowUp] CREATE RECOVERY LOAD FAILED | Customer={payload.CustomerID} | " +
                $"Status={(int)reload.StatusCode} | Error={reload.ErrorMessage}");
            return null;
        }

        var match = reload.Value
            .Where(item =>
                !string.IsNullOrWhiteSpace(item.CustomerVisitNoteID) &&
                !preCreateIds.Contains(item.CustomerVisitNoteID) &&
                string.Equals(item.RtfMessage, payload.RtfMessage, StringComparison.Ordinal) &&
                string.Equals(item.CustomerID, payload.CustomerID, StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(ResolveRecordDate)
            .FirstOrDefault();

        if (match is null)
        {
            Console.WriteLine(
                $"[CustomerFollowUp] CREATE RECOVERY NOT FOUND | Customer={payload.CustomerID} | " +
                $"BeforeCount={preCreateIds.Count} | ReloadCount={reload.Value.Count}");
            return null;
        }

        return ToFollowUpRecord(match, payload.CustomerID);
    }

    private static DateTime ResolveRecordDate(CustomerFollowUpDTO row) =>
        row.ModifiedDateTime != default && row.ModifiedDateTime != DateTime.MinValue
            ? row.ModifiedDateTime
            : row.CreatedDateTime != default && row.CreatedDateTime != DateTime.MinValue
                ? row.CreatedDateTime
                : row.FinancialDate != default && row.FinancialDate != DateTime.MinValue
                    ? row.FinancialDate
                    : DateTime.MinValue;

    private string ResolveAuditUser()
    {
        if (!string.IsNullOrWhiteSpace(appState.UserEmail))
        {
            return appState.UserEmail.Trim();
        }

        if (!string.IsNullOrWhiteSpace(appState.CurrentUserJson))
        {
            try
            {
                using var document = JsonDocument.Parse(appState.CurrentUserJson);
                foreach (var key in new[]
                {
                    "email",
                    "userName",
                    "username",
                    "displayName",
                    "name",
                    "employeeID",
                    "employeeId",
                    "userID",
                    "userId",
                    "id"
                })
                {
                    var value = FindStringValue(document.RootElement, key);
                    if (!string.IsNullOrWhiteSpace(value))
                    {
                        return value.Trim();
                    }
                }
            }
            catch (JsonException)
            {
            }
        }

        // CustomerFollowUp backend maps audit columns directly to string and
        // throws when SQL returns DBNull. Always provide a non-empty actor.
        return "POS";
    }

    private static string NonNullBackendText(string? value) =>
        string.IsNullOrWhiteSpace(value) ? "-" : value.Trim();

    private static string? FindStringValue(JsonElement element, string propertyName)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                foreach (var property in element.EnumerateObject())
                {
                    if (string.Equals(property.Name, propertyName, StringComparison.OrdinalIgnoreCase) &&
                        property.Value.ValueKind == JsonValueKind.String &&
                        !string.IsNullOrWhiteSpace(property.Value.GetString()))
                    {
                        return property.Value.GetString();
                    }

                    var nested = FindStringValue(property.Value, propertyName);
                    if (!string.IsNullOrWhiteSpace(nested))
                    {
                        return nested;
                    }
                }
                break;

            case JsonValueKind.Array:
                foreach (var item in element.EnumerateArray())
                {
                    var nested = FindStringValue(item, propertyName);
                    if (!string.IsNullOrWhiteSpace(nested))
                    {
                        return nested;
                    }
                }
                break;
        }

        return null;
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
