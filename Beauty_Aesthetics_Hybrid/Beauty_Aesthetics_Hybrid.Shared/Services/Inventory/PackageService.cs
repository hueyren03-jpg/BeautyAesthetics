using System.Net;
using Beauty_Aesthetics_WebPos.APIClient;
using Beauty_Aesthetics_WebPos.APIClient.ResultPattern;
using Beauty_Aesthetics_WebPos.Components.ViewModels;
using Beauty_Aesthetics_WebPos.Components.Services.Feedback;
using Beauty_Aesthetics_WebPos.Models.DTOs;
using EBI.DM;
using EBI.Enum;

namespace Beauty_Aesthetics_WebPos.Components.Services.Inventory;

public sealed class PackageService : IPackageService
{
    private const int ServiceInventoryTypeId = 3;
    private const int PackageInventoryTypeId = 5;
    private const string PackageInventoryTypeName = "Package";
    private static readonly DateTime InventoryAvailableFrom = new(2000, 1, 1);
    private static readonly DateTime InventoryAvailableTo = new(2049, 12, 31);

    private readonly ServiceInventoryAC serviceInventoryAC;
    private readonly AppFeedbackService feedback;

    public PackageService(ServiceInventoryAC serviceInventoryAC, AppFeedbackService feedback)
    {
        this.serviceInventoryAC = serviceInventoryAC;
        this.feedback = feedback;
    }

    public async Task<ApiCallResult<IReadOnlyList<InventoryPackageSummary>>> LoadPackagesAsync(
        IReadOnlyCollection<ServiceViewModel.ServiceItem> services,
        string branchId = "hq",
        CancellationToken cancellationToken = default)
    {
        var normalizedBranchId = NormalizeBranchId(branchId);
        var result = await serviceInventoryAC.LoadProxyAsync(normalizedBranchId, cancellationToken);

        if (!result.Success || result.Value is null)
        {
            return ApiCallResult<IReadOnlyList<InventoryPackageSummary>>.Failure(
                result.StatusCode,
                result.ErrorMessage ?? "Unable to load package records.");
        }

        var packageHeaders = result.Value
            .Where(record => record.InventoryTypeID == PackageInventoryTypeId)
            .ToList();

        Console.WriteLine(
            $"[Sales Catalog] PACKAGE | Branch={normalizedBranchId} | Records={result.Value.Count} | Packages={packageHeaders.Count}");

        return await LoadPackageSummariesAsync(
            packageHeaders, services, result.StatusCode, cancellationToken);
    }

    public async Task<ApiCallResult<InventoryPackageSummary>> LoadPackageAsync(
        string masterAccountId,
        IReadOnlyCollection<ServiceViewModel.ServiceItem> services,
        CancellationToken cancellationToken = default)
    {
        var header = await serviceInventoryAC.LoadRecordAsync(masterAccountId, cancellationToken);
        if (!header.Success || header.Value is null)
        {
            return ApiCallResult<InventoryPackageSummary>.Failure(
                header.StatusCode,
                header.ErrorMessage ?? "Unable to refresh the package record.");
        }

        var result = await LoadPackageSummariesAsync(
            [header.Value], services, header.StatusCode, cancellationToken, requireFullRecord: true);
        var package = result.Value?.FirstOrDefault();
        return result.Success && package is not null
            ? ApiCallResult<InventoryPackageSummary>.Ok(result.StatusCode, package)
            : ApiCallResult<InventoryPackageSummary>.Failure(
                result.StatusCode, result.ErrorMessage ?? "Unable to refresh the full package record.");
    }

