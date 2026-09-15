using EBI.DM;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.JSInterop;
using SenangRetails.Shared.Model;
using SenangRetails.Shared.Models.DTOs;
using SenangRetails.Shared.Services;
using SenangRetails.Shared.Services.InventoryService;
using SenangRetails.Shared.Services.MembershipTypeService;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using DtoPackageItem = SenangRetails.Shared.Models.DTOs.PackageItemEntry;

namespace SenangRetails.Shared.Pages
{
    public partial class Package : BasePage
    {
        [Inject] public IJSRuntime JS { get; set; } = default!;

        [Parameter] public string ViewType { get; set; } = "Package";
        [Parameter] public EventCallback OnToggleSidebar { get; set; }
        [Parameter] public bool StartInAddMode { get; set; } = false;
        [Parameter] public EventCallback OnBackToList { get; set; }

        private string SelectedSec { get; set; } = "All";
        private bool IsFilterVisible { get; set; } = false;
        private bool IsAddMode { get; set; } = false;
        private string ActiveAddTab { get; set; } = "Info";

        // Popup States
        private bool IsCategoryPopupOpen { get; set; } = false;
        private bool IsUploadPopupOpen { get; set; } = false;
        private bool IsAmountPopupOpen { get; set; } = false;
        private bool IsDurationPopupOpen { get; set; } = false;
        private bool IsBarcodePopupOpen { get; set; } = false;
        private bool IsOutletPopupOpen { get; set; } = false;
        private bool IsFilterPopupOpen { get; set; } = false;

        // Voucher Selection Popup State
        private bool IsVoucherPopupOpen { get; set; } = false;
        private string VoucherSelectionType { get; set; } = "Service";
        private string VoucherSearchQuery { get; set; } = "";

        private bool IsUnitDropdownOpen { get; set; } = false;

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
        private bool FilterShowDisabledOnly { get; set; } = false;
        private string FilterRedeemStatus { get; set; } = "All";

        private bool IsErrorPopupOpen { get; set; } = false;
        private string ErrorMessage { get; set; } = "";
        private bool IsDeleteConfirmOpen { get; set; } = false;
        private PackageItem? _itemToDelete;
        private PackageItem NewItem { get; set; } = new PackageItem();

        // Loading / saving state
        private bool IsLoading { get; set; } = false;
        private bool IsSaving { get; set; } = false;

        // API MasterAccountID of the item being edited (empty = new item)
        private string _editingMasterAccountID = "";

        // Membership credit state
        private List<MembershipCreditEntry> MembershipCreditEntries { get; set; } = new();
        private int MemberExpiryDays { get; set; } = 365;
        private List<MembershipTypeDM> AllMembershipTypes { get; set; } = new();
        private bool IsMemberTypePickerOpen { get; set; } = false;
        private string MemberTypeSearchQuery { get; set; } = "";
        private int _memberCreditEditIndex = -1;

        // Section Settings Data
        public class SectionModel
        {
            public Guid Id { get; set; } = Guid.NewGuid();
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

        // Package Item (for the Items tab)
        public class PackageItemEntry
        {
            public string? AutoID { get; set; }
            public string ItemID { get; set; } = "";
            public string ItemName { get; set; } = "";
            public bool IsDeferred { get; set; } = false;
            public decimal UnitPV { get; set; }
            public string UOM { get; set; } = "unit";
            public decimal Qty { get; set; } = 1;
        }

        private List<PackageItemEntry> PackageItems { get; set; } = new();
        private bool IsPackageItemPickerOpen { get; set; } = false;
        private string PackageItemPickerType { get; set; } = "Service";
        private string PackageItemSearchQuery { get; set; } = "";

        // Real inventory items for the Items-tab picker
        private List<InventoryDM> RealServices { get; set; } = new();
        private List<InventoryDM> RealProducts { get; set; } = new();

        private List<string> Categories = new List<string> { "Bundles", "Family Sets", "Promo Packs", "Seasonal", "Gift Boxes" };
        private List<string> UnitList = new List<string> { "centimeter", "gram", "inch", "kati", "kilogram", "liter", "meter", "miligram", "mililiter", "ounce", "piece", "package", "unit" };

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

