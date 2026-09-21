using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

namespace Beauty_Aesthetics_WebPos.Models.DTOs;

public sealed class StockGrnLookupDTO
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;
}

public sealed class StockGrnProxyRequestDTO
{
    [JsonPropertyName("branchID")]
    public string BranchID { get; set; } = string.Empty;

    [JsonPropertyName("startDate")]
    public DateTime StartDate { get; set; }

    [JsonPropertyName("endDate")]
    public DateTime EndDate { get; set; }

    [JsonPropertyName("pageNumber")]
    public int PageNumber { get; set; } = 1;

    [JsonPropertyName("pageSize")]
    public int PageSize { get; set; } = 100;
}

public sealed class StockGrnEnvelopeDTO
{
    [JsonPropertyName("mobjDoc_Stock_GRN")]
    public StockGrnDocumentDTO Document { get; set; } = new();

    [JsonPropertyName("lstDocumentLine")]
    public List<JsonObject> DocumentLines { get; set; } = new();
}

public sealed class StockGrnDocumentDTO
{
    [JsonPropertyName("verifyStatus")]
    public string? VerifyStatus { get; set; }

    [JsonPropertyName("isLoading")]
    public bool IsLoading { get; set; }

    [JsonPropertyName("documentID")]
    public string? DocumentID { get; set; }

    [JsonPropertyName("documentTypeID")]
    public int DocumentTypeID { get; set; } = 51;

    [JsonPropertyName("friendlyDocumentName")]
    public string? FriendlyDocumentName { get; set; } = "GRN";

    [JsonPropertyName("alphaCode")]
    public string? AlphaCode { get; set; }

    [JsonPropertyName("numericCode")]
    public int NumericCode { get; set; }

    [JsonPropertyName("branchID")]
    public string? BranchID { get; set; }

    [JsonPropertyName("editBranchID")]
    public string? EditBranchID { get; set; }

    [JsonPropertyName("displayCode")]
    public string? DisplayCode { get; set; }

    [JsonPropertyName("financialDate")]
    public DateTime FinancialDate { get; set; } = DateTime.Today;

    [JsonPropertyName("accountID")]
    public string? AccountID { get; set; }

    [JsonPropertyName("accountName")]
    public string? AccountName { get; set; }

    [JsonPropertyName("referenceNumber")]
    public string? ReferenceNumber { get; set; }

    [JsonPropertyName("jobAccountID")]
    public string? JobAccountID { get; set; }

    [JsonPropertyName("jobName")]
    public string? JobName { get; set; }

    [JsonPropertyName("totalBeforeTax")]
    public decimal TotalBeforeTax { get; set; }

    [JsonPropertyName("taxableAmount")]
    public decimal TaxableAmount { get; set; }

    [JsonPropertyName("taxAmount")]
    public decimal TaxAmount { get; set; }

    [JsonPropertyName("roundingAmount")]
    public decimal RoundingAmount { get; set; }

    [JsonPropertyName("totalAfterTax")]
    public decimal TotalAfterTax { get; set; }

    [JsonPropertyName("transactionCurrencyID")]
    public string? TransactionCurrencyID { get; set; }

    [JsonPropertyName("localCurrencyID")]
    public string? LocalCurrencyID { get; set; }

    [JsonPropertyName("exchangeRate")]
    public decimal ExchangeRate { get; set; } = 1;

    [JsonPropertyName("localTotalBeforeTax")]
    public decimal LocalTotalBeforeTax { get; set; }

    [JsonPropertyName("localTaxableAmount")]
    public decimal LocalTaxableAmount { get; set; }

    [JsonPropertyName("localTaxAmount")]
    public decimal LocalTaxAmount { get; set; }

    [JsonPropertyName("localRoundingAmount")]
    public decimal LocalRoundingAmount { get; set; }

    [JsonPropertyName("localTotalAfterTax")]
    public decimal LocalTotalAfterTax { get; set; }

    [JsonPropertyName("createdBy")]
    public string? CreatedBy { get; set; }

    [JsonPropertyName("createdDateTime")]
    public DateTime CreatedDateTime { get; set; }

    [JsonPropertyName("modifiedBy")]
    public string? ModifiedBy { get; set; }

    [JsonPropertyName("modifiedDateTime")]
    public DateTime ModifiedDateTime { get; set; }

    [JsonPropertyName("updateTimeStamp")]
    public DateTime UpdateTimeStamp { get; set; }

    [JsonPropertyName("createdByDocumentTypeID")]
    public int CreatedByDocumentTypeID { get; set; }

    [JsonPropertyName("createdByDocumentTypeName")]
    public string? CreatedByDocumentTypeName { get; set; }

    [JsonPropertyName("createdByDocumentID")]
    public string? CreatedByDocumentID { get; set; }

    [JsonPropertyName("createdByDocumentDisplayCode")]
    public string? CreatedByDocumentDisplayCode { get; set; }

    [JsonPropertyName("isLocked")]
    public bool IsLocked { get; set; }

    [JsonPropertyName("isVoid")]
    public bool IsVoid { get; set; }

    [JsonPropertyName("paymentTermID")]
    public string? PaymentTermID { get; set; }

    [JsonPropertyName("paymentTermName")]
    public string? PaymentTermName { get; set; }

    [JsonPropertyName("taxTypeID")]
    public string? TaxTypeID { get; set; }

    [JsonPropertyName("orderBranchID")]
    public string? OrderBranchID { get; set; }

    [JsonPropertyName("poDocumentID")]
    public string? PODocumentID { get; set; }

    [JsonPropertyName("poDisplayCode")]
    public string? PODisplayCode { get; set; }

    [JsonPropertyName("salesOrderID")]
    public string? SalesOrderID { get; set; }

    [JsonPropertyName("soDisplayCode")]
    public string? SODisplayCode { get; set; }

    [JsonPropertyName("deliveryOrderID")]
    public string? DeliveryOrderID { get; set; }

    [JsonPropertyName("deliveryOrderDisplayCode")]
    public string? DeliveryOrderDisplayCode { get; set; }

    [JsonPropertyName("remarks")]
    public string? Remarks { get; set; }

    [JsonPropertyName("transactionCurrencyName")]
    public string? TransactionCurrencyName { get; set; }

    [JsonPropertyName("localCurrencyName")]
    public string? LocalCurrencyName { get; set; }

    [JsonPropertyName("groupID")]
    public string? GroupID { get; set; }

    [JsonPropertyName("financialAccountID")]
    public string? FinancialAccountID { get; set; }

    [JsonPropertyName("postingDate")]
    public DateTime PostingDate { get; set; }

    [JsonPropertyName("isPostingDateDifferent")]
    public bool IsPostingDateDifferent { get; set; }

    [JsonPropertyName("stockActivityType")]
    public string? StockActivityType { get; set; }

    [JsonPropertyName("poFinancialDate")]
    public DateTime POFinancialDate { get; set; }

    [JsonPropertyName("saveAction")]
    public object? SaveAction { get; set; }

    [JsonPropertyName("isDirty")]
    public bool IsDirty { get; set; }

    [JsonExtensionData]
    public Dictionary<string, JsonElement>? ExtensionData { get; set; }
}
