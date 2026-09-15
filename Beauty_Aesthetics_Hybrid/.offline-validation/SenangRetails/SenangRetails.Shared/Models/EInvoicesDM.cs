using System;
using System.Collections.Generic;
using System.Text;

namespace SenangRetails.Shared.Models
{
    public class EInvoicesDM
    {
        // Summary
        public int TotalInvoice { get; set; }
        public decimal TotalValue { get; set; }
        public int SubmittedInvoice { get; set; }
        public decimal SubmittedValue { get; set; }

        public enum InvoiceStatus
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

        public enum DatePreset
        {
            Today,
            Last7Days,
            ThisMonth,
            LastMonth,
            ThreeMonths,
            SixMonths,
            Custom
        }

        // Filters
        public InvoiceStatus SelectedStatus { get; set; } = InvoiceStatus.All;
        public DatePreset SelectedDatePreset { get; set; } = DatePreset.Today;

        public DateTime? DateFrom { get; set; }
        public DateTime? DateTo { get; set; }

        public string SearchKeyword { get; set; }

        // Table
        public List<EInvoicesTableDM> InvoicesTable { get; set; } = new();

        // UI State
        public bool IsLoading { get; set; }
        public bool IsDownloadingCsv { get; set; }
        public bool IsDownloadingPDF { get; set; }
    }
}