    private async Task<ApiCallResult<IReadOnlyList<InventoryPackageSummary>>> LoadPackageSummariesAsync(
        IReadOnlyCollection<InventoryDM> packageHeaders,
        IReadOnlyCollection<ServiceViewModel.ServiceItem> services,
        HttpStatusCode statusCode,
        CancellationToken cancellationToken,
        bool requireFullRecord = false)
    {
        var serviceById = services
            .Where(service => !string.IsNullOrWhiteSpace(service.MasterAccountId))
            .GroupBy(service => service.MasterAccountId!, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.OrdinalIgnoreCase);

        var summaries = new List<InventoryPackageSummary>(packageHeaders.Count);
        foreach (var header in packageHeaders)
        {
            var fullRecord = !string.IsNullOrWhiteSpace(header.MasterAccountID)
                ? await serviceInventoryAC.LoadFullAsync(header.MasterAccountID, cancellationToken)
                : null;

            if (requireFullRecord && (fullRecord?.Success != true || fullRecord.Value is null))
            {
                return ApiCallResult<IReadOnlyList<InventoryPackageSummary>>.Failure(
                    fullRecord?.StatusCode ?? HttpStatusCode.NotFound,
                    fullRecord?.ErrorMessage ?? "Unable to refresh the full package record.");
            }

            var package = fullRecord?.Success == true
                ? fullRecord.Value?.ObjInventory
                : null;
            var packageLines = (package?.PackageLines
                ?? fullRecord?.Value?.PackageLines
                ?? [])
                .Where(line => !line.IsVoided)
                .ToList();
            var membershipCredits = InventoryMembershipCreditMapper.Read(
                fullRecord?.Value, header, allowLegacyPrimaryCredit: false);
            var serviceIds = packageLines
                .Where(line => !string.IsNullOrWhiteSpace(line.InventoryId))
                .Select(line => line.InventoryId!)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            var lineSummaries = packageLines
                .Where(line => !string.IsNullOrWhiteSpace(line.InventoryId))
                .Select(line => new InventoryPackageLineSummary(
                    line.InventoryId!,
                    line.Description ?? string.Empty,
                    line.Quantity,
                    line.UnitPrice,
                    line.IsDeferred,
                    line.InventoryTypeId,
                    line.AutoId.ValueKind is System.Text.Json.JsonValueKind.Null
                        or System.Text.Json.JsonValueKind.Undefined
                        ? null
                        : line.AutoId.ToString(),
                    line.PackageId))
                .ToList();

            var totalDuration = packageLines
                .Where(line => line.InventoryTypeId == ServiceInventoryTypeId && !string.IsNullOrWhiteSpace(line.InventoryId))
                .Select(line => line.InventoryId!)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Sum(id => serviceById.TryGetValue(id, out var service)
                    ? ParseDuration(service.DurationSpend)
                    : 0);

            var packagePoints = package is not null
                ? GetInventoryDecimal(package, "Points")
                : 0m;
            if (packagePoints == 0m)
            {
                packagePoints = GetInventoryDecimal(header, "Points");
            }

            var packageRemarks = package?.Remarks;
            if (string.IsNullOrWhiteSpace(packageRemarks))
            {
                packageRemarks = GetInventoryString(header, "Remarks");
            }

            summaries.Add(new InventoryPackageSummary(
                package?.MasterAccountId ?? header.MasterAccountID ?? string.Empty,
                FirstNonEmpty(package?.AccountName, package?.SalesDescription, header.AccountName, header.SalesDescription)
                    ?? string.Empty,
                FirstNonEmpty(
                    package?.DisplayCode,
                    package?.VendorItemCode,
                    header.DisplayCode,
                    header.VendorItemCode,
                    package?.MasterAccountId,
                    header.MasterAccountID) ?? string.Empty,
                packageLines.Count,
                totalDuration,
                package?.SalesPrice ?? header.SalesPrice,
                serviceIds,
                FirstNonEmpty(package?.AccountStatus, header.AccountStatus, "Active") ?? "Active",
                package?.BranchId ?? header.BranchID ?? string.Empty,
                FirstNonEmpty(header.InventoryTypeName, PackageInventoryTypeName) ?? PackageInventoryTypeName,
                package?.SalesDescription ?? header.SalesDescription ?? string.Empty,
                header.AvailableDateFrom,
                header.AvailableDateTo,
                header.AvailableTimeFrom,
                header.AvailableTimeTo,
                header.eInvoiceClassificationCode ?? string.Empty,
                package?.ImagePath ?? header.ImagePath ?? string.Empty,
                package?.ImageFileName ?? header.ImageFileName ?? string.Empty,
                FirstNonEmpty(package?.ItemGroupName, header.ItemGroupName) ?? string.Empty,
                string.Equals(
                    FirstNonEmpty(package?.AccountStatus, header.AccountStatus, "Active"),
                    "Active",
                    StringComparison.OrdinalIgnoreCase),
                FirstNonEmpty(package?.UnitOfMeasureId, header.UnitOfMeasureName, header.UnitOfMeasureID, "unit") ?? "unit",
                package?.ValidityDays ?? header.ValidityDays,
                InventoryMembershipCreditMapper.ReadExpiryDays(fullRecord?.Value, header),
                FirstNonEmpty(
                    package?.TriggeredMemberTypeId,
                    GetInventoryString(header, "TriggeredMemberTypeID")) ?? string.Empty,
                package?.MemberMainAccountCredit ?? GetInventoryDecimal(header, "MemberMainAccountCredit"),
                lineSummaries,
                packagePoints,
                GetPackagePolicy(
                    package?.SalesDescription ?? header.SalesDescription,
                    package?.AccountName ?? header.AccountName),
                GetPackageTerm(packageRemarks, 0),
                GetPackageTerm(packageRemarks, 1),
                GetPackageTerm(packageRemarks, 2),
                GetPackagePriceLimit(packageRemarks, "MIN_PRICE"),
                GetPackagePriceLimit(packageRemarks, "MAX_PRICE"),
                package?.PurchasePrice ?? header.PurchasePrice,
                package?.TaxCodeId ?? header.TaxCodeID ?? string.Empty,
                package is not null ? package.IsTaxInclusive : header.IsTaxInclusive,
                membershipCredits,
                FirstNonEmpty(package?.ItemGroupId, GetInventoryString(header, "ItemGroupID")) ?? string.Empty,
                fullRecord?.Value?.Branches?.Where(branch => !string.IsNullOrWhiteSpace(branch.BranchId))
                    .Select(branch => new InventoryPackageBranchEdit(
                        branch.BranchId!, branch.GroupId ?? string.Empty, branch.IsEnabled)).ToList()));
        }

        return ApiCallResult<IReadOnlyList<InventoryPackageSummary>>.Ok(
            statusCode,
            summaries.OrderBy(package => package.Name).ToList());
    }

