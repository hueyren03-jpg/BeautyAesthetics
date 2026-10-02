using System.Net;
using System.Text.Json;
using System.Text.Json.Nodes;
using Beauty_Aesthetics_WebPos.APIClient;
using Beauty_Aesthetics_WebPos.APIClient.ResultPattern;
using Beauty_Aesthetics_WebPos.Components.Models;
using Beauty_Aesthetics_WebPos.Components.Services.Feedback;
using Beauty_Aesthetics_WebPos.Components.Services.Files;
using Beauty_Aesthetics_WebPos.Components.Services.Inventory;
using Beauty_Aesthetics_WebPos.Components.Services.Customers;
using Beauty_Aesthetics_WebPos.Components.Services.Printing;
using Beauty_Aesthetics_WebPos.Components.Services.Tax;
using Beauty_Aesthetics_WebPos.Models.DTOs;
using Microsoft.JSInterop;

namespace Beauty_Aesthetics_WebPos.Components.Services.Sales;

public sealed class CashSalesService : ICashSalesService
{
    private readonly CashSalesAC cashSalesAC;
    private readonly BranchAC branchAC;
    private readonly WebDashboardAC dashboardAC;
    private readonly AppFeedbackService feedback;
    private readonly IFileDownloadService fileDownloadService;
    private readonly IMemberCreditService memberCreditService;
    private readonly ICustomerService customerService;
    private readonly IMemberCreditWalletService memberCreditWalletService;
    private readonly IJSRuntime jsRuntime;

    public CashSalesService(
        CashSalesAC cashSalesAC,
        BranchAC branchAC,
        WebDashboardAC dashboardAC,
        AppFeedbackService feedback,
        IFileDownloadService fileDownloadService,
        IMemberCreditService memberCreditService,
        ICustomerService customerService,
        IMemberCreditWalletService memberCreditWalletService,
        IJSRuntime jsRuntime)
    {
        this.cashSalesAC = cashSalesAC;
        this.branchAC = branchAC;
        this.dashboardAC = dashboardAC;
        this.feedback = feedback;
        this.fileDownloadService = fileDownloadService;
        this.memberCreditService = memberCreditService;
        this.customerService = customerService;
        this.memberCreditWalletService = memberCreditWalletService;
        this.jsRuntime = jsRuntime;
    }

    public async Task<ApiCallResult<IReadOnlyList<Transaction>>> LoadTransactionsAsync(
        DateTime startDate, DateTime endDate, string branchId = "", bool resolvePaymentMethods = true, CancellationToken cancellationToken = default)
    {
        var normalizedBranchId = branchId?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(normalizedBranchId))
        {
            Console.WriteLine(
                "[Cash Sales Load] BLOCKED | GetAppSalesList requires a working branch; strBranches was empty.");

            return ApiCallResult<IReadOnlyList<Transaction>>.Failure(
                HttpStatusCode.BadRequest,
                "Working branch is required before sales history can be loaded.");
        }

