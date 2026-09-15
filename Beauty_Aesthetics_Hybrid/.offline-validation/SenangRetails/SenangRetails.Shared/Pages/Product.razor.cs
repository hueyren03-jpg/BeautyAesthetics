using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using EBI.DM;
using EBI.Enum;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.JSInterop;
using SenangRetails.Shared.Services.FilePickerService;
using SenangRetails.Shared.ApiClient;
using SenangRetails.Shared.Model;
using SenangRetails.Shared.Models;
using SenangRetails.Shared.Models.DTOs;
using SenangRetails.Shared.Services;
using SenangRetails.Shared.Services.AuthService;
using SenangRetails.Shared.Services.BranchService;
using SenangRetails.Shared.Services.FileDownloadService;
using SenangRetails.Shared.Services.InventoryService;
using SenangRetails.Shared.Services.MembershipTypeService;
using SenangRetails.Shared.Services.NotificationService;
using SenangRetails.Shared.Services.SupportingTableService;

namespace SenangRetails.Shared.Pages
{
    public partial class Product : BasePage, IDisposable
    {
        [Inject]
        private IJSRuntime JS { get; set; } = default!;
        [Inject]
        private IInventoryService InventoryService { get; set; } = default!;
        [Inject]
        private ISupportingTableService SupportingTableService { get; set; } = default!;
        [Inject]
        private ProductCacheService CacheService { get; set; } = default!;
        [Inject]
        private IStoreTokenService StoreTokenService { get; set; } = default!;
        [Inject]
        private AppState AppState { get; set; } = default!;
        [Inject]
        private LocalImageCacheService LocalImageCache { get; set; } = default!;
        [Inject]
        private INotificationService NotificationSvc { get; set; } = default!;
        [Inject]
        private IMembershipTypeService MembershipTypeService { get; set; } = default!;
        [Inject]
        private IFileDownloadService FileDownloadService { get; set; } = default!;
        [Inject]
        private IFilePickerService FilePickerService { get; set; } = default!;
        [Inject]
        private IFormFactor FormFactor { get; set; } = default!;
        [Inject]
        private IBranchService BranchService { get; set; } = default!;
        [Inject] private NavigationManager Navigation { get; set; } = default!;
        [Inject] private SenangRetails.Shared.Services.PriceGroupService.IPriceGroupService PriceGroupService { get; set; } = default!;
        [Inject] private SenangRetails.Shared.Services.PromotionSetupService.IPromotionSetupService PromotionSetupService { get; set; } = default!;

        private List<PriceGroupModel> availablePriceGroups = new();
        private string selectedPriceGroupCode = "";
        private decimal _originalSalesPrice = 0;

        private List<PromotionSetupModel> availablePromotions = new();
        private string selectedPromoCode = "";

        [Parameter] public string ViewType { get; set; } = "Product";
        [Parameter] public EventCallback OnToggleSidebar { get; set; }
        [Parameter] public bool StartInSectionMode { get; set; } = false;
        [Parameter] public bool ShowRestoreButton { get; set; } = false;

        private string ViewTypeDisplay => LangSvc.GetText("Menu" + ViewType) is { } t && t != "Menu" + ViewType ? t : ViewType;

        private static string GetItemType(int inventoryTypeId) =>
            inventoryTypeId == 3 ? "Service" : inventoryTypeId == 5 ? "Package" : inventoryTypeId == 7 ? "TopUp" : "Product";

        private string GetDisplayImageUrl(InventoryDM item) =>
            CacheService.GetImage(item.MasterAccountID ?? "")
            ?? LocalImageCache.GetImage(item.MasterAccountID ?? "")
            ?? ((!string.IsNullOrEmpty(item.ImagePath) && !string.IsNullOrEmpty(item.ImageFileName))
                ? $"{item.ImagePath}/{item.ImageFileName}" : "");

        private bool NewItemIsAvailable
        {
            get => string.Equals(newItem.AccountStatus, "Active", StringComparison.OrdinalIgnoreCase);
            set => newItem.AccountStatus = value ? "Active" : "Inactive";
        }

        private string selectedSec = "All";
        private bool isFilterVisible = false;
        private bool isAddMode = false;
        private bool isEditingExistingItem = false;
        private string activeAddTab = "Info";

        // Popup States
        private bool isAmountPopupOpen = false;
        private bool isDurationPopupOpen = false;
        private bool isBarcodePopupOpen = false;
        private bool isScanning = false;
        private bool isOutletPopupOpen = false;

        // NEW: Dropdown State for Unit and Category
        private bool isUnitDropdownOpen = false;
        private bool isCategoryDropdownOpen = false;

        // Stock In State
        private bool isStockInMode = false;
        private bool isStockInProductSelectionOpen = false;
        private string stockInAction = "";
        private string stockInRemarks = "";
        private string stockInSearchQuery = "";
        private string stockInSortOption = "Name"; // Name, LowPrice, HighPrice
        private bool isStockInFilterDropdownOpen = false;
        private bool isStockInSearchVisible = false;
        private string stockInSelectedCategory = "All";
        private InventoryDM? _currentStockInItem; // Track item being added to stock

        private List<string> stockInActionList = new List<string> { "Stock In", "Adjustment", "Internal Transferred In", "Recover" };

        // BOM Popup States - kept in code but UI entry removed
        private bool isBomPopupOpen = false;
        private bool isAddMaterialPopupOpen = false;

        private List<InventoryDM> selectedBomItems = new List<InventoryDM>();
        private List<InventoryDM> _bomSnapshot = new List<InventoryDM>();

        private string bomSearchQuery = "";
        private string bomSortOption = "NameAsc";
        private bool isBomSortMenuOpen = false;
        private string bomSectionFilter = "All";

        // Commission — not in InventoryDM; tracked locally
        private decimal _commission1, _commission2, _commission3;
        private bool _commission1IsPercent = true, _commission2IsPercent = true, _commission3IsPercent = true;
        // Service duration — not in InventoryDM
        private int? _duration;
        // Redeem point — not in InventoryDM
        private decimal _redeemPoint;
        // Barcode — tracked locally to avoid sharing the DisplayCode backing field in EBI InventoryDM
        private string _barcode = "";
        // Bill of material string — not in InventoryDM
        private string _billOfMaterial = "";
        // Image name display
        private string _imageName = "";

        // Toggles for Advance Tab
        private bool isMinMaxPriceEnabled = false;
        private bool isRedeemPointEnabled = false;

        // Product Promotion Setup (Item-Level Advance Tab)
        private bool isPromoEnabled = false;
        private string promoType = "Fixed Price";
        private decimal promoValue = 0.00m;
        private DateTime? promoStartDate = DateTime.Today;
        private DateTime? promoEndDate = DateTime.Today.AddMonths(1);
        private int promoMinQty = 1;
        private int promoMaxLimit = 0;

        // Visible At Branch
        private Dictionary<string, string> _branchNames = new();
        private HashSet<string> _visibleAtBranchIds = new();

        // Calculator Logic
        private string _calculatorTarget = "Price";
        private string _tempAmountString = "0";
        private string _tempImagePreview = "";

        // Filter/Sort variables
        private string filterSearchQuery = "";
        private string sortOption = "NameAsc";
        private string filterActiveStatus = "All"; // "All", "Active", "Inactive"
        private string filterCategory = "All";

        // Pagination
        private int _currentPage = 1;
        private const int PageSize = 10;

        private int TotalFilteredCount => FilteredItems.Count();
        private int TotalPages => Math.Max(1, (int)Math.Ceiling(TotalFilteredCount / (double)PageSize));

        private IEnumerable<InventoryDM> PagedItems
        {
            get
            {
                var total = TotalPages;
                if (_currentPage > total) _currentPage = total;
                if (_currentPage < 1) _currentPage = 1;
                return FilteredItems.Skip((_currentPage - 1) * PageSize).Take(PageSize);
            }
        }

        private int PageRangeStart => TotalFilteredCount == 0 ? 0 : (_currentPage - 1) * PageSize + 1;
        private int PageRangeEnd => Math.Min(_currentPage * PageSize, TotalFilteredCount);

        private void PreviousPage() { if (_currentPage > 1) _currentPage--; }
        private void NextPage() { if (_currentPage < TotalPages) _currentPage++; }
        private void GoToPage(int page) { if (page >= 1 && page <= TotalPages) _currentPage = page; }
        private void ResetPage() => _currentPage = 1;

        private IEnumerable<int> GetVisiblePages()
        {
            const int maxVisible = 5;
            int start = Math.Max(1, _currentPage - 2);
            int end = Math.Min(TotalPages, start + maxVisible - 1);
            start = Math.Max(1, end - maxVisible + 1);
            return Enumerable.Range(start, end - start + 1);
        }

        private void OnActiveStatusChanged(ChangeEventArgs e) { filterActiveStatus = e.Value?.ToString() ?? "All"; ResetPage(); }
        private void OnCategoryChanged(ChangeEventArgs e)
        {
            filterCategory = e.Value?.ToString() ?? "All";
            ResetPage();
            // Optionally clear search when a category is selected
            // _categorySearchQuery = "";
        }
        private bool isLoading = false;
        private bool _isSaving = false;       // guards SaveItem against double-tap
        private bool _isSavingSection = false; // guards SaveSection against double-tap
        private bool isErrorPopupOpen = false;
        private string errorMessage = "";
        private string errorTitle = "Missing Information";
        private bool isSuccessPopupOpen = false;
        private string successMessage = "";
        private bool _remarkCaptureInitialized = false;

        private bool _suppressNextCacheUpdate = false;
        private bool isDeleteConfirmOpen = false;
        private InventoryDM? _itemToDelete;
        private bool isSectionDeleteConfirmOpen = false;
        private SectionModel? _sectionToDelete;
        private InventoryDM newItem = new InventoryDM();

        // Section Settings Data
        public class SectionModel
        {
            public Guid Id = Guid.NewGuid();
            /// <summary>API SupportingTableID. Populated after loading from API.</summary>
            public string SupportingTableID = "";
            public string Name = "";
            public string AdditionalTitle = "";
            public int FolderCount { get; set; }
            public int SkuCount { get; set; }
            public string Code = "";
            public bool IsCollectionRewardEnabled { get; set; }
            public decimal CreditPerCollection { get; set; }
            public decimal PointPerCollection { get; set; }
            public bool IsReverseRate = false;
            public DateTime CreatedDateTime;
        }

        private bool isSectionSettingsMode = false;
        private bool isAddSectionMode = false;
        private bool isEditSectionMode = false;
        private List<SectionModel> sectionsDb = new List<SectionModel>();
        private SectionModel newSection = new SectionModel();

        // Section search/filter
        private string sectionSearchQuery = "";
        private bool isSectionFilterVisible = false;
        private string sectionFilterStatus = "All";

        private IEnumerable<SectionModel> FilteredSections
        {
            get
            {
                var query = sectionsDb.AsEnumerable();

                // Apply search filter (name search)
                if (!string.IsNullOrWhiteSpace(sectionSearchQuery))
                    query = query.Where(s => s.Name.Contains(sectionSearchQuery, StringComparison.OrdinalIgnoreCase));

                // Apply sorting based on selected filter
                switch (sectionFilterStatus)
                {
                    case "Newest":
                        // Newest first, then apply name sorting within same date
                        if (itemGroupSortColumn == "Name" && itemGroupSortAscending)
                            return query.OrderByDescending(s => s.CreatedDateTime).ThenBy(s => s.Name);
                        else if (itemGroupSortColumn == "Name" && !itemGroupSortAscending)
                            return query.OrderByDescending(s => s.CreatedDateTime).ThenByDescending(s => s.Name);
                        else
                            return query.OrderByDescending(s => s.CreatedDateTime).ThenBy(s => s.Name);

                    case "Oldest":
                        // Oldest first, then apply name sorting within same date
                        if (itemGroupSortColumn == "Name" && itemGroupSortAscending)
                            return query.OrderBy(s => s.CreatedDateTime).ThenBy(s => s.Name);
                        else if (itemGroupSortColumn == "Name" && !itemGroupSortAscending)
                            return query.OrderBy(s => s.CreatedDateTime).ThenByDescending(s => s.Name);
                        else
                            return query.OrderBy(s => s.CreatedDateTime).ThenBy(s => s.Name);

                    default: // "All"
                             // Use column-based sorting only
                        return itemGroupSortColumn switch
                        {
                            "Name" => itemGroupSortAscending
                                ? query.OrderBy(s => s.Name)
                                : query.OrderByDescending(s => s.Name),
                            _ => query.OrderBy(s => s.Name) // Default fallback
                        };
                }
            }
        }

        // Section Pagination
        private int _sectionCurrentPage = 1;
        private const int SectionPageSize = 10;

        private int TotalSectionCount => FilteredSections.Count();
        private int TotalSectionPages => Math.Max(1, (int)Math.Ceiling(TotalSectionCount / (double)SectionPageSize));
        private int SectionPageRangeStart => TotalSectionCount == 0 ? 0 : (_sectionCurrentPage - 1) * SectionPageSize + 1;
        private int SectionPageRangeEnd => Math.Min(_sectionCurrentPage * SectionPageSize, TotalSectionCount);

