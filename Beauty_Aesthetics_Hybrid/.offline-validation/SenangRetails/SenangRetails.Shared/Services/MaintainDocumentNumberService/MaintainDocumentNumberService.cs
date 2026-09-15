using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.JSInterop;
using SenangRetails.Shared.Models.DTOs;

namespace SenangRetails.Shared.Services.MaintainDocumentNumberService
{
    public class MaintainDocumentNumberService : IMaintainDocumentNumberService
    {
        private readonly IJSRuntime _js;
        private const string StorageKey = "senang_maintain_doc_numbers";

        public MaintainDocumentNumberService(IJSRuntime js)
        {
            _js = js;
        }

        public async Task<List<MaintainDocumentNumberModel>> GetDocumentNumbersAsync(string? category = null)
        {
            try
            {
                var json = await _js.InvokeAsync<string?>("localStorage.getItem", StorageKey);
                if (!string.IsNullOrWhiteSpace(json))
                {
                    var list = JsonSerializer.Deserialize<List<MaintainDocumentNumberModel>>(json);
                    if (list != null && list.Any())
                    {
                        if (!string.IsNullOrEmpty(category))
                        {
                            return list.Where(x => x.Category.Equals(category, StringComparison.OrdinalIgnoreCase)).ToList();
                        }
                        return list;
                    }
                }
            }
            catch
            {
            }

            var seedList = GetSeedData();
            await SaveDocumentNumbersAsync(seedList);

            if (!string.IsNullOrEmpty(category))
            {
                return seedList.Where(x => x.Category.Equals(category, StringComparison.OrdinalIgnoreCase)).ToList();
            }
            return seedList;
        }

        public async Task<bool> SaveDocumentNumbersAsync(List<MaintainDocumentNumberModel> list)
        {
            try
            {
                // Get all existing items from storage to update correctly across categories
                var json = await _js.InvokeAsync<string?>("localStorage.getItem", StorageKey);
                List<MaintainDocumentNumberModel> fullList = new();
                if (!string.IsNullOrWhiteSpace(json))
                {
                    fullList = JsonSerializer.Deserialize<List<MaintainDocumentNumberModel>>(json) ?? new();
                }
                else
                {
                    fullList = GetSeedData();
                }

                // Update items in fullList from incoming list
                foreach (var item in list)
                {
                    var existing = fullList.FirstOrDefault(x => x.DocType == item.DocType && x.Category == item.Category);
                    if (existing != null)
                    {
                        existing.Prefix = item.Prefix;
                        existing.LastNumberUsed = item.LastNumberUsed;
                        existing.CharacterCount = item.CharacterCount;
                    }
                    else
                    {
                        fullList.Add(item);
                    }
                }

                var outputJson = JsonSerializer.Serialize(fullList);
                await _js.InvokeVoidAsync("localStorage.setItem", StorageKey, outputJson);
                return true;
            }
            catch
            {
                return false;
            }
        }

