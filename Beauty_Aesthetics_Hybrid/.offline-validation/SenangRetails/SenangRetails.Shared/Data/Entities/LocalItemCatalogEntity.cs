using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SenangRetails.Shared.Data.Entities
{
    [Table("LocalItemCatalogs")]
    public class LocalItemCatalogEntity
    {
        [Key]
        [MaxLength(64)]
        public string BranchId { get; set; } = "default";

        public string ItemsJson { get; set; } = "[]";

        public string CategoriesJson { get; set; } = "[]";

        public string PaymentMethodsJson { get; set; } = "[]";

        public DateTime LastUpdatedAtUtc { get; set; } = DateTime.UtcNow;
    }
}