        private IEnumerable<SectionModel> PagedSections
        {
            get
            {
                var total = TotalSectionPages;
                if (_sectionCurrentPage > total) _sectionCurrentPage = total;
                if (_sectionCurrentPage < 1) _sectionCurrentPage = 1;
                return FilteredSections.Skip((_sectionCurrentPage - 1) * SectionPageSize).Take(SectionPageSize);
            }
        }

        private void SectionPreviousPage() { if (_sectionCurrentPage > 1) _sectionCurrentPage--; }
        private void SectionNextPage() { if (_sectionCurrentPage < TotalSectionPages) _sectionCurrentPage++; }
        private void GoToSectionPage(int pg) { if (pg >= 1 && pg <= TotalSectionPages) _sectionCurrentPage = pg; }
        private void ResetSectionPage() => _sectionCurrentPage = 1;

        private IEnumerable<int> GetVisibleSectionPages()
        {
            const int maxVisible = 5;
            int start = Math.Max(1, _sectionCurrentPage - 2);
            int end = Math.Min(TotalSectionPages, start + maxVisible - 1);
            start = Math.Max(1, end - maxVisible + 1);
            return Enumerable.Range(start, end - start + 1);
        }

        private void ToggleSectionFilterVisibility() { isSectionFilterVisible = !isSectionFilterVisible; }

        // Populated from SupportingTable type=4 (Item Categories) via API
        private List<SupportingTableItem> _apiCategories = new();
        // Branch ID taken from the first loaded inventory item — required by CreateSimple and Update APIs.
        private string _defaultBranchID = string.Empty;
        private List<string> Categories => _apiCategories
            .Select(c => c.SupportingTableName ?? "")
            .Where(n => !string.IsNullOrEmpty(n))
            .OrderBy(n => n) 
            .ToList();
        private List<string> unitList = new List<string> { "centimeter", "gram", "inch", "kati", "kilogram", "liter", "meter", "miligram", "mililiter", "ounce", "piece", "package", "unit" };

        public class OutletItem
        {
            public string Name = string.Empty;
            public string Code = string.Empty;
        }

        private List<OutletItem> outletList = new List<OutletItem>
        {
            new OutletItem { Name = "Main Branch", Code = "#1001" },
            new OutletItem { Name = "City Mall", Code = "#1002" },
            new OutletItem { Name = "West Wing", Code = "#1003" },
            new OutletItem { Name = "Airport Kiosk", Code = "#1004" }
        };
        private HashSet<string> selectedOutletCodes = new HashSet<string>();

        private List<InventoryDM> menuDb = new List<InventoryDM>();

        protected override void OnInitialized()
        {
            // Subscribe before OnInitializedAsync so we never miss a background update.
            CacheService.OnCacheUpdated += HandleCacheUpdated;
        }

        protected override async System.Threading.Tasks.Task OnAfterRenderAsync(bool firstRender)
        {
            if (isAddMode)
            {
                // Reset capture state exactly once when the modal first opens so that
                // _remarkTouched is cleared and the initial textarea value is trusted.
                if (!_remarkCaptureInitialized)
                {
                    _remarkCaptureInitialized = true;
                    try { await JS.InvokeVoidAsync("resetRemarkCapture"); } catch { }
                }
                // Re-attach the native listener each render while the textarea is visible.
                // After the user types (_remarkTouched=true) this also restores the typed
                // value if Blazor's re-render reset the DOM value.
                if (activeAddTab == "Info")
                {
                    try { await JS.InvokeVoidAsync("attachRemarkListener", "product-remark-input", newItem.Remarks ?? ""); } catch { }
                }
            }
            else
            {
                _remarkCaptureInitialized = false;
            }
        }

        protected override async System.Threading.Tasks.Task OnInitializedAsync()
        {
            if (StartInSectionMode) isSectionSettingsMode = true;

            availablePriceGroups = await PriceGroupService.GetPriceGroupsAsync();
            availablePromotions = await PromotionSetupService.GetPromotionsAsync();

            if (ViewType == "Package" || ViewType == "TopUp")
                await LoadMembershipTypesAsync();

            if (CacheService.HasCache)
            {
                // Instant render from cache — no spinner.
                ApplyCachedData();
                // Silently refresh in the background so data stays fresh.
                CacheService.TriggerBackgroundRefresh();
                _ = LoadBranchNamesAsync();
            }
            else
            {
                // First visit: fetch from API, show spinner while waiting.
                isLoading = true;
                await System.Threading.Tasks.Task.WhenAll(LoadCategoriesAsync(), LoadInventoryItemsAsync(), LoadBranchNamesAsync());
                isLoading = false;
            }
        }

        private async System.Threading.Tasks.Task LoadBranchNamesAsync()
        {
            var tasks = AppState.AvailableBranches.Select(async id =>
            {
                var (details, _) = await BranchService.GetBranchDetailsAsync(id);
                if (details != null && !string.IsNullOrWhiteSpace(details.Branch))
                    _branchNames[id] = details.Branch;
            });
            await System.Threading.Tasks.Task.WhenAll(tasks);
            await InvokeAsync(StateHasChanged);
        }

        private async System.Threading.Tasks.Task LoadMembershipTypesAsync()
        {
            try
            {
                var types = await MembershipTypeService.GetAllMembershipTypesAsync();
                _membershipTypes = types?.Where(t => t.Active).ToList() ?? new();
            }
            catch { }
        }

        /// <summary>Called on a background thread when the silent refresh finishes. Updates the UI.</summary>
        private void HandleCacheUpdated()
        {
            InvokeAsync(() =>
            {
                // Do NOT re-render while the user has the add/edit form open —
                // a StateHasChanged() during input can disrupt focus and reset typed text in MAUI WebView.
                if (isAddMode || isStockInMode) return;
                // Skip one background refresh after a save to prevent stale pre-save data from
                // overwriting the fresh data that was just loaded by SaveItem().
                if (_suppressNextCacheUpdate) { _suppressNextCacheUpdate = false; return; }
                ApplyCachedData();
                StateHasChanged();
            });
        }

        public void Dispose()
        {
            CacheService.OnCacheUpdated -= HandleCacheUpdated;
        }

        /// <summary>Apply whatever raw data the cache currently holds into local component state.</summary>
        private void ApplyCachedData()
        {
            if (CacheService.Categories != null) ApplyCategories(CacheService.Categories);
            if (CacheService.Items != null) ApplyItems(CacheService.Items);
        }

        private void ApplyCategories(List<SenangRetails.Shared.Models.DTOs.SupportingTableItem> items)
        {
            _apiCategories = items.Where(i => i.Active).ToList();
            sectionsDb = _apiCategories.Select(c => new SectionModel
            {
                SupportingTableID = c.SupportingTableID ?? "",
                Name = c.SupportingTableName ?? "",
                Code = c.SupportingTableID ?? "",
                CreatedDateTime = DateTime.TryParse(c.CreatedDateTime?.ToString(), out var dt) ? dt : DateTime.Now
            }).ToList();
        }

        private void ApplyItems(List<InventoryDM> items)
        {
            menuDb = items.Where(i =>
                !(i.InventoryTypeID == 5 && i.lstPackage != null && i.lstPackage.Any(p => p.PackageQuantityTypeID == 1))
            ).ToList();
            if (string.IsNullOrEmpty(_defaultBranchID))
                _defaultBranchID = items.FirstOrDefault(i => !string.IsNullOrEmpty(i.BranchID))?.BranchID ?? string.Empty;
            System.Diagnostics.Debug.WriteLine($"[Product] _defaultBranchID = '{_defaultBranchID}'");
        }

        private async System.Threading.Tasks.Task LoadCategoriesAsync()
        {
            var items = await SupportingTableService.LoadListByTypeAsync(4);
            if (items != null)
            {
                CacheService.SetCategories(items);
                ApplyCategories(items);
            }
        }

        private async System.Threading.Tasks.Task LoadInventoryItemsAsync()
        {
            var items = await InventoryService.LoadItemsAsync(AppState.SelectedBranchID);
            if (items != null)
            {
                CacheService.SetItems(items);
                ApplyItems(items);
            }
        }


        private string? GetDisplayCodeForSave(InventoryDM item)
        {
            if (!string.IsNullOrEmpty(item.DisplayCode) && item.DisplayCode != "-")
                return item.DisplayCode;
            return string.IsNullOrEmpty(item.MasterAccountID) ? null : item.MasterAccountID;
        }

        private async System.Threading.Tasks.Task TriggerToggleSidebar()
        {
            if (OnToggleSidebar.HasDelegate)
            {
                await OnToggleSidebar.InvokeAsync();
            }
        }

        private IEnumerable<string> AvailableSecs => menuDb
            .Where(x => GetItemType(x.InventoryTypeID) == ViewType)
            .Select(x => x.ItemGroupName ?? "")
            .Where(n => !string.IsNullOrEmpty(n))
            .Distinct()
            .OrderBy(x => x);

        private IEnumerable<InventoryDM> FilteredItems
        {
            get
            {
                var query = menuDb.Where(x => GetItemType(x.InventoryTypeID) == ViewType);

                if (!string.IsNullOrEmpty(filterSearchQuery))
                    query = query.Where(e => (e.AccountName ?? "").Contains(filterSearchQuery, StringComparison.OrdinalIgnoreCase));
                if (filterActiveStatus == "Active")
                    query = query.Where(e => string.Equals(e.AccountStatus, "Active", StringComparison.OrdinalIgnoreCase));
                else if (filterActiveStatus == "Inactive")
                    query = query.Where(e => !string.Equals(e.AccountStatus, "Active", StringComparison.OrdinalIgnoreCase));
                if (filterCategory != "All")
                    query = query.Where(e => e.ItemGroupName == filterCategory);

                return sortColumn switch
                {
                    "Name" => sortAscending
                        ? query.OrderBy(e => e.AccountName)
                        : query.OrderByDescending(e => e.AccountName),
                    "Category" => sortAscending
                        ? query.OrderBy(e => e.ItemGroupName ?? string.Empty)
                        : query.OrderByDescending(e => e.ItemGroupName ?? string.Empty),
                    "Reorder" => sortAscending
                        ? query.OrderBy(e => e.StockReorderLevel)
                        : query.OrderByDescending(e => e.StockReorderLevel),
                    "Price" => sortAscending
                        ? query.OrderBy(e => e.SalesPrice)
                        : query.OrderByDescending(e => e.SalesPrice),
                    _ => query.OrderBy(e => e.AccountName)
                };
            }
        }

        private IEnumerable<InventoryDM> StockInFilteredItems
        {
            get
            {
                var query = menuDb.Where(x => GetItemType(x.InventoryTypeID) == ViewType);

                if (stockInSelectedCategory != "All") query = query.Where(e => e.ItemGroupName == stockInSelectedCategory);
                if (!string.IsNullOrEmpty(stockInSearchQuery)) query = query.Where(e => (e.AccountName ?? "").Contains(stockInSearchQuery, StringComparison.OrdinalIgnoreCase));

                return stockInSortOption switch
                {
                    "Name" => query.OrderBy(e => e.AccountName),
                    "Low Price" => query.OrderBy(e => e.SalesPrice),
                    "High Price" => query.OrderByDescending(e => e.SalesPrice),
                    _ => query.OrderBy(e => e.AccountName)
                };
            }
        }

        private string GetIconForCategory(string category)
        {
            return "<svg class='cat-icon' width='24' height='24' xmlns='http://www.w3.org/2000/svg' viewBox='0 0 24 24' fill='none' stroke='currentColor' stroke-width='2' stroke-linecap='round' stroke-linejoin='round'><circle cx='12' cy='12' r='10'></circle></svg>";
        }

        private void OpenAmountPopup(string target, InventoryDM? item = null)
        {
            _calculatorTarget = target;
            decimal val = 0;
            if (target == "Price") val = newItem.SalesPrice;
            else if (target == "Cost") val = newItem.PurchasePrice;
            else if (target == "FreePoint") val = 0;
            else if (target == "RedeemPoint") val = _redeemPoint;
            else if (target == "MinPrice") val = 0;
            else if (target == "MaxPrice") val = 0;
            else if (target == "CreditReward") val = newSection.CreditPerCollection;
            else if (target == "PointReward") val = newSection.PointPerCollection;
            else if (target == "LowStockAlert") val = newItem.StockReorderLevel;
            else if (target == "Commission1") val = _commission1;
            else if (target == "Commission2") val = _commission2;
            else if (target == "Commission3") val = _commission3;
            else if (target == "StockInQty")
            {
                _currentStockInItem = item;
                val = 0;
            }

            _tempAmountString = (target == "LowStockAlert" || target == "StockInQty") ? ((int)val).ToString() : (val == 0 ? "0.00" : val.ToString("0.00"));
            if (target == "StockInQty" && val == 0) _tempAmountString = "0";

            isAmountPopupOpen = true;
        }

