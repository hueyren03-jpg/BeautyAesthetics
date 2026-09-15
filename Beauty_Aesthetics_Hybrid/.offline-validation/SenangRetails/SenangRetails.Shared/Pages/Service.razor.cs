using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using EBI.DM;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.JSInterop;
using SenangRetails.Shared.Model;
using SenangRetails.Shared.Models;
using SenangRetails.Shared.Models.DTOs;
using SenangRetails.Shared.Services;
using SenangRetails.Shared.Services.AuthService;
using SenangRetails.Shared.Services.FileDownloadService;
using SenangRetails.Shared.Services.InventoryService;
using SenangRetails.Shared.Services.NotificationService;
using SenangRetails.Shared.Services.SupportingTableService;

namespace SenangRetails.Shared.Pages
{
    public partial class Service : BasePage
    {
        [Inject] public IJSRuntime JS { get; set; } = default!;
        [Inject] public IInventoryService InventoryService { get; set; } = default!;
        [Inject] public ISupportingTableService SupportingTableService { get; set; } = default!;
        [Inject] public IStoreTokenService StoreTokenService { get; set; } = default!;
        [Inject] public LocalImageCacheService LocalImageCache { get; set; } = default!;
        [Inject] public INotificationService NotificationSvc { get; set; } = default!;
        [Inject] public IFileDownloadService FileDownloadService { get; set; } = default!;

        [Parameter] public string ViewType { get; set; } = "Service";
        [Parameter] public EventCallback OnToggleSidebar { get; set; }

        private string SelectedSec { get; set; } = "All";
        private bool IsFilterVisible { get; set; } = false;
        private bool IsAddMode { get; set; } = false;
        private bool IsEditingExistingItem { get; set; } = false;
        private string ActiveAddTab { get; set; } = "Info";

        // Loading / saving state
        private bool IsLoading { get; set; } = false;
        private bool _isSaving = false;
        private bool _isSavingSection = false;

        // Popup States
        private bool IsCategoryPopupOpen { get; set; } = false;
        private bool IsUploadPopupOpen { get; set; } = false;
        private bool IsAmountPopupOpen { get; set; } = false;
        private bool IsDurationPopupOpen { get; set; } = false;
        private bool IsBarcodePopupOpen { get; set; } = false;
        private bool IsOutletPopupOpen { get; set; } = false;
        private bool IsFilterPopupOpen { get; set; } = false;

        // Dropdown State for Section and Unit
        private bool IsCategoryDropdownOpen { get; set; } = false;
        private bool IsUnitDropdownOpen { get; set; } = false;

        // BOM Popup States
        private bool IsBomPopupOpen { get; set; } = false;
        private bool IsAddMaterialPopupOpen { get; set; } = false;

        private List<ServiceItem> SelectedBomItems { get; set; } = new List<ServiceItem>();
        private List<ServiceItem> _bomSnapshot { get; set; } = new List<ServiceItem>();

        private string BomSearchQuery { get; set; } = "";
        private string BomSortOption { get; set; } = "NameAsc";
        private bool IsBomSortMenuOpen { get; set; } = false;
        private string BomSectionFilter { get; set; } = "All";

        // Toggles for Advance Tab
        private bool IsMinMaxPriceEnabled { get; set; } = false;
        private bool IsRedeemPointEnabled { get; set; } = false;

        // Calculator Logic
        private string _calculatorTarget { get; set; } = "Price";
        private string _tempAmountString = "0";
        private string _tempImagePreview = "";

        // Filter/Sort variables
        private string FilterSearchQuery { get; set; } = "";
        private string SortOption { get; set; } = "NameAsc";
        private string FilterActiveStatus { get; set; } = "All"; // "All", "Active", "Inactive"
        private string FilterSection { get; set; } = "All";

        // Error / success feedback
        private bool IsErrorPopupOpen { get; set; } = false;
        private string ErrorMessage { get; set; } = "";
        private string ErrorTitle { get; set; } = "Missing Information";
        private bool IsSuccessPopupOpen { get; set; } = false;
        private string SuccessMessage { get; set; } = "";

        // Delete
        private bool IsDeleteConfirmOpen { get; set; } = false;
        private ServiceItem? _itemToDelete;

        private ServiceItem NewItem { get; set; } = new ServiceItem();

        // Branch ID captured from loaded items — required when creating new items
        private string _defaultBranchID = string.Empty;

        // Section Settings Data
        public class SectionModel
        {
            public Guid Id { get; set; } = Guid.NewGuid();
            /// <summary>API SupportingTableID. Populated after loading from API.</summary>
            public string SupportingTableID { get; set; } = "";
            public string Name { get; set; } = "";
            public string AdditionalTitle { get; set; } = "";
            public int FolderCount { get; set; }
            public int SkuCount { get; set; }
            public string Code { get; set; } = "";
            public bool IsCollectionRewardEnabled { get; set; }
            public decimal CreditPerCollection { get; set; }
            public decimal PointPerCollection { get; set; }
            public bool IsReverseRate { get; set; } = false;
        }

