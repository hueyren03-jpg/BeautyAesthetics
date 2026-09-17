namespace Beauty_Aesthetics_WebPos.Components.ViewModels;

public sealed class StockTransferViewModel
{
    public string DocumentId { get; set; } = string.Empty;
    public int DocumentTypeId { get; set; }
    public string DisplayCode { get; set; } = string.Empty;
    public DateTime Date { get; set; } = DateTime.Today;
    public string BranchId { get; set; } = "HQ";
    public string FromBranchId { get; set; } = string.Empty;
    public string FromBranchName { get; set; } = string.Empty;
    public string ToBranchId { get; set; } = string.Empty;
    public string ToBranchName { get; set; } = string.Empty;
    public string Remarks { get; set; } = string.Empty;
    public string Status { get; set; } = "Pending";
    public List<StockTransferLineViewModel> Lines { get; set; } = new();
}

public sealed class StockTransferLineViewModel
{
    public string DocumentLineId { get; set; } = string.Empty;
    public string InventoryMovementId { get; set; } = string.Empty;
    public string InventoryId { get; set; } = string.Empty;
    public string Sku { get; set; } = string.Empty;
    public string ProductName { get; set; } = string.Empty;
    public decimal Quantity { get; set; }
    public string UnitOfMeasurementId { get; set; } = string.Empty;
}
