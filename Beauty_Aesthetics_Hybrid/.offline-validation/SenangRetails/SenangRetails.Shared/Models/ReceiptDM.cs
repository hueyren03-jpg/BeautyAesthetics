using System;
using System.Collections.Generic;
using System.Text;
using SenangRetails.Shared.Components.Modals;


namespace SenangRetails.Shared.Models
{
    public class ReceiptDM
    {
        public MemberDetailsDM Member { get; set; } = new MemberDetailsDM();
        public string ReceiptId { get; set; } = " ";

        public string ReceiptNo { get; set; } = " ";
        public DateTime TransactionDate { get; set; }

        public List<ServiceItemDM> Items { get; set; } = new List<ServiceItemDM>();

        public string CashierName { get; set; } = " ";

        public List<PaymentMethodDM> Payments { get; set; } = new List<PaymentMethodDM>();

        public decimal TotalSales => Items.Sum(x => x.Subtotal);
        public decimal TotalPaid => Payments.Sum(x => x.Amount);

        public decimal OutstandingBalance => TotalSales - TotalPaid;

        public string Remarks { get; set; } = " ";

        public enum EInvoiceStatus
        {
            All,
            NoStatus,
            Pending,
            Submitted,
            Failed,
            Cancelled,
            Verified,
            Voided
        }

        public EInvoiceStatus Status { get; set; }

        public DateTime StatusUpdateDate { get; set; }

        public bool IsVoided { get; set; }
        public DateTime VoidedDate { get; set; }
    }
}
