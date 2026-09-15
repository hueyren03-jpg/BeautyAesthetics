using System.Collections.Generic;
using System.Threading.Tasks;
using SenangRetails.Shared.Models.DTOs;

namespace SenangRetails.Shared.Services.PriceGroupService
{
    public interface IPriceGroupService
    {
        Task<List<PriceGroupModel>> GetPriceGroupsAsync();
        Task<PriceGroupModel?> GetPriceGroupAsync(string code);
        Task<PriceGroupModel?> GetPriceGroupByProductIdAsync(string productId);
        Task<bool> SavePriceGroupAsync(PriceGroupModel group, bool isNew);
        Task<bool> DeletePriceGroupAsync(string code);
        Task<bool> AssignProductToPriceGroupAsync(string productId, string? priceGroupCode);
        Task<string?> GetAssignedPriceGroupCodeAsync(string productId);
        Task<decimal?> GetEffectiveProductPriceAsync(string productId, decimal originalPrice);
    }
}
