namespace SenangRetails.Shared.Services.ReceiptPrinterService
{
    public class ReceiptData
    {
        public string CompanyName { get; set; } = "";
        public string BranchName { get; set; } = "";
        public string Address { get; set; } = "";
        public string Address1 { get; set; } = "";
        public string Address2 { get; set; } = "";
        public string Address3 { get; set; } = "";
        public string Phone { get; set; } = "";
        public string Email { get; set; } = "";
        public string? CoRegistrationNo { get; set; }
        public string? TIN { get; set; }
        public string ReceiptNo { get; set; } = "";
        public DateTime? DateTimeOfSale { get; set; }
        public string ReferenceNumber { get; set; } = string.Empty;
        public string CustomerID { get; set; } = string.Empty;
        public string CustomerName { get; set; } = "";
        public List<ReceiptLineItem> Items { get; set; } = new();
        public List<ReceiptPaymentLine> Payments { get; set; } = new();
        public List<TaxSummaryLine> TaxSummary { get; set; } = new();
        public decimal ChangeAmount { get; set; }
        public decimal Subtotal { get; set; }
        public decimal RoundingAmount { get; set; }
        public decimal GrandTotal { get; set; }
        public string PaymentMethod { get; set; } = "";
        public decimal PaidAmount { get; set; }
        public string CustomerAddress1 { get; set; } = "";
        public string CustomerAddress2 { get; set; } = "";
        public string CustomerAddress3 { get; set; } = "";
        public string CustomerCity { get; set; } = "";
        public string CustomerPostcode { get; set; } = "";
        public string CustomerState { get; set; } = "";
        public string CustomerCountry { get; set; } = "";
        public string CustomerPhone { get; set; } = "";
        public string CurrencyName { get; set; } = "";
        public string CashierName { get; set; } = "";
        public decimal TaxAmount { get; set; }
        public decimal TotalBeforeTax_ServiceCharge { get; set; }
        public string EInvoiceQrUrl { get; set; } = "";
    }

    public class ReceiptLineItem
    {
        public string Name { get; set; } = "";
        public decimal Quantity { get; set; }
        public decimal Discount { get; set; }
        public decimal LineTotal { get; set; }
        public string Remarks { get; set; } = string.Empty;
        public string PaymentMethod { get; set; } = string.Empty;
    }

    public class ReceiptPaymentLine
    {
        public string Method { get; set; } = "";
        public decimal Amount { get; set; }
    }

    public class TaxSummaryLine
    {
        public string TaxCode { get; set; } = "";
        public decimal Amount { get; set; }
        public decimal Tax { get; set; }
    }
}
