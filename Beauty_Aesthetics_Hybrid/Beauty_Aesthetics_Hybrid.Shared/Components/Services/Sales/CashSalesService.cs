using System.Net;
using System.Text.Json;
using System.Text.Json.Nodes;
using Beauty_Aesthetics_WebPos.APIClient;
using Beauty_Aesthetics_WebPos.APIClient.ResultPattern;
using Beauty_Aesthetics_WebPos.Components.Models;
using Beauty_Aesthetics_WebPos.Components.Services.Feedback;
using Beauty_Aesthetics_WebPos.Models.DTOs;

namespace Beauty_Aesthetics_WebPos.Components.Services.Sales;

public sealed class CashSalesService : ICashSalesService
{
    private readonly CashSalesAC cashSalesAC;
    private readonly WebDashboardAC dashboardAC;
    private readonly AppFeedbackService feedback;

    public CashSalesService(
        CashSalesAC cashSalesAC,
        WebDashboardAC dashboardAC,
        AppFeedbackService feedback)
    {
        this.cashSalesAC = cashSalesAC;
        this.dashboardAC = dashboardAC;
        this.feedback = feedback;
    }

    public async Task<ApiCallResult<IReadOnlyList<Transaction>>> LoadTransactionsAsync(
        DateTime startDate, DateTime endDate, string branchId = "", bool resolvePaymentMethods = true, CancellationToken cancellationToken = default)
    {
        var result = await cashSalesAC.GetAppSalesListAsync(new CashSalesLoadRequestDTO
        {
            Id = branchId ?? string.Empty,
            StartDate = startDate.Date,
            EndDate = endDate.Date.AddDays(1).AddTicks(-1)
        }, cancellationToken);

        if (!result.Success || result.Value is null)
            return ApiCallResult<IReadOnlyList<Transaction>>.Failure(result.StatusCode, result.ErrorMessage ?? "Unable to load cash sales.");

        var transactions = result.Value.Select(ToTransaction).OrderByDescending(item => item.Date).ToList();
        if (resolvePaymentMethods)
            await PopulatePaymentMethodsAsync(transactions, cancellationToken);
        return ApiCallResult<IReadOnlyList<Transaction>>.Ok(result.StatusCode, transactions);
    }

    private async Task PopulatePaymentMethodsAsync(
        IReadOnlyList<Transaction> transactions,
        CancellationToken cancellationToken)
    {
        var unresolved = transactions
            .Where(transaction =>
                transaction.Payments.Count == 0 &&
                !string.IsNullOrWhiteSpace(transaction.DocumentId))
            .ToList();
        if (unresolved.Count == 0)
            return;

        var paymentTypesResult = await cashSalesAC.LoadPaymentTypesAsync(cancellationToken);
        var paymentTypeNames = paymentTypesResult.Success && paymentTypesResult.Value is not null
            ? paymentTypesResult.Value
                .Where(payment => payment.POSPaymentTypeID != 0)
                .GroupBy(payment => payment.POSPaymentTypeID)
                .ToDictionary(
                    group => group.Key,
                    group => group.First().POSPaymentTypeName ?? string.Empty)
            : new Dictionary<int, string>();

        using var throttler = new SemaphoreSlim(6);
        await Task.WhenAll(unresolved.Select(async transaction =>
        {
            await throttler.WaitAsync(cancellationToken);
            try
            {
                var receiptResult = await cashSalesAC.LoadReceiptLinesAsync(
                    transaction.DocumentId,
                    cancellationToken);
                if (!receiptResult.Success || receiptResult.Value is null)
                    return;

                ApplyReceiptLines(transaction, receiptResult.Value, paymentTypeNames);
            }
            finally
            {
                throttler.Release();
            }
        }));
    }
    public Task<ApiCallResult<SalesByTypeDTO>> LoadSalesByTypeAsync(
        DateTime startDate, DateTime endDate, string branchId, CancellationToken cancellationToken = default) =>
        dashboardAC.GetSalesByTypeAsync(new DashboardDateRangeRequest
        {
            StartDate = startDate.Date,
            EndDate = endDate.Date.AddDays(1).AddTicks(-1),
            InventoryTypeID = 0,
            BranchID = branchId
        }, cancellationToken);

