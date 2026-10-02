using EBI.DM;
using System.Collections.ObjectModel;
using System.Text.Json.Serialization;

namespace Beauty_Aesthetics_WebPos.Models.DTOs;

public sealed class OutstandingSettlementLinesRequestDTO
{
    [JsonPropertyName("customerID")]
    public string CustomerID { get; set; } = string.Empty;

    [JsonPropertyName("documentID")]
    public string DocumentID { get; set; } = "all";

    [JsonPropertyName("branchGroupID")]
    public string BranchGroupID { get; set; } = string.Empty;
}

public sealed class OutstandingDocumentDTO
{
    // Keep the server's source identifiers. RetrieveSettlementLines is the
    // source of truth for later AR Receipt settlement.
    public string ARAPOutstandingID { get; set; } = string.Empty;
    public string SourceDocumentID { get; set; } = string.Empty;
    public string DocumentID { get; set; } = string.Empty;
    public string DisplayCode { get; set; } = string.Empty;
    public string DocumentNo { get; set; } = string.Empty;
    public string ReferenceNumber { get; set; } = string.Empty;
    public int DocumentTypeID { get; set; }
    public string DocumentTypeName { get; set; } = string.Empty;

    public DateTime FinancialDate { get; set; }
    public DateTime DueDate { get; set; }

    public decimal TotalAmount { get; set; }
    public decimal OriginalAmount { get; set; }
    public decimal PaidAmount { get; set; }
    public decimal Outstanding { get; set; }
    public decimal LocalOutstanding { get; set; }

    public string AccountID { get; set; } = string.Empty;
    public string AccountName { get; set; } = string.Empty;
    public string BranchID { get; set; } = string.Empty;
    public string GroupID { get; set; } = string.Empty;

    public string CurrencyID { get; set; } = string.Empty;
    public string CurrencyName { get; set; } = string.Empty;
    public decimal ExchangeRate { get; set; }

    // Fields used by Senang when the line is later allocated into an AR Receipt.
    // Step 2 only loads these; it does not mutate/save them.
    public decimal AllocatedAmount { get; set; }
    public decimal LocalAllocatedAmount { get; set; }
    public decimal ActualLocalSettlement { get; set; }
    public DateTime SettlementDate { get; set; }
    public decimal SettlementExchangeRate { get; set; }

    [JsonIgnore]
    public string SourceId =>
        !string.IsNullOrWhiteSpace(SourceDocumentID)
            ? SourceDocumentID
            : !string.IsNullOrWhiteSpace(DocumentID)
                ? DocumentID
                : ARAPOutstandingID;

    [JsonIgnore]
    public string DocumentNumber =>
        !string.IsNullOrWhiteSpace(DisplayCode)
            ? DisplayCode
            : !string.IsNullOrWhiteSpace(DocumentNo)
                ? DocumentNo
                : ReferenceNumber;

    [JsonIgnore]
    public decimal RemainingAmount =>
        Outstanding != 0m || LocalOutstanding == 0m
            ? Outstanding
            : LocalOutstanding;

    [JsonIgnore]
    public decimal EffectiveOriginalAmount =>
        OriginalAmount != 0m
            ? OriginalAmount
            : TotalAmount;

    [JsonIgnore]
    public decimal EffectivePaidAmount =>
        PaidAmount != 0m
            ? PaidAmount
            : Math.Max(0m, EffectiveOriginalAmount - RemainingAmount);
}


public sealed class OutstandingSettlementSaveRequestDTO
{
    public string CustomerID { get; set; } = string.Empty;
    public string CustomerName { get; set; } = string.Empty;
    public string BranchID { get; set; } = string.Empty;
    public string GroupID { get; set; } = string.Empty;

    // Optional branch currency fallback. The service prefers the live
    // settlement-line currency returned by RetrieveSettlementLines.
    public string CurrencyID { get; set; } = string.Empty;
    public string CurrencyName { get; set; } = string.Empty;

    public string FinancialAccountID { get; set; } = string.Empty;
    public string FinancialAccountName { get; set; } = string.Empty;

    // Step 8: use Beauty's existing multi-payment allocations. AR Receipt itself
    // has one financial account, so the service persists one AR Receipt per
    // configured payment method when more than one allocation is supplied.
    public List<OutstandingSettlementPaymentDTO> Payments { get; set; } = new();

    // Step 9 stale-data snapshot captured when the customer confirms
    // the Outstanding dialog. Key uses the same source/document identifier
    // as SelectedAmounts; value is the balance visible at selection time.
    public Dictionary<string, decimal> ExpectedOutstandingAmounts { get; set; } =
        new(StringComparer.OrdinalIgnoreCase);

    // Key = outstanding source/document identifier from Step 3.
    // Value = amount to settle against that source.
    public Dictionary<string, decimal> SelectedAmounts { get; set; } =
        new(StringComparer.OrdinalIgnoreCase);
}