    public async Task<ApiCallResult<InventoryPackageSaveOutcome>> CreatePackageAsync(
        InventoryPackageEdit package,
        string branchId = "hq",
        CancellationToken cancellationToken = default,
        string branchGroupId = "")
    {
        var normalizedBranchId = NormalizeBranchId(branchId);
        var record = CreatePackageRecord(package, normalizedBranchId);
        var request = CreatePackageRequest(
            record,
            normalizedBranchId,
            package.Price,
            NormalizeBranchGroupId(branchGroupId, normalizedBranchId),
            package.MembershipCredits,
            isUpdate: false,
            branches: package.Branches);

        // Senang creates the header first, then saves its items against the returned ID.
        var lines = record.lstPackage.ToList();
        record.lstPackage.Clear();
        var headerResult = await serviceInventoryAC.CreateFullAsync(request, cancellationToken);
        if (!headerResult.Success)
        {
            return ApiCallResult<InventoryPackageSaveOutcome>.Failure(
                headerResult.StatusCode, headerResult.ErrorMessage ?? "Unable to create package.");
        }

        var recordId = headerResult.Value?.Id;
        if (string.IsNullOrWhiteSpace(recordId))
        {
            return ApiCallResult<InventoryPackageSaveOutcome>.Ok(headerResult.StatusCode,
                new(null, false, "Package header saved without a returned ID. Reload packages before editing it."));
        }

        if (lines.Count > 0)
        {
            record.MasterAccountID = recordId;
            record.SaveAction = EntityState.Changed;
            record.IsDirty = true;
            foreach (var line in lines)
            {
                line.PackageID = recordId;
                record.lstPackage.Add(line);
            }
            request.Branches.Clear();
            try
            {
                var itemResult = await serviceInventoryAC.CreateFullAsync(request, cancellationToken);
                if (!itemResult.Success)
                {
                    return ApiCallResult<InventoryPackageSaveOutcome>.Ok(itemResult.StatusCode,
                        new(recordId, false, itemResult.ErrorMessage ?? "Package items could not be saved. Retry Save."));
                }
            }
            catch (Exception ex)
            {
                // Never retry header creation after its ID has been allocated.
                return ApiCallResult<InventoryPackageSaveOutcome>.Ok(headerResult.StatusCode,
                    new(recordId, false, $"Package items could not be confirmed. Retry Save. {ex.Message}"));
            }
        }

        feedback.Success("Package created successfully.", "Package created");
        return ApiCallResult<InventoryPackageSaveOutcome>.Ok(headerResult.StatusCode, new(recordId, true));
    }

