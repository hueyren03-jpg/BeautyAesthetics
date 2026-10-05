namespace Beauty_Aesthetics_WebPos.Models.DTOs;

public sealed class PointHistorySnapshotDTO
{
    public string CustomerId { get; set; } = string.Empty;
    public string BranchId { get; set; } = string.Empty;
    public DateTime FromDate { get; set; }
    public DateTime ToDate { get; set; }
    public decimal CurrentPointBalance { get; set; }
    public List<PointHistoryEntryDTO> Entries { get; set; } = new();
}

public sealed class PointHistoryEntryDTO
{
    public string DocumentId { get; set; } = string.Empty;
    public string InvoiceNumber { get; set; } = string.Empty;
    public string BranchId { get; set; } = string.Empty;
    public DateTime FinancialDate { get; set; }
    public decimal SaleAmount { get; set; }

    // Exact backend-persisted redemption from Cash Sale DocumentLine.Points.
    public decimal RedeemedPoints { get; set; }

    // The backend does not expose a dedicated historical point-movement endpoint.
    // EarnedPoints is populated only when the dated Point Setup rule can be
    // deterministically matched for the customer's member type.
    public decimal? EarnedPoints { get; set; }
    public bool EarnedPointsCalculated { get; set; }
    public string PointRuleId { get; set; } = string.Empty;

    // No historical balance-after field is exposed by the available backend APIs.
    public decimal? BalanceAfter { get; set; }

    public decimal? NetPoints =>
        EarnedPoints.HasValue
            ? EarnedPoints.Value - RedeemedPoints
            : RedeemedPoints > 0m
                ? -RedeemedPoints
                : null;
}
