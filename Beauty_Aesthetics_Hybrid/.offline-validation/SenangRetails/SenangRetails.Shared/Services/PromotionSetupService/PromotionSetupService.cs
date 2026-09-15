using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.JSInterop;
using SenangRetails.Shared.Models.DTOs;

namespace SenangRetails.Shared.Services.PromotionSetupService
{
    public class PromotionSetupService : IPromotionSetupService
    {
        private readonly IJSRuntime _js;
        private const string StorageKey = "senang_promotion_rules";
        public string? LastError { get; private set; }

        public PromotionSetupService(IJSRuntime js)
        {
            _js = js;
        }

        public async Task<List<PromotionSetupModel>> GetPromotionsAsync()
        {
            try
            {
                var json = await _js.InvokeAsync<string?>("localStorage.getItem", StorageKey);
                if (json != null)
                {
                    if (string.IsNullOrWhiteSpace(json)) return new List<PromotionSetupModel>();
                    var list = JsonSerializer.Deserialize<List<PromotionSetupModel>>(json);
                    return list ?? new List<PromotionSetupModel>();
                }
            }
            catch
            {
            }

            var defaultPromos = GetDefaultSeedPromotions();
            await SaveListAsync(defaultPromos);
            return defaultPromos;
        }

        public async Task<PromotionSetupModel?> GetPromotionAsync(string code)
        {
            var list = await GetPromotionsAsync();
            return list.FirstOrDefault(p => p.Code.Equals(code, StringComparison.OrdinalIgnoreCase));
        }

        public async Task<bool> SavePromotionAsync(PromotionSetupModel promo, bool isNew)
        {
            LastError = null;
            try
            {
                var list = await GetPromotionsAsync();

                // Ensure mutual exclusivity: remove assigned products from all other promotions
                if (promo.AppliedProductIds != null && promo.AppliedProductIds.Any())
                {
                    var assignedIds = promo.AppliedProductIds
                        .Where(id => !string.IsNullOrWhiteSpace(id))
                        .Select(id => id.Trim())
                        .ToHashSet(StringComparer.OrdinalIgnoreCase);

                    foreach (var other in list)
                    {
                        if (!string.Equals(other.Code, promo.Code, StringComparison.OrdinalIgnoreCase) && other.AppliedProductIds != null)
                        {
                            other.AppliedProductIds.RemoveAll(id => !string.IsNullOrWhiteSpace(id) && assignedIds.Contains(id.Trim()));
                        }
                    }
                }

                var index = list.FindIndex(p => p.Code.Equals(promo.Code, StringComparison.OrdinalIgnoreCase));
                if (index >= 0)
                {
                    list[index] = promo;
                }
                else
                {
                    list.Insert(0, promo);
                }

                return await SaveListAsync(list);
            }
            catch (Exception ex)
            {
                LastError = ex.Message;
                return false;
            }
        }

        public async Task<bool> DeletePromotionAsync(string code)
        {
            try
            {
                var list = await GetPromotionsAsync();
                list.RemoveAll(p => p.Code.Equals(code, StringComparison.OrdinalIgnoreCase));
                return await SaveListAsync(list);
            }
            catch
            {
                return false;
            }
        }

        public async Task<string?> GetAssignedPromotionCodeAsync(string productId)
        {
            if (string.IsNullOrWhiteSpace(productId)) return null;
            var trimmedId = productId.Trim();
            var promos = await GetPromotionsAsync();
            return promos.FirstOrDefault(p => p.AppliedProductIds != null && p.AppliedProductIds.Any(id => !string.IsNullOrEmpty(id) && string.Equals(id.Trim(), trimmedId, StringComparison.OrdinalIgnoreCase)))?.Code;
        }

        public async Task<bool> AssignProductToPromotionAsync(string productId, string? promoCode)
        {
            if (string.IsNullOrWhiteSpace(productId)) return false;
            var trimmedId = productId.Trim();
            var promos = await GetPromotionsAsync();
            bool modified = false;

            foreach (var p in promos)
            {
                if (p.AppliedProductIds == null) p.AppliedProductIds = new List<string>();

                if (!string.IsNullOrWhiteSpace(promoCode) && p.Code.Equals(promoCode.Trim(), StringComparison.OrdinalIgnoreCase))
                {
                    if (!p.AppliedProductIds.Any(id => string.Equals(id?.Trim(), trimmedId, StringComparison.OrdinalIgnoreCase)))
                    {
                        p.AppliedProductIds.Add(trimmedId);
                        modified = true;
                    }
                }
                else
                {
                    var existing = p.AppliedProductIds.FirstOrDefault(id => string.Equals(id?.Trim(), trimmedId, StringComparison.OrdinalIgnoreCase));
                    if (existing != null)
                    {
                        p.AppliedProductIds.Remove(existing);
                        modified = true;
                    }
                }
            }

            if (modified)
            {
                return await SaveListAsync(promos);
            }

            return true;
        }

        private async Task<bool> SaveListAsync(List<PromotionSetupModel> list)
        {
            try
            {
                var json = JsonSerializer.Serialize(list);
                await _js.InvokeVoidAsync("localStorage.setItem", StorageKey, json);
                LastError = null;
                return true;
            }
            catch (Exception ex)
            {
                LastError = ex.Message;
                return false;
            }
        }

        private List<PromotionSetupModel> GetDefaultSeedPromotions()
        {
            return new List<PromotionSetupModel>();
        }
    }
}