    public async Task<ApiCallResult<InventoryPackageSaveOutcome>> UpdatePackageAsync(
        InventoryPackageEdit package,
        string branchId = "hq",
        CancellationToken cancellationToken = default,
        string branchGroupId = "")
    {
        if (string.IsNullOrWhiteSpace(package.MasterAccountId))
        {
            return ApiCallResult<InventoryPackageSaveOutcome>.Failure(
                HttpStatusCode.BadRequest,
                "The selected package has no record ID.");
        }

        var loadResult = await serviceInventoryAC.LoadFullAsync(package.MasterAccountId, cancellationToken);
        if (!loadResult.Success || loadResult.Value is null)
        {
            return ApiCallResult<InventoryPackageSaveOutcome>.Failure(
                loadResult.StatusCode,
                loadResult.ErrorMessage ?? "Unable to load the package before updating it.");
        }

        var normalizedBranchId = NormalizeBranchId(branchId);
        var loadedPackage = loadResult.Value.ObjInventory;
        var loadedLines = loadedPackage?.PackageLines
            ?? loadResult.Value.PackageLines
            ?? [];

        var record = CreatePackageRecord(package, normalizedBranchId);
        record.MasterAccountID = package.MasterAccountId;
        record.AccountStatus = string.IsNullOrWhiteSpace(loadedPackage?.AccountStatus)
            ? "Active"
            : loadedPackage.AccountStatus;
        record.BranchID = FirstNonEmpty(loadedPackage?.BranchId, normalizedBranchId);
        // Preserve inventory metadata that is not editable in the package form.
        record.VendorItemCode = loadedPackage?.VendorItemCode;
        record.UnitOfMeasureID = FirstNonEmpty(loadedPackage?.UnitOfMeasureId, record.UnitOfMeasureID);
        record.UnitOfMeasureName = record.UnitOfMeasureID;
        SetInventoryProperty(record, "TriggeredMemberTypeID", loadedPackage?.TriggeredMemberTypeId ?? string.Empty);
        SetInventoryProperty(record, "MemberMainAccountCredit", loadedPackage?.MemberMainAccountCredit ?? 0m);
        record.lstPackage.Clear();

        foreach (var loadedLine in loadedLines)
        {
            record.lstPackage.Add(ToPackageLine(loadedLine));
        }

        ApplyPackageValues(record, package, normalizedBranchId, isUpdate: true);

        var request = CreatePackageRequest(
            record,
            normalizedBranchId,
            package.Price,
            NormalizeBranchGroupId(branchGroupId, normalizedBranchId),
            package.MembershipCredits,
            isUpdate: true,
            branches: package.Branches,
            existingBranches: loadResult.Value.Branches);

        var result = ToSaveResult(
            await serviceInventoryAC.UpdateFullAsync(request, cancellationToken),
            "Unable to update package.");

        if (result.Success)
            feedback.Success("Package updated successfully.", "Package updated");
        else
            feedback.Error(result.ErrorMessage ?? "Unable to update package.", "Package not updated");

        return result.Success
            ? ApiCallResult<InventoryPackageSaveOutcome>.Ok(result.StatusCode, new(package.MasterAccountId, true))
            : ApiCallResult<InventoryPackageSaveOutcome>.Failure(result.StatusCode, result.ErrorMessage ?? "Unable to update package.");
    }

