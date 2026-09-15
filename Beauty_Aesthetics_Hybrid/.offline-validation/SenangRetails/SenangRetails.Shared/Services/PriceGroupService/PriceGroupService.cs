using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.JSInterop;
using SenangRetails.Shared.Models.DTOs;

namespace SenangRetails.Shared.Services.PriceGroupService
{
    public class PriceGroupService : IPriceGroupService
    {
        private readonly IJSRuntime _js;
        private const string StorageKey = "senang_price_groups_data";
        private const string MapStorageKey = "senang_product_pricegroup_map";

        private static readonly List<PriceGroupModel> SeedGroups = new()
        {
            new PriceGroupModel
            {
                Code = "PG-VIP",
                Name = "VIP Member Price Group",
                Price = 18.00m,
                MlmCommRate = 5.0m,
                MlmSalesTarget = 1000.0m,
                VisibleBranchIds = new List<string> { "HQ", "B01" },
                AppliedProductIds = new List<string>()
            },
            new PriceGroupModel
            {
                Code = "PG-REG",
                Name = "Standard Retail Price Group",
                Price = 25.00m,
                MlmCommRate = 2.0m,
                MlmSalesTarget = 500.0m,
                VisibleBranchIds = new List<string> { "HQ", "B01", "B02" },
                AppliedProductIds = new List<string>()
            }
        };

        public PriceGroupService(IJSRuntime js)
        {
            _js = js;
        }

        public async Task<List<PriceGroupModel>> GetPriceGroupsAsync()
        {
            try
            {
                var json = await _js.InvokeAsync<string?>("localStorage.getItem", StorageKey);
                if (json != null)
                {
                    if (string.IsNullOrWhiteSpace(json)) return new List<PriceGroupModel>();
                    var items = JsonSerializer.Deserialize<List<PriceGroupModel>>(json);
                    return items ?? new List<PriceGroupModel>();
                }
            }
            catch
            {
            }
            return SeedGroups.ToList();
        }

        public async Task<PriceGroupModel?> GetPriceGroupAsync(string code)
        {
            if (string.IsNullOrWhiteSpace(code)) return null;
            var groups = await GetPriceGroupsAsync();
            return groups.FirstOrDefault(g => g.Code.Equals(code, StringComparison.OrdinalIgnoreCase));
        }

        public async Task<PriceGroupModel?> GetPriceGroupByProductIdAsync(string productId)
        {
            if (string.IsNullOrWhiteSpace(productId)) return null;
            var trimmedId = productId.Trim();
            var groups = await GetPriceGroupsAsync();
            return groups.FirstOrDefault(g => g.AppliedProductIds != null && g.AppliedProductIds.Any(id => !string.IsNullOrEmpty(id) && string.Equals(id.Trim(), trimmedId, StringComparison.OrdinalIgnoreCase)));
        }

        public async Task<string?> GetAssignedPriceGroupCodeAsync(string productId)
        {
            if (string.IsNullOrWhiteSpace(productId)) return null;
            var pg = await GetPriceGroupByProductIdAsync(productId);
            return pg?.Code;
        }

        public async Task<decimal?> GetEffectiveProductPriceAsync(string productId, decimal originalPrice)
        {
            if (string.IsNullOrWhiteSpace(productId)) return originalPrice;

            var pg = await GetPriceGroupByProductIdAsync(productId);
            if (pg != null && pg.Price > 0)
            {
                return pg.Price;
            }

            return originalPrice;
        }

        public async Task<bool> SavePriceGroupAsync(PriceGroupModel group, bool isNew)
        {
            var groups = await GetPriceGroupsAsync();

            // Ensure mutual exclusivity: remove assigned products from all other groups
            if (group.AppliedProductIds != null && group.AppliedProductIds.Any())
            {
                var assignedIds = group.AppliedProductIds
                    .Where(id => !string.IsNullOrWhiteSpace(id))
                    .Select(id => id.Trim())
                    .ToHashSet(StringComparer.OrdinalIgnoreCase);

                foreach (var other in groups)
                {
                    if (!string.Equals(other.Code, group.Code, StringComparison.OrdinalIgnoreCase) && other.AppliedProductIds != null)
                    {
                        other.AppliedProductIds.RemoveAll(id => !string.IsNullOrWhiteSpace(id) && assignedIds.Contains(id.Trim()));
                    }
                }
            }

            if (isNew)
            {
                if (groups.Any(g => g.Code.Equals(group.Code, StringComparison.OrdinalIgnoreCase)))
                    return false; // Code already exists

                groups.Add(group);
            }
            else
            {
                var index = groups.FindIndex(g => g.Code.Equals(group.Code, StringComparison.OrdinalIgnoreCase));
                if (index >= 0)
                {
                    groups[index] = group;
                }
                else
                {
                    groups.Add(group);
                }
            }

            return await SaveListAsync(groups);
        }

        public async Task<bool> DeletePriceGroupAsync(string code)
        {
            var groups = await GetPriceGroupsAsync();
            groups.RemoveAll(g => g.Code.Equals(code, StringComparison.OrdinalIgnoreCase));
            return await SaveListAsync(groups);
        }

        public async Task<bool> AssignProductToPriceGroupAsync(string productId, string? priceGroupCode)
        {
            if (string.IsNullOrWhiteSpace(productId)) return false;
            var trimmedId = productId.Trim();

            var groups = await GetPriceGroupsAsync();
            bool modified = false;

            foreach (var g in groups)
            {
                if (g.AppliedProductIds == null) g.AppliedProductIds = new List<string>();

                if (!string.IsNullOrWhiteSpace(priceGroupCode) && g.Code.Equals(priceGroupCode.Trim(), StringComparison.OrdinalIgnoreCase))
                {
                    if (!g.AppliedProductIds.Any(id => string.Equals(id?.Trim(), trimmedId, StringComparison.OrdinalIgnoreCase)))
                    {
                        g.AppliedProductIds.Add(trimmedId);
                        modified = true;
                    }
                }
                else
                {
                    var existing = g.AppliedProductIds.FirstOrDefault(id => string.Equals(id?.Trim(), trimmedId, StringComparison.OrdinalIgnoreCase));
                    if (existing != null)
                    {
                        g.AppliedProductIds.Remove(existing);
                        modified = true;
                    }
                }
            }

            if (modified)
            {
                return await SaveListAsync(groups);
            }

            return true;
        }

        private async Task<Dictionary<string, string>> GetProductMapAsync()
        {
            try
            {
                var json = await _js.InvokeAsync<string?>("localStorage.getItem", MapStorageKey);
                if (!string.IsNullOrWhiteSpace(json))
                {
                    var map = JsonSerializer.Deserialize<Dictionary<string, string>>(json);
                    if (map != null) return map;
                }
            }
            catch
            {
            }
            return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        }

        private async Task<bool> SaveProductMapAsync(Dictionary<string, string> map)
        {
            try
            {
                var json = JsonSerializer.Serialize(map);
                await _js.InvokeVoidAsync("localStorage.setItem", MapStorageKey, json);
                return true;
            }
            catch
            {
                return false;
            }
        }

        private async Task<bool> SaveListAsync(List<PriceGroupModel> list)
        {
            try
            {
                var json = JsonSerializer.Serialize(list);
                await _js.InvokeVoidAsync("localStorage.setItem", StorageKey, json);
                return true;
            }
            catch
            {
                return false;
            }
        }
    }
}