        private void AppendToAmount(string val)
        {
            // Block decimal point for LowStockAlert AND StockInQty
            if ((_calculatorTarget == "LowStockAlert" || _calculatorTarget == "StockInQty") && val == ".") return;

            if (_tempAmountString == "0.00" || _tempAmountString == "0") _tempAmountString = val;
            else if (!(val == "." && _tempAmountString.Contains("."))) _tempAmountString += val;
        }

        private void ClearAmount() => _tempAmountString = "0";
        private void BackspaceAmount() => _tempAmountString = _tempAmountString.Length > 1 ? _tempAmountString.Substring(0, _tempAmountString.Length - 1) : "0";

        private void SaveAmount()
        {
            if (decimal.TryParse(_tempAmountString, out decimal result))
            {
                if (_calculatorTarget == "Price") newItem.SalesPrice = result;
                else if (_calculatorTarget == "Cost") newItem.PurchasePrice = result;
                else if (_calculatorTarget == "CreditReward") newSection.CreditPerCollection = result;
                else if (_calculatorTarget == "PointReward") newSection.PointPerCollection = result;
                else if (_calculatorTarget == "LowStockAlert")
                {
                    newItem.StockReorderLevel = result;
                    if (ViewType == "Service") _duration = (int)result;
                }
                else if (_calculatorTarget == "Commission1") _commission1 = result;
                else if (_calculatorTarget == "Commission2") _commission2 = result;
                else if (_calculatorTarget == "Commission3") _commission3 = result;
                else if (_calculatorTarget == "RedeemPoint") _redeemPoint = result;
                else if (_calculatorTarget == "StockInQty")
                {
                    isStockInProductSelectionOpen = false;
                }
            }
            isAmountPopupOpen = false;
        }

        private void ToggleOutletSelection(string code)
        {
            if (selectedOutletCodes.Contains(code)) selectedOutletCodes.Remove(code);
            else selectedOutletCodes.Add(code);
        }

        private bool IsAllVisibleBranchesSelected() =>
            AppState.AvailableBranches.Any() && _visibleAtBranchIds.Count == AppState.AvailableBranches.Count;

        private void ToggleVisibleAtBranch(string branchId)
        {
            if (_visibleAtBranchIds.Contains(branchId)) _visibleAtBranchIds.Remove(branchId);
            else _visibleAtBranchIds.Add(branchId);
        }

        private void ToggleAllVisibleBranches(ChangeEventArgs e)
        {
            var isChecked = e.Value is bool b && b;
            if (isChecked)
                foreach (var id in AppState.AvailableBranches)
                    _visibleAtBranchIds.Add(id);
            else
                _visibleAtBranchIds.Clear();
        }

        private void ToggleMinMaxPrice()
        {
            isMinMaxPriceEnabled = !isMinMaxPriceEnabled;
        }

        private void ToggleRedeemPoint()
        {
            isRedeemPointEnabled = !isRedeemPointEnabled;
        }

        private bool _isCustomDuration = false;
        private string _customDurationValue = "";

        private void OpenDurationPopup()
        {
            _isCustomDuration = false;
            _customDurationValue = "";
            isDurationPopupOpen = true;
        }

        private void OpenCustomDuration()
        {
            _isCustomDuration = true;
            _customDurationValue = _duration.HasValue ? _duration.Value.ToString() : "";
        }

        private void ConfirmCustomDuration()
        {
            if (int.TryParse(_customDurationValue, out var mins) && mins > 0)
            {
                _duration = mins;
                isDurationPopupOpen = false;
                _isCustomDuration = false;
            }
        }

        private void SelectDuration(int? minutes)
        {
            _duration = minutes;
            _isCustomDuration = false;
            isDurationPopupOpen = false;
        }

        private void ToggleFilterVisibility() { isFilterVisible = !isFilterVisible; }
        private void ResetFilters() { filterActiveStatus = "All"; filterCategory = "All"; filterSearchQuery = ""; sortOption = "NameAsc"; }

        private void OpenSectionSettings() { isSectionSettingsMode = true; isAddSectionMode = false; isEditSectionMode = false; }
        private void GoBackToMain() { isSectionSettingsMode = false; isAddSectionMode = false; isEditSectionMode = false; }

        private void OpenAddSectionMode() { newSection = new SectionModel(); isAddSectionMode = true; isEditSectionMode = false; sectionSearchQuery = ""; isSectionFilterVisible = false; }
        private void OpenEditSectionMode(SectionModel section)
        {
            newSection = new SectionModel { Id = section.Id, SupportingTableID = section.SupportingTableID, Name = section.Name, AdditionalTitle = section.AdditionalTitle, Code = section.Code, IsCollectionRewardEnabled = section.IsCollectionRewardEnabled, CreditPerCollection = section.CreditPerCollection, PointPerCollection = section.PointPerCollection, IsReverseRate = section.IsReverseRate, FolderCount = section.FolderCount, SkuCount = section.SkuCount };
            isAddSectionMode = true; isEditSectionMode = true;
        }
        private void CloseAddSectionMode() { isAddSectionMode = false; isEditSectionMode = false; }

        private async System.Threading.Tasks.Task SaveSection()
        {
            if (_isSavingSection) return;
            _isSavingSection = true;
            try
            {
                if (string.IsNullOrWhiteSpace(newSection.Name))
                {
                    errorTitle = "Missing Information";
                    errorMessage = "Section Name is a compulsory field. Please enter a section name before saving.";
                    isErrorPopupOpen = true;
                    return;
                }

                var token = await StoreTokenService.GetTokenAsync();
                if (string.IsNullOrEmpty(token))
                {
                    errorTitle = "Session Expired";
                    errorMessage = "Your login session has expired. Please go back and log in again.";
                    isErrorPopupOpen = true;
                    return;
                }

                if (isEditSectionMode)
                {
                    var model = new SupportingTableModel
                    {
                        supportingTableID = newSection.SupportingTableID,
                        supportingTableName = newSection.Name,
                        supportingTableTypeID = 4,
                        active = true
                    };
                    var (success, message) = await SupportingTableService.UpdateAsync(model);
                    if (success)
                    {
                        NotificationSvc.Add(new AppNotification
                        {
                            Icon = "🗂️",
                            TitleKey = "NotifSectionUpdatedTitle",
                            MessageKey = "NotifSectionUpdatedMsg",
                            MessageParam = newSection.Name
                        });
                        await JS.InvokeVoidAsync("localStorage.setItem", "app_notifications", NotificationSvc.ToJson());
                        await LoadCategoriesAsync();
                    }
                    else
                    {
                        var isAuthError = !string.IsNullOrWhiteSpace(message) &&
                            (message.Contains("401") || message.Contains("Unauthorized", StringComparison.OrdinalIgnoreCase) || message.Contains("expired", StringComparison.OrdinalIgnoreCase));
                        errorTitle = isAuthError ? "Session Expired" : "Save Failed";
                        errorMessage = isAuthError
                            ? "Your login session has expired. Please go back and log in again."
                            : (string.IsNullOrWhiteSpace(message) ? "Unable to save section. Please check your connection and try again." : message);
                        isErrorPopupOpen = true;
                        return;
                    }
                }
                else
                {
                    var model = new SupportingTableModel
                    {
                        supportingTableID = null,
                        supportingTableName = newSection.Name,
                        supportingTableTypeID = 4,
                        active = true
                    };
                    var (success, message) = await SupportingTableService.CreateAsync(model);
                    if (success)
                    {
                        NotificationSvc.Add(new AppNotification
                        {
                            Icon = "🗂️",
                            TitleKey = "NotifnewSectionTitle",
                            MessageKey = "NotifnewSectionMsg",
                            MessageParam = newSection.Name
                        });
                        await JS.InvokeVoidAsync("localStorage.setItem", "app_notifications", NotificationSvc.ToJson());
                        await LoadCategoriesAsync();
                    }
                    else
                    {
                        var isAuthError = !string.IsNullOrWhiteSpace(message) &&
                            (message.Contains("401") || message.Contains("Unauthorized", StringComparison.OrdinalIgnoreCase) || message.Contains("expired", StringComparison.OrdinalIgnoreCase));
                        errorTitle = isAuthError ? "Session Expired" : "Save Failed";
                        errorMessage = isAuthError
                            ? "Your login session has expired. Please go back and log in again."
                            : (string.IsNullOrWhiteSpace(message) ? "Unable to create section. Please check your connection and try again." : message);
                        isErrorPopupOpen = true;
                        return;
                    }
                }

                isAddSectionMode = false; isEditSectionMode = false;
            } // end try
            finally { _isSavingSection = false; }
        }

        private void ToggleCollectionReward() { newSection.IsCollectionRewardEnabled = !newSection.IsCollectionRewardEnabled; }
        private void ToggleReverseRate() { newSection.IsReverseRate = !newSection.IsReverseRate; }

        // ── Package Items ────────────────────────────────────────────────────────
        public class PackageItemLine
        {
            public string AutoID = "";        // server autoID — set when loaded from API
            public string PackageLineID = ""; // server packageID — set when loaded from API
            public string MasterAccountID = "";
            public string Name = "";
            public string ItemType = "";
            public decimal Qty = 1;
            public decimal Price = 0;
        }

        public class MembershipCreditLine
        {
            public string MemberTypeID = "";   // actual server ID
            public string MemberTypeName = "";
            public decimal CreditAmount = 0;
            public bool IsSelected = false;
            public bool IsFromServer = false;  // drives saveAction "Changed" vs "Added"
        }

        private List<PackageItemLine> packageItems = new();
        private List<MembershipCreditLine> membershipCredits = new();
        private List<PackageItemLine> _serverLoadedPackageItems = new();
        private List<MembershipCreditLine> _serverLoadedMembershipCredits = new();
        private int expiryDays = 0;
        private List<MembershipTypeDM> _membershipTypes = new();

        // Item picker state
        private bool _showItemPicker = false;
        private string _itemPickerSearch = "";

        // Member type picker state
        private bool _showMemberTypePicker = false;

        private List<string> availableMembershipTypes => _membershipTypes.Select(m => m.MemberTypeName).Where(n => !string.IsNullOrEmpty(n)).ToList();

        private void OpenItemPicker() { _showItemPicker = true; _itemPickerSearch = ""; }

        private void SelectPackageItem(InventoryDM item)
        {
            packageItems.Add(new PackageItemLine
            {
                MasterAccountID = item.MasterAccountID ?? "",
                Name = item.AccountName ?? "",
                ItemType = GetItemType(item.InventoryTypeID),
                Price = item.SalesPrice,
                Qty = 1
            });
            _showItemPicker = false;
        }

        private void OpenMemberTypePicker() => _showMemberTypePicker = true;

        private void SelectMemberTypeForCredit(string mt)
        {
            if (!membershipCredits.Any(m => m.MemberTypeName == mt))
            {
                var typeId = _membershipTypes.FirstOrDefault(t => t.MemberTypeName == mt)?.MemberTypeID ?? "";
                membershipCredits.Add(new MembershipCreditLine { MemberTypeName = mt, MemberTypeID = typeId });
            }
            _showMemberTypePicker = false;
        }

        private void RemoveSelectedCredits() => membershipCredits.RemoveAll(m => m.IsSelected);

        private void TogglePackageMembership(string mt) { }

        private InventoryCreateModel BuildInventoryCreateModel(InventoryDM item, string saveAction)
        {
            var category = _apiCategories.FirstOrDefault(c =>
                string.Equals(c.SupportingTableName, item.ItemGroupName, StringComparison.OrdinalIgnoreCase));
            var groupId = category?.SupportingTableID ?? item.ItemGroupID;
            var groupName = category?.SupportingTableName ?? item.ItemGroupName;
            var unitId = !string.IsNullOrEmpty(item.UnitOfMeasureID) ? item.UnitOfMeasureID : item.UnitOfMeasureName;
            var unitName = !string.IsNullOrEmpty(item.UnitOfMeasureName) ? item.UnitOfMeasureName : item.UnitOfMeasureID;
            var branch = !string.IsNullOrEmpty(item.BranchID) ? item.BranchID : _defaultBranchID;

            return new InventoryCreateModel
            {
                saveAction = saveAction,
                masterAccountID = string.IsNullOrEmpty(item.MasterAccountID) ? null : item.MasterAccountID,
                inventoryTypeID = item.InventoryTypeID > 0 ? item.InventoryTypeID : 1,
                isDirty = true,
                accountName = item.AccountName ?? "",
                itemGroupID = string.IsNullOrEmpty(groupId) ? null : groupId,
                itemGroupName = string.IsNullOrEmpty(groupName) ? null : groupName,
                salesDescription = string.IsNullOrEmpty(item.SalesDescription) ? null : item.SalesDescription,
                displayCode = GetDisplayCodeForSave(item),
                vendorItemCode = string.IsNullOrEmpty(_barcode) ? null : _barcode,
                salesPrice = item.SalesPrice,
                purchasePrice = item.PurchasePrice,
                taxCodeID = string.IsNullOrEmpty(item.TaxCodeID) ? null : item.TaxCodeID,
                isTaxInclusive = item.IsTaxInclusive,
                isSold = true,
                accountStatus = string.IsNullOrEmpty(item.AccountStatus) ? "Active" : item.AccountStatus,
                unitOfMeasureID = string.IsNullOrEmpty(unitId) ? null : unitId,
                unitOfMeasureName = string.IsNullOrEmpty(unitName) ? null : unitName,
                stockReorderLevel = ViewType == "Service" ? (_duration.HasValue ? (decimal)_duration.Value : 0m) : item.StockReorderLevel,
                branchID = string.IsNullOrEmpty(branch) ? null : branch,
                hasPackage = true,
                validityDays = expiryDays,
                remarks = string.IsNullOrEmpty(item.Remarks) ? null : item.Remarks,
                staffCommissionA = FormatCommissionFormula(_commission1, _commission1IsPercent),
                staffCommissionB = FormatCommissionFormula(_commission2, _commission2IsPercent),
                staffCommissionC = FormatCommissionFormula(_commission3, _commission3IsPercent),
                PointToRedeem = _redeemPoint > 0 ? _redeemPoint : (decimal?)null,
                AllowPointRedemption = _redeemPoint > 0
            };
        }

