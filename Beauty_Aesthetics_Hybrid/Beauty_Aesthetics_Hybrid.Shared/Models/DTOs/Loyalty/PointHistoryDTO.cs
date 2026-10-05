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
    public DateTime FinancialDate { get; set; }

    // Exact signed point movement from
    // POST /api/WebDashboard/GetMemberOtherBalanceDetail with balanceType = "Point".
    public decimal MovementPoints { get; set; }

    // Exact running point balance returned by the same backend point ledger.
    public decimal BalanceAfter { get; set; }

    public decimal? EarnedPoints =>
        MovementPoints > 0m ? MovementPoints : null;

    public decimal RedeemedPoints =>
        MovementPoints < 0m ? Math.Abs(MovementPoints) : 0m;

    public decimal NetPoints => MovementPoints;
}
