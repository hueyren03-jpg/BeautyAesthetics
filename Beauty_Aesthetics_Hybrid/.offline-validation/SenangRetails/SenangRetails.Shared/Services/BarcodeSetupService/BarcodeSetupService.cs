using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.JSInterop;
using SenangRetails.Shared.Models.DTOs;

namespace SenangRetails.Shared.Services.BarcodeSetupService
{
    public class BarcodeSetupService : IBarcodeSetupService
    {
        private readonly IJSRuntime _js;
        private const string RulesStorageKey = "senang_barcode_reading_rules_list";
        private const string SetupStorageKey = "senang_barcode_reading_setup";

        public BarcodeSetupService(IJSRuntime js)
        {
            _js = js;
        }

        public async Task<List<BarcodeReadingSetupModel>> GetRulesAsync()
        {
            try
            {
                var json = await _js.InvokeAsync<string?>("localStorage.getItem", RulesStorageKey);
                if (json != null)
                {
                    if (string.IsNullOrWhiteSpace(json)) return new List<BarcodeReadingSetupModel>();
                    var list = JsonSerializer.Deserialize<List<BarcodeReadingSetupModel>>(json);
                    return list ?? new List<BarcodeReadingSetupModel>();
                }
            }
            catch
            {
            }

            var defaultRules = new List<BarcodeReadingSetupModel>
            {
                new BarcodeReadingSetupModel
                {
                    Prefix = "21",
                    BarcodeType = "BarCode",
                    SelectionType = "Barcode",
                    ItemCodeLength = 5,
                    QuantityLength = 5,
                    IsQuantityFixedAsOne = false,
                    PriceLength = 0,
                    DecimalPlace = 2,
                    ChecksumLength = 1,
                    BarcodeIncludesPrefix = false,
                    HasOverlappingBarcodeItem = false,
                    IsActive = true
                },
                new BarcodeReadingSetupModel
                {
                    Prefix = "22",
                    BarcodeType = "BarCode",
                    SelectionType = "Barcode",
                    ItemCodeLength = 5,
                    QuantityLength = 0,
                    IsQuantityFixedAsOne = true,
                    PriceLength = 5,
                    DecimalPlace = 2,
                    ChecksumLength = 1,
                    BarcodeIncludesPrefix = true,
                    HasOverlappingBarcodeItem = false,
                    IsActive = true
                }
            };

            await SaveRulesToStorage(defaultRules);
            return defaultRules;
        }

        public async Task<ParsedBarcodeResult> ParseBarcodeAsync(string scannedCode)
        {
            if (string.IsNullOrWhiteSpace(scannedCode))
            {
                return new ParsedBarcodeResult { Success = false, StatusMessage = "Empty barcode" };
            }

            var cleanCode = scannedCode.Trim();
            var allRules = await GetRulesAsync();
            var activeRules = allRules.Where(r => r.IsActive).OrderByDescending(r => r.Prefix.Length).ToList();

            foreach (var rule in activeRules)
            {
                if (string.IsNullOrEmpty(rule.Prefix) || !cleanCode.StartsWith(rule.Prefix, StringComparison.OrdinalIgnoreCase))
                    continue;

                int prefixLen = rule.Prefix.Length;
                int itemCodeStart = rule.BarcodeIncludesPrefix ? 0 : prefixLen;
                int itemCodeLen = rule.ItemCodeLength;

                if (cleanCode.Length < itemCodeStart + itemCodeLen)
                    continue;

                string extractedItemCode = cleanCode.Substring(itemCodeStart, itemCodeLen).Trim();
                int cursor = itemCodeStart + itemCodeLen;
                decimal quantity = 1;
                decimal? price = null;

                if (rule.IsQuantityFixedAsOne)
                {
                    quantity = 1;
                }
                else if (rule.QuantityLength > 0 && cleanCode.Length >= cursor + rule.QuantityLength)
                {
                    string qtyStr = cleanCode.Substring(cursor, rule.QuantityLength);
                    if (decimal.TryParse(qtyStr, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var rawQty))
                    {
                        decimal divisor = (decimal)Math.Pow(10, rule.DecimalPlace);
                        quantity = divisor > 0 ? rawQty / divisor : rawQty;
                    }
                    cursor += rule.QuantityLength;
                }

                if (rule.PriceLength > 0 && cleanCode.Length >= cursor + rule.PriceLength)
                {
                    string priceStr = cleanCode.Substring(cursor, rule.PriceLength);
                    if (decimal.TryParse(priceStr, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var rawPrice))
                    {
                        decimal divisor = (decimal)Math.Pow(10, rule.DecimalPlace);
                        price = divisor > 0 ? rawPrice / divisor : rawPrice;
                    }
                    cursor += rule.PriceLength;
                }

                return new ParsedBarcodeResult
                {
                    Success = true,
                    ItemCode = extractedItemCode,
                    Quantity = quantity > 0 ? quantity : 1,
                    Price = price,
                    MatchedRule = rule,
                    RawCode = cleanCode,
                    StatusMessage = $"Matched Prefix [{rule.Prefix}] ({rule.BarcodeType})"
                };
            }

            return new ParsedBarcodeResult
            {
                Success = false,
                RawCode = cleanCode,
                ItemCode = cleanCode,
                Quantity = 1,
                StatusMessage = "No matching barcode rule found"
            };
        }

        public async Task<bool> SaveRuleAsync(BarcodeReadingSetupModel rule, bool isNew)
        {
            try
            {
                var list = await GetRulesAsync();
                var existing = list.FirstOrDefault(r => r.Prefix.Equals(rule.Prefix, StringComparison.OrdinalIgnoreCase));
                if (existing != null)
                {
                    list.Remove(existing);
                }
                list.Add(rule);
                return await SaveRulesToStorage(list);
            }
            catch
            {
                return false;
            }
        }

        public async Task<bool> DeleteRuleAsync(string prefix)
        {
            try
            {
                var list = await GetRulesAsync();
                list.RemoveAll(r => r.Prefix.Equals(prefix, StringComparison.OrdinalIgnoreCase));
                return await SaveRulesToStorage(list);
            }
            catch
            {
                return false;
            }
        }

        private async Task<bool> SaveRulesToStorage(List<BarcodeReadingSetupModel> list)
        {
            try
            {
                var json = JsonSerializer.Serialize(list);
                await _js.InvokeVoidAsync("localStorage.setItem", RulesStorageKey, json);
                return true;
            }
            catch
            {
                return false;
            }
        }

        public async Task<BarcodeReadingSetupModel> GetSetupAsync()
        {
            var rules = await GetRulesAsync();
            return rules.FirstOrDefault() ?? new BarcodeReadingSetupModel();
        }

        public async Task<bool> SaveSetupAsync(BarcodeReadingSetupModel setup)
        {
            return await SaveRuleAsync(setup, false);
        }
    }
}