        private List<MaintainDocumentNumberModel> GetSeedData()
        {
            return new List<MaintainDocumentNumberModel>
            {
                // --- DOCUMENT TYPES ---
                new MaintainDocumentNumberModel { DocType = 0, Category = "Document", Name = "InvalidDocument", Prefix = "", LastNumberUsed = 1, CharacterCount = 15 },
                new MaintainDocumentNumberModel { DocType = 1, Category = "Document", Name = "SalesQuotation", Prefix = "SQ", LastNumberUsed = 1, CharacterCount = 15 },
                new MaintainDocumentNumberModel { DocType = 2, Category = "Document", Name = "InventoryAdjustment", Prefix = "IA", LastNumberUsed = 1, CharacterCount = 15 },
                new MaintainDocumentNumberModel { DocType = 3, Category = "Document", Name = "SalesOrder", Prefix = "SO", LastNumberUsed = 1, CharacterCount = 15 },
                new MaintainDocumentNumberModel { DocType = 4, Category = "Document", Name = "Invoice", Prefix = "INV", LastNumberUsed = 1, CharacterCount = 15 },
                new MaintainDocumentNumberModel { DocType = 6, Category = "Document", Name = "Customer Debit Note", Prefix = "DN", LastNumberUsed = 1, CharacterCount = 15 },
                new MaintainDocumentNumberModel { DocType = 7, Category = "Document", Name = "Customer Credit Note", Prefix = "CN", LastNumberUsed = 4, CharacterCount = 15 },
                new MaintainDocumentNumberModel { DocType = 8, Category = "Document", Name = "Purchase Order", Prefix = "HQPO", LastNumberUsed = 1, CharacterCount = 15 },
                new MaintainDocumentNumberModel { DocType = 10, Category = "Document", Name = "Journal Entry", Prefix = "JN", LastNumberUsed = 1, CharacterCount = 15 },
                new MaintainDocumentNumberModel { DocType = 13, Category = "Document", Name = "Supplier Debit Note", Prefix = "DN", LastNumberUsed = 1, CharacterCount = 15 },
                new MaintainDocumentNumberModel { DocType = 14, Category = "Document", Name = "Supplier Credit Note", Prefix = "CN", LastNumberUsed = 1, CharacterCount = 15 },
                new MaintainDocumentNumberModel { DocType = 15, Category = "Document", Name = "Stock Transfer", Prefix = "GT", LastNumberUsed = 1, CharacterCount = 15 },
                new MaintainDocumentNumberModel { DocType = 16, Category = "Document", Name = "Cash Purchase", Prefix = "HQCP", LastNumberUsed = 1, CharacterCount = 15 },
                new MaintainDocumentNumberModel { DocType = 17, Category = "Document", Name = "Fund Transfer", Prefix = "FTR", LastNumberUsed = 1, CharacterCount = 15 },
                new MaintainDocumentNumberModel { DocType = 19, Category = "Document", Name = "Deposit", Prefix = "DEP", LastNumberUsed = 1, CharacterCount = 15 },
                new MaintainDocumentNumberModel { DocType = 20, Category = "Document", Name = "Credit Sales Finance Charge", Prefix = "INT", LastNumberUsed = 1, CharacterCount = 15 },
                new MaintainDocumentNumberModel { DocType = 23, Category = "Document", Name = "Customer Bad Debt", Prefix = "BD", LastNumberUsed = 1, CharacterCount = 15 },
                new MaintainDocumentNumberModel { DocType = 24, Category = "Document", Name = "Customer Credit Allocation", Prefix = "AL", LastNumberUsed = 1, CharacterCount = 15 },
                new MaintainDocumentNumberModel { DocType = 25, Category = "Document", Name = "Cash Book Payment", Prefix = "CBP", LastNumberUsed = 1, CharacterCount = 15 },
                new MaintainDocumentNumberModel { DocType = 26, Category = "Document", Name = "Cash Book Receipt", Prefix = "CBR", LastNumberUsed = 1, CharacterCount = 15 },
                new MaintainDocumentNumberModel { DocType = 30, Category = "Document", Name = "Bank Reconciliation", Prefix = "BR", LastNumberUsed = 1, CharacterCount = 15 },
                new MaintainDocumentNumberModel { DocType = 31, Category = "Document", Name = "Inventory Adjustment", Prefix = "HQIA", LastNumberUsed = 1, CharacterCount = 15 },
                new MaintainDocumentNumberModel { DocType = 33, Category = "Document", Name = "Point Redemption", Prefix = "PR", LastNumberUsed = 1, CharacterCount = 15 },
                new MaintainDocumentNumberModel { DocType = 34, Category = "Document", Name = "Customer Point", Prefix = "CP", LastNumberUsed = 1, CharacterCount = 15 },
                new MaintainDocumentNumberModel { DocType = 37, Category = "Document", Name = "Delivery Order", Prefix = "DO", LastNumberUsed = 1, CharacterCount = 15 },
                new MaintainDocumentNumberModel { DocType = 39, Category = "Document", Name = "Stock Return Credit Note", Prefix = "CN", LastNumberUsed = 1, CharacterCount = 15 },
                new MaintainDocumentNumberModel { DocType = 42, Category = "Document", Name = "Academy Invoice", Prefix = "AI", LastNumberUsed = 1, CharacterCount = 15 },
                new MaintainDocumentNumberModel { DocType = 45, Category = "Document", Name = "Instalment", Prefix = "IST", LastNumberUsed = 0, CharacterCount = 15 },
                new MaintainDocumentNumberModel { DocType = 47, Category = "Document", Name = "Instalment Receipt", Prefix = "IR", LastNumberUsed = 1, CharacterCount = 15 },
                new MaintainDocumentNumberModel { DocType = 49, Category = "Document", Name = "Academy Enrolment", Prefix = "E", LastNumberUsed = 1, CharacterCount = 15 },
                new MaintainDocumentNumberModel { DocType = 49, Category = "Document", Name = "GST Adjustment", Prefix = "GAJ", LastNumberUsed = 1, CharacterCount = 15 },
                new MaintainDocumentNumberModel { DocType = 50, Category = "Document", Name = "Service Job Sheet", Prefix = "JS", LastNumberUsed = 1, CharacterCount = 15 },
                new MaintainDocumentNumberModel { DocType = 51, Category = "Document", Name = "GRN", Prefix = "GRN", LastNumberUsed = 1, CharacterCount = 15 },
                new MaintainDocumentNumberModel { DocType = 53, Category = "Document", Name = "POSReceipt", Prefix = "POSR", LastNumberUsed = 1, CharacterCount = 15 },
                new MaintainDocumentNumberModel { DocType = 57, Category = "Document", Name = "GIN", Prefix = "GI", LastNumberUsed = 1, CharacterCount = 15 },
                new MaintainDocumentNumberModel { DocType = 58, Category = "Document", Name = "Purchase Request", Prefix = "PR", LastNumberUsed = 1, CharacterCount = 15 },

                // --- ACCOUNT TYPES ---
                new MaintainDocumentNumberModel { DocType = 1, Category = "Account", Name = "Financial Account", Prefix = "F", LastNumberUsed = 1, CharacterCount = 10 },
                new MaintainDocumentNumberModel { DocType = 2, Category = "Account", Name = "Bank Account", Prefix = "BNK", LastNumberUsed = 1, CharacterCount = 10 },
                new MaintainDocumentNumberModel { DocType = 3, Category = "Account", Name = "Customer", Prefix = "CA", LastNumberUsed = 8, CharacterCount = 10 },
                new MaintainDocumentNumberModel { DocType = 4, Category = "Account", Name = "Inventory", Prefix = "STK", LastNumberUsed = 17, CharacterCount = 10 },
                new MaintainDocumentNumberModel { DocType = 5, Category = "Account", Name = "Job Account", Prefix = "JB", LastNumberUsed = 1, CharacterCount = 10 },
                new MaintainDocumentNumberModel { DocType = 6, Category = "Account", Name = "Employee", Prefix = "HR", LastNumberUsed = 6, CharacterCount = 10 },
                new MaintainDocumentNumberModel { DocType = 7, Category = "Account", Name = "Sales Tax Authority", Prefix = "TAX", LastNumberUsed = 1, CharacterCount = 10 },
                new MaintainDocumentNumberModel { DocType = 8, Category = "Account", Name = "Creditor", Prefix = "CR", LastNumberUsed = 1, CharacterCount = 10 },
                new MaintainDocumentNumberModel { DocType = 9, Category = "Account", Name = "Student", Prefix = "S", LastNumberUsed = 8, CharacterCount = 10 }
            };
        }
    }
}
