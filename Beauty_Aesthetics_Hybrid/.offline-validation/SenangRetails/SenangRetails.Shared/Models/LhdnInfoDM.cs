using System;
using System.Collections.Generic;
using System.Text;

namespace SenangRetails.Shared.Models
{
    public class LhdnInfoDM
    {
        // Invoice Details
        public string InvoiceUuid { get; set; } = "-";

        //要和那个ReceiptDM拿TotalPaid
        public decimal TotalAmount { get; set; }

        //public LhdnInfoDM(ReceiptDM receipt)
        //{
        //    if (receipt != null)
        //    {
        //        // 按照你的要求：把 TotalSales 赋值给 TotalAmount
        //        this.TotalAmount = receipt.TotalSales;

        //        // 建议：如果 Receipt 有日期，也可以同步过来
        //        this.IssuedDate = receipt.TransactionDate;
        //    }
        //}
        public decimal TotalTax { get; set; }



        // Submission Details
        public string SubmissionUuid { get; set; } = "-";
        public string Status { get; set; } = "-";
        public DateTime? ReceivedDate { get; set; }
        public DateTime? VerifiedDate { get; set; }
        public DateTime? IssuedDate { get; set; }
    }
}
