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