        private static string? FormatCommissionFormula(decimal amount, bool isPercent)
        {
            if (amount == 0) return null;
            return isPercent
                ? $"{amount.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture)}%"
                : amount.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture);
        }

        private static (decimal amount, bool isPercent) ParseCommissionFormula(string? formula)
        {
            if (string.IsNullOrEmpty(formula)) return (0, true);
            var str = formula.TrimStart('T', 'F').TrimEnd('A');
            var isPercent = str.EndsWith("%");
            str = str.TrimEnd('%');
            if (decimal.TryParse(str, System.Globalization.NumberStyles.Any,
                System.Globalization.CultureInfo.InvariantCulture, out var val))
                return (val, isPercent);
            return (0, true);
        }

        private List<MasterAccountBranchEntry> BuildBranchEntries(string? masterAccountId)
        {
            var branchIds = AppState.AvailableBranches.Any()
                ? AppState.AvailableBranches
                : new List<string> { !string.IsNullOrEmpty(newItem.BranchID) ? newItem.BranchID : _defaultBranchID };

            var entries = new List<MasterAccountBranchEntry>();
            foreach (var id in branchIds.Where(id => !string.IsNullOrEmpty(id)))
            {
                var isEnabled = _visibleAtBranchIds.Contains(id);
                entries.Add(new MasterAccountBranchEntry
                {
                    masterAccountID = masterAccountId,
                    branchID = id,
                    branchPrice = newItem.SalesPrice,
                    isEnabled = isEnabled,
                    groupID = id,
                    saveAction = "Added",
                    isDirty = true
                });
            }
            return entries;
        }

        private void OpenAddProductMode()
        {
            isAddMode = true;
            isEditingExistingItem = false;
            newItem = new InventoryDM();
            newItem.InventoryTypeID = ViewType == "Service" ? 3 : ViewType == "Package" ? 5 : ViewType == "TopUp" ? 7 : 1;
            newItem.AccountStatus = "Active";
            newItem.BranchID = _defaultBranchID;
            packageItems.Clear();
            membershipCredits.Clear();
            expiryDays = 0;
            isOpenTopUp = false;
            selectedNonCreditMemberTypeID = "";
            selectedNonCreditMemberTypeName = "";
            activeAddTab = "Info";
            isMinMaxPriceEnabled = false;
            isRedeemPointEnabled = false;
            selectedOutletCodes.Clear();
            selectedBomItems.Clear();
            _tempImagePreview = "";
            _imageName = "";
            _commission1 = 0; _commission2 = 0; _commission3 = 0;
            _commission1IsPercent = true; _commission2IsPercent = true; _commission3IsPercent = true;
            _duration = null;
            _redeemPoint = 0;
            _barcode = "";
            _billOfMaterial = "";
            selectedPriceGroupCode = "";
            _originalSalesPrice = 0;
            selectedPromoCode = "";
            foreach (var o in outletList) selectedOutletCodes.Add(o.Code);
            _visibleAtBranchIds.Clear();
            foreach (var id in AppState.AvailableBranches) _visibleAtBranchIds.Add(id);
            isItemGroupDropdownOpen = false; 
            _itemGroupSearchQuery = ""; 
        }

        private void OnPriceGroupChanged(ChangeEventArgs e)
        {
            selectedPriceGroupCode = e.Value?.ToString() ?? "";
        }

        private void CloseAddMode()
        {
            isAddMode = false;
            isEditingExistingItem = false;
            isFilterVisible = false;
            isItemGroupDropdownOpen = false; 
            _itemGroupSearchQuery = ""; 
        }

        private void StartScanning()
        {
            isScanning = true;
        }

        private void CancelScanning()
        {
            isScanning = false;
        }

        private Task OnBarcodeScan(string scannedCode)
        {
            if (!string.IsNullOrEmpty(scannedCode))
            {
                _barcode = scannedCode;
                isScanning = false;
            }
            return Task.CompletedTask;
        }

        private void CloseBarcodePopup()
        {
            isScanning = false;
            isBarcodePopupOpen = false;
        }

        // ── Upload Menu ────────────────────────────────────────────────────────────
        private bool showUploadMenuModal = false;
        private string? menuSelectedFileName;
        private MemoryStream? menuSelectedFileStream;
        private string? menuSelectedFileExtension;
        private bool IsMenuProcessing = false;
        private string MenuUploadStatus = "";
        private string? MenuErrorMessage;
        private bool menuUploadSuccess = false;
        private string menuSuccessMessage = "";

        private void OpenUploadMenuModal()
        {
            menuSelectedFileName = null;
            menuSelectedFileStream = null;
            menuSelectedFileExtension = null;
            MenuErrorMessage = null;
            MenuUploadStatus = "";
            menuUploadSuccess = false;
            menuSuccessMessage = "";
            showUploadMenuModal = true;
        }

        private void CloseUploadMenuModal()
        {
            showUploadMenuModal = false;
            menuUploadSuccess = false;
            menuSuccessMessage = "";
            menuSelectedFileStream?.Dispose();
            menuSelectedFileStream = null;
            menuSelectedFileName = null;
            menuSelectedFileExtension = null;
        }

        private async Task DownloadMenuTemplate()
        {
            try
            {
                var headers = new[] { "Name", "Section", "Tax Code", "Price Inclusive Tax", "Price", "Cost", "Barcode", "UOM", "Item Code" };
                await FileDownloadService.DownloadCsvTemplateAsync("menu_upload_template.csv", headers);
            }
            catch (Exception ex)
            {
                MenuErrorMessage = $"Error downloading template: {ex.Message}";
            }
        }

        private async Task HandleMenuFileSelected(InputFileChangeEventArgs e)
        {
            var file = e.File;
            MenuErrorMessage = null;
            var ext = Path.GetExtension(file.Name).ToLower();
            if (ext != ".csv")
            {
                MenuErrorMessage = "Only CSV files are accepted. Please select a .csv file.";
                menuSelectedFileName = null;
                menuSelectedFileStream = null;
                StateHasChanged();
                return;
            }
            menuSelectedFileName = file.Name;
            menuSelectedFileExtension = ext;
            menuSelectedFileStream = new MemoryStream();
            using var stream = file.OpenReadStream(1024 * 1024 * 20);
            await stream.CopyToAsync(menuSelectedFileStream);
            menuSelectedFileStream.Position = 0;
            StateHasChanged();
        }

        private async Task PickMenuFileNativeAsync()
        {
            var result = await FilePickerService.PickFileAsync(new[] { ".csv" });
            if (result == null) return;
            menuSelectedFileName = result.FileName;
            menuSelectedFileExtension = result.Extension;
            menuSelectedFileStream = result.Stream;
            MenuErrorMessage = null;
            StateHasChanged();
        }

        private async Task ConfirmMenuUpload()
        {
            if (menuSelectedFileStream == null || string.IsNullOrEmpty(menuSelectedFileExtension))
            {
                MenuErrorMessage = "Please select a CSV file first.";
                return;
            }
            if (menuSelectedFileExtension != ".csv")
            {
                MenuErrorMessage = "Only CSV files are accepted.";
                return;
            }

            MenuErrorMessage = null;
            IsMenuProcessing = true;
            MenuUploadStatus = "Reading file...";
            StateHasChanged();
            await Task.Yield();

            try
            {
                menuSelectedFileStream.Position = 0;
                using var reader = new StreamReader(menuSelectedFileStream, System.Text.Encoding.UTF8, leaveOpen: true);

                var headerLine = await reader.ReadLineAsync();
                if (string.IsNullOrWhiteSpace(headerLine))
                {
                    MenuErrorMessage = "The CSV file is empty or missing the header row.";
                    return;
                }

                var rows = new List<MenuUploadRow>();
                int lineNumber = 1;
                string? line;
                while ((line = await reader.ReadLineAsync()) != null)
                {
                    lineNumber++;
                    if (string.IsNullOrWhiteSpace(line)) continue;

                    var fields = ParseCsvLine(line);

                    if (fields.Length < 4)
                    {
                        MenuErrorMessage = $"Row {lineNumber}: Not enough columns. Expected at least 4 (Name, Section, Tax Code, Price Inclusive Tax).";
                        return;
                    }

                    var name = fields.Length > 0 ? fields[0].Trim() : "";
                    var section = fields.Length > 1 ? fields[1].Trim() : "";
                    var taxCode = fields.Length > 2 ? fields[2].Trim() : "";
                    var priceInclusiveRaw = fields.Length > 3 ? fields[3].Trim() : "";
                    var priceRaw = fields.Length > 4 ? fields[4].Trim() : "0";
                    var costRaw = fields.Length > 5 ? fields[5].Trim() : "0";
                    var barcode = fields.Length > 6 ? fields[6].Trim() : "";
                    var uom = fields.Length > 7 ? fields[7].Trim() : "";
                    var itemCode = fields.Length > 8 ? fields[8].Trim() : "";

                    if (string.IsNullOrWhiteSpace(name)) { MenuErrorMessage = $"Row {lineNumber}: Name is required."; return; }
                    if (string.IsNullOrWhiteSpace(section)) { MenuErrorMessage = $"Row {lineNumber}: Section is required."; return; }
                    if (!priceInclusiveRaw.Equals("Yes", StringComparison.OrdinalIgnoreCase) &&
                        !priceInclusiveRaw.Equals("No", StringComparison.OrdinalIgnoreCase))
                    { MenuErrorMessage = $"Row {lineNumber}: Price Inclusive Tax must be \"Yes\" or \"No\" (found: \"{priceInclusiveRaw}\")."; return; }

                    decimal price = 0;
                    if (!string.IsNullOrWhiteSpace(priceRaw) && !decimal.TryParse(priceRaw, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out price))
                    { MenuErrorMessage = $"Row {lineNumber}: Price must be a number. Found: \"{priceRaw}\"."; return; }

                    decimal cost = 0;
                    if (!string.IsNullOrWhiteSpace(costRaw) && !decimal.TryParse(costRaw, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out cost))
                    { MenuErrorMessage = $"Row {lineNumber}: Cost must be a number. Found: \"{costRaw}\"."; return; }

                    rows.Add(new MenuUploadRow
                    {
                        Name = name,
                        Section = section,
                        TaxCode = taxCode,
                        PriceInclusiveTax = priceInclusiveRaw.Equals("Yes", StringComparison.OrdinalIgnoreCase),
                        Price = price,
                        Cost = cost,
                        Barcode = barcode,
                        UOM = uom,
                        ItemCode = itemCode
                    });
                }

                if (rows.Count == 0) { MenuErrorMessage = "No data rows found in the CSV file."; return; }

                MenuUploadStatus = $"Validated {rows.Count} rows. Importing...";
                StateHasChanged();
                await Task.Yield();

                var branchID = CacheService.Items?.FirstOrDefault(i => !string.IsNullOrEmpty(i.BranchID))?.BranchID ?? "";
                if (string.IsNullOrEmpty(branchID))
                {
                    var liveItems = await InventoryService.LoadItemsAsync("");
                    branchID = liveItems?.FirstOrDefault(i => !string.IsNullOrEmpty(i.BranchID))?.BranchID ?? "";
                }

                var existingSections = await SupportingTableService.LoadListByTypeAsync(4);
                var sectionMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                if (existingSections != null)
                {
                    foreach (var s in existingSections)
                    {
                        var key = s.SupportingTableName?.Trim() ?? "";
                        if (!string.IsNullOrEmpty(key) && !sectionMap.ContainsKey(key))
                            sectionMap[key] = s.SupportingTableID ?? "";
                    }
                }

                int successCount = 0;
                for (int i = 0; i < rows.Count; i++)
                {
                    var row = rows[i];
                    if (!sectionMap.ContainsKey(row.Section))
                    {
                        var newSection = new SenangRetails.Shared.Models.DTOs.SupportingTableModel
                        {
                            supportingTableName = row.Section,
                            supportingTableTypeID = 4,
                            active = true
                        };
                        var (ok, _) = await SupportingTableService.CreateAsync(newSection);
                        if (ok)
                        {
                            var refreshed = await SupportingTableService.LoadListByTypeAsync(4);
                            if (refreshed != null)
                                foreach (var s in refreshed)
                                {
                                    var k = s.SupportingTableName?.Trim() ?? "";
                                    if (!string.IsNullOrEmpty(k))
                                        sectionMap[k] = s.SupportingTableID ?? "";
                                }
                        }
                    }

                    sectionMap.TryGetValue(row.Section, out var sectionId);

                    var model = new EBI.UC.Inventory
                    {
                        objInventory = new InventoryDM
                        {
                            AccountName = row.Name,
                            ItemGroupID = sectionId ?? "",
                            ItemGroupName = row.Section,
                            TaxCodeID = row.TaxCode,
                            IsTaxInclusive = row.PriceInclusiveTax,
                            SalesPrice = row.Price,
                            PurchasePrice = row.Cost,
                            DisplayCode = row.ItemCode,
                            UnitOfMeasureID = row.UOM,
                            VendorItemCode = row.Barcode,
                            InventoryTypeID = ViewType == "Service" ? 3 : ViewType == "Package" ? 5 : ViewType == "TopUp" ? 7 : 1,
                            IsSold = true,
                            AccountStatus = "Active",
                            BranchID = branchID
                        }
                    };

                    var result = await InventoryService.CreateItemAsync(model);
                    if (result.Success) successCount++;

                    if (i % 5 == 0)
                    {
                        MenuUploadStatus = $"Importing {i + 1} of {rows.Count}...";
                        StateHasChanged();
                    }
                }

                menuSuccessMessage = $"Successfully imported {successCount} of {rows.Count} product(s) into the menu.";
                menuUploadSuccess = true;
                IsMenuProcessing = false;
                await LoadInventoryItemsAsync();
                StateHasChanged();
            }
            catch (Exception ex)
            {
                MenuErrorMessage = $"Import failed: {ex.Message}";
            }
            finally
            {
                IsMenuProcessing = false;
                StateHasChanged();
            }
        }

