using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using EBI.DM;
using Microsoft.EntityFrameworkCore;
using SenangRetails.Shared.Data;
using SenangRetails.Shared.Data.Entities;
using SenangRetails.Shared.Models.DTOs;
using SenangRetails.Shared.Services.InventoryService;
using SenangRetails.Shared.Services.SupportingTableService;

namespace SenangRetails.Shared.Services
{
    /// <summary>
    /// Hybrid in-memory and SQLite cache for inventory items and categories.
    /// Loads immediately from memory or local SQLite when offline.
    /// Refreshes silently in the background when online.
    /// </summary>
    public class ProductCacheService
    {
        private readonly IInventoryService _inventoryService;
        private readonly ISupportingTableService _supportingTableService;

        private bool _isRefreshing;
        private int _itemsVersion = 0;
        private int _categoriesVersion = 0;
        private readonly Dictionary<string, InventoryDM> _localPatches = new();
        private readonly Dictionary<string, string> _imageCache = new();

        public List<InventoryDM>? Items { get; private set; }
        public List<SupportingTableItem>? Categories { get; private set; }
        public bool HasCache => Items != null && Items.Count > 0;

        /// <summary>Raised on a background thread when a silent refresh completes.</summary>
        public event Action? OnCacheUpdated;

        public ProductCacheService(IInventoryService inventoryService, ISupportingTableService supportingTableService)
        {
            _inventoryService = inventoryService;
            _supportingTableService = supportingTableService;
        }

        /// <summary>
        /// Ensures items are loaded, first checking memory, then local SQLite, then online API.
        /// </summary>
        public async Task<List<InventoryDM>?> EnsureLoadedAsync(string branchId = "")
        {
            if (Items != null && Items.Count > 0)
            {
                return Items;
            }

            // 1. Try loading from SQLite (Instant offline load)
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

                if (cached != null && !string.IsNullOrWhiteSpace(cached.ItemsJson))
                {
                    var sqliteItems = JsonSerializer.Deserialize<List<InventoryDM>>(cached.ItemsJson);
                    if (sqliteItems != null && sqliteItems.Count > 0)
                    {
                        ApplyLocalPatches(sqliteItems);
                        Items = sqliteItems;

                        if (!string.IsNullOrWhiteSpace(cached.CategoriesJson))
                        {
                            var cats = JsonSerializer.Deserialize<List<SupportingTableItem>>(cached.CategoriesJson);
                            if (cats != null) Categories = cats;
                        }

                        Console.WriteLine($"[ProductCacheService] Loaded {Items.Count} items from SQLite offline cache.");
                        return Items;
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ProductCacheService] SQLite load error: {ex.Message}");
            }

            // 2. Fallback to online API fetch if SQLite is empty
            try
            {
                var onlineItems = await _inventoryService.LoadItemsAsync(branchId);
                if (onlineItems == null || onlineItems.Count == 0)
                {
                    onlineItems = await _inventoryService.LoadItemsAsync("");
                }

                if (onlineItems != null && onlineItems.Count > 0)
                {
                    SetItems(onlineItems, branchId);
                    return onlineItems;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ProductCacheService] Online fetch error: {ex.Message}");
            }

            return Items;
        }

        /// <summary>
        /// Store fresh items and persist to SQLite in background.
        /// </summary>
        public void SetItems(List<InventoryDM> items, string branchId = "")
        {
            _itemsVersion++;
            ApplyLocalPatches(items);
            Items = items;

            _ = SaveToSqliteAsync(items, Categories, branchId);
        }

        public void PatchCachedItem(string masterAccountId, Action<InventoryDM> patch)
        {
            if (Items == null) return;
            var item = Items.FirstOrDefault(x => x.MasterAccountID == masterAccountId);
            if (item == null) return;
            patch(item);
            _localPatches[masterAccountId] = item;
            _itemsVersion++;

            _ = SaveToSqliteAsync(Items, Categories);
        }

        public void RemoveLocalPatch(string masterAccountId)
        {
            _localPatches.Remove(masterAccountId);
        }

        public void ClearLocalPatches()
        {
            _localPatches.Clear();
        }

        public void StoreImage(string masterAccountId, string base64DataUri)
        {
            if (!string.IsNullOrEmpty(masterAccountId) && !string.IsNullOrEmpty(base64DataUri))
                _imageCache[masterAccountId] = base64DataUri;
        }

        public string? GetImage(string masterAccountId)
        {
            if (string.IsNullOrEmpty(masterAccountId)) return null;
            return _imageCache.TryGetValue(masterAccountId, out var v) ? v : null;
        }

        public void RemoveImage(string masterAccountId)
        {
            if (!string.IsNullOrEmpty(masterAccountId))
                _imageCache.Remove(masterAccountId);
        }

        public void SetCategories(List<SupportingTableItem> categories, string branchId = "")
        {
            _categoriesVersion++;
            Categories = categories;

            _ = SaveToSqliteAsync(Items, categories, branchId);
        }

        public void ClearAll()
        {
            Items = null;
            Categories = null;
            _localPatches.Clear();
            _itemsVersion++;
        }

        public void TriggerBackgroundRefresh()
        {
            if (_isRefreshing) return;
            _ = DoBackgroundRefreshAsync();
        }

        private async Task DoBackgroundRefreshAsync()
        {
            _isRefreshing = true;
            try
            {
                await Task.WhenAll(FetchItemsAsync(), FetchCategoriesAsync());
                OnCacheUpdated?.Invoke();
            }
            catch
            {
            }
            finally
            {
                _isRefreshing = false;
            }
        }

        private async Task FetchItemsAsync()
        {
            try
            {
                int capturedVersion = _itemsVersion;
                var result = await _inventoryService.LoadItemsAsync("");
                if (result != null && result.Count > 0 && _itemsVersion == capturedVersion)
                {
                    ApplyLocalPatches(result);
                    Items = result;
                    _ = SaveToSqliteAsync(result, Categories);
                }
            }
            catch
            {
            }
        }

        private void ApplyLocalPatches(List<InventoryDM> list)
        {
            if (_localPatches.Count == 0) return;
            for (int i = 0; i < list.Count; i++)
            {
                var id = list[i].MasterAccountID;
                if (id != null && _localPatches.TryGetValue(id, out var patch))
                    list[i] = patch;
            }
        }

        private async Task FetchCategoriesAsync()
        {
            try
            {
                int capturedVersion = _categoriesVersion;
                var result = await _supportingTableService.LoadListByTypeAsync(4);
                if (result != null && _categoriesVersion == capturedVersion)
                {
                    Categories = result;
                    _ = SaveToSqliteAsync(Items, result);
                }
            }
            catch
            {
            }
        }

        private async Task SaveToSqliteAsync(List<InventoryDM>? items, List<SupportingTableItem>? categories, string branchId = "")
        {
            if (items == null || items.Count == 0) return;

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

                existing.ItemsJson = JsonSerializer.Serialize(items);
                if (categories != null)
                {
                    existing.CategoriesJson = JsonSerializer.Serialize(categories);
                }
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
                    defaultEntry.ItemsJson = existing.ItemsJson;
                    if (categories != null) defaultEntry.CategoriesJson = existing.CategoriesJson;
                    defaultEntry.LastUpdatedAtUtc = DateTime.UtcNow;
                    await db.SaveChangesAsync();
                }

                Console.WriteLine($"[ProductCacheService] Saved {items.Count} items to SQLite for key '{key}' and 'default'.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ProductCacheService] SQLite save error: {ex.Message}");
            }
        }
    }
}
