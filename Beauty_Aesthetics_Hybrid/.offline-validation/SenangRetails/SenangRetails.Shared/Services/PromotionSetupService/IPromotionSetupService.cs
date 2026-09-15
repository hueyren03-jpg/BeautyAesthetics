using System.Collections.Generic;
using System.Threading.Tasks;
using SenangRetails.Shared.Models.DTOs;

namespace SenangRetails.Shared.Services.PromotionSetupService
{
    public interface IPromotionSetupService
    {
        string? LastError { get; }
        Task<List<PromotionSetupModel>> GetPromotionsAsync();
        Task<PromotionSetupModel?> GetPromotionAsync(string code);
        Task<bool> SavePromotionAsync(PromotionSetupModel promo, bool isNew);
        Task<bool> DeletePromotionAsync(string code);
        Task<string?> GetAssignedPromotionCodeAsync(string productId);
        Task<bool> AssignProductToPromotionAsync(string productId, string? promoCode);
    }
}