public sealed class OutstandingSettlementPaymentDTO
{
    public int PaymentTypeID { get; set; }
    public string PaymentMethod { get; set; } = string.Empty;
    public string FinancialAccountID { get; set; } = string.Empty;
    public string FinancialAccountName { get; set; } = string.Empty;
    public decimal Amount { get; set; }
}

public sealed class OutstandingSettlementSaveResultDTO
{
    public string Id { get; set; } = string.Empty;
    public string DisplayCode { get; set; } = string.Empty;
    public decimal TotalAllocatedAmount { get; set; }
    public int SettledDocumentCount { get; set; }
    public List<string> ReceiptIds { get; set; } = new();
    public List<string> ReceiptCodes { get; set; } = new();

    // Step 11: post-save verification. A false value never means the AR Receipt
    // was rolled back; it means the receipt saved but the follow-up balance check
    // could not be confirmed exactly, so the UI must not encourage a blind retry.
    public bool VerificationCompleted { get; set; }
    public bool VerificationPassed { get; set; }
    public string VerificationMessage { get; set; } = string.Empty;
    public decimal? VerifiedCustomerOutstanding { get; set; }
    public Dictionary<string, decimal> VerifiedRemainingAmounts { get; set; } =
        new(StringComparer.OrdinalIgnoreCase);
}



public sealed class OutstandingPaymentHistoryDTO
{
    public string ReceiptID { get; set; } = string.Empty;
    public string ReceiptNo { get; set; } = string.Empty;
    public DateTime PaymentDate { get; set; }
    public string CustomerID { get; set; } = string.Empty;
    public string CustomerName { get; set; } = string.Empty;
    public string BranchID { get; set; } = string.Empty;
    public string GroupID { get; set; } = string.Empty;
    public decimal PaidAmount { get; set; }
    public string PaymentMethod { get; set; } = string.Empty;
    public string FinancialAccountID { get; set; } = string.Empty;
    public string FinancialAccountName { get; set; } = string.Empty;
    public bool VerificationPassed { get; set; }
    public string VerificationMessage { get; set; } = string.Empty;
    public decimal? CustomerOutstandingAfterPayment { get; set; }
    public List<OutstandingPaymentHistoryLineDTO> Documents { get; set; } = new();
}

public sealed class OutstandingPaymentHistoryLineDTO
{
    public string SourceDocumentID { get; set; } = string.Empty;
    public string DocumentNo { get; set; } = string.Empty;
    public decimal AmountPaid { get; set; }
    public decimal RemainingAfterPayment { get; set; }
}


/// <summary>
/// Beauty-owned wire contract for POST /api/Doc_ARReceipt/CreateRecord.
/// Senang's Doc_ARReceipt wrapper is app-side code and is not present in Beauty's DLLs,
/// so Beauty keeps the same JSON shape without taking a compile-time dependency on it.
/// </summary>
public sealed class OutstandingARReceiptCreateDTO
{
    [JsonPropertyName("objDoc_ARReceipt")]
    public OutstandingARReceiptHeaderDTO Header { get; set; } = new();

    [JsonPropertyName("lstud_ARAPPaymentOffSetLineDM")]
    public ObservableCollection<ud_ARAPPaymentOffSetLineDM> SettlementLines { get; set; } = new();
}

public sealed class OutstandingARReceiptHeaderDTO
{
    public string BranchID { get; set; } = string.Empty;
    public string EditBranchID { get; set; } = string.Empty;
    public string AccountID { get; set; } = string.Empty;
    public string AccountName { get; set; } = string.Empty;
    public string ReferenceNumber { get; set; } = string.Empty;
    public string CustomerCurrencyID { get; set; } = string.Empty;
    public string CustomerCurrencyName { get; set; } = string.Empty;
    public string LocalCurrencyID { get; set; } = string.Empty;
    public string LocalCurrencyName { get; set; } = string.Empty;
    public decimal ExchangeRate { get; set; } = 1m;
    public string Remarks { get; set; } = string.Empty;
    public string GroupID { get; set; } = string.Empty;
    public decimal BankExchangeRate { get; set; } = 1m;
    public string BankAccountID { get; set; } = string.Empty;
    public string BankAccountName { get; set; } = string.Empty;
    public string BankCurrencyID { get; set; } = string.Empty;
    public string BankCurrencyName { get; set; } = string.Empty;
    public decimal BankAmountReceived { get; set; }
    public decimal BankCharges { get; set; }
    public decimal TotalAllocatedAmount { get; set; }
    public int SaveAction { get; set; } = 1;
    public string FinancialAccountID { get; set; } = string.Empty;
    public bool IsDirty { get; set; } = true;
    public bool blnIsPeriodClosed { get; set; } = true;
    public bool blnIsBankReconciliationDone { get; set; } = true;
}