        private List<PackageItem> MenuDb = new List<PackageItem>();

        // Voucher popup still uses mock data (voucher list is display-only)
        private List<PackageItem> MockServices = new List<PackageItem>();
        private List<PackageItem> MockProducts = new List<PackageItem>();

        protected override async Task OnInitializedAsync()
        {
            IsLoading = true;
            try
            {
                var branchId = AppState.SelectedBranchID;
                var allItems = await InventoryService.LoadItemsAsync(branchId);
                if (allItems != null)
                {
                    int inventoryTypeId = ViewType == "Package" ? 5 : 7;
                    MenuDb = allItems
                        .Where(i => i.InventoryTypeID == inventoryTypeId)
                        .Select(i => new PackageItem
                        {
                            MasterAccountID = i.MasterAccountID ?? "",
                            ItemType = ViewType,
                            ItemName = i.AccountName ?? "",
                            Price = i.SalesPrice,
                            PackageSection = i.ItemGroupName ?? "",
                            DisplayImageUrl = i.ImagePath ?? "",
                            IsAvailable = i.AccountStatus?.Equals("Active", StringComparison.OrdinalIgnoreCase) == true
                        })
                        .ToList();

                    RealServices = allItems.Where(i => i.InventoryTypeID == 3).ToList();
                    RealProducts = allItems.Where(i => i.InventoryTypeID == 1).ToList();
                }

                // Derive section list from loaded data
                SectionsDb = MenuDb
                    .Select(i => i.PackageSection)
                    .Where(s => !string.IsNullOrEmpty(s))
                    .Distinct()
                    .Select(s => new SectionModel { Name = s })
                    .ToList();

                var mtList = await MembershipTypeService.GetAllMembershipTypesAsync();
                AllMembershipTypes = mtList?.Where(x => x.Active).ToList() ?? new();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Package] Load error: {ex.Message}");
            }
            finally
            {
                IsLoading = false;
            }

            // Mock voucher data (voucher list is a display-only feature)
            MockServices.Add(new PackageItem { ItemName = "Hair Cut", PackageSection = "Hair", Price = 50 });
            MockServices.Add(new PackageItem { ItemName = "Manicure", PackageSection = "Nails", Price = 80 });
            MockServices.Add(new PackageItem { ItemName = "Massage", PackageSection = "Body", Price = 150 });
            MockProducts.Add(new PackageItem { ItemName = "Shampoo", PackageSection = "Care", Price = 25 });
            MockProducts.Add(new PackageItem { ItemName = "Gel", PackageSection = "Care", Price = 15 });
            MockProducts.Add(new PackageItem { ItemName = "Towel", PackageSection = "Accessories", Price = 10 });

            if (StartInAddMode) OpenAddPackageMode();
        }

        private async System.Threading.Tasks.Task TriggerToggleSidebar()
        {
            if (OnToggleSidebar.HasDelegate)
                await OnToggleSidebar.InvokeAsync();
        }

        private IEnumerable<string> AvailableSecs => MenuDb
            .Where(x => x.ItemType == ViewType)
            .Select(x => x.PackageSection)
            .Distinct()
            .OrderBy(x => x);