        var result = await cashSalesAC.GetAppSalesListAsync(new CashSalesLoadRequestDTO
        {
            Id = normalizedBranchId,
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

    public async Task<ApiCallResult<IReadOnlyList<Transaction>>> LoadRedemptionsAsync(
        DateTime startDate,
        DateTime endDate,
        string branchId = "",
        CancellationToken cancellationToken = default)
    {
        var result = await cashSalesAC.LoadRedemptionProxyAsync(
            new RedemptionLoadRequestDTO
            {
                Id = branchId ?? string.Empty,
                StartDate = startDate.Date,
                EndDate = endDate.Date.AddDays(1).AddTicks(-1)
            },
            cancellationToken);

        if (!result.Success || result.Value is null)
        {
            return ApiCallResult<IReadOnlyList<Transaction>>.Failure(
                result.StatusCode,
                result.ErrorMessage ?? "Unable to load redemption history.");
        }

        var redemptions = result.Value
            .Select(ToTransaction)
            .Select(transaction =>
            {
                transaction.DocumentTypeId = 52;
                transaction.Type = "Redemption";
                transaction.PaymentTypeId = -10;
                transaction.PaymentMethod = "Member Credit";
                return transaction;
            })
            .OrderByDescending(transaction => transaction.Date)
            .ToList();

        return ApiCallResult<IReadOnlyList<Transaction>>.Ok(result.StatusCode, redemptions);
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

    public Task<ApiCallResult<Transaction>> LoadTransactionAsync(
        string documentId,
        CancellationToken cancellationToken = default) =>
        LoadTransactionAsync(documentId, 5, cancellationToken);

    public async Task<ApiCallResult<Transaction>> LoadTransactionAsync(
        string documentId,
        int documentTypeId,
        CancellationToken cancellationToken = default)
    {
        var result = documentTypeId == 52
            ? await cashSalesAC.LoadRedemptionRecordAsync(documentId, cancellationToken)
            : await cashSalesAC.LoadRecordAsync(documentId, cancellationToken);

        if (!result.Success || result.Value is null)
        {
            return ApiCallResult<Transaction>.Failure(
                result.StatusCode,
                result.ErrorMessage ??
                (documentTypeId == 52
                    ? "Unable to load the redemption."
                    : "Unable to load the cash sale."));
        }

        var transaction = ToTransaction(result.Value);
        transaction.DocumentTypeId = documentTypeId == 52 ? 52 : transaction.DocumentTypeId;

        if (string.IsNullOrWhiteSpace(transaction.DocumentId))
        {
            transaction.DocumentId = documentId;
        }

        var receiptTask = cashSalesAC.LoadReceiptLinesAsync(documentId, cancellationToken);
        var paymentTypeTask = cashSalesAC.LoadPaymentTypesAsync(cancellationToken);
        await Task.WhenAll(receiptTask, paymentTypeTask);

        var receiptResult = await receiptTask;
        var paymentTypeResult = await paymentTypeTask;
        if (receiptResult.Success &&
            receiptResult.Value is not null &&
            receiptResult.Value.Count > 0)
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

    public async Task<ApiCallResult<string>> RequestInvoiceLinkAsync(
        string documentId,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(documentId))
        {
            return ApiCallResult<string>.Failure(
                HttpStatusCode.BadRequest,
                "A completed cash sale document is required before requesting an invoice.");
        }

        var loadResult = await cashSalesAC.LoadRecordAsync(documentId, cancellationToken);
        if (!loadResult.Success || loadResult.Value is null)
        {
            return ApiCallResult<string>.Failure(
                loadResult.StatusCode,
                loadResult.ErrorMessage ?? "Unable to load the completed cash sale.");
        }

        var header = loadResult.Value["objDoc_CashSales"] as JsonObject;
        if (header is null)
        {
            return ApiCallResult<string>.Failure(
                HttpStatusCode.OK,
                "The completed cash sale did not contain an invoice header.");
        }

        var savedDocumentId = Text(header, "DocumentID");
        if (string.IsNullOrWhiteSpace(savedDocumentId))
        {
            savedDocumentId = documentId;
        }

        var documentTypeId = Integer(header, "DocumentTypeID");
        if (documentTypeId == 0)
        {
            documentTypeId = 5;
        }

        var financialDate = DateValue(header, "FinancialDate") ?? DateTime.Now;

        var invoiceResult = await cashSalesAC.RequestBillDownloadLinkAsync(
            new CashSalesBillLinkRequestDTO
            {
                DocumentTypeId = documentTypeId,
                DocumentId = savedDocumentId,
                FinancialDate = financialDate
            },
            cancellationToken);

        if (!invoiceResult.Success || string.IsNullOrWhiteSpace(invoiceResult.Value))
        {
            return ApiCallResult<string>.Failure(
                invoiceResult.StatusCode,
                invoiceResult.ErrorMessage ?? "The invoice link was empty.");
        }

        return ApiCallResult<string>.Ok(invoiceResult.StatusCode, invoiceResult.Value);
    }

    public async Task<ApiCallResult<string>> RequestEInvoiceLinkAsync(
        string documentId,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(documentId))
        {
            return ApiCallResult<string>.Failure(
                HttpStatusCode.BadRequest,
                "A completed cash sale document is required before requesting an e-Invoice.");
        }

        var loadResult = await cashSalesAC.LoadRecordAsync(documentId, cancellationToken);
        if (!loadResult.Success || loadResult.Value is null)
        {
            return ApiCallResult<string>.Failure(
                loadResult.StatusCode,
                loadResult.ErrorMessage ?? "Unable to load the completed cash sale.");
        }

        var header = loadResult.Value["objDoc_CashSales"] as JsonObject;
        if (header is null)
        {
            return ApiCallResult<string>.Failure(
                HttpStatusCode.OK,
                "The completed cash sale did not contain an e-Invoice header.");
        }

        var savedDocumentId = TextIgnoreCase(header, "DocumentID");
        if (string.IsNullOrWhiteSpace(savedDocumentId))
        {
            savedDocumentId = documentId;
        }

        var status = TextIgnoreCase(header, "eInvoiceStatus");
        var documentUid = TextIgnoreCase(header, "eInvoiceDocumentUid");
        var longId = TextIgnoreCase(header, "eInvoiceLongId");

        if (string.Equals(status, "Valid", StringComparison.OrdinalIgnoreCase) &&
            !string.IsNullOrWhiteSpace(documentUid) &&
            !string.IsNullOrWhiteSpace(longId))
        {
            var branchId = TextIgnoreCase(header, "BranchID");
            var financialDate = DateValueIgnoreCase(header, "FinancialDate") ?? DateTime.Now;

            if (!string.IsNullOrWhiteSpace(branchId))
            {
                var branchResult = await branchAC.LoadRecordAsync(branchId, cancellationToken);
                if (branchResult.Success && branchResult.Value is not null)
                {
                    var liveDate = FindDateIgnoreCase(branchResult.Value, "eInvoiceLiveDate");
                    if (liveDate.HasValue)
                    {
                        var baseUrl = liveDate.Value > DateTime.MinValue &&
                                      financialDate >= liveDate.Value
                            ? "https://myinvois.hasil.gov.my"
                            : "https://preprod.myinvois.hasil.gov.my";

                        var existingUrl = $"{baseUrl}/{documentUid}/share/{longId}";
                        return ApiCallResult<string>.Ok(loadResult.StatusCode, existingUrl);
                    }
                }
            }
        }

        var submitResult = await cashSalesAC.RequestEInvoiceDirectSubmitAsync(
            savedDocumentId,
            cancellationToken);

        if (!submitResult.Success || string.IsNullOrWhiteSpace(submitResult.Value))
        {
            return ApiCallResult<string>.Failure(
                submitResult.StatusCode,
                submitResult.ErrorMessage ?? "The e-Invoice link was empty.");
        }

        return ApiCallResult<string>.Ok(submitResult.StatusCode, submitResult.Value);
    }

    public Task<ApiCallResult<string>> RequestReceiptPdfAsync(
        string documentId,
        CancellationToken cancellationToken = default) =>
        RequestReceiptPdfAsync(documentId, 5, cancellationToken);

    public async Task<ApiCallResult<string>> RequestReceiptPdfAsync(
        string documentId,
        int documentTypeId,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(documentId))
        {
            return ApiCallResult<string>.Failure(
                HttpStatusCode.BadRequest,
                "A completed sales document is required before requesting a receipt.");
        }

        var resolvedDocumentTypeId = documentTypeId == 52 ? 52 : 5;
        ApiCallResult<JsonObject>? loadResult = null;

        // Match Senang Retail: normal receipts default to type 5 and become
        // redemption statements only when a saved line is owned by type 52.
        if (resolvedDocumentTypeId == 52)
        {
            loadResult = await cashSalesAC.LoadRedemptionRecordAsync(
                documentId,
                cancellationToken);
        }
        else
        {
            loadResult = await cashSalesAC.LoadRecordAsync(documentId, cancellationToken);
            if (loadResult.Success && loadResult.Value is not null)
            {
                var lines = loadResult.Value["lstDocumentLine"] as JsonArray;
                if (lines is not null && lines.OfType<JsonObject>().Any(line =>
                        IntegerIgnoreCase(line, "OwnerDocumentTypeID") == 52))
                {
                    resolvedDocumentTypeId = 52;
                }
            }
        }

        // Receipt generation must be read-only for redemption documents.
        // A saved redemption (DocumentTypeID 52) is already the accounting source
        // of truth, so never run the legacy cash-sale repair/save path against it.
        if (resolvedDocumentTypeId == 52)
        {
            if (loadResult?.Success != true || loadResult.Value is null)
            {
                return ApiCallResult<string>.Failure(
                    loadResult?.StatusCode ?? HttpStatusCode.BadRequest,
                    loadResult?.ErrorMessage ?? "Unable to load the completed redemption.");
            }
        }
        else if (loadResult?.Success == true && loadResult.Value is not null)
        {
            // Keep the existing repair behavior for legacy normal cash-sale receipts only.
            var repairResult = await RepairReceiptAmountsAsync(
                loadResult.Value,
                documentId,
                resolvedDocumentTypeId,
                cancellationToken);

            if (!repairResult.Success)
            {
                return ApiCallResult<string>.Failure(
                    repairResult.StatusCode,
                    repairResult.ErrorMessage ?? "Unable to repair the receipt amounts.");
            }
        }

        ApiCallResult<string> receiptResult;
        try
        {
            receiptResult = await cashSalesAC.GetThermalReceiptPdfAsync(
                documentId,
                resolvedDocumentTypeId,
                cancellationToken);
        }
        catch (JsonException ex)
        {
            Console.WriteLine(
                $"[Receipt PDF] Invalid response from thermal receipt API | DocumentID={documentId} | DocumentTypeID={resolvedDocumentTypeId} | {ex.Message}");

            return ApiCallResult<string>.Failure(
                HttpStatusCode.BadGateway,
                "The receipt API returned an empty or invalid response.");
        }

        if (!receiptResult.Success || string.IsNullOrWhiteSpace(receiptResult.Value))
        {
            Console.WriteLine(
                $"[Receipt PDF] Thermal receipt API failed | DocumentID={documentId} | DocumentTypeID={resolvedDocumentTypeId} | Status={receiptResult.StatusCode} | Error={receiptResult.ErrorMessage}");

            return ApiCallResult<string>.Failure(
                receiptResult.StatusCode,
                receiptResult.ErrorMessage ?? "The receipt PDF was empty.");
        }

        return ApiCallResult<string>.Ok(receiptResult.StatusCode, receiptResult.Value);
    }

    private async Task<ApiCallResult<bool>> RepairReceiptAmountsAsync(
        JsonObject document,
        string documentId,
        int documentTypeId,
        CancellationToken cancellationToken)
    {
        var header = document["objDoc_CashSales"] as JsonObject;
        var lines = document["lstDocumentLine"] as JsonArray;
        if (header is null || lines is null)
        {
            return ApiCallResult<bool>.Ok(HttpStatusCode.OK, false);
        }

        var activeLines = lines
            .OfType<JsonObject>()
            .Where(line =>
                IntegerIgnoreCase(line, "SaveAction") != 3 &&
                !string.Equals(
                    TextIgnoreCase(line, "SaveAction"),
                    "Deleted",
                    StringComparison.OrdinalIgnoreCase))
            .ToList();

        if (activeLines.Count == 0)
        {
            return ApiCallResult<bool>.Ok(HttpStatusCode.OK, false);
        }

        var receiptTotal = 0m;
        if (document["lstReceiptLines"] is JsonArray receipts)
        {
            receiptTotal = Math.Round(
                receipts.OfType<JsonObject>().Sum(receipt =>
                    Math.Max(
                        0m,
                        Number(receipt, "POSReceiptLineAmount") -
                        Math.Abs(Number(receipt, "POSReceiptChangeAmount")))),
                2,
                MidpointRounding.AwayFromZero);
        }

        var headerTotal = Math.Round(
            Math.Max(0m, Number(header, "TotalAfterTax")),
            2,
            MidpointRounding.AwayFromZero);
        var singleLineFallbackTotal = activeLines.Count == 1
            ? Math.Max(headerTotal, receiptTotal)
            : 0m;
        var repairedAnyLine = false;

        foreach (var line in activeLines)
        {
            var quantity = Number(line, "Quantity");
            if (quantity <= 0m)
            {
                quantity = 1m;
            }

            var discount = Math.Max(
                0m,
                Math.Max(
                    Number(line, "Discount"),
                    Number(line, "DiscountAmount")));
            var unitPrice = Math.Max(0m, Number(line, "UnitPrice"));
            var beforeTax = Math.Max(
                0m,
                Math.Max(
                    Number(line, "SubTotalBeforeGST"),
                    Number(line, "ConvertedSubTotalBeforeGST")));
            var taxAmount = Math.Max(
                0m,
                Math.Max(
                    Number(line, "TaxAmount"),
                    Number(line, "ConvertedTaxAmount")));
            var lineTotal = Math.Max(
                0m,
                Math.Max(
                    Number(line, "SubTotal"),
                    Math.Max(
                        Number(line, "Amount"),
                        Number(line, "ConvertedAmount"))));
            var storedTaxRate = Math.Max(0m, Number(line, "TaxPercentage"));
            var calculationTaxRate = storedTaxRate > 1m
                ? storedTaxRate / 100m
                : storedTaxRate;
            var isTaxInclusive = Bool(line, "IsTaxInclusive");

            if (beforeTax <= 0m && unitPrice > 0m)
            {
                OrderLineTaxCalculator.ComputeLineAmounts(
                    unitPrice,
                    quantity,
                    discount,
                    calculationTaxRate,
                    isTaxInclusive,
                    out beforeTax,
                    out taxAmount);
                lineTotal = Math.Round(
                    beforeTax + taxAmount,
                    2,
                    MidpointRounding.AwayFromZero);
            }
            else if (beforeTax <= 0m)
            {
                var recoverableTotal = lineTotal > 0m
                    ? lineTotal
                    : singleLineFallbackTotal;
                if (recoverableTotal > 0m)
                {
                    beforeTax = calculationTaxRate > 0m
                        ? Math.Round(
                            recoverableTotal / (1m + calculationTaxRate),
                            2,
                            MidpointRounding.AwayFromZero)
                        : recoverableTotal;
                    taxAmount = Math.Round(
                        Math.Max(0m, recoverableTotal - beforeTax),
                        2,
                        MidpointRounding.AwayFromZero);
                    lineTotal = Math.Round(
                        beforeTax + taxAmount,
                        2,
                        MidpointRounding.AwayFromZero);
                }
            }
            else
            {
                if (taxAmount <= 0m && calculationTaxRate > 0m)
                {
                    taxAmount = Math.Round(
                        beforeTax * calculationTaxRate,
                        2,
                        MidpointRounding.AwayFromZero);
                }

                if (lineTotal <= 0m)
                {
                    lineTotal = Math.Round(
                        beforeTax + taxAmount,
                        2,
                        MidpointRounding.AwayFromZero);
                }
            }

            if (unitPrice <= 0m && (beforeTax > 0m || lineTotal > 0m))
            {
                var priceBasis = isTaxInclusive && lineTotal > 0m
                    ? lineTotal
                    : beforeTax;
                unitPrice = Math.Round(
                    (priceBasis + discount) / quantity,
                    2,
                    MidpointRounding.AwayFromZero);
            }

            beforeTax = Math.Round(beforeTax, 2, MidpointRounding.AwayFromZero);
            taxAmount = Math.Round(taxAmount, 2, MidpointRounding.AwayFromZero);
            lineTotal = Math.Round(lineTotal, 2, MidpointRounding.AwayFromZero);
            unitPrice = Math.Round(unitPrice, 2, MidpointRounding.AwayFromZero);

            if (beforeTax <= 0m && lineTotal <= 0m && unitPrice <= 0m)
            {
                continue;
            }

            var changed = false;
            changed |= SetDecimalIfDifferent(line, "UnitPrice", unitPrice);
            changed |= SetDecimalIfDifferent(line, "Discount", discount);
            changed |= SetDecimalIfDifferent(line, "DiscountAmount", discount);
            changed |= SetDecimalIfDifferent(line, "SubTotalBeforeGST", beforeTax);
            changed |= SetDecimalIfDifferent(line, "ConvertedSubTotalBeforeGST", beforeTax);
            changed |= SetDecimalIfDifferent(line, "TaxableAmount", beforeTax);
            changed |= SetDecimalIfDifferent(line, "ConvertedTaxableAmount", beforeTax);
            changed |= SetDecimalIfDifferent(line, "TaxAmount", taxAmount);
            changed |= SetDecimalIfDifferent(line, "ConvertedTaxAmount", taxAmount);
            changed |= SetDecimalIfDifferent(line, "SubTotal", lineTotal);
            changed |= SetDecimalIfDifferent(line, "Amount", lineTotal);
            changed |= SetDecimalIfDifferent(line, "ConvertedAmount", lineTotal);

            if (!changed)
            {
                continue;
            }

            line["DocumentID"] = documentId;
            line["DocumentLineTypeID"] = Integer(line, "DocumentLineTypeID") == 0
                ? 1
                : Integer(line, "DocumentLineTypeID");
            line["OwnerDocumentTypeID"] = documentTypeId;
            line["SaveAction"] = "Changed";
            line["IsDirty"] = true;

            var lineSaveResult = await cashSalesAC.SaveDocumentLineAsync(
                line,
                cancellationToken);
            if (!lineSaveResult.Success)
            {
                return ApiCallResult<bool>.Failure(
                    lineSaveResult.StatusCode,
                    lineSaveResult.ErrorMessage ?? "Unable to save the repaired receipt item amounts.");
            }

            repairedAnyLine = true;
        }

        var totalBeforeTax = Math.Round(
            activeLines.Sum(line => Number(line, "SubTotalBeforeGST")),
            2,
            MidpointRounding.AwayFromZero);
        var totalTax = Math.Round(
            activeLines.Sum(line => Number(line, "TaxAmount")),
            2,
            MidpointRounding.AwayFromZero);
        var rounding = Math.Round(
            Number(header, "RoundingAmount"),
            2,
            MidpointRounding.AwayFromZero);
        var totalAfterTax = Math.Round(
            activeLines.Sum(line => Number(line, "SubTotal")) + rounding,
            2,
            MidpointRounding.AwayFromZero);
        var serviceChargeBeforeTax = Math.Clamp(
            Number(header, "TotalBeforeTax_ServiceCharge"),
            0m,
            totalBeforeTax);

        var headerChanged = false;
        headerChanged |= SetDecimalIfDifferent(header, "TotalBeforeTax", totalBeforeTax);
        headerChanged |= SetDecimalIfDifferent(
            header,
            "TotalBeforeTax_NonServiceCharge",
            Math.Max(0m, totalBeforeTax - serviceChargeBeforeTax));
        headerChanged |= SetDecimalIfDifferent(
            header,
            "TotalBeforeTax_ServiceCharge",
            serviceChargeBeforeTax);
        headerChanged |= SetDecimalIfDifferent(header, "TaxableAmount", totalBeforeTax);
        headerChanged |= SetDecimalIfDifferent(header, "TaxAmount", totalTax);
        headerChanged |= SetDecimalIfDifferent(header, "TotalAfterTax", totalAfterTax);
        headerChanged |= SetDecimalIfDifferent(header, "LocalTotalBeforeTax", totalBeforeTax);
        headerChanged |= SetDecimalIfDifferent(header, "LocalTaxableAmount", totalBeforeTax);
        headerChanged |= SetDecimalIfDifferent(header, "LocalTaxAmount", totalTax);
        headerChanged |= SetDecimalIfDifferent(header, "LocalRoundingAmount", rounding);
        headerChanged |= SetDecimalIfDifferent(header, "LocalTotalAfterTax", totalAfterTax);

        if (headerChanged)
        {
            var now = DateTime.Now;
            header["DocumentID"] = documentId;
            header["DocumentTypeID"] = documentTypeId;
            header["ModifiedDateTime"] = now;
            header["UpdateTimeStamp"] = now;
            header["SaveAction"] = "Changed";
            header["IsDirty"] = true;

            var headerSaveResult = await cashSalesAC.SaveHeaderAsync(
                header,
                cancellationToken);
            if (!headerSaveResult.Success)
            {
                return ApiCallResult<bool>.Failure(
                    headerSaveResult.StatusCode,
                    headerSaveResult.ErrorMessage ?? "Unable to save the repaired receipt totals.");
            }
        }

        return ApiCallResult<bool>.Ok(
            HttpStatusCode.OK,
            repairedAnyLine || headerChanged);
    }

    public Task<bool> DownloadReceiptPdfAsync(
        string documentId,
        CancellationToken cancellationToken = default) =>
        DownloadReceiptPdfAsync(documentId, 5, cancellationToken);

    public async Task<bool> DownloadReceiptPdfAsync(
        string documentId,
        int documentTypeId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var result = await RequestReceiptPdfAsync(
                documentId,
                documentTypeId,
                cancellationToken);

            if (!result.Success || string.IsNullOrWhiteSpace(result.Value))
            {
                return false;
            }

            var brandedPdf = await jsRuntime.InvokeAsync<string>(
                "receiptPdfBranding.replaceLogo",
                cancellationToken,
                result.Value,
                ReceiptBranding.LogoUrl);

            if (string.IsNullOrWhiteSpace(brandedPdf))
            {
                throw new InvalidOperationException("The EBI-branded receipt PDF was empty.");
            }

            var fileName = $"Thermal_Receipt_{documentId}.pdf";
            await fileDownloadService.DownloadBinaryFileAsync(
                fileName,
                brandedPdf,
                "application/pdf",
                cancellationToken);

            return true;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Receipt download error: {ex.Message}");
            return false;
        }
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

    public async Task<ApiCallResult<IReadOnlyList<CashSalesPaymentTypeDTO>>> LoadPaymentTypesAsync(
        string branchId,
        string groupId,
        string customerId,
        CancellationToken cancellationToken = default)
    {
        var result = await cashSalesAC.LoadSystemPaymentTypesAsync(new CashSalesPaymentTypeRequestDTO
        {
            BranchId = string.IsNullOrWhiteSpace(branchId) ? null : branchId,
            GroupId = string.IsNullOrWhiteSpace(groupId) ? null : groupId,
            CustomerId = string.IsNullOrWhiteSpace(customerId) ? null : customerId
        }, cancellationToken);

        if (!result.Success || result.Value is null)
            return ApiCallResult<IReadOnlyList<CashSalesPaymentTypeDTO>>.Failure(
                result.StatusCode,
                result.ErrorMessage ?? "Unable to load payment methods.");

        var values = result.Value
            .Where(item => item.Active &&
                           (string.IsNullOrWhiteSpace(item.VisibleInModules) ||
                            item.VisibleInModules.Contains("Sales", StringComparison.OrdinalIgnoreCase)))
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
        var gstTypeId = string.Empty;
        var currencyId = "MYR";
        if (!string.IsNullOrWhiteSpace(transaction.BranchId))
        {
            var branchResult = await branchAC.LoadRecordAsync(
                transaction.BranchId,
                cancellationToken);
            if (branchResult.Success && branchResult.Value is not null)
            {
                gstTypeId = FindTextIgnoreCase(
                    branchResult.Value,
                    "TaxTypeID",
                    "GSTTypeID") ?? string.Empty;
                currencyId = First(
                    FindTextIgnoreCase(branchResult.Value, "CurrencyID"),
                    transaction.CurrencyName,
                    "MYR")!;
                transaction.GroupId = First(
                    FindTextIgnoreCase(branchResult.Value, "BranchGroupID", "GroupID"),
                    transaction.GroupId,
                    transaction.BranchId,
                    string.Empty)!;
                transaction.CurrencyName = First(
                    FindTextIgnoreCase(branchResult.Value, "CurrencyName"),
                    transaction.CurrencyName,
                    "MYR")!;
            }
        }

        var documentLines = BuildDocumentLines(
            document,
            transaction,
            gstTypeId,
            currencyId);
        SynchronizeTransactionTotals(transaction, documentLines);
        ApplyHeader(header, transaction, true, currencyId, gstTypeId);
        document["objDoc_CashSales"] = header;
        document["lstDocumentLine"] = documentLines;

        var redemptionTaxValidation = ValidateAndApplyRedemptionTaxBasis(
            transaction,
            documentLines,
            header);

        if (!redemptionTaxValidation.Success)
        {
            Console.WriteLine(
                $"[Member Credit Step 35] BLOCKED | Customer={transaction.AccountId} | {redemptionTaxValidation.ErrorMessage}");

            return ApiCallResult<Transaction>.Failure(
                HttpStatusCode.BadRequest,
                redemptionTaxValidation.ErrorMessage);
        }

        var redemptionDiscountValidation = ValidateRedemptionDiscountInteraction(
            transaction,
            documentLines);

        if (!redemptionDiscountValidation.Success)
        {
            Console.WriteLine(
                $"[Member Credit Step 36] BLOCKED | Customer={transaction.AccountId} | {redemptionDiscountValidation.ErrorMessage}");

            return ApiCallResult<Transaction>.Failure(
                HttpStatusCode.BadRequest,
                redemptionDiscountValidation.ErrorMessage);
        }

        document["lstReceiptLines"] = BuildReceiptLines(transaction);

        var memberCreditBuild = await BuildMemberCreditGrantCollectionsAsync(
            transaction,
            documentLines,
            header,
            cancellationToken);

        if (!memberCreditBuild.Success)
        {
            return ApiCallResult<Transaction>.Failure(
                HttpStatusCode.BadRequest,
                memberCreditBuild.ErrorMessage);
        }

        document["lstARAPOutstanding_MemberCredit"] = memberCreditBuild.RootCredits;

        var singleCreditVerification = await CaptureSingleMemberCreditRedemptionAsync(
            transaction,
            cancellationToken);

        var multiCreditVerification = await CaptureMultiMemberCreditRedemptionAsync(
            transaction,
            cancellationToken);

        var step30TotalCreditBefore = await CaptureStep30TotalCreditBeforeAsync(
            transaction,
            cancellationToken);

        // Step 28 — Final pre-save Member Credit revalidation.
        // Reload the customer's current redeemable-credit state immediately before
        // CreateRecord so stale UI/FIFO allocations cannot over-redeem an account.
        var preSaveCreditValidation = await ValidateMemberCreditRedemptionBeforeCreateAsync(
            transaction,
            cancellationToken);

        if (!preSaveCreditValidation.Success)
        {
            Console.WriteLine(
                $"[Member Credit Step 28] BLOCKED | Customer={transaction.AccountId} | {preSaveCreditValidation.ErrorMessage}");

            return ApiCallResult<Transaction>.Failure(
                preSaveCreditValidation.StatusCode,
                preSaveCreditValidation.ErrorMessage);
        }

        var finalRedemptionRequestValidation = ValidateFinalRedemptionRequest(
            transaction,
            document);

        if (!finalRedemptionRequestValidation.Success)
        {
            Console.WriteLine(
                $"[Member Credit Step 29] BLOCKED | Customer={transaction.AccountId} | {finalRedemptionRequestValidation.ErrorMessage}");

            return ApiCallResult<Transaction>.Failure(
                HttpStatusCode.BadRequest,
                finalRedemptionRequestValidation.ErrorMessage);
        }

        // Point Step 2 — Senang saves point spending on normal Cash Sales lines.
        // Re-read the latest customer PointBalance immediately before CreateRecord
        // so another terminal cannot make this request overspend a stale balance.
        var preSavePointValidation = await ValidatePointRedemptionBeforeCreateAsync(
            transaction,
            cancellationToken);

        if (!preSavePointValidation.Success)
        {
            Console.WriteLine(
                $"[Point Step 2] BLOCKED | Customer={transaction.AccountId} | {preSavePointValidation.ErrorMessage}");

            return ApiCallResult<Transaction>.Failure(
                preSavePointValidation.StatusCode,
                preSavePointValidation.ErrorMessage);
        }

        var createResult = await cashSalesAC.CreateRecordAsync(document, cancellationToken);
        if (!createResult.Success)
        {
            var message = createResult.ErrorMessage ?? "Unable to create the cash sale.";
            return ApiCallResult<Transaction>.Failure(createResult.StatusCode, message);
        }

        if (createResult.Value is null)
        {
            return ApiCallResult<Transaction>.Failure(
                createResult.StatusCode,
                "The cash sale was created but the API did not return the created record.");
        }

        transaction.DocumentId = createResult.Value.Id ?? string.Empty;
        transaction.InvoiceNumber = createResult.Value.DisplayCode ?? string.Empty;

        Console.WriteLine(
            $"[Cash Sales Create] DocumentID={transaction.DocumentId} | DisplayCode={transaction.InvoiceNumber}");

        if (string.IsNullOrWhiteSpace(transaction.DocumentId))
        {
            return ApiCallResult<Transaction>.Failure(
                createResult.StatusCode,
                "The cash sale API did not return Result.Id.");
        }

        await VerifyMemberCreditGrantPersistenceAsync(
            transaction,
            memberCreditBuild.RootCredits,
            cancellationToken);

        await VerifySingleMemberCreditRedemptionAsync(
            transaction,
            singleCreditVerification,
            cancellationToken);

        await VerifyMultiMemberCreditRedemptionAsync(
            transaction,
            multiCreditVerification,
            cancellationToken);

        await VerifyStep30RedemptionAfterSaveAsync(
            transaction,
            singleCreditVerification,
            multiCreditVerification,
            step30TotalCreditBefore,
            cancellationToken);

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
                .Where(payment => payment.PaymentTypeId != -10)
                .ToList()
            : transaction.PaymentTypeId != 0 && transaction.PaymentTypeId != -10
                ? new List<TransactionPayment>
                {
                    new() { PaymentTypeId = transaction.PaymentTypeId, PaymentMethod = transaction.PaymentMethod, Amount = transaction.Amount }
                }
                : new List<TransactionPayment>();
        var change = Math.Max(0, payments.Sum(payment => payment.Amount) - transaction.Amount);
        var changePayment = payments.LastOrDefault(payment =>
            payment.PaymentMethod.Contains("cash", StringComparison.OrdinalIgnoreCase)) ?? payments.LastOrDefault();

        // Member Credit redemption receipts are backend accounting rows, not normal
        // Cash/Card tender rows. A normal payment edit must not delete them.
        var retainedReceiptIds = existing
            .Where(line => line.POSPaymentTypeID == -10 &&
                           !string.IsNullOrWhiteSpace(line.POSReceiptLineID))
            .Select(line => line.POSReceiptLineID!)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
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
            DocumentTypeId = Integer(header, "DocumentTypeID") == 0 ? 5 : Integer(header, "DocumentTypeID"),
            AccountId = Text(header, "AccountID"),
            BranchId = Text(header, "BranchID"),
            GroupId = Text(header, "GroupID"),
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
            RoundingAmount = Number(header, "RoundingAmount"),
            ServiceChargeAmount = Number(header, "TotalBeforeTax_ServiceCharge"),
            CurrencyName = First(Text(header, "LocalCurrencyName"), Text(header, "TransactionCurrencyName"), "MYR")!,
            Status = Bool(header, "IsVoid") ? "Cancelled" : "Paid",
            ReferenceNumber = Text(header, "ReferenceNumber"),
            Notes = Text(header, "Remarks"),
            CreatedBy = First(Text(header, "CashierName"), Text(header, "SalesPersonName"), Text(header, "CreatedBy"), string.Empty)!,
            Items = lines.Select(ToTransactionItem).ToList()
        };

        // Some LoadRecord responses leave the header TaxAmount at zero even
        // though the saved document lines contain the tax fields. Receipts are
        // line-driven, so retain the item tax instead of silently printing 0.00.
        if (transaction.Tax == 0m && transaction.Items.Count > 0)
        {
            transaction.Tax = transaction.Items.Sum(ResolveReceiptLineTax);
        }

        transaction.ReceiptPayments = receipts.OfType<JsonObject>()
            .Select(receipt => new TransactionPayment
            {
                ReceiptLineId = Text(receipt, "POSReceiptLineID"),
                PaymentTypeId = Integer(receipt, "POSPaymentTypeID"),
                PaymentMethod = Integer(receipt, "POSPaymentTypeID") == -10
                    ? "Member Credit"
                    : First(Text(receipt, "POSPaymentTypeName"), Text(receipt, "Description"), "Payment")!,
                SourceDocumentLineId = Text(receipt, "SourceDocumentLineID"),
                Amount = Number(receipt, "POSReceiptLineAmount"),
                ChangeAmount = Math.Abs(Number(receipt, "POSReceiptChangeAmount")),
                FinancialAccountId = Text(receipt, "FinancialAccountID"),
                BankName = Text(receipt, "BankName")
            })
            .Where(payment => payment.PaymentTypeId != 0 && payment.Amount > 0)
            .ToList();

        transaction.Payments = transaction.ReceiptPayments
            .Where(payment => payment.PaymentTypeId != -10)
            .ToList();

        if (transaction.Payments.Count > 0)
        {
            transaction.PaymentTypeId = transaction.Payments[0].PaymentTypeId;
            transaction.PaymentMethod = string.Join(" + ", transaction.Payments
                .Select(payment => payment.PaymentMethod)
                .Distinct(StringComparer.OrdinalIgnoreCase));
        }
        else if (transaction.DocumentTypeId == 52 &&
                 transaction.ReceiptPayments.Any(payment => payment.PaymentTypeId == -10))
        {
            transaction.PaymentTypeId = -10;
            transaction.PaymentMethod = "Member Credit";
        }

        var categories = transaction.Items.Select(item => item.Category).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        transaction.Type = categories.Count == 1 ? categories[0] : "Mixed";
        return transaction;
    }

