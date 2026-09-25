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

public sealed class ProductInventoryService : IProductInventoryService
{
    private const int ProductInventoryTypeId = 1;
    private const string ProductInventoryTypeName = "Stock";
    private static readonly DateTime AvailableFrom = new(2000, 1, 1);
    private static readonly DateTime AvailableTo = new(2049, 12, 31);

    private readonly InventoryAC inventoryAC;
    private readonly ServiceInventoryAC serviceInventoryAC;
    private readonly AppFeedbackService feedback;

    public ProductInventoryService(
        InventoryAC inventoryAC,
        ServiceInventoryAC serviceInventoryAC,
        AppFeedbackService feedback)
    {
        this.inventoryAC = inventoryAC;
        this.serviceInventoryAC = serviceInventoryAC;
        this.feedback = feedback;
    }

    public async Task<ApiCallResult<IReadOnlyList<InventoryViewModel.InventoryItem>>> LoadProductsAsync(
        string branchId = "HQ",
        CancellationToken cancellationToken = default)
    {
        var result = await inventoryAC.LoadProxyAsync(null, cancellationToken);
        if (!result.Success || result.Value is null)
        {
            return ApiCallResult<IReadOnlyList<InventoryViewModel.InventoryItem>>.Failure(
                result.StatusCode,
                result.ErrorMessage ?? "Unable to load products.");
        }

        var products = result.Value
            .Where(record => record.InventoryTypeID == ProductInventoryTypeId)
            .GroupBy(record => record.MasterAccountID ?? record.DisplayCode ?? string.Empty)
            .Select(group => ToProduct(group.First()))
            .OrderBy(product => product.Type)
            .ToList();

        var inventoryIds = products
            .Select(product => product.MasterAccountId)
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (inventoryIds.Count > 0)
        {
            var today = DateTime.Today;
            var balanceResult = await inventoryAC.GetStockBalanceByBranchAndByItemAsync(
                new StockBalanceRequestDTO
                {
                    BranchID = NormalizeBranchId(branchId),
                    InventoryIDs = string.Join(",", inventoryIds),
                    FinancialDate = today,
                    EndDate = today,
                    RedemptionEndDate = today,
                    NewExpiryDate = today,
                    MinQuantityBalanceToShow = 0
                },
                cancellationToken);

            if (balanceResult.Success && balanceResult.Value is not null)
            {
                var balances = new Dictionary<string, rpt_StockBalanceByBranchByItemsDM>(
                    balanceResult.Value,
                    StringComparer.OrdinalIgnoreCase);

                products = products
                    .Select(product => product with
                    {
                        StockQuantity = balances.TryGetValue(product.MasterAccountId, out var balance)
                            ? balance.ExistingQuantity
                            : null
                    })
                    .ToList();
            }
        }

        return ApiCallResult<IReadOnlyList<InventoryViewModel.InventoryItem>>.Ok(result.StatusCode, products);
    }

    public async Task<ApiCallResult<InventoryViewModel.InventoryItem>> LoadProductAsync(
        string masterAccountId,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(masterAccountId))
        {
            return ApiCallResult<InventoryViewModel.InventoryItem>.Failure(
                HttpStatusCode.BadRequest,
                "Product record ID is missing.");
        }

        var result = await inventoryAC.LoadRecordAsync(masterAccountId, cancellationToken);
        if (!result.Success || result.Value is null)
        {
            return ApiCallResult<InventoryViewModel.InventoryItem>.Failure(
                result.StatusCode,
                result.ErrorMessage ?? "Unable to load product details.");
        }

