using System;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;

namespace SenangRetails.Shared.Data
{
    public static class LocalDatabaseInitializer
    {
        private static bool _initialized = false;
        private static readonly SemaphoreSlim _lock = new(1, 1);

        public static async Task InitializeAsync(LocalAppDbContext context)
        {
            if (_initialized) return;

            await _lock.WaitAsync();
            try
            {
                if (_initialized) return;
                await context.Database.EnsureCreatedAsync();
                await context.Database.ExecuteSqlRawAsync("""
                    CREATE TABLE IF NOT EXISTS LocalDataCaches (
                        CacheKey TEXT NOT NULL CONSTRAINT PK_LocalDataCaches PRIMARY KEY,
                        DataJson TEXT NOT NULL,
                        LastUpdatedAtUtc TEXT NOT NULL
                    );
                    """);
                await context.Database.ExecuteSqlRawAsync(
                    "CREATE INDEX IF NOT EXISTS IX_LocalDataCaches_LastUpdatedAtUtc ON LocalDataCaches (LastUpdatedAtUtc);");
                _initialized = true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[LocalDatabaseInitializer] Failed to initialize SQLite database: {ex.Message}");
            }
            finally
            {
                _lock.Release();
            }
        }
    }
}