        private IEnumerable<PackageItem> FilteredItems
        {
            get
            {
                var query = MenuDb.Where(x => x.ItemType == ViewType);
                if (SelectedSec != "All") query = query.Where(e => e.PackageSection == SelectedSec);
                if (!string.IsNullOrEmpty(FilterSearchQuery)) query = query.Where(e => e.ItemName.Contains(FilterSearchQuery, StringComparison.OrdinalIgnoreCase));
                if (FilterShowDisabledOnly) query = query.Where(e => !e.IsAvailable);
                if (FilterRedeemStatus == "Redeemable") query = query.Where(e => e.RedeemPoint > 0);
                if (FilterRedeemStatus == "Not Redeemable") query = query.Where(e => e.RedeemPoint <= 0);
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

        private IEnumerable<PackageItem> VoucherFilteredItems
        {
            get
            {
                var list = VoucherSelectionType == "Service" ? MockServices : MockProducts;
                if (!string.IsNullOrEmpty(VoucherSearchQuery))
                    return list.Where(x => x.ItemName.Contains(VoucherSearchQuery, StringComparison.OrdinalIgnoreCase));
                return list;
            }
        }

        private IEnumerable<InventoryDM> PackageItemPickerFiltered
        {
            get
            {
                var list = PackageItemPickerType == "Service" ? RealServices : RealProducts;
                if (!string.IsNullOrEmpty(PackageItemSearchQuery))
                    return list.Where(x => (x.AccountName ?? "").Contains(PackageItemSearchQuery, StringComparison.OrdinalIgnoreCase));
                return list;
            }
        }

        private IEnumerable<MembershipTypeDM> FilteredMemberTypes =>
            AllMembershipTypes.Where(mt =>
                !MembershipCreditEntries.Any(e => e.memberTypeID == mt.MemberTypeID) &&
                (string.IsNullOrEmpty(MemberTypeSearchQuery) ||
                 mt.MemberTypeName.Contains(MemberTypeSearchQuery, StringComparison.OrdinalIgnoreCase)));

        private string GetMemberTypeName(string memberTypeID) =>
            AllMembershipTypes.FirstOrDefault(m => m.MemberTypeID == memberTypeID)?.MemberTypeName ?? memberTypeID;

        private string GetIconForCategory(string category)
        {
            return "<svg class='cat-icon' width='24' height='24' xmlns='http://www.w3.org/2000/svg' viewBox='0 0 24 24' fill='none' stroke='currentColor' stroke-width='2' stroke-linecap='round' stroke-linejoin='round'><rect x='2' y='2' width='20' height='20' rx='5' ry='5'></rect><path d='M16 11.37A4 4 0 1 1 12.63 8 4 4 0 0 1 16 11.37z'></path><line x1='17.5' y1='6.5' x2='17.51' y2='6.5'></line></svg>";
        }

        private void OpenAmountPopup(string target, PackageItem? item = null)
        {
            _calculatorTarget = target;
            decimal val = 0;

            if (target == "Price") val = NewItem.Price;
            else if (target == "Credits") val = NewItem.Credits;
            else if (target == "Points") val = NewItem.Points;
            else if (target == "PackageValue") val = NewItem.PackageValue;
            else if (target == "FreePoint") val = NewItem.FreePoint;
            else if (target == "RedeemPoint") val = NewItem.RedeemPoint;
            else if (target == "MinPrice") val = NewItem.MinPrice;
            else if (target == "MaxPrice") val = NewItem.MaxPrice;
            else if (target == "CreditReward") val = NewSection.CreditPerCollection;
            else if (target == "PointReward") val = NewSection.PointPerCollection;
            else if (target == "LowStockAlert") val = NewItem.LowStockAlert ?? 0;

            bool isInt = target == "Points" || target == "LowStockAlert";
            _tempAmountString = isInt ? ((int)val).ToString() : (val == 0 ? "0.00" : val.ToString("0.00"));
            if (target == "Points" && val == 0) _tempAmountString = "0";

            IsAmountPopupOpen = true;
        }

        private void OpenCreditAmountEditor(int index)
        {
            if (index < 0 || index >= MembershipCreditEntries.Count) return;
            _memberCreditEditIndex = index;
            _calculatorTarget = "MemberCredit";
            var val = MembershipCreditEntries[index].memberCredit;
            _tempAmountString = val == 0 ? "0.00" : val.ToString("0.00");
            IsAmountPopupOpen = true;
        }

        private void AppendToAmount(string val)
        {
            if ((_calculatorTarget == "Points" || _calculatorTarget == "LowStockAlert") && val == ".") return;
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
                else if (_calculatorTarget == "Credits") NewItem.Credits = result;
                else if (_calculatorTarget == "Points") NewItem.Points = (int)result;
                else if (_calculatorTarget == "PackageValue") NewItem.PackageValue = result;
                else if (_calculatorTarget == "FreePoint") NewItem.FreePoint = result;
                else if (_calculatorTarget == "RedeemPoint") NewItem.RedeemPoint = result;
                else if (_calculatorTarget == "MinPrice") NewItem.MinPrice = result;
                else if (_calculatorTarget == "MaxPrice") NewItem.MaxPrice = result;
                else if (_calculatorTarget == "CreditReward") NewSection.CreditPerCollection = result;
                else if (_calculatorTarget == "PointReward") NewSection.PointPerCollection = result;
                else if (_calculatorTarget == "LowStockAlert") NewItem.LowStockAlert = (int)result;
                else if (_calculatorTarget == "MemberCredit" && _memberCreditEditIndex >= 0 && _memberCreditEditIndex < MembershipCreditEntries.Count)
                {
                    MembershipCreditEntries[_memberCreditEditIndex].memberCredit = result;
                    _memberCreditEditIndex = -1;
                }
            }
            IsAmountPopupOpen = false;
        }

        // --- Voucher Selection Logic ---
        private void OpenVoucherSelection(string type)
        {
            VoucherSelectionType = type;
            VoucherSearchQuery = "";
            IsVoucherPopupOpen = true;
        }

        private void AddVoucherToPackage(PackageItem voucher)
        {
            if (NewItem.VoucherList.Count >= 10)
            {
                ErrorMessage = "Maximum 10 vouchers allowed per package.";
                IsErrorPopupOpen = true;
                return;
            }
            NewItem.VoucherList.Add($"{VoucherSelectionType}: {voucher.ItemName}");
            IsVoucherPopupOpen = false;
        }

        private void RemoveVoucher(string voucherString) => NewItem.VoucherList.Remove(voucherString);

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
        private void SelectDuration(int? minutes) { NewItem.Duration = minutes; IsDurationPopupOpen = false; }

        private void ToggleFilterVisibility() { IsFilterPopupOpen = true; }
        private void ResetFilters() { FilterShowDisabledOnly = false; FilterRedeemStatus = "All"; FilterSearchQuery = ""; SortOption = "NameAsc"; }

        private void OpenSectionSettings() { IsSectionSettingsMode = true; IsAddSectionMode = false; IsEditSectionMode = false; }
        private void GoBackToMain() { IsSectionSettingsMode = false; IsAddSectionMode = false; IsEditSectionMode = false; }

        private void OpenAddSectionMode() { NewSection = new SectionModel(); IsAddSectionMode = true; IsEditSectionMode = false; }
        private void OpenEditSectionMode(SectionModel section)
        {
            NewSection = new SectionModel { Id = section.Id, Name = section.Name, AdditionalTitle = section.AdditionalTitle, Code = section.Code, IsCollectionRewardEnabled = section.IsCollectionRewardEnabled, CreditPerCollection = section.CreditPerCollection, PointPerCollection = section.PointPerCollection, IsReverseRate = section.IsReverseRate, FolderCount = section.FolderCount, SkuCount = section.SkuCount };
            IsAddSectionMode = true; IsEditSectionMode = true;
        }
        private void CloseAddSectionMode() { IsAddSectionMode = false; IsEditSectionMode = false; }

        private void SaveSection()
        {
            if (IsEditSectionMode)
            {
                var existing = SectionsDb.FirstOrDefault(x => x.Id == NewSection.Id);
                if (existing != null)
                {
                    existing.Name = NewSection.Name; existing.AdditionalTitle = NewSection.AdditionalTitle; existing.IsCollectionRewardEnabled = NewSection.IsCollectionRewardEnabled; existing.CreditPerCollection = NewSection.CreditPerCollection; existing.PointPerCollection = NewSection.PointPerCollection; existing.IsReverseRate = NewSection.IsReverseRate;
                }
            }
            else { NewSection.Code = "#" + new Random().Next(100000, 999999).ToString(); SectionsDb.Add(NewSection); }
            IsAddSectionMode = false; IsEditSectionMode = false;
        }

        private void ToggleCollectionReward() => NewSection.IsCollectionRewardEnabled = !NewSection.IsCollectionRewardEnabled;
        private void ToggleReverseRate() => NewSection.IsReverseRate = !NewSection.IsReverseRate;

        private void OpenAddPackageMode()
        {
            IsAddMode = true;
            NewItem = new PackageItem();
            NewItem.ItemType = ViewType;
            ActiveAddTab = "Info";
            IsMinMaxPriceEnabled = false;
            IsRedeemPointEnabled = false;
            SelectedOutletCodes.Clear();
            PackageItems.Clear();
            MembershipCreditEntries.Clear();
            MemberExpiryDays = 365;
            _editingMasterAccountID = "";
            _tempImagePreview = "";
            foreach (var o in OutletList) SelectedOutletCodes.Add(o.Code);
        }

        private async System.Threading.Tasks.Task CloseAddMode()
        {
            IsAddMode = false;
            if (OnBackToList.HasDelegate)
                await OnBackToList.InvokeAsync();
        }

        // Package Item Picker
        private void OpenPackageItemPicker(string type)
        {
            PackageItemPickerType = type;
            PackageItemSearchQuery = "";
            IsPackageItemPickerOpen = true;
        }

        private void AddPackageItem(InventoryDM item)
        {
            PackageItems.Add(new PackageItemEntry
            {
                AutoID = null,
                ItemID = item.MasterAccountID ?? "",
                ItemName = item.AccountName ?? "",
                IsDeferred = false,
                UnitPV = item.SalesPrice,
                UOM = "unit",
                Qty = 1
            });
            IsPackageItemPickerOpen = false;
        }

        private void RemovePackageItem(PackageItemEntry entry) => PackageItems.Remove(entry);

        // --- Membership Credit Management ---
        private void AddMemberType(MembershipTypeDM mt)
        {
            if (MembershipCreditEntries.Count >= 1) return;
            MembershipCreditEntries.Add(new MembershipCreditEntry
            {
                memberTypeID = mt.MemberTypeID,
                memberCredit = 0,
                saveAction = "Added",
                isDirty = true
            });
            IsMemberTypePickerOpen = false;
        }

        private void RemoveMemberType(MembershipCreditEntry entry) => MembershipCreditEntries.Remove(entry);

        private async System.Threading.Tasks.Task SaveItem()
        {
            if (string.IsNullOrWhiteSpace(NewItem.ItemName) || NewItem.Price <= 0)
            {
                ErrorMessage = "Please fill in the Name and Price fields.";
                IsErrorPopupOpen = true;
                return;
            }

            IsSaving = true;
            StateHasChanged();
            try
            {
                bool isEdit = !string.IsNullOrEmpty(_editingMasterAccountID);
                int inventoryTypeId = ViewType == "Package" ? 5 : 7;

                // API saves credits via scalar fields inside objInventory (lstMembershipCredit is read-only/derived)
                var firstCredit = MembershipCreditEntries.FirstOrDefault();

                var objInventory = new InventoryCreateModel
                {
                    saveAction = isEdit ? "Changed" : "Added",
                    masterAccountID = isEdit ? _editingMasterAccountID : null,
                    inventoryTypeID = inventoryTypeId,
                    isDirty = true,
                    accountName = NewItem.ItemName,
                    salesPrice = NewItem.Price,
                    hasPackage = PackageItems.Any(),
                    validityDays = NewItem.Duration ?? 0,
                    memberExpiryDays = MemberExpiryDays,
                    triggeredMemberTypeID = firstCredit?.memberTypeID,
                    memberMainAccountCredit = firstCredit != null ? (double)firstCredit.memberCredit : 0,
                    accountStatus = "Active",
                    isSold = true,
                    branchID = AppState.SelectedBranchID,
                    lstPackage = PackageItems.Select(p => new DtoPackageItem
                    {
                        autoID = p.AutoID,
                        inventoryID = p.ItemID,
                        description = p.ItemName,
                        quantity = p.Qty,
                        unitPrice = p.UnitPV,
                        totalPrice = p.UnitPV * p.Qty,
                        unitActualValue = p.UnitPV,
                        totalActualValue = p.UnitPV * p.Qty,
                        isDeferred = p.IsDeferred,
                        saveAction = string.IsNullOrEmpty(p.AutoID) ? "Added" : "Changed",
                        isDirty = true
                    }).ToList()
                };

                var request = new InventoryFullCreateRequest
                {
                    objInventory = objInventory,
                    lstMasterAccount_Branch = new List<MasterAccountBranchEntry>
                    {
                        new MasterAccountBranchEntry
                        {
                            branchID = AppState.SelectedBranchID,
                            groupID = AppState.SelectedBranchGroupID,
                            isEnabled = true,
                            branchPrice = NewItem.Price,
                            saveAction = "Added",
                            isDirty = true
                        }
                    }
                };

                bool success;
                string message;

                if (isEdit)
                {
                    (success, message) = await InventoryService.UpdatePackageFullAsync(request);
                }
                else
                {
                    string? newId;
                    (success, message, newId) = await InventoryService.SavePackageFullAsync(request);
                    if (success && newId != null)
                        _editingMasterAccountID = newId;
                }

                if (success)
                {
                    // Refresh the list from the server
                    var allItems = await InventoryService.LoadItemsAsync(AppState.SelectedBranchID);
                    if (allItems != null)
                    {
                        int typeFilter = ViewType == "Package" ? 5 : 7;
                        MenuDb = allItems
                            .Where(i => i.InventoryTypeID == typeFilter)
                            .Select(i => new PackageItem
                            {
                                MasterAccountID = i.MasterAccountID ?? "",
                                ItemType = ViewType,
                                ItemName = i.AccountName ?? "",
                                Price = i.SalesPrice,
                                PackageSection = i.ItemGroupName ?? "",
                                DisplayImageUrl = i.ImagePath ?? "",
                                IsAvailable = i.AccountStatus?.Equals("Active", StringComparison.OrdinalIgnoreCase) == true
                            })
                            .ToList();
                    }
                    IsAddMode = false;
                }
                else
                {
                    ErrorMessage = string.IsNullOrEmpty(message) ? "Save failed. Please try again." : message;
                    IsErrorPopupOpen = true;
                }
            }
            catch (Exception ex)
            {
                ErrorMessage = "An error occurred: " + ex.Message;
                IsErrorPopupOpen = true;
                Console.WriteLine($"[Package.SaveItem] {ex}");
            }
            finally
            {
                IsSaving = false;
                StateHasChanged();
            }
        }

        private void CloseErrorPopup() => IsErrorPopupOpen = false;

        private async System.Threading.Tasks.Task HandleFileSelected(InputFileChangeEventArgs e)
        {
            IsUploadPopupOpen = false;
            if (e.File != null)
            {
                var format = "image/png";
                var resizedImage = await e.File.RequestImageFileAsync(format, 400, 400);
                var buffer = new byte[resizedImage.Size];
                using var stream = resizedImage.OpenReadStream();
                int totalRead = 0;
                while (totalRead < buffer.Length)
                {
                    int read = await stream.ReadAsync(buffer, totalRead, buffer.Length - totalRead);
                    if (read == 0) break;
                    totalRead += read;
                }
                _tempImagePreview = $"data:{format};base64,{Convert.ToBase64String(buffer)}";
                NewItem.PackageFolder = $"c://image/{e.File.Name}";
                NewItem.DisplayImageUrl = _tempImagePreview;
                NewItem.ImageName = e.File.Name;
            }
        }

        private void SelectCategoryFromPopup(string cat) { NewItem.PackageSection = cat; IsCategoryPopupOpen = false; }
        private async System.Threading.Tasks.Task DownloadReport(string format) { await JS.InvokeVoidAsync("window.print"); }

        private void RequestDelete(PackageItem item) { _itemToDelete = item; IsDeleteConfirmOpen = true; }

        private async System.Threading.Tasks.Task ConfirmDelete()
        {
            if (_itemToDelete == null) { IsDeleteConfirmOpen = false; return; }

            if (!string.IsNullOrEmpty(_itemToDelete.MasterAccountID))
            {
                var (success, message) = await InventoryService.DeleteItemAsync(_itemToDelete.MasterAccountID);
                if (!success)
                {
                    ErrorMessage = $"Delete failed: {message}";
                    IsErrorPopupOpen = true;
                    _itemToDelete = null;
                    IsDeleteConfirmOpen = false;
                    return;
                }
            }

            MenuDb.Remove(_itemToDelete);
            _itemToDelete = null;
            IsDeleteConfirmOpen = false;
        }

        private async System.Threading.Tasks.Task ShowItemDetailsAsync(PackageItem item)
        {
            NewItem = new PackageItem
            {
                Id = item.Id,
                MasterAccountID = item.MasterAccountID,
                ItemType = item.ItemType,
                PackageSection = item.PackageSection,
                PackageFolder = item.PackageFolder,
                DisplayImageUrl = item.DisplayImageUrl,
                Station = item.Station,
                ItemName = item.ItemName,
                Price = item.Price,
                Duration = item.Duration,
                Barcode = item.Barcode,
                FreePoint = item.FreePoint,
                RedeemPoint = item.RedeemPoint,
                BillOfMaterial = item.BillOfMaterial,
                Policy = item.Policy,
                TermCondition1 = item.TermCondition1,
                TermCondition2 = item.TermCondition2,
                TermCondition3 = item.TermCondition3,
                ImageName = item.ImageName,
                Unit = item.Unit,
                LowStockAlert = item.LowStockAlert,
                Credits = item.Credits,
                Points = item.Points,
                PackageValue = item.PackageValue,
                VoucherList = new List<string>(item.VoucherList)
            };
            _tempImagePreview = item.DisplayImageUrl;
            IsMinMaxPriceEnabled = NewItem.MinPrice > 0 || NewItem.MaxPrice > 0;
            IsRedeemPointEnabled = NewItem.RedeemPoint > 0;
            SelectedOutletCodes.Clear();
            foreach (var o in OutletList) SelectedOutletCodes.Add(o.Code);
            _editingMasterAccountID = item.MasterAccountID;
            MembershipCreditEntries.Clear();
            MemberExpiryDays = 365;
            PackageItems.Clear();
            ActiveAddTab = "Info";

            // Load full package detail from API (membership credits + package items)
            if (!string.IsNullOrEmpty(item.MasterAccountID))
            {
                try
                {
                    var detail = await InventoryService.LoadFullPackageDetailAsync(item.MasterAccountID);
                    if (detail != null)
                    {
                        var src = detail.objInventory ?? detail;

                        MemberExpiryDays = src.memberExpiryDays > 0 ? src.memberExpiryDays
                            : (detail.memberExpiryDays > 0 ? detail.memberExpiryDays : 365);

                        if (src.lstMembershipCredit?.Any() == true)
                        {
                            MembershipCreditEntries = src.lstMembershipCredit
                                .Select(mc => new MembershipCreditEntry
                                {
                                    memberTypeID = mc.memberTypeID,
                                    memberCredit = mc.memberCredit,
                                    saveAction = "Changed",
                                    isDirty = false
                                })
                                .ToList();
                        }
                        else if (!string.IsNullOrEmpty(src.triggeredMemberTypeID) && src.memberMainAccountCredit > 0)
                        {
                            // Fallback: scalar credit fields (single member type)
                            MembershipCreditEntries.Add(new MembershipCreditEntry
                            {
                                memberTypeID = src.triggeredMemberTypeID,
                                memberCredit = (decimal)src.memberMainAccountCredit,
                                saveAction = "Changed",
                                isDirty = false
                            });
                        }

                        var pkgItems = src.lstPackage ?? detail.lstPackage;
                        if (pkgItems != null)
                        {
                            PackageItems = pkgItems.Select(p => new PackageItemEntry
                            {
                                AutoID = p.autoID,
                                ItemID = p.inventoryID,
                                ItemName = p.description ?? "",
                                IsDeferred = p.isDeferred,
                                UnitPV = p.unitPrice,
                                UOM = "unit",
                                Qty = p.quantity
                            }).ToList();
                        }
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[Package] LoadFullPackageDetail error: {ex.Message}");
                }
            }

            IsAddMode = true;
        }

        protected void SelectSec(string section) => SelectedSec = section;

        private void ToggleUnitDropdown() => IsUnitDropdownOpen = !IsUnitDropdownOpen;
        private void SelectUnit(string unit) { NewItem.Unit = unit; IsUnitDropdownOpen = false; }
    }
}