        private bool IsSectionSettingsMode { get; set; } = false;
        private bool IsAddSectionMode { get; set; } = false;
        private bool IsEditSectionMode { get; set; } = false;
        private List<SectionModel> SectionsDb { get; set; } = new List<SectionModel>();
        private SectionModel NewSection { get; set; } = new SectionModel();

        // Populated from SupportingTable type=4 (Item Categories) via API
        private List<SupportingTableItem> _apiCategories = new();
        private List<string> Categories => _apiCategories
            .Select(c => c.SupportingTableName ?? "")
            .Where(n => !string.IsNullOrEmpty(n))
            .ToList();

        private List<string> UnitList = new List<string> { "unit", "pcs", "set", "box", "kg", "litre", "hour", "session" };

        public class OutletItem
        {
            public string Name { get; set; } = "";
            public string Code { get; set; } = "";
        }

        private List<OutletItem> OutletList = new List<OutletItem>
        {
            new OutletItem { Name = "Main Branch", Code = "#1001" },
            new OutletItem { Name = "City Mall", Code = "#1002" },
            new OutletItem { Name = "West Wing", Code = "#1003" },
            new OutletItem { Name = "Airport Kiosk", Code = "#1004" }
        };
        private HashSet<string> SelectedOutletCodes { get; set; } = new HashSet<string>();

        // Service items loaded from API (InventoryTypeID == 2)
        private List<ServiceItem> MenuDb = new List<ServiceItem>();

        protected override async System.Threading.Tasks.Task OnInitializedAsync()
        {
            IsLoading = true;
            await System.Threading.Tasks.Task.WhenAll(LoadCategoriesAsync(), LoadServiceItemsAsync());
            IsLoading = false;
        }

        // ── Data Loading ──────────────────────────────────────────────────────────

        private async System.Threading.Tasks.Task LoadCategoriesAsync()
        {
            var items = await SupportingTableService.LoadListByTypeAsync(4);
            if (items != null)
            {
                _apiCategories = items.Where(i => i.Active).ToList();
                SectionsDb = _apiCategories.Select(c => new SectionModel
                {
                    SupportingTableID = c.SupportingTableID ?? "",
                    Name = c.SupportingTableName ?? "",
                    Code = c.SupportingTableID ?? ""
                }).ToList();
            }
        }

        private async System.Threading.Tasks.Task LoadServiceItemsAsync()
        {
            var items = await InventoryService.LoadItemsAsync("");
            if (items != null)
            {
                // Filter to service items only (InventoryTypeID == 2)
                MenuDb = items
                    .Where(i => i.InventoryTypeID == 2)
                    .Select(MapFromApi)
                    .ToList();

                if (string.IsNullOrEmpty(_defaultBranchID))
                    _defaultBranchID = items.FirstOrDefault(i => !string.IsNullOrEmpty(i.BranchID))?.BranchID ?? string.Empty;
            }
        }

        /// <summary>Map an InventoryDM to a ServiceItem.</summary>
        private ServiceItem MapFromApi(InventoryDM item)
        {
            var sectionName = !string.IsNullOrWhiteSpace(item.ItemGroupName)
                ? item.ItemGroupName
                : _apiCategories.FirstOrDefault(c => c.SupportingTableID == item.ItemGroupID)?.SupportingTableName ?? "";

            return new ServiceItem
            {
                MasterAccountID = item.MasterAccountID ?? "",
                ItemGroupID = item.ItemGroupID ?? "",
                BranchID = item.BranchID ?? "",
                InventoryTypeID = item.InventoryTypeID > 0 ? item.InventoryTypeID : 2,
                ItemType = "Service",
                ItemName = item.AccountName ?? "",
                ServiceSection = sectionName,
                Price = item.SalesPrice,
                Cost = item.PurchasePrice,
                TaxCode = item.TaxCodeID ?? "",
                PriceInclusiveTax = item.IsTaxInclusive,
                IsAvailable = string.Equals(item.AccountStatus, "Active", StringComparison.OrdinalIgnoreCase),
                Barcode = item.VendorItemCode ?? "",    // vendorItemCode = Barcode in POS
                VendorItemCode = item.DisplayCode ?? "", // displayCode = Display Code in POS
                Unit = item.UnitOfMeasureName ?? item.UnitOfMeasureID ?? "unit",
                UnitID = item.UnitOfMeasureID ?? "",
                DisplayImageUrl = LocalImageCache.GetImage(item.MasterAccountID ?? "")
                    ?? ((!string.IsNullOrEmpty(item.ImagePath) && !string.IsNullOrEmpty(item.ImageFileName))
                        ? $"{item.ImagePath}/{item.ImageFileName}" : ""),
                Description = item.SalesDescription ?? "",
                Station = ""
            };
        }

