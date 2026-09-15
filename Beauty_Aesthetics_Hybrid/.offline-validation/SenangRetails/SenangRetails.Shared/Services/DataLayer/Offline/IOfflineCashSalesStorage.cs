using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using EBI.UC;
using SenangRetails.Shared.Data.Entities;
using SenangRetails.Shared.Models.DTOs;

namespace SenangRetails.Shared.Services.DataLayer.Offline
{
    public interface IOfflineCashSalesStorage
    {
        Task<OfflineCashSaleEntity> SaveOfflineSaleAsync(Doc_CashSales order, List<PaymentLine> payments);
        Task<List<OfflineCashSaleEntity>> GetPendingOfflineSalesAsync();
        Task<List<OfflineCashSaleEntity>> GetAllLocalSalesAsync(string branchId, DateTime date);
        Task<bool> MarkSaleSyncedAsync(string localId, string serverDocId, string serverDisplayCode);
        Task<bool> MarkSaleFailedAsync(string localId, string errorMessage);
        Task<int> GetPendingCountAsync();
        Task<OfflineCashSaleEntity?> GetSaleByLocalIdAsync(string localId);
        Task<bool> DeleteLocalSaleAsync(string localId);
    }
}
