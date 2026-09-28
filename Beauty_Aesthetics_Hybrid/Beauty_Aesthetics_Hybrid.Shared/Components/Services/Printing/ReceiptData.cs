namespace Beauty_Aesthetics_WebPos.Components.Services.Printing;

public sealed class ReceiptData
{
    public string CompanyName { get; set; } = string.Empty;
    public string BranchName { get; set; } = string.Empty;
    public string ReceiptNo { get; set; } = string.Empty;
    public DateTime DateTimeOfSale { get; set; } = DateTime.Now;
    public string CustomerName { get; set; } = string.Empty;
    public string CustomerPhone { get; set; } = string.Empty;
    public string CashierName { get; set; } = string.Empty;
    public string CurrencyName { get; set; } = "MYR";
    public List<ReceiptLineItem> Items { get; set; } = new();
    public List<ReceiptPaymentLine> Payments { get; set; } = new();
    public decimal Subtotal { get; set; }
    public decimal Discount { get; set; }
    public decimal TaxAmount { get; set; }
    public decimal GrandTotal { get; set; }
    public decimal PaidAmount { get; set; }
    public decimal ChangeAmount { get; set; }
    public string EInvoiceQrUrl { get; set; } = string.Empty;
}

public sealed class ReceiptLineItem
{
    public string Name { get; set; } = string.Empty;
    public decimal Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal Discount { get; set; }
    public decimal LineTotal { get; set; }
    public string Remarks { get; set; } = string.Empty;
}

public sealed class ReceiptPaymentLine
{
    public string Method { get; set; } = string.Empty;
    public decimal Amount { get; set; }
}
