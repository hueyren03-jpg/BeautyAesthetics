using System;
using System.Collections.Generic;
using System.Text;


namespace SenangRetails.Shared.Models
{
    public class EInvoicesTableDM
    {
        public string ReceiptNo { get; set; }

        //这里可能拿那个receiptDM的transaction Date 如果说有分期的话 是不是代表说有多个时间
        public DateTime ReceiptDate { get; set; }

        //这里要拿那个memberdetailsDM
        public string BuyerName { get; set; }

        //这里要拿那个receiptDM
        public decimal Amount { get; set; }

        public decimal Tax { get; set; }

        public EInvoicesDM.InvoiceStatus Status { get; set; }
    }
}
