using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.JSInterop;
using SenangRetails.Shared.Models.DTOs;

namespace SenangRetails.Shared.Services.CommissionSetupService
{
    public class CommissionSetupService : ICommissionSetupService
    {
        private readonly IJSRuntime _js;
        private const string StorageKey = "senang_commission_schemes";

        public CommissionSetupService(IJSRuntime js)
        {
            _js = js;
        }

        public async Task<List<CommissionSchemeModel>> GetSchemesAsync()
        {
            try
            {
                var json = await _js.InvokeAsync<string?>("localStorage.getItem", StorageKey);
                if (json != null)
                {
                    if (string.IsNullOrWhiteSpace(json)) return new List<CommissionSchemeModel>();
                    var list = JsonSerializer.Deserialize<List<CommissionSchemeModel>>(json);
                    return list ?? new List<CommissionSchemeModel>();
                }
            }
            catch
            {
            }

            var defaultSchemes = GetDefaultSeedSchemes();
            await SaveSchemesListAsync(defaultSchemes);
            return defaultSchemes;
        }

        public async Task<(decimal amount, string type)> GetCommissionForStaffAsync(string? schemeId, string inEvent, decimal unitPrice)
        {
            if (string.IsNullOrWhiteSpace(schemeId)) return (0m, "%");

            var scheme = await GetSchemeByIdAsync(schemeId);
            if (scheme == null || !scheme.IsActive || scheme.Lines == null || !scheme.Lines.Any())
                return (0m, "%");

            var targetEvent = string.IsNullOrWhiteSpace(inEvent) ? "Sales" : inEvent.Trim();
            var matchedLine = scheme.Lines.FirstOrDefault(l => l.InEvent.Equals(targetEvent, StringComparison.OrdinalIgnoreCase))
                           ?? scheme.Lines.FirstOrDefault(l => l.InEvent.Equals("Sales", StringComparison.OrdinalIgnoreCase))
                           ?? scheme.Lines.FirstOrDefault();

            if (matchedLine == null || matchedLine.NotForStaff)
                return (0m, "%");

            if (matchedLine.Type.Equals("Fixed Amount", StringComparison.OrdinalIgnoreCase))
            {
                return (matchedLine.AllocationAmt, "MYR");
            }
            else
            {
                var rate = matchedLine.SharingPercent > 0 ? matchedLine.SharingPercent : matchedLine.AllocationAmt;
                return (rate, "%");
            }
        }

        public async Task<CommissionSchemeModel?> GetSchemeByIdAsync(string id)
        {
            var schemes = await GetSchemesAsync();
            return schemes.FirstOrDefault(s => s.Id == id);
        }

        public async Task<bool> SaveSchemeAsync(CommissionSchemeModel scheme)
        {
            try
            {
                var schemes = await GetSchemesAsync();
                var index = schemes.FindIndex(s => s.Id == scheme.Id);
                if (index >= 0)
                {
                    schemes[index] = scheme;
                }
                else
                {
                    schemes.Insert(0, scheme);
                }

                return await SaveSchemesListAsync(schemes);
            }
            catch
            {
                return false;
            }
        }

        public async Task<bool> DeleteSchemeAsync(string id)
        {
            try
            {
                var schemes = await GetSchemesAsync();
                schemes.RemoveAll(s => s.Id == id);
                return await SaveSchemesListAsync(schemes);
            }
            catch
            {
                return false;
            }
        }

        private async Task<bool> SaveSchemesListAsync(List<CommissionSchemeModel> schemes)
        {
            try
            {
                var json = JsonSerializer.Serialize(schemes);
                await _js.InvokeVoidAsync("localStorage.setItem", StorageKey, json);
                return true;
            }
            catch
            {
                return false;
            }
        }

        private List<CommissionSchemeModel> GetDefaultSeedSchemes()
        {
            return new List<CommissionSchemeModel>
            {
                new CommissionSchemeModel
                {
                    Id = "SCHEME-001",
                    Name = "Standard Retail Commission Scheme",
                    IsActive = true,
                    Lines = new List<CommissionLineItemModel>
                    {
                        new CommissionLineItemModel
                        {
                            LineId = "LINE-001",
                            InEvent = "Sales",
                            Type = "Percentage",
                            Description = "Standard Product Sale Commission",
                            AllocationType = "Standard",
                            UseRetailPrice = true,
                            AllocationAmt = 0.00m,
                            ByRangeDetails = "0 - 100%",
                            SharingPercent = 5.00m,
                            IsBalance = false,
                            IsPaid = true,
                            NotForStaff = false,
                            DeductionType = "None",
                            XRange = "All",
                            DeductionAmt = 0.00m,
                            AllocationGroup = "Group A",
                            DiscountSetting = "Standard Discount"
                        },
                        new CommissionLineItemModel
                        {
                            LineId = "LINE-002",
                            InEvent = "Service",
                            Type = "Fixed Amount",
                            Description = "Therapist Treatment Incentive",
                            AllocationType = "Fixed Bonus",
                            UseRetailPrice = false,
                            AllocationAmt = 15.00m,
                            ByRangeDetails = "Fixed RM15",
                            SharingPercent = 0.00m,
                            IsBalance = false,
                            IsPaid = true,
                            NotForStaff = false,
                            DeductionType = "None",
                            XRange = "All",
                            DeductionAmt = 0.00m,
                            AllocationGroup = "Group B",
                            DiscountSetting = "No Discount"
                        }
                    }
                }
            };
        }
    }
}
