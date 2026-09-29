using System.Net;
using Beauty_Aesthetics_WebPos.APIClient;   
using Beauty_Aesthetics_WebPos.APIClient.ResultPattern;
using Beauty_Aesthetics_WebPos.Components.ViewModels;
using Beauty_Aesthetics_WebPos.Components.Services.Feedback;
using Beauty_Aesthetics_WebPos.Models.DTOs;
using EBI.DM;
using EBI.Enum;

namespace Beauty_Aesthetics_WebPos.Components.Services.Inventory;

public sealed class MemberCreditService : IMemberCreditService
{
    private const int MemberCreditInventoryTypeId = 7;
    private const string MemberCreditInventoryTypeName = "Member Credit";
    private static readonly DateTime InventoryAvailableFrom = new(2000, 1, 1);
    private static readonly DateTime InventoryAvailableTo = new(2049, 12, 31);

    private readonly ServiceInventoryAC serviceInventoryAC;
    private readonly AppFeedbackService feedback;

    public MemberCreditService(ServiceInventoryAC serviceInventoryAC, AppFeedbackService feedback)
    {
        this.serviceInventoryAC = serviceInventoryAC;
        this.feedback = feedback;
    }

    public async Task<ApiCallResult<IReadOnlyList<MembershipViewModel.MemberCredit>>> LoadMemberCreditsAsync(
        CancellationToken cancellationToken = default)
    {
        var result = await serviceInventoryAC.LoadProxyAsync(null, cancellationToken);
        if (!result.Success || result.Value is null)
        {
            return ApiCallResult<IReadOnlyList<MembershipViewModel.MemberCredit>>.Failure(
                result.StatusCode,
                result.ErrorMessage ?? "Unable to load member credits.");
        }

        var memberCredits = result.Value
            .Where(record =>
                record.InventoryTypeID == MemberCreditInventoryTypeId &&
                !string.Equals(record.AccountStatus, "Inactive", StringComparison.OrdinalIgnoreCase))
            .Select(record => new MembershipViewModel.MemberCredit(
                FirstNonEmpty(record.AccountName, record.SalesDescription) ?? string.Empty,
                FirstNonEmpty(record.DisplayCode, record.MasterAccountID) ?? string.Empty,
                Convert.ToDecimal(record.MemberMainAccountCredit),
                record.SalesPrice,
                record.MasterAccountID,
                FirstNonEmpty(record.AccountStatus, "Active") ?? "Active",
                record.BranchID ?? string.Empty,
                FirstNonEmpty(record.InventoryTypeName, MemberCreditInventoryTypeName) ?? MemberCreditInventoryTypeName,
                record.SalesDescription ?? string.Empty,
                record.ValidityDays,
                Convert.ToDecimal(record.MemberCreditSettlementRatio),
                record.AvailableDateFrom,
                record.AvailableDateTo,
                record.AvailableTimeFrom,
                record.AvailableTimeTo,
                record.eInvoiceClassificationCode ?? string.Empty,
                FirstNonEmpty(
                    GetInventoryString(record, "TriggeredMemberTypeID"),
                    ParseFirstMemberTypeId(GetInventoryString(record, "MembershipCredit"))) ?? string.Empty))
            .OrderBy(memberCredit => memberCredit.Name)
            .ToList();

        return ApiCallResult<IReadOnlyList<MembershipViewModel.MemberCredit>>.Ok(
            result.StatusCode,
            memberCredits);
    }

    public async Task<ApiCallResult<MembershipViewModel.MemberCredit>> LoadMemberCreditAsync(
        string masterAccountId,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(masterAccountId))
        {
            return ApiCallResult<MembershipViewModel.MemberCredit>.Failure(
                HttpStatusCode.BadRequest,
                "The selected member credit has no record ID.");
        }

        var baseResult = await serviceInventoryAC.LoadRecordAsync(masterAccountId.Trim(), cancellationToken);
        if (!baseResult.Success || baseResult.Value is null)
        {
            return ApiCallResult<MembershipViewModel.MemberCredit>.Failure(
                baseResult.StatusCode,
                baseResult.ErrorMessage ?? "Unable to load the member credit record.");
        }

        var fullResult = await serviceInventoryAC.LoadFullAsync(masterAccountId.Trim(), cancellationToken);
        if (!fullResult.Success || fullResult.Value is null)
        {
            return ApiCallResult<MembershipViewModel.MemberCredit>.Failure(
                fullResult.StatusCode,
                fullResult.ErrorMessage ?? "Unable to load the full member credit configuration.");
        }

