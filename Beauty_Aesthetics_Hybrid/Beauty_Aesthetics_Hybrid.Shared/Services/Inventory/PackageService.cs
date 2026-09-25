using System.Net;
using System.Text.Json;
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
        var result = await serviceInventoryAC.LoadProxyAsync(null, cancellationToken);

        if (!result.Success || result.Value is null)
        {
            return ApiCallResult<IReadOnlyList<InventoryPackageSummary>>.Failure(
                result.StatusCode,
                result.ErrorMessage ?? "Unable to load package records.");
        }

        var packageHeaders = result.Value
            .Where(record => record.InventoryTypeID == PackageInventoryTypeId)
            .ToList();

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

            var package = fullRecord?.Success == true
                ? fullRecord.Value?.ObjInventory
                : null;
            var packageLines = package?.PackageLines
                ?? fullRecord?.Value?.PackageLines
                ?? [];
            var membershipCredits = package?.MembershipCredits
                ?? fullRecord?.Value?.MembershipCredits
                ?? [];
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
                    GetFirstObjectString(line, "UOM", "UnitOfMeasureID", "UnitOfMeasure", "UnitOfMeasureName")))
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

            var packageRemarks = package is not null
                ? GetInventoryString(package, "Remarks")
                : null;
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
                header.ImagePath ?? string.Empty,
                header.ImageFileName ?? string.Empty,
                FirstNonEmpty(package?.ItemGroupName, header.ItemGroupName) ?? string.Empty,
                string.Equals(
                    FirstNonEmpty(package?.AccountStatus, header.AccountStatus, "Active"),
                    "Active",
                    StringComparison.OrdinalIgnoreCase),
                FirstNonEmpty(package?.VendorItemCode, header.VendorItemCode) ?? string.Empty,
                FirstNonEmpty(package?.UnitOfMeasureId, header.UnitOfMeasureName, header.UnitOfMeasureID, "unit") ?? "unit",
                package is not null && package.ValidityDays != 0 ? package.ValidityDays : header.ValidityDays,
                package is not null && package.MemberExpiryDays != 0
                    ? package.MemberExpiryDays
                    : GetInventoryInt(header, "MemberExpiryDays"),
                FirstNonEmpty(
                    package?.TriggeredMemberTypeId,
                    GetInventoryString(header, "TriggeredMemberTypeID")) ?? string.Empty,
                package is not null && package.MemberMainAccountCredit != 0
                    ? package.MemberMainAccountCredit
                    : GetInventoryDecimal(header, "MemberMainAccountCredit"),
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
                package is not null && package.PurchasePrice != 0m
                    ? package.PurchasePrice
                    : header.PurchasePrice,
                FirstNonEmpty(package?.TaxCodeId, header.TaxCodeID) ?? string.Empty,
                package is not null ? package.IsTaxInclusive : header.IsTaxInclusive,
                membershipCredits
                    .Where(credit => !string.IsNullOrWhiteSpace(credit.MemberTypeId))
                    .Select(credit => new InventoryMembershipCreditSummary(
                        credit.MemberTypeId,
                        Math.Max(0m, credit.MemberCredit)))
                    .ToList()));
        }

        return ApiCallResult<IReadOnlyList<InventoryPackageSummary>>.Ok(
            result.StatusCode,
            summaries.OrderBy(package => package.Name).ToList());
    }

    public async Task<ApiCallResult<bool>> CreatePackageAsync(
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
            "Added",
            package.MembershipCredits,
            isUpdate: false);
        var result = ToSaveResult(
            await serviceInventoryAC.CreateFullAsync(request, cancellationToken),
            "Unable to create package.");

        if (result.Success)
            feedback.Success("Package created successfully.", "Package created");
        else
            feedback.Error(result.ErrorMessage ?? "Unable to create package.", "Package not created");

        return result;
    }

    public async Task<ApiCallResult<bool>> UpdatePackageAsync(
        InventoryPackageEdit package,
        string branchId = "hq",
        CancellationToken cancellationToken = default,
        string branchGroupId = "")
    {
        if (string.IsNullOrWhiteSpace(package.MasterAccountId))
        {
            return ApiCallResult<bool>.Failure(
                HttpStatusCode.BadRequest,
                "The selected package has no record ID.");
        }

        var loadResult = await serviceInventoryAC.LoadFullAsync(package.MasterAccountId, cancellationToken);
        if (!loadResult.Success || loadResult.Value is null)
        {
            return ApiCallResult<bool>.Failure(
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
            "Changed",
            package.MembershipCredits,
            isUpdate: true);

        var result = ToSaveResult(
            await serviceInventoryAC.UpdateFullAsync(request, cancellationToken),
            "Unable to update package.");

        if (result.Success)
            feedback.Success("Package updated successfully.", "Package updated");
        else
            feedback.Error(result.ErrorMessage ?? "Unable to update package.", "Package not updated");

        return result;
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
            UnitOfMeasureID = "UNIT",
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
        record.SalesPrice = Math.Max(0, package.Price);
        record.PurchasePrice = Math.Max(0, package.Cost);
        record.TaxCodeID = package.TaxCode?.Trim() ?? string.Empty;
        record.IsTaxInclusive = package.IsTaxInclusive;
        record.VendorItemCode = package.Barcode?.Trim() ?? string.Empty;
        record.UnitOfMeasureID = string.IsNullOrWhiteSpace(package.UnitOfMeasure)
            ? "unit"
            : package.UnitOfMeasure.Trim();
        record.UnitOfMeasureName = record.UnitOfMeasureID;
        record.ImagePath = string.IsNullOrWhiteSpace(package.ImagePath)
            ? null
            : package.ImagePath.Trim();
        record.ImageFileName = string.IsNullOrWhiteSpace(package.ImageFileName)
            ? null
            : package.ImageFileName.Trim();
        record.ValidityDays = Math.Max(0, package.ValidityDays);
        SetInventoryProperty(record, "MemberExpiryDays", Math.Max(0, package.MemberExpiryDays));
        SetInventoryProperty(record, "TriggeredMemberTypeID", package.TriggeredMemberTypeId?.Trim() ?? string.Empty);
        SetInventoryProperty(record, "MemberMainAccountCredit", Math.Max(0, package.MemberMainAccountCredit));
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
        record.HasPackage = (package.Lines?.Count ?? package.Services.Count) > 0;
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

        var selectedIds = editedLines
            .Select(line => line.InventoryId)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        record.lstPackage ??= new System.Collections.ObjectModel.ObservableCollection<Inventory_PackageItemDM>();
        record.lstPackage.Clear();

        foreach (var lineEdit in editedLines)
        {
            var existing = existingLines.FirstOrDefault(line =>
                string.Equals(line.InventoryID, lineEdit.InventoryId, StringComparison.OrdinalIgnoreCase));

            var quantity = Math.Max(1m, lineEdit.Quantity);
            var unitPrice = Math.Max(0m, lineEdit.UnitPrice);

            var line = existing ?? new Inventory_PackageItemDM();
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
            SetFirstObjectProperty(
                line,
                string.IsNullOrWhiteSpace(lineEdit.UnitOfMeasure) ? "unit" : lineEdit.UnitOfMeasure.Trim(),
                "UOM",
                "UnitOfMeasureID",
                "UnitOfMeasure",
                "UnitOfMeasureName");
            line.SaveAction = existing is null ? EntityState.Added : EntityState.Changed;
            line.IsDirty = true;
            record.lstPackage.Add(line);
        }

        foreach (var removedLine in existingLines.Where(line =>
                     !string.IsNullOrWhiteSpace(line.InventoryID) &&
                     !selectedIds.Contains(line.InventoryID)))
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
        string saveAction,
        IReadOnlyCollection<InventoryMembershipCreditEdit>? membershipCredits,
        bool isUpdate)
    {
        return new InventoryPackageRequestDTO
        {
            ObjInventory = BuildInventoryPayload(record, membershipCredits, isUpdate),
            Branches =
            [
                new InventoryBranchDTO
                {
                    MasterAccountId = record.MasterAccountID,
                    BranchId = branchId,
                    BranchPrice = price,
                    IsEnabled = true,
                    GroupId = branchGroupId,
                    SaveAction = saveAction,
                    IsDirty = true
                }
            ]
        };
    }

    private static JsonElement BuildInventoryPayload(
        InventoryDM record,
        IReadOnlyCollection<InventoryMembershipCreditEdit>? membershipCredits,
        bool isUpdate)
    {
        var serializedRecord = JsonSerializer.SerializeToElement(record);
        var payload = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);

        foreach (var property in serializedRecord.EnumerateObject())
        {
            payload[property.Name] = property.Value.Clone();
        }

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

        payload["lstMembershipCredit"] = credits;

        // Keep the legacy scalar fields populated for API deployments that still read them.
        var firstCredit = credits.FirstOrDefault(credit =>
            !string.Equals(credit.SaveAction, "Deleted", StringComparison.OrdinalIgnoreCase));
        payload["triggeredMemberTypeID"] = firstCredit?.MemberTypeId ?? string.Empty;
        payload["memberMainAccountCredit"] = firstCredit?.MemberCredit ?? 0m;

        return JsonSerializer.SerializeToElement(payload);
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

    private static int GetInventoryInt(object record, string propertyName)
    {
        var value = record.GetType().GetProperty(propertyName)?.GetValue(record);
        return value is null ? 0 : Convert.ToInt32(value);
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

    private static string GetFirstObjectString(object source, params string[] propertyNames)
    {
        foreach (var propertyName in propertyNames)
        {
            var property = source.GetType().GetProperty(propertyName);
            if (property is null || !property.CanRead)
            {
                continue;
            }

            var value = property.GetValue(source)?.ToString();
            if (!string.IsNullOrWhiteSpace(value))
            {
                return value;
            }
        }

        return "unit";
    }

    private static void SetFirstObjectProperty(object target, object? value, params string[] propertyNames)
    {
        foreach (var propertyName in propertyNames)
        {
            var property = target.GetType().GetProperty(propertyName);
            if (property is null || !property.CanWrite)
            {
                continue;
            }

            try
            {
                var targetType = Nullable.GetUnderlyingType(property.PropertyType) ?? property.PropertyType;
                var converted = value is null || targetType.IsInstanceOfType(value)
                    ? value
                    : Convert.ChangeType(value, targetType, System.Globalization.CultureInfo.InvariantCulture);
                property.SetValue(target, converted);
                return;
            }
            catch (InvalidCastException)
            {
            }
            catch (FormatException)
            {
            }
            catch (OverflowException)
            {
            }
            catch (ArgumentException)
            {
            }
        }
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


