using System;
using System.IO;
using Microsoft.EntityFrameworkCore;
using SenangRetails.Shared.Data.Entities;

namespace SenangRetails.Shared.Data
{
    public class LocalAppDbContext : DbContext
    {
        public DbSet<OfflineCashSaleEntity> OfflineCashSales => Set<OfflineCashSaleEntity>();
        public DbSet<LocalItemCatalogEntity> LocalItemCatalogs => Set<LocalItemCatalogEntity>();
        public DbSet<LocalCustomerEntity> LocalCustomers => Set<LocalCustomerEntity>();
        public DbSet<LocalDataCacheEntity> LocalDataCaches => Set<LocalDataCacheEntity>();

        public LocalAppDbContext() : base()
        {
        }

        public LocalAppDbContext(DbContextOptions<LocalAppDbContext> options) : base(options)
        {
        }

        public static string? CustomDbPath { get; set; }

        public static string GetDefaultDbPath()
        {
            if (!string.IsNullOrWhiteSpace(CustomDbPath))
            {
                return CustomDbPath;
            }

            var folder = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            if (string.IsNullOrWhiteSpace(folder))
            {
                folder = Environment.GetFolderPath(Environment.SpecialFolder.Personal);
            }
            if (string.IsNullOrWhiteSpace(folder))
            {
                folder = AppDomain.CurrentDomain.BaseDirectory;
            }
            return Path.Combine(folder, "senang_local_pos.db");
        }

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            if (!optionsBuilder.IsConfigured)
            {
                var dbPath = GetDefaultDbPath();
                optionsBuilder.UseSqlite($"Data Source={dbPath}");
            }
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<OfflineCashSaleEntity>(entity =>
            {
                entity.HasKey(e => e.LocalId);
                entity.HasIndex(e => e.Status);
                entity.HasIndex(e => e.FinancialDate);
                entity.HasIndex(e => e.BranchId);
                entity.HasIndex(e => e.CreatedAtUtc);
            });

            modelBuilder.Entity<LocalCustomerEntity>(entity =>
            {
                entity.HasKey(e => e.MasterAccountId);
                entity.HasIndex(e => e.AccountName);
                entity.HasIndex(e => e.Phone);
                entity.HasIndex(e => e.NRIC);
                entity.HasIndex(e => e.Email);
            });

            modelBuilder.Entity<LocalDataCacheEntity>(entity =>
            {
                entity.HasKey(e => e.CacheKey);
                entity.HasIndex(e => e.LastUpdatedAtUtc);
            });
        }
    }
}