        var record = baseResult.Value;
        var full = fullResult.Value;
        var fullInventory = full.ObjInventory;

        var configuredCredits = (full.MembershipCredits ?? fullInventory?.MembershipCredits ?? [])
            .Where(item =>
                !string.IsNullOrWhiteSpace(item.MemberTypeId) &&
                !string.Equals(item.SaveAction, "Deleted", StringComparison.OrdinalIgnoreCase))
            .ToList();

        var primaryCredit = configuredCredits.FirstOrDefault();
        var memberTypeId = FirstNonEmpty(
            primaryCredit?.MemberTypeId,
            fullInventory?.TriggeredMemberTypeId,
            GetInventoryString(record, "TriggeredMemberTypeID"),
            ParseFirstMemberTypeId(GetInventoryString(record, "MembershipCredit"))) ?? string.Empty;

        var creditValue = primaryCredit is not null
            ? Math.Max(0m, primaryCredit.MemberCredit)
            : Math.Max(
                0m,
                fullInventory?.MemberMainAccountCredit
                    ?? Convert.ToDecimal(record.MemberMainAccountCredit));

        var expiryDays = fullInventory is not null && fullInventory.MemberExpiryDays > 0
            ? fullInventory.MemberExpiryDays
            : fullInventory is not null
                ? Math.Max(0, fullInventory.ValidityDays)
                : Math.Max(0, record.ValidityDays);

