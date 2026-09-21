using System.Globalization;
using System.Net;
using System.Text.Json.Nodes;
using Beauty_Aesthetics_WebPos.APIClient;
using Beauty_Aesthetics_WebPos.APIClient.ResultPattern;
using Beauty_Aesthetics_WebPos.Components.ViewModels;
using Beauty_Aesthetics_WebPos.Models.DTOs;

namespace Beauty_Aesthetics_WebPos.Components.Services.Inventory;

public sealed class StockGinService : IStockGinService
{
    private readonly StockGinAC stockGinAC;

    public StockGinService(StockGinAC stockGinAC)
    {
        this.stockGinAC = stockGinAC;
    }

    public async Task<ApiCallResult<IReadOnlyList<StockGinViewModel>>> LoadGinsAsync(
        string branchId,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(branchId))
        {
            return ApiCallResult<IReadOnlyList<StockGinViewModel>>.Ok(
                HttpStatusCode.OK,
                Array.Empty<StockGinViewModel>());
        }

        // Same history flow as SenangRetails InventoryStockOut:
        // one LoadProxy request, 30-day window, page 1, page size 500.
        var result = await stockGinAC.LoadProxyAsync(new StockGinProxyRequestDTO
        {
            BranchID = branchId.Trim(),
            StartDate = DateTime.Today.AddDays(-30),
            EndDate = DateTime.Today.AddDays(1).AddTicks(-1),
            PageNumber = 1,
            PageSize = 500
        }, cancellationToken);

        if (!result.Success || result.Value is null)
        {
            return ApiCallResult<IReadOnlyList<StockGinViewModel>>.Failure(
                result.StatusCode,
                result.ErrorMessage ?? "Unable to load GIN records.");
        }

        var rows = result.Value
            .Where(record => !record.IsVoid)
            .OrderByDescending(record => record.FinancialDate)
            .Select(ToViewModel)
            .ToList();

