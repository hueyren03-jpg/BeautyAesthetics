using System.Text.Json;
using System.Text.Json.Serialization;

namespace Beauty_Aesthetics_WebPos.Models.DTOs;

public sealed class CashSalesLoadRequestDTO
{
    [JsonPropertyName("strBranches")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("startDate")]
    public DateTime StartDate { get; set; }

    [JsonPropertyName("endDate")]
    public DateTime EndDate { get; set; }
}

public sealed class CashSalesHistoryRequestDTO
{
    [JsonPropertyName("strBranches")]
    public string Branches { get; set; } = string.Empty;

    [JsonPropertyName("startDate")]
    public DateTime StartDate { get; set; }

    [JsonPropertyName("endDate")]
    public DateTime EndDate { get; set; }
}

public sealed class CashSalesLookupDTO
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;
}

public sealed class CreateCashSalesResponseDTO
{
    [JsonPropertyName("Id")]
    public string? Id { get; set; }

    [JsonPropertyName("DisplayCode")]
    public string? DisplayCode { get; set; }
}

public sealed class CashSalesThermalReceiptRequestDTO
{
    [JsonPropertyName("Id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("IntDocumentTypeID")]
    public int DocumentTypeId { get; set; }
}

public sealed class CashSalesBillLinkRequestDTO
{
    [JsonPropertyName("DocumentTypeID")]
    public int DocumentTypeId { get; set; }

    [JsonPropertyName("DocumentID")]
    public string DocumentId { get; set; } = string.Empty;

    [JsonPropertyName("FinancialDate")]
    public DateTime FinancialDate { get; set; }
}

public sealed class CashSalesDeleteDTO
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("documentID")]
    public string DocumentId { get; set; } = string.Empty;

    [JsonPropertyName("branchID")]
    public string BranchId { get; set; } = string.Empty;

    [JsonPropertyName("financialDate")]
    public DateTime FinancialDate { get; set; }

    [JsonPropertyName("reason")]
    public string Reason { get; set; } = string.Empty;
}

public sealed class CashSalesProxyDTO
{
    public string? DocumentID { get; set; }
    public string? DisplayCode { get; set; }
    public DateTime FinancialDate { get; set; }
    public string? BranchID { get; set; }
    public string? AccountID { get; set; }
    public string? AccountName { get; set; }
    public string? ReferenceNumber { get; set; }
    public decimal TotalBeforeTax { get; set; }
    public decimal TaxAmount { get; set; }
    public decimal RoundingAmount { get; set; }
    public decimal TotalAfterTax { get; set; }
    public string? SalesPersonName { get; set; }
    public string? CashierName { get; set; }
    public string? Remarks { get; set; }
    public bool IsVoid { get; set; }
    public int ItemCount { get; set; }
    public string? PaymentMethod { get; set; }
    public string? PaymentStatus { get; set; }

    [JsonExtensionData]
    public Dictionary<string, JsonElement>? AdditionalData { get; set; }
}

public sealed class CashSalesPaymentTypeRequestDTO
{
    [JsonPropertyName("ID")]
    public string? CustomerId { get; set; }

    [JsonPropertyName("BranchID")]
    public string? BranchId { get; set; }

    [JsonPropertyName("GroupID")]
    public string? GroupId { get; set; }
}

public sealed class CashSalesPaymentTypeDTO
{
    public int POSPaymentTypeID { get; set; }
    public string? POSPaymentTypeName { get; set; }
    public string? Grouping { get; set; }
    public int Sorting { get; set; }
    public string? ReceiptGroup { get; set; }
    public string? FinancialAccountID { get; set; }
    public string? VisibleInModules { get; set; }
    public string? VisibleInBranch { get; set; }
    public bool Active { get; set; }
    public bool IsOpenCashDrawer { get; set; }
    public bool IsReferenceNoCompulsory { get; set; }
}

public sealed class CashSalesReceiptLineDTO
{
    public string? POSReceiptLineID { get; set; }
    public string? DocumentID { get; set; }
    public string? FinancialAccountID { get; set; }
    public string? BankName { get; set; }
    public decimal POSReceiptLineAmount { get; set; }
    public string? AccountID { get; set; }
    public int AccountTypeID { get; set; }
    public string? Description { get; set; }
    public string? Reference { get; set; }
    public string? PackageID { get; set; }
    public string? SourceDocumentLineID { get; set; }
    public decimal QuantityRedeemed { get; set; }
    public decimal SourceUnitPrice { get; set; }
    public decimal SourceUnitActualValue { get; set; }
    public string? InventoryID { get; set; }
    public string? CurrencyID { get; set; }
    public string? CurrencyName { get; set; }
    public string? GroupID { get; set; }
    public decimal ExchangeRate { get; set; }
    public decimal AmountInForeignCurrency { get; set; }
    public int POSPaymentTypeID { get; set; }
    public string? POSPaymentTypeName { get; set; }
    public decimal POSReceiptChangeAmount { get; set; }
    public string? BranchID { get; set; }
    public DateTime FinancialDate { get; set; }
    public decimal PointDeduction { get; set; }
    public int SaveAction { get; set; } = 1;
    public bool IsDirty { get; set; } = true;
}