    private static decimal ResolveReceiptLineTax(TransactionItem item)
    {
        if (item.TaxAmount != 0m)
        {
            return item.TaxAmount;
        }

        ComputeLineTaxBasis(item, out _, out var tax, out _);
        return tax;
    }

    private static TransactionItem ToTransactionItem(JsonNode? node)
    {
        var line = node as JsonObject;
        var inventoryType = Integer(line, "InventoryTypeID");
        var quantity = Number(line, "Quantity");
        var unitPrice = Number(line, "UnitPrice");
        var total = Number(line, "SubTotal");
        var points = Math.Max(0m, Number(line, "Points"));
        var memberCreditAccountId = Text(line, "MemberCreditAccountID");
        var memberTypeId = Text(line, "MemberTypeID");
        var memberCreditAllocations = ParseSavedMemberCreditAllocations(
            line,
            memberCreditAccountId,
            memberTypeId,
            total != 0 ? total : quantity * unitPrice);
        var membershipCredit = Text(line, "MembershipCredit");

        // Some redemption LoadRecord responses persist the multi-credit collection
        // as lstMembershipCredit while MembershipCredit is blank. Reconstruct the
        // serialized field so a reopened redemption has both representations.
        if (string.IsNullOrWhiteSpace(membershipCredit) &&
            memberCreditAllocations.Count > 1)
        {
            membershipCredit = SerializeMemberCreditAllocations(memberCreditAllocations);
        }

        return new TransactionItem
        {
            InventoryId = Text(line, "InventoryID"),
            Name = First(Text(line, "ItemName"), Text(line, "Description"), "Item")!,
            Description = Text(line, "Description"),
            Remarks = Text(line, "RefCompanyName"),
            Category = inventoryType switch
            {
                3 => "Service",
                5 => "Package",
                7 => "Member Credit",
                _ => "Product"
            },
            InventoryTypeId = inventoryType,
            Quantity = (int)Math.Max(1, quantity),
            UnitPrice = unitPrice,
            TotalPrice = total != 0 ? total : quantity * unitPrice,
            Discount = Number(line, "DiscountAmount"),
            CashDiscountId = Text(line, "CashDiscountID"),
            DiscountMemo = Text(line, "Memo"),
            TaxCodeId = Text(line, "TaxCodeID"),
            TaxPercentage = Number(line, "TaxPercentage"),
            TaxAmount = Number(line, "TaxAmount"),
            UnitOfMeasureId = Text(line, "UnitOfMeasureID"),
            IsTaxInclusive = Bool(line, "IsTaxInclusive"),
            Points = points,
            PointToRedeem = Math.Max(0m, Number(line, "PointToRedeem")),
            AllowPointRedemption = Bool(line, "AllowPointRedemption") || points > 0m,
            ActivityTypeId = Integer(line, "ActivityTypeID") == 0 ? 1 : Integer(line, "ActivityTypeID"),
            MemberCreditAccountId = memberCreditAccountId,
            MemberTypeId = memberTypeId,
            MembershipCredit = membershipCredit,
            MemberCreditAllocations = memberCreditAllocations
        };
    }

    private static (bool Success, string ErrorMessage) ValidateRedemptionDiscountInteraction(
        Transaction transaction,
        JsonArray documentLines)
    {
        if (transaction.DocumentTypeId != 52)
        {
            return (true, string.Empty);
        }

        if (documentLines.Count != transaction.Items.Count)
        {
            return (
                false,
                "Redemption discount validation failed because the generated document lines do not match the cart lines.");
        }

        decimal grossTotal = 0m;
        decimal discountTotal = 0m;
        decimal eligibleCreditTotal = 0m;
        decimal creditTotal = 0m;
        var discountedLines = 0;
        var ruleDiscountLines = 0;
        var manualDiscountLines = 0;

        for (var index = 0; index < transaction.Items.Count; index++)
        {
            var item = transaction.Items[index];
            if (documentLines[index] is not JsonObject line)
            {
                return (
                    false,
                    $"Redemption discount validation failed for '{First(item.Name, item.InventoryId, "Item")}': generated line is missing.");
            }

            var quantity = Math.Max(1, item.Quantity);
            var gross = Math.Round(
                Math.Max(0m, quantity * item.UnitPrice),
                2,
                MidpointRounding.AwayFromZero);

            if (item.Discount < -0.009m || item.Discount - gross > 0.009m)
            {
                return (
                    false,
                    $"Discount for '{First(item.Name, item.InventoryId, "Item")}' is outside the valid line range. " +
                    $"Gross RM {gross:N2}, discount RM {item.Discount:N2}.");
            }

            var resolvedDiscount = Math.Round(
                Math.Clamp(item.Discount, 0m, gross),
                2,
                MidpointRounding.AwayFromZero);

            var requestDiscount = Math.Round(
                Number(line, "DiscountAmount"),
                2,
                MidpointRounding.AwayFromZero);

            if (Math.Abs(requestDiscount - resolvedDiscount) >= 0.01m)
            {
                return (
                    false,
                    $"Discount for '{First(item.Name, item.InventoryId, "Item")}' changed while building the redemption request. " +
                    $"Expected RM {resolvedDiscount:N2}, request contains RM {requestDiscount:N2}.");
            }

            var itemCashDiscountId = item.CashDiscountId?.Trim() ?? string.Empty;
            var requestCashDiscountId = Text(line, "CashDiscountID").Trim();
            if (!string.Equals(
                    itemCashDiscountId,
                    requestCashDiscountId,
                    StringComparison.OrdinalIgnoreCase))
            {
                return (
                    false,
                    $"Discount rule for '{First(item.Name, item.InventoryId, "Item")}' changed while building the redemption request.");
            }

            var eligibleSubTotal = RedeemableLineSubTotal(item);
            var requestSubTotal = Math.Round(
                Number(line, "SubTotal"),
                2,
                MidpointRounding.AwayFromZero);

            if (eligibleSubTotal < -0.009m ||
                Math.Abs(requestSubTotal - eligibleSubTotal) >= 0.01m)
            {
                return (
                    false,
                    $"Discounted redeemable subtotal for '{First(item.Name, item.InventoryId, "Item")}' is invalid. " +
                    $"Expected RM {eligibleSubTotal:N2}, request contains RM {requestSubTotal:N2}.");
            }

            if (resolvedDiscount > 0.009m)
            {
                discountedLines++;
                if (string.IsNullOrWhiteSpace(itemCashDiscountId))
                {
                    manualDiscountLines++;
                }
                else
                {
                    // Open, percentage, fixed and compound rules all reach this point
                    // only after Beauty has resolved them to the final line discount.
                    ruleDiscountLines++;
                }
            }

            grossTotal += gross;
            discountTotal += resolvedDiscount;
            if (item.ActivityTypeId != 6)
            {
                continue;
            }

            if (item.MemberCreditAllocations.Any(allocation => allocation.Amount < -0.009m))
            {
                return (
                    false,
                    $"Member Credit for '{First(item.Name, item.InventoryId, "Item")}' contains a negative allocation after discount.");
            }

            var allocated = Math.Round(
                EffectiveMemberCreditAllocations(item)
                    .Sum(allocation => Math.Max(0m, allocation.Amount)),
                2,
                MidpointRounding.AwayFromZero);

            if (eligibleSubTotal <= 0m)
            {
                return (
                    false,
                    $"'{First(item.Name, item.InventoryId, "Item")}' is fully discounted and no longer has a redeemable amount. " +
                    "Remove Member Credit from this line.");
            }

            if (allocated - eligibleSubTotal > 0.009m)
            {
                return (
                    false,
                    $"Discount caused Member Credit over-allocation for '{First(item.Name, item.InventoryId, "Item")}'. " +
                    $"Credit RM {allocated:N2}, discounted subtotal RM {eligibleSubTotal:N2}.");
            }

            if (Math.Abs(allocated - eligibleSubTotal) > 0.009m)
            {
                return (
                    false,
                    $"Member Credit for '{First(item.Name, item.InventoryId, "Item")}' does not match the discounted subtotal. " +
                    $"Credit RM {allocated:N2}, discounted subtotal RM {eligibleSubTotal:N2}.");
            }

            eligibleCreditTotal += eligibleSubTotal;
            creditTotal += allocated;
        }

        grossTotal = Math.Round(grossTotal, 2, MidpointRounding.AwayFromZero);
        discountTotal = Math.Round(discountTotal, 2, MidpointRounding.AwayFromZero);
        eligibleCreditTotal = Math.Round(eligibleCreditTotal, 2, MidpointRounding.AwayFromZero);
        creditTotal = Math.Round(creditTotal, 2, MidpointRounding.AwayFromZero);

        if (discountTotal < 0m || eligibleCreditTotal < 0m || creditTotal < 0m)
        {
            return (
                false,
                "Redemption discount validation produced a negative total.");
        }

        Console.WriteLine(
            $"[Member Credit Step 36] PASS | DiscountedLines={discountedLines} | " +
            $"RuleDiscountLines={ruleDiscountLines} | ManualDiscountLines={manualDiscountLines} | " +
            $"Gross={grossTotal:N2} | Discount={discountTotal:N2} | " +
            $"EligibleCreditSubtotal={eligibleCreditTotal:N2} | MemberCredit={creditTotal:N2}");

        return (true, string.Empty);
    }

    private static (bool Success, string ErrorMessage) ValidateAndApplyRedemptionTaxBasis(
        Transaction transaction,
        JsonArray documentLines,
        JsonObject header)
    {
        if (transaction.DocumentTypeId != 52)
        {
            return (true, string.Empty);
        }

        if (documentLines.Count != transaction.Items.Count)
        {
            return (
                false,
                "Redemption tax validation failed because the generated document lines do not match the cart lines.");
        }

        decimal canonicalBeforeTax = 0m;
        decimal canonicalTax = 0m;
        decimal canonicalAfterTax = 0m;
        var redemptionLineCount = 0;

        for (var index = 0; index < transaction.Items.Count; index++)
        {
            var item = transaction.Items[index];
            if (documentLines[index] is not JsonObject line)
            {
                return (
                    false,
                    $"Redemption tax validation failed for '{First(item.Name, item.InventoryId, "Item")}': generated line is missing.");
            }

            ComputeLineTaxBasis(
                item,
                out var expectedBeforeTax,
                out var expectedTax,
                out var expectedSubTotal);

            var lineBeforeTax = Math.Round(
                Number(line, "SubTotalBeforeGST"),
                2,
                MidpointRounding.AwayFromZero);
            var lineTax = Math.Round(
                Number(line, "TaxAmount"),
                2,
                MidpointRounding.AwayFromZero);
            var lineSubTotal = Math.Round(
                Number(line, "SubTotal"),
                2,
                MidpointRounding.AwayFromZero);
            var lineTaxPercentage = Number(line, "TaxPercentage");
            var lineIsTaxInclusive = Bool(line, "IsTaxInclusive");

            if (Math.Abs(lineBeforeTax - expectedBeforeTax) >= 0.01m ||
                Math.Abs(lineTax - expectedTax) >= 0.01m ||
                Math.Abs(lineSubTotal - expectedSubTotal) >= 0.01m ||
                Math.Abs((lineBeforeTax + lineTax) - lineSubTotal) >= 0.01m)
            {
                return (
                    false,
                    $"Redemption tax values for '{First(item.Name, item.InventoryId, "Item")}' are inconsistent. " +
                    $"Expected BeforeGST RM {expectedBeforeTax:N2} + Tax RM {expectedTax:N2} = SubTotal RM {expectedSubTotal:N2}, " +
                    $"but the generated line contains RM {lineBeforeTax:N2} + RM {lineTax:N2} = RM {lineSubTotal:N2}.");
            }

            if (Math.Abs(lineTaxPercentage - item.TaxPercentage) >= 0.0001m ||
                lineIsTaxInclusive != item.IsTaxInclusive)
            {
                return (
                    false,
                    $"Redemption tax settings for '{First(item.Name, item.InventoryId, "Item")}' changed while building the request.");
            }

            canonicalBeforeTax += lineBeforeTax;
            canonicalTax += lineTax;
            canonicalAfterTax += lineSubTotal;

            if (Integer(line, "ActivityTypeID") != 6)
            {
                continue;
            }

            redemptionLineCount++;
            var allocated = Math.Round(
                EffectiveMemberCreditAllocations(item).Sum(allocation => allocation.Amount),
                2,
                MidpointRounding.AwayFromZero);

            // Senang's redemption workflow uses DocumentLine.SubTotal as the
            // redeemable amount. SubTotalBeforeGST and TaxAmount remain separate
            // accounting fields and must not become the credit allocation basis.
            if (Math.Abs(allocated - lineSubTotal) >= 0.01m)
            {
                return (
                    false,
                    $"Member Credit for '{First(item.Name, item.InventoryId, "Item")}' must cover the final line SubTotal " +
                    $"RM {lineSubTotal:N2} (BeforeGST RM {lineBeforeTax:N2} + Tax RM {lineTax:N2}). " +
                    $"Currently allocated RM {allocated:N2}.");
            }

            Console.WriteLine(
                $"[Member Credit Step 35] LINE PASS | Item={First(item.Name, item.InventoryId, "Item")} | " +
                $"TaxInclusive={item.IsTaxInclusive} | TaxPercent={item.TaxPercentage:N4} | " +
                $"BeforeGST={lineBeforeTax:N2} | Tax={lineTax:N2} | SubTotal={lineSubTotal:N2} | Credit={allocated:N2}");
        }

        canonicalBeforeTax = Math.Round(
            canonicalBeforeTax,
            2,
            MidpointRounding.AwayFromZero);
        canonicalTax = Math.Round(
            canonicalTax,
            2,
            MidpointRounding.AwayFromZero);
        canonicalAfterTax = Math.Round(
            canonicalAfterTax,
            2,
            MidpointRounding.AwayFromZero);

        // Keep the document header synchronized with the exact rounded line values
        // that the backend receives. This preserves Beauty's existing tax engine
        // while preventing header/line drift in redemption requests.
        transaction.Subtotal = canonicalBeforeTax;
        transaction.Tax = canonicalTax;
        transaction.RoundingAmount = 0m;
        transaction.Amount = canonicalAfterTax;

        header["TotalBeforeTax"] = canonicalBeforeTax;
        header["TaxableAmount"] = canonicalBeforeTax;
        header["TaxAmount"] = canonicalTax;

        // Senang explicitly disables rounding for DocumentTypeID 52 redemption.
        // Keep Beauty's line tax rounding, but do not apply an additional
        // cash-sale/header rounding adjustment to Member Credit redemption.
        header["RoundingAmount"] = 0m;
        header["TotalAfterTax"] = canonicalAfterTax;
        header["LocalTotalBeforeTax"] = canonicalBeforeTax;
        header["LocalTaxableAmount"] = canonicalBeforeTax;
        header["LocalTaxAmount"] = canonicalTax;
        header["LocalRoundingAmount"] = 0m;
        header["LocalTotalAfterTax"] = canonicalAfterTax;

        Console.WriteLine(
            $"[Member Credit Step 35] PASS | RedemptionLines={redemptionLineCount} | " +
            $"TotalBeforeGST={canonicalBeforeTax:N2} | Tax={canonicalTax:N2} | Rounding=0.00 | TotalAfterTax={canonicalAfterTax:N2} | " +
            $"CreditBasis=DocumentLine.SubTotal");

        return (true, string.Empty);
    }