    public async Task<ApiCallResult<IReadOnlyList<SalesByCollectionDTO>>> LoadSalesByCollectionAsync(
        DateTime startDate, DateTime endDate, string branchId, CancellationToken cancellationToken = default)
    {
        var result = await dashboardAC.GetSalesByCollectionAsync(new DashboardDateRangeRequest
        {
            StartDate = startDate.Date,
            EndDate = endDate.Date.AddDays(1).AddTicks(-1),
            InventoryTypeID = 0,
            BranchID = branchId
        }, cancellationToken);

        return result.Success && result.Value is not null
            ? ApiCallResult<IReadOnlyList<SalesByCollectionDTO>>.Ok(result.StatusCode, result.Value)
            : ApiCallResult<IReadOnlyList<SalesByCollectionDTO>>.Failure(
                result.StatusCode, result.ErrorMessage ?? "Unable to load sales collections.");
    }

    public async Task<ApiCallResult<Transaction>> LoadTransactionAsync(string documentId, CancellationToken cancellationToken = default)
    {
        var result = await cashSalesAC.LoadRecordAsync(documentId, cancellationToken);
        if (!result.Success || result.Value is null)
            return ApiCallResult<Transaction>.Failure(result.StatusCode, result.ErrorMessage ?? "Unable to load the cash sale.");

        var transaction = ToTransaction(result.Value);
        var receiptTask = cashSalesAC.LoadReceiptLinesAsync(documentId, cancellationToken);
        var paymentTypeTask = cashSalesAC.LoadPaymentTypesAsync(cancellationToken);
        await Task.WhenAll(receiptTask, paymentTypeTask);

        var receiptResult = await receiptTask;
        var paymentTypeResult = await paymentTypeTask;
        if (receiptResult.Success && receiptResult.Value is not null)
        {
            var names = paymentTypeResult.Success && paymentTypeResult.Value is not null
                ? paymentTypeResult.Value
                    .Where(payment => payment.POSPaymentTypeID != 0)
                    .GroupBy(payment => payment.POSPaymentTypeID)
                    .ToDictionary(group => group.Key, group => group.First().POSPaymentTypeName ?? string.Empty)
                : new Dictionary<int, string>();
            ApplyReceiptLines(transaction, receiptResult.Value, names);
        }

        return ApiCallResult<Transaction>.Ok(result.StatusCode, transaction);
    }

    public async Task<ApiCallResult<IReadOnlyList<CashSalesPaymentTypeDTO>>> LoadPaymentTypesAsync(CancellationToken cancellationToken = default)
    {
        var result = await cashSalesAC.LoadPaymentTypesAsync(cancellationToken);
        if (!result.Success || result.Value is null)
            return ApiCallResult<IReadOnlyList<CashSalesPaymentTypeDTO>>.Failure(result.StatusCode, result.ErrorMessage ?? "Unable to load payment methods.");

        var values = result.Value
            .Where(item => item.Active && (string.IsNullOrWhiteSpace(item.VisibleInModules) || item.VisibleInModules.Contains("Sales", StringComparison.OrdinalIgnoreCase)))
            .OrderBy(item => item.Sorting)
            .ThenBy(item => item.POSPaymentTypeName)
            .ToList();
        return ApiCallResult<IReadOnlyList<CashSalesPaymentTypeDTO>>.Ok(result.StatusCode, values);
    }