        return ApiCallResult<MembershipViewModel.MemberCredit>.Ok(
            baseResult.StatusCode,
            new MembershipViewModel.MemberCredit(
                FirstNonEmpty(fullInventory?.AccountName, record.AccountName, record.SalesDescription) ?? string.Empty,
                FirstNonEmpty(fullInventory?.DisplayCode, record.DisplayCode, record.MasterAccountID) ?? string.Empty,
                creditValue,
                fullInventory?.SalesPrice ?? record.SalesPrice,
                record.MasterAccountID,
                FirstNonEmpty(fullInventory?.AccountStatus, record.AccountStatus, "Active") ?? "Active",
                FirstNonEmpty(fullInventory?.BranchId, record.BranchID) ?? string.Empty,
                FirstNonEmpty(record.InventoryTypeName, MemberCreditInventoryTypeName) ?? MemberCreditInventoryTypeName,
                FirstNonEmpty(fullInventory?.SalesDescription, record.SalesDescription) ?? string.Empty,
                expiryDays,
                Convert.ToDecimal(record.MemberCreditSettlementRatio),
                record.AvailableDateFrom,
                record.AvailableDateTo,
                record.AvailableTimeFrom,
                record.AvailableTimeTo,
                record.eInvoiceClassificationCode ?? string.Empty,
                memberTypeId));
    }

    public async Task<ApiCallResult<bool>> CreateMemberCreditAsync(
        MembershipViewModel.MemberCredit memberCredit,
        string branchId = "hq",
        CancellationToken cancellationToken = default)
    {
        var normalizedBranchId = NormalizeBranchId(branchId);
        var record = CreateMemberCreditRecord(memberCredit, normalizedBranchId);
        var request = CreatePackageRequest(
            record,
            normalizedBranchId,
            memberCredit,
            isUpdate: false,
            existingCredits: null);
        var result = ToSaveResult(
            await serviceInventoryAC.CreateFullAsync(request, cancellationToken),
            "Unable to create member credit.");

        if (result.Success)
            feedback.Success("Member credit created successfully.", "Member credit created");
        else
            feedback.Error(result.ErrorMessage ?? "Unable to create member credit.", "Member credit not created");

        return result;
    }

    public async Task<ApiCallResult<bool>> UpdateMemberCreditAsync(
        MembershipViewModel.MemberCredit memberCredit,
        string branchId = "hq",
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(memberCredit.MasterAccountId))
        {
            return ApiCallResult<bool>.Failure(
                HttpStatusCode.BadRequest,
                "The selected member credit has no record ID.");
        }

        var loadResult = await serviceInventoryAC.LoadRecordAsync(memberCredit.MasterAccountId, cancellationToken);
        if (!loadResult.Success || loadResult.Value is null)
        {
            return ApiCallResult<bool>.Failure(
                loadResult.StatusCode,
                loadResult.ErrorMessage ?? "Unable to load the member credit before updating it.");
        }

        var fullLoadResult = await serviceInventoryAC.LoadFullAsync(memberCredit.MasterAccountId, cancellationToken);
        if (!fullLoadResult.Success || fullLoadResult.Value is null)
        {
            return ApiCallResult<bool>.Failure(
                fullLoadResult.StatusCode,
                fullLoadResult.ErrorMessage ?? "Unable to load the full member credit before updating it.");
        }

        var normalizedBranchId = NormalizeBranchId(branchId);
        ApplyMemberCreditValues(loadResult.Value, memberCredit, normalizedBranchId);
        loadResult.Value.SaveAction = EntityState.Changed;
        loadResult.Value.IsDirty = true;

        var existingCredits = fullLoadResult.Value.MembershipCredits
            ?? fullLoadResult.Value.ObjInventory?.MembershipCredits
            ?? [];

        var request = CreatePackageRequest(
            loadResult.Value,
            normalizedBranchId,
            memberCredit,
            isUpdate: true,
            existingCredits);
        var result = ToSaveResult(
            await serviceInventoryAC.UpdateFullAsync(request, cancellationToken),
            "Unable to update member credit.");

        if (result.Success)
            feedback.Success("Member credit updated successfully.", "Member credit updated");
        else
            feedback.Error(result.ErrorMessage ?? "Unable to update member credit.", "Member credit not updated");

        return result;
    }

    public async Task<ApiCallResult<bool>> DeleteMemberCreditAsync(
        string masterAccountId,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(masterAccountId))
        {
            return ApiCallResult<bool>.Failure(
                HttpStatusCode.BadRequest,
                "The selected member credit has no record ID.");
        }

        var result = await serviceInventoryAC.DeleteSimpleAsync(masterAccountId.Trim(), cancellationToken);

        if (result.Success)
            feedback.Success("Member credit deleted successfully.", "Member credit deleted");
        else
            feedback.Error(result.ErrorMessage ?? "Unable to delete member credit.", "Member credit not deleted");

        return result;
    }

    private static InventoryDM CreateMemberCreditRecord(
        MembershipViewModel.MemberCredit memberCredit,
        string branchId)
    {
        var record = new InventoryDM
        {
            AccountTypeID = 4,
            AccountStatus = "Active",
            IsSold = true,
            IsPurchased = false,
            CreatedDateTime = DateTime.Now,
            AvailableDateFrom = InventoryAvailableFrom,
            AvailableDateTo = InventoryAvailableTo,
            AvailableTimeFrom = TimeSpan.Zero,
            AvailableTimeTo = new TimeSpan(23, 59, 59),
            QuantityFactor = 1,
            UnitOfMeasureID = "UNIT",
            ValidityDays = 0,
            MemberCreditSettlementRatio = 1,
            KitchenCopies = 1,
            UOMBase = 1,
            ReportingUOMBase = 1,
            DefaultDosage = 1,
            eInvoiceClassificationCode = "022"
        };

        ApplyMemberCreditValues(record, memberCredit, branchId);
        record.SaveAction = EntityState.Added;
        record.IsDirty = true;
        return record;
    }

    private static void ApplyMemberCreditValues(
        InventoryDM record,
        MembershipViewModel.MemberCredit memberCredit,
        string branchId)
    {
        record.InventoryTypeID = MemberCreditInventoryTypeId;
        record.InventoryTypeName = MemberCreditInventoryTypeName;
        record.AccountName = memberCredit.Name.Trim();
        record.SalesDescription = memberCredit.Name.Trim();
        record.DisplayCode = memberCredit.Code.Trim();
        record.MemberMainAccountCredit = Math.Max(0, memberCredit.CreditValue);
        record.SalesPrice = Math.Max(0, memberCredit.Price);
        record.ValidityDays = Math.Max(0, memberCredit.ValidityDays);
        record.MemberCreditSettlementRatio = memberCredit.SettlementRatio > 0m
            ? memberCredit.SettlementRatio
            : 1m;

        var memberTypeId = memberCredit.MemberTypeId?.Trim() ?? string.Empty;
        SetInventoryProperty(record, "MemberExpiryDays", Math.Max(0, memberCredit.ValidityDays));
        SetInventoryProperty(record, "TriggeredMemberTypeID", memberTypeId);
        SetInventoryProperty(
            record,
            "MembershipCredit",
            string.IsNullOrWhiteSpace(memberTypeId)
                ? string.Empty
                : $"{memberTypeId},{Math.Max(0m, memberCredit.CreditValue):0.00}");

        record.BranchID = branchId;
        record.AccountStatus = string.IsNullOrWhiteSpace(record.AccountStatus) ? "Active" : record.AccountStatus;
        record.IsSold = true;
    }

    private static InventoryPackageRequestDTO CreatePackageRequest(
        InventoryDM record,
        string branchId,
        MembershipViewModel.MemberCredit memberCredit,
        bool isUpdate,
        IReadOnlyCollection<InventoryMembershipCreditDTO>? existingCredits)
    {
        var memberTypeId = memberCredit.MemberTypeId?.Trim() ?? string.Empty;
        var credits = new List<InventoryMembershipCreditDTO>();

        if (isUpdate)
        {
            foreach (var existing in existingCredits ?? [])
            {
                if (string.IsNullOrWhiteSpace(existing.MemberTypeId))
                {
                    continue;
                }

                if (!string.Equals(existing.MemberTypeId, memberTypeId, StringComparison.OrdinalIgnoreCase))
                {
                    credits.Add(new InventoryMembershipCreditDTO
                    {
                        MemberTypeId = existing.MemberTypeId.Trim(),
                        MemberCredit = Math.Max(0m, existing.MemberCredit),
                        SaveAction = "Deleted",
                        IsDirty = true
                    });
                }
            }
        }

        if (!string.IsNullOrWhiteSpace(memberTypeId))
        {
            var existed = (existingCredits ?? []).Any(existing =>
                string.Equals(existing.MemberTypeId, memberTypeId, StringComparison.OrdinalIgnoreCase));

            credits.Add(new InventoryMembershipCreditDTO
            {
                MemberTypeId = memberTypeId,
                MemberCredit = Math.Max(0m, memberCredit.CreditValue),
                SaveAction = isUpdate && existed ? "Changed" : "Added",
                IsDirty = true
            });
        }

        return new InventoryPackageRequestDTO
        {
            ObjInventory = record,
            MembershipCredits = credits,
            Branches =
            [
                new InventoryBranchDTO
                {
                    MasterAccountId = record.MasterAccountID,
                    BranchId = branchId,
                    BranchPrice = Math.Max(0m, memberCredit.Price),
                    IsEnabled = true,
                    SaveAction = isUpdate ? "Changed" : "Added",
                    IsDirty = true
                }
            ]
        };
    }

    private static string NormalizeBranchId(string branchId)
    {
        return string.IsNullOrWhiteSpace(branchId)
            ? "HQ"
            : branchId.Trim().ToUpperInvariant();
    }

    private static ApiCallResult<bool> ToSaveResult(
        ApiCallResult<InventorySaveResultDTO> result,
        string fallbackMessage)
    {
        return result.Success
            ? ApiCallResult<bool>.Ok(result.StatusCode, true)
            : ApiCallResult<bool>.Failure(result.StatusCode, result.ErrorMessage ?? fallbackMessage);
    }

    private static string? ParseFirstMemberTypeId(string? membershipCredit)
    {
        if (string.IsNullOrWhiteSpace(membershipCredit))
        {
            return null;
        }

        var firstRow = membershipCredit
            .Split('|', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .FirstOrDefault();

        if (string.IsNullOrWhiteSpace(firstRow))
        {
            return null;
        }

        return firstRow
            .Split(',', StringSplitOptions.TrimEntries)
            .FirstOrDefault();
    }

    private static string? GetInventoryString(object record, string propertyName)
    {
        var value = record.GetType().GetProperty(propertyName)?.GetValue(record);
        return value?.ToString();
    }

    private static void SetInventoryProperty(InventoryDM record, string propertyName, object? value)
    {
        var property = record.GetType().GetProperty(propertyName);
        if (property is null || !property.CanWrite)
        {
            return;
        }

        if (value is null)
        {
            property.SetValue(record, null);
            return;
        }

        var targetType = Nullable.GetUnderlyingType(property.PropertyType) ?? property.PropertyType;
        var converted = targetType.IsInstanceOfType(value)
            ? value
            : Convert.ChangeType(value, targetType);
        property.SetValue(record, converted);
    }

    private static string? FirstNonEmpty(params string?[] values)
    {
        return values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value));
    }
}