        private static string[] ParseCsvLine(string line)
        {
            var fields = new List<string>();
            int i = 0;
            while (i < line.Length)
            {
                if (line[i] == '"')
                {
                    i++;
                    var sb = new System.Text.StringBuilder();
                    while (i < line.Length)
                    {
                        if (line[i] == '"' && i + 1 < line.Length && line[i + 1] == '"') { sb.Append('"'); i += 2; }
                        else if (line[i] == '"') { i++; break; }
                        else { sb.Append(line[i++]); }
                    }
                    fields.Add(sb.ToString());
                    if (i < line.Length && line[i] == ',') i++;
                }
                else
                {
                    int start = i;
                    while (i < line.Length && line[i] != ',') i++;
                    fields.Add(line[start..i]);
                    if (i < line.Length) i++;
                }
            }
            return fields.ToArray();
        }

        private class MenuUploadRow
        {
            public string Name { get; set; } = "";
            public string Section { get; set; } = "";
            public string TaxCode { get; set; } = "";
            public bool PriceInclusiveTax { get; set; }
            public decimal Price { get; set; }
            public decimal Cost { get; set; }
            public string Barcode { get; set; } = "";
            public string UOM { get; set; } = "";
            public string ItemCode { get; set; } = "";
        }

        // NEW: Stock In Methods
        private void OpenStockInMode()
        {
            Navigation.NavigateTo("/inventory");
        }

        private void CloseStockInMode()
        {
            isStockInMode = false;
        }

        private void OpenStockInProductSelection()
        {
            isStockInProductSelectionOpen = true;
        }

        private void CloseStockInProductSelection()
        {
            isStockInProductSelectionOpen = false;
        }

        private void ToggleStockInFilter()
        {
            isStockInFilterDropdownOpen = !isStockInFilterDropdownOpen;
        }

        private void SelectStockInSort(string option)
        {
            stockInSortOption = option;
            isStockInFilterDropdownOpen = false;
        }

        private void ToggleStockInSearch()
        {
            isStockInSearchVisible = !isStockInSearchVisible;
            if (!isStockInSearchVisible) stockInSearchQuery = "";
        }

        private void SelectStockInCategory(string category)
        {
            stockInSelectedCategory = category;
        }

        private void SaveStockIn()
        {
            // Implementation for saving stock in record
            isStockInMode = false;
        }