        /// <summary>Map a ServiceItem to the API create/update model.</summary>
        private EBI.UC.Inventory MapToApiModel(ServiceItem item)
        {
            var category = _apiCategories.FirstOrDefault(c =>
                string.Equals(c.SupportingTableName, item.ServiceSection, StringComparison.OrdinalIgnoreCase));

            return new EBI.UC.Inventory
            {
                objInventory = new InventoryDM
                {
                    MasterAccountID = item.MasterAccountID,
                    InventoryTypeID = item.InventoryTypeID > 0 ? item.InventoryTypeID : 2,
                    AccountName = item.ItemName,
                    ItemGroupID = category?.SupportingTableID ?? item.ItemGroupID,
                    ItemGroupName = category?.SupportingTableName ?? item.ServiceSection,
                    SalesDescription = item.Description,
                    DisplayCode = item.VendorItemCode,  // DisplayCode = Display Code in POS
                    VendorItemCode = item.Barcode,      // VendorItemCode = Barcode in POS
                    SalesPrice = item.Price,
                    PurchasePrice = item.Cost,
                    TaxCodeID = item.TaxCode,
                    IsTaxInclusive = item.PriceInclusiveTax,
                    IsSold = true,
                    AccountStatus = item.IsAvailable ? "Active" : "Inactive",
                    UnitOfMeasureID = !string.IsNullOrEmpty(item.UnitID) ? item.UnitID : item.Unit,
                    BranchID = !string.IsNullOrEmpty(item.BranchID) ? item.BranchID : _defaultBranchID
                }
            };
        }

        // ── Navigation ────────────────────────────────────────────────────────────

        private async System.Threading.Tasks.Task TriggerToggleSidebar()
        {
            if (OnToggleSidebar.HasDelegate)
                await OnToggleSidebar.InvokeAsync();
        }

        // ── Filtered data ─────────────────────────────────────────────────────────

        private IEnumerable<string> AvailableSecs => MenuDb
            .Select(x => x.ServiceSection)
            .Distinct()
            .OrderBy(x => x);

        private IEnumerable<ServiceItem> FilteredItems
        {
            get
            {
                var query = MenuDb.AsEnumerable();

                if (SelectedSec != "All") query = query.Where(e => e.ServiceSection == SelectedSec);
                if (!string.IsNullOrEmpty(FilterSearchQuery)) query = query.Where(e => e.ItemName.Contains(FilterSearchQuery, StringComparison.OrdinalIgnoreCase));
                if (FilterActiveStatus == "Active") query = query.Where(e => e.IsAvailable);
                else if (FilterActiveStatus == "Inactive") query = query.Where(e => !e.IsAvailable);
                if (FilterSection != "All") query = query.Where(e => e.ServiceSection == FilterSection);

                return SortOption switch
                {
                    "NameAsc" => query.OrderBy(e => e.ItemName),
                    "NameDesc" => query.OrderByDescending(e => e.ItemName),
                    "PriceAsc" => query.OrderBy(e => e.Price),
                    "PriceDesc" => query.OrderByDescending(e => e.Price),
                    _ => query.OrderBy(e => e.ItemName)
                };
            }
        }

        private string GetIconForCategory(string category)
        {
            return "<svg class='cat-icon' width='24' height='24' xmlns='http://www.w3.org/2000/svg' viewBox='0 0 24 24' fill='none' stroke='currentColor' stroke-width='2' stroke-linecap='round' stroke-linejoin='round'><circle cx='12' cy='12' r='10'></circle></svg>";
        }

        // ── Calculator Popup ──────────────────────────────────────────────────────

        private void OpenAmountPopup(string target)
        {
            _calculatorTarget = target;
            decimal val = 0;
            if (target == "Price") val = NewItem.Price;
            else if (target == "Cost") val = NewItem.Cost;
            else if (target == "FreePoint") val = NewItem.FreePoint;
            else if (target == "RedeemPoint") val = NewItem.RedeemPoint;
            else if (target == "MinPrice") val = NewItem.MinPrice;
            else if (target == "MaxPrice") val = NewItem.MaxPrice;
            else if (target == "CreditReward") val = NewSection.CreditPerCollection;
            else if (target == "PointReward") val = NewSection.PointPerCollection;

            _tempAmountString = val == 0 ? "0.00" : val.ToString("0.00");
            IsAmountPopupOpen = true;
        }

        private void AppendToAmount(string val)
        {
            if (_tempAmountString == "0.00" || _tempAmountString == "0") _tempAmountString = val;
            else if (!(val == "." && _tempAmountString.Contains("."))) _tempAmountString += val;
        }

        private void ClearAmount() => _tempAmountString = "0";
        private void BackspaceAmount() => _tempAmountString = _tempAmountString.Length > 1 ? _tempAmountString.Substring(0, _tempAmountString.Length - 1) : "0";

        private void SaveAmount()
        {
            if (decimal.TryParse(_tempAmountString, out decimal result))
            {
                if (_calculatorTarget == "Price") NewItem.Price = result;
                else if (_calculatorTarget == "Cost") NewItem.Cost = result;
                else if (_calculatorTarget == "FreePoint") NewItem.FreePoint = result;
                else if (_calculatorTarget == "RedeemPoint") NewItem.RedeemPoint = result;
                else if (_calculatorTarget == "MinPrice") NewItem.MinPrice = result;
                else if (_calculatorTarget == "MaxPrice") NewItem.MaxPrice = result;
                else if (_calculatorTarget == "CreditReward") NewSection.CreditPerCollection = result;
                else if (_calculatorTarget == "PointReward") NewSection.PointPerCollection = result;
            }
            IsAmountPopupOpen = false;
        }