    public async Task<ApiCallResult<Transaction>> CreateTransactionAsync(Transaction transaction, CancellationToken cancellationToken = default)
    {
        var templateResult = await cashSalesAC.LoadRecordAsync(string.Empty, cancellationToken);
        if (!templateResult.Success || templateResult.Value is null)
        {
            var message = templateResult.ErrorMessage ?? "Unable to initialize a cash sale.";
            return ApiCallResult<Transaction>.Failure(templateResult.StatusCode, message);
        }

        var document = (JsonObject)templateResult.Value.DeepClone();
        var header = document["objDoc_CashSales"] as JsonObject ?? new JsonObject();
        ApplyHeader(header, transaction, true);
        document["objDoc_CashSales"] = header;
        document["lstDocumentLine"] = BuildDocumentLines(document, transaction);
        document["lstReceiptLines"] = new JsonArray();

        var createResult = await cashSalesAC.CreateRecordAsync(document, cancellationToken);
        if (!createResult.Success)
        {
            var message = createResult.ErrorMessage ?? "Unable to create the cash sale.";
            return ApiCallResult<Transaction>.Failure(createResult.StatusCode, message);
        }

        transaction.DocumentId = FindString(createResult.Value, "DocumentID", "Id") ?? transaction.DocumentId;
        transaction.InvoiceNumber = FindString(createResult.Value, "DisplayCode") ?? transaction.InvoiceNumber;

        if (!string.IsNullOrWhiteSpace(transaction.DocumentId) &&
            (transaction.Payments.Count > 0 || transaction.PaymentTypeId != 0))
        {
            var paymentResult = await SavePaymentsAsync(transaction, cancellationToken);
            if (!paymentResult.Success)
            {
                var message = paymentResult.ErrorMessage ?? "The sale was created, but its payment could not be saved.";
                return ApiCallResult<Transaction>.Failure(paymentResult.StatusCode, message);
            }
        }

        return ApiCallResult<Transaction>.Ok(createResult.StatusCode, transaction);
    }

    public async Task<ApiCallResult<Transaction>> UpdateTransactionAsync(Transaction transaction, CancellationToken cancellationToken = default)
    {
        var loadResult = await cashSalesAC.LoadRecordAsync(transaction.DocumentId, cancellationToken);
        if (!loadResult.Success || loadResult.Value is null)
        {
            var message = loadResult.ErrorMessage ?? "Unable to load the cash sale for editing.";
            return ApiCallResult<Transaction>.Failure(loadResult.StatusCode, message);
        }

        var header = loadResult.Value["objDoc_CashSales"] as JsonObject;
        if (header is null)
        {
            const string message = "Cash sale header was missing.";
            return ApiCallResult<Transaction>.Failure(HttpStatusCode.OK, message);
        }

        ApplyHeader(header, transaction, false);
        var saveResult = await cashSalesAC.SaveHeaderAsync(header, cancellationToken);
        if (!saveResult.Success)
        {
            var message = saveResult.ErrorMessage ?? "Unable to update the cash sale.";
            return ApiCallResult<Transaction>.Failure(saveResult.StatusCode, message);
        }

        if (transaction.Payments.Count > 0 || transaction.PaymentTypeId != 0)
        {
            var paymentResult = await SavePaymentsAsync(transaction, cancellationToken);
            if (!paymentResult.Success)
            {
                var message = paymentResult.ErrorMessage ?? "The sale was updated, but its payment could not be saved.";
                return ApiCallResult<Transaction>.Failure(paymentResult.StatusCode, message);
            }
        }

        return ApiCallResult<Transaction>.Ok(saveResult.StatusCode, transaction);
    }

    public async Task<ApiCallResult<string>> DeleteTransactionAsync(
        Transaction transaction,
        string reason,
        CancellationToken cancellationToken = default)
    {
        var result = await cashSalesAC.DeleteAsync(new CashSalesDeleteDTO
        {
            Id = transaction.DocumentId,
            DocumentId = transaction.DocumentId,
            BranchId = transaction.BranchId,
            FinancialDate = transaction.Date,
            Reason = reason
        }, cancellationToken);

        if (result.Success)
        {
        }
        else
        {
        }

        return result;
    }