        private async System.Threading.Tasks.Task SaveItem()
        {
            if (_isSaving) return;
            _isSaving = true;
            successMessage = "";
            try
            {
                // ── Step 1: DOM fallback ─────────────────────────────────────────────
                try
                {
                    var domName = await JS.InvokeAsync<string?>("getFieldValue", "product-name-input");
                    if (!string.IsNullOrWhiteSpace(domName))
                        newItem.AccountName = domName.Trim();
                }
                catch { /* JS not available */ }

                try
                {
                    // getStoredRemark uses a native JS listener (reliable in MAUI WebView).
                    // Fall back to getFieldValue (direct DOM read) if listener wasn't attached.
                    var storedRemark = await JS.InvokeAsync<string?>("getStoredRemark");
                    var domRemark = storedRemark
                        ?? await JS.InvokeAsync<string?>("getFieldValue", "product-remark-input");
                    if (domRemark != null)
                        newItem.Remarks = domRemark;
                }
                catch { /* JS not available */ }

                // ── Step 2: Validate ─────────────────────────────────────────────────
                if (string.IsNullOrWhiteSpace(newItem.AccountName) && string.IsNullOrWhiteSpace(newItem.ItemGroupName))
                {
                    errorTitle = "Missing Information";
                    errorMessage = "Name and Select Section are compulsory fields. Please fill them in before saving.";
                    isErrorPopupOpen = true;
                    return;
                }
                if (string.IsNullOrWhiteSpace(newItem.AccountName))
                {
                    errorTitle = "Missing Information";
                    errorMessage = "Name is a compulsory field. Please enter a product name before saving.";
                    isErrorPopupOpen = true;
                    return;
                }
                if (string.IsNullOrWhiteSpace(newItem.ItemGroupName))
                {
                    errorTitle = "Missing Information";
                    errorMessage = "Select Section is a compulsory field. Please select a section before saving.";
                    isErrorPopupOpen = true;
                    return;
                }

                // ── Step 3: Token check ──────────────────────────────────────────────
                var token = await StoreTokenService.GetTokenAsync();
                if (string.IsNullOrEmpty(token))
                {
                    errorTitle = "Session Expired";
                    errorMessage = "Your login session has expired. Please go back and log in again.";
                    isErrorPopupOpen = true;
                    return;
                }

                // ── Step 4: Build membership credit string ──────────────────────────
                // Format: "MemberTypeID1,500|MemberTypeID2,1000|MemberTypeID3,200"
                var membershipCreditString = "";
                var validCredits = membershipCredits
                    .Where(mc => mc.CreditAmount > 0 && !string.IsNullOrEmpty(mc.MemberTypeID))
                    .ToList();

                if (validCredits.Any())
                {
                    var creditParts = new List<string>();
                    foreach (var mc in validCredits)
                    {
                        var typeId = !string.IsNullOrEmpty(mc.MemberTypeID)
                            ? mc.MemberTypeID
                            : _membershipTypes.FirstOrDefault(t => t.MemberTypeName == mc.MemberTypeName)?.MemberTypeID ?? mc.MemberTypeName;
                        creditParts.Add($"{typeId},{mc.CreditAmount.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture)}");
                    }
                    membershipCreditString = string.Join("|", creditParts);
                }

                // ── Step 5: (Price group and promotion overrides applied dynamically at billing) ──


                // ── Step 6: Call API ─────────────────────────────────────────────────
                (bool success, string message) result;

                if (ViewType == "Package" || ViewType == "TopUp")
                {
                    var branch = !string.IsNullOrEmpty(newItem.BranchID) ? newItem.BranchID : _defaultBranchID;

                    // Package item lines
                    var allPkgEntries = packageItems.Select(p => new PackageItemEntry
                    {
                        autoID = string.IsNullOrEmpty(p.AutoID) ? null : p.AutoID,
                        packageID = !string.IsNullOrEmpty(p.PackageLineID) ? p.PackageLineID
                                    : (isEditingExistingItem ? newItem.MasterAccountID : null),
                        inventoryID = p.MasterAccountID,
                        description = p.Name,
                        quantity = p.Qty,
                        unitPrice = p.Price,
                        totalPrice = p.Price * p.Qty,
                        unitActualValue = p.Price,
                        totalActualValue = p.Price * p.Qty,
                        inventoryTypeID = p.ItemType == "Service" ? 3 : p.ItemType == "Package" ? 5 : p.ItemType == "TopUp" ? 7 : 1,
                        isDeferred = false,
                        packageQuantityTypeID = 0,
                        saveAction = string.IsNullOrEmpty(p.AutoID) ? "Added" : "Changed",
                        isDirty = true
                    }).Concat(_serverLoadedPackageItems
                        .Where(orig => !string.IsNullOrEmpty(orig.AutoID) &&
                                       !packageItems.Any(cur => cur.AutoID == orig.AutoID))
                        .Select(p => new PackageItemEntry
                        {
                            autoID = p.AutoID,
                            packageID = string.IsNullOrEmpty(p.PackageLineID) ? null : p.PackageLineID,
                            inventoryID = p.MasterAccountID,
                            description = p.Name,
                            quantity = p.Qty,
                            unitPrice = p.Price,
                            totalPrice = p.Price * p.Qty,
                            unitActualValue = p.Price,
                            totalActualValue = p.Price * p.Qty,
                            inventoryTypeID = p.ItemType == "Service" ? 3 : 1,
                            packageQuantityTypeID = 0,
                            saveAction = "Deleted",
                            isDirty = true
                        })).ToList();

                    if (isEditingExistingItem && !string.IsNullOrEmpty(newItem.MasterAccountID))
                    {
                        var objInv = BuildInventoryCreateModel(newItem, "Changed");
                        objInv.lstPackage = allPkgEntries;
                        objInv.lstMembershipCredit = null;
                        objInv.membershipCredit = membershipCreditString;
                        objInv.validityDays = expiryDays;
                        objInv.IsOpenTopUp = isOpenTopUp;
                        objInv.triggeredMemberTypeID = string.IsNullOrEmpty(selectedNonCreditMemberTypeID) ? null : selectedNonCreditMemberTypeID;
                        objInv.triggeredMemberTypeName = string.IsNullOrEmpty(selectedNonCreditMemberTypeName) ? null : selectedNonCreditMemberTypeName;

                        var req = new InventoryFullCreateRequest
                        {
                            objInventory = objInv,
                            lstMasterAccount_Branch = BuildBranchEntries(newItem.MasterAccountID),
                        };
                        result = await InventoryService.UpdatePackageFullAsync(req);
                        // api/InventoryFull/Update ignores Remarks and UOM name; patch via full-payload update.
                        if (result.success)
                            await InventoryService.UpdateRemarksPascalAsync(
                                newItem.MasterAccountID, newItem.AccountName, newItem.InventoryTypeID,
                                newItem.ItemGroupID, newItem.Remarks ?? "",
                                newItem.UnitOfMeasureID, newItem.UnitOfMeasureName,
                                _redeemPoint > 0 ? _redeemPoint : (decimal?)null,
                                _redeemPoint > 0);
                    }
                    else
                    {
                        // Step 1: header + credits + branch (no items yet)
                        var objInv1 = BuildInventoryCreateModel(newItem, "Added");
                        objInv1.lstPackage = new List<PackageItemEntry>();
                        objInv1.lstMembershipCredit = null;
                        objInv1.membershipCredit = membershipCreditString;
                        objInv1.validityDays = expiryDays;
                        objInv1.IsOpenTopUp = isOpenTopUp;
                        objInv1.triggeredMemberTypeID = string.IsNullOrEmpty(selectedNonCreditMemberTypeID) ? null : selectedNonCreditMemberTypeID;
                        objInv1.triggeredMemberTypeName = string.IsNullOrEmpty(selectedNonCreditMemberTypeName) ? null : selectedNonCreditMemberTypeName;

                        var req1 = new InventoryFullCreateRequest
                        {
                            objInventory = objInv1,
                            lstMasterAccount_Branch = BuildBranchEntries(null),
                        };
                        var (s1, m1, newId) = await InventoryService.SavePackageFullAsync(req1);
                        result = (s1, m1);

                        // Step 2: attach package items using the server-assigned ID
                        if (s1 && !string.IsNullOrEmpty(newId) && allPkgEntries.Any())
                        {
                            foreach (var entry in allPkgEntries)
                                entry.packageID = newId;

                            var objInv2 = BuildInventoryCreateModel(newItem, "Changed");
                            objInv2.masterAccountID = newId;
                            objInv2.lstPackage = allPkgEntries;
                            objInv2.lstMembershipCredit = null;
                            objInv2.membershipCredit = membershipCreditString;
                            objInv2.validityDays = expiryDays;
                            objInv2.IsOpenTopUp = isOpenTopUp;
                            objInv2.triggeredMemberTypeID = string.IsNullOrEmpty(selectedNonCreditMemberTypeID) ? null : selectedNonCreditMemberTypeID;
                            objInv2.triggeredMemberTypeName = string.IsNullOrEmpty(selectedNonCreditMemberTypeName) ? null : selectedNonCreditMemberTypeName;

                            var req2 = new InventoryFullCreateRequest
                            {
                                objInventory = objInv2,
                                lstMasterAccount_Branch = new List<MasterAccountBranchEntry>()
                            };
                            var (s2, m2, _) = await InventoryService.SavePackageFullAsync(req2);
                            result = (s2, m2);
                        }

                        // Ensure remarks and UOM name are persisted — api/InventoryFull/Create "Added" may not save them.
                        if (result.success && !string.IsNullOrEmpty(newId))
                            await InventoryService.UpdateRemarksPascalAsync(
                                newId, newItem.AccountName, newItem.InventoryTypeID,
                                newItem.ItemGroupID, newItem.Remarks ?? "",
                                newItem.UnitOfMeasureID, newItem.UnitOfMeasureName,
                                _redeemPoint > 0 ? _redeemPoint : (decimal?)null,
                                _redeemPoint > 0);

                        if (!isEditingExistingItem && result.success)
                        {
                            _suppressNextCacheUpdate = true;
                            await LoadInventoryItemsAsync();
                        }
                    }
                }
                else if (isEditingExistingItem && !string.IsNullOrEmpty(newItem.MasterAccountID))
                {
                    var branch = !string.IsNullOrEmpty(newItem.BranchID) ? newItem.BranchID : _defaultBranchID;
                    var objInv = BuildInventoryCreateModel(newItem, "Changed");
                    objInv.hasPackage = false; // Product/Service: no package line processing
                    var req = new InventoryFullCreateRequest
                    {
                        objInventory = objInv,
                        lstMasterAccount_Branch = BuildBranchEntries(newItem.MasterAccountID),
                        PointToRedeem = _redeemPoint > 0 ? _redeemPoint : (decimal?)null,
                        AllowPointRedemption = _redeemPoint > 0
                    };
                    // Use SavePackageFullAsync (api/InventoryFull/Create with saveAction "Changed")
                    // because api/InventoryFull/Update silently ignores the Remarks field.
                    var (rOk, rMsg, _) = await InventoryService.SavePackageFullAsync(req);
                    result = (rOk, rMsg);
                    if (rOk)
                    {
                        // Also call api/InventoryFull/Update — server may read PointToRedeem from root
                        // of request (matching LoadRecord response structure) rather than from objInventory.
                        var rpEditModel = BuildInventoryCreateModel(newItem, "Changed");
                        rpEditModel.hasPackage = false;
                        await InventoryService.UpdatePackageFullAsync(new InventoryFullCreateRequest
                        {
                            objInventory = rpEditModel,
                            lstMasterAccount_Branch = BuildBranchEntries(newItem.MasterAccountID),
                            PointToRedeem = _redeemPoint > 0 ? _redeemPoint : (decimal?)null,
                            AllowPointRedemption = _redeemPoint > 0
                        });

                        // api/InventoryFull/Create "Changed" may not persist stockReorderLevel (duration).
                        // Send a direct api/Inventory/Update to ensure it is saved — same pattern used for new items.
                        var directEditModel = BuildInventoryCreateModel(newItem, "Changed");
                        directEditModel.hasPackage = false;
                        await InventoryService.UpdateInventoryDirectAsync(directEditModel);

                        var svcStockLevel = ViewType == "Service" ? (decimal?)(_duration.HasValue ? (decimal)_duration.Value : 0m) : null;
                        await InventoryService.UpdateRemarksPascalAsync(
                            newItem.MasterAccountID, newItem.AccountName, newItem.InventoryTypeID,
                            newItem.ItemGroupID, newItem.Remarks ?? "",
                            newItem.UnitOfMeasureID, newItem.UnitOfMeasureName,
                            _redeemPoint > 0 ? _redeemPoint : (decimal?)null,
                            _redeemPoint > 0,
                            svcStockLevel);
                    }
                }
                else
                {
                    var branch = !string.IsNullOrEmpty(newItem.BranchID) ? newItem.BranchID : _defaultBranchID;
                    var objInv = BuildInventoryCreateModel(newItem, "Added");
                    objInv.hasPackage = false;
                    var req = new InventoryFullCreateRequest
                    {
                        objInventory = objInv,
                        lstMasterAccount_Branch = BuildBranchEntries(null),
                        PointToRedeem = _redeemPoint > 0 ? _redeemPoint : (decimal?)null,
                        AllowPointRedemption = _redeemPoint > 0
                    };
                    var (s, m, newId) = await InventoryService.SavePackageFullAsync(req);
                    result = (s, m);
                    if (s && !string.IsNullOrEmpty(newId))
                    {
                        // Also call api/InventoryFull/Update — server may read PointToRedeem from root
                        // of request (matching LoadRecord response structure) rather than from objInventory.
                        var rpNewModel = BuildInventoryCreateModel(newItem, "Changed");
                        rpNewModel.masterAccountID = newId;
                        rpNewModel.hasPackage = false;
                        await InventoryService.UpdatePackageFullAsync(new InventoryFullCreateRequest
                        {
                            objInventory = rpNewModel,
                            lstMasterAccount_Branch = BuildBranchEntries(newId),
                            PointToRedeem = _redeemPoint > 0 ? _redeemPoint : (decimal?)null,
                            AllowPointRedemption = _redeemPoint > 0
                        });

                        var directModel = BuildInventoryCreateModel(newItem, "Changed");
                        directModel.masterAccountID = newId;
                        directModel.hasPackage = false;
                        await InventoryService.UpdateInventoryDirectAsync(directModel);
                        var newSvcStockLevel = ViewType == "Service" ? (decimal?)(_duration.HasValue ? (decimal)_duration.Value : 0m) : null;
                        await InventoryService.UpdateRemarksPascalAsync(
                            newId, newItem.AccountName, newItem.InventoryTypeID,
                            newItem.ItemGroupID, newItem.Remarks ?? "",
                            newItem.UnitOfMeasureID, newItem.UnitOfMeasureName,
                            _redeemPoint > 0 ? _redeemPoint : (decimal?)null,
                            _redeemPoint > 0,
                            newSvcStockLevel);
                    }
                }

                if (result.success)
                {
                    var notifIcon = ViewType == "Service" ? "✂️" : ViewType == "Package" ? "📦" : ViewType == "TopUp" ? "💳" : "🛍️";
                    if (string.IsNullOrEmpty(successMessage))
                        successMessage = isEditingExistingItem
                            ? LangSvc.GetText($"{ViewType}UpdatedSuccess")
                            : LangSvc.GetText($"{ViewType}CreatedSuccess");

                    if (isEditingExistingItem && !string.IsNullOrEmpty(newItem.MasterAccountID))
                    {
                        NotificationSvc.Add(new AppNotification
                        {
                            Icon = notifIcon,
                            TitleKey = $"Notif{ViewType}UpdatedTitle",
                            MessageKey = $"Notif{ViewType}UpdatedMsg",
                            MessageParam = newItem.AccountName
                        });
                        await JS.InvokeVoidAsync("localStorage.setItem", "app_notifications", NotificationSvc.ToJson());

                        if (!string.IsNullOrEmpty(_tempImagePreview))
                        {
                            CacheService.StoreImage(newItem.MasterAccountID, _tempImagePreview);
                            LocalImageCache.StoreImage(newItem.MasterAccountID, _tempImagePreview);
                        }

                        var idx = menuDb.FindIndex(x => x.MasterAccountID == newItem.MasterAccountID);
                        if (idx >= 0)
                            menuDb[idx] = newItem;

                        var catForPatch = _apiCategories.FirstOrDefault(c =>
                            string.Equals(c.SupportingTableName, newItem.ItemGroupName, StringComparison.OrdinalIgnoreCase));

                        CacheService.PatchCachedItem(newItem.MasterAccountID, (InventoryDM cached) =>
                        {
                            cached.AccountName = newItem.AccountName;
                            cached.ItemGroupID = catForPatch?.SupportingTableID ?? newItem.ItemGroupID;
                            cached.ItemGroupName = newItem.ItemGroupName;
                            cached.SalesDescription = newItem.SalesDescription;
                            cached.VendorItemCode = _barcode;
                            cached.SalesPrice = newItem.SalesPrice;
                            cached.PurchasePrice = newItem.PurchasePrice;
                            cached.TaxCodeID = newItem.TaxCodeID;
                            cached.IsTaxInclusive = newItem.IsTaxInclusive;
                            cached.IsSold = true;
                            cached.AccountStatus = newItem.AccountStatus;
                            cached.UnitOfMeasureID = !string.IsNullOrEmpty(newItem.UnitOfMeasureID) ? newItem.UnitOfMeasureID : newItem.UnitOfMeasureName;
                            cached.UnitOfMeasureName = newItem.UnitOfMeasureName;
                            cached.StockReorderLevel = newItem.StockReorderLevel;
                            cached.BranchID = !string.IsNullOrEmpty(newItem.BranchID) ? newItem.BranchID : _defaultBranchID;
                            if (newItem.InventoryTypeID > 0) cached.InventoryTypeID = newItem.InventoryTypeID;
                            cached.Remarks = newItem.Remarks;
                        });

                        // Persist remark locally so it survives app restarts
                        // (the EBI API does not reliably save the Remarks field).
                        try { await JS.InvokeVoidAsync("storeItemRemark", newItem.MasterAccountID, newItem.Remarks ?? ""); } catch { }

                        // Persist Price Group and Promotion assignments
                        try { await PriceGroupService.AssignProductToPriceGroupAsync(newItem.MasterAccountID, selectedPriceGroupCode); } catch { }
                        try { await PromotionSetupService.AssignProductToPromotionAsync(newItem.MasterAccountID, selectedPromoCode); } catch { }

                        _suppressNextCacheUpdate = true;
                    }
                    else
                    {
                        NotificationSvc.Add(new AppNotification
                        {
                            Icon = notifIcon,
                            TitleKey = $"NotifNew{ViewType}Title",
                            MessageKey = $"NotifNew{ViewType}Msg",
                            MessageParam = newItem.AccountName
                        });
                        await JS.InvokeVoidAsync("localStorage.setItem", "app_notifications", NotificationSvc.ToJson());
                        _suppressNextCacheUpdate = true;
                        await LoadInventoryItemsAsync();

                        // Persist remark and Price Group for new items after the list refresh gives us the server ID.
                        var createdItem = menuDb.FirstOrDefault(x =>
                            string.Equals(x.AccountName, newItem.AccountName, StringComparison.OrdinalIgnoreCase) &&
                            !string.IsNullOrEmpty(x.MasterAccountID));

                        if (createdItem != null && !string.IsNullOrEmpty(createdItem.MasterAccountID))
                        {
                            if (!string.IsNullOrEmpty(newItem.Remarks))
                                try { await JS.InvokeVoidAsync("storeItemRemark", createdItem.MasterAccountID, newItem.Remarks); } catch { }

                            try { await PriceGroupService.AssignProductToPriceGroupAsync(createdItem.MasterAccountID, selectedPriceGroupCode); } catch { }
                            try { await PromotionSetupService.AssignProductToPromotionAsync(createdItem.MasterAccountID, selectedPromoCode); } catch { }

                            if (!string.IsNullOrEmpty(_tempImagePreview))
                            {
                                CacheService.StoreImage(createdItem.MasterAccountID, _tempImagePreview);
                                LocalImageCache.StoreImage(createdItem.MasterAccountID, _tempImagePreview);
                            }
                        }
                    }

                    isAddMode = false;
                    isSuccessPopupOpen = true;
                }
                else
                {
                    errorTitle = "Save Failed";
                    var apiMsg = string.IsNullOrWhiteSpace(result.message)
                        ? "No response from server."
                        : result.message;

                    if (apiMsg.Contains("401", StringComparison.OrdinalIgnoreCase) ||
                        apiMsg.Contains("Unauthorized", StringComparison.OrdinalIgnoreCase) ||
                        apiMsg.Contains("expired", StringComparison.OrdinalIgnoreCase))
                    {
                        errorTitle = "Session Expired";
                        errorMessage = "Your login session has expired. Please go back and log in again.";
                    }
                    else
                    {
                        errorMessage = $"Unable to save. Server replied: {apiMsg}";
                    }
                    isErrorPopupOpen = true;
                }
            }
            finally { _isSaving = false; }
        }

        private void CloseErrorPopup() => isErrorPopupOpen = false;
        private void CloseSuccessPopup() => isSuccessPopupOpen = false;