        // ── Toggle helpers ────────────────────────────────────────────────────────

        private void ToggleOutletSelection(string code)
        {
            if (SelectedOutletCodes.Contains(code)) SelectedOutletCodes.Remove(code);
            else SelectedOutletCodes.Add(code);
            NewItem.OutletAvailability = SelectedOutletCodes.Any();
        }

        private void ToggleMinMaxPrice()
        {
            IsMinMaxPriceEnabled = !IsMinMaxPriceEnabled;
            if (!IsMinMaxPriceEnabled) { NewItem.MinPrice = 0; NewItem.MaxPrice = 0; }
        }

        private void ToggleRedeemPoint()
        {
            IsRedeemPointEnabled = !IsRedeemPointEnabled;
            if (!IsRedeemPointEnabled) NewItem.RedeemPoint = 0;
        }

        private void OpenDurationPopup() => IsDurationPopupOpen = true;
        private void SelectDuration(int? minutes)
        {
            NewItem.Duration = minutes;
            IsDurationPopupOpen = false;
        }

        private void ToggleFilterVisibility() { IsFilterVisible = !IsFilterVisible; }
        private void ResetFilters() { FilterActiveStatus = "All"; FilterSection = "All"; FilterSearchQuery = ""; SortOption = "NameAsc"; }

        // ── Dropdown helpers ──────────────────────────────────────────────────────

        private void ToggleCategoryDropdown() { IsCategoryDropdownOpen = !IsCategoryDropdownOpen; }
        private void SelectCategoryFromDropdown(string cat) { NewItem.ServiceSection = cat; IsCategoryDropdownOpen = false; }
        private void SelectCategoryFromPopup(string cat) { NewItem.ServiceSection = cat; IsCategoryPopupOpen = false; }
        private void ToggleUnitDropdown() { IsUnitDropdownOpen = !IsUnitDropdownOpen; }
        private void SelectUnit(string unit) { NewItem.Unit = unit; IsUnitDropdownOpen = false; }

        // ── Section Settings ──────────────────────────────────────────────────────

        private void OpenSectionSettings() { IsSectionSettingsMode = true; IsAddSectionMode = false; IsEditSectionMode = false; }
        private void GoBackToMain() { IsSectionSettingsMode = false; IsAddSectionMode = false; IsEditSectionMode = false; }

        private void OpenAddSectionMode() { NewSection = new SectionModel(); IsAddSectionMode = true; IsEditSectionMode = false; }
        private void OpenEditSectionMode(SectionModel section)
        {
            NewSection = new SectionModel
            {
                Id = section.Id,
                SupportingTableID = section.SupportingTableID,
                Name = section.Name,
                AdditionalTitle = section.AdditionalTitle,
                Code = section.Code,
                IsCollectionRewardEnabled = section.IsCollectionRewardEnabled,
                CreditPerCollection = section.CreditPerCollection,
                PointPerCollection = section.PointPerCollection,
                IsReverseRate = section.IsReverseRate,
                FolderCount = section.FolderCount,
                SkuCount = section.SkuCount
            };
            IsAddSectionMode = true; IsEditSectionMode = true;
        }
        private void CloseAddSectionMode() { IsAddSectionMode = false; IsEditSectionMode = false; }

        private async System.Threading.Tasks.Task SaveSection()
        {
            if (_isSavingSection) return;
            _isSavingSection = true;
            try
            {
                if (string.IsNullOrWhiteSpace(NewSection.Name))
                {
                    ErrorTitle = "Missing Information";
                    ErrorMessage = "Section Name is a compulsory field. Please enter a section name before saving.";
                    IsErrorPopupOpen = true;
                    return;
                }

                var token = await StoreTokenService.GetTokenAsync();
                if (string.IsNullOrEmpty(token))
                {
                    ErrorTitle = "Session Expired";
                    ErrorMessage = "Your login session has expired. Please go back and log in again.";
                    IsErrorPopupOpen = true;
                    return;
                }

                if (IsEditSectionMode)
                {
                    var model = new SupportingTableModel
                    {
                        supportingTableID = NewSection.SupportingTableID,
                        supportingTableName = NewSection.Name,
                        supportingTableTypeID = 4,
                        active = true
                    };
                    var (success, message) = await SupportingTableService.UpdateAsync(model);
                    if (success)
                    {
                        await LoadCategoriesAsync();
                    }
                    else
                    {
                        var isAuth = IsAuthError(message);
                        ErrorTitle = isAuth ? "Session Expired" : "Save Failed";
                        ErrorMessage = isAuth ? "Your login session has expired. Please go back and log in again."
                            : (string.IsNullOrWhiteSpace(message) ? "Unable to save section. Please check your connection and try again." : message);
                        IsErrorPopupOpen = true;
                        return;
                    }
                }
                else
                {
                    var model = new SupportingTableModel
                    {
                        supportingTableID = "",
                        supportingTableName = NewSection.Name,
                        supportingTableTypeID = 4,
                        active = true
                    };
                    var (success, message) = await SupportingTableService.CreateAsync(model);
                    if (success)
                    {
                        await LoadCategoriesAsync();
                    }
                    else
                    {
                        var isAuth = IsAuthError(message);
                        ErrorTitle = isAuth ? "Session Expired" : "Save Failed";
                        ErrorMessage = isAuth ? "Your login session has expired. Please go back and log in again."
                            : (string.IsNullOrWhiteSpace(message) ? "Unable to create section. Please check your connection and try again." : message);
                        IsErrorPopupOpen = true;
                        return;
                    }
                }

                IsAddSectionMode = false; IsEditSectionMode = false;
            }
            finally { _isSavingSection = false; }
        }