    private async Task<ApiCallResult<string>> SavePaymentsAsync(Transaction transaction, CancellationToken cancellationToken)
    {
        var existingResult = await cashSalesAC.LoadReceiptLinesAsync(transaction.DocumentId, cancellationToken);
        var existing = existingResult.Success && existingResult.Value is not null
            ? existingResult.Value.ToList()
            : new List<CashSalesReceiptLineDTO>();
        var payments = transaction.Payments.Count > 0
            ? transaction.Payments
            : new List<TransactionPayment>
            {
                new() { PaymentTypeId = transaction.PaymentTypeId, PaymentMethod = transaction.PaymentMethod, Amount = transaction.Amount }
            };
        var change = Math.Max(0, payments.Sum(payment => payment.Amount) - transaction.Amount);
        var changePayment = payments.LastOrDefault(payment =>
            payment.PaymentMethod.Contains("cash", StringComparison.OrdinalIgnoreCase)) ?? payments.Last();

        var retainedReceiptIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var matchedReceipts = new HashSet<CashSalesReceiptLineDTO>();
        foreach (var payment in payments.Where(payment => payment.PaymentTypeId != 0 && payment.Amount > 0))
        {
            var receipt = existing.FirstOrDefault(line =>
                              !matchedReceipts.Contains(line) &&
                              !string.IsNullOrWhiteSpace(payment.ReceiptLineId) &&
                              string.Equals(line.POSReceiptLineID, payment.ReceiptLineId, StringComparison.OrdinalIgnoreCase))
                ?? existing.FirstOrDefault(line =>
                    !matchedReceipts.Contains(line) && line.POSPaymentTypeID == payment.PaymentTypeId)
                ?? new CashSalesReceiptLineDTO();
            matchedReceipts.Add(receipt);
            receipt.DocumentID = transaction.DocumentId;
            receipt.AccountID = transaction.AccountId;
            receipt.Reference = transaction.ReferenceNumber;
            receipt.Description = payment.PaymentMethod;
            receipt.POSReceiptLineAmount = payment.Amount;
            receipt.POSReceiptChangeAmount = ReferenceEquals(payment, changePayment) ? change : 0;
            receipt.POSPaymentTypeID = payment.PaymentTypeId;
            receipt.BranchID = transaction.BranchId;
            receipt.CurrencyID = "MYR";
            receipt.ExchangeRate = 1;
            receipt.FinancialDate = transaction.Date;
            receipt.SaveAction = string.IsNullOrWhiteSpace(receipt.POSReceiptLineID) ? 1 : 2;
            receipt.IsDirty = true;

            var saveResult = await cashSalesAC.SaveReceiptLineAsync(receipt, cancellationToken);
            if (!saveResult.Success)
                return saveResult;

            payment.ReceiptLineId = receipt.POSReceiptLineID ?? payment.ReceiptLineId;
            if (!string.IsNullOrWhiteSpace(receipt.POSReceiptLineID))
                retainedReceiptIds.Add(receipt.POSReceiptLineID);
        }

        foreach (var removed in existing.Where(line =>
                     !string.IsNullOrWhiteSpace(line.POSReceiptLineID) &&
                     !retainedReceiptIds.Contains(line.POSReceiptLineID!)))
        {
            removed.SaveAction = 3;
            removed.IsDirty = true;
            var removeResult = await cashSalesAC.SaveReceiptLineAsync(removed, cancellationToken);
            if (!removeResult.Success)
                return removeResult;
        }

        return ApiCallResult<string>.Ok(HttpStatusCode.OK, "Payments saved successfully.");
    }

    private static Transaction ToTransaction(CashSalesProxyDTO source)
    {
        var itemCount = source.ItemCount > 0 ? source.ItemCount : GetInt(source.AdditionalData, "TotalItems", "LineCount", "Quantity");
        var payment = First(source.PaymentMethod, GetString(source.AdditionalData, "POSPaymentTypeName", "PaymentTypeName", "CollectionType"));
        var type = First(GetString(source.AdditionalData, "InventoryTypeName", "SalesType", "ItemType"), "Mixed");
        var status = source.IsVoid ? "Cancelled" : First(source.PaymentStatus, GetString(source.AdditionalData, "Status", "PaymentStatus"), "Paid");
        return new Transaction
        {
            DocumentId = source.DocumentID ?? string.Empty,
            AccountId = source.AccountID ?? string.Empty,
            BranchId = source.BranchID ?? string.Empty,
            InvoiceNumber = First(source.DisplayCode, source.DocumentID, "-")!,
            Date = source.FinancialDate == default ? DateTime.Today : source.FinancialDate,
            CustomerName = First(source.AccountName, "Walk-in Customer")!,
            Branch = First(source.BranchID, "-")!,
            Type = NormalizeType(type),
            ItemCount = itemCount,
            Amount = source.TotalAfterTax,
            Subtotal = source.TotalBeforeTax,
            Tax = source.TaxAmount,
            PaymentMethod = First(payment, "-")!,
            Status = NormalizeStatus(status),
            ReferenceNumber = source.ReferenceNumber ?? string.Empty,
            Notes = source.Remarks ?? string.Empty,
            CreatedBy = First(source.CashierName, source.SalesPersonName, string.Empty)!
        };
    }

