using System.Net;
using Beauty_Aesthetics_WebPos.APIClient;   
using Beauty_Aesthetics_WebPos.APIClient.ResultPattern;
using Beauty_Aesthetics_WebPos.Components.ViewModels;
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

    public MemberCreditService(ServiceInventoryAC serviceInventoryAC)
    {
        this.serviceInventoryAC = serviceInventoryAC;
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
                record.eInvoiceClassificationCode ?? string.Empty))
            .OrderBy(memberCredit => memberCredit.Name)
            .ToList();

        return ApiCallResult<IReadOnlyList<MembershipViewModel.MemberCredit>>.Ok(
            result.StatusCode,
            memberCredits);
    }

    public async Task<ApiCallResult<bool>> CreateMemberCreditAsync(
        MembershipViewModel.MemberCredit memberCredit,
        string branchId = "hq",
        CancellationToken cancellationToken = default)
    {
        var normalizedBranchId = NormalizeBranchId(branchId);
        var record = CreateMemberCreditRecord(memberCredit, normalizedBranchId);
        var request = CreatePackageRequest(record, normalizedBranchId, memberCredit.Price);
        var result = await serviceInventoryAC.CreateFullAsync(request, cancellationToken);

        return ToSaveResult(result, "Unable to create member credit.");
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

        var normalizedBranchId = NormalizeBranchId(branchId);
        ApplyMemberCreditValues(loadResult.Value, memberCredit, normalizedBranchId);
        loadResult.Value.SaveAction = EntityState.Changed;
        loadResult.Value.IsDirty = true;

        var request = CreatePackageRequest(loadResult.Value, normalizedBranchId, memberCredit.Price);
        var result = await serviceInventoryAC.UpdateFullAsync(request, cancellationToken);
        return ToSaveResult(result, "Unable to update member credit.");
    }

    public async Task<ApiCallResult<bool>> DeactivateMemberCreditAsync(
        string masterAccountId,
        string branchId = "hq",
        CancellationToken cancellationToken = default)
    {
        var loadResult = await serviceInventoryAC.LoadRecordAsync(masterAccountId, cancellationToken);
        if (!loadResult.Success || loadResult.Value is null)
        {
            return ApiCallResult<bool>.Failure(
                loadResult.StatusCode,
                loadResult.ErrorMessage ?? "Unable to load the member credit.");
        }

        loadResult.Value.AccountStatus = "Inactive";
        loadResult.Value.SaveAction = EntityState.Changed;
        loadResult.Value.IsDirty = true;

        var normalizedBranchId = NormalizeBranchId(branchId);
        var request = CreatePackageRequest(loadResult.Value, normalizedBranchId, loadResult.Value.SalesPrice);
        var result = await serviceInventoryAC.UpdateFullAsync(request, cancellationToken);
        return ToSaveResult(result, "Unable to deactivate member credit.");
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
            ValidityDays = 8888,
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
        record.BranchID = branchId;
        record.AccountStatus = string.IsNullOrWhiteSpace(record.AccountStatus) ? "Active" : record.AccountStatus;
        record.IsSold = true;
    }

    private static InventoryPackageRequestDTO CreatePackageRequest(
        InventoryDM record,
        string branchId,
        decimal price)
    {
        return new InventoryPackageRequestDTO
        {
            ObjInventory = record,
            Branches =
            [
                new InventoryBranchDTO
                {
                    MasterAccountId = record.MasterAccountID,
                    BranchId = branchId,
                    BranchPrice = price,
                    IsEnabled = true,
                    SaveAction = "Added",
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

    private static string? FirstNonEmpty(params string?[] values)
    {
        return values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value));
    }
}