        private void ClearImage()
        {
            _tempImagePreview = "";
            _imageName = "";
            newItem.ImagePath = null;
            newItem.ImageFileName = null;
            // Also remove from both caches if editing an existing item.
            if (!string.IsNullOrEmpty(newItem.MasterAccountID))
            {
                CacheService.RemoveImage(newItem.MasterAccountID);
                LocalImageCache.RemoveImage(newItem.MasterAccountID);
            }
        }

        private async System.Threading.Tasks.Task HandleFileSelected(InputFileChangeEventArgs e)
        {
            if (e.File != null)
            {
                var format = "image/png";
                var resizedImage = await e.File.RequestImageFileAsync(format, 400, 400);
                var buffer = new byte[resizedImage.Size];

                using var stream = resizedImage.OpenReadStream();
                int bytesRead = 0;
                while (bytesRead < buffer.Length)
                {
                    var read = await stream.ReadAsync(buffer, bytesRead, buffer.Length - bytesRead);
                    if (read == 0) break;
                    bytesRead += read;
                }

                _tempImagePreview = $"data:{format};base64,{Convert.ToBase64String(buffer)}";
                _imageName = e.File.Name;
            }
        }

        private void ToggleCategoryDropdown() { isCategoryDropdownOpen = !isCategoryDropdownOpen; }
        private void SelectCategoryFromDropdown(string cat)
        {
            newItem.ItemGroupName = cat;
            newItem.ItemGroupID = _apiCategories.FirstOrDefault(c => string.Equals(c.SupportingTableName, cat, StringComparison.OrdinalIgnoreCase))?.SupportingTableID;
            isItemGroupDropdownOpen = false;
            _itemGroupSearchQuery = "";
        }

        private async System.Threading.Tasks.Task DownloadExcel()
        {
            try
            {
                var dataToExport = FilteredItems.Any() ? FilteredItems : menuDb.Where(x => GetItemType(x.InventoryTypeID) == ViewType);

                if (!dataToExport.Any())
                {
                    errorTitle = "No Data";
                    errorMessage = "No data available to export.";
                    isErrorPopupOpen = true;
                    return;
                }

                // Build CSV content
                var csv = new StringBuilder();
                csv.Append("\uFEFF"); // UTF-8 BOM for Excel compatibility

                // Report Summary Section
                csv.AppendLine($"\"Product Export Summary\",,");
                csv.AppendLine($"\"Type\",\"{ViewTypeDisplay}\",");
                csv.AppendLine($"\"Total Items\",,{dataToExport.Count()}");
                csv.AppendLine($"\"Exported On\",\"{DateTime.Now:dd/MM/yyyy HH:mm:ss}\",");

                // Add filter information if filters are active
                var hasFilters = !string.IsNullOrWhiteSpace(filterSearchQuery) ||
                                filterActiveStatus != "All" ||
                                filterCategory != "All";

                if (hasFilters)
                {
                    csv.AppendLine($"\"Filters Applied\",,");
                    if (!string.IsNullOrWhiteSpace(filterSearchQuery))
                        csv.AppendLine($"\"  - Search\",\"{EscapeCsvValue(filterSearchQuery)}\",");
                    if (filterActiveStatus != "All")
                        csv.AppendLine($"\"  - Status\",\"{filterActiveStatus}\",");
                    if (filterCategory != "All")
                        csv.AppendLine($"\"  - Category\",\"{EscapeCsvValue(filterCategory)}\",");
                }

                // Empty line separator
                csv.AppendLine();

                // Data Header
                csv.AppendLine($"\"Name\",\"Section\",\"Tax Code\",\"Tax Inclusive\",\"Price\",\"Cost\",\"Barcode\",\"UOM\",\"Item Code\"");

                // Data rows
                foreach (var item in dataToExport)
                {
                    csv.AppendLine($"\"{EscapeCsvValue(item.AccountName ?? "")}\",\"{EscapeCsvValue(item.ItemGroupName ?? "")}\",\"{EscapeCsvValue(item.TaxCodeID ?? "")}\",\"{(item.IsTaxInclusive ? "Yes" : "No")}\",{item.SalesPrice.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture)},{item.PurchasePrice.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture)},\"{EscapeCsvValue(item.VendorItemCode ?? "")}\",\"{EscapeCsvValue(item.UnitOfMeasureName ?? "")}\",\"{EscapeCsvValue(item.DisplayCode ?? "")}\"");
                }

                var fileName = $"{ViewType}_{DateTime.Now:yyyyMMdd_HHmmss}.csv";
                var csvContent = csv.ToString();

                // Convert string content to Base64
                var csvBytes = Encoding.UTF8.GetBytes(csvContent);
                var base64Content = Convert.ToBase64String(csvBytes);

                // Use the file download service instead of JS
                await FileDownloadService.DownloadBinaryFileAsync(fileName, base64Content, "text/csv");

                // Show success message
                successMessage = $"Exported {dataToExport.Count()} items to {fileName}";
                isSuccessPopupOpen = true;
            }
            catch (Exception ex)
            {
                errorTitle = "Export Failed";
                errorMessage = $"Error exporting data: {ex.Message}";
                isErrorPopupOpen = true;
            }
        }

        private string EscapeCsvValue(string input)
        {
            if (string.IsNullOrEmpty(input)) return string.Empty;
            return input.Replace("\"", "\"\"");
        }

        private void RequestDelete(InventoryDM item) { _itemToDelete = item; isDeleteConfirmOpen = true; }

        private void RequestDeleteSection(SectionModel section) { _sectionToDelete = section; isSectionDeleteConfirmOpen = true; }

        private async System.Threading.Tasks.Task ConfirmDeleteSection()
        {
            if (_sectionToDelete == null) { isSectionDeleteConfirmOpen = false; return; }

            if (!string.IsNullOrEmpty(_sectionToDelete.SupportingTableID))
            {
                var token = await StoreTokenService.GetTokenAsync();
                if (string.IsNullOrEmpty(token))
                {
                    errorTitle = "Session Expired";
                    errorMessage = "Your login session has expired. Please go back and log in again.";
                    isErrorPopupOpen = true;
                    isSectionDeleteConfirmOpen = false;
                    return;
                }

                var (success, message) = await SupportingTableService.DeleteAsync(_sectionToDelete.SupportingTableID);
                if (success)
                {
                    // Remove immediately so the UI reflects the deletion right away,
                    // even if the subsequent API reload is slow or returns null.
                    sectionsDb.Remove(_sectionToDelete);
                    // Prevent the in-flight background refresh from restoring the
                    // deleted section via stale cache data (categories have no version guard).
                    _suppressNextCacheUpdate = true;
                    await LoadCategoriesAsync();
                }
                else
                {
                    var isAuthError = !string.IsNullOrWhiteSpace(message) &&
                        (message.Contains("401") || message.Contains("Unauthorized", StringComparison.OrdinalIgnoreCase) || message.Contains("expired", StringComparison.OrdinalIgnoreCase));
                    errorTitle = isAuthError ? "Session Expired" : "Delete Failed";
                    errorMessage = isAuthError
                        ? "Your login session has expired. Please go back and log in again."
                        : (string.IsNullOrWhiteSpace(message) ? "Unable to delete section. Please try again." : message);
                    isErrorPopupOpen = true;
                }
            }
            else
            {
                sectionsDb.Remove(_sectionToDelete);
            }

            _sectionToDelete = null;
            isSectionDeleteConfirmOpen = false;
        }

        private async System.Threading.Tasks.Task ConfirmDelete()
        {
            if (_itemToDelete == null) { isDeleteConfirmOpen = false; return; }

            if (!string.IsNullOrEmpty(_itemToDelete.MasterAccountID))
            {
                var token = await StoreTokenService.GetTokenAsync();
                if (string.IsNullOrEmpty(token))
                {
                    errorTitle = "Session Expired";
                    errorMessage = "Your login session has expired. Please go back and log in again.";
                    isErrorPopupOpen = true;
                    isDeleteConfirmOpen = false;
                    return;
                }

                var (success, message) = await InventoryService.DeleteItemAsync(_itemToDelete.MasterAccountID);
                if (success)
                {
                    menuDb.Remove(_itemToDelete);
                    CacheService.RemoveLocalPatch(_itemToDelete.MasterAccountID);
                    CacheService.RemoveImage(_itemToDelete.MasterAccountID);
                    LocalImageCache.RemoveImage(_itemToDelete.MasterAccountID);
                }
                else
                {
                    var isAuthError = !string.IsNullOrWhiteSpace(message) &&
                        (message.Contains("401") || message.Contains("Unauthorized", StringComparison.OrdinalIgnoreCase) || message.Contains("expired", StringComparison.OrdinalIgnoreCase));
                    errorTitle = isAuthError ? "Session Expired" : "Delete Failed";
                    errorMessage = isAuthError
                        ? "Your login session has expired. Please go back and log in again."
                        : (string.IsNullOrWhiteSpace(message) ? "Unable to delete item. Please try again." : message);
                    isErrorPopupOpen = true;
                }
            }
            else
            {
                menuDb.Remove(_itemToDelete);
            }

            _itemToDelete = null;
            isDeleteConfirmOpen = false;
        }