        private void ToggleCollectionReward() { NewSection.IsCollectionRewardEnabled = !NewSection.IsCollectionRewardEnabled; }
        private void ToggleReverseRate() { NewSection.IsReverseRate = !NewSection.IsReverseRate; }

        // ── Add/Edit Mode ─────────────────────────────────────────────────────────

        private void OpenAddServiceMode()
        {
            IsAddMode = true;
            IsEditingExistingItem = false;
            NewItem = new ServiceItem();
            NewItem.ItemType = ViewType;
            NewItem.BranchID = _defaultBranchID;
            ActiveAddTab = "Info";
            IsMinMaxPriceEnabled = false;
            IsRedeemPointEnabled = false;
            SelectedOutletCodes.Clear();
            SelectedBomItems.Clear();
            _tempImagePreview = "";
            foreach (var o in OutletList) SelectedOutletCodes.Add(o.Code);
        }

        private void CloseAddMode()
        {
            IsAddMode = false;
            IsEditingExistingItem = false;
        }

        // ── Save Item (API) ───────────────────────────────────────────────────────

        private async System.Threading.Tasks.Task SaveItem()
        {
            if (_isSaving) return;
            _isSaving = true;
            try
            {
                // ── Step 1: DOM fallback ────────────────────────────────────────
                try
                {
                    var domName = await JS.InvokeAsync<string?>(
                        "eval", "document.getElementById('service-name-input')?.value ?? null");
                    if (!string.IsNullOrWhiteSpace(domName))
                        NewItem.ItemName = domName.Trim();
                }
                catch { /* JS not available — rely on @oninput value */ }

                // ── Step 2: Validate ────────────────────────────────────────────
                if (string.IsNullOrWhiteSpace(NewItem.ItemName) && string.IsNullOrWhiteSpace(NewItem.ServiceSection))
                {
                    ErrorTitle = "Missing Information";
                    ErrorMessage = "Name and Select Section are compulsory fields. Please fill them in before saving.";
                    IsErrorPopupOpen = true;
                    return;
                }
                if (string.IsNullOrWhiteSpace(NewItem.ItemName))
                {
                    ErrorTitle = "Missing Information";
                    ErrorMessage = "Name is a compulsory field. Please enter a service name before saving.";
                    IsErrorPopupOpen = true;
                    return;
                }
                if (string.IsNullOrWhiteSpace(NewItem.ServiceSection))
                {
                    ErrorTitle = "Missing Information";
                    ErrorMessage = "Select Section is a compulsory field. Please select a section before saving.";
                    IsErrorPopupOpen = true;
                    return;
                }

                // ── Step 3: Token check ─────────────────────────────────────────
                var token = await StoreTokenService.GetTokenAsync();
                if (string.IsNullOrEmpty(token))
                {
                    ErrorTitle = "Session Expired";
                    ErrorMessage = "Your login session has expired. Please go back and log in again.";
                    IsErrorPopupOpen = true;
                    return;
                }

                // ── Step 4: Call API ────────────────────────────────────────────
                if (string.IsNullOrEmpty(NewItem.DisplayImageUrl) && !string.IsNullOrEmpty(_tempImagePreview))
                    NewItem.DisplayImageUrl = _tempImagePreview;

                var model = MapToApiModel(NewItem);
                (bool success, string message) result;

                if (IsEditingExistingItem && !string.IsNullOrEmpty(NewItem.MasterAccountID))
                {
                    result = await InventoryService.UpdateItemAsync(model);
                }
                else
                {
                    result = await InventoryService.CreateItemAsync(model);
                }

                if (result.success)
                {
                    if (!string.IsNullOrEmpty(_tempImagePreview))
                        NewItem.DisplayImageUrl = _tempImagePreview;

                    if (IsEditingExistingItem && !string.IsNullOrEmpty(NewItem.MasterAccountID))
                    {
                        SuccessMessage = "Service updated successfully.";
                        NotificationSvc.Add(new AppNotification
                        {
                            Icon = "✂️",
                            TitleKey = "NotifServiceUpdatedTitle",
                            MessageKey = "NotifServiceUpdatedMsg",
                            MessageParam = NewItem.ItemName
                        });
                        await JS.InvokeVoidAsync("localStorage.setItem", "app_notifications", NotificationSvc.ToJson());

                        if (!string.IsNullOrEmpty(_tempImagePreview))
                            LocalImageCache.StoreImage(NewItem.MasterAccountID, _tempImagePreview);

                        var idx = MenuDb.FindIndex(x => x.MasterAccountID == NewItem.MasterAccountID);
                        if (idx >= 0) MenuDb[idx] = NewItem;
                    }
                    else
                    {
                        SuccessMessage = "Service created successfully.";
                        NotificationSvc.Add(new AppNotification
                        {
                            Icon = "✂️",
                            TitleKey = "NotifNewServiceTitle",
                            MessageKey = "NotifNewServiceMsg",
                            MessageParam = NewItem.ItemName
                        });
                        await JS.InvokeVoidAsync("localStorage.setItem", "app_notifications", NotificationSvc.ToJson());

                        // Reload from API to get server-assigned MasterAccountID
                        await LoadServiceItemsAsync();

                        if (!string.IsNullOrEmpty(_tempImagePreview))
                        {
                            var created = MenuDb.FirstOrDefault(x =>
                                string.Equals(x.ItemName, NewItem.ItemName, StringComparison.OrdinalIgnoreCase) &&
                                !string.IsNullOrEmpty(x.MasterAccountID));
                            if (created != null)
                            {
                                LocalImageCache.StoreImage(created.MasterAccountID, _tempImagePreview);
                                created.DisplayImageUrl = _tempImagePreview;
                            }
                        }
                    }

                    IsAddMode = false;
                    IsEditingExistingItem = false;
                    IsSuccessPopupOpen = true;
                }
                else
                {
                    ErrorTitle = "Save Failed";
                    var apiMsg = string.IsNullOrWhiteSpace(result.message) ? "No response from server." : result.message;
                    if (IsAuthError(apiMsg))
                    {
                        ErrorTitle = "Session Expired";
                        ErrorMessage = "Your login session has expired. Please go back and log in again.";
                    }
                    else
                    {
                        ErrorMessage = $"Unable to save. Server replied: {apiMsg}";
                    }
                    IsErrorPopupOpen = true;
                }
            }
            finally { _isSaving = false; }
        }

