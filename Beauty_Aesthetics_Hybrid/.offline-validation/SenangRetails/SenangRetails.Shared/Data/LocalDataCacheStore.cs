using System;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using SenangRetails.Shared.Data.Entities;

namespace SenangRetails.Shared.Data
{
    public static class LocalDataCacheStore
    {
        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            PropertyNameCaseInsensitive = true
        };

        public static async Task<T?> GetAsync<T>(string cacheKey)
        {
            try
            {
                using var db = new LocalAppDbContext();
                await LocalDatabaseInitializer.InitializeAsync(db);
                var row = await db.LocalDataCaches.AsNoTracking()
                    .FirstOrDefaultAsync(x => x.CacheKey == cacheKey);

                return row == null || string.IsNullOrWhiteSpace(row.DataJson)
                    ? default
                    : JsonSerializer.Deserialize<T>(row.DataJson, JsonOptions);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[LocalDataCacheStore] Read '{cacheKey}' failed: {ex.Message}");
                return default;
            }
        }

        public static async Task SetAsync<T>(string cacheKey, T value)
        {
            try
            {
                using var db = new LocalAppDbContext();
                await LocalDatabaseInitializer.InitializeAsync(db);
                var row = await db.LocalDataCaches.FirstOrDefaultAsync(x => x.CacheKey == cacheKey);
                if (row == null)
                {
                    row = new LocalDataCacheEntity { CacheKey = cacheKey };
                    db.LocalDataCaches.Add(row);
                }

                row.DataJson = JsonSerializer.Serialize(value, JsonOptions);
                row.LastUpdatedAtUtc = DateTime.UtcNow;
                await db.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[LocalDataCacheStore] Write '{cacheKey}' failed: {ex.Message}");
            }
        }
    }
}
