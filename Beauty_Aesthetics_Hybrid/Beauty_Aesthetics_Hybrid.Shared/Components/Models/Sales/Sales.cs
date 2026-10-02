namespace Beauty_Aesthetics_WebPos.Components.Models
{
    /// <summary>
    /// Represents a sales transaction
    /// </summary>
    public class Transaction
    {
        public int Id { get; set; }
        public string DocumentId { get; set; } = "";
        public int DocumentTypeId { get; set; } = 5;
        public string AccountId { get; set; } = "";
        public string BranchId { get; set; } = "";
        public string GroupId { get; set; } = "";
        public int PaymentTypeId { get; set; }
        public DateTime Date { get; set; }
        public string InvoiceNumber { get; set; } = "";
        public string CustomerName { get; set; } = "";
        public string CustomerContact { get; set; } = "";
        public string CustomerEmail { get; set; } = "";
        public string Branch { get; set; } = "";
        public string Type { get; set; } = ""; // Service or Product
        public int ItemCount { get; set; }
        public decimal Amount { get; set; }
        public decimal Subtotal { get; set; }
        public decimal Tax { get; set; }
        public decimal Discount { get; set; }
        public decimal RoundingAmount { get; set; }
        public decimal ServiceChargeAmount { get; set; }
        public string CurrencyName { get; set; } = "MYR";
        public string PaymentMethod { get; set; } = ""; // Cash, Card, Online Transfer, E-Wallet
        public List<TransactionPayment> Payments { get; set; } = new();

        // Complete receipt-line collection used for printing/history presentation.
        // This intentionally includes system-controlled rows such as Member Credit
        // (POSPaymentTypeID = -10), while Payments remains the ordinary tender list.
        public List<TransactionPayment> ReceiptPayments { get; set; } = new();

        public string Status { get; set; } = ""; // Paid, Pending, Cancelled, Voided
        public string ReferenceNumber { get; set; } = "";
        public string Notes { get; set; } = "";
        public List<TransactionItem> Items { get; set; } = new();
        public DateTime CreatedAt { get; set; } = DateTime.Now;
        public string CreatedBy { get; set; } = "";
        public DateTime? ModifiedAt { get; set; }
        public string? ModifiedBy { get; set; }
        public DateTime? CancelledAt { get; set; }
        public string? CancelledBy { get; set; }
        public string? CancellationReason { get; set; }
        public bool IsDeleted { get; set; } = false; // Soft delete flag
    }

    public class TransactionPayment
    {
        public string ReceiptLineId { get; set; } = "";
        public int PaymentTypeId { get; set; }
        public string PaymentMethod { get; set; } = "";
        public string SourceDocumentLineId { get; set; } = "";
        public decimal Amount { get; set; }
        public decimal ChangeAmount { get; set; }
        public string FinancialAccountId { get; set; } = "";
        public string BankName { get; set; } = "";
    }

    /// <summary>
    /// Represents an item/service in a transaction
    /// </summary>
    public class TransactionItem
    {
        public int Id { get; set; }
        public int TransactionId { get; set; }
        public string InventoryId { get; set; } = "";
        public string Name { get; set; } = "";
        public string Sku { get; set; } = "";
        public string Description { get; set; } = "";
        public string Remarks { get; set; } = "";
        public string ImageUrl { get; set; } = "";
        public string Category { get; set; } = ""; // Service, Product, Package, Member Credit
        public int InventoryTypeId { get; set; }
        public int Quantity { get; set; }
        public decimal UnitPrice { get; set; }
        public decimal TotalPrice { get; set; }
        public decimal Discount { get; set; }
        public string CashDiscountId { get; set; } = "";
        public string DiscountMemo { get; set; } = "";
        public string UnitOfMeasureId { get; set; } = "";
        public string TaxCodeId { get; set; } = "";
        public decimal TaxPercentage { get; set; }
        public decimal TaxAmount { get; set; }
        public bool IsTaxInclusive { get; set; }

        // Point redemption (normal Sales / DocumentTypeID 5).
        // PointToRedeem and AllowPointRedemption come from the selected inventory record.
        // OriginalUnitPrice is local cart state so removing a point redemption can restore
        // the normal selling price after UnitPrice is set to 0, matching Senang.
        public decimal PointToRedeem { get; set; }
        public bool AllowPointRedemption { get; set; }
        public decimal Points { get; set; }
        public decimal OriginalUnitPrice { get; set; }

        // Member Credit redemption (Senang single-account structure).
        public int ActivityTypeId { get; set; } = 1;
        public string MemberCreditAccountId { get; set; } = "";
        public string MemberTypeId { get; set; } = "";
        public string MembershipCredit { get; set; } = "";
        public List<MemberCreditAllocation> MemberCreditAllocations { get; set; } = new();
    }

    public sealed class MemberCreditAllocation
    {
        public string MemberCreditAccountId { get; set; } = "";
        public string MemberTypeId { get; set; } = "";
        public decimal Amount { get; set; }
    }

    /// <summary>
    /// Represents transaction category summary (Services vs Products)
    /// </summary>
    public class TransactionCategory
    {
        public string Category { get; set; } = "";
        public decimal Amount { get; set; }
        public int Count { get; set; }
        public decimal Percentage { get; set; }
    }

    /// <summary>
    /// Represents cash flow data
    /// </summary>
    public class CashFlowItem
    {
        public string Category { get; set; } = "";
        public decimal Amount { get; set; }
        public DateTime Date { get; set; }
    }

    /// <summary>
    /// Sales dashboard summary
    /// </summary>
    public class SalesSummary
    {
        public decimal TotalSales { get; set; }
        public decimal TotalCash { get; set; }
        public decimal TotalCard { get; set; }
        public decimal TotalOnlineTransfer { get; set; }
        public decimal TotalEWallet { get; set; }
        public decimal Outstanding { get; set; }
        public decimal Rounding { get; set; }
        public int TotalTransactions { get; set; }
        public decimal AverageSale { get; set; }
        public decimal ServicesAmount { get; set; }
        public decimal ProductsAmount { get; set; }
        public string ServicesPercentage { get; set; } = "0";
        public string ProductsPercentage { get; set; } = "0";
        public DateTime FromDate { get; set; }
        public DateTime ToDate { get; set; }
    }

    /// <summary>
    /// Sales filter criteria
    /// </summary>
    public class SalesFilter
    {
        public DateTime? StartDate { get; set; }
        public DateTime? EndDate { get; set; }
        public string? Branch { get; set; }
        public string? Status { get; set; }
        public string? PaymentMethod { get; set; }
        public string? Type { get; set; }
        public decimal? MinAmount { get; set; }
        public decimal? MaxAmount { get; set; }
    }

    /// <summary>
    /// Branch/Outlet information
    /// </summary>
    public class Branch
    {
        public int Id { get; set; }
        public string Name { get; set; } = "";
        public string Code { get; set; } = "";
        public string Location { get; set; } = "";
        public string ContactNumber { get; set; } = "";
        public bool IsActive { get; set; } = true;
    }

    /// <summary>
    /// Invoice details for printing
    /// </summary>
    public class Invoice
    {
        public Transaction Transaction { get; set; } = new();
        public Branch Branch { get; set; } = new();
        public string CompanyName { get; set; } = "EBI Medical Aesthetic";
        public string CompanyAddress { get; set; } = "";
        public string CompanyPhone { get; set; } = "";
        public string CompanyEmail { get; set; } = "";
        public string TaxRegistrationNumber { get; set; } = "";
        public string TermsAndConditions { get; set; } = "";
    }
}