        private async System.Threading.Tasks.Task ShowItemDetails(InventoryDM item)
        {
            isEditingExistingItem = true;
            activeAddTab = "Info";
            packageItems.Clear();
            membershipCredits.Clear();
            expiryDays = 0;
            isOpenTopUp = false;
            selectedNonCreditMemberTypeID = "";
            selectedNonCreditMemberTypeName = "";
            newItem = new InventoryDM
            {
                MasterAccountID = item.MasterAccountID,
                AccountName = item.AccountName,
                InventoryTypeID = item.InventoryTypeID > 0 ? item.InventoryTypeID : 1,
                ItemGroupID = item.ItemGroupID,
                ItemGroupName = item.ItemGroupName,
                BranchID = item.BranchID,
                SalesPrice = item.SalesPrice,
                PurchasePrice = item.PurchasePrice,
                TaxCodeID = item.TaxCodeID,
                IsTaxInclusive = item.IsTaxInclusive,
                AccountStatus = item.AccountStatus,
                UnitOfMeasureID = item.UnitOfMeasureID,
                UnitOfMeasureName = item.UnitOfMeasureName,
                StockReorderLevel = item.StockReorderLevel,
                DisplayCode = item.DisplayCode,
                VendorItemCode = item.VendorItemCode,
                Remarks = item.Remarks,
                ImagePath = item.ImagePath,
                ImageFileName = item.ImageFileName,
                SalesDescription = item.SalesDescription,
                IsSold = item.IsSold
            };
            _commission1 = 0; _commission2 = 0; _commission3 = 0;
            _commission1IsPercent = true; _commission2IsPercent = true; _commission3IsPercent = true;
            _duration = ViewType == "Service" && item.StockReorderLevel > 0 ? (int?)item.StockReorderLevel : null;
            _redeemPoint = 0;
            _barcode = item.VendorItemCode ?? "";
            _billOfMaterial = "";
            _imageName = "";
            _originalSalesPrice = item.SalesPrice;
            selectedPriceGroupCode = "";
            isMinMaxPriceEnabled = false;
            isRedeemPointEnabled = false;
            selectedOutletCodes.Clear();
            foreach (var o in outletList) selectedOutletCodes.Add(o.Code);
            _visibleAtBranchIds.Clear();
            foreach (var id in AppState.AvailableBranches) _visibleAtBranchIds.Add(id);
            selectedBomItems.Clear();
            isAddMode = true;

            // Load locally stored image if available (overrides API URL)
            var localImg = CacheService.GetImage(item.MasterAccountID ?? "")
                ?? LocalImageCache.GetImage(item.MasterAccountID ?? "");
            _tempImagePreview = !string.IsNullOrEmpty(localImg) ? localImg : GetDisplayImageUrl(item);

            // Try to load full item record from API (gets BillOfMaterial + other detail fields)
            if (!string.IsNullOrEmpty(item.MasterAccountID))
            {
                try
                {
                    var detail = await InventoryService.LoadItemAsync(item.MasterAccountID);
                    if (detail != null)
                    {
                        // Refresh branch/unit IDs from authoritative record in case proxy omitted them
                        if (!string.IsNullOrEmpty(detail.BranchID)) newItem.BranchID = detail.BranchID;
                        if (!string.IsNullOrEmpty(detail.UnitOfMeasureID)) newItem.UnitOfMeasureID = detail.UnitOfMeasureID;
                        if (!string.IsNullOrEmpty(detail.UnitOfMeasureName)) newItem.UnitOfMeasureName = detail.UnitOfMeasureName;
                        // displayCode = Item Code in POS; vendorItemCode = Barcode in POS
                        if (!string.IsNullOrEmpty(detail.DisplayCode)) newItem.DisplayCode = detail.DisplayCode;
                        if (!string.IsNullOrEmpty(detail.VendorItemCode)) _barcode = detail.VendorItemCode;
                        if (detail.PurchasePrice > 0) newItem.PurchasePrice = detail.PurchasePrice;
                        // LoadRecord may omit Remarks; will fall back to InventoryFull/LoadRecord below
                        if (!string.IsNullOrEmpty(detail.Remarks)) newItem.Remarks = detail.Remarks;
                        // Load commission formulas from InventoryDM
                        (_commission1, _commission1IsPercent) = ParseCommissionFormula(detail.StaffCommissionA);
                        (_commission2, _commission2IsPercent) = ParseCommissionFormula(detail.StaffCommissionB);
                        (_commission3, _commission3IsPercent) = ParseCommissionFormula(detail.StaffCommissionC);
                        // Proxy list may omit or cache a stale StockReorderLevel — always prefer detail
                        if (detail.StockReorderLevel > 0)
                        {
                            newItem.StockReorderLevel = detail.StockReorderLevel;
                            if (ViewType == "Service") _duration = (int)detail.StockReorderLevel;
                        }
                    }
                }
                catch { /* non-critical — proceed with proxy data */ }

                // Always load the full record — needed for remarks, branch visibility, redeem point,
                // and package/topup details.
                try
                {
                    var full = await InventoryService.LoadFullPackageDetailAsync(item.MasterAccountID);
                    var inner = full?.objInventory ?? full;

                    // Remarks (api/Inventory/LoadRecord omits them)
                    if (string.IsNullOrEmpty(newItem.Remarks) && !string.IsNullOrEmpty(inner?.remarks))
                        newItem.Remarks = inner.remarks;

                    // Branch visibility — load actual enabled branches from the server
                    var branchList = full?.lstMasterAccount_Branch ?? inner?.lstMasterAccount_Branch;
                    if (branchList != null && branchList.Any())
                    {
                        _visibleAtBranchIds.Clear();
                        foreach (var b in branchList.Where(b => b.isEnabled && !string.IsNullOrEmpty(b.branchID)))
                            _visibleAtBranchIds.Add(b.branchID);
                    }

                    // Redeem point — stored as PointToRedeem inside objInventory
                    var rp = inner?.PointToRedeem ?? full?.PointToRedeem;
                    if (rp > 0) { _redeemPoint = rp.Value; isRedeemPointEnabled = true; }

                    // Package / TopUp specific data
                    if ((ViewType == "Package" || ViewType == "TopUp") && inner != null)
                    {
                        expiryDays = inner.validityDays;
                        isOpenTopUp = inner.IsOpenTopUp;
                        if (!string.IsNullOrEmpty(inner.remarks))
                            newItem.Remarks = inner.remarks;

                        if (!string.IsNullOrEmpty(inner.triggeredMemberTypeID))
                        {
                            selectedNonCreditMemberTypeID = inner.triggeredMemberTypeID;
                            selectedNonCreditMemberTypeName = _membershipTypes.FirstOrDefault(t => t.MemberTypeID == inner.triggeredMemberTypeID)?.MemberTypeName
                                                               ?? inner.triggeredMemberTypeName ?? inner.triggeredMemberTypeID;
                        }
                        else
                        {
                            selectedNonCreditMemberTypeID = "";
                            selectedNonCreditMemberTypeName = "";
                        }

                        var packagesToLoad = (inner.lstPackage?.Any() == true) ? inner.lstPackage : full?.lstPackage;
                        if (packagesToLoad != null)
                        {
                            foreach (var p in packagesToLoad)
                            {
                                var existing = menuDb.FirstOrDefault(x => x.MasterAccountID == p.inventoryID);
                                packageItems.Add(new PackageItemLine
                                {
                                    AutoID = p.autoID ?? "",
                                    PackageLineID = p.packageID ?? "",
                                    MasterAccountID = p.inventoryID,
                                    Name = p.description ?? existing?.AccountName ?? "",
                                    ItemType = existing != null ? GetItemType(existing.InventoryTypeID) : (p.inventoryTypeID == 3 ? "Service" : "Product"),
                                    Qty = p.quantity,
                                    Price = p.unitPrice
                                });
                            }
                        }

                        if (!string.IsNullOrEmpty(inner.membershipCredit))
                        {
                            var seenTypes = new HashSet<string>();
                            var parts = inner.membershipCredit.Split('|', StringSplitOptions.RemoveEmptyEntries);
                            foreach (var part in parts)
                            {
                                var creditParts = part.Split(',', StringSplitOptions.RemoveEmptyEntries);
                                if (creditParts.Length == 2)
                                {
                                    var typeId = creditParts[0].Trim();
                                    if (!seenTypes.Contains(typeId) &&
                                        decimal.TryParse(creditParts[1], System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var amount) &&
                                        amount > 0)
                                    {
                                        seenTypes.Add(typeId);
                                        var typeName = _membershipTypes.FirstOrDefault(t => t.MemberTypeID == typeId)?.MemberTypeName ?? typeId;
                                        membershipCredits.Add(new MembershipCreditLine
                                        {
                                            MemberTypeID = typeId,
                                            MemberTypeName = typeName,
                                            CreditAmount = amount,
                                            IsFromServer = true
                                        });
                                    }
                                }
                            }
                        }

                        System.Diagnostics.Debug.WriteLine($"[ShowItemDetails] loaded {packageItems.Count} pkg items, {membershipCredits.Count} credits, expiry={expiryDays}, isOpenTopUp={isOpenTopUp}, nonCreditMemberType={selectedNonCreditMemberTypeName}");
                        _serverLoadedPackageItems = packageItems.ToList();
                        _serverLoadedMembershipCredits = membershipCredits.ToList();
                    }
                }
                catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"[ShowItemDetails] LoadFullPackageDetail exception: {ex.Message}"); }
            }

            // If the API didn't return a remark, restore from localStorage (our local backup).
            if (string.IsNullOrEmpty(newItem.Remarks) && !string.IsNullOrEmpty(item.MasterAccountID))
            {
                try
                {
                    var storedRemark = await JS.InvokeAsync<string?>("getItemRemark", item.MasterAccountID);
                    if (!string.IsNullOrEmpty(storedRemark))
                        newItem.Remarks = storedRemark;
                }
                catch { }
            }

            // Load Price Group and Promotion assignment
            if (!string.IsNullOrEmpty(item.MasterAccountID))
            {
                try
                {
                    selectedPriceGroupCode = await PriceGroupService.GetAssignedPriceGroupCodeAsync(item.MasterAccountID) ?? "";
                }
                catch { }

                try
                {
                    selectedPromoCode = await PromotionSetupService.GetAssignedPromotionCodeAsync(item.MasterAccountID) ?? "";
                }
                catch { }
            }

            isItemGroupDropdownOpen = false; 
            _itemGroupSearchQuery = ""; 
        }

        protected void SelectSec(string section) { selectedSec = section; }

        private void OpenBomPopup()
        {
            selectedBomItems.Clear();
            if (!string.IsNullOrEmpty(_billOfMaterial))
            {
                var names = _billOfMaterial.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
                foreach (var name in names) { var found = menuDb.FirstOrDefault(x => x.AccountName == name); if (found != null) selectedBomItems.Add(found); }
            }
            isBomPopupOpen = true;
        }

        private void CloseBomPopup(bool save) { if (save) UpdateBomString(); isBomPopupOpen = false; }
        private void OpenAddMaterialPopup() { _bomSnapshot = new List<InventoryDM>(selectedBomItems); bomSearchQuery = ""; bomSectionFilter = "All"; bomSortOption = "NameAsc"; isBomSortMenuOpen = false; isAddMaterialPopupOpen = true; }
        private void CloseAddMaterialPopup(bool save) { if (!save) selectedBomItems = new List<InventoryDM>(_bomSnapshot); isAddMaterialPopupOpen = false; }
        private void AddToBom(InventoryDM item) { if (!selectedBomItems.Any(x => x.MasterAccountID == item.MasterAccountID)) selectedBomItems.Add(item); }
        private void RemoveFromBom(InventoryDM item) { selectedBomItems.Remove(item); }
        private void UpdateBomString() { _billOfMaterial = string.Join(", ", selectedBomItems.Select(x => x.AccountName)); }
        private void ToggleBomSortMenu() { isBomSortMenuOpen = !isBomSortMenuOpen; }
        private void SelectBomSort(string sort) { bomSortOption = sort; isBomSortMenuOpen = false; }
        private void SelectBomSection(string section) { bomSectionFilter = section; }

        private IEnumerable<InventoryDM> GetFilteredBomMaterials()
        {
            var query = menuDb.AsEnumerable();
            if (bomSectionFilter != "All") query = query.Where(x => x.ItemGroupName == bomSectionFilter);
            if (!string.IsNullOrEmpty(bomSearchQuery)) query = query.Where(x => (x.AccountName ?? "").Contains(bomSearchQuery, StringComparison.OrdinalIgnoreCase));
            return bomSortOption switch { "NameAsc" => query.OrderBy(x => x.AccountName), "PriceAsc" => query.OrderBy(x => x.SalesPrice), "PriceDesc" => query.OrderByDescending(x => x.SalesPrice), _ => query.OrderBy(x => x.AccountName) };
        }

        // Unit Dropdown logic
        private void ToggleUnitDropdown() => isUnitDropdownOpen = !isUnitDropdownOpen;
        private void SelectUnit(string unit) { newItem.UnitOfMeasureName = unit; isUnitDropdownOpen = false; }

        // Keep only these - the per-staff version:
        private bool isCommissionTypeDropdownOpen = false;
        private string _activeCommissionStaff = "";

        private void ToggleCommissionTypeDropdown(string staff)
        {
            if (_activeCommissionStaff == staff && isCommissionTypeDropdownOpen)
            {
                isCommissionTypeDropdownOpen = false;
                _activeCommissionStaff = "";
            }
            else
            {
                _activeCommissionStaff = staff;
                isCommissionTypeDropdownOpen = true;
            }
        }

        private void SelectCommissionType(string staff, bool isPercent)
        {
            switch (staff)
            {
                case "Staff1":
                    _commission1IsPercent = isPercent;
                    break;
                case "Staff2":
                    _commission2IsPercent = isPercent;
                    break;
                case "Staff3":
                    _commission3IsPercent = isPercent;
                    break;
            }
            isCommissionTypeDropdownOpen = false;
            _activeCommissionStaff = "";
        }

        private void GoBack() => Navigation.NavigateTo("/home");

        // Sort state
        private string sortColumn = "Name";
        private bool sortAscending = true;

        private void SortBy(string column)
        {
            if (sortColumn == column)
            {
                // Toggle direction if same column
                sortAscending = !sortAscending;
            }
            else
            {
                // New column, default to ascending
                sortColumn = column;
                sortAscending = true;
            }
            ResetPage(); // Reset to first page when sorting changes
        }

        // For the main product pagination
        private void FirstPage() => _currentPage = 1;
        private void LastPage() => _currentPage = TotalPages;

        // For the section pagination
        private void SectionFirstPage() => _sectionCurrentPage = 1;
        private void SectionLastPage() => _sectionCurrentPage = TotalSectionPages;

        // Item Group Sort state
        private string itemGroupSortColumn = "Name"; 
        private bool itemGroupSortAscending = true;

        private void SortItemGroupBy(string column)
        {
            if (itemGroupSortColumn == column)
            {
                // Toggle direction if same column
                itemGroupSortAscending = !itemGroupSortAscending;
            }
            else
            {
                // New column, default to ascending
                itemGroupSortColumn = column;
                itemGroupSortAscending = true;
            }
            ResetSectionPage();
        }

        private bool isOpenTopUp = false;
        private string selectedNonCreditMemberTypeID = "";
        private string selectedNonCreditMemberTypeName = "";
        private bool _isNonCreditSelected = false;
        private bool _showNonCreditMemberPicker = false;

        private void OpenNonCreditMemberPicker()
        {
            _showNonCreditMemberPicker = true;
        }

        private void SelectNonCreditMemberTypeFromPicker(string mt)
        {
            var typeId = _membershipTypes.FirstOrDefault(t => t.MemberTypeName == mt)?.MemberTypeID ?? "";
            if (!string.IsNullOrEmpty(typeId))
            {
                selectedNonCreditMemberTypeID = typeId;
                selectedNonCreditMemberTypeName = mt;
            }
            _showNonCreditMemberPicker = false;
        }

        private void RemoveNonCreditMember()
        {
            if (_isNonCreditSelected)
            {
                selectedNonCreditMemberTypeID = "";
                selectedNonCreditMemberTypeName = "";
                _isNonCreditSelected = false;
            }
        }

        private string _categorySearchQuery = "";

        private List<string> FilteredCategories
        {
            get
            {
                var categories = Categories;
                if (!string.IsNullOrWhiteSpace(_categorySearchQuery))
                {
                    categories = categories
                        .Where(c => c.Contains(_categorySearchQuery, StringComparison.OrdinalIgnoreCase))
                        .ToList();
                }
                return categories;
            }
        }

        private bool isItemGroupDropdownOpen = false;
        private string _itemGroupSearchQuery = "";

        private void ToggleItemGroupDropdown()
        {
            isItemGroupDropdownOpen = !isItemGroupDropdownOpen;
            if (isItemGroupDropdownOpen)
            {
                _itemGroupSearchQuery = ""; 
            }
        }

    }
}