        // ── Delete ────────────────────────────────────────────────────────────────

        private void RequestDelete(ServiceItem item) { _itemToDelete = item; IsDeleteConfirmOpen = true; }

        private async System.Threading.Tasks.Task ConfirmDelete()
        {
            if (_itemToDelete == null) { IsDeleteConfirmOpen = false; return; }

            if (!string.IsNullOrEmpty(_itemToDelete.MasterAccountID))
            {
                var token = await StoreTokenService.GetTokenAsync();
                if (string.IsNullOrEmpty(token))
                {
                    ErrorTitle = "Session Expired";
                    ErrorMessage = "Your login session has expired. Please go back and log in again.";
                    IsErrorPopupOpen = true;
                    IsDeleteConfirmOpen = false;
                    return;
                }

                var (success, message) = await InventoryService.DeleteItemAsync(_itemToDelete.MasterAccountID);
                if (success)
                {
                    LocalImageCache.RemoveImage(_itemToDelete.MasterAccountID);
                    MenuDb.Remove(_itemToDelete);
                }
                else
                {
                    var isAuth = IsAuthError(message);
                    ErrorTitle = isAuth ? "Session Expired" : "Delete Failed";
                    ErrorMessage = isAuth ? "Your login session has expired. Please go back and log in again."
                        : (string.IsNullOrWhiteSpace(message) ? "Unable to delete item. Please try again." : message);
                    IsErrorPopupOpen = true;
                }
            }
            else
            {
                MenuDb.Remove(_itemToDelete);
            }

            _itemToDelete = null;
            IsDeleteConfirmOpen = false;
        }

        // ── Show Item Details (Edit) ───────────────────────────────────────────────

