using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SenangRetails.Shared.Data.Entities
{
    [Table("LocalDataCaches")]
    public class LocalDataCacheEntity
    {
        [Key]
        [MaxLength(200)]
        public string CacheKey { get; set; } = string.Empty;

        public string DataJson { get; set; } = "null";

        public DateTime LastUpdatedAtUtc { get; set; } = DateTime.UtcNow;
    }
}
