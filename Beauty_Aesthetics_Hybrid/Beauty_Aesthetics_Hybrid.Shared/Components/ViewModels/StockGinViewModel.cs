namespace Beauty_Aesthetics_WebPos.Components.ViewModels;

public sealed class StockGinViewModel
{
    public string DocumentId { get; set; } = string.Empty;
    public string DisplayCode { get; set; } = string.Empty;
    public string BranchId { get; set; } = "HQ";
    public DateTime Date { get; set; } = DateTime.Today;
    public string IssueType { get; set; } = string.Empty;
    public string AccountId { get; set; } = string.Empty;
    public string AccountName { get; set; } = string.Empty;
    public string OrderBranchId { get; set; } = string.Empty;
    public string ReferenceNumber { get; set; } = string.Empty;
    public string Remarks { get; set; } = string.Empty;
    public string VerifyStatus { get; set; } = string.Empty;
    public int CreatedByDocumentTypeId { get; set; }
    public string CreatedByDocumentTypeName { get; set; } = string.Empty;
    public string CreatedByDocumentId { get; set; } = string.Empty;
    public string CreatedByDocumentDisplayCode { get; set; } = string.Empty;
    public bool IsLocked { get; set; }
    public bool IsVoid { get; set; }
    public List<StockGinLineViewModel> Lines { get; set; } = new();

    public decimal TotalQuantity => Lines.Sum(line => line.Quantity);
    public bool IsGeneratedFromDocument =>
        CreatedByDocumentTypeId > 0 ||
        !string.IsNullOrWhiteSpace(CreatedByDocumentId) ||
        !string.IsNullOrWhiteSpace(CreatedByDocumentDisplayCode);

    public string Status
    {
        get
        {
            if (IsVoid) return "Voided";

            var status = VerifyStatus.Trim();
            if (status.Contains("void", StringComparison.OrdinalIgnoreCase) ||
                status.Contains("cancel", StringComparison.OrdinalIgnoreCase)) return "Voided";
            if (status.Contains("draft", StringComparison.OrdinalIgnoreCase)) return "Draft";
            if (status.Contains("post", StringComparison.OrdinalIgnoreCase) ||
                status.Contains("complete", StringComparison.OrdinalIgnoreCase) ||
                status.Contains("verify", StringComparison.OrdinalIgnoreCase) ||
                status.Contains("approve", StringComparison.OrdinalIgnoreCase)) return "Posted";

            return IsLocked || IsGeneratedFromDocument ? "Posted" : "Pending";
        }
    }
}

public sealed class StockGinLineViewModel
{
    public string DocumentLineId { get; set; } = string.Empty;
    public string InventoryId { get; set; } = string.Empty;
    public string ProductName { get; set; } = string.Empty;
    public string Sku { get; set; } = string.Empty;
    public decimal Quantity { get; set; }
    public string UnitOfMeasurementId { get; set; } = string.Empty;
    public int InventoryTypeId { get; set; } = 1;
    public decimal UnitCost { get; set; }
}