        private async System.Threading.Tasks.Task ShowItemDetails(ServiceItem item)
        {
            IsEditingExistingItem = true;
            ActiveAddTab = "Info";
            NewItem = new ServiceItem
            {
                Id = item.Id,
                MasterAccountID = item.MasterAccountID,
                ItemGroupID = item.ItemGroupID,
                BranchID = item.BranchID,
                InventoryTypeID = item.InventoryTypeID > 0 ? item.InventoryTypeID : 2,
                ItemType = item.ItemType,
                ServiceSection = item.ServiceSection,
                ServiceFolder = item.ServiceFolder,
                DisplayImageUrl = item.DisplayImageUrl,
                Station = item.Station,
                ItemName = item.ItemName,
                IsAvailable = item.IsAvailable,
                VendorItemCode = item.VendorItemCode,
                Price = item.Price,
                Cost = item.Cost,
                TaxCode = item.TaxCode,
                PriceInclusiveTax = item.PriceInclusiveTax,
                Unit = item.Unit,
                UnitID = item.UnitID,
                Duration = item.Duration,
                Barcode = item.Barcode,
                MinPrice = item.MinPrice,
                MaxPrice = item.MaxPrice,
                OutletAvailability = item.OutletAvailability,
                FreePoint = item.FreePoint,
                RedeemPoint = item.RedeemPoint,
                BillOfMaterial = item.BillOfMaterial,
                Description = item.Description,
                Policy = item.Policy,
                TermCondition1 = item.TermCondition1,
                TermCondition2 = item.TermCondition2,
                TermCondition3 = item.TermCondition3,
                ImageName = item.ImageName
            };

            var localImg = LocalImageCache.GetImage(item.MasterAccountID);
            _tempImagePreview = !string.IsNullOrEmpty(localImg) ? localImg : item.DisplayImageUrl;
            if (!string.IsNullOrEmpty(localImg)) NewItem.DisplayImageUrl = localImg;

            // Load full detail record from API to get BillOfMaterial and refreshed fields
            if (!string.IsNullOrEmpty(item.MasterAccountID))
            {
                try
                {
                    var detail = await InventoryService.LoadItemAsync(item.MasterAccountID);
                    if (detail != null)
                    {
                        if (!string.IsNullOrEmpty(detail.BranchID)) NewItem.BranchID = detail.BranchID;
                        if (!string.IsNullOrEmpty(detail.UnitOfMeasureID)) NewItem.UnitID = detail.UnitOfMeasureID;
                        if (!string.IsNullOrEmpty(detail.UnitOfMeasureName)) NewItem.Unit = detail.UnitOfMeasureName;
                        if (!string.IsNullOrEmpty(detail.DisplayCode)) NewItem.VendorItemCode = detail.DisplayCode;
                        if (!string.IsNullOrEmpty(detail.VendorItemCode)) NewItem.Barcode = detail.VendorItemCode;
                        if (detail.PurchasePrice > 0) NewItem.Cost = detail.PurchasePrice;
                    }
                }
                catch { /* non-critical — proceed with list data */ }
            }

            IsMinMaxPriceEnabled = NewItem.MinPrice > 0 || NewItem.MaxPrice > 0;
            IsRedeemPointEnabled = NewItem.RedeemPoint > 0;
            SelectedOutletCodes.Clear();
            foreach (var o in OutletList) SelectedOutletCodes.Add(o.Code);
            SelectedBomItems.Clear();
            if (!string.IsNullOrEmpty(NewItem.BillOfMaterial))
            {
                var names = NewItem.BillOfMaterial.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
                foreach (var name in names)
                {
                    var found = MenuDb.FirstOrDefault(x => x.ItemName == name);
                    if (found != null) SelectedBomItems.Add(found);
                }
            }
            IsAddMode = true;
        }

        // ── File Upload ───────────────────────────────────────────────────────────

        private async System.Threading.Tasks.Task HandleFileSelected(InputFileChangeEventArgs e)
        {
            IsUploadPopupOpen = false;
            if (e.File != null)
            {
                var format = "image/png";
                var resizedImage = await e.File.RequestImageFileAsync(format, 400, 400);
                var buffer = new byte[resizedImage.Size];

                using (var stream = resizedImage.OpenReadStream(maxAllowedSize: 1024 * 1024 * 15))
                {
                    int totalBytesRead = 0;
                    while (totalBytesRead < buffer.Length)
                    {
                        var bytesRead = await stream.ReadAsync(buffer, totalBytesRead, buffer.Length - totalBytesRead);
                        if (bytesRead == 0) break;
                        totalBytesRead += bytesRead;
                    }
                }

                _tempImagePreview = $"data:{format};base64,{Convert.ToBase64String(buffer)}";
                NewItem.ServiceFolder = $"c://image/{e.File.Name}";
                NewItem.DisplayImageUrl = _tempImagePreview;
                NewItem.ImageName = e.File.Name;
            }
        }

        private async System.Threading.Tasks.Task DownloadReport(string format) { await JS.InvokeVoidAsync("window.print"); }

        protected void SelectSec(string section) { SelectedSec = section; }

        // ── BOM Popup ─────────────────────────────────────────────────────────────

        private void OpenBomPopup()
        {
            SelectedBomItems.Clear();
            if (!string.IsNullOrEmpty(NewItem.BillOfMaterial))
            {
                var names = NewItem.BillOfMaterial.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
                foreach (var name in names) { var found = MenuDb.FirstOrDefault(x => x.ItemName == name); if (found != null) SelectedBomItems.Add(found); }
            }
            IsBomPopupOpen = true;
        }

        private void CloseBomPopup(bool save) { if (save) UpdateBomString(); IsBomPopupOpen = false; }
        private void OpenAddMaterialPopup() { _bomSnapshot = new List<ServiceItem>(SelectedBomItems); BomSearchQuery = ""; BomSectionFilter = "All"; BomSortOption = "NameAsc"; IsBomSortMenuOpen = false; IsAddMaterialPopupOpen = true; }
        private void CloseAddMaterialPopup(bool save) { if (!save) SelectedBomItems = new List<ServiceItem>(_bomSnapshot); IsAddMaterialPopupOpen = false; }
        private void AddToBom(ServiceItem item) { if (!SelectedBomItems.Any(x => x.Id == item.Id)) SelectedBomItems.Add(item); }
        private void RemoveFromBom(ServiceItem item) { SelectedBomItems.Remove(item); }
        private void UpdateBomString() { NewItem.BillOfMaterial = string.Join(", ", SelectedBomItems.Select(x => x.ItemName)); }
        private void ToggleBomSortMenu() { IsBomSortMenuOpen = !IsBomSortMenuOpen; }
        private void SelectBomSort(string sort) { BomSortOption = sort; IsBomSortMenuOpen = false; }
        private void SelectBomSection(string section) { BomSectionFilter = section; }

