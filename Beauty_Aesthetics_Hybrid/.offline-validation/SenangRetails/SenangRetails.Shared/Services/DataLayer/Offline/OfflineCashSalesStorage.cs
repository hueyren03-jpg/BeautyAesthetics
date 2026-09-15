using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using EBI.UC;
using Microsoft.EntityFrameworkCore;
using SenangRetails.Shared.Data;
using SenangRetails.Shared.Data.Entities;
using SenangRetails.Shared.Models.DTOs;

namespace SenangRetails.Shared.Services.DataLayer.Offline
{
    public class OfflineCashSalesStorage : IOfflineCashSalesStorage
    {
        private readonly LocalAppDbContext _dbContext;

        public OfflineCashSalesStorage(LocalAppDbContext dbContext)
        {
            _dbContext = dbContext;
        }

        public async Task<OfflineCashSaleEntity> SaveOfflineSaleAsync(Doc_CashSales order, List<PaymentLine> payments)
        {
            await LocalDatabaseInitializer.InitializeAsync(_dbContext);

            var localId = Guid.NewGuid().ToString();
            var localDisplayCode = $"OFF-{DateTime.Now:yyyyMMdd}-{DateTime.Now:HHmmss}-{new Random().Next(100, 999)}";

            decimal total = 0;
            int itemCount = 0;

            if (order.lstDocumentLine != null)
            {
                itemCount = order.lstDocumentLine.Count;
                total = order.lstDocumentLine.Sum(d => (decimal)(d.UnitPrice * d.Quantity - d.Discount));
            }
            if (total == 0 && order.objDoc_CashSales != null)
            {
                total = order.objDoc_CashSales.TotalAfterTax != 0 ? order.objDoc_CashSales.TotalAfterTax : order.objDoc_CashSales.TotalBeforeTax;
            }
            if (total == 0 && payments != null)
            {
                total = payments.Sum(p => p.Amount);
            }

            var branchId = order.objDoc_CashSales?.BranchID ?? "";
            var finDate = order.objDoc_CashSales != null && order.objDoc_CashSales.FinancialDate != default 
                ? order.objDoc_CashSales.FinancialDate 
                : DateTime.Today;
            var accountId = order.objDoc_CashSales?.AccountID ?? "";
            var accountName = string.IsNullOrWhiteSpace(order.objDoc_CashSales?.AccountName) 
                ? "Walk-In Customer" 
                : order.objDoc_CashSales.AccountName;

            var orderJson = JsonSerializer.Serialize(order);
            var paymentsJson = JsonSerializer.Serialize(payments ?? new List<PaymentLine>());

            var entity = new OfflineCashSaleEntity
            {
                LocalId = localId,
                LocalDisplayCode = localDisplayCode,
                BranchId = branchId,
                FinancialDate = finDate,
                AccountId = accountId,
                AccountName = accountName,
                TotalAmount = total,
                ItemCount = itemCount,
                OrderPayloadJson = orderJson,
                PaymentLinesJson = paymentsJson,
                Status = SyncStatus.PendingSync,
                CreatedAtUtc = DateTime.UtcNow,
                RetryCount = 0
            };

            _dbContext.OfflineCashSales.Add(entity);
            await _dbContext.SaveChangesAsync();

            return entity;
        }

        public async Task<List<OfflineCashSaleEntity>> GetPendingOfflineSalesAsync()
        {
            await LocalDatabaseInitializer.InitializeAsync(_dbContext);

            return await _dbContext.OfflineCashSales
                .Where(x => x.Status == SyncStatus.PendingSync || x.Status == SyncStatus.Failed)
                .OrderBy(x => x.CreatedAtUtc)
                .ToListAsync();
        }

        public async Task<List<OfflineCashSaleEntity>> GetAllLocalSalesAsync(string branchId, DateTime date)
        {
            await LocalDatabaseInitializer.InitializeAsync(_dbContext);

            var targetDate = date.Date;
            return await _dbContext.OfflineCashSales
                .Where(x => x.FinancialDate.Date == targetDate && (string.IsNullOrEmpty(branchId) || x.BranchId == branchId))
                .OrderByDescending(x => x.CreatedAtUtc)
                .ToListAsync();
        }

        public async Task<bool> MarkSaleSyncedAsync(string localId, string serverDocId, string serverDisplayCode)
        {
            await LocalDatabaseInitializer.InitializeAsync(_dbContext);

            var sale = await _dbContext.OfflineCashSales.FirstOrDefaultAsync(x => x.LocalId == localId);
            if (sale == null) return false;

            sale.Status = SyncStatus.Synced;
            sale.SyncedAtUtc = DateTime.UtcNow;
            sale.ServerDocumentId = serverDocId;
            sale.ServerDisplayCode = serverDisplayCode;
            sale.LastErrorMessage = null;

            await _dbContext.SaveChangesAsync();
            return true;
        }

        public async Task<bool> MarkSaleFailedAsync(string localId, string errorMessage)
        {
            await LocalDatabaseInitializer.InitializeAsync(_dbContext);

            var sale = await _dbContext.OfflineCashSales.FirstOrDefaultAsync(x => x.LocalId == localId);
            if (sale == null) return false;

            sale.Status = SyncStatus.Failed;
            sale.LastErrorMessage = errorMessage;
            sale.RetryCount++;

            await _dbContext.SaveChangesAsync();
            return true;
        }

        public async Task<int> GetPendingCountAsync()
        {
            await LocalDatabaseInitializer.InitializeAsync(_dbContext);

            return await _dbContext.OfflineCashSales
                .CountAsync(x => x.Status == SyncStatus.PendingSync || x.Status == SyncStatus.Failed);
        }

        public async Task<OfflineCashSaleEntity?> GetSaleByLocalIdAsync(string localId)
        {
            await LocalDatabaseInitializer.InitializeAsync(_dbContext);

            return await _dbContext.OfflineCashSales.FirstOrDefaultAsync(x => x.LocalId == localId);
        }

        public async Task<bool> DeleteLocalSaleAsync(string localId)
        {
            await LocalDatabaseInitializer.InitializeAsync(_dbContext);

            var sale = await _dbContext.OfflineCashSales.FirstOrDefaultAsync(x => x.LocalId == localId);
            if (sale == null) return false;

            _dbContext.OfflineCashSales.Remove(sale);
            await _dbContext.SaveChangesAsync();
            return true;
        }
    }
}
