namespace Beauty_Aesthetics_WebPos.Components.ViewModels;

public sealed class StockGrnViewModel
{
    public string DocumentId { get; set; } = string.Empty;

    public string BranchId { get; set; } = "HQ";

    public DateTime Date { get; set; } = DateTime.Today;

    public string DisplayCode { get; set; } = string.Empty;

    public string AccountName { get; set; } = string.Empty;

    public string AccountId { get; set; } = string.Empty;

    public string PODisplayCode { get; set; } = string.Empty;

    public string PODocumentId { get; set; } = string.Empty;

    public string OrderBranchId { get; set; } = string.Empty;

    public string PaymentTermId { get; set; } = string.Empty;

    public string PaymentTermName { get; set; } = string.Empty;

    public string CreatedByDocumentId { get; set; } = string.Empty;

    public string CreatedByDocumentDisplayCode { get; set; } = string.Empty;

    public string VerifyStatus { get; set; } = string.Empty;

    public string ReferenceNumber { get; set; } = string.Empty;

    public DateTime? PODate { get; set; }

    public DateTime PostingDate { get; set; } = DateTime.Today;

    public bool IsPostingDateDifferent { get; set; }

    public string Remarks { get; set; } = string.Empty;

    public string ItemName { get; set; } = string.Empty;

    public string ItemSku { get; set; } = string.Empty;

    public string InventoryId { get; set; } = string.Empty;

    public string Uom { get; set; } = string.Empty;

    public string OrderDocumentLineId { get; set; } = string.Empty;

    public string SourceDocumentLineId { get; set; } = string.Empty;

    public int InventoryTypeId { get; set; } = 1;

    public decimal OrderedQuantity { get; set; }

    public decimal ReceivedQuantity { get; set; }

    public decimal UnitPrice { get; set; }

    public decimal Amount { get; set; }

    public string TransactionCurrencyId { get; set; } = "MYR";

    public string LocalCurrencyId { get; set; } = "MYR";

    public decimal ExchangeRate { get; set; } = 1m;

    public string TaxTypeId { get; set; } = string.Empty;

    public string TaxCodeId { get; set; } = string.Empty;

    public string GstTypeId { get; set; } = string.Empty;

    public decimal TaxPercentage { get; set; }

    public decimal TaxAmount { get; set; }

    public decimal RoundingAmount { get; set; }

    public bool IsTaxInclusive { get; set; }

    public string BatchNumber { get; set; } = string.Empty;

    public string SerialNumber { get; set; } = string.Empty;

    public string StockActivityType { get; set; } = string.Empty;

    public bool IsLocked { get; set; }

    public bool IsVoid { get; set; }

    public List<StockGrnLineViewModel> Lines { get; set; } = new();

    public decimal TotalQuantity => Lines.Count > 0
        ? Lines.Sum(line => line.Quantity)
        : ReceivedQuantity;

    public string Status => IsVoid ? "Voided" : (IsLocked ? "Completed" : "Pending");
}

public sealed class StockGrnLineViewModel
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