        private IEnumerable<ServiceItem> GetFilteredBomMaterials()
        {
            var query = MenuDb.AsEnumerable();
            if (BomSectionFilter != "All") query = query.Where(x => x.ServiceSection == BomSectionFilter);
            if (!string.IsNullOrEmpty(BomSearchQuery)) query = query.Where(x => x.ItemName.Contains(BomSearchQuery, StringComparison.OrdinalIgnoreCase));
            return BomSortOption switch { "NameAsc" => query.OrderBy(x => x.ItemName), "PriceAsc" => query.OrderBy(x => x.Price), "PriceDesc" => query.OrderByDescending(x => x.Price), _ => query.OrderBy(x => x.ItemName) };
        }

        // ── Export ────────────────────────────────────────────────────────────────

        private async System.Threading.Tasks.Task DownloadExcel()
        {
            try
            {
                var dataToExport = FilteredItems.Any() ? FilteredItems : MenuDb;

                if (!dataToExport.Any())
                {
                    ErrorTitle = "No Data";
                    ErrorMessage = "No data available to export.";
                    IsErrorPopupOpen = true;
                    return;
                }

                // Build CSV content
                var csv = new System.Text.StringBuilder();
                csv.Append("\uFEFF"); // UTF-8 BOM for Excel compatibility

                // Report Summary Section
                csv.AppendLine($"\"Service Export Summary\",,");
                csv.AppendLine($"\"Type\",\"{ViewType}\",");
                csv.AppendLine($"\"Total Items\",,{dataToExport.Count()}");
                csv.AppendLine($"\"Exported On\",\"{DateTime.Now:dd/MM/yyyy HH:mm:ss}\",");

                // Add filter information if filters are active
                var hasFilters = !string.IsNullOrWhiteSpace(FilterSearchQuery) ||
                                FilterActiveStatus != "All" ||
                                FilterSection != "All";

                if (hasFilters)
                {
                    csv.AppendLine($"\"Filters Applied\",,");
                    if (!string.IsNullOrWhiteSpace(FilterSearchQuery))
                        csv.AppendLine($"\"  - Search\",\"{EscapeCsvValue(FilterSearchQuery)}\",");
                    if (FilterActiveStatus != "All")
                        csv.AppendLine($"\"  - Status\",\"{FilterActiveStatus}\",");
                    if (FilterSection != "All")
                        csv.AppendLine($"\"  - Section\",\"{EscapeCsvValue(FilterSection)}\",");
                }

                // Empty line separator
                csv.AppendLine();

                // Data Header
                csv.AppendLine($"\"Name\",\"Section\",\"Tax Code\",\"Tax Inclusive\",\"Price\",\"Cost\",\"Barcode\",\"UOM\",\"Duration (min)\"");

                // Data rows
                foreach (var item in dataToExport)
                {
                    var duration = item.Duration.HasValue ? item.Duration.Value.ToString() : "";
                    csv.AppendLine($"\"{EscapeCsvValue(item.ItemName)}\",\"{EscapeCsvValue(item.ServiceSection)}\",\"{EscapeCsvValue(item.TaxCode)}\",\"{(item.PriceInclusiveTax ? "Yes" : "No")}\",{item.Price.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture)},{item.Cost.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture)},\"{EscapeCsvValue(item.Barcode ?? "")}\",\"{EscapeCsvValue(item.Unit)}\",\"{EscapeCsvValue(duration)}\"");
                }

                var fileName = $"Services_{DateTime.Now:yyyyMMdd_HHmmss}.csv";
                var csvContent = csv.ToString();

                // Convert string content to Base64
                var csvBytes = System.Text.Encoding.UTF8.GetBytes(csvContent);
                var base64Content = Convert.ToBase64String(csvBytes);

                // Use the file download service instead of JS
                await FileDownloadService.DownloadBinaryFileAsync(fileName, base64Content, "text/csv");

                // Show success message
                SuccessMessage = $"Exported {dataToExport.Count()} items to {fileName}";
                IsSuccessPopupOpen = true;
            }
            catch (Exception ex)
            {
                ErrorTitle = "Export Failed";
                ErrorMessage = $"Error exporting data: {ex.Message}";
                IsErrorPopupOpen = true;
            }
        }

        private string EscapeCsvValue(string input)
        {
            if (string.IsNullOrEmpty(input)) return string.Empty;
            return input.Replace("\"", "\"\"");
        }
        // ── Error helpers ─────────────────────────────────────────────────────────

        private void CloseErrorPopup() => IsErrorPopupOpen = false;
        private void CloseSuccessPopup() => IsSuccessPopupOpen = false;

        private static bool IsAuthError(string? message) =>
            !string.IsNullOrWhiteSpace(message) &&
            (message.Contains("401") ||
             message.Contains("Unauthorized", StringComparison.OrdinalIgnoreCase) ||
             message.Contains("expired", StringComparison.OrdinalIgnoreCase));
    }
}
