using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using EBI.DM;
using Microsoft.EntityFrameworkCore;
using SenangRetails.Shared.ApiClient;
using SenangRetails.Shared.Data;
using SenangRetails.Shared.Data.Entities;
using SenangRetails.Shared.Models.DTOs;
using SenangRetails.Shared.Services.Connectivity;

namespace SenangRetails.Shared.Services.PaymentService
{
    public class PaymentService : IPaymentService
    {
        private readonly POSPaymentLineTypeAC _ac;
        private readonly AppState _appState;
        private readonly INetworkStatusService _network;

        public PaymentService(POSPaymentLineTypeAC ac, AppState appState, INetworkStatusService network)
        {
            _ac = ac;
            _appState = appState;
            _network = network;
        }

        public async Task<List<Doc_CashSales_POSPaymentLineTypeDM>> GetPaymentMethodsAsync(string branchId, string groupId, string customerId, bool includeInactive = false)
        {
            List<Doc_CashSales_POSPaymentLineTypeDM>? rawList = null;

            // 1. Attempt online fetch
            if (_network.IsInternetAvailable)
            {
                try
                {
                    var requestPayload = new POSPaymentLineTypeRequest
                    {
                        ID = string.IsNullOrEmpty(customerId) ? null : customerId,
                        BranchID = string.IsNullOrEmpty(branchId) ? null : branchId,
                        GroupID = string.IsNullOrEmpty(groupId) ? null : groupId
                    };

                    var response = await _ac.GetSystemControlledSalesSettlementTypeAsync(requestPayload);

                    if (response?.statusCode == 200 && response.result != null && response.result.Count > 0)
                    {
                        rawList = response.result;
                        await SavePaymentMethodsToSqliteAsync(rawList, branchId);
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[PaymentService] Online fetch failed: {ex.Message}. Falling back to SQLite cache.");
                }
            }

            // 2. Fallback to SQLite if online fetch failed or returned null
            if (rawList == null || rawList.Count == 0)
            {
                rawList = await LoadPaymentMethodsFromSqliteAsync(branchId);
            }

            // 3. Fallback to default POS payment methods if SQLite is also empty
            if (rawList == null || rawList.Count == 0)
            {
                rawList = GetDefaultOfflinePaymentMethods(groupId);
            }

            // 4. Apply filtering by group and customer
            var targetGroupId = groupId;
            var filteredList = rawList.Where(m =>
            {
                bool isVisibleInGroup = string.IsNullOrEmpty(targetGroupId) ||
                                        string.IsNullOrEmpty(m.VisibleInGroup) ||
                                        m.VisibleInGroup.Split(',')
                                            .Select(x => x.Trim())
                                            .Contains(targetGroupId, StringComparer.OrdinalIgnoreCase);

                if (m.POSPaymentTypeID == -1)
                {
                    if (_appState.SelectedCustomer == null || string.IsNullOrEmpty(_appState.SelectedCustomer.MasterAccountID))
                    {
                        return false;
                    }
                }

                return (includeInactive || m.Active) && isVisibleInGroup;
            }).ToList();

            return filteredList;
        }

        private async Task<List<Doc_CashSales_POSPaymentLineTypeDM>?> LoadPaymentMethodsFromSqliteAsync(string branchId)
        {
            try
            {
                using var db = new LocalAppDbContext();
                await LocalDatabaseInitializer.InitializeAsync(db);

                var key = string.IsNullOrEmpty(branchId) ? "default" : branchId;
                var cached = await db.LocalItemCatalogs.FirstOrDefaultAsync(x => x.BranchId == key);
                if (cached == null && key != "default")
                {
                    cached = await db.LocalItemCatalogs.FirstOrDefaultAsync(x => x.BranchId == "default");
                }
                if (cached == null)
                {
                    cached = await db.LocalItemCatalogs.OrderByDescending(x => x.LastUpdatedAtUtc).FirstOrDefaultAsync();
                }

                if (cached != null && !string.IsNullOrWhiteSpace(cached.PaymentMethodsJson))
                {
                    var list = JsonSerializer.Deserialize<List<Doc_CashSales_POSPaymentLineTypeDM>>(cached.PaymentMethodsJson);
                    if (list != null && list.Count > 0)
                    {
                        return list;
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[PaymentService] SQLite load error: {ex.Message}");
            }

            return null;
        }

        private async Task SavePaymentMethodsToSqliteAsync(List<Doc_CashSales_POSPaymentLineTypeDM> methods, string branchId)
        {
            if (methods == null || methods.Count == 0) return;

            try
            {
                using var db = new LocalAppDbContext();
                await LocalDatabaseInitializer.InitializeAsync(db);

                var key = string.IsNullOrEmpty(branchId) ? "default" : branchId;
                var existing = await db.LocalItemCatalogs.FirstOrDefaultAsync(x => x.BranchId == key);
                if (existing == null)
                {
                    existing = new LocalItemCatalogEntity
                    {
                        BranchId = key
                    };
                    db.LocalItemCatalogs.Add(existing);
                }

                existing.PaymentMethodsJson = JsonSerializer.Serialize(methods);
                existing.LastUpdatedAtUtc = DateTime.UtcNow;
                await db.SaveChangesAsync();

                if (key != "default")
                {
                    var defaultEntry = await db.LocalItemCatalogs.FirstOrDefaultAsync(x => x.BranchId == "default");
                    if (defaultEntry == null)
                    {
                        defaultEntry = new LocalItemCatalogEntity { BranchId = "default" };
                        db.LocalItemCatalogs.Add(defaultEntry);
                    }
                    defaultEntry.PaymentMethodsJson = existing.PaymentMethodsJson;
                    defaultEntry.LastUpdatedAtUtc = DateTime.UtcNow;
                    await db.SaveChangesAsync();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[PaymentService] SQLite save error: {ex.Message}");
            }
        }

        private List<Doc_CashSales_POSPaymentLineTypeDM> GetDefaultOfflinePaymentMethods(string groupId)
        {
            return new List<Doc_CashSales_POSPaymentLineTypeDM>
            {
                new Doc_CashSales_POSPaymentLineTypeDM
                {
                    POSPaymentTypeID = 1,
                    POSPaymentTypeName = "Cash",
                    Active = true,
                    VisibleInGroup = string.IsNullOrEmpty(groupId) ? "" : groupId
                },
                new Doc_CashSales_POSPaymentLineTypeDM
                {
                    POSPaymentTypeID = 2,
                    POSPaymentTypeName = "Credit Card",
                    Active = true,
                    VisibleInGroup = string.IsNullOrEmpty(groupId) ? "" : groupId
                },
                new Doc_CashSales_POSPaymentLineTypeDM
                {
                    POSPaymentTypeID = 3,
                    POSPaymentTypeName = "Debit Card",
                    Active = true,
                    VisibleInGroup = string.IsNullOrEmpty(groupId) ? "" : groupId
                },
                new Doc_CashSales_POSPaymentLineTypeDM
                {
                    POSPaymentTypeID = 4,
                    POSPaymentTypeName = "E-Wallet / QR Pay",
                    Active = true,
                    VisibleInGroup = string.IsNullOrEmpty(groupId) ? "" : groupId
                },
                new Doc_CashSales_POSPaymentLineTypeDM
                {
                    POSPaymentTypeID = 5,
                    POSPaymentTypeName = "Online Transfer",
                    Active = true,
                    VisibleInGroup = string.IsNullOrEmpty(groupId) ? "" : groupId
                }
            };
        }
    }
}
