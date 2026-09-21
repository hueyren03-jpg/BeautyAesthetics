using System.Text.Json;
using System.Text.Json.Serialization;

namespace Beauty_Aesthetics_WebPos.Models.DTOs;

public sealed class DashboardDateRangeRequest
{
    [JsonPropertyName("startDate")]
    public DateTime StartDate { get; set; }

    [JsonPropertyName("endDate")]
    public DateTime EndDate { get; set; }

    [JsonPropertyName("inventoryTypeID")]
    public int InventoryTypeID { get; set; }

    [JsonPropertyName("branchID")]
    public string BranchID { get; set; } = string.Empty;
}

public sealed class DashboardIdRequest
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;
}

public sealed class CustomerLastVisitRequest
{
    [JsonPropertyName("daysRangeFrom")]
    public int DaysRangeFrom { get; set; }

    [JsonPropertyName("daysRangeTo")]
    public int DaysRangeTo { get; set; }

    [JsonPropertyName("pageNumber")]
    public int PageNumber { get; set; } = 1;

    [JsonPropertyName("pageSize")]
    public int PageSize { get; set; } = 20;
}

public sealed class MemberOtherBalanceDetailRequest
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("startDate")]
    public DateTime StartDate { get; set; }

    [JsonPropertyName("balanceType")]
    public string BalanceType { get; set; } = string.Empty;
}

public sealed class BranchPerformanceSummaryDTO
{
    public string? BranchID { get; set; }
    public string? Branch { get; set; }
    public decimal Sales { get; set; }
    public int TransCount { get; set; }
    public decimal Quantity { get; set; }
}

public sealed class MemberStatisticSummaryDTO
{
    public int TotalCustomers { get; set; }
    public int Today { get; set; }
    public int ThisMonth { get; set; }
    public int LastMonth { get; set; }
}

public sealed class DashboardRawResultDTO
{
    public JsonElement Value { get; set; }
}

public sealed class SalesByTypeDTO
{
    public decimal TotalBeforeTax_NonServiceCharge { get; set; }
    public decimal TotalBeforeTax_ServiceCharge { get; set; }
    public decimal TotalBeforeTax { get; set; }
    public decimal TaxAmount { get; set; }
    public decimal TourismTax { get; set; }
    public decimal HeritageTax { get; set; }
    public decimal RoundingAmount { get; set; }
    public decimal TotalAfterTax { get; set; }
    public decimal Product { get; set; }
    public decimal Service { get; set; }
    public decimal Voucher { get; set; }
    public decimal Package { get; set; }
    public decimal TopUp { get; set; }
    public decimal Bundle { get; set; }
    public decimal ServiceCharge { get; set; }
    public decimal Deposit { get; set; }
}

public sealed class SalesByCollectionDTO
{
    public string? ReportGroup { get; set; }
    public DateTime FinancialDate { get; set; }
    public int Sorting { get; set; }
    public decimal Amount { get; set; }
}
