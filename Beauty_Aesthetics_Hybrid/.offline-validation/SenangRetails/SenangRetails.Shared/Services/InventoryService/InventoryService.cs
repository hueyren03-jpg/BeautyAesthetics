using EBI.DM;
using EBI.Enum;
using EBI.UC;
using Microsoft.JSInterop;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;
using SenangRetails.Shared.ApiClient;
using SenangRetails.Shared.Data;
using SenangRetails.Shared.Data.Entities;
using SenangRetails.Shared.Models.DTOs;
using SenangRetails.Shared.Models.Entities;
using SenangRetails.Shared.Services.Connectivity;

namespace SenangRetails.Shared.Services.InventoryService
{
    public class InventoryService : IInventoryService
    {
        private readonly InventoryAC _ac;
        private readonly AppState _appState;
        private readonly IJSRuntime _js;
        private readonly INetworkStatusService _network;
        public InventoryService(InventoryAC ac, AppState appState, IJSRuntime JS, INetworkStatusService network)
        {
            _ac = ac;
            _appState = appState;
            _js = JS;
            _network = network;
        }

        public async Task<Dictionary<string, List<InventoryDM>>?> GetCurrentBranchGroupedItemsAsync()
        {
            string branchId = _appState.SelectedBranchID;
            if (string.IsNullOrEmpty(branchId))
            {
                try
                {
                    branchId = await _js.InvokeAsync<string>("localStorage.getItem", "currentBranch");
                    if (!string.IsNullOrEmpty(branchId))
                        _appState.SelectedBranchID = branchId;
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error reading from localStorage: {ex.Message}");
                }
            }

            if (string.IsNullOrEmpty(branchId))
                return null;

            return await LoadGroupedItemsAsync(branchId);
        }

