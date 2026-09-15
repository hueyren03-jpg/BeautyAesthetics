namespace SenangRetails.Shared.Models
{
    public class EInvoiceSubmissionSummary
    {
        public int ValidIndividualBill { get; set; }
        public decimal ValidIndividualAmount { get; set; }
        public int ValidConsoBill { get; set; }
        public decimal ValidConsoAmount { get; set; }
        public int InvalidBill { get; set; }
        public decimal InvalidAmount { get; set; }
        public int CancelledBill { get; set; }
        public decimal CancelledAmount { get; set; }
        public int PendingBill { get; set; }
        public decimal PendingAmount { get; set; }
    }
}