    private static Transaction ToTransaction(JsonObject document)
    {
        var header = document["objDoc_CashSales"] as JsonObject ?? new JsonObject();
        var lines = document["lstDocumentLine"] as JsonArray ?? new JsonArray();
        var receipts = document["lstReceiptLines"] as JsonArray ?? new JsonArray();
        var transaction = new Transaction
        {
            DocumentId = Text(header, "DocumentID"),
            AccountId = Text(header, "AccountID"),
            BranchId = Text(header, "BranchID"),
            InvoiceNumber = First(Text(header, "DisplayCode"), Text(header, "DocumentID"), "-")!,
            Date = DateValue(header, "FinancialDate") ?? DateTime.Today,
            CustomerName = First(Text(header, "AccountName"), "Walk-in Customer")!,
            CustomerContact = Text(header, "Phone"),
            Branch = First(Text(header, "BranchID"), "-")!,
            ItemCount = lines.Count,
            Amount = Number(header, "TotalAfterTax"),
            Subtotal = Number(header, "TotalBeforeTax"),
            Tax = Number(header, "TaxAmount"),
            Discount = lines.Sum(line => Number(line as JsonObject, "DiscountAmount")),
            Status = Bool(header, "IsVoid") ? "Cancelled" : "Paid",
            ReferenceNumber = Text(header, "ReferenceNumber"),
            Notes = Text(header, "Remarks"),
            CreatedBy = First(Text(header, "CashierName"), Text(header, "SalesPersonName"), Text(header, "CreatedBy"), string.Empty)!,
            Items = lines.Select(ToTransactionItem).ToList()
        };

        transaction.Payments = receipts.OfType<JsonObject>()
            .Select(receipt => new TransactionPayment
            {
                ReceiptLineId = Text(receipt, "POSReceiptLineID"),
                PaymentTypeId = Integer(receipt, "POSPaymentTypeID"),
                PaymentMethod = First(Text(receipt, "POSPaymentTypeName"), Text(receipt, "Description"), "Payment")!,
                Amount = Number(receipt, "POSReceiptLineAmount")
            })
            .Where(payment => payment.PaymentTypeId != 0 && payment.Amount > 0)
            .ToList();
        if (transaction.Payments.Count > 0)
        {
            transaction.PaymentTypeId = transaction.Payments[0].PaymentTypeId;
            transaction.PaymentMethod = string.Join(" + ", transaction.Payments
                .Select(payment => payment.PaymentMethod)
                .Distinct(StringComparer.OrdinalIgnoreCase));
        }

        var categories = transaction.Items.Select(item => item.Category).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        transaction.Type = categories.Count == 1 ? categories[0] : "Mixed";
        return transaction;
    }

    private static TransactionItem ToTransactionItem(JsonNode? node)
    {
        var line = node as JsonObject;
        var inventoryType = Integer(line, "InventoryTypeID");
        var quantity = Number(line, "Quantity");
        var unitPrice = Number(line, "UnitPrice");
        var total = Number(line, "SubTotal");
        return new TransactionItem
        {
            InventoryId = Text(line, "InventoryID"),
            Name = First(Text(line, "ItemName"), Text(line, "Description"), "Item")!,
            Description = Text(line, "Description"),
            Category = inventoryType == 3 ? "Service" : "Product",
            Quantity = (int)Math.Max(1, quantity),
            UnitPrice = unitPrice,
            TotalPrice = total != 0 ? total : quantity * unitPrice,
            Discount = Number(line, "DiscountAmount")
        };
    }

