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
    private readonly IJSRuntime jsRuntime;

    public CashSalesService(
        CashSalesAC cashSalesAC,
        BranchAC branchAC,
        WebDashboardAC dashboardAC,
        AppFeedbackService feedback,
        IFileDownloadService fileDownloadService,
        IMemberCreditService memberCreditService,
        ICustomerService customerService,
        IJSRuntime jsRuntime)
    {
        this.cashSalesAC = cashSalesAC;
        this.branchAC = branchAC;
        this.dashboardAC = dashboardAC;
        this.feedback = feedback;
        this.fileDownloadService = fileDownloadService;
        this.memberCreditService = memberCreditService;
        this.customerService = customerService;
        this.jsRuntime = jsRuntime;
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

        // Match Senang Retail: normal receipts default to type 5 and become
        // redemption statements only when a saved line is owned by type 52.
        if (resolvedDocumentTypeId != 52)
        {
            var loadResult = await cashSalesAC.LoadRecordAsync(documentId, cancellationToken);
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

        var receiptResult = await cashSalesAC.GetThermalReceiptPdfAsync(
            documentId,
            resolvedDocumentTypeId,
            cancellationToken);

        if (!receiptResult.Success || string.IsNullOrWhiteSpace(receiptResult.Value))
        {
            return ApiCallResult<string>.Failure(
                receiptResult.StatusCode,
                receiptResult.ErrorMessage ?? "The receipt PDF was empty.");
        }

        return ApiCallResult<string>.Ok(receiptResult.StatusCode, receiptResult.Value);
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
        ApplyHeader(header, transaction, true);
        document["objDoc_CashSales"] = header;
        var documentLines = BuildDocumentLines(document, transaction);
        document["lstDocumentLine"] = documentLines;
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

        transaction.Payments = receipts.OfType<JsonObject>()
            .Select(receipt => new TransactionPayment
            {
                ReceiptLineId = Text(receipt, "POSReceiptLineID"),
                PaymentTypeId = Integer(receipt, "POSPaymentTypeID"),
                PaymentMethod = First(Text(receipt, "POSPaymentTypeName"), Text(receipt, "Description"), "Payment")!,
                Amount = Number(receipt, "POSReceiptLineAmount"),
                ChangeAmount = Math.Abs(Number(receipt, "POSReceiptChangeAmount"))
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
            ActivityTypeId = Integer(line, "ActivityTypeID") == 0 ? 1 : Integer(line, "ActivityTypeID"),
            MemberCreditAccountId = Text(line, "MemberCreditAccountID"),
            MemberTypeId = Text(line, "MemberTypeID"),
            MembershipCredit = Text(line, "MembershipCredit"),
            MemberCreditAllocations = ParseMemberCreditAllocations(
                Text(line, "MembershipCredit"),
                Text(line, "MemberTypeID"))
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
            OrderLineTaxCalculator.ComputeLineAmounts(
                item.UnitPrice,
                quantity,
                discount,
                item.TaxPercentage,
                item.IsTaxInclusive,
                out var beforeTax,
                out var lineTax);
            var lineTotal = Math.Round(beforeTax + lineTax, 2, MidpointRounding.AwayFromZero);
            var line = template is null ? new JsonObject() : (JsonObject)template.DeepClone();
            var currentLineOrder = lineOrder++;
            line["DocumentLineID"] = currentLineOrder.ToString("D2");
            line["DocumentID"] = string.Empty;
            line["InventoryID"] = item.InventoryId;
            line["LineItemID"] = item.InventoryId;
            line["LineItemDisplayCode"] = item.Sku;
            line["LineOrder"] = currentLineOrder;
            line["Description"] = First(item.Description, item.Name, $"{transaction.Type} Item");
            line["ItemName"] = First(item.Name, item.Description, $"{transaction.Type} Item");
            line["Quantity"] = quantity;
            line["UnitPrice"] = item.UnitPrice;
            line["DiscountAmount"] = discount;
            line["CashDiscountID"] = item.CashDiscountId;
            line["Memo"] = item.DiscountMemo;
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
            line["UnitOfMeasureID"] = item.UnitOfMeasureId;
            line["TaxCodeID"] = item.TaxCodeId;
            line["IsTaxInclusive"] = item.IsTaxInclusive;
            line["ActivityTypeID"] = item.ActivityTypeId <= 0 ? 1 : item.ActivityTypeId;
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
            line["CurrencyID"] = string.IsNullOrWhiteSpace(transaction.CurrencyName) ? "MYR" : transaction.CurrencyName;
            line["ExchangeRate"] = 1m;
            line["FinancialDate"] = transaction.Date;
            line["SaveAction"] = 1;
            line["IsDirty"] = true;
            lines.Add(line);
        }

        return lines;
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
            var lineAmount = Math.Round(
                Math.Max(0m, item.TotalPrice),
                2,
                MidpointRounding.AwayFromZero);

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

        var latestResult = await customerService.GetRedeemableCreditsAsync(
            transaction.AccountId,
            DateTime.Now,
            cancellationToken);

        if (!latestResult.Success || latestResult.Value is null)
        {
            return (
                false,
                HttpStatusCode.ServiceUnavailable,
                "Member Credit balances could not be refreshed before saving. " +
                "No redemption was created. Refresh the customer's credits and try again.");
        }

        var latestByAccount = latestResult.Value
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
                ["POSReceiptLineID"] = string.Empty,
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
                        ["POSReceiptLineID"] = string.Empty,
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
            : Math.Max(0m, item.TotalPrice);
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
                Amount = Math.Max(0m, item.TotalPrice)
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

    private static void ApplyHeader(JsonObject header, Transaction transaction, bool isNew)
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
        header["ReferenceNumber"] = transaction.ReferenceNumber;
        header["TotalBeforeTax"] = transaction.Subtotal;
        header["TaxableAmount"] = transaction.Subtotal;
        header["TaxAmount"] = transaction.Tax;
        header["RoundingAmount"] = transaction.RoundingAmount;
        header["TotalAfterTax"] = transaction.Amount;
        header["ExchangeRate"] = 1;
        header["LocalTotalBeforeTax"] = transaction.Subtotal;
        header["LocalTaxableAmount"] = transaction.Subtotal;
        header["LocalTaxAmount"] = transaction.Tax;
        header["LocalRoundingAmount"] = transaction.RoundingAmount;
        header["LocalTotalAfterTax"] = transaction.Amount;
        header["TransactionCurrencyID"] = "MYR";
        header["LocalCurrencyID"] = "MYR";
        header["TransactionCurrencyName"] = "MYR";
        header["LocalCurrencyName"] = "MYR";
        header["Remarks"] = transaction.Notes;
        header["Phone"] = transaction.CustomerContact;
        header["CashierName"] = transaction.CreatedBy;
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
