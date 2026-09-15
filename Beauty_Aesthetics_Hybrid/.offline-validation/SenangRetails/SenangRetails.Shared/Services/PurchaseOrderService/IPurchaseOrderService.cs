using System.Collections.Generic;
using System.Threading.Tasks;
using SenangRetails.Shared.Models.DTOs;

namespace SenangRetails.Shared.Services.PurchaseOrderService
{
    public interface IPurchaseOrderService
    {
        Task<List<PurchaseOrderModel>> GetPurchaseOrdersAsync();
        Task<PurchaseOrderModel?> GetPurchaseOrderAsync(string poNo);
        Task<bool> SavePurchaseOrderAsync(PurchaseOrderModel po, bool isNew);
        Task<bool> DeletePurchaseOrderAsync(string poNo);
    }
}