        var product = ToProduct(result.Value);
        var fullResult = await serviceInventoryAC.LoadFullAsync(masterAccountId, cancellationToken);
        if (fullResult.Success && fullResult.Value is not null)
        {
            var full = fullResult.Value;
            var fullRecord = full.ObjInventory;
            var commission1 = ParseCommissionFormula(fullRecord?.StaffCommissionA);
            var commission2 = ParseCommissionFormula(fullRecord?.StaffCommissionB);
            var commission3 = ParseCommissionFormula(fullRecord?.StaffCommissionC);

            product = product with
            {
                RedeemPoint = full.PointToRedeem
                    ?? fullRecord?.PointToRedeem
                    ?? 0m,
                Commission1 = commission1.Amount,
                Commission2 = commission2.Amount,
                Commission3 = commission3.Amount,
                Commission1IsPercent = commission1.IsPercent,
                Commission2IsPercent = commission2.IsPercent,
                Commission3IsPercent = commission3.IsPercent,
                VisibleBranchIds = (full.Branches ?? [])
                    .Where(branch => branch.IsEnabled && !string.IsNullOrWhiteSpace(branch.BranchId))
                    .Select(branch => branch.BranchId!.Trim())
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToList(),
                SellingUnits = (fullRecord?.SellingUnits ?? [])
                    .Where(unit => !string.IsNullOrWhiteSpace(unit.SkuName))
                    .Select(unit => new InventoryViewModel.ProductSellingUnit(
                        unit.AutoId,
                        unit.SkuName,
                        unit.SkuQuantity,
                        unit.SalesPrice,
                        unit.PurchasePrice,
                        unit.Barcode,
                        true))
                    .ToList()
            };
        }