        return ApiCallResult<IReadOnlyList<StockGinViewModel>>.Ok(result.StatusCode, rows);
    }

    public async Task<ApiCallResult<StockGinViewModel>> LoadGinAsync(
        string documentId,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(documentId))
        {
            return ApiCallResult<StockGinViewModel>.Failure(HttpStatusCode.BadRequest, "GIN document ID is missing.");
        }

        var result = await stockGinAC.LoadRecordAsync(documentId, cancellationToken);
        if (!result.Success || result.Value?.Document is null)
        {
            return ApiCallResult<StockGinViewModel>.Failure(
                result.StatusCode,
                result.ErrorMessage ?? "The GIN record was not found.");
        }

        return ApiCallResult<StockGinViewModel>.Ok(result.StatusCode, ToViewModel(result.Value));
    }

    public async Task<ApiCallResult<bool>> CreateGinAsync(
        StockGinViewModel gin,
        CancellationToken cancellationToken = default)
    {
        var validationError = Validate(gin);
        if (validationError is not null)
        {
            return ApiCallResult<bool>.Failure(HttpStatusCode.BadRequest, validationError);
        }

        // Prefer an API-supplied blank template when available. The endpoint can also
        // return a null header, so CreateRecord must not depend on that template.
        var templateResult = await stockGinAC.LoadRecordAsync(string.Empty, cancellationToken);
        var envelope = templateResult.Success && templateResult.Value?.Document is not null
            ? templateResult.Value
            : StockGinEnvelopeDTO.CreateNew();
        ApplyDocument(envelope.Document!, gin);
        ApplyLines(envelope, gin, isNew: true);
        envelope.Document!.SaveAction = "Added";
        envelope.Document.IsDirty = true;

        return ToBoolean(
            await stockGinAC.CreateRecordAsync(envelope, cancellationToken),
            "Unable to create GIN.");
    }

    public async Task<ApiCallResult<bool>> UpdateGinAsync(
        StockGinViewModel gin,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(gin.DocumentId))
        {
            return ApiCallResult<bool>.Failure(HttpStatusCode.BadRequest, "GIN document ID is missing.");
        }

        var validationError = Validate(gin);
        if (validationError is not null)
        {
            return ApiCallResult<bool>.Failure(HttpStatusCode.BadRequest, validationError);
        }

        var loadResult = await stockGinAC.LoadRecordAsync(gin.DocumentId, cancellationToken);
        if (!loadResult.Success || loadResult.Value?.Document is null)
        {
            return ApiCallResult<bool>.Failure(
                loadResult.StatusCode,
                loadResult.ErrorMessage ?? "Unable to load the GIN before updating.");
        }

        ApplyDocument(loadResult.Value.Document, gin);
        ApplyLines(loadResult.Value, gin, isNew: false);
        loadResult.Value.Document.DocumentID = gin.DocumentId;
        loadResult.Value.Document.SaveAction = "Changed";
        loadResult.Value.Document.IsDirty = true;

        return ToBoolean(
            await stockGinAC.UpdateRecordAsync(loadResult.Value, cancellationToken),
            "Unable to update GIN.");
    }

    private static void ApplyDocument(StockGrnDocumentDTO document, StockGinViewModel gin)
    {
        var branchId = NormalizeBranchId(gin.BranchId);
        var date = gin.Date == default ? DateTime.Today : gin.Date;
        var total = gin.Lines.Sum(line => Math.Max(0, line.Quantity) * Math.Max(0, line.UnitCost));

        document.FriendlyDocumentName = string.IsNullOrWhiteSpace(document.FriendlyDocumentName)
            ? "GIN"
            : document.FriendlyDocumentName;
        document.BranchID = branchId;
        document.EditBranchID = branchId;
        document.FinancialDate = date;
        document.PostingDate = date;
        document.IsPostingDateDifferent = false;
        document.DisplayCode = NullIfWhiteSpace(gin.DisplayCode);
        document.StockActivityType = NullIfWhiteSpace(gin.IssueType);
        document.AccountID = NullIfWhiteSpace(gin.AccountId);
        document.AccountName = NullIfWhiteSpace(gin.AccountName);
        document.ReferenceNumber = NullIfWhiteSpace(gin.ReferenceNumber);
        document.Remarks = NullIfWhiteSpace(gin.Remarks);
        document.ExchangeRate = document.ExchangeRate <= 0 ? 1 : document.ExchangeRate;
        document.TotalBeforeTax = total;
        document.TaxableAmount = total;
        document.TotalAfterTax = total;
        document.LocalTotalBeforeTax = total;
        document.LocalTaxableAmount = total;
        document.LocalTotalAfterTax = total;
    }

    private static void ApplyLines(StockGinEnvelopeDTO envelope, StockGinViewModel gin, bool isNew)
    {
        var document = envelope.Document ?? throw new InvalidOperationException("GIN document header is missing.");
        var existingLines = envelope.DocumentLines.ToList();
        var existingById = existingLines
            .Where(line => !string.IsNullOrWhiteSpace(ReadString(line, "documentLineID")))
            .DistinctBy(line => ReadString(line, "documentLineID"), StringComparer.OrdinalIgnoreCase)
            .ToDictionary(line => ReadString(line, "documentLineID"), StringComparer.OrdinalIgnoreCase);
        var blankTemplate = isNew ? existingLines.FirstOrDefault() : null;
        var updatedLines = new List<JsonObject>();

        for (var index = 0; index < gin.Lines.Count; index++)
        {
            var source = gin.Lines[index];
            JsonObject? existingLine = null;
            var hasExistingLine = !string.IsNullOrWhiteSpace(source.DocumentLineId) &&
                                  existingById.TryGetValue(source.DocumentLineId, out existingLine);
            var line = hasExistingLine
                ? existingLine!
                : blankTemplate?.DeepClone().AsObject() ?? new JsonObject();
            var lineId = hasExistingLine ? source.DocumentLineId : Guid.NewGuid().ToString();
            var quantity = Math.Max(0, source.Quantity);
            var unitCost = Math.Max(0, source.UnitCost);
            var amount = quantity * unitCost;

            Set(line, "documentLineID", lineId);
            Set(line, "documentID", document.DocumentID);
            Set(line, "ownerDocumentTypeID", document.DocumentTypeID);
            Set(line, "lineOrder", index + 1);
            Set(line, "lineItemID", source.InventoryId);
            Set(line, "inventoryItemAccountID", source.InventoryId);
            Set(line, "lineItemDisplayCode", source.Sku);
            Set(line, "skuName", source.Sku);
            Set(line, "description", source.ProductName);
            Set(line, "itemName", source.ProductName);
            Set(line, "quantity", quantity);
            Set(line, "adjustedQuantity", quantity);
            Set(line, "unitOfMeasurementID", source.UnitOfMeasurementId);
            Set(line, "inventoryTypeID", source.InventoryTypeId <= 0 ? 1 : source.InventoryTypeId);
            Set(line, "unitPrice", unitCost);
            Set(line, "cost", unitCost);
            Set(line, "subTotal", amount);
            Set(line, "amount", amount);
            Set(line, "taxableAmount", amount);
            Set(line, "branchID", NormalizeBranchId(gin.BranchId));
            Set(line, "editBranchID", NormalizeBranchId(gin.BranchId));
            Set(line, "financialDate", gin.Date == default ? DateTime.Today : gin.Date);
            Set(line, "documentDisplayCode", document.DisplayCode);
            Set(line, "saveAction", isNew || !hasExistingLine ? "Added" : "Changed");
            Set(line, "isDirty", true);
            updatedLines.Add(line);
        }

        foreach (var removedLine in existingLines.Except(updatedLines))
        {
            if (isNew && ReferenceEquals(removedLine, blankTemplate)) continue;
            Set(removedLine, "saveAction", "Deleted");
            Set(removedLine, "isDirty", true);
            updatedLines.Add(removedLine);
        }

        envelope.DocumentLines = updatedLines;
    }

    private static StockGinViewModel ToViewModel(StockGrnDocumentDTO document) => new()
    {
        DocumentId = document.DocumentID ?? string.Empty,
        DisplayCode = First(document.DisplayCode, document.DocumentID),
        BranchId = First(document.BranchID, document.EditBranchID),
        Date = document.FinancialDate == default ? DateTime.Today : document.FinancialDate,
        IssueType = document.StockActivityType ?? string.Empty,
        AccountId = document.AccountID ?? string.Empty,
        AccountName = document.AccountName ?? string.Empty,
        OrderBranchId = document.OrderBranchID ?? string.Empty,
        ReferenceNumber = document.ReferenceNumber ?? string.Empty,
        Remarks = document.Remarks ?? string.Empty,
        VerifyStatus = document.VerifyStatus ?? string.Empty,
        CreatedByDocumentTypeId = document.CreatedByDocumentTypeID,
        CreatedByDocumentTypeName = document.CreatedByDocumentTypeName ?? string.Empty,
        CreatedByDocumentId = document.CreatedByDocumentID ?? string.Empty,
        CreatedByDocumentDisplayCode = document.CreatedByDocumentDisplayCode ?? string.Empty,
        IsLocked = document.IsLocked,
        IsVoid = document.IsVoid,
        TotalAmount = document.TotalAfterTax != 0 ? document.TotalAfterTax : document.LocalTotalAfterTax
    };

    private static StockGinViewModel ToViewModel(StockGinEnvelopeDTO envelope)
    {
        var gin = envelope.Document is null ? new StockGinViewModel() : ToViewModel(envelope.Document);
        gin.Lines = envelope.DocumentLines
            .Where(line => !string.Equals(ReadString(line, "saveAction"), "Deleted", StringComparison.OrdinalIgnoreCase))
            .Select(line => new StockGinLineViewModel
            {
                DocumentLineId = ReadString(line, "documentLineID"),
                InventoryId = First(ReadString(line, "lineItemID"), ReadString(line, "inventoryItemAccountID")),
                ProductName = First(ReadString(line, "itemName"), ReadString(line, "description")),
                Sku = First(ReadString(line, "lineItemDisplayCode"), ReadString(line, "skuName")),
                Quantity = ReadDecimal(line, "quantity"),
                UnitOfMeasurementId = ReadString(line, "unitOfMeasurementID"),
                InventoryTypeId = Math.Max(1, (int)ReadDecimal(line, "inventoryTypeID")),
                UnitCost = FirstPositive(ReadDecimal(line, "cost"), ReadDecimal(line, "unitPrice"))
            })
            .Where(line => !string.IsNullOrWhiteSpace(line.InventoryId) || !string.IsNullOrWhiteSpace(line.Sku))
            .ToList();
        return gin;
    }

    private static string? Validate(StockGinViewModel gin)
    {
        if (string.IsNullOrWhiteSpace(gin.BranchId)) return "Issuing branch is required.";
        if (string.IsNullOrWhiteSpace(gin.IssueType)) return "Select an issue purpose.";
        if (string.Equals(gin.IssueType, "Supplier Return", StringComparison.OrdinalIgnoreCase) &&
            string.IsNullOrWhiteSpace(gin.AccountId)) return "Select the supplier receiving the returned stock.";
        if (gin.Lines.Count == 0) return "Select at least one product.";
        if (gin.Lines.Any(line => string.IsNullOrWhiteSpace(line.InventoryId))) return "A selected product is missing its inventory ID.";
        if (gin.Lines.Any(line => line.Quantity <= 0)) return "Issue quantity must be greater than zero.";
        return null;
    }

    private static void Set(JsonObject line, string name, string? value) => line[name] = value;
    private static void Set(JsonObject line, string name, int value) => line[name] = value;
    private static void Set(JsonObject line, string name, decimal value) => line[name] = value;
    private static void Set(JsonObject line, string name, bool value) => line[name] = value;
    private static void Set(JsonObject line, string name, DateTime value) => line[name] = value;

    private static string ReadString(JsonObject line, string name)
    {
        var property = line.FirstOrDefault(item => string.Equals(item.Key, name, StringComparison.OrdinalIgnoreCase));
        return property.Value?.ToString().Trim('"') ?? string.Empty;
    }

    private static decimal ReadDecimal(JsonObject line, string name)
    {
        var value = ReadString(line, name);
        return decimal.TryParse(value, NumberStyles.Any, CultureInfo.InvariantCulture, out var invariant)
            ? invariant
            : decimal.TryParse(value, NumberStyles.Any, CultureInfo.CurrentCulture, out var current) ? current : 0;
    }

    private static decimal FirstPositive(params decimal[] values) => values.FirstOrDefault(value => value > 0);

    private static ApiCallResult<bool> ToBoolean(ApiCallResult<string> result, string fallback) =>
        result.Success
            ? ApiCallResult<bool>.Ok(result.StatusCode, true)
            : ApiCallResult<bool>.Failure(result.StatusCode, result.ErrorMessage ?? fallback);

    private static string NormalizeBranchId(string? branchId) =>
        string.IsNullOrWhiteSpace(branchId) ? "HQ" : branchId.Trim().ToUpperInvariant();

    private static string? NullIfWhiteSpace(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static string First(params string?[] values) =>
        values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value)) ?? string.Empty;
}
