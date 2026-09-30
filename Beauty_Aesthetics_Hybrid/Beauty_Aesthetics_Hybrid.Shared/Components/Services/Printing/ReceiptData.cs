namespace Beauty_Aesthetics_WebPos.Components.Services.Printing;

public sealed class ReceiptData
{
    public string CompanyName { get; set; } = string.Empty;
    public string LogoUrl { get; set; } = string.Empty;
    public string BranchName { get; set; } = string.Empty;
    public string Address { get; set; } = string.Empty;
    public string Address1 { get; set; } = string.Empty;
    public string Address2 { get; set; } = string.Empty;
    public string Address3 { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string? CoRegistrationNo { get; set; }
    public string? TIN { get; set; }
    public string ReceiptNo { get; set; } = string.Empty;
    public DateTime? DateTimeOfSale { get; set; }
    public string ReferenceNumber { get; set; } = string.Empty;
    public string CustomerID { get; set; } = string.Empty;
    public string CustomerName { get; set; } = string.Empty;
    public List<ReceiptLineItem> Items { get; set; } = new();
    public List<ReceiptPaymentLine> Payments { get; set; } = new();
    public List<TaxSummaryLine> TaxSummary { get; set; } = new();
    public decimal ChangeAmount { get; set; }
    public decimal Subtotal { get; set; }
    public decimal RoundingAmount { get; set; }
    public decimal GrandTotal { get; set; }
    public string PaymentMethod { get; set; } = string.Empty;
    public decimal PaidAmount { get; set; }
    public string CustomerAddress1 { get; set; } = string.Empty;
    public string CustomerAddress2 { get; set; } = string.Empty;
    public string CustomerAddress3 { get; set; } = string.Empty;
    public string CustomerCity { get; set; } = string.Empty;
    public string CustomerPostcode { get; set; } = string.Empty;
    public string CustomerState { get; set; } = string.Empty;
    public string CustomerCountry { get; set; } = string.Empty;
    public string CustomerPhone { get; set; } = string.Empty;
    public string CurrencyName { get; set; } = string.Empty;
    public string CashierName { get; set; } = string.Empty;
    public decimal TaxAmount { get; set; }
    public decimal TotalBeforeTax_ServiceCharge { get; set; }
    public string EInvoiceQrUrl { get; set; } = string.Empty;
}

public sealed class ReceiptLineItem
{
    public string Name { get; set; } = string.Empty;
    public decimal Quantity { get; set; }
    public decimal Discount { get; set; }
    public decimal LineTotal { get; set; }
    public string Remarks { get; set; } = string.Empty;
    public string PaymentMethod { get; set; } = string.Empty;
}

public sealed class ReceiptPaymentLine
{
    public string Method { get; set; } = string.Empty;
    public decimal Amount { get; set; }
}

public sealed class TaxSummaryLine
{
    public string TaxCode { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public decimal Tax { get; set; }
}
