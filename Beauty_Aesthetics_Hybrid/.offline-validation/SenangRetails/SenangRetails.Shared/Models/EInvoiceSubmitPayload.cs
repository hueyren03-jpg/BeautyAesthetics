namespace SenangRetails.Shared.Models
{
    public class EInvoiceSubmitPayload
    {
        public string? UUID { get; set; }
        public string? DocumentStatus { get; set; }
        public object? DocumentReason { get; set; }
        public List<ConsolidatedInvoiceItem> lstConsolidatedInvoice { get; set; } = new();
    }

    public class ConsolidatedInvoiceItem
    {
        public object? DocumentID { get; set; }
        public string? BranchID { get; set; }
        public DateTime? FinancialDate { get; set; }
        public string? AlphaCode { get; set; }
        public int? NumericCode { get; set; }
        public string DisplayCode { get; set; } = string.Empty;
        public object? TransactionCurrencyID { get; set; }
        public string? TransactionCurrencyName { get; set; }
        public decimal TaxableAmount { get; set; }
        public decimal TotalBeforeTax { get; set; }
        public decimal TaxAmount { get; set; }
        public decimal TourismTax { get; set; }
        public decimal RoundingAmount { get; set; }
        public decimal TotalAfterTax { get; set; }
        public bool IsSelected { get; set; }
        public string? ToDisplayCode { get; set; }
    }
}