        return ApiCallResult<InventoryViewModel.InventoryItem>.Ok(result.StatusCode, product);
    }

    public async Task<ApiCallResult<bool>> CreateProductAsync(
        InventoryViewModel.InventoryItem product,
        string branchId = "HQ",
        CancellationToken cancellationToken = default)
    {
        var record = CreateRecord();
        ApplyProduct(record, product, branchId);
        record.SaveAction = EntityState.Added;
        record.IsDirty = true;

        var normalizedBranchId = NormalizeBranchId(branchId);
        var request = BuildProductRequest(
            record,
            product,
            normalizedBranchId,
            masterAccountId: null,
            saveAction: "Added",
            existingBranchIds: null);

        var result = ToBoolean(
            await inventoryAC.CreateFullAsync(request, cancellationToken),
            "Unable to create product.");

        if (result.Success)
        {
            feedback.Success("Product created successfully.", "Product created");
        }
        else
        {
            feedback.Error(result.ErrorMessage ?? "Unable to create product.", "Product not created");
        }

        return result;
    }

    public async Task<ApiCallResult<bool>> UpdateProductAsync(
        InventoryViewModel.InventoryItem product,
        string branchId = "HQ",
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(product.MasterAccountId))
        {
            return ApiCallResult<bool>.Failure(HttpStatusCode.BadRequest, "Product record ID is missing.");
        }

        var loadResult = await inventoryAC.LoadRecordAsync(product.MasterAccountId, cancellationToken);
        if (!loadResult.Success || loadResult.Value is null)
        {
            return ApiCallResult<bool>.Failure(
                loadResult.StatusCode,
                loadResult.ErrorMessage ?? "Unable to load product before updating.");
        }

        ApplyProduct(loadResult.Value, product, branchId);
        loadResult.Value.SaveAction = EntityState.Changed;
        loadResult.Value.IsDirty = true;

        var normalizedBranchId = NormalizeBranchId(branchId);
        var currentFull = await serviceInventoryAC.LoadFullAsync(product.MasterAccountId, cancellationToken);
        var existingBranchIds = currentFull.Success && currentFull.Value?.Branches is not null
            ? currentFull.Value.Branches
                .Where(branch => branch.IsEnabled && !string.IsNullOrWhiteSpace(branch.BranchId))
                .Select(branch => branch.BranchId!.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList()
            : new List<string>();

        var request = BuildProductRequest(
            loadResult.Value,
            product,
            normalizedBranchId,
            product.MasterAccountId,
            "Changed",
            existingBranchIds);

        var result = ToBoolean(
            await inventoryAC.UpdateFullAsync(request, cancellationToken),
            "Unable to update product.");

        if (result.Success)
        {
            feedback.Success("Product updated successfully.", "Product updated");
        }
        else
        {
            feedback.Error(result.ErrorMessage ?? "Unable to update product.", "Product not updated");
        }

        return result;
    }

    public async Task<ApiCallResult<bool>> DeleteProductAsync(
        InventoryViewModel.InventoryItem product,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(product.MasterAccountId))
        {
            return ApiCallResult<bool>.Failure(HttpStatusCode.BadRequest, "Product record ID is missing.");
        }

        var result = await inventoryAC.DeleteFullAsync(product.MasterAccountId, cancellationToken);

        if (result.Success)
        {
            feedback.Success("Product deleted successfully.", "Product deleted");
        }
        else
        {
            feedback.Error(result.ErrorMessage ?? "Unable to delete product.", "Product not deleted");
        }

        return result;
    }

    private static InventoryViewModel.InventoryItem ToProduct(InventoryDM record)
    {
        var sku = First(record.DisplayCode, record.ProductCode, record.MasterAccountID);
        var stockUnit = First(record.UnitOfMeasureName, record.UnitOfMeasureID);
        var reportingUnit = record.ReportingUOM ?? string.Empty;

        return new InventoryViewModel.InventoryItem(
            sku,
            First(record.AccountName, record.SalesDescription, sku),
            First(record.VendorItemCode, record.ProductCode),
            record.QuantityFactor > 0 ? record.QuantityFactor.ToString("0.##") : string.Empty,
            record.SalesPrice,
            record.BrandName ?? string.Empty,
            First(record.ItemCategoryName, record.ItemGroupName),
            First(record.SupplierName, record.PreferredVendorAccountID),
            string.Equals(record.AccountStatus, "Inactive", StringComparison.OrdinalIgnoreCase) || string.Equals(record.strStatus, "Locked", StringComparison.OrdinalIgnoreCase),
            string.IsNullOrWhiteSpace(stockUnit) ? "-" : $"- {stockUnit}",
            string.IsNullOrWhiteSpace(reportingUnit) ? "-" : $"- {reportingUnit}",
            record.Rack ?? string.Empty,
            record.StockReorderLevel.ToString("0.##"),
            record.MaxDiscountLimit)
        {
            MasterAccountId = record.MasterAccountID ?? string.Empty,
            UnitOfMeasurementId = record.UnitOfMeasureID ?? string.Empty,
            SupplierAccountId = record.PreferredVendorAccountID ?? string.Empty,
            CategoryId = record.ItemCategoryID ?? string.Empty,
            InventoryTypeId = record.InventoryTypeID,
            Cost = record.PurchasePrice,
            TaxCode = record.TaxCodeID ?? string.Empty,
            IsTaxInclusive = record.IsTaxInclusive,
            IsActive = !string.Equals(record.AccountStatus, "Inactive", StringComparison.OrdinalIgnoreCase),
            Remarks = record.Remarks ?? string.Empty,
            ItemGroupId = record.ItemGroupID ?? string.Empty,
            ItemGroupName = record.ItemGroupName ?? string.Empty,
            ImagePath = record.ImagePath ?? string.Empty,
            ImageFileName = record.ImageFileName ?? string.Empty
        };
    }

    private static InventoryDM CreateRecord()
    {
        return new InventoryDM
        {
            AccountTypeID = 4,
            InventoryTypeID = ProductInventoryTypeId,
            InventoryTypeName = ProductInventoryTypeName,
            AccountStatus = "Active",
            IsSold = true,
            IsPurchased = true,
            CreatedDateTime = DateTime.Now,
            AvailableDateFrom = AvailableFrom,
            AvailableDateTo = AvailableTo,
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
    }

    private static void ApplyProduct(
        InventoryDM record,
        InventoryViewModel.InventoryItem product,
        string branchId)
    {
        record.InventoryTypeID = ProductInventoryTypeId;
        record.InventoryTypeName = ProductInventoryTypeName;
        record.DisplayCode = product.Sku.Trim();
        record.VendorItemCode = string.IsNullOrWhiteSpace(product.Barcode) ? null : product.Barcode.Trim();
        record.AccountName = product.Type.Trim();
        record.SalesDescription = product.Type.Trim();
        record.PurchaseDescription = product.Type.Trim();
        record.SalesPrice = product.Price;
        record.PurchasePrice = Math.Max(0m, product.Cost);
        record.TaxCodeID = string.IsNullOrWhiteSpace(product.TaxCode) ? null : product.TaxCode.Trim();
        record.IsTaxInclusive = product.IsTaxInclusive;
        record.BrandName = product.Brand.Trim();
        record.ItemCategoryID = string.IsNullOrWhiteSpace(product.CategoryId)
            ? null
            : product.CategoryId.Trim();
        record.ItemCategoryName = product.Category.Trim();
        record.ItemGroupID = string.IsNullOrWhiteSpace(product.ItemGroupId)
            ? record.ItemCategoryID
            : product.ItemGroupId.Trim();
        record.ItemGroupName = string.IsNullOrWhiteSpace(product.ItemGroupName)
            ? record.ItemCategoryName
            : product.ItemGroupName.Trim();
        record.SupplierName = product.Supplier.Trim();
        record.Remarks = product.Remarks?.Trim();
        record.Rack = product.Location.Trim();
        record.StockReorderLevel = ParseDecimal(product.LowAlertCount);
        record.MaxDiscountLimit = product.DiscountCap;
        record.ImagePath = string.IsNullOrWhiteSpace(product.ImagePath) ? null : product.ImagePath.Trim();
        record.ImageFileName = string.IsNullOrWhiteSpace(product.ImageFileName) ? null : product.ImageFileName.Trim();
        record.QuantityFactor = Math.Max(1, ParseDecimal(product.ConversionFactor));
        var primaryUom = ParseStockUom(product.StockUom1);
        var secondaryUom = ParseStockUom(product.StockUom2);
        record.UnitOfMeasureID = primaryUom.Unit;
        record.UnitOfMeasureName = primaryUom.Unit;
        record.ReportingUOM = secondaryUom.Unit;
        if (primaryUom.Quantity > 0 && secondaryUom.Quantity > 0)
        {
            record.QuantityFactor = primaryUom.Quantity / secondaryUom.Quantity;
        }
        record.AccountStatus = product.IsActive && !product.Locked ? "Active" : "Inactive";
        record.strStatus = product.IsActive && !product.Locked ? "Active" : "Locked";
        record.BranchID = string.IsNullOrWhiteSpace(branchId) ? "HQ" : branchId.Trim().ToUpperInvariant();
    }

    private static (decimal Quantity, string Unit) ParseStockUom(string value)
    {
        var normalized = (value ?? string.Empty).Trim().TrimStart('-').Trim();
        if (string.IsNullOrWhiteSpace(normalized))
        {
            return (0, "UNIT");
        }

        var parts = normalized.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var hasQuantity = decimal.TryParse(parts[0], out var quantity);
        var unit = string.Join(' ', hasQuantity ? parts.Skip(1) : parts).Trim();
        return (hasQuantity ? quantity : 0, string.IsNullOrWhiteSpace(unit) ? "UNIT" : unit.ToUpperInvariant());
    }
    private static InventoryPackageRequestDTO BuildProductRequest(
        InventoryDM record,
        InventoryViewModel.InventoryItem product,
        string fallbackBranchId,
        string? masterAccountId,
        string saveAction,
        IReadOnlyCollection<string>? existingBranchIds)
    {
        var visibleBranches = (product.VisibleBranchIds ?? Array.Empty<string>())
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => value.Trim().ToUpperInvariant())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (visibleBranches.Count == 0)
        {
            visibleBranches.Add(fallbackBranchId);
        }

        var existingBranches = (existingBranchIds ?? Array.Empty<string>())
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => value.Trim().ToUpperInvariant())
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var branchEntries = visibleBranches
            .Select(branch => new InventoryBranchDTO
            {
                MasterAccountId = masterAccountId,
                BranchId = branch,
                BranchPrice = product.Price,
                IsEnabled = true,
                GroupId = branch,
                SaveAction = existingBranches.Contains(branch) ? "Changed" : saveAction,
                IsDirty = true
            })
            .ToList();

        foreach (var removedBranch in existingBranches.Where(branch =>
                     !visibleBranches.Contains(branch, StringComparer.OrdinalIgnoreCase)))
        {
            branchEntries.Add(new InventoryBranchDTO
            {
                MasterAccountId = masterAccountId,
                BranchId = removedBranch,
                BranchPrice = product.Price,
                IsEnabled = false,
                GroupId = removedBranch,
                SaveAction = "Deleted",
                IsDirty = true
            });
        }

        return new InventoryPackageRequestDTO
        {
            ObjInventory = record,
            Branches = branchEntries,
            SellingUnits = (product.SellingUnits ?? Array.Empty<InventoryViewModel.ProductSellingUnit>())
                .Where(unit => unit.IsDeleted || !string.IsNullOrWhiteSpace(unit.UnitName))
                .Select(unit => new InventoryProductSkuDTO
                {
                    AutoId = unit.AutoId ?? string.Empty,
                    InventoryAccountId = masterAccountId ?? string.Empty,
                    SkuName = unit.UnitName.Trim().ToUpperInvariant(),
                    SkuQuantity = Math.Max(0.0001m, unit.Quantity),
                    SalesPrice = Math.Max(0m, unit.SalesPrice),
                    PurchasePrice = Math.Max(0m, unit.PurchasePrice),
                    Barcode = unit.Barcode?.Trim() ?? string.Empty,
                    SaveAction = unit.IsDeleted ? "Deleted" : unit.IsExisting ? "Changed" : "Added",
                    IsDirty = true
                })
                .ToList(),
            StaffCommissionA = FormatCommissionFormula(product.Commission1, product.Commission1IsPercent),
            StaffCommissionB = FormatCommissionFormula(product.Commission2, product.Commission2IsPercent),
            StaffCommissionC = FormatCommissionFormula(product.Commission3, product.Commission3IsPercent),
            PointToRedeem = product.RedeemPoint > 0 ? product.RedeemPoint : null,
            AllowPointRedemption = product.RedeemPoint > 0
        };
    }

    private static string? FormatCommissionFormula(decimal amount, bool isPercent)
    {
        if (amount <= 0m)
        {
            return null;
        }

        var value = amount.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture);
        return isPercent ? $"{value}%" : value;
    }

    private static (decimal Amount, bool IsPercent) ParseCommissionFormula(string? formula)
    {
        if (string.IsNullOrWhiteSpace(formula))
        {
            return (0m, true);
        }

        var normalized = formula.Trim().TrimStart('T', 'F').TrimEnd('A');
        var isPercent = normalized.EndsWith("%", StringComparison.Ordinal);
        normalized = normalized.TrimEnd('%');

        return decimal.TryParse(
            normalized,
            System.Globalization.NumberStyles.Any,
            System.Globalization.CultureInfo.InvariantCulture,
            out var value)
            ? (value, isPercent)
            : (0m, true);
    }

    private static string NormalizeBranchId(string branchId) =>
        string.IsNullOrWhiteSpace(branchId) ? "HQ" : branchId.Trim().ToUpperInvariant();

    private static decimal ParseDecimal(string value)
    {
        var numeric = new string(value
            .TakeWhile(character => char.IsDigit(character) || character is '.' or '-')
            .ToArray());
        return decimal.TryParse(numeric, out var result) ? result : 0;
    }

    private static ApiCallResult<bool> ToBoolean(ApiCallResult<InventorySaveResultDTO> result, string fallback)
    {
        return result.Success
            ? ApiCallResult<bool>.Ok(result.StatusCode, true)
            : ApiCallResult<bool>.Failure(result.StatusCode, result.ErrorMessage ?? fallback);
    }
private static ApiCallResult<bool> ToBoolean(ApiCallResult<JsonElement> result, string fallback)
    {
        return result.Success
            ? ApiCallResult<bool>.Ok(result.StatusCode, true)
            : ApiCallResult<bool>.Failure(result.StatusCode, result.ErrorMessage ?? fallback);
    }

    private static string First(params string?[] values) =>
        values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value)) ?? string.Empty;
}
