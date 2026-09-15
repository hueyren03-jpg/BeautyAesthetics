using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SenangRetails.Shared.Data.Entities
{
    [Table("OfflineCashSales")]
    public class OfflineCashSaleEntity
    {
        [Key]
        [MaxLength(64)]
        public string LocalId { get; set; } = Guid.NewGuid().ToString();

        [Required]
        [MaxLength(64)]
        public string LocalDisplayCode { get; set; } = string.Empty;

        [MaxLength(64)]
        public string BranchId { get; set; } = string.Empty;

        public DateTime FinancialDate { get; set; } = DateTime.Today;

        [MaxLength(64)]
        public string AccountId { get; set; } = string.Empty;

        [MaxLength(128)]
        public string AccountName { get; set; } = "Walk-In Customer";

        [Column(TypeName = "decimal(18,2)")]
        public decimal TotalAmount { get; set; }

        public int ItemCount { get; set; }

        /// <summary>
        /// Serialized Doc_CashSales request object ready to be posted to CashSalesAC
        /// </summary>
        [Required]
        public string OrderPayloadJson { get; set; } = string.Empty;

        /// <summary>
        /// Serialized List&lt;PaymentLine&gt; payments associated with this cash sale
        /// </summary>
        public string PaymentLinesJson { get; set; } = "[]";

        public SyncStatus Status { get; set; } = SyncStatus.PendingSync;

        public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

        public DateTime? SyncedAtUtc { get; set; }

        [MaxLength(64)]
        public string? ServerDocumentId { get; set; }

        [MaxLength(64)]
        public string? ServerDisplayCode { get; set; }

        public string? LastErrorMessage { get; set; }

        public int RetryCount { get; set; } = 0;
    }
}
