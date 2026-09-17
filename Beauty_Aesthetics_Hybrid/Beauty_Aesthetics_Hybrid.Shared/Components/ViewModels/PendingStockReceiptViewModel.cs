namespace Beauty_Aesthetics_WebPos.Components.ViewModels;

public sealed class PendingStockReceiptViewModel
{
    public string DocumentId { get; init; } = string.Empty;
    public string DisplayCode { get; init; } = string.Empty;
    public DateTime FinancialDate { get; init; }
    public string SourceName { get; init; } = "-";
    public string DestinationBranchId { get; set; } = string.Empty;
    public string Remarks { get; init; } = string.Empty;
    public decimal TotalQuantity { get; init; }
    public decimal TotalCost { get; init; }
    public IReadOnlyList<PendingStockReceiptLineViewModel> Lines { get; set; } = [];
    public bool DetailsLoaded { get; set; }
    public bool IsExpanded { get; set; }
}

public sealed class PendingStockReceiptLineViewModel
{
    public string LineId { get; init; } = string.Empty;
    public string InventoryMovementId { get; init; } = string.Empty;
    public string ItemId { get; init; } = string.Empty;
    public string ItemCode { get; init; } = "-";
    public string ItemName { get; init; } = "-";
    public decimal Quantity { get; init; }
    public string BatchNo { get; init; } = string.Empty;
    public decimal TotalCost { get; init; }
}