    private static JsonArray BuildDocumentLines(JsonObject document, Transaction transaction)
    {
        var template = document["ServiceChargeLine"] as JsonObject;
        var items = transaction.Items.Any()
            ? transaction.Items
            : new List<TransactionItem>
            {
                new()
                {
                    Name = $"{transaction.Type} Item",
                    Category = transaction.Type,
                    Quantity = Math.Max(1, transaction.ItemCount),
                    UnitPrice = transaction.Subtotal / Math.Max(1, transaction.ItemCount),
                    TotalPrice = transaction.Subtotal
                }
            };

        var lines = new JsonArray();
        var lineOrder = 1;
        foreach (var item in items)
        {
            var quantity = Math.Max(1, item.Quantity);
            var gross = quantity * item.UnitPrice;
            var discount = Math.Clamp(item.Discount, 0, gross);
            var lineTotal = Math.Max(0, gross - discount);
            var line = template is null ? new JsonObject() : (JsonObject)template.DeepClone();
            line["DocumentLineID"] = string.Empty;
            line["DocumentID"] = string.Empty;
            line["InventoryID"] = item.InventoryId;
            line["LineOrder"] = lineOrder++;
            line["Description"] = First(item.Description, item.Name, $"{transaction.Type} Item");
            line["ItemName"] = First(item.Name, item.Description, $"{transaction.Type} Item");
            line["Quantity"] = quantity;
            line["UnitPrice"] = item.UnitPrice;
            line["DiscountAmount"] = discount;
            line["SubTotal"] = lineTotal;
            line["Amount"] = lineTotal;
            line["TaxAmount"] = 0;
            line["InventoryTypeID"] = InventoryTypeFor(item.Category);
            line["BranchID"] = transaction.BranchId;
            line["FinancialDate"] = transaction.Date;
            line["SaveAction"] = 1;
            line["IsDirty"] = true;
            lines.Add(line);
        }

        return lines;
    }

    private static void ApplyReceiptLines(
        Transaction transaction,
        IEnumerable<CashSalesReceiptLineDTO> receiptLines,
        IReadOnlyDictionary<int, string> paymentTypeNames)
    {
        transaction.Payments = receiptLines
            .Where(line => line.POSPaymentTypeID != 0 && line.POSReceiptLineAmount > 0)
            .Select(receipt => new TransactionPayment
            {
                ReceiptLineId = receipt.POSReceiptLineID ?? string.Empty,
                PaymentTypeId = receipt.POSPaymentTypeID,
                PaymentMethod = First(receipt.POSPaymentTypeName, receipt.Description,
                    paymentTypeNames.GetValueOrDefault(receipt.POSPaymentTypeID), "Payment")!,
                Amount = receipt.POSReceiptLineAmount
            })
            .ToList();

        if (transaction.Payments.Count == 0)
            return;

        transaction.PaymentTypeId = transaction.Payments[0].PaymentTypeId;
        transaction.PaymentMethod = string.Join(" + ", transaction.Payments
            .Select(payment => payment.PaymentMethod)
            .Distinct(StringComparer.OrdinalIgnoreCase));
    }

    private static int InventoryTypeFor(string? category)
    {
        if (category?.Contains("Service", StringComparison.OrdinalIgnoreCase) == true) return 3;
        if (category?.Contains("Package", StringComparison.OrdinalIgnoreCase) == true) return 4;
        if (category?.Contains("Voucher", StringComparison.OrdinalIgnoreCase) == true) return 5;
        return 1;
    }

