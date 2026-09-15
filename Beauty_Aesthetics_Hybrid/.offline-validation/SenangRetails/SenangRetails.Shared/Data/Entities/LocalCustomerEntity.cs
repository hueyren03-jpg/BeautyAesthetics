using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SenangRetails.Shared.Data.Entities
{
    [Table("LocalCustomers")]
    public class LocalCustomerEntity
    {
        [Key]
        [MaxLength(128)]
        public string MasterAccountId { get; set; } = string.Empty;

        [MaxLength(256)]
        public string AccountName { get; set; } = string.Empty;

        [MaxLength(64)]
        public string Phone { get; set; } = string.Empty;

        [MaxLength(256)]
        public string Email { get; set; } = string.Empty;

        [MaxLength(64)]
        public string NRIC { get; set; } = string.Empty;

        [MaxLength(128)]
        public string MembershipTypeName { get; set; } = string.Empty;

        public string RawJson { get; set; } = "{}";

        public DateTime LastUpdatedAtUtc { get; set; } = DateTime.UtcNow;
    }
}