    public async Task<ApiCallResult<bool>> DeletePackageAsync(
        string masterAccountId,
        CancellationToken cancellationToken = default)
    {
        var result = await serviceInventoryAC.DeleteFullAsync(masterAccountId, cancellationToken);
        if (result.Success)
            feedback.Success("Package deleted successfully.", "Package deleted");
        else
            feedback.Error(result.ErrorMessage ?? "Unable to delete package.", "Package not deleted");
        return result;
    }

    private static InventoryDM CreatePackageRecord(
        InventoryPackageEdit package,
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
            UnitOfMeasureID = "unit",
            UnitOfMeasureName = "unit",
            ValidityDays = 8888,
            MemberCreditSettlementRatio = 1,
            KitchenCopies = 1,
            UOMBase = 1,
            ReportingUOMBase = 1,
            DefaultDosage = 1,
            eInvoiceClassificationCode = "022"
        };

        ApplyPackageValues(record, package, branchId, isUpdate: false);
        return record;
    }

    private static void ApplyPackageValues(
        InventoryDM record,
        InventoryPackageEdit package,
        string branchId,
        bool isUpdate)
    {
        record.InventoryTypeID = PackageInventoryTypeId;
        record.InventoryTypeName = PackageInventoryTypeName;
        record.AccountName = package.Name.Trim();
        record.SalesDescription = string.IsNullOrWhiteSpace(package.Policy)
            ? package.Name.Trim()
            : package.Policy.Trim();
        record.DisplayCode = package.Sku.Trim();
        record.ItemGroupName = package.Section?.Trim() ?? string.Empty;
        SetInventoryProperty(record, "ItemGroupID", package.ItemGroupId?.Trim() ?? string.Empty);
        record.SalesPrice = Math.Max(0, package.Price);
        record.PurchasePrice = Math.Max(0, package.Cost);
        record.TaxCodeID = package.TaxCode?.Trim() ?? string.Empty;
        record.IsTaxInclusive = package.IsTaxInclusive;
        record.ImagePath = string.IsNullOrWhiteSpace(package.ImagePath)
            ? null
            : package.ImagePath.Trim();
        record.ImageFileName = string.IsNullOrWhiteSpace(package.ImageFileName)
            ? null
            : package.ImageFileName.Trim();
        record.ValidityDays = Math.Max(0, package.ValidityDays);
        SetInventoryProperty(record, "MemberExpiryDays", Math.Max(0, package.MemberExpiryDays));
        if (!isUpdate)
        {
            SetInventoryProperty(record, "TriggeredMemberTypeID", package.TriggeredMemberTypeId?.Trim() ?? string.Empty);
            SetInventoryProperty(record, "MemberMainAccountCredit", Math.Max(0, package.MemberMainAccountCredit));
        }
        SetInventoryProperty(record, "Points", Math.Max(0m, package.Points));
        SetInventoryProperty(
            record,
            "Remarks",
            EncodePackageEditorRemarks(
                Math.Max(0m, package.MinPrice),
                Math.Max(0m, package.MaxPrice),
                package.TermCondition1,
                package.TermCondition2,
                package.TermCondition3));
        record.BranchID = branchId;
        record.HasPackage = true;
        record.AccountStatus = package.IsActive ? "Active" : "Inactive";
        record.IsSold = true;
        record.SaveAction = isUpdate ? EntityState.Changed : EntityState.Added;
        record.IsDirty = true;

        var existingLines = record.lstPackage?.ToList() ?? new List<Inventory_PackageItemDM>();

        var editedLines = package.Lines?.Where(line => !string.IsNullOrWhiteSpace(line.InventoryId)).ToList()
            ?? package.Services
                .Where(service => !string.IsNullOrWhiteSpace(service.MasterAccountId))
                .Select(service => new InventoryPackageLineEdit(
                    service.MasterAccountId!,
                    service.ServiceName,
                    ServiceInventoryTypeId,
                    1m,
                    Math.Max(0m, service.Price),
                    false))
                .ToList();

        var retainedLines = new HashSet<Inventory_PackageItemDM>();

        record.lstPackage ??= new System.Collections.ObjectModel.ObservableCollection<Inventory_PackageItemDM>();
        record.lstPackage.Clear();

        foreach (var lineEdit in editedLines)
        {
            var existing = string.IsNullOrWhiteSpace(lineEdit.AutoId) ? null : existingLines.FirstOrDefault(line =>
                string.Equals(line.AutoID?.ToString(), lineEdit.AutoId, StringComparison.OrdinalIgnoreCase));
            // A retry after a partial create can already have persisted Added rows.
            if (existing is null && package.ResumeIncompleteCreate && string.IsNullOrWhiteSpace(lineEdit.AutoId))
            {
                existing = existingLines.FirstOrDefault(line => !line.IsVoided && !retainedLines.Contains(line) &&
                    string.Equals(line.InventoryID, lineEdit.InventoryId, StringComparison.OrdinalIgnoreCase) &&
                    line.Quantity == lineEdit.Quantity && line.UnitPrice == lineEdit.UnitPrice);
            }
            if (existing is not null)
            {
                retainedLines.Add(existing);
            }

            var quantity = Math.Max(1m, lineEdit.Quantity);
            var unitPrice = Math.Max(0m, lineEdit.UnitPrice);

            var line = existing ?? new Inventory_PackageItemDM();
            line.PackageID = FirstNonEmpty(lineEdit.PackageId, record.MasterAccountID);
            line.InventoryID = lineEdit.InventoryId;
            line.Description = lineEdit.Description;
            line.Quantity = quantity;
            line.UnitPrice = unitPrice;
            line.TotalPrice = unitPrice * quantity;
            line.UnitActualValue = unitPrice;
            line.TotalActualValue = unitPrice * quantity;
            line.InventoryTypeID = lineEdit.InventoryTypeId > 0
                ? lineEdit.InventoryTypeId
                : ServiceInventoryTypeId;
            line.IsDeferred = lineEdit.IsDeferred;
            line.IsVoided = false;
            line.IsConfirmed = true;
            line.PackageQuantityTypeID = 0;
            line.SaveAction = existing is null ? EntityState.Added : EntityState.Changed;
            line.IsDirty = true;
            record.lstPackage.Add(line);
        }

        foreach (var removedLine in existingLines.Where(line =>
                     !line.IsVoided && !retainedLines.Contains(line)))
        {
            removedLine.SaveAction = EntityState.Deleted;
            removedLine.IsDirty = true;
            record.lstPackage.Add(removedLine);
        }
    }

    private static Inventory_PackageItemDM ToPackageLine(InventoryPackageLineDTO source)
    {
        return new Inventory_PackageItemDM
        {
            AutoID = source.AutoId.ValueKind is System.Text.Json.JsonValueKind.Null
                or System.Text.Json.JsonValueKind.Undefined
                ? null
                : source.AutoId.ToString(),
            PackageID = source.PackageId,
            InventoryID = source.InventoryId,
            Description = source.Description,
            Quantity = source.Quantity,
            UnitPrice = source.UnitPrice,
            TotalPrice = source.TotalPrice,
            UnitActualValue = source.UnitActualValue,
            TotalActualValue = source.TotalActualValue,
            InventoryTypeID = source.InventoryTypeId,
            IsDeferred = source.IsDeferred,
            IsVoided = source.IsVoided,
            IsConfirmed = source.IsConfirmed,
            PackageQuantityTypeID = source.PackageQuantityTypeId,
            SaveAction = (EntityState)(-1),
            IsDirty = false
        };
    }

    private static InventoryPackageRequestDTO CreatePackageRequest(
        InventoryDM record,
        string branchId,
        decimal price,
        string branchGroupId,
        IReadOnlyCollection<InventoryMembershipCreditEdit>? membershipCredits,
        bool isUpdate,
        IReadOnlyCollection<InventoryPackageBranchEdit>? branches = null,
        IReadOnlyList<InventoryPackageBranchLoadDTO>? existingBranches = null)
    {
        var credits = (membershipCredits ?? [])
            .Where(credit => !string.IsNullOrWhiteSpace(credit.MemberTypeId))
            .Select(credit => new InventoryMembershipCreditDTO
            {
                MemberTypeId = credit.MemberTypeId.Trim(),
                MemberCredit = Math.Max(0m, credit.MemberCredit),
                SaveAction = string.IsNullOrWhiteSpace(credit.SaveAction)
                    ? (isUpdate ? "Changed" : "Added")
                    : credit.SaveAction,
                IsDirty = credit.IsDirty
            })
            .ToList();

        // Credit grants and the non-credit membership trigger are separate in Senang.
        SetInventoryProperty(record, "MembershipCredit", InventoryMembershipCreditMapper.Encode(credits));

        var branchEdits = (branches ?? [new InventoryPackageBranchEdit(branchId, branchGroupId, true)])
            .Where(branch => !string.IsNullOrWhiteSpace(branch.BranchId))
            .GroupBy(branch => branch.BranchId.Trim(), StringComparer.OrdinalIgnoreCase)
            .Select(group => group.Last()).ToList();
        // Preserve branches outside this session's available branch list.
        foreach (var existing in existingBranches ?? [])
        {
            if (!string.IsNullOrWhiteSpace(existing.BranchId) && !branchEdits.Any(branch =>
                string.Equals(branch.BranchId, existing.BranchId, StringComparison.OrdinalIgnoreCase)))
            {
                branchEdits.Add(new(existing.BranchId, existing.GroupId ?? string.Empty, existing.IsEnabled));
            }
        }

        return new InventoryPackageRequestDTO
        {
            ObjInventory = record,
            MembershipCredits = credits,
            UseEncodedMembershipCreditOnly = true,
            Branches = branchEdits.Select(branch =>
                new InventoryBranchDTO
                {
                    MasterAccountId = record.MasterAccountID,
                    BranchId = NormalizeBranchId(branch.BranchId),
                    BranchPrice = price,
                    IsEnabled = branch.IsEnabled,
                    GroupId = NormalizeBranchGroupId(branch.GroupId, branch.BranchId),
                    SaveAction = isUpdate && existingBranches?.Any(existing => string.Equals(
                        existing.BranchId, branch.BranchId, StringComparison.OrdinalIgnoreCase)) == true
                        ? "Changed" : "Added",
                    IsDirty = true
                }).ToList()
        };
    }

    private static string NormalizeBranchId(string branchId)
    {
        return string.IsNullOrWhiteSpace(branchId)
            ? "HQ"
            : branchId.Trim().ToUpperInvariant();
    }

    private static string NormalizeBranchGroupId(string branchGroupId, string branchId)
    {
        return string.IsNullOrWhiteSpace(branchGroupId)
            ? branchId
            : branchGroupId.Trim();
    }

    private static int ParseDuration(string duration)
    {
        var firstPart = duration.Split(' ', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault();
        return int.TryParse(firstPart, out var minutes) ? Math.Max(0, minutes) : 0;
    }

    private static ApiCallResult<bool> ToSaveResult(
        ApiCallResult<InventorySaveResultDTO> result,
        string fallbackMessage)
    {
        return result.Success
            ? ApiCallResult<bool>.Ok(result.StatusCode, true)
            : ApiCallResult<bool>.Failure(result.StatusCode, result.ErrorMessage ?? fallbackMessage);
    }

    private static string? GetInventoryString(object record, string propertyName)
    {
        var value = record.GetType().GetProperty(propertyName)?.GetValue(record);
        return value?.ToString();
    }

    private static decimal GetInventoryDecimal(object record, string propertyName)
    {
        var value = record.GetType().GetProperty(propertyName)?.GetValue(record);
        return value is null ? 0m : Convert.ToDecimal(value);
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

    private static string GetPackagePolicy(string? salesDescription, string? packageName)
    {
        if (string.IsNullOrWhiteSpace(salesDescription) ||
            string.Equals(
                salesDescription.Trim(),
                packageName?.Trim(),
                StringComparison.OrdinalIgnoreCase))
        {
            return string.Empty;
        }

        return salesDescription.Trim();
    }

    private static string EncodePackageEditorRemarks(
        decimal minPrice,
        decimal maxPrice,
        params string?[] terms)
    {
        static string Clean(string? value) =>
            (value ?? string.Empty).Replace("\r", " ").Replace("\n", " ").Trim();

        return string.Join(
            "\n",
            $"MIN_PRICE={minPrice.ToString(System.Globalization.CultureInfo.InvariantCulture)}",
            $"MAX_PRICE={maxPrice.ToString(System.Globalization.CultureInfo.InvariantCulture)}",
            $"TC1={Clean(terms.ElementAtOrDefault(0))}",
            $"TC2={Clean(terms.ElementAtOrDefault(1))}",
            $"TC3={Clean(terms.ElementAtOrDefault(2))}");
    }

    private static string GetPackageTerm(string? encodedTerms, int index)
    {
        if (string.IsNullOrWhiteSpace(encodedTerms))
        {
            return string.Empty;
        }

        var lines = encodedTerms.Split('\n');
        var prefix = $"TC{index + 1}=";
        var keyed = lines.FirstOrDefault(line =>
            line.StartsWith(prefix, StringComparison.OrdinalIgnoreCase));

        if (keyed is not null)
        {
            return keyed[prefix.Length..].Trim();
        }

        var legacyTerms = lines
            .Where(line =>
                !line.StartsWith("MIN_PRICE=", StringComparison.OrdinalIgnoreCase) &&
                !line.StartsWith("MAX_PRICE=", StringComparison.OrdinalIgnoreCase))
            .Select(line => line.Trim())
            .ToArray();

        return index >= 0 && index < legacyTerms.Length
            ? legacyTerms[index]
            : string.Empty;
    }

    private static decimal GetPackagePriceLimit(string? encodedTerms, string key)
    {
        if (string.IsNullOrWhiteSpace(encodedTerms))
        {
            return 0m;
        }

        var prefix = key + "=";
        var value = encodedTerms
            .Split('\n')
            .FirstOrDefault(line => line.StartsWith(prefix, StringComparison.OrdinalIgnoreCase));

        return value is not null &&
               decimal.TryParse(
                   value[prefix.Length..],
                   System.Globalization.NumberStyles.Any,
                   System.Globalization.CultureInfo.InvariantCulture,
                   out var result)
            ? Math.Max(0m, result)
            : 0m;
    }

    private static string? FirstNonEmpty(params string?[] values)
    {
        return values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value));
    }
}