    private static void ApplyHeader(JsonObject header, Transaction transaction, bool isNew)
    {
        var now = DateTime.Now;
        header["DocumentTypeID"] = 5;
        header["FriendlyDocumentName"] = "CashSales";
        header["BranchID"] = transaction.BranchId;
        header["EditBranchID"] = transaction.BranchId;
        header["FinancialDate"] = transaction.Date;
        header["AccountID"] = transaction.AccountId;
        header["AccountName"] = transaction.CustomerName;
        header["ReferenceNumber"] = transaction.ReferenceNumber;
        header["TotalBeforeTax"] = transaction.Subtotal;
        header["TaxableAmount"] = transaction.Subtotal;
        header["TaxAmount"] = transaction.Tax;
        header["RoundingAmount"] = 0;
        header["TotalAfterTax"] = transaction.Amount;
        header["ExchangeRate"] = 1;
        header["LocalTotalBeforeTax"] = transaction.Subtotal;
        header["LocalTaxableAmount"] = transaction.Subtotal;
        header["LocalTaxAmount"] = transaction.Tax;
        header["LocalRoundingAmount"] = 0;
        header["LocalTotalAfterTax"] = transaction.Amount;
        header["TransactionCurrencyID"] = "MYR";
        header["LocalCurrencyID"] = "MYR";
        header["TransactionCurrencyName"] = "MYR";
        header["LocalCurrencyName"] = "MYR";
        header["Remarks"] = transaction.Notes;
        header["Phone"] = transaction.CustomerContact;
        header["ModifiedDateTime"] = now;
        header["UpdateTimeStamp"] = now;
        header["IsVoid"] = string.Equals(transaction.Status, "Cancelled", StringComparison.OrdinalIgnoreCase);
        header["IsDirty"] = true;
        header["SaveAction"] = isNew ? 1 : 0;
        if (isNew)
        {
            header["DocumentID"] = string.Empty;
            header["DisplayCode"] = string.Empty;
            header["CreatedDateTime"] = now;
        }
    }

    private static string NormalizeType(string? value) => value?.Contains("Service", StringComparison.OrdinalIgnoreCase) == true ? "Service" : value?.Contains("Product", StringComparison.OrdinalIgnoreCase) == true ? "Product" : "Mixed";
    private static string NormalizeStatus(string? value) => value?.Contains("void", StringComparison.OrdinalIgnoreCase) == true || value?.Contains("cancel", StringComparison.OrdinalIgnoreCase) == true ? "Cancelled" : value?.Contains("pending", StringComparison.OrdinalIgnoreCase) == true ? "Pending" : "Paid";
    private static string Text(JsonObject? source, string name) => source?[name]?.GetValue<string?>() ?? string.Empty;
    private static decimal Number(JsonObject? source, string name) => source?[name] is JsonValue value && value.TryGetValue<decimal>(out var number) ? number : 0;
    private static int Integer(JsonObject? source, string name) => source?[name] is JsonValue value && value.TryGetValue<int>(out var number) ? number : 0;
    private static bool Bool(JsonObject? source, string name) => source?[name] is JsonValue value && value.TryGetValue<bool>(out var result) && result;
    private static DateTime? DateValue(JsonObject? source, string name) => source?[name] is JsonValue value && value.TryGetValue<DateTime>(out var result) ? result : null;

    private static string? GetString(Dictionary<string, JsonElement>? values, params string[] names)
    {
        if (values is null) return null;
        foreach (var name in names)
        {
            var match = values.FirstOrDefault(item => string.Equals(item.Key, name, StringComparison.OrdinalIgnoreCase));
            if (!string.IsNullOrEmpty(match.Key) && match.Value.ValueKind == JsonValueKind.String) return match.Value.GetString();
        }
        return null;
    }

    private static int GetInt(Dictionary<string, JsonElement>? values, params string[] names)
    {
        if (values is null) return 0;
        foreach (var name in names)
        {
            var match = values.FirstOrDefault(item => string.Equals(item.Key, name, StringComparison.OrdinalIgnoreCase));
            if (!string.IsNullOrEmpty(match.Key) && match.Value.TryGetInt32(out var result)) return result;
        }
        return 0;
    }

    private static string? FindString(JsonElement value, params string[] names)
    {
        if (value.ValueKind == JsonValueKind.String) return value.GetString();
        if (value.ValueKind != JsonValueKind.Object) return null;
        foreach (var property in value.EnumerateObject())
        {
            if (names.Any(name => string.Equals(property.Name, name, StringComparison.OrdinalIgnoreCase)) && property.Value.ValueKind == JsonValueKind.String)
                return property.Value.GetString();
            if (property.Value.ValueKind == JsonValueKind.Object)
            {
                var nested = FindString(property.Value, names);
                if (!string.IsNullOrWhiteSpace(nested)) return nested;
            }
        }
        return null;
    }

    private static string? First(params string?[] values) => values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value));
}