    private static decimal RedeemableLineSubTotal(TransactionItem item)
    {
        ComputeLineTaxBasis(
            item,
            out _,
            out _,
            out var subTotal);

        return subTotal;
    }

    private static void ComputeLineTaxBasis(
        TransactionItem item,
        out decimal beforeTax,
        out decimal tax,
        out decimal subTotal)
    {
        var quantity = Math.Max(1, item.Quantity);
        var gross = quantity * item.UnitPrice;
        var discount = Math.Clamp(item.Discount, 0m, gross);

        OrderLineTaxCalculator.ComputeLineAmounts(
            item.UnitPrice,
            quantity,
            discount,
            item.TaxPercentage,
            item.IsTaxInclusive,
            out var calculatedBeforeTax,
            out var calculatedTax);

        beforeTax = Math.Round(
            Math.Max(0m, calculatedBeforeTax),
            2,
            MidpointRounding.AwayFromZero);
        tax = Math.Round(
            Math.Max(0m, calculatedTax),
            2,
            MidpointRounding.AwayFromZero);
        subTotal = Math.Round(
            beforeTax + tax,
            2,
            MidpointRounding.AwayFromZero);
    }

    private async Task<ApiCallResult<bool>> ValidatePointRedemptionBeforeCreateAsync(
        Transaction transaction,
        CancellationToken cancellationToken)
    {
        var pointLines = transaction.Items
            .Where(item => item.Points > 0m)
            .ToList();

        if (pointLines.Count == 0)
        {
            return ApiCallResult<bool>.Ok(HttpStatusCode.OK, true);
        }

        if (transaction.DocumentTypeId != 5)
        {
            return ApiCallResult<bool>.Failure(
                HttpStatusCode.BadRequest,
                "Point redemption is only supported in normal Sales mode (DocumentTypeID = 5).");
        }

        if (string.IsNullOrWhiteSpace(transaction.AccountId))
        {
            return ApiCallResult<bool>.Failure(
                HttpStatusCode.BadRequest,
                "A customer is required before points can be redeemed.");
        }

        foreach (var item in pointLines)
        {
            if (!item.AllowPointRedemption ||
                item.PointToRedeem <= 0m ||
                Math.Abs(item.PointToRedeem - item.Points) > 0.009m)
            {
                return ApiCallResult<bool>.Failure(
                    HttpStatusCode.BadRequest,
                    $"Point redemption for {First(item.Name, item.InventoryId, "item")} is invalid. Reapply points before completing the sale.");
            }

            if (Math.Abs(item.UnitPrice) > 0.009m)
            {
                return ApiCallResult<bool>.Failure(
                    HttpStatusCode.BadRequest,
                    $"Point-redeemed item {First(item.Name, item.InventoryId, "item")} must have UnitPrice = 0.");
            }
        }

        var latestBalanceResult = await customerService.GetBalanceSummaryAsync(
            transaction.AccountId,
            cancellationToken);

        if (!latestBalanceResult.Success || latestBalanceResult.Value is null)
        {
            return ApiCallResult<bool>.Failure(
                HttpStatusCode.ServiceUnavailable,
                latestBalanceResult.ErrorMessage ??
                "Unable to verify the latest customer point balance.");
        }

        var totalPoints = Math.Round(
            pointLines.Sum(item =>
                Math.Max(0m, item.Points) * Math.Max(1, item.Quantity)),
            2,
            MidpointRounding.AwayFromZero);
        var latestBalance = Math.Max(
            0m,
            latestBalanceResult.Value.PointBalance);

        if (totalPoints - latestBalance > 0.009m)
        {
            return ApiCallResult<bool>.Failure(
                HttpStatusCode.BadRequest,
                $"Customer point balance changed. Latest balance: {latestBalance:0.##} pts; required: {totalPoints:0.##} pts.");
        }

        Console.WriteLine(
            $"[Point Step 2] PRE-SAVE PASS | Customer={transaction.AccountId} | " +
            $"LatestBalance={latestBalance:0.##} | Required={totalPoints:0.##} | Lines={pointLines.Count}");

        return ApiCallResult<bool>.Ok(HttpStatusCode.OK, true);
    }

    private static JsonArray BuildDocumentLines(
        JsonObject document,
        Transaction transaction,
        string gstTypeId,
        string currencyId)
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
            var discount = Math.Clamp(item.Discount, 0m, gross);
            ComputeLineTaxBasis(
                item,
                out var beforeTax,
                out var lineTax,
                out var lineTotal);
            var line = template is null ? new JsonObject() : (JsonObject)template.DeepClone();
            var currentLineOrder = lineOrder++;
            var documentTypeId = transaction.DocumentTypeId == 52 ? 52 : 5;
            var unitOfMeasurementId = string.IsNullOrWhiteSpace(item.UnitOfMeasureId)
                ? "UNIT"
                : item.UnitOfMeasureId;
            item.TaxAmount = lineTax;
            item.TotalPrice = lineTotal;

            line["DocumentLineID"] = currentLineOrder.ToString("D2");
            line["DocumentID"] = string.Empty;
            line["DocumentLineTypeID"] = 1;
            line["OwnerDocumentTypeID"] = documentTypeId;
            line["InventoryID"] = item.InventoryId;
            line["LineItemID"] = item.InventoryId;
            line["InventoryItemAccountID"] = item.InventoryId;
            line["LineItemDisplayCode"] = item.Sku;
            line["LineOrder"] = currentLineOrder;
            line["Description"] = First(item.Description, item.Name, $"{transaction.Type} Item");
            line["ItemName"] = First(item.Name, item.Description, $"{transaction.Type} Item");
            line["Quantity"] = quantity;
            line["UnitPrice"] = item.UnitPrice;
            line["Points"] = Math.Max(0m, item.Points);
            line["Discount"] = discount;
            line["DiscountAmount"] = discount;
            line["CashDiscountID"] = item.CashDiscountId;
            line["Memo"] = item.DiscountMemo;
            line["RefCompanyName"] = item.Remarks;
            line["SubTotal"] = lineTotal;
            line["SubTotalBeforeGST"] = beforeTax;
            line["ConvertedSubTotalBeforeGST"] = beforeTax;
            line["Amount"] = lineTotal;
            line["ConvertedAmount"] = lineTotal;
            line["TaxableAmount"] = beforeTax;
            line["ConvertedTaxableAmount"] = beforeTax;
            line["TaxPercentage"] = item.TaxPercentage;
            line["TaxAmount"] = lineTax;
            line["ConvertedTaxAmount"] = lineTax;
            line["InventoryTypeID"] = item.InventoryTypeId > 0
                ? item.InventoryTypeId
                : InventoryTypeFor(item.Category);
            line["UnitOfMeasureID"] = unitOfMeasurementId;
            line["UnitOfMeasurementID"] = unitOfMeasurementId;
            line["SKUName"] = unitOfMeasurementId;
            line["TaxCodeID"] = item.TaxCodeId;
            line["GSTTypeID"] = gstTypeId;
            line["IsTaxInclusive"] = item.IsTaxInclusive;
            line["ActivityTypeID"] = item.ActivityTypeId <= 0 ? 1 : item.ActivityTypeId;
            line["AccountID"] = transaction.AccountId;
            line["FinancialAccountID"] = string.Empty;
            line["MemberCreditAccountID"] = item.MemberCreditAllocations.Count > 1
                ? string.Empty
                : item.MemberCreditAccountId;
            line["MemberTypeID"] = item.MemberTypeId;
            line["MembershipCredit"] = item.MemberCreditAllocations.Count > 1
                ? SerializeMemberCreditAllocations(item.MemberCreditAllocations)
                : item.MembershipCredit;

            var usedCredits = new JsonArray();
            if (item.MemberCreditAllocations.Count > 1)
            {
                foreach (var allocation in EffectiveMemberCreditAllocations(item))
                {
                    usedCredits.Add(new JsonObject
                    {
                        ["MemberCreditAccountID"] = allocation.MemberCreditAccountId,
                        ["MemberCredit"] = allocation.Amount
                    });
                }
            }
            line["lstMembershipCredit"] = usedCredits;

