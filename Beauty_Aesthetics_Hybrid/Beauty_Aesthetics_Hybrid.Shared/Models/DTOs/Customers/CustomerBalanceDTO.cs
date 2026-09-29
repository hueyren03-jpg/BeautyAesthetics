using System.Text.Json.Serialization;

namespace Beauty_Aesthetics_WebPos.Models.DTOs;

public sealed class MemberBalanceSummaryDTO
{
    public decimal PackageBalance { get; set; }
    public decimal CreditBalance { get; set; }
    public decimal PointBalance { get; set; }
    public decimal PointRebateBalance { get; set; }
    public decimal VoucherBalance { get; set; }
}

public sealed class PackageBalanceDetailDTO
{
    public string? AutoID { get; set; }
    public string? PackageName { get; set; }
    public string? PackageCode { get; set; }
    public string? Description { get; set; }
    public decimal NetBalanceAfterUtilised { get; set; }
    public decimal BalancePVValue { get; set; }
    public DateTime ExpiryDate { get; set; }
    public bool IsRedeemable { get; set; }
}

public sealed class CreditBalanceDetailDTO
{
    public string? ARAPOutstandingID { get; set; }
    public string? DisplayCode { get; set; }
    public string? ItemDescription { get; set; }
    public string? MemberTypeID { get; set; }
    public decimal NetBalanceAfterUtilised { get; set; }
    public decimal BalanceCredit { get; set; }
    public DateTime DueDate { get; set; }
    public bool IsRedeemable { get; set; }
}

public sealed class RedeemableCreditRequestDTO
{
    [JsonPropertyName("customerID")]
    public string CustomerId { get; set; } = string.Empty;

    [JsonPropertyName("purchaseCutOffDate")]
    public DateTime PurchaseCutOffDate { get; set; }
}

public sealed class RedeemableCreditDTO
{
    public string? ARAPOutstandingID { get; set; }
    public string? AccountID { get; set; }
    public DateTime FinancialDate { get; set; }
    public DateTime DueDate { get; set; }
    public string? DocumentID { get; set; }
    public string? DisplayCode { get; set; }
    public int DocumentTypeID { get; set; }
    public string? DocumentTypeName { get; set; }
    public string? DocumentLineID { get; set; }
    public string? ItemDescription { get; set; }
    public string? CurrencyID { get; set; }
    public string? CurrencyName { get; set; }
    public decimal ExchangeRate { get; set; }
    public decimal InterOutletRatio { get; set; }
    public decimal InterOutletAmount { get; set; }
    public decimal TotalAmount { get; set; }
    public decimal BalanceCredit { get; set; }
    public decimal AmountUtilised { get; set; }
    public decimal NetBalanceAfterUtilised { get; set; }
    public string? BranchID { get; set; }
    public string? GroupID { get; set; }
    public string? LineItemID { get; set; }
    public string? MemberTypeID { get; set; }
    public bool IsRedeemable { get; set; }
}

public sealed class CreditRedemptionHistoryDTO
{
    public string ARAPOutstandingID { get; set; } = string.Empty;
    public DateTime FinancialDate { get; set; }
    public string DisplayCode { get; set; } = string.Empty;
    public string SourceARAPOutstandingID { get; set; } = string.Empty;
    public decimal TotalAmount { get; set; }
    public string BranchID { get; set; } = string.Empty;
}

public sealed class MemberOtherBalanceSummaryRequestDTO
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("cutOffDate")]
    public DateTime CutOffDate { get; set; }
}

public sealed class MemberOtherBalanceSummaryDTO
{
    public string CustomerID { get; set; } = string.Empty;
    public string CustomerName { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
    public decimal Outstanding { get; set; }
    public decimal Point { get; set; }
    public decimal PointRebate { get; set; }
    public decimal MGM { get; set; }
    public decimal Deposit { get; set; }
}