        public async Task<List<InventoryDM>?> LoadItemsAsync(string branchId = "")
        {
            if (_network.IsInternetAvailable)
            {
                try
                {
                    var response = await _ac.LoadProxyAsync(branchId);
                    if (response?.statusCode == 200 && response.result != null)
                    {
                        var items = response.result;
                        var semaphore = new System.Threading.SemaphoreSlim(5);
                        var packageTasks = items.Where(i => i.InventoryTypeID == 5).Select(async i =>
                        {
                            await semaphore.WaitAsync();
                            try
                            {
                                var fullPkg = await _ac.LoadFullAsync(i.MasterAccountID);
                                if (fullPkg?.objInventory != null) i.lstPackage = fullPkg.objInventory.lstPackage;
                            }
                            catch (Exception ex)
                            {
                                System.Diagnostics.Debug.WriteLine($"[InventoryService] Package load failed: {ex.Message}");
                            }
                            finally { semaphore.Release(); }
                        });
                        await Task.WhenAll(packageTasks);
                        await SaveItemsToSqliteAsync(items, branchId);
                        return items;
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[InventoryService] Online load failed: {ex.Message}");
                }
            }

            return await LoadItemsFromSqliteAsync(branchId);
        }

        private static async Task<List<InventoryDM>> LoadItemsFromSqliteAsync(string branchId)
        {
            try
            {
                using var db = new LocalAppDbContext();
                await LocalDatabaseInitializer.InitializeAsync(db);
                var key = string.IsNullOrWhiteSpace(branchId) ? "default" : branchId;
                var cached = await db.LocalItemCatalogs.AsNoTracking().FirstOrDefaultAsync(x => x.BranchId == key);
                if (cached == null && key != "default")
                    cached = await db.LocalItemCatalogs.AsNoTracking().FirstOrDefaultAsync(x => x.BranchId == "default");
                cached ??= await db.LocalItemCatalogs.AsNoTracking().OrderByDescending(x => x.LastUpdatedAtUtc).FirstOrDefaultAsync();
                return string.IsNullOrWhiteSpace(cached?.ItemsJson)
                    ? new()
                    : JsonSerializer.Deserialize<List<InventoryDM>>(cached.ItemsJson) ?? new();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[InventoryService] SQLite load failed: {ex.Message}");
                return new();
            }
        }

        private static async Task SaveItemsToSqliteAsync(List<InventoryDM> items, string branchId)
        {
            try
            {
                using var db = new LocalAppDbContext();
                await LocalDatabaseInitializer.InitializeAsync(db);
                var key = string.IsNullOrWhiteSpace(branchId) ? "default" : branchId;
                var json = JsonSerializer.Serialize(items);
                async Task Upsert(string id)
                {
                    var row = await db.LocalItemCatalogs.FirstOrDefaultAsync(x => x.BranchId == id);
                    if (row == null)
                    {
                        row = new LocalItemCatalogEntity { BranchId = id };
                        db.LocalItemCatalogs.Add(row);
                    }
                    row.ItemsJson = json;
                    row.LastUpdatedAtUtc = DateTime.UtcNow;
                }
                await Upsert(key);
                if (key != "default") await Upsert("default");
                await db.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[InventoryService] SQLite save failed: {ex.Message}");
            }
        }

        public async Task<InventoryDM?> LoadItemAsync(string masterAccountId)
        {
            if (_network.IsInternetAvailable)
            {
                try
                {
                    var response = await _ac.LoadRecordAsync(masterAccountId);
                    if (response?.statusCode == 200) return response.result;
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[InventoryService] Online item load failed: {ex.Message}");
                }
            }
            return (await LoadItemsFromSqliteAsync(_appState.SelectedBranchID))
                .FirstOrDefault(x => x.MasterAccountID == masterAccountId);
        }

        public async Task<(bool Success, string Message)> CreateItemAsync(Inventory model)
        {
            model.objInventory.SaveAction = EBI.Enum.EntityState.Added;
            model.objInventory.IsDirty = true;
            var response = await _ac.CreateAsync(model);
            if (response == null)
                return (false, "No response — check connection and login.");
            if (response.statusCode >= 200 && response.statusCode < 300)
                return (true, !string.IsNullOrWhiteSpace(response.message) ? response.message : "Item created successfully.");
            var errMsg = !string.IsNullOrWhiteSpace(response.message)
                ? response.message
                : $"Server returned code {response.statusCode}.";
            System.Diagnostics.Debug.WriteLine($"[InventoryService] CreateItem failed: code={response.statusCode} msg={response.message}");
            return (false, errMsg);
        }

        public async Task<(bool Success, string Message, string? NewId)> CreatePackageAsync(Inventory model)
        {
            model.objInventory.IsDirty = true;
            var response = await _ac.CreateAsync(model);
            if (response == null)
                return (false, "No response — check connection and login.", null);
            if (response.statusCode >= 200 && response.statusCode < 300)
                return (true, !string.IsNullOrWhiteSpace(response.message) ? response.message : "Package created successfully.", response.result?.Id);
            var errMsg = !string.IsNullOrWhiteSpace(response.message)
                ? response.message
                : $"Server returned code {response.statusCode}.";
            System.Diagnostics.Debug.WriteLine($"[InventoryService] CreatePackage failed: code={response.statusCode} msg={response.message}");
            return (false, errMsg, null);
        }

        public async Task<(bool Success, string Message, string? NewId)> SavePackageFullAsync(InventoryFullCreateRequest request)
        {
            var response = await _ac.CreatePackageFullAsync(request);
            if (response == null)
                return (false, "No response — check connection and login.", null);
            if (response.statusCode >= 200 && response.statusCode < 300)
                return (true, !string.IsNullOrWhiteSpace(response.message) ? response.message : "Saved.", response.result?.Id);
            return (false, !string.IsNullOrWhiteSpace(response.message) ? response.message : $"Server returned {response.statusCode}.", null);
        }

        public async Task<(bool Success, string Message)> UpdatePackageFullAsync(InventoryFullCreateRequest request)
        {
            var response = await _ac.UpdatePackageFullAsync(request);
            if (response == null)
                return (false, "No response — check connection and login.");
            if (response.statusCode >= 200 && response.statusCode < 300)
                return (true, !string.IsNullOrWhiteSpace(response.message) ? response.message : "Updated.");
            var errMsg = !string.IsNullOrWhiteSpace(response.message) ? response.message : $"Server returned {response.statusCode}.";
            System.Diagnostics.Debug.WriteLine($"[InventoryService] UpdatePackageFull failed: code={response.statusCode} msg={response.message}");
            return (false, errMsg);
        }

        public async Task<(bool Success, string Message)> UpdateItemAsync(Inventory model)
        {
            model.objInventory.SaveAction = EBI.Enum.EntityState.Changed;
            model.objInventory.IsDirty = true;
            var response = await _ac.UpdateAsync(model);
            if (response == null)
                return (false, "No response — check connection and login.");
            if (response.statusCode >= 200 && response.statusCode < 300)
                return (true, !string.IsNullOrWhiteSpace(response.message) ? response.message : "Item updated successfully.");
            var errMsg = !string.IsNullOrWhiteSpace(response.message)
                ? response.message
                : $"Server returned code {response.statusCode}.";
            System.Diagnostics.Debug.WriteLine($"[InventoryService] UpdateItem failed: code={response.statusCode} msg={response.message}");
            return (false, errMsg);
        }

        public async Task<string> UpdateRemarksPascalAsync(string masterAccountId, string accountName, int inventoryTypeID, string? itemGroupId, string remarks, string? unitOfMeasureID = null, string? unitOfMeasureName = null, decimal? pointToRedeem = null, bool? allowPointRedemption = null, decimal? stockReorderLevel = null)
        {
            var resp = await _ac.UpdateFieldsFullPayloadAsync(masterAccountId, remarks, unitOfMeasureID, unitOfMeasureName, pointToRedeem, allowPointRedemption, stockReorderLevel);
            return resp?.message ?? "null";
        }

        public async Task<(bool Success, string Message)> UpdateInventoryDirectAsync(InventoryCreateModel model)
        {
            model.saveAction = "Changed";
            model.isDirty = true;
            var response = await _ac.UpdateDirectAsync(model);
            if (response == null) return (false, "No response.");
            if (response.statusCode >= 200 && response.statusCode < 300) return (true, "");
            System.Diagnostics.Debug.WriteLine($"[InventoryService] UpdateInventoryDirect failed: {response.statusCode} {response.message}");
            return (false, response.message ?? $"Server returned {response.statusCode}.");
        }

        public async Task<(bool Success, string Message)> DeleteItemAsync(string masterAccountId)
        {
            var response = await _ac.DeleteAsync(masterAccountId);
            if (response?.statusCode == 200)
                return (true, response.message ?? "Item deleted successfully.");
            return (false, response?.message ?? "Failed to delete item.");
        }

        public async Task<Inventory?> LoadFullPackageAsync(string masterAccountId)
        {
            return await _ac.LoadFullAsync(masterAccountId);
        }

        public async Task<InventoryFullLoadDetail?> LoadFullPackageDetailAsync(string masterAccountId)
        {
            return await _ac.LoadFullDetailAsync(masterAccountId);
        }

        public async Task<Dictionary<string, List<InventoryDM>>?> LoadGroupedItemsAsync(string branchId = "")
        {
            if (_network.IsInternetAvailable)
            {
                try
                {
                    var response = await _ac.LoadProxyByItemGroupAsync(branchId);
                    if (response?.statusCode == 200 && response.result != null)
                    {
                        await SaveItemsToSqliteAsync(response.result.Values.SelectMany(x => x).ToList(), branchId);
                        return response.result;
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[InventoryService] Grouped load failed: {ex.Message}");
                }
            }

            var items = await LoadItemsFromSqliteAsync(branchId);
            return items.GroupBy(x => string.IsNullOrWhiteSpace(x.ItemGroupName) ? "Uncategorized" : x.ItemGroupName)
                .ToDictionary(x => x.Key, x => x.ToList());
        }

        public async Task<List<LowStockItem>?> GetStockBelowReorderPointAsync()
        {
            var response = await _ac.GetStockBelowReorderPoint();
            if (response?.statusCode == 200)
                return response.result;
            return null;
        }

        public async Task<Dictionary<string, StockBalanceItem>?> GetStockBalanceByBranchAndByItemAsync(StockBalanceRequest request)
        {
            var response = await _ac.GetStockBalanceByBranchAndByItem(request);
            if (response?.statusCode == 200)
                return response.result;
            return null;
        }

        public async Task<bool> CreateStockInGRN(Doc_Stock_GRN request)
        {
            var response = await _ac.DocStockGRNCreateRecord(request);
            if (response.statusCode == 200)
            {
                return true;
            }
            return false;
        }

        public async Task<List<InventoryMovement_PendingAcceptDM>?> GetPendingAcceptDocumentByBranchIdAsync()
        {
            var response = await _ac.GetPendingAcceptDocumentByBranchId();
            if (response?.statusCode == 200)
            {
                return response.result;
            }
            return null;
        }

        public async Task<List<InventoryMovement_PendingAcceptDM>?> GetPendingAcceptDocumentDetailsAsync(string documentId)
        {
            var response = await _ac.GetPendingAcceptDocumentDetails(documentId);
            if (response?.statusCode == 200)
            {
                return response.result;
            }
            return null;
        }

        public async Task<ApiResponseRoot<string>?> AcceptStockInAsync(string documentId)
        {
            return await _ac.AcceptStockIn(documentId);
        }
    }
}