            line["BranchID"] = transaction.BranchId;
            line["EditBranchID"] = transaction.BranchId;
            line["GroupID"] = transaction.GroupId;
            line["CurrencyID"] = string.IsNullOrWhiteSpace(currencyId) ? "MYR" : currencyId;
            line["ExchangeRate"] = 1m;
            line["FinancialDate"] = transaction.Date;
            line["GSTTaxPointDate"] = documentTypeId == 52
                ? DateTime.MinValue
                : transaction.Date;
            line["SaveAction"] = 1;
            line["IsDirty"] = true;
            lines.Add(line);
        }

        return lines;
    }

    private static void SynchronizeTransactionTotals(
        Transaction transaction,
        JsonArray documentLines)
    {
        var activeLines = documentLines.OfType<JsonObject>().ToList();
        var totalBeforeTax = Math.Round(
            activeLines.Sum(line => Number(line, "SubTotalBeforeGST")),
            2,
            MidpointRounding.AwayFromZero);
        var totalTax = Math.Round(
            activeLines.Sum(line => Number(line, "TaxAmount")),
            2,
            MidpointRounding.AwayFromZero);
        var totalAfterTaxBeforeRounding = Math.Round(
            activeLines.Sum(line => Number(line, "SubTotal")),
            2,
            MidpointRounding.AwayFromZero);

        transaction.Subtotal = totalBeforeTax;
        transaction.Tax = totalTax;
        transaction.Discount = Math.Round(
            activeLines.Sum(line => Number(line, "Discount")),
            2,
            MidpointRounding.AwayFromZero);
        transaction.Amount = Math.Round(
            totalAfterTaxBeforeRounding + transaction.RoundingAmount,
            2,
            MidpointRounding.AwayFromZero);
        transaction.ItemCount = transaction.Items.Sum(item => Math.Max(1, item.Quantity));
    }

    private async Task<(bool Success, string ErrorMessage, JsonArray RootCredits)> BuildMemberCreditGrantCollectionsAsync(
        Transaction transaction,
        JsonArray documentLines,
        JsonObject header,
        CancellationToken cancellationToken)
    {
        var rootCredits = new JsonArray();

        // Redemption consumes existing Member Credit; it must never grant new credit.
        if (transaction.DocumentTypeId == 52)
        {
            return (true, string.Empty, rootCredits);
        }

        var memberCreditIndexes = transaction.Items
            .Select((item, index) => new { Item = item, Index = index })
            .Where(entry =>
                entry.Item.InventoryTypeId == 7 ||
                string.Equals(entry.Item.Category, "Member Credit", StringComparison.OrdinalIgnoreCase))
            .ToList();

        if (memberCreditIndexes.Count == 0)
        {
            return (true, string.Empty, rootCredits);
        }

        if (string.IsNullOrWhiteSpace(transaction.AccountId))
        {
            return (
                false,
                "A customer is required before purchasing Member Credit.",
                rootCredits);
        }

        foreach (var entry in memberCreditIndexes)
        {
            var item = entry.Item;
            if (string.IsNullOrWhiteSpace(item.InventoryId))
            {
                return (
                    false,
                    $"Member Credit '{item.Name}' has no inventory ID.",
                    rootCredits);
            }

            var fullResult = await memberCreditService.LoadMemberCreditAsync(
                item.InventoryId,
                cancellationToken);

            if (!fullResult.Success || fullResult.Value is null)
            {
                return (
                    false,
                    fullResult.ErrorMessage ??
                    $"Unable to load the Member Credit configuration for '{item.Name}'.",
                    rootCredits);
            }

            var creditSetup = fullResult.Value;
            if (!creditSetup.IsActive ||
                string.Equals(creditSetup.Status, "Inactive", StringComparison.OrdinalIgnoreCase))
            {
                return (
                    false,
                    $"Member Credit '{creditSetup.Name}' is inactive and cannot be purchased.",
                    rootCredits);
            }

            var allocations = (creditSetup.MembershipCredits ??
                               Array.Empty<Beauty_Aesthetics_WebPos.Components.ViewModels.MembershipViewModel.MemberCreditAllocation>())
                .Where(allocation =>
                    !string.IsNullOrWhiteSpace(allocation.MemberTypeId) &&
                    allocation.CreditAmount > 0m)
                .GroupBy(allocation => allocation.MemberTypeId.Trim(), StringComparer.OrdinalIgnoreCase)
                .Select(group => group.Last())
                .ToList();

            if (allocations.Count == 0)
            {
                return (
                    false,
                    $"Member Credit '{creditSetup.Name}' has no configured membership credit amount.",
                    rootCredits);
            }

            if (entry.Index >= documentLines.Count ||
                documentLines[entry.Index] is not JsonObject line)
            {
                return (
                    false,
                    $"Unable to match Member Credit '{creditSetup.Name}' to its sale line.",
                    rootCredits);
            }

            var lineCredits = new JsonArray();
            var quantity = Math.Max(1, item.Quantity);
            var settlementRatio = creditSetup.SettlementRatio > 0m
                ? creditSetup.SettlementRatio
                : 1m;
            var dueDate = creditSetup.ValidityDays > 0
                ? transaction.Date.Date.AddDays(creditSetup.ValidityDays)
                : new DateTime(2049, 12, 31);

            foreach (var allocation in allocations)
            {
                var totalCredit = allocation.CreditAmount * quantity;
                var credit = new JsonObject
                {
                    ["ARAPOutstandingID"] = string.Empty,
                    ["AccountID"] = transaction.AccountId,
                    ["FinancialDate"] = transaction.Date,
                    ["DueDate"] = dueDate,
                    ["DocumentID"] = Text(header, "DocumentID"),
                    ["DisplayCode"] = Text(header, "DisplayCode"),
                    ["DocumentTypeID"] = Integer(header, "DocumentTypeID") == 0
                        ? 5
                        : Integer(header, "DocumentTypeID"),
                    ["DocumentTypeName"] = First(Text(header, "FriendlyDocumentName"), "CashSales"),
                    ["DocumentLineID"] = Text(line, "DocumentLineID"),
                    ["ItemDescription"] = quantity == 1
                        ? First(item.Description, item.Name, creditSetup.Name)
                        : $"{First(item.Description, item.Name, creditSetup.Name)}(x {quantity})",
                    ["CurrencyID"] = First(Text(header, "TransactionCurrencyID"), "MYR"),
                    ["CurrencyName"] = First(Text(header, "TransactionCurrencyName"), "MYR"),
                    ["ExchangeRate"] = Number(header, "ExchangeRate") == 0m
                        ? 1m
                        : Number(header, "ExchangeRate"),
                    ["InterOutletRatio"] = settlementRatio,
                    ["InterOutletAmount"] = totalCredit * settlementRatio,
                    ["MGMTier"] = string.Empty,
                    ["TotalAmount"] = totalCredit,
                    ["BranchID"] = transaction.BranchId,
                    ["GroupID"] = transaction.GroupId,
                    ["LineItemID"] = item.InventoryId,
                    ["MemberTypeID"] = allocation.MemberTypeId.Trim(),
                    ["SaveAction"] = 1,
                    ["IsDirty"] = true
                };

                lineCredits.Add(credit);
                rootCredits.Add(credit.DeepClone());

                Console.WriteLine(
                    $"[Member Credit Grant] Customer={transaction.AccountId} | MemberType={allocation.MemberTypeId} | Item={creditSetup.Name} | Amount={totalCredit:N2} | Qty={quantity}");
            }

            line["lstARAPOutstanding_MemberCredit"] = lineCredits;
        }

        return (true, string.Empty, rootCredits);
    }

    private async Task VerifyMemberCreditGrantPersistenceAsync(
        Transaction transaction,
        JsonArray expectedCredits,
        CancellationToken cancellationToken)
    {
        if (expectedCredits.Count == 0 || string.IsNullOrWhiteSpace(transaction.AccountId))
        {
            return;
        }

        var verifyResult = await customerService.GetRedeemableCreditsAsync(
            transaction.AccountId,
            DateTime.Now,
            cancellationToken);

        if (!verifyResult.Success || verifyResult.Value is null)
        {
            Console.WriteLine(
                $"[Member Credit Verify] Sale {transaction.DocumentId}: GetRedeemableCredits failed: {verifyResult.ErrorMessage}");
            return;
        }

        foreach (var node in expectedCredits)
        {
            if (node is not JsonObject expected)
            {
                continue;
            }

            var expectedLineItemId = Text(expected, "LineItemID");
            var expectedMemberTypeId = Text(expected, "MemberTypeID");
            var expectedAmount = Number(expected, "TotalAmount");

            var persisted = verifyResult.Value.FirstOrDefault(credit =>
                !string.IsNullOrWhiteSpace(credit.ARAPOutstandingID) &&
                string.Equals(credit.LineItemID, expectedLineItemId, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(credit.MemberTypeID, expectedMemberTypeId, StringComparison.OrdinalIgnoreCase) &&
                (string.IsNullOrWhiteSpace(credit.DocumentID) ||
                 string.IsNullOrWhiteSpace(transaction.DocumentId) ||
                 string.Equals(credit.DocumentID, transaction.DocumentId, StringComparison.OrdinalIgnoreCase)) &&
                (expectedAmount <= 0m || Math.Abs(credit.TotalAmount - expectedAmount) < 0.01m));

            if (persisted is null)
            {
                Console.WriteLine(
                    $"[Member Credit Verify] Sale {transaction.DocumentId}: no redeemable ARAP record found for Item={expectedLineItemId}, MemberType={expectedMemberTypeId}, Amount={expectedAmount:N2}.");
                continue;
            }

            Console.WriteLine(
                $"[Member Credit Verify] Sale {transaction.DocumentId}: ARAPOutstandingID={persisted.ARAPOutstandingID} | Item={persisted.LineItemID} | MemberType={persisted.MemberTypeID} | Balance={persisted.NetBalanceAfterUtilised:N2}");
        }
    }

    private async Task<decimal?> CaptureStep30TotalCreditBeforeAsync(
        Transaction transaction,
        CancellationToken cancellationToken)
    {
        if (transaction.DocumentTypeId != 52 ||
            string.IsNullOrWhiteSpace(transaction.AccountId) ||
            !transaction.Items.Any(item => item.ActivityTypeId == 6))
        {
            return null;
        }

        var summaryResult = await customerService.GetBalanceSummaryAsync(
            transaction.AccountId,
            cancellationToken);

        if (!summaryResult.Success || summaryResult.Value is null)
        {
            Console.WriteLine(
                $"[Member Credit Step 30] BEFORE TOTAL WARNING | Customer={transaction.AccountId} | Unable to load GetMemberBalanceSummary: {summaryResult.ErrorMessage}");
            return null;
        }

        Console.WriteLine(
            $"[Member Credit Step 30] BEFORE TOTAL | Customer={transaction.AccountId} | Credit={summaryResult.Value.CreditBalance:N2}");

        return summaryResult.Value.CreditBalance;
    }

    private async Task VerifyStep30RedemptionAfterSaveAsync(
        Transaction transaction,
        (string AccountId, decimal Amount, decimal? BeforeBalance)? singleVerification,
        IReadOnlyList<(string AccountId, decimal Amount, decimal? BeforeBalance)>? multiVerification,
        decimal? totalCreditBefore,
        CancellationToken cancellationToken)
    {
        if (transaction.DocumentTypeId != 52 ||
            string.IsNullOrWhiteSpace(transaction.DocumentId) ||
            string.IsNullOrWhiteSpace(transaction.AccountId))
        {
            return;
        }

        var expectedAllocations = transaction.Items
            .Where(item => item.ActivityTypeId == 6)
            .SelectMany(EffectiveMemberCreditAllocations)
            .Where(allocation =>
                !string.IsNullOrWhiteSpace(allocation.MemberCreditAccountId) &&
                allocation.Amount > 0m)
            .GroupBy(
                allocation => allocation.MemberCreditAccountId.Trim(),
                StringComparer.OrdinalIgnoreCase)
            .ToDictionary(
                group => group.Key,
                group => Math.Round(
                    group.Sum(allocation => allocation.Amount),
                    2,
                    MidpointRounding.AwayFromZero),
                StringComparer.OrdinalIgnoreCase);

        if (expectedAllocations.Count == 0)
        {
            Console.WriteLine(
                $"[Member Credit Step 30] WARNING | Document={transaction.DocumentId} | No Member Credit allocations were available for verification.");
            return;
        }

        var expectedReceiptLineCount = transaction.Items
            .Where(item => item.ActivityTypeId == 6)
            .SelectMany(EffectiveMemberCreditAllocations)
            .Count(allocation =>
                !string.IsNullOrWhiteSpace(allocation.MemberCreditAccountId) &&
                allocation.Amount > 0m);

        var totalRedeemed = Math.Round(
            expectedAllocations.Values.Sum(),
            2,
            MidpointRounding.AwayFromZero);

        var beforeByAccount = new Dictionary<string, decimal?>(StringComparer.OrdinalIgnoreCase);

        if (singleVerification.HasValue)
        {
            beforeByAccount[singleVerification.Value.AccountId] =
                singleVerification.Value.BeforeBalance;
        }

        if (multiVerification is not null)
        {
            foreach (var item in multiVerification)
            {
                beforeByAccount[item.AccountId] = item.BeforeBalance;
            }
        }

        var definiteFailure = false;
        var accountBalancesFullyVerified = true;

        // Step 30.1 — reload GetRedeemableCredits and confirm each used account decreased.
        var afterCreditsResult = await customerService.GetRedeemableCreditsAsync(
            transaction.AccountId,
            DateTime.Now,
            cancellationToken);

        if (!afterCreditsResult.Success || afterCreditsResult.Value is null)
        {
            accountBalancesFullyVerified = false;
            Console.WriteLine(
                $"[Member Credit Step 30] ACCOUNT BALANCE WARNING | Customer={transaction.AccountId} | Unable to reload GetRedeemableCredits: {afterCreditsResult.ErrorMessage}");
        }
        else
        {
            var afterByAccount = afterCreditsResult.Value
                .Where(credit => !string.IsNullOrWhiteSpace(credit.ARAPOutstandingID))
                .GroupBy(
                    credit => credit.ARAPOutstandingID!.Trim(),
                    StringComparer.OrdinalIgnoreCase)
                .ToDictionary(
                    group => group.Key,
                    group => group.First().NetBalanceAfterUtilised,
                    StringComparer.OrdinalIgnoreCase);

            foreach (var expected in expectedAllocations)
            {
                var actualAfter = afterByAccount.TryGetValue(expected.Key, out var remaining)
                    ? remaining
                    : 0m;

                if (!beforeByAccount.TryGetValue(expected.Key, out var beforeBalance) ||
                    !beforeBalance.HasValue)
                {
                    accountBalancesFullyVerified = false;
                    Console.WriteLine(
                        $"[Member Credit Step 30] ACCOUNT BALANCE WARNING | Account={expected.Key} | Redeemed={expected.Value:N2} | ActualAfter={actualAfter:N2} | Before balance unavailable.");
                    continue;
                }

                var expectedAfter = Math.Max(
                    0m,
                    Math.Round(
                        beforeBalance.Value - expected.Value,
                        2,
                        MidpointRounding.AwayFromZero));

                var passed = Math.Abs(actualAfter - expectedAfter) < 0.01m;
                definiteFailure |= !passed;

                Console.WriteLine(
                    $"[Member Credit Step 30] ACCOUNT BALANCE {(passed ? "PASS" : "FAIL")} | " +
                    $"Account={expected.Key} | Before={beforeBalance.Value:N2} | Redeemed={expected.Value:N2} | " +
                    $"ExpectedAfter={expectedAfter:N2} | ActualAfter={actualAfter:N2}");
            }
        }

        // Step 30.2 — reload GetMemberBalanceSummary and confirm total Credit decreased.
        var totalCreditVerified = true;
        var afterSummaryResult = await customerService.GetBalanceSummaryAsync(
            transaction.AccountId,
            cancellationToken);

        if (!afterSummaryResult.Success || afterSummaryResult.Value is null)
        {
            totalCreditVerified = false;
            Console.WriteLine(
                $"[Member Credit Step 30] TOTAL CREDIT WARNING | Customer={transaction.AccountId} | Unable to reload GetMemberBalanceSummary: {afterSummaryResult.ErrorMessage}");
        }
        else if (!totalCreditBefore.HasValue)
        {
            totalCreditVerified = false;
            Console.WriteLine(
                $"[Member Credit Step 30] TOTAL CREDIT WARNING | Customer={transaction.AccountId} | After={afterSummaryResult.Value.CreditBalance:N2} | Before total Credit was unavailable.");
        }
        else
        {
            var expectedTotalAfter = Math.Max(
                0m,
                Math.Round(
                    totalCreditBefore.Value - totalRedeemed,
                    2,
                    MidpointRounding.AwayFromZero));
            var actualTotalAfter = afterSummaryResult.Value.CreditBalance;
            var totalPassed = Math.Abs(actualTotalAfter - expectedTotalAfter) < 0.01m;
            definiteFailure |= !totalPassed;

            Console.WriteLine(
                $"[Member Credit Step 30] TOTAL CREDIT {(totalPassed ? "PASS" : "FAIL")} | " +
                $"Before={totalCreditBefore.Value:N2} | Redeemed={totalRedeemed:N2} | " +
                $"ExpectedAfter={expectedTotalAfter:N2} | ActualAfter={actualTotalAfter:N2}");
        }

        // Step 30.3 — load the official saved redemption and compare its persisted
        // document lines and Member Credit (-10) receipt lines to the request.
        var savedRecordVerified = true;
        var savedResult = await cashSalesAC.LoadRedemptionRecordAsync(
            transaction.DocumentId,
            cancellationToken);

        if (!savedResult.Success || savedResult.Value is null)
        {
            savedRecordVerified = false;
            Console.WriteLine(
                $"[Member Credit Step 30] SAVED DOCUMENT WARNING | Document={transaction.DocumentId} | /api/Doc_Redemption/LoadRecord could not be verified: {savedResult.ErrorMessage}");
        }
        else
        {
            var saved = savedResult.Value;
            var savedHeader = saved["objDoc_CashSales"] as JsonObject;
            var savedLines = saved["lstDocumentLine"] as JsonArray ?? new JsonArray();
            var savedReceipts = saved["lstReceiptLines"] as JsonArray ?? new JsonArray();

            var savedDocumentId = savedHeader is null
                ? string.Empty
                : Text(savedHeader, "DocumentID");
            var savedDocumentTypeId = savedHeader is null
                ? 0
                : Integer(savedHeader, "DocumentTypeID");

            var savedRedemptionLines = savedLines
                .OfType<JsonObject>()
                .Where(line => Integer(line, "ActivityTypeID") == 6)
                .ToList();

            var savedLineAllocations =
                new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase);

            foreach (var line in savedRedemptionLines)
            {
                var singleAccountId = Text(line, "MemberCreditAccountID").Trim();

                if (!string.IsNullOrWhiteSpace(singleAccountId))
                {
                    var amount = Number(line, "SubTotal");
                    if (amount <= 0m)
                    {
                        amount = Number(line, "Amount");
                    }

                    if (amount > 0m)
                    {
                        savedLineAllocations[singleAccountId] =
                            savedLineAllocations.TryGetValue(singleAccountId, out var current)
                                ? current + amount
                                : amount;
                    }

                    continue;
                }

                var nestedCredits = line["lstMembershipCredit"] as JsonArray;
                var nestedFound = false;

                if (nestedCredits is not null)
                {
                    foreach (var node in nestedCredits.OfType<JsonObject>())
                    {
                        var accountId = Text(node, "MemberCreditAccountID").Trim();
                        var amount = Number(node, "MemberCredit");

                        if (string.IsNullOrWhiteSpace(accountId) || amount <= 0m)
                        {
                            continue;
                        }

                        nestedFound = true;
                        savedLineAllocations[accountId] =
                            savedLineAllocations.TryGetValue(accountId, out var current)
                                ? current + amount
                                : amount;
                    }
                }

                if (!nestedFound)
                {
                    foreach (var allocation in ParseMemberCreditAllocations(
                                 Text(line, "MembershipCredit"),
                                 Text(line, "MemberTypeID")))
                    {
                        var accountId = allocation.MemberCreditAccountId.Trim();
                        if (string.IsNullOrWhiteSpace(accountId) || allocation.Amount <= 0m)
                        {
                            continue;
                        }

                        savedLineAllocations[accountId] =
                            savedLineAllocations.TryGetValue(accountId, out var current)
                                ? current + allocation.Amount
                                : allocation.Amount;
                    }
                }
            }

            var savedCreditReceipts = savedReceipts
                .OfType<JsonObject>()
                .Where(receipt => Integer(receipt, "POSPaymentTypeID") == -10)
                .ToList();

            var savedReceiptAllocations =
                new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase);

            foreach (var receipt in savedCreditReceipts)
            {
                var accountId = Text(receipt, "SourceDocumentLineID").Trim();
                var amount = Number(receipt, "POSReceiptLineAmount");

                if (string.IsNullOrWhiteSpace(accountId) || amount <= 0m)
                {
                    continue;
                }

                savedReceiptAllocations[accountId] =
                    savedReceiptAllocations.TryGetValue(accountId, out var current)
                        ? current + amount
                        : amount;
            }

            var headerPassed =
                savedDocumentTypeId == 52 &&
                string.Equals(
                    savedDocumentId,
                    transaction.DocumentId,
                    StringComparison.OrdinalIgnoreCase);

            var lineAccountsPassed =
                MemberCreditAmountMapsMatch(expectedAllocations, savedLineAllocations);

            var receiptAccountsPassed =
                MemberCreditAmountMapsMatch(expectedAllocations, savedReceiptAllocations);

            var receiptCountPassed =
                savedCreditReceipts.Count == expectedReceiptLineCount;

            var savedReceiptTotal = Math.Round(
                savedCreditReceipts.Sum(receipt => Number(receipt, "POSReceiptLineAmount")),
                2,
                MidpointRounding.AwayFromZero);
            var redeemedAmountPassed =
                Math.Abs(savedReceiptTotal - totalRedeemed) < 0.01m;

            var savedPassed =
                headerPassed &&
                savedRedemptionLines.Count == transaction.Items.Count(item => item.ActivityTypeId == 6) &&
                lineAccountsPassed &&
                receiptAccountsPassed &&
                receiptCountPassed &&
                redeemedAmountPassed;

            definiteFailure |= !savedPassed;

            Console.WriteLine(
                $"[Member Credit Step 30] SAVED DOCUMENT {(savedPassed ? "PASS" : "FAIL")} | " +
                $"Document={transaction.DocumentId} | DocumentTypeID={savedDocumentTypeId} | " +
                $"RedemptionLines={savedRedemptionLines.Count} | CreditReceiptLines={savedCreditReceipts.Count} | " +
                $"RedeemTotal={totalRedeemed:N2} | SavedReceiptTotal={savedReceiptTotal:N2}");

            if (!lineAccountsPassed)
            {
                Console.WriteLine(
                    $"[Member Credit Step 30] SAVED LINES FAIL | Expected={FormatMemberCreditAmounts(expectedAllocations)} | Saved={FormatMemberCreditAmounts(savedLineAllocations)}");
            }

            if (!receiptAccountsPassed || !receiptCountPassed)
            {
                Console.WriteLine(
                    $"[Member Credit Step 30] SAVED RECEIPTS FAIL | Expected={FormatMemberCreditAmounts(expectedAllocations)} | Saved={FormatMemberCreditAmounts(savedReceiptAllocations)} | ExpectedLines={expectedReceiptLineCount} | SavedLines={savedCreditReceipts.Count}");
            }
        }

        var fullyVerified =
            accountBalancesFullyVerified &&
            totalCreditVerified &&
            savedRecordVerified;

        var status = definiteFailure
            ? "FAIL"
            : fullyVerified
                ? "PASS"
                : "WARNING";

        Console.WriteLine(
            $"[Member Credit Step 30] {status} | Document={transaction.DocumentId} | " +
            $"Customer={transaction.AccountId} | Accounts={expectedAllocations.Count} | RedeemTotal={totalRedeemed:N2}");
    }

    private static bool MemberCreditAmountMapsMatch(
        IReadOnlyDictionary<string, decimal> expected,
        IReadOnlyDictionary<string, decimal> actual)
    {
        if (expected.Count != actual.Count)
        {
            return false;
        }

        foreach (var item in expected)
        {
            if (!actual.TryGetValue(item.Key, out var actualAmount) ||
                Math.Abs(actualAmount - item.Value) >= 0.01m)
            {
                return false;
            }
        }

        return true;
    }

    private static string FormatMemberCreditAmounts(
        IReadOnlyDictionary<string, decimal> values) =>
        values.Count == 0
            ? "(none)"
            : string.Join(
                ", ",
                values
                    .OrderBy(item => item.Key, StringComparer.OrdinalIgnoreCase)
                    .Select(item => $"{item.Key}={item.Value:N2}"));

    private static (bool Success, string ErrorMessage) ValidateFinalRedemptionRequest(
        Transaction transaction,
        JsonObject document)
    {
        if (transaction.DocumentTypeId != 52)
        {
            return (true, string.Empty);
        }

        var header = document["objDoc_CashSales"] as JsonObject;
        if (header is null || Integer(header, "DocumentTypeID") != 52)
        {
            return (
                false,
                "Final redemption request is invalid: DocumentTypeID must be 52.");
        }

        if (string.IsNullOrWhiteSpace(Text(header, "AccountID")))
        {
            return (
                false,
                "Final redemption request is invalid: customer AccountID is missing.");
        }

        if (string.IsNullOrWhiteSpace(Text(header, "BranchID")))
        {
            return (
                false,
                "Final redemption request is invalid: BranchID is missing.");
        }

        if (string.IsNullOrWhiteSpace(Text(header, "GroupID")))
        {
            return (
                false,
                "Final redemption request is invalid: GroupID is missing. Refresh the branch and try again.");
        }

        var documentLines = document["lstDocumentLine"] as JsonArray ?? new JsonArray();
        var redemptionLines = documentLines
            .OfType<JsonObject>()
            .Where(line => Integer(line, "ActivityTypeID") == 6)
            .ToList();

        var expectedRedemptionItems = transaction.Items
            .Where(item => item.ActivityTypeId == 6)
            .ToList();

        if (redemptionLines.Count == 0 ||
            redemptionLines.Count != expectedRedemptionItems.Count)
        {
            return (
                false,
                "Final redemption request is invalid: redemption document lines are missing or incomplete.");
        }

        foreach (var line in redemptionLines)
        {
            if (!line.ContainsKey("MemberCreditAccountID") ||
                !line.ContainsKey("MemberTypeID") ||
                !line.ContainsKey("MembershipCredit") ||
                !line.ContainsKey("lstMembershipCredit"))
            {
                return (
                    false,
                    "Final redemption request is invalid: a redemption line is missing Member Credit fields.");
            }

            var singleAccountId = Text(line, "MemberCreditAccountID");
            var serializedMultiple = Text(line, "MembershipCredit");
            var multiple = line["lstMembershipCredit"] as JsonArray;

            var hasSingle = !string.IsNullOrWhiteSpace(singleAccountId);
            var hasMultiple =
                !string.IsNullOrWhiteSpace(serializedMultiple) &&
                multiple is not null &&
                multiple.Count > 0;

            if (hasSingle == hasMultiple)
            {
                return (
                    false,
                    "Final redemption request is invalid: each redemption line must contain either one MemberCreditAccountID or a multi-credit allocation collection.");
            }
        }

        var receipts = document["lstReceiptLines"] as JsonArray ?? new JsonArray();
        var creditReceipts = receipts
            .OfType<JsonObject>()
            .Where(receipt => Integer(receipt, "POSPaymentTypeID") == -10)
            .ToList();

        var expectedReceiptAmounts = expectedRedemptionItems
            .SelectMany(item => EffectiveMemberCreditAllocations(item)
                .Select(allocation => new
                {
                    AccountId = allocation.MemberCreditAccountId.Trim(),
                    InventoryId = item.InventoryId?.Trim() ?? string.Empty,
                    Amount = Math.Round(allocation.Amount, 2, MidpointRounding.AwayFromZero)
                }))
            .GroupBy(
                item => $"{item.AccountId}\u001f{item.InventoryId}",
                StringComparer.OrdinalIgnoreCase)
            .ToDictionary(
                group => group.Key,
                group => Math.Round(group.Sum(item => item.Amount), 2, MidpointRounding.AwayFromZero),
                StringComparer.OrdinalIgnoreCase);

        if (creditReceipts.Count == 0)
        {
            return (
                false,
                "Final redemption request is invalid: POSPaymentTypeID = -10 receipt lines are missing.");
        }

        var requiredReceiptFields = new[]
        {
            "POSReceiptLineID",
            "DocumentID",
            "FinancialAccountID",
            "BankName",
            "POSReceiptLineAmount",
            "AccountID",
            "Description",
            "Reference",
            "PackageID",
            "SourceDocumentLineID",
            "QuantityRedeemed",
            "SourceUnitPrice",
            "SourceUnitActualValue",
            "InventoryID",
            "CurrencyID",
            "CurrencyName",
            "GroupID",
            "ExchangeRate",
            "AmountInForeignCurrency",
            "POSPaymentTypeID",
            "POSReceiptChangeAmount",
            "BranchID"
        };

        foreach (var receipt in creditReceipts)
        {
            if (requiredReceiptFields.Any(field => !receipt.ContainsKey(field)))
            {
                return (
                    false,
                    "Final redemption request is invalid: a Member Credit receipt line is missing required Senang receipt fields.");
            }

            var accountId = Text(receipt, "SourceDocumentLineID").Trim();
            var inventoryId = Text(receipt, "InventoryID").Trim();
            var receiptLineId = Text(receipt, "POSReceiptLineID").Trim();
            var description = Text(receipt, "Description");
            var amount = Math.Round(
                Number(receipt, "POSReceiptLineAmount"),
                2,
                MidpointRounding.AwayFromZero);

            if (string.IsNullOrWhiteSpace(receiptLineId) ||
                string.IsNullOrWhiteSpace(accountId) ||
                amount <= 0m ||
                !string.Equals(description, "Member Credit", StringComparison.OrdinalIgnoreCase) ||
                string.IsNullOrWhiteSpace(Text(receipt, "BranchID")) ||
                string.IsNullOrWhiteSpace(Text(receipt, "GroupID")))
            {
                return (
                    false,
                    $"Final redemption request is invalid for Member Credit {First(accountId, "unknown")}.");
            }

            var key = $"{accountId}\u001f{inventoryId}";
            if (!expectedReceiptAmounts.TryGetValue(key, out var expectedAmount))
            {
                return (
                    false,
                    $"Final redemption request contains an unexpected Member Credit receipt for {accountId}.");
            }

            expectedReceiptAmounts[key] = Math.Round(
                expectedAmount - amount,
                2,
                MidpointRounding.AwayFromZero);
        }

        var unmatched = expectedReceiptAmounts
            .Where(pair => Math.Abs(pair.Value) > 0.009m)
            .ToList();

        if (unmatched.Count > 0)
        {
            var firstMismatch = unmatched[0];
            var accountId = firstMismatch.Key.Split('\u001f')[0];

            return (
                false,
                $"Final redemption request receipt amount does not match the allocated Member Credit for {accountId}.");
        }

        Console.WriteLine(
            $"[Member Credit Step 29] PASS | DocumentTypeID=52 | " +
            $"RedemptionLines={redemptionLines.Count} | CreditReceiptLines={creditReceipts.Count}");

        return (true, string.Empty);
    }

    private static bool CreditScopeAllows(string? scope, string currentValue)
    {
        if (string.IsNullOrWhiteSpace(scope))
        {
            return true;
        }

        if (string.IsNullOrWhiteSpace(currentValue))
        {
            return false;
        }

        return scope
            .Split([',', ';', '|'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Any(value =>
                value == "*" ||
                value.Equals("ALL", StringComparison.OrdinalIgnoreCase) ||
                value.Equals(currentValue, StringComparison.OrdinalIgnoreCase));
    }

    private static (bool Success, string ErrorMessage) ValidateMemberCreditBranchEligibility(
        RedeemableCreditDTO credit,
        Transaction transaction)
    {
        var accountId = credit.ARAPOutstandingID?.Trim() ?? string.Empty;

        if (string.IsNullOrWhiteSpace(transaction.BranchId) ||
            string.IsNullOrWhiteSpace(transaction.GroupId))
        {
            return (
                false,
                "The working branch/group is missing. Select the working branch again before redeeming Member Credit.");
        }

        if (!string.IsNullOrWhiteSpace(credit.AccountID) &&
            !string.Equals(
                credit.AccountID,
                transaction.AccountId,
                StringComparison.OrdinalIgnoreCase))
        {
            return (
                false,
                $"Member Credit {accountId} belongs to another customer and cannot be redeemed.");
        }

        if (!CreditScopeAllows(credit.RedeemableAtBranch, transaction.BranchId))
        {
            return (
                false,
                $"Member Credit {accountId} is not redeemable at branch {transaction.BranchId}.");
        }

        if (!CreditScopeAllows(credit.RedeemableAtGroup, transaction.GroupId))
        {
            return (
                false,
                $"Member Credit {accountId} is not redeemable for branch group {transaction.GroupId}.");
        }

        var isCrossBranch =
            !string.IsNullOrWhiteSpace(credit.BranchID) &&
            !string.Equals(
                credit.BranchID,
                transaction.BranchId,
                StringComparison.OrdinalIgnoreCase);

        if (isCrossBranch && credit.InterOutletRatio <= 0m)
        {
            return (
                false,
                $"Member Credit {accountId} has no valid inter-outlet settlement ratio for redemption at branch {transaction.BranchId}.");
        }

        return (true, string.Empty);
    }

    private async Task<(bool Success, HttpStatusCode StatusCode, string ErrorMessage)> ValidateMemberCreditRedemptionBeforeCreateAsync(
        Transaction transaction,
        CancellationToken cancellationToken)
    {
        var redemptionItems = transaction.Items
            .Where(item => item.ActivityTypeId == 6)
            .ToList();

        var isMemberCreditRedemption =
            transaction.DocumentTypeId == 52 ||
            redemptionItems.Count > 0;

        if (!isMemberCreditRedemption)
        {
            return (true, HttpStatusCode.OK, string.Empty);
        }

        if (string.IsNullOrWhiteSpace(transaction.AccountId))
        {
            return (
                false,
                HttpStatusCode.BadRequest,
                "Member Credit redemption requires a customer before the sale can be completed.");
        }

        if (redemptionItems.Count == 0)
        {
            return (
                false,
                HttpStatusCode.BadRequest,
                "Member Credit redemption has no redemption lines to validate.");
        }

        var allocatedByAccount = new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase);
        decimal expectedRedemptionTotal = 0m;
        decimal allocatedRedemptionTotal = 0m;

        foreach (var item in redemptionItems)
        {
            var lineAmount = RedeemableLineSubTotal(item);

            if (lineAmount <= 0m)
            {
                return (
                    false,
                    HttpStatusCode.BadRequest,
                    $"Member Credit redemption line '{First(item.Name, item.InventoryId, "Item")}' has no redeemable amount.");
            }

            var rawAllocationAccountIds = item.MemberCreditAllocations
                .Where(allocation =>
                    !string.IsNullOrWhiteSpace(allocation.MemberCreditAccountId) &&
                    allocation.Amount > 0m)
                .Select(allocation => allocation.MemberCreditAccountId.Trim())
                .ToList();

            if (rawAllocationAccountIds.Count !=
                rawAllocationAccountIds.Distinct(StringComparer.OrdinalIgnoreCase).Count())
            {
                return (
                    false,
                    HttpStatusCode.BadRequest,
                    $"Member Credit redemption line '{First(item.Name, item.InventoryId, "Item")}' contains the same credit account more than once.");
            }

            var allocations = EffectiveMemberCreditAllocations(item);
            if (allocations.Count == 0)
            {
                return (
                    false,
                    HttpStatusCode.BadRequest,
                    $"Member Credit redemption line '{First(item.Name, item.InventoryId, "Item")}' has no Member Credit account allocated.");
            }

            var lineAllocated = Math.Round(
                allocations.Sum(allocation => allocation.Amount),
                2,
                MidpointRounding.AwayFromZero);

            if (Math.Abs(lineAllocated - lineAmount) > 0.009m)
            {
                return (
                    false,
                    HttpStatusCode.Conflict,
                    $"Member Credit allocation for '{First(item.Name, item.InventoryId, "Item")}' is incomplete. " +
                    $"Allocated RM {lineAllocated:N2}, but the redemption line requires RM {lineAmount:N2}.");
            }

            foreach (var allocation in allocations)
            {
                var accountId = allocation.MemberCreditAccountId?.Trim() ?? string.Empty;
                var amount = Math.Round(
                    allocation.Amount,
                    2,
                    MidpointRounding.AwayFromZero);

                if (string.IsNullOrWhiteSpace(accountId))
                {
                    return (
                        false,
                        HttpStatusCode.BadRequest,
                        "A Member Credit allocation is missing its ARAPOutstandingID.");
                }

                if (amount <= 0m)
                {
                    return (
                        false,
                        HttpStatusCode.BadRequest,
                        $"Member Credit {accountId} has an invalid redemption amount.");
                }

                allocatedByAccount[accountId] =
                    allocatedByAccount.TryGetValue(accountId, out var existing)
                        ? existing + amount
                        : amount;
            }

            expectedRedemptionTotal += lineAmount;
            allocatedRedemptionTotal += lineAllocated;
        }

        expectedRedemptionTotal = Math.Round(
            expectedRedemptionTotal,
            2,
            MidpointRounding.AwayFromZero);
        allocatedRedemptionTotal = Math.Round(
            allocatedRedemptionTotal,
            2,
            MidpointRounding.AwayFromZero);

        if (Math.Abs(expectedRedemptionTotal - allocatedRedemptionTotal) > 0.009m)
        {
            return (
                false,
                HttpStatusCode.Conflict,
                $"Member Credit redemption allocation is inconsistent. " +
                $"Expected RM {expectedRedemptionTotal:N2}, but RM {allocatedRedemptionTotal:N2} is allocated.");
        }

        // Step 40: use the same shared Member Credit wallet service used by
        // New Sales and Case Note Billing for the final pre-save revalidation.
        var latestResult = await memberCreditWalletService.RevalidateAsync(
            transaction.AccountId,
            allocatedByAccount,
            DateTime.Now,
            transaction.BranchId,
            transaction.GroupId,
            cancellationToken);

        if (!latestResult.Success || latestResult.Value is null)
        {
            return (
                false,
                HttpStatusCode.Conflict,
                latestResult.ErrorMessage ??
                "Member Credit balances could not be refreshed before saving. " +
                "No redemption was created. Refresh the customer's credits and try again.");
        }

        var latestByAccount = latestResult.Value.Accounts
            .Where(credit => !string.IsNullOrWhiteSpace(credit.ARAPOutstandingID))
            .GroupBy(
                credit => credit.ARAPOutstandingID!.Trim(),
                StringComparer.OrdinalIgnoreCase)
            .ToDictionary(
                group => group.Key,
                group => group.First(),
                StringComparer.OrdinalIgnoreCase);

        foreach (var allocation in allocatedByAccount)
        {
            if (!latestByAccount.TryGetValue(allocation.Key, out var latestCredit))
            {
                return (
                    false,
                    HttpStatusCode.Conflict,
                    $"Member Credit {allocation.Key} is no longer available. " +
                    "The customer's credit balances were refreshed and the redemption was not saved.");
            }

            var branchEligibility =
                ValidateMemberCreditBranchEligibility(latestCredit, transaction);

            if (!branchEligibility.Success)
            {
                Console.WriteLine(
                    $"[Member Credit Step 38] BLOCKED | Account={allocation.Key} | " +
                    $"RedeemBranch={transaction.BranchId} | RedeemGroup={transaction.GroupId} | " +
                    $"CreditBranch={latestCredit.BranchID} | CreditGroup={latestCredit.GroupID} | " +
                    $"InterOutletRatio={latestCredit.InterOutletRatio:N4} | {branchEligibility.ErrorMessage}");

                return (
                    false,
                    HttpStatusCode.Conflict,
                    branchEligibility.ErrorMessage);
            }

            if (!latestCredit.IsRedeemable)
            {
                return (
                    false,
                    HttpStatusCode.Conflict,
                    $"Member Credit {allocation.Key} is no longer redeemable. " +
                    "The redemption was not saved.");
            }

            if (latestCredit.DueDate.Year > 1900 &&
                latestCredit.DueDate.Date < DateTime.Today)
            {
                return (
                    false,
                    HttpStatusCode.Conflict,
                    $"Member Credit {allocation.Key} expired on {latestCredit.DueDate:dd/MM/yyyy}. " +
                    "The redemption was not saved.");
            }

            var latestBalance = Math.Round(
                Math.Max(0m, latestCredit.NetBalanceAfterUtilised),
                2,
                MidpointRounding.AwayFromZero);

            if (latestBalance <= 0m)
            {
                return (
                    false,
                    HttpStatusCode.Conflict,
                    $"Member Credit {allocation.Key} no longer has an available balance. " +
                    "The redemption was not saved.");
            }

            var requestedAmount = Math.Round(
                allocation.Value,
                2,
                MidpointRounding.AwayFromZero);

            if (requestedAmount - latestBalance > 0.009m)
            {
                return (
                    false,
                    HttpStatusCode.Conflict,
                    $"Member Credit {allocation.Key} changed before saving. " +
                    $"Latest balance is RM {latestBalance:N2}, but RM {requestedAmount:N2} is allocated. " +
                    "The redemption was not saved.");
            }
        }

        Console.WriteLine(
            $"[Member Credit Step 38] PASS | Customer={transaction.AccountId} | " +
            $"Branch={transaction.BranchId} | Group={transaction.GroupId} | " +
            $"Accounts={allocatedByAccount.Count} | BranchEligibility=True | SettlementRatioValidated=True");

        Console.WriteLine(
            $"[Member Credit Step 28] PASS | Customer={transaction.AccountId} | " +
            $"Accounts={allocatedByAccount.Count} | RedeemTotal={allocatedRedemptionTotal:N2}");

        return (true, HttpStatusCode.OK, string.Empty);
    }

    private async Task<(string AccountId, decimal Amount, decimal? BeforeBalance)?> CaptureSingleMemberCreditRedemptionAsync(
        Transaction transaction,
        CancellationToken cancellationToken)
    {
        if (transaction.DocumentTypeId != 52 || string.IsNullOrWhiteSpace(transaction.AccountId))
        {
            return null;
        }

        var allocations = transaction.Items
            .Where(item => item.ActivityTypeId == 6)
            .SelectMany(EffectiveMemberCreditAllocations)
            .Where(allocation =>
                !string.IsNullOrWhiteSpace(allocation.MemberCreditAccountId) &&
                allocation.Amount > 0m)
            .ToList();

        // Step 26 is intentionally limited to the single-credit checkpoint.
        // Multi-credit FIFO verification is handled by the next stage.
        if (allocations.Count != 1)
        {
            return null;
        }

        var allocation = allocations[0];
        decimal? beforeBalance = null;

        var beforeResult = await customerService.GetRedeemableCreditsAsync(
            transaction.AccountId,
            DateTime.Now,
            cancellationToken);

        if (beforeResult.Success && beforeResult.Value is not null)
        {
            var sourceCredit = beforeResult.Value.FirstOrDefault(credit =>
                string.Equals(
                    credit.ARAPOutstandingID,
                    allocation.MemberCreditAccountId,
                    StringComparison.OrdinalIgnoreCase));

            if (sourceCredit is not null)
            {
                beforeBalance = sourceCredit.NetBalanceAfterUtilised;
            }
            else
            {
                Console.WriteLine(
                    $"[Member Credit Single Verify] BEFORE WARNING | Account={allocation.MemberCreditAccountId} was not returned by GetRedeemableCredits.");
            }
        }
        else
        {
            Console.WriteLine(
                $"[Member Credit Single Verify] BEFORE WARNING | Unable to load redeemable credits for Customer={transaction.AccountId}: {beforeResult.ErrorMessage}");
        }

        Console.WriteLine(
            $"[Member Credit Single Verify] BEFORE | Account={allocation.MemberCreditAccountId} | Redeem={allocation.Amount:N2} | Balance={(beforeBalance.HasValue ? beforeBalance.Value.ToString("N2") : "unknown")}");

        return (
            allocation.MemberCreditAccountId,
            allocation.Amount,
            beforeBalance);
    }

    private async Task VerifySingleMemberCreditRedemptionAsync(
        Transaction transaction,
        (string AccountId, decimal Amount, decimal? BeforeBalance)? verification,
        CancellationToken cancellationToken)
    {
        if (verification is null)
        {
            return;
        }

        var expected = verification.Value;

        if (string.IsNullOrWhiteSpace(transaction.DocumentId))
        {
            Console.WriteLine(
                $"[Member Credit Single Verify] FAIL | Account={expected.AccountId} | Created redemption has no DocumentID, so the saved receipt cannot be verified.");
            return;
        }

        var receiptResult = await cashSalesAC.LoadReceiptLinesAsync(
            transaction.DocumentId,
            cancellationToken);

        if (!receiptResult.Success || receiptResult.Value is null)
        {
            Console.WriteLine(
                $"[Member Credit Single Verify] RECEIPT FAIL | Document={transaction.DocumentId} | Unable to reload receipt lines: {receiptResult.ErrorMessage}");
        }
        else
        {
            var memberCreditReceipts = receiptResult.Value
                .Where(line => line.POSPaymentTypeID == -10)
                .ToList();

            var matchingReceipts = memberCreditReceipts
                .Where(line =>
                    !string.IsNullOrWhiteSpace(line.POSReceiptLineID) &&
                    string.Equals(
                        line.SourceDocumentLineID,
                        expected.AccountId,
                        StringComparison.OrdinalIgnoreCase) &&
                    Math.Abs(line.POSReceiptLineAmount - expected.Amount) < 0.01m)
                .ToList();

            var receiptPassed =
                memberCreditReceipts.Count == 1 &&
                matchingReceipts.Count == 1;

            Console.WriteLine(
                $"[Member Credit Single Verify] RECEIPT {(receiptPassed ? "PASS" : "FAIL")} | Document={transaction.DocumentId} | ExpectedAccount={expected.AccountId} | ExpectedAmount={expected.Amount:N2} | SavedMinus10Lines={memberCreditReceipts.Count} | MatchingLines={matchingReceipts.Count}");

            if (matchingReceipts.Count == 1)
            {
                var saved = matchingReceipts[0];
                Console.WriteLine(
                    $"[Member Credit Single Verify] RECEIPT SAVED | POSReceiptLineID={saved.POSReceiptLineID} | SourceDocumentLineID={saved.SourceDocumentLineID} | Amount={saved.POSReceiptLineAmount:N2} | POSPaymentTypeID={saved.POSPaymentTypeID}");
            }
        }

        var afterResult = await customerService.GetRedeemableCreditsAsync(
            transaction.AccountId,
            DateTime.Now,
            cancellationToken);

        if (!afterResult.Success || afterResult.Value is null)
        {
            Console.WriteLine(
                $"[Member Credit Single Verify] BALANCE WARNING | Account={expected.AccountId} | Unable to reload redeemable credits: {afterResult.ErrorMessage}");
            return;
        }

        var afterCredit = afterResult.Value.FirstOrDefault(credit =>
            string.Equals(
                credit.ARAPOutstandingID,
                expected.AccountId,
                StringComparison.OrdinalIgnoreCase));

        var afterBalance = afterCredit?.NetBalanceAfterUtilised ?? 0m;

        if (!expected.BeforeBalance.HasValue)
        {
            Console.WriteLine(
                $"[Member Credit Single Verify] BALANCE UNVERIFIED | Account={expected.AccountId} | After={afterBalance:N2} | Before balance was unavailable.");
            return;
        }

        var expectedAfter = Math.Max(0m, expected.BeforeBalance.Value - expected.Amount);
        var balancePassed = Math.Abs(afterBalance - expectedAfter) < 0.01m;

        Console.WriteLine(
            $"[Member Credit Single Verify] BALANCE {(balancePassed ? "PASS" : "FAIL")} | Account={expected.AccountId} | Before={expected.BeforeBalance.Value:N2} | Redeemed={expected.Amount:N2} | ExpectedAfter={expectedAfter:N2} | ActualAfter={afterBalance:N2}");
    }

    private async Task<IReadOnlyList<(string AccountId, decimal Amount, decimal? BeforeBalance)>?> CaptureMultiMemberCreditRedemptionAsync(
        Transaction transaction,
        CancellationToken cancellationToken)
    {
        if (transaction.DocumentTypeId != 52 || string.IsNullOrWhiteSpace(transaction.AccountId))
        {
            return null;
        }

        var allocations = transaction.Items
            .Where(item => item.ActivityTypeId == 6)
            .SelectMany(EffectiveMemberCreditAllocations)
            .Where(allocation =>
                !string.IsNullOrWhiteSpace(allocation.MemberCreditAccountId) &&
                allocation.Amount > 0m)
            .GroupBy(allocation => allocation.MemberCreditAccountId, StringComparer.OrdinalIgnoreCase)
            .Select(group => (
                AccountId: group.Key,
                Amount: group.Sum(allocation => allocation.Amount)))
            .ToList();

        // Step 27 is the multi-account FIFO checkpoint. Single-account verification
        // remains handled by Step 26.
        if (allocations.Count <= 1)
        {
            return null;
        }

        var beforeResult = await customerService.GetRedeemableCreditsAsync(
            transaction.AccountId,
            DateTime.Now,
            cancellationToken);

        var beforeByAccount = beforeResult.Success && beforeResult.Value is not null
            ? beforeResult.Value
                .Where(credit => !string.IsNullOrWhiteSpace(credit.ARAPOutstandingID))
                .GroupBy(
                    credit => credit.ARAPOutstandingID!,
                    StringComparer.OrdinalIgnoreCase)
                .ToDictionary(
                    group => group.Key,
                    group => group.First().NetBalanceAfterUtilised,
                    StringComparer.OrdinalIgnoreCase)
            : new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase);

        if (!beforeResult.Success || beforeResult.Value is null)
        {
            Console.WriteLine(
                $"[Member Credit FIFO Verify] BEFORE WARNING | Unable to load redeemable credits for Customer={transaction.AccountId}: {beforeResult.ErrorMessage}");
        }

        var captured = allocations
            .Select(allocation => (
                allocation.AccountId,
                allocation.Amount,
                BeforeBalance: beforeByAccount.TryGetValue(allocation.AccountId, out var balance)
                    ? (decimal?)balance
                    : null))
            .ToList();

        Console.WriteLine(
            $"[Member Credit FIFO Verify] BEFORE | Accounts={captured.Count} | RedeemTotal={captured.Sum(item => item.Amount):N2} | Order={string.Join(" -> ", captured.Select(item => item.AccountId))}");

        foreach (var allocation in captured)
        {
            Console.WriteLine(
                $"[Member Credit FIFO Verify] BEFORE ACCOUNT | Account={allocation.AccountId} | Redeem={allocation.Amount:N2} | Balance={(allocation.BeforeBalance.HasValue ? allocation.BeforeBalance.Value.ToString("N2") : "unknown")}");
        }

        return captured;
    }

    private async Task VerifyMultiMemberCreditRedemptionAsync(
        Transaction transaction,
        IReadOnlyList<(string AccountId, decimal Amount, decimal? BeforeBalance)>? verification,
        CancellationToken cancellationToken)
    {
        if (verification is null || verification.Count <= 1)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(transaction.DocumentId))
        {
            Console.WriteLine(
                $"[Member Credit FIFO Verify] FAIL | Created redemption has no DocumentID, so {verification.Count} FIFO receipt lines cannot be verified.");
            return;
        }

        var receiptResult = await cashSalesAC.LoadReceiptLinesAsync(
            transaction.DocumentId,
            cancellationToken);

        if (!receiptResult.Success || receiptResult.Value is null)
        {
            Console.WriteLine(
                $"[Member Credit FIFO Verify] RECEIPT FAIL | Document={transaction.DocumentId} | Unable to reload receipt lines: {receiptResult.ErrorMessage}");
        }
        else
        {
            var memberCreditReceipts = receiptResult.Value
                .Where(line => line.POSPaymentTypeID == -10)
                .ToList();

            var receiptAccountsPassed = true;

            foreach (var expected in verification)
            {
                var matching = memberCreditReceipts
                    .Where(line =>
                        string.Equals(
                            line.SourceDocumentLineID,
                            expected.AccountId,
                            StringComparison.OrdinalIgnoreCase))
                    .ToList();

                var accountPassed =
                    matching.Count == 1 &&
                    !string.IsNullOrWhiteSpace(matching[0].POSReceiptLineID) &&
                    Math.Abs(matching[0].POSReceiptLineAmount - expected.Amount) < 0.01m;

                receiptAccountsPassed &= accountPassed;

                Console.WriteLine(
                    $"[Member Credit FIFO Verify] RECEIPT ACCOUNT {(accountPassed ? "PASS" : "FAIL")} | Account={expected.AccountId} | ExpectedAmount={expected.Amount:N2} | SavedLines={matching.Count} | SavedAmount={(matching.Count == 1 ? matching[0].POSReceiptLineAmount.ToString("N2") : "n/a")} | POSReceiptLineID={(matching.Count == 1 ? matching[0].POSReceiptLineID : "n/a")}");
            }

            var expectedAccountIds = verification
                .Select(item => item.AccountId)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            var unexpectedReceipts = memberCreditReceipts
                .Where(line =>
                    string.IsNullOrWhiteSpace(line.SourceDocumentLineID) ||
                    !expectedAccountIds.Contains(line.SourceDocumentLineID))
                .ToList();

            var receiptPassed =
                receiptAccountsPassed &&
                memberCreditReceipts.Count == verification.Count &&
                unexpectedReceipts.Count == 0;

            Console.WriteLine(
                $"[Member Credit FIFO Verify] RECEIPT {(receiptPassed ? "PASS" : "FAIL")} | Document={transaction.DocumentId} | ExpectedAccounts={verification.Count} | SavedMinus10Lines={memberCreditReceipts.Count} | UnexpectedLines={unexpectedReceipts.Count}");
        }

        var afterResult = await customerService.GetRedeemableCreditsAsync(
            transaction.AccountId,
            DateTime.Now,
            cancellationToken);

        if (!afterResult.Success || afterResult.Value is null)
        {
            Console.WriteLine(
                $"[Member Credit FIFO Verify] BALANCE WARNING | Unable to reload redeemable credits for Customer={transaction.AccountId}: {afterResult.ErrorMessage}");
            return;
        }

        var afterByAccount = afterResult.Value
            .Where(credit => !string.IsNullOrWhiteSpace(credit.ARAPOutstandingID))
            .GroupBy(
                credit => credit.ARAPOutstandingID!,
                StringComparer.OrdinalIgnoreCase)
            .ToDictionary(
                group => group.Key,
                group => group.First().NetBalanceAfterUtilised,
                StringComparer.OrdinalIgnoreCase);

        var balancesVerified = true;
        var everyBeforeBalanceAvailable = true;

        foreach (var expected in verification)
        {
            var actualAfter = afterByAccount.TryGetValue(expected.AccountId, out var balance)
                ? balance
                : 0m;

            if (!expected.BeforeBalance.HasValue)
            {
                everyBeforeBalanceAvailable = false;
                balancesVerified = false;
                Console.WriteLine(
                    $"[Member Credit FIFO Verify] BALANCE UNVERIFIED | Account={expected.AccountId} | Redeemed={expected.Amount:N2} | ActualAfter={actualAfter:N2} | Before balance was unavailable.");
                continue;
            }

            var expectedAfter = Math.Max(0m, expected.BeforeBalance.Value - expected.Amount);
            var accountPassed = Math.Abs(actualAfter - expectedAfter) < 0.01m;
            balancesVerified &= accountPassed;

            Console.WriteLine(
                $"[Member Credit FIFO Verify] BALANCE ACCOUNT {(accountPassed ? "PASS" : "FAIL")} | Account={expected.AccountId} | Before={expected.BeforeBalance.Value:N2} | Redeemed={expected.Amount:N2} | ExpectedAfter={expectedAfter:N2} | ActualAfter={actualAfter:N2}");
        }

        Console.WriteLine(
            everyBeforeBalanceAvailable
                ? $"[Member Credit FIFO Verify] BALANCE {(balancesVerified ? "PASS" : "FAIL")} | Accounts={verification.Count} | RedeemTotal={verification.Sum(item => item.Amount):N2}"
                : $"[Member Credit FIFO Verify] BALANCE UNVERIFIED | Accounts={verification.Count} | One or more before balances were unavailable.");
    }

    private static JsonArray BuildReceiptLines(Transaction transaction)
    {
        var payments = transaction.Payments.Count > 0
            ? transaction.Payments
                .Where(payment => payment.PaymentTypeId != -10)
                .ToList()
            : transaction.PaymentTypeId != 0 && transaction.PaymentTypeId != -10
                ? new List<TransactionPayment>
                {
                    new()
                    {
                        PaymentTypeId = transaction.PaymentTypeId,
                        PaymentMethod = transaction.PaymentMethod,
                        Amount = transaction.Amount
                    }
                }
                : new List<TransactionPayment>();

        var lines = new JsonArray();
        var currency = string.IsNullOrWhiteSpace(transaction.CurrencyName)
            ? "MYR"
            : transaction.CurrencyName.Trim();
        var totalTendered = payments.Sum(payment => payment.Amount);
        var redeemedAmount = transaction.DocumentTypeId == 52
            ? transaction.Items
                .Where(item => item.ActivityTypeId == 6)
                .Sum(GetRedeemedCreditAmount)
            : 0m;
        var cashDue = Math.Max(0m, transaction.Amount - redeemedAmount);
        var changeDue = Math.Max(0m, totalTendered - cashDue);
        var changePayment = payments.LastOrDefault(payment =>
                                payment.PaymentMethod.Contains("cash", StringComparison.OrdinalIgnoreCase))
                            ?? payments.LastOrDefault();

        foreach (var payment in payments.Where(payment => payment.PaymentTypeId != 0 && payment.Amount > 0))
        {
            lines.Add(new JsonObject
            {
                ["POSReceiptLineID"] = (lines.Count + 1).ToString(),
                ["DocumentID"] = string.Empty,
                ["AccountID"] = transaction.AccountId,
                ["AccountTypeID"] = 3,
                ["Reference"] = transaction.ReferenceNumber,
                ["Description"] = payment.PaymentMethod,
                ["POSPaymentTypeID"] = payment.PaymentTypeId,
                ["POSReceiptLineAmount"] = payment.Amount,
                ["POSReceiptChangeAmount"] = ReferenceEquals(payment, changePayment) && changeDue > 0m ? -changeDue : 0m,
                ["FinancialAccountID"] = payment.FinancialAccountId,
                ["BankName"] = payment.BankName,
                ["BranchID"] = transaction.BranchId,
                ["GroupID"] = transaction.GroupId,
                ["FinancialDate"] = transaction.Date,
                ["ExchangeRate"] = 1m,
                ["CurrencyID"] = currency,
                ["CurrencyName"] = currency,
                ["SaveAction"] = 1,
                ["IsDirty"] = true
            });
        }

        if (transaction.DocumentTypeId == 52)
        {
            foreach (var item in transaction.Items.Where(item => item.ActivityTypeId == 6))
            {
                var allocations = EffectiveMemberCreditAllocations(item);
                foreach (var allocation in allocations)
                {
                    lines.Add(new JsonObject
                    {
                        ["POSReceiptLineID"] = (lines.Count + 1).ToString(),
                        ["DocumentID"] = transaction.DocumentId,
                        ["FinancialAccountID"] = string.Empty,
                        ["BankName"] = string.Empty,
                        ["POSReceiptLineAmount"] = allocation.Amount,
                        ["AccountID"] = transaction.AccountId,
                        ["AccountTypeID"] = 3,
                        ["Description"] = "Member Credit",
                        ["Reference"] = string.IsNullOrWhiteSpace(allocation.MemberTypeId)
                            ? allocation.MemberCreditAccountId
                            : $"{allocation.MemberTypeId} - ({allocation.MemberCreditAccountId})",
                        ["PackageID"] = string.Empty,
                        ["SourceDocumentLineID"] = allocation.MemberCreditAccountId,
                        ["QuantityRedeemed"] = 0m,
                        ["SourceUnitPrice"] = 0m,
                        ["SourceUnitActualValue"] = 0m,
                        ["InventoryID"] = item.InventoryId,
                        ["CurrencyID"] = currency,
                        ["CurrencyName"] = currency,
                        ["GroupID"] = transaction.GroupId,
                        ["ExchangeRate"] = 1m,
                        ["AmountInForeignCurrency"] = 0m,
                        ["POSPaymentTypeID"] = -10,
                        ["POSReceiptChangeAmount"] = 0m,
                        ["BranchID"] = transaction.BranchId,
                        ["FinancialDate"] = transaction.Date,
                        ["SaveAction"] = 1,
                        ["IsDirty"] = true
                    });

                    Console.WriteLine(
                        $"[Member Credit Receipt] Account={allocation.MemberCreditAccountId} | Amount={allocation.Amount:N2} | Inventory={item.InventoryId} | POSPaymentTypeID=-10");
                }
            }
        }

        return lines;
    }

    private static void ApplyReceiptLines(
        Transaction transaction,
        IEnumerable<CashSalesReceiptLineDTO> receiptLines,
        IReadOnlyDictionary<int, string> paymentTypeNames)
    {
        var savedReceiptLines = receiptLines
            .Where(line => line.POSPaymentTypeID != 0 &&
                           line.POSReceiptLineAmount > 0)
            .ToList();

        // Preserve every saved receipt row for the printed receipt, including the
        // system-controlled Member Credit rows (POSPaymentTypeID = -10).
        transaction.ReceiptPayments = savedReceiptLines
            .Select(receipt => new TransactionPayment
            {
                ReceiptLineId = receipt.POSReceiptLineID ?? string.Empty,
                PaymentTypeId = receipt.POSPaymentTypeID,
                PaymentMethod = receipt.POSPaymentTypeID == -10
                    ? "Member Credit"
                    : First(receipt.POSPaymentTypeName, receipt.Description,
                        paymentTypeNames.GetValueOrDefault(receipt.POSPaymentTypeID), "Payment")!,
                SourceDocumentLineId = receipt.SourceDocumentLineID ?? string.Empty,
                Amount = receipt.POSReceiptLineAmount,
                ChangeAmount = Math.Abs(receipt.POSReceiptChangeAmount)
            })
            .ToList();

        // Keep Member Credit outside the normal tender collection so normal
        // Cash/Card/Multi-Payment editing does not treat -10 as a selectable payment.
        transaction.Payments = transaction.ReceiptPayments
            .Where(payment => payment.PaymentTypeId != -10)
            .ToList();

        if (transaction.Payments.Count == 0)
            return;

        transaction.PaymentTypeId = transaction.Payments[0].PaymentTypeId;
        transaction.PaymentMethod = string.Join(" + ", transaction.Payments
            .Select(payment => payment.PaymentMethod)
            .Distinct(StringComparer.OrdinalIgnoreCase));
    }

    private static decimal GetRedeemedCreditAmount(TransactionItem item)
    {
        if (item.MemberCreditAllocations.Count > 0)
        {
            return item.MemberCreditAllocations
                .Where(allocation =>
                    !string.IsNullOrWhiteSpace(allocation.MemberCreditAccountId) &&
                    allocation.Amount > 0m)
                .Sum(allocation => allocation.Amount);
        }

        return string.IsNullOrWhiteSpace(item.MemberCreditAccountId)
            ? 0m
            : RedeemableLineSubTotal(item);
    }

    private static IReadOnlyList<MemberCreditAllocation> EffectiveMemberCreditAllocations(TransactionItem item)
    {
        var multiple = item.MemberCreditAllocations
            .Where(allocation =>
                !string.IsNullOrWhiteSpace(allocation.MemberCreditAccountId) &&
                allocation.Amount > 0m)
            .GroupBy(allocation => allocation.MemberCreditAccountId, StringComparer.OrdinalIgnoreCase)
            .Select(group => new MemberCreditAllocation
            {
                MemberCreditAccountId = group.Key,
                MemberTypeId = group.Select(x => x.MemberTypeId).FirstOrDefault(x => !string.IsNullOrWhiteSpace(x)) ?? string.Empty,
                Amount = group.Sum(x => x.Amount)
            })
            .ToList();

        if (multiple.Count > 0)
        {
            return multiple;
        }

        if (string.IsNullOrWhiteSpace(item.MemberCreditAccountId))
        {
            return Array.Empty<MemberCreditAllocation>();
        }

        return new[]
        {
            new MemberCreditAllocation
            {
                MemberCreditAccountId = item.MemberCreditAccountId,
                MemberTypeId = item.MemberTypeId,
                Amount = RedeemableLineSubTotal(item)
            }
        };
    }

    private static string SerializeMemberCreditAllocations(IEnumerable<MemberCreditAllocation> allocations) =>
        string.Join(
            "|",
            allocations
                .Where(allocation =>
                    !string.IsNullOrWhiteSpace(allocation.MemberCreditAccountId) &&
                    allocation.Amount > 0m)
                .Select(allocation =>
                    $"{allocation.MemberCreditAccountId.Trim()},{allocation.Amount.ToString("F2", System.Globalization.CultureInfo.InvariantCulture)}"));

    private static List<MemberCreditAllocation> ParseSavedMemberCreditAllocations(
        JsonObject? line,
        string? singleAccountId,
        string? fallbackMemberTypeId,
        decimal fallbackAmount)
    {
        var result = new List<MemberCreditAllocation>();

        if (line?["lstMembershipCredit"] is JsonArray nestedCredits)
        {
            foreach (var node in nestedCredits.OfType<JsonObject>())
            {
                var accountId = First(
                    Text(node, "MemberCreditAccountID"),
                    Text(node, "ARAPOutstandingID"),
                    Text(node, "SourceDocumentLineID"),
                    string.Empty) ?? string.Empty;
                var memberTypeId = First(
                    Text(node, "MemberTypeID"),
                    fallbackMemberTypeId,
                    string.Empty) ?? string.Empty;
                var amount = Number(node, "MemberCredit");
                if (amount <= 0m)
                {
                    amount = Number(node, "Amount");
                }
                if (amount <= 0m)
                {
                    amount = Number(node, "POSReceiptLineAmount");
                }

                if (string.IsNullOrWhiteSpace(accountId) || amount <= 0m)
                {
                    continue;
                }

                result.Add(new MemberCreditAllocation
                {
                    MemberCreditAccountId = accountId.Trim(),
                    MemberTypeId = memberTypeId,
                    Amount = amount
                });
            }
        }

        if (result.Count == 0)
        {
            result.AddRange(ParseMemberCreditAllocations(
                Text(line, "MembershipCredit"),
                fallbackMemberTypeId));
        }

        // Senang's single-credit structure stores the account directly on the
        // document line and may leave both multi-credit fields empty.
        if (result.Count == 0 &&
            !string.IsNullOrWhiteSpace(singleAccountId) &&
            fallbackAmount > 0m)
        {
            result.Add(new MemberCreditAllocation
            {
                MemberCreditAccountId = singleAccountId.Trim(),
                MemberTypeId = fallbackMemberTypeId ?? string.Empty,
                Amount = fallbackAmount
            });
        }

        return result
            .Where(allocation =>
                !string.IsNullOrWhiteSpace(allocation.MemberCreditAccountId) &&
                allocation.Amount > 0m)
            .GroupBy(
                allocation => allocation.MemberCreditAccountId.Trim(),
                StringComparer.OrdinalIgnoreCase)
            .Select(group => new MemberCreditAllocation
            {
                MemberCreditAccountId = group.Key,
                MemberTypeId = group
                    .Select(allocation => allocation.MemberTypeId)
                    .FirstOrDefault(value => !string.IsNullOrWhiteSpace(value))
                    ?? string.Empty,
                Amount = group.Sum(allocation => allocation.Amount)
            })
            .ToList();
    }

    private static List<MemberCreditAllocation> ParseMemberCreditAllocations(
        string? serialized,
        string? fallbackMemberTypeId)
    {
        var result = new List<MemberCreditAllocation>();
        if (string.IsNullOrWhiteSpace(serialized))
        {
            return result;
        }

        foreach (var row in serialized.Split('|', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var columns = row.Split(',', StringSplitOptions.TrimEntries);
            if (columns.Length < 2 ||
                string.IsNullOrWhiteSpace(columns[0]) ||
                !decimal.TryParse(
                    columns[1],
                    System.Globalization.NumberStyles.Number,
                    System.Globalization.CultureInfo.InvariantCulture,
                    out var amount) ||
                amount <= 0m)
            {
                continue;
            }

            result.Add(new MemberCreditAllocation
            {
                MemberCreditAccountId = columns[0],
                MemberTypeId = fallbackMemberTypeId ?? string.Empty,
                Amount = amount
            });
        }

        return result;
    }

    private static int InventoryTypeFor(string? category)
    {
        if (category?.Contains("Service", StringComparison.OrdinalIgnoreCase) == true) return 3;
        if (category?.Contains("Package", StringComparison.OrdinalIgnoreCase) == true) return 5;
        if (category?.Contains("Member Credit", StringComparison.OrdinalIgnoreCase) == true ||
            category?.Contains("TopUp", StringComparison.OrdinalIgnoreCase) == true ||
            category?.Contains("Top Up", StringComparison.OrdinalIgnoreCase) == true) return 7;
        return 1;
    }

    private static void ApplyHeader(
        JsonObject header,
        Transaction transaction,
        bool isNew,
        string? currencyId = null,
        string? gstTypeId = null)
    {
        var now = DateTime.Now;
        var documentTypeId = transaction.DocumentTypeId == 52 ? 52 : 5;
        header["DocumentTypeID"] = documentTypeId;
        header["FriendlyDocumentName"] = documentTypeId == 52 ? "Redemption" : "CashSales";
        header["BranchID"] = transaction.BranchId;
        header["EditBranchID"] = transaction.BranchId;
        header["GroupID"] = transaction.GroupId;
        header["FinancialDate"] = transaction.Date;
        header["AccountID"] = transaction.AccountId;
        header["AccountName"] = transaction.CustomerName;
        if (!string.IsNullOrWhiteSpace(gstTypeId))
        {
            header["TaxTypeID"] = gstTypeId;
        }
        header["ReferenceNumber"] = transaction.ReferenceNumber;
        header["TotalBeforeTax"] = transaction.Subtotal;
        header["TotalBeforeTax_NonServiceCharge"] = Math.Max(
            0m,
            transaction.Subtotal - transaction.ServiceChargeAmount);
        header["TotalBeforeTax_ServiceCharge"] = transaction.ServiceChargeAmount;
        header["TaxableAmount"] = transaction.Subtotal;
        header["TaxAmount"] = transaction.Tax;
        header["TourismTax"] = 0m;
        header["HeritageTax"] = 0m;
        header["RoundingAmount"] = transaction.RoundingAmount;
        header["TotalAfterTax"] = transaction.Amount;
        header["ExchangeRate"] = 1;
        header["LocalTotalBeforeTax"] = transaction.Subtotal;
        header["LocalTaxableAmount"] = transaction.Subtotal;
        header["LocalTaxAmount"] = transaction.Tax;
        header["LocalRoundingAmount"] = transaction.RoundingAmount;
        header["LocalTotalAfterTax"] = transaction.Amount;
        var resolvedCurrencyId = First(
            currencyId,
            Text(header, "TransactionCurrencyID"),
            Text(header, "LocalCurrencyID"),
            "MYR")!;
        header["TransactionCurrencyID"] = resolvedCurrencyId;
        header["LocalCurrencyID"] = resolvedCurrencyId;
        var currencyName = string.IsNullOrWhiteSpace(transaction.CurrencyName)
            ? "MYR"
            : transaction.CurrencyName;
        header["TransactionCurrencyName"] = currencyName;
        header["LocalCurrencyName"] = currencyName;
        header["Remarks"] = transaction.Notes;
        header["Phone"] = transaction.CustomerContact;
        header["CashierName"] = transaction.CreatedBy;
        header["ModifiedDateTime"] = now;
        header["UpdateTimeStamp"] = now;
        header["TablePaidTime"] = now;
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
    private static string TextIgnoreCase(JsonObject? source, string name)
    {
        if (source is null)
        {
            return string.Empty;
        }

        foreach (var item in source)
        {
            if (string.Equals(item.Key, name, StringComparison.OrdinalIgnoreCase) &&
                item.Value is JsonValue value &&
                value.TryGetValue<string>(out var text))
            {
                return text ?? string.Empty;
            }
        }

        return string.Empty;
    }

    private static string? FindTextIgnoreCase(JsonNode? node, params string[] names)
    {
        if (node is JsonObject obj)
        {
            foreach (var item in obj)
            {
                if (names.Any(name =>
                        string.Equals(item.Key, name, StringComparison.OrdinalIgnoreCase)) &&
                    item.Value is JsonValue value)
                {
                    if (value.TryGetValue<string>(out var text) &&
                        !string.IsNullOrWhiteSpace(text))
                    {
                        return text;
                    }

                    var rendered = item.Value?.ToJsonString().Trim('"');
                    if (!string.IsNullOrWhiteSpace(rendered) && rendered != "null")
                    {
                        return rendered;
                    }
                }

                var nested = FindTextIgnoreCase(item.Value, names);
                if (!string.IsNullOrWhiteSpace(nested))
                {
                    return nested;
                }
            }
        }
        else if (node is JsonArray array)
        {
            foreach (var item in array)
            {
                var nested = FindTextIgnoreCase(item, names);
                if (!string.IsNullOrWhiteSpace(nested))
                {
                    return nested;
                }
            }
        }

        return null;
    }

    private static DateTime? DateValueIgnoreCase(JsonObject? source, string name)
    {
        if (source is null)
        {
            return null;
        }

        foreach (var item in source)
        {
            if (!string.Equals(item.Key, name, StringComparison.OrdinalIgnoreCase) ||
                item.Value is not JsonValue value)
            {
                continue;
            }

            if (value.TryGetValue<DateTime>(out var date))
            {
                return date;
            }

            if (value.TryGetValue<string>(out var text) &&
                DateTime.TryParse(text, out date))
            {
                return date;
            }
        }

        return null;
    }

    private static DateTime? FindDateIgnoreCase(JsonNode? node, string name)
    {
        if (node is JsonObject obj)
        {
            var direct = DateValueIgnoreCase(obj, name);
            if (direct.HasValue)
            {
                return direct;
            }

            foreach (var child in obj)
            {
                var nested = FindDateIgnoreCase(child.Value, name);
                if (nested.HasValue)
                {
                    return nested;
                }
            }
        }
        else if (node is JsonArray array)
        {
            foreach (var child in array)
            {
                var nested = FindDateIgnoreCase(child, name);
                if (nested.HasValue)
                {
                    return nested;
                }
            }
        }

        return null;
    }

    private static decimal Number(JsonObject? source, string name) => source?[name] is JsonValue value && value.TryGetValue<decimal>(out var number) ? number : 0;
    private static bool SetDecimalIfDifferent(
        JsonObject target,
        string name,
        decimal value)
    {
        var rounded = Math.Round(value, 2, MidpointRounding.AwayFromZero);
        if (target[name] is JsonValue currentValue &&
            currentValue.TryGetValue<decimal>(out var current) &&
            Math.Abs(current - rounded) < 0.005m)
        {
            return false;
        }

        target[name] = rounded;
        return true;
    }

    private static int Integer(JsonObject? source, string name) => source?[name] is JsonValue value && value.TryGetValue<int>(out var number) ? number : 0;
    private static int IntegerIgnoreCase(JsonObject? source, string name)
    {
        if (source is null)
        {
            return 0;
        }

        foreach (var item in source)
        {
            if (!string.Equals(item.Key, name, StringComparison.OrdinalIgnoreCase) ||
                item.Value is not JsonValue value)
            {
                continue;
            }

            if (value.TryGetValue<int>(out var number))
            {
                return number;
            }

            if (value.TryGetValue<string>(out var text) &&
                int.TryParse(text, out number))
            {
                return number;
            }
        }

        return 0;
    }

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

    private static string? First(params string?[] values) => values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value));
}
