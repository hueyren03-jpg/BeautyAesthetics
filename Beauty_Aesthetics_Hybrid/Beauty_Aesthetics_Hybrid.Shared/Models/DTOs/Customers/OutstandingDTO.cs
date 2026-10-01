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
