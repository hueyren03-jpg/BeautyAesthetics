using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.JSInterop;
using System.Collections.ObjectModel;
using EBI.UC;
using SenangRetails.Shared.Models.DTOs.MembersCredit;
using SenangRetails.Shared.Enums;
using SenangRetails.Shared.Services.InventoryService;
using SenangRetails.Shared.Services.CashDiscountService;
using SenangRetails.Shared.ApiClient;
using SenangRetails.Shared.Entities;
using SenangRetails.Shared.Models;
using SenangRetails.Shared.Models.DTOs;
using SenangRetails.Shared.Models.DTOs.MembersPackage;
using SenangRetails.Shared.Models.DTOs.MembersBalanceSummary;
using SenangRetails.Shared.Models.Entities;
using SenangRetails.Shared.Services;
using SenangRetails.Shared.Services.BluetoothPrinterService;
using SenangRetails.Shared.Services.CashSalesService;
using SenangRetails.Shared.Services.CustomerService;
using SenangRetails.Shared.Services.MembershipTypeService;
using SenangRetails.Shared.Services.MembersPackageService;
using SenangRetails.Shared.Services.MembersCreditService;
using SenangRetails.Shared.Services.NotificationService;
using SenangRetails.Shared.Services.ReceiptPrinterService;
using SenangRetails.Shared.Services.WhatsappService;
using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.Reflection.Emit;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using static SenangRetails.Shared.ApiClient.CustomerAC;
using static SenangRetails.Shared.Pages.Members;
using System.Globalization;
using CsvHelper;
using EBI.DM;
using EBI.Enum;

namespace SenangRetails.Shared.Pages
{
    public partial class Members
    {
        [Inject] private ICustomerService _customerService { get; set; } = default!;
        [Inject] private IMembersPackageService _memberPackageService { get; set; } = default!;
        [Inject] private IMembersCreditService MembersCreditService { get; set; } = default!;
        [Inject] private IInventoryService InventoryService { get; set; } = default!;
        [Inject] private ICashDiscountService cashDiscountService { get; set; } = default!;
        [Inject] private INotificationService NotificationSvc { get; set; } = default!;
        [Inject] private ICashSalesService SalesService { get; set; } = default!;
        [Inject] private IReceiptPrinterService ReceiptPrinterSvc { get; set; } = default!;
        [Inject] private IBluetoothPrinterService PrinterSvc { get; set; } = default!;
        [Inject] private CashSalesAC _cashSalesAC { get; set; } = default!;
        [Inject] private StaffService StaffService { get; set; } = default!;
        [Inject] private IMembershipTypeService MembershipTypeService { get; set; } = default!;
        [Inject] private SenangRetails.Shared.Services.PriceGroupService.IPriceGroupService PriceGroupService { get; set; } = default!;
        private List<StaffResponseDTO> staffList = new();
        private int totalApptCount = 0;
        private DateTime? firstApptDate = null;
        private DateTime? lastApptDate = null;
        private List<MembershipTypeDM> AllMembershipTypes = new();
        [Parameter]
        public string? MemberId { get; set; }

        private string activeFilter = "Recent";
        private CustomerDM? selectedMember;
        private CreditResponseDTO creditResponseDTO = new();
        private CustomerDM? MemberToUpdate;
        private Member? selectedMemberForUI;
        private int cartItemCount = 0;
        private bool sidebarOpen = false;
        private void GoBack()
        {
            var returnUrl = Nav.Uri.Contains("returnTo=admin") ? "/admin" : "/home";
            Nav.NavigateTo(returnUrl);
        }

        private string searchTerm = "";
        private List<CustomerDM> _customerList = new List<CustomerDM>();
        private CancellationTokenSource? _cts;

        public string SearchTerm
        {
            get => searchTerm;
            set
            {
                searchTerm = value;

                // Cancel the previous timer/search if the user types again
                _cts?.Cancel();
                _cts = new CancellationTokenSource();

                // Start a delayed task
                _ = DebouncedSearch(_cts.Token);
            }
        }

        private async Task DebouncedSearch(CancellationToken ct)
        {
            try
            {
                // Wait 300ms. If the user types again, this task is cancelled.
                await Task.Delay(300, ct);

                if (!ct.IsCancellationRequested)
                {
                    _customerList = await _customerService.SearchCustomer(searchTerm);
                    StateHasChanged();
                }
            }
            catch (TaskCanceledException)
            {
                // Normal behavior when typing fast
            }
        }

        //The dynamic list the UI loops through
        private IEnumerable<CustomerDM> FilteredMembers
        {
            get
            {
                if (_customerList == null || !_customerList.Any())
                    return Enumerable.Empty<CustomerDM>();

                var query = _customerList.Where(c => !string.Equals(c.AccountStatus, "Inactive", StringComparison.OrdinalIgnoreCase)).AsQueryable();
                var today = DateTime.Today;

                return activeFilter switch
                {
                    // Latest customers added, newest first (displays all)
                    "Recent" => query.OrderByDescending(c => c.CreatedDateTime),

                    // Customers added in the current calendar month
                    "New" => query.Where(c => c.CreatedDateTime.Year == today.Year &&
                                             c.CreatedDateTime.Month == today.Month)
                                  .OrderByDescending(c => c.CreatedDateTime),

                    // Birthday is today (matching Day and Month)
                    "Birthday" => query.Where(c => c.BirthdayMonth == today.Month &&
                                                  c.BirthdayDay == today.Day)
                                       .OrderBy(c => c.AccountName),

                    // Alphabetical order by name
                    "Name" => query.OrderBy(c => c.AccountName),

                    _ => query
                };
            }
        }

        private void SetFilter(string filter)
        {
            activeFilter = filter;

            StateHasChanged();
        }

        protected override async Task OnInitializedAsync()
        {
            _customerList = await _customerService.SearchCustomer("") ?? new List<CustomerDM>();

            if (!string.IsNullOrEmpty(MemberId))
            {
                var match = _customerList.FirstOrDefault(c => c.MasterAccountID == MemberId);
                if (match != null)
                {
                    await SelectMember(match);
                    SelectMemberForUI();
                }
            }
            var mtResponse = await MembershipTypeService.GetAllMembershipTypesAsync();
            if (mtResponse != null)
            {
                AllMembershipTypes = mtResponse.ToList();
            }
            var staffResponse = await StaffService.GetStaffListAsync();

            if (staffResponse != null && staffResponse.StatusCode == 200)
            {
                staffList = staffResponse.Result ?? new List<StaffResponseDTO>();
            }
            await LoadSettings();
            await LoadSales();
            StateHasChanged();
        }

        private MemberOtherBalanceSummaryResult? otherBalanceSummary;

        private async Task LoadCredits()
        {
            var response = await _customerService.GetCreditBalance(selectedMember?.MasterAccountID);
            if (response.result != null)
            {
                creditResponseDTO = response.result;
            }

            if (selectedMember != null && !string.IsNullOrEmpty(selectedMember.MasterAccountID))
            {
                var otherBalanceResponse = await _customerService.GetMemberOtherBalanceSummaryAsync(selectedMember.MasterAccountID, DateTime.UtcNow);
                if (otherBalanceResponse != null)
                {
                    otherBalanceSummary = otherBalanceResponse.FirstOrDefault();
                }
            }
        }

        private string GetStaffName(string? staffId)
        {
            if (string.IsNullOrEmpty(staffId))
                return LangSvc.GetText("Unassigned");

            var staff = staffList.FirstOrDefault(s =>
                string.Equals(s.MasterAccountID, staffId, StringComparison.OrdinalIgnoreCase));

            return staff?.AccountName ?? staffId;
        }
        private void ToggleSidebar() => sidebarOpen = !sidebarOpen;

        private void CloseSidebarOnMobile()
        {
            if (sidebarOpen) sidebarOpen = false;
        }

        private void OpenAddMemberModal()
        {
            newName = newPhone = newEmail = newIC = newEthnic = newMarital = newSource = newTier = newTags = "";
            newBirthday = null;
            newGender = "";
            showAddMemberModal = true;
            CloseSidebarOnMobile();
        }

        private void CloseAddMemberModal() => showAddMemberModal = false;

        private bool showMembershipPicker = false;
        private bool isEditModeTierSelection = false;

        private void OpenMembershipPicker(bool isEditMode)
        {
            isEditModeTierSelection = isEditMode;
            showMembershipPicker = true;
        }

        private void SelectMembershipType(string typeName)
        {
            if (isEditModeTierSelection)
            {
                editMemberTier = typeName;
            }
            else
            {
                newTier = typeName;
            }
            showMembershipPicker = false;
        }

        private bool showAddMemberModal = false;
        private string newName = "", newPhone = "", newEmail = "", newIC = "", newEthnic = "", newMarital = "", newSource = "", newTier = "", newTags = "";
        private DateTime? newBirthday = null;
        private string newGender = "";
        
        private bool isNumpadOpen;
        private string numpadInitialValue = "";
        private string numpadTarget = "";
        
        private void OpenNewPhoneNumpad()
        {
            numpadTarget = "newPhone";
            numpadInitialValue = newPhone;
            isNumpadOpen = true;
        }

        private void OpenEditPhoneNumpad()
        {
            numpadTarget = "editPhone";
            numpadInitialValue = editPhone;
            isNumpadOpen = true;
        }

        private void OnNumpadSaved(string val)
        {
            if (numpadTarget == "newPhone")
            {
                newPhone = val;
            }
            else if (numpadTarget == "editPhone")
            {
                editPhone = val;
            }
        }
        private bool showFormSettingsModal = false;
        private bool showManageGroupsModal = false;
        private string newGroupName = "";
        private string selectedLogic = "OR";
        private string selectedCategory = "";
        private string selectedCondition = "";

        private List<MemberGroup> memberGroups = new()
        {
            new MemberGroup { Id = -1, Name = "Recent", IsLocked = true },
            new MemberGroup { Id = -2, Name = "New", IsLocked = true },
            new MemberGroup { Id = -3, Name = "Birthday", IsLocked = true },
            new MemberGroup { Id = -4, Name = "Name", IsLocked = true }
        };

        private class FormFieldSettings
        {
            public bool ShowMobile { get; set; } = true;
            public bool ShowName { get; set; } = true;
            public bool ShowBirthday { get; set; } = true;
            public bool ShowGender { get; set; } = false;
            public bool ShowEmail { get; set; } = false;
            public bool ShowIC { get; set; } = false;
            public bool ShowEthnic { get; set; } = false;
            public bool ShowMarital { get; set; } = false;
            public bool ShowSource { get; set; } = false;
            public bool ShowTier { get; set; } = false;
            public bool ShowTag { get; set; } = false;
        }

        private FormFieldSettings formSettings = new();

        // Save the form settings
        private string SettingsKey = "memberFormSettings";
        protected override async Task OnAfterRenderAsync(bool firstRender)
        {
            if (firstRender)
            {
                await LoadSettings();
                await LoadSales();
                StateHasChanged();
            }
        }
        private async Task LoadSettings()
        {
            try
            {
                var savedJson = await JS.InvokeAsync<string>("localStorage.getItem", SettingsKey);
                if (!string.IsNullOrEmpty(savedJson))
                {
                    var savedSettings = JsonSerializer.Deserialize<FormFieldSettings>(savedJson);
                    if (savedSettings != null)
                    {
                        formSettings = savedSettings;
                        StateHasChanged(); // Refresh UI with loaded settings
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error loading settings: {ex.Message}");
            }
        }

        private async Task SaveSettings()
        {
            try
            {
                var json = JsonSerializer.Serialize(formSettings);
                await JS.InvokeVoidAsync("localStorage.setItem", SettingsKey, json);
                NotificationSvc.Add(new AppNotification
                {
                    Icon = "⚙️",
                    TitleKey = "NotifSettingsUpdatedTitle",
                    MessageKey = "NotifSettingsUpdatedMsg"
                });
                await JS.InvokeVoidAsync("localStorage.setItem", "app_notifications", NotificationSvc.ToJson());
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error saving settings: {ex.Message}");
            }
        }
        private class MemberGroup
        {
            public int Id { get; set; }
            public string Name { get; set; } = "";
            public bool IsLocked { get; set; }
        }

        private string editMemberTier = "";
        private string editRemarks = "";
        private string editInfo1 = "";
        private string editInfo2 = "";
        private string editInfo3 = "";
        private string editInfo4 = "";

        public class ChecklistItem
        {
            public int Id { get; set; }
            public string Title { get; set; } = "";
            public bool IsCompleted { get; set; }
            public DateTime? DueDate { get; set; }
            public DateTime CreatedDate { get; set; } = DateTime.Now;
        }

        public class MemberNote
        {
            public int Id { get; set; }
            public string Content { get; set; } = "";
            public DateTime CreatedDate { get; set; } = DateTime.Now;
            public string Category { get; set; } = "General";
        }

        public class FamilyMember
        {
            public int Id { get; set; }
            public string Name { get; set; } = "";
            public DateTime DateOfBirth { get; set; } = DateTime.Now;
            public string Relationship { get; set; } = "";
            public string IdentificationNumber { get; set; } = "";
            public string Gender { get; set; } = "";
            public string? ContactNumber { get; set; }
            public string? Email { get; set; }
            public string? Notes { get; set; }
            public DateTime CreatedDate { get; set; } = DateTime.Now;
        }

        public class Member
        {
            public int Id { get; set; }
            public string Avatar { get; set; } = "";
            public string? ProfilePhoto { get; set; }
            public string Name { get; set; } = "";
            public string Phone { get; set; } = "";
            public string IdNumber { get; set; } = "";
            public int Visits { get; set; }
            public List<string> Stats { get; set; } = new();
            public string? LastActivityType { get; set; }
            public string? LastActivityTime { get; set; }
            public decimal Credit { get; set; }
            public int Points { get; set; }
            public string MemberId { get; set; } = "";
            public string Gender { get; set; } = "";
            public DateTime BirthDate { get; set; }
            public string? Email { get; set; }
            public string? Ethnic { get; set; }
            public string? Marital { get; set; }
            public string? Source { get; set; }

            public string MemberTier { get; set; } = "VIP";

            public string Info1 { get; set; } = "";
            public string Info2 { get; set; } = "";
            public string Info3 { get; set; } = "";
            public string Info4 { get; set; } = "";

            //public List<MemberAddress> Addresses { get; set; } = new List<MemberAddress>();

            public List<BalanceTransaction> Transactions { get; set; } = new();
            //public List<string> Photos { get; set; } = new();
            //public List<ChecklistItem> ChecklistItems { get; set; } = new();
            //public List<MemberNote> Notes { get; set; } = new();
            //public List<FamilyMember> FamilyMembers { get; set; } = new();
            //public List<ConsentForm> ConsentForms { get; set; } = new();
            //public List<Referral> Referrals { get; set; } = new();
            //public List<MemberRemark> Remarks { get; set; } = new List<MemberRemark>();
            //public List<DocumentFile> DocumentFiles { get; set; } = new();
            //public List<BeautyRecord> BeautyRecords { get; set; } = new();
            //public List<FormResponse> FormResponses { get; set; } = new();
            public List<SalesRecord> SalesRecords { get; set; } = new();
        }

        public class MemberAddress
        {
            public string Line1 { get; set; } = "";
            public string Line2 { get; set; } = "";
            public string Line3 { get; set; } = "";
            public string PostalCode { get; set; } = "";
            public string City { get; set; } = "";           // or use a City model later
            public string AddressType { get; set; } = "";    // Home, Office, Other, etc.
        }

        private List<MemberAddress> editAddresses = new();

        public class BalanceTransaction
        {
            public DateTime DateTime { get; set; } = DateTime.Now;
            public string Type { get; set; } = "";
            public decimal Amount { get; set; }
            public bool IsPoints { get; set; }
        }

        private class VoucherItem
        {
            public string Category { get; set; } = "";
            public string Name { get; set; } = "";
            public int Quantity { get; set; }
            public string Description { get; set; } = "";
            public decimal? Value { get; set; }
            public DateTime? ExpiryDate { get; set; }
        }

        private List<VoucherItem> vouchers = new()
        {
            new VoucherItem { Category = "Service", Name = "Treatment", Quantity = 8, Description = "Valid until 31 Dec 2026 • Any facial / massage", Value = 62.50m, ExpiryDate = new DateTime(2026, 12, 31) },
            new VoucherItem { Category = "Service", Name = "Treatment", Quantity = 8, Description = "Valid until 31 Dec 2026 • Any facial / massage", Value = 62.50m, ExpiryDate = new DateTime(2026, 1, 13) },
            new VoucherItem { Category = "Service", Name = "Hair Wash + Cut", Quantity = 3, Description = "Valid until 15 Mar 2026", Value = 45.00m, ExpiryDate = new DateTime(2026, 3, 15) },
            new VoucherItem { Category = "Product", Name = "Skincare Set", Quantity = 2, Description = "RM150 value • Redeem in-store", Value = 150.00m, ExpiryDate = new DateTime(2026, 6, 30) },
            new VoucherItem { Category = "Product", Name = "Hair Oil 100ml", Quantity = 1, Description = "Free with purchase", Value = null, ExpiryDate = new DateTime(2026, 8, 31) },
            new VoucherItem { Category = "Package", Name = "10-Session Package", Quantity = 1, Description = "Valid for 12 months", Value = 880.00m, ExpiryDate = new DateTime(2027, 1, 29) }
        };

        private int GetTotalVouchers() => vouchers.Sum(v => v.Quantity);
        private int GetTotalPackages() => vouchers.Where(v => v.Category == "Package").Sum(v => v.Quantity);
        private int GetTotalVouchersForTab() => vouchers.Where(v => v.Category == activeVoucherTab).Sum(v => v.Quantity);
        private List<VoucherItem> GetVouchersForTab() => vouchers.Where(v => v.Category == activeVoucherTab).ToList();
        private List<VoucherItem> GetPackages() => vouchers.Where(v => v.Category == "Package").ToList();

        private bool showBalanceModal = false;
        private bool isPointsMode = false;
        private bool showTopUpModal = false;
        private bool showRedeemModal = false;
        private bool showVoucherModal = false;
        private string topUpAmount = "";
        private string redeemAmount = "";
        private bool showMaxRedeemHint = false;
        private decimal MaxRedeemable => selectedMemberForUI?.Credit ?? 0m;
        private bool showContactOptions = false;
        private bool showCallConfirmation = false;
        private CustomerDM? contactMember;
        private string activeVoucherTab = "Service";

        private bool showVoucherDetailModal = false;
        private VoucherItem? selectedVoucherDetail = null;
        private string activeVoucherDetailTab = "Available";

        private bool showEditVoucherModal = false;

        //        // New modals for Checklist, Notes, and Family
        //        private bool showChecklistModal = false;
        //        private bool showNotesModal = false;
        //        private bool showFamilyModal = false;
        //        private string newChecklistItem = "";
        //        private string newNoteContent = "";
        //        private string newNoteCategory = "General";
        //        private DateTime? newChecklistDueDate = null;

        //        // Family modal properties
        //        private FamilyMember newFamilyMember = new();
        //        private bool showAddFamilyForm = false;
        //        private bool isEditingFamily = false;

        //        private readonly List<string> relationshipOptions = new()
        //        {
        //            "Father", "Mother", "Uncle", "Aunt", "Cousin (Female)",
        //            "Sister-in-law", "Brother", "Husband", "Sister", "Nephew",
        //            "Daughter", "Son", "Brother-in-law", "Grandfather",
        //            "Grandmother", "Niece", "Wife", "Cousin (Male)",
        //            "Friend (Male)", "Friend (Female)", "Self"
        //        };

        //        private void ToggleSidebar() => sidebarOpen = !sidebarOpen;

        //        private void CloseSidebarOnMobile()
        //        {
        //            if (sidebarOpen) sidebarOpen = false;
        //        }

        private void OpenFormSettings() => showFormSettingsModal = true;
        private async Task CloseFormSettingsModal()
        {
            showFormSettingsModal = false;
            await SaveSettings();
        }
        //private void CloseFormSettingsModal() => showFormSettingsModal = false;

        //        private void OpenManageGroupsModal()
        //        {
        //            newGroupName = "";
        //            selectedLogic = "OR";
        //            selectedCategory = "";
        //            selectedCondition = "";
        //            showManageGroupsModal = true;
        //            CloseSidebarOnMobile();
        //        }

        //        private void CloseManageGroupsModal() => showManageGroupsModal = false;

        //        private void CreateNewGroup()
        //        {
        //            if (string.IsNullOrWhiteSpace(newGroupName) || string.IsNullOrEmpty(selectedCategory) || string.IsNullOrEmpty(selectedCondition)) return;
        //            var newId = memberGroups.Any() ? memberGroups.Max(g => g.Id) + 1 : 1;
        //            memberGroups.Add(new MemberGroup { Id = newId, Name = newGroupName.Trim(), IsLocked = false });
        //            newGroupName = "";
        //            selectedCategory = "";
        //            selectedCondition = "";
        //            ShowNotification("New group created successfully");
        //        }

        //        private void DeleteGroup(MemberGroup group)
        //        {
        //            if (group.IsLocked) return;
        //            memberGroups.Remove(group);
        //            ShowNotification($"Group '{group.Name}' deleted");
        //        }
        private bool isSaving = false;

        private async Task SaveNewMember()
        {
            if (string.IsNullOrWhiteSpace(newName)) { ShowNotification(LangSvc.GetText("NameRequired")); return; }
            if (string.IsNullOrWhiteSpace(newPhone)) { ShowNotification(LangSvc.GetText("MobileRequired")); return; }

            if (newBirthday.HasValue && newBirthday.Value.Date > DateTime.Today)
            {
                ShowNotification(LangSvc.GetText("MemberBirthdateFutureError"));
                return;
            }

            if (newPhone.Any(c => (c >= 'a' && c <= 'z') || (c >= 'A' && c <= 'Z')))
            {
                ShowNotification(LangSvc.GetText("MemberPhoneLettersError"));
                return;
            }

            isSaving = true;

            var request = new CustomerDM
            {
                MasterAccountID = "string",
                //VisibleToBranch = "ALL",
                // UI FIELDS
                AccountName = newName?.Trim() ?? "",
                Phone = newPhone?.Trim() ?? "",
                BirthdayDay = newBirthday?.Day ?? 0,
                BirthdayMonth = newBirthday?.Month ?? 0,
                BirthdayYear = newBirthday?.Year ?? 0,
                Gender = newGender,
                Email = newEmail?.Trim() ?? "",
                NRIC = newIC?.Trim() ?? "",
                RaceName = newEthnic?.Trim() ?? "",
                MaritalStatus = newMarital ?? "",
                CustomerSourceName = newSource?.Trim() ?? "",
                MembershipTypeName = newTier,
                SaveAction = EntityState.Added
            };

            // 3. Call Service
            var response = await _customerService.CreateCustomer(request);

            if (response != null && response.statusCode == 200)
            {
                // SUCCESS
                ShowNotification(LangSvc.GetText("MemberCreatedSuccess"));
                NotificationSvc.Add(new AppNotification
                {
                    Icon = "👤",
                    TitleKey = "NotifNewMemberTitle",
                    MessageKey = "NotifNewMemberMsg",
                    MessageParam = request.AccountName
                });
                await JS.InvokeVoidAsync("localStorage.setItem", "app_notifications", NotificationSvc.ToJson());
                CloseAddMemberModal();
                ResetForm();
                await _customerService.SearchCustomer(searchTerm);
            }
            else
            {
                var resultMsg = response?.message?.Trim().Replace(" ", "").ToLower() ?? "";

                var nameInError = response?.message?.Split("with").Last().Trim() ?? "";

                if (resultMsg.Contains("phonenumber"))
                {
                    ShowNotification($"Phone Number [{newPhone}] has already been registered.");
                }
                else if (resultMsg.Contains("nric"))
                {
                    ShowNotification($"Duplicate NRIC detected with {nameInError}");
                }
                else
                {
                    ShowNotification(response?.message ?? "Error creating customer");
                }
            }

            isSaving = false;
        }

        private void ResetForm()
        {
            newName = "";
            newPhone = "";
            newEmail = "";
            newIC = "";
            newBirthday = null;
            newGender = "";
            newMarital = "";
            newSource = "";
            newTier = "";
        }
        //private void SaveNewMember()
        //{
        //    if (formSettings.ShowName && string.IsNullOrWhiteSpace(newName)) { ShowNotification("Name is required."); return; }
        //    if (formSettings.ShowMobile && string.IsNullOrWhiteSpace(newPhone)) { ShowNotification("Mobile is required."); return; }

        //    var newId = members.Any() ? members.Max(m => m.Id) + 1 : 1;
        //    var newMember = new Member
        //    {
        //        Id = newId,
        //        Avatar = GetInitials(newName),
        //        Name = newName?.Trim() ?? "",
        //        Phone = newPhone?.Trim() ?? "",
        //        BirthDate = newBirthday ?? DateTime.Today,
        //        Gender = newGender,
        //        IdNumber = string.IsNullOrEmpty(newIC) ? $"......{new Random().Next(1000, 9999)}" : newIC.Trim(),
        //        LastActivityType = "New Member",
        //        LastActivityTime = DateTime.Now.ToString("dd MMMM, hh:mm tt"),
        //        MemberId = (members.Any() ? int.Parse(members.Max(m => m.MemberId)) + 1 : 35000000).ToString(),
        //        Credit = 0,
        //        Points = 0,
        //        Visits = 0,
        //        Stats = new List<string>(),
        //        Transactions = new List<BalanceTransaction>(),
        //        ChecklistItems = new List<ChecklistItem>(),
        //        Notes = new List<MemberNote>(),
        //        FamilyMembers = new List<FamilyMember>()
        //    };

        //    members.Add(newMember);
        //    selectedMember = newMember;
        //    CloseAddMemberModal();
        //    ShowNotification($"Member {newName?.Trim() ?? "New"} added successfully.");
        //}

        //        private string GetInitials(string? name)
        //        {
        //            if (string.IsNullOrWhiteSpace(name)) return "NA";
        //            var parts = name.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
        //            return parts.Length >= 2 ? $"{parts[0][0]}{parts[^1][0]}".ToUpper() : parts[0][0].ToString().ToUpper();
        //        }

        private List<CustomerServiceRecordsDM> customerServiceRecords = new();
        private bool isLoadingServiceRecords = false;

        private async Task LoadCustomerServiceRecords()
        {
            if (selectedMember == null || string.IsNullOrEmpty(selectedMember.MasterAccountID))
            {
                customerServiceRecords.Clear();
                return;
            }

            isLoadingServiceRecords = true;
            try
            {
                var now = DateTime.Today;
                var response = await _customerService.GetCustomerServiceRecordByMonthAsync(selectedMember.MasterAccountID, now.Year, now.Month);
                if (response?.statusCode == 200 && response.result != null)
                {
                    customerServiceRecords = response.result.Values.SelectMany(x => x).ToList();
                }
                else
                {
                    customerServiceRecords.Clear();
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error loading customer service records: {ex.Message}");
                customerServiceRecords.Clear();
            }
            finally
            {
                isLoadingServiceRecords = false;
                StateHasChanged();
            }
        }

        private async Task SelectMember(CustomerDM member)
        {
            selectedMember = member;
            await LoadCredits();
            await GetCashSales();
            await LoadActivePakages();
            await LoadCustomerServiceRecords();
            CloseSidebarOnMobile();
        }

        private List<CashSales_Series_UnconsumedItemDM> ActivePackages = new();

        private async Task LoadActivePakages()
        {
            try
            {
                ActivePackages = await _memberPackageService.GetPackageBalanceByCustomerIDAsync(selectedMember.MasterAccountID);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error loading packages: {ex.Message}");
                ActivePackages = null;
            }

            //if (ActivePackages == null || !ActivePackages.Any())
            //{
            //    ActivePackages = new List<PackageBalanceResult>
            //    {
            //        new PackageBalanceResult
            //        {
            //            PackageName = "Package 1",
            //            NetBalanceAfterUtilised = 4,
            //            ExpiryDate = new DateTime(2026, 10, 1)
            //        },
            //        new PackageBalanceResult
            //        {
            //            PackageName = "Package 2",
            //            NetBalanceAfterUtilised = 10,
            //            ExpiryDate = new DateTime(2027, 1, 1)
            //        },
            //        new PackageBalanceResult
            //        {
            //            PackageName = "Package 3",
            //            NetBalanceAfterUtilised = 7,
            //            ExpiryDate = new DateTime(2026, 10, 31)
            //        }
            //    };
            //}
        }
        private List<Doc_CashSalesDM> cashSalesHistoryMonth = new List<Doc_CashSalesDM>();
        private DateTime FirstSales;
        private DateTime LastSales;
        private decimal SalesOfYear;
        private async Task GetCashSales()
        {
            if (AppState.CurrentBranch == null)
            {
                cashSalesHistoryMonth = new List<Doc_CashSalesDM>();
                return;
            }
            DateTime currentTime = DateTime.Now;
            var response = await _cashSalesAC.GetCashSalesAsync(AppState.CurrentBranch.BranchID, new DateTime(currentTime.Year, 1, 1), new DateTime(currentTime.Year, 12, 31));
            if (response != null && response.StatusCode == 200 && response.Result != null)
            {
                cashSalesHistoryMonth = response.Result;
            }
            else
            {
                cashSalesHistoryMonth = new List<Doc_CashSalesDM>();
            }
            cashSalesHistoryMonth = cashSalesHistoryMonth.Where(s => s.AccountID == selectedMember?.MasterAccountID).ToList();
            cashSalesHistoryMonth.OrderBy(s => s.CreatedDateTime);
            FirstSales = cashSalesHistoryMonth.Any() ? cashSalesHistoryMonth.First().FinancialDate : new DateTime();
            LastSales = cashSalesHistoryMonth.Any() ? cashSalesHistoryMonth.Last().FinancialDate : new DateTime();
            SalesOfYear = cashSalesHistoryMonth.Sum(s => s.TotalAfterTax);
        }

        private void SelectMemberForUI()
        {
            selectedMemberForUI = new Member
            {
                Name = "WJ"
            };

        }

        private void CloseMemberDetailsModal()
        {
            selectedMember = null;
        }

        //        private void SetFilter(string filter)
        //        {
        //            activeFilter = filter;
        //        }

        //        private void UploadPhoto() => ShowNotification("Uploading photo...");
        //        private void OpenChecklist() => showChecklistModal = true;
        //        private void OpenEngagement() => Nav.NavigateTo("/engagement");
        //        private void OpenFamily() => showFamilyModal = true;
        //        private void OpenNotes() => showNotesModal = true;

        private void ViewCart()
        {
            AppState.SelectedCustomer = selectedMember;
            Nav.NavigateTo("/orders");
        }

        private string GetAvatarColor(int id)
        {
            var colors = new[] { "#E1BEE7", "#CE93D8", "#F8BBD0", "#FFCDD2", "#FFCCBC", "#FFE0B2", "#FFF9C4", "#E6EE9C", "#C5E1A5", "#A5D6A7", "#80DEEA", "#B3E5FC" };
            return colors[id % colors.Length];
        }

        //        private string GetCategoryColor(string category)
        //        {
        //            return category switch
        //            {
        //                "General" => "#6c757d",
        //                "Follow-up" => "#0d6efd",
        //                "Medical" => "#dc3545",
        //                "Preferences" => "#198754",
        //                "Payment" => "#fd7e14",
        //                "Appointment" => "#6f42c1",
        //                _ => "#6c757d"
        //            };
        //        }
        private string GetCategoryText(string category)
        {
            return category switch
            {
                "General" => LangSvc.GetText("MemberCategoryGeneral"),
                "Follow-up" => LangSvc.GetText("MemberCategoryFollowUp"),
                "Medical" => LangSvc.GetText("MemberCategoryMedical"),
                "Preferences" => LangSvc.GetText("MemberCategoryPreferences"),
                "Payment" => LangSvc.GetText("MemberCategoryPayment"),
                "Appointment" => LangSvc.GetText("MemberCategoryAppointment"),
                _ => category
            };
        }

        private string GetGenderText(string gender)
        {
            return gender switch
            {
                "Male" => LangSvc.GetText("MemberGenderMale"),
                "Female" => LangSvc.GetText("MemberGenderFemale"),
                _ => gender
            };
        }

        private string GetBroadcastStatusText(string status)
        {
            return status switch
            {
                "Sent" => LangSvc.GetText("MemberBroadcastStatusSent"),
                "Draft" => LangSvc.GetText("MemberBroadcastStatusDraft"),
                "Failed" => LangSvc.GetText("MemberBroadcastStatusFailed"),
                _ => status
            };
        }

        private string GetAppointmentStatusText(string status)
        {
            return status switch
            {
                "Upcoming" => LangSvc.GetText("MemberTabUpcoming"),
                "Past" => LangSvc.GetText("MemberTabPast"),
                "No Show" => LangSvc.GetText("MemberTabNoShow"),
                _ => status
            };
        }

        private string GetNoAppointmentsTitle(string tab)
        {
            return tab switch
            {
                "Upcoming" => LangSvc.GetText("MemberNoUpcomingAppointments"),
                "Past" => LangSvc.GetText("MemberNoPastAppointments"),
                "No Show" => LangSvc.GetText("MemberNoNoShowAppointments"),
                _ => LangSvc.GetText("MemberNoAppointmentsInCategory")
            };
        }
        private string CleanErrorMessage(string message)
        {
            if (string.IsNullOrEmpty(message)) return "Unknown error";
            
            int jsonStartIndex = message.IndexOf('{');
            if (jsonStartIndex >= 0)
            {
                string jsonPart = message[jsonStartIndex..];
                try
                {
                    using var doc = System.Text.Json.JsonDocument.Parse(jsonPart);
                    if (doc.RootElement.TryGetProperty("Title", out var titleProp))
                    {
                        return titleProp.GetString() ?? message;
                    }
                    if (doc.RootElement.TryGetProperty("title", out var titlePropLower))
                    {
                        return titlePropLower.GetString() ?? message;
                    }
                }
                catch
                {
                    // Fall-safe: return original message
                }
            }
            return message;
        }

        private void ShowNotification(string message)
        {
            ShowAlertDialog("Notification", CleanErrorMessage(message));
        }

        private void ShowContactOptions(CustomerDM member)
        {
            contactMember = member;
            showContactOptions = true;
            showCallConfirmation = false;
        }

        private void CloseContactOptions()
        {
            showContactOptions = false;
            contactMember = null;
        }

        private string GetWhatsAppUrl()
        {
            // Clean characters: space, brackets, dashes, underscores
            var cleanPhone = contactMember.Phone.Replace(" ", "").Replace("(", "").Replace(")", "")
                                               .Replace("-", "").Replace("_", "");

            if (cleanPhone.StartsWith("0"))
                cleanPhone = "60" + cleanPhone.Substring(1);
            else if (cleanPhone.StartsWith("+"))
                cleanPhone = cleanPhone.Substring(1);
            else if (!cleanPhone.StartsWith("60"))
                cleanPhone = "60" + cleanPhone;

            return $"https://wa.me/{cleanPhone}";
        }

        private async Task OpenWhatsApp()
        {
            if (contactMember == null || string.IsNullOrWhiteSpace(contactMember.Phone) || contactMember.Phone == "-")
            {
                ShowNotification("Member has no valid phone number registered.");
                return;
            }

            var url = GetWhatsAppUrl();

            try
            {
                // Try to use a common JS bridge that works for both
                // This is the safest way for Hybrid apps
                await JS.InvokeVoidAsync("open", url, "_top");
            }
            catch (Exception ex)
            {
                // If "window.open" fails (common on some mobile emulators), 
                // fallback to a standard location change
                await JS.InvokeVoidAsync("eval", $"window.location.href = '{url}'");
            }

            CloseContactOptions();
        }

        //private void OpenWhatsApp()
        //{
        //    if (contactMember == null || string.IsNullOrWhiteSpace(contactMember.Phone)) return;
        //    var cleanPhone = contactMember.Phone.Replace(" ", "").Replace("(", "").Replace(")", "").Replace("-", "").Replace("_", "");
        //    if (cleanPhone.StartsWith("0")) cleanPhone = "+60" + cleanPhone.Substring(1);
        //    else if (cleanPhone.StartsWith("60")) cleanPhone = "+" + cleanPhone;
        //    else if (!cleanPhone.StartsWith("+")) cleanPhone = "+60" + cleanPhone;
        //    var whatsappUrl = $"https://wa.me/{cleanPhone}";
        //    JS.InvokeVoidAsync("window.open", whatsappUrl, "_blank");
        //    CloseContactOptions();
        //}

        private void ShowCallConfirmation()
        {
            showContactOptions = false;
            showCallConfirmation = true;
        }

        private void CloseCallConfirmation()
        {
            showCallConfirmation = false;
            contactMember = null;
        }

        private async Task ConfirmCall()
        {
            // 1. Validation: Ensure we have a valid member and phone
            if (contactMember == null || string.IsNullOrWhiteSpace(contactMember.Phone) || contactMember.Phone == "-")
            {
                ShowNotification("No valid phone number available.");
                return;
            }

            // 2. Clean the phone number (remove non-numeric characters except +)
            var cleanPhone = contactMember.Phone.Replace(" ", "").Replace("(", "").Replace(")", "")
                                               .Replace("-", "").Replace("_", "");

            var telUrl = $"tel:{cleanPhone}";

            try
            {
                // Use window.open with _top to signal the system dialer
                // This is highly compatible with MAUI Android/iOS and Web Browsers
                await JS.InvokeVoidAsync("open", telUrl, "_top");
            }
            catch (Exception ex)
            {
                // Fallback for extreme cases (mostly older emulators)
                ShowNotification($"Redirecting to dialer for {cleanPhone}...");
                Console.WriteLine($"Call Error: {ex.Message}");
            }

            CloseCallConfirmation();
        }

        //private async void ConfirmCall()
        //{
        //    if (contactMember == null || string.IsNullOrWhiteSpace(contactMember.Phone)) return;
        //    var cleanPhone = contactMember.Phone.Replace(" ", "").Replace("(", "").Replace(")", "").Replace("-", "").Replace("_", "");
        //    var telUrl = $"tel:{cleanPhone}";
        //    try { await JS.InvokeVoidAsync("window.location.href", telUrl); }
        //    catch { ShowNotification($"Calling {contactMember.Phone}..."); }
        //    CloseCallConfirmation();
        //}

        private void OpenBalanceModal(bool isPoints)
        {
            if (selectedMember == null)
            {
                ShowNotification("Please select a customer first.");
                return;
            }

            isPointsMode = isPoints;
            if (!isPoints)
            {
                selectedCreditArapID = null;
                selectedCreditToRedeem = null;
                creditRedemptionHistory.Clear();
                _ = LoadCustomerWalletDetails();
            }
            showBalanceModal = true;
            topUpAmount = "";
            redeemAmount = "";
            showMaxRedeemHint = false;
        }

        private void OpenTopUp()
        {
            showBalanceModal = false;
            showTopUpModal = true;
            topUpAmount = "";
        }

        private void CloseTopUp()
        {
            showTopUpModal = false;
            topUpAmount = "";
            showBalanceModal = true;
        }

        private void SaveTopUp()
        {
            if (decimal.TryParse(topUpAmount, out var amount) && amount > 0)
            {
                var transaction = new BalanceTransaction
                {
                    DateTime = DateTime.Now,
                    Type = amount >= 500 ? "Topup (Manual)" : "Topup",
                    Amount = amount,
                    IsPoints = isPointsMode
                };
                if (isPointsMode)
                    selectedMemberForUI!.Points += (int)amount;
                else
                    selectedMemberForUI!.Credit += amount;
                selectedMemberForUI.Transactions.Add(transaction);
                ShowNotification($"Added {amount}{(isPointsMode ? " points" : " RM")}. New balance: {(isPointsMode ? selectedMemberForUI.Points : selectedMemberForUI.Credit.ToString("F2"))}");
            }
            CloseTopUp();
        }

        private void OpenRedeem()
        {
            showBalanceModal = false;
            showRedeemModal = true;
            redeemAmount = "";
            showMaxRedeemHint = false;
        }

        private void CloseRedeem()
        {
            showRedeemModal = false;
            redeemAmount = "";
            showMaxRedeemHint = false;
            showBalanceModal = true;
        }

        private void SaveRedeem()
        {
            if (decimal.TryParse(redeemAmount, out var amount) && amount > 0)
            {
                var max = isPointsMode ? selectedMemberForUI!.Points : selectedMemberForUI!.Credit;
                if (amount <= max)
                {
                    var transaction = new BalanceTransaction
                    {
                        DateTime = DateTime.Now,
                        Type = "Redeem",
                        Amount = amount,
                        IsPoints = isPointsMode
                    };
                    if (isPointsMode)
                        selectedMemberForUI!.Points -= (int)amount;
                    else
                        selectedMemberForUI!.Credit -= amount;
                    selectedMemberForUI.Transactions.Add(transaction);
                    ShowNotification($"Redeemed {amount}{(isPointsMode ? " points" : " RM")}. New balance: {(isPointsMode ? selectedMemberForUI.Points : selectedMemberForUI.Credit.ToString("F2"))}");
                }
            }
            CloseRedeem();
        }

        private void OpenVoucherModal()
        {
            activeVoucherTab = "Service";
            showVoucherModal = true;
        }

        private void RedeemVoucher()
        {
            var currentTabVouchers = GetVouchersForTab();
            if (currentTabVouchers.Any() && currentTabVouchers.First().Quantity > 0)
            {
                currentTabVouchers.First().Quantity--;
                ShowNotification($"Redeemed 1 voucher from {activeVoucherTab}. Remaining total: {GetTotalVouchers()}");
            }
            else
            {
                ShowNotification("No vouchers available to redeem in this category.");
            }
        }

        private void HandleNumpadInput(string input, bool isTopUp)
        {
            var current = isTopUp ? topUpAmount : redeemAmount;
            if (input == "⌫")
            {
                if (!string.IsNullOrEmpty(current))
                    current = current.Length > 1 ? current[..^1] : "";
            }
            else if (input == "." && !isPointsMode)
            {
                if (!current.Contains(".")) current += current == "" ? "0." : ".";
            }
            else if (char.IsDigit(input[0]))
            {
                if (current == "0") current = "";
                current += input;
            }
            if (isTopUp) topUpAmount = current;
            else redeemAmount = current;
        }

        private void OpenVoucherDetail(VoucherItem voucher)
        {
            selectedVoucherDetail = voucher;
            activeVoucherDetailTab = "Available";
            showVoucherDetailModal = true;
        }

        private void OpenEditVoucherModal()
        {
            showVoucherDetailModal = false;
            showEditVoucherModal = true;
        }

        private void RedeemThisVoucher()
        {
            if (selectedVoucherDetail!.Quantity > 0)
            {
                selectedVoucherDetail.Quantity--;
                ShowNotification($"Redeemed 1 × {selectedVoucherDetail.Name}. Remaining: {selectedVoucherDetail.Quantity}");
            }
            else
            {
                ShowNotification("No more vouchers to redeem.");
            }
        }

        private void ExpireThisVoucher()
        {
            ShowNotification($"Voucher '{selectedVoucherDetail?.Name}' marked as expired.");
            showEditVoucherModal = false;
        }

        private void DeleteThisVoucher()
        {
            if (selectedVoucherDetail != null && vouchers.Remove(selectedVoucherDetail))
            {
                ShowNotification($"Voucher '{selectedVoucherDetail.Name}' deleted.");
                showEditVoucherModal = false;
            }
        }

        //        private bool showPhotoUploadModal = false;
        //        private List<PhotoPreview> pendingPhotos = new();

        //        private class PhotoPreview
        //        {
        //            public string DataUrl { get; set; } = "";
        //            public string FileName { get; set; } = "";
        //            public bool Selected { get; set; } = true;
        //        }

        //        private async void HandleGalleryFiles(InputFileChangeEventArgs e)
        //        {
        //            LoadImageFiles(e.GetMultipleFiles());
        //        }

        //        private async void HandleCameraFile(InputFileChangeEventArgs e)
        //        {
        //            if (e.File != null)
        //            {
        //                LoadImageFiles(new[] { e.File });
        //            }
        //        }

        //        private async void LoadImageFiles(IEnumerable<IBrowserFile> files)
        //        {
        //            const long maxSizeBytes = 6 * 1024 * 1024; // 6 MB

        //            foreach (var file in files)
        //            {
        //                if (file.Size > maxSizeBytes)
        //                {
        //                    await JS.InvokeVoidAsync("alert", $"File {file.Name} is too large (max 6MB)");
        //                    continue;
        //                }

        //                if (!file.ContentType.StartsWith("image/"))
        //                {
        //                    await JS.InvokeVoidAsync("alert", $"File {file.Name} is not an image");
        //                    continue;
        //                }

        //                try
        //                {
        //                    using var stream = file.OpenReadStream(maxSizeBytes);
        //                    using var ms = new MemoryStream();
        //                    await stream.CopyToAsync(ms);

        //                    var base64 = Convert.ToBase64String(ms.ToArray());
        //                    var dataUrl = $"data:{file.ContentType};base64,{base64}";

        //                    pendingPhotos.Add(new PhotoPreview
        //                    {
        //                        DataUrl = dataUrl,
        //                        FileName = file.Name,
        //                        Selected = true
        //                    });
        //                }
        //                catch (Exception ex)
        //                {
        //                    await JS.InvokeVoidAsync("alert", $"Error reading {file.Name}: {ex.Message}");
        //                }
        //            }

        //            StateHasChanged();
        //        }

        //        private void OpenPhotoUploadModal()
        //        {
        //            pendingPhotos.Clear();
        //            showPhotoUploadModal = true;
        //        }

        //        private void ClosePhotoModal()
        //        {
        //            pendingPhotos.Clear();
        //            showPhotoUploadModal = false;
        //        }

        //        private void SaveSelectedPhotos()
        //        {
        //            if (selectedMember == null) return;

        //            var selectedPhotos = pendingPhotos.Where(p => p.Selected).ToList();

        //            foreach (var photo in selectedPhotos)
        //            {
        //                selectedMember.Photos.Add(photo.DataUrl);
        //            }

        //            ShowNotification($"Added {selectedPhotos.Count} photo{(selectedPhotos.Count == 1 ? "" : "s")} to gallery.");

        //            ClosePhotoModal();
        //        }

        private bool showProfileEditModal = false;
        private string activeProfileTab = "Info";

        private string editName = "";
        private string editPhone = "";
        private string editIC = "";
        private DateTime? editBirthday = null;
        private string editEthnic = "";
        private string editSource = "";
        private string editEmail = "";
        private string editMarital = "";
        private string editGender = "";
        private string editAddress1 = "";
        private string editAddress2 = "";
        private string editZipCode = "";
        private string editCity = "";
        private string editCountryState = "";
        private string editCountry = "";
        private bool isDeleteConfirmOpen = false;

        private void OpenProfileEditModal()
        {
            if (selectedMember == null) return;

            var currentId = selectedMember.MasterAccountID;
            Debug.WriteLine($"Opening Modal for Customer ID: {currentId}");

            // Info tab fields (already there)
            editName = selectedMember.AccountName ?? "";
            editPhone = selectedMember.Phone ?? "";
            editIC = selectedMember.NRIC ?? "";

            // Convert API fields (Year, Month, Day) to a DateTime object for the UI
            // Check if they have values before trying to create a DateTime
            if (selectedMember.BirthdayYear > 0 &&
                selectedMember.BirthdayMonth > 0 &&
                selectedMember.BirthdayDay > 0)
            {
                try
                {
                    editBirthday = new DateTime(
                        selectedMember.BirthdayYear,
                        selectedMember.BirthdayMonth,
                        selectedMember.BirthdayDay
                    );
                }
                catch
                {
                    editBirthday = null;
                }
            }
            else
            {
                editBirthday = null;
            }
            editEthnic = selectedMember.RaceName ?? "";
            editSource = selectedMember.CustomerSourceName ?? "";
            editEmail = selectedMember.Email ?? "";
            editMarital = selectedMember.MaritalStatus ?? "";
            editGender = selectedMember.Gender ?? "";

            // Details tab fields
            editMemberTier = selectedMember.MembershipTypeName ?? "";
            //editInfo1 = selectedMember.Info1 ?? "";
            //editInfo2 = selectedMember.Info2 ?? "";
            //editInfo3 = selectedMember.Info3 ?? "";
            //editInfo4 = selectedMember.Info4 ?? "";

            // Address tab
            editAddress1 = selectedMember.Address1 ?? "";
            editAddress2 = selectedMember.Address2 ?? "";
            editZipCode = selectedMember.ZipCode ?? "";
            editCity = selectedMember.City ?? "";
            editCountryState = selectedMember.CountryState ?? "";
            editCountry = selectedMember.Country ?? "";
            //editAddresses = selectedMember.Addresses
            //    .Select(a => new MemberAddress
            //    {
            //        Line1 = a.Line1,
            //        Line2 = a.Line2,
            //        Line3 = a.Line3,
            //        PostalCode = a.PostalCode,
            //        City = a.City,
            //        AddressType = a.AddressType
            //    })
            //    .ToList();

            activeProfileTab = "Info";
            showProfileEditModal = true;
        }

        private bool isCustomerUpdate = false;

        private async Task SaveProfileChanges()
        {
            if (selectedMember == null) return;

            if (string.IsNullOrWhiteSpace(editName))
            {
                ShowNotification(LangSvc.GetText("NameRequired"));
                return;
            }
            if (string.IsNullOrWhiteSpace(editPhone))
            {
                ShowNotification(LangSvc.GetText("MobileRequired"));
                return;
            }

            if (editBirthday.HasValue && editBirthday.Value.Date > DateTime.Today)
            {
                ShowNotification(LangSvc.GetText("MemberBirthdateFutureError"));
                return;
            }

            if (editPhone.Any(c => (c >= 'a' && c <= 'z') || (c >= 'A' && c <= 'Z')))
            {
                ShowNotification(LangSvc.GetText("MemberPhoneLettersError"));
                return;
            }

            if (!editPhone.All(c => char.IsDigit(c) || c == '+' || c == '-' || c == '(' || c == ')' || char.IsWhiteSpace(c)))
            {
                ShowNotification(LangSvc.GetText("MemberPhoneNumericError"));
                return;
            }

            try
            {
                isCustomerUpdate = true;
                MemberToUpdate = new CustomerDM
                {
                    MasterAccountID = selectedMember.MasterAccountID,
                    AccountName = editName.Trim(),
                    Phone = editPhone.Trim(),
                    Email = editEmail?.Trim() ?? "",
                    NRIC = editIC?.Trim() ?? "",
                    BirthdayDay = editBirthday?.Day ?? 0,
                    BirthdayMonth = editBirthday?.Month ?? 0,
                    BirthdayYear = editBirthday?.Year ?? 0,
                    RaceName = editEthnic?.Trim() ?? "",
                    CustomerSourceName = editSource?.Trim() ?? "",
                    MaritalStatus = editMarital, //API cant update marital status
                    Gender = editGender,
                    MembershipTypeName = editMemberTier,
                    Comment = editRemarks,
                    Address1 = editAddress1,
                    Address2 = editAddress2,
                    ZipCode = editZipCode,
                    City = editCity,
                    CountryState = editCountryState,
                    Country = editCountry,
                    SaveAction = EntityState.Changed,
                    IsLoading = false
                };
                //MemberToUpdate?.MasterAccountID = selectedMember.MasterAccountID;
                //// 2. Map UI fields back to the model
                //MemberToUpdate?.AccountName = editName.Trim();
                //MemberToUpdate?.Phone = editPhone.Trim();
                ////MemberToUpdate.NRIC = editIC?.Trim() ?? selectedMember.NRIC;
                //// Handle Birthday Conversion back to Year/Month/Day
                //if (editBirthday.HasValue)
                //{
                //    selectedMember.BirthdayYear = editBirthday.Value.Year;
                //    selectedMember.BirthdayMonth = editBirthday.Value.Month;
                //    selectedMember.BirthdayDay = editBirthday.Value.Day;
                //}
                //selectedMember.RaceName = editEthnic?.Trim() ?? selectedMember.RaceName;
                //selectedMember.CustomerSourceName = editSource?.Trim() ?? selectedMember.CustomerSourceName;
                //selectedMember.Email = editEmail?.Trim() ?? selectedMember.Email;
                //selectedMember.MaritalStatus = editMarital;
                //selectedMember.Gender = editGender;

                Debug.WriteLine($"Sending Update for ID: {selectedMember.MasterAccountID}");
                // 3. Call the API using your Service
                var response = await _customerService.UpdateCustomer(MemberToUpdate);

                if (response != null && response.statusCode == 200)
                {
                    selectedMember.AccountName = editName.Trim();
                    selectedMember.Phone = editPhone.Trim();
                    selectedMember.Email = editEmail?.Trim() ?? "";
                    selectedMember.NRIC = editIC?.Trim() ?? "";
                    selectedMember.BirthdayDay = editBirthday?.Day ?? 0;
                    selectedMember.BirthdayMonth = editBirthday?.Month ?? 0;
                    selectedMember.BirthdayYear = editBirthday?.Year ?? 0;
                    selectedMember.RaceName = editEthnic?.Trim() ?? "";
                    selectedMember.CustomerSourceName = editSource?.Trim() ?? "";
                    selectedMember.MaritalStatus = editMarital;
                    selectedMember.Gender = editGender;
                    selectedMember.MembershipTypeName = editMemberTier;
                    selectedMember.Comment = editRemarks;
                    selectedMember.Address1 = editAddress1;
                    selectedMember.Address2 = editAddress2;
                    selectedMember.ZipCode = editZipCode;
                    selectedMember.City = editCity;
                    selectedMember.CountryState = editCountryState;
                    selectedMember.Country = editCountry;
                    // 4. Success handling
                    //selectedMember.Avatar = GetInitials(selectedMember.AccountName);
                    ShowNotification(LangSvc.GetText("MemberProfileUpdatedSuccess"));
                    NotificationSvc.Add(new AppNotification
                    {
                        Icon = "✏️",
                        TitleKey = "NotifMemberUpdatedTitle",
                        MessageKey = "NotifMemberUpdatedMsg",
                        MessageParam = selectedMember.AccountName
                    });
                    await JS.InvokeVoidAsync("localStorage.setItem", "app_notifications", NotificationSvc.ToJson());
                    showProfileEditModal = false;
                }
                else
                {
                    // 5. API Error handling
                    ShowNotification($"Update failed: {response?.message ?? "Unknown error"}");
                }
            }
            catch (Exception ex)
            {
                ShowNotification(LangSvc.GetText("MemberSaveError"));
                Console.WriteLine($"[SAVE ERROR]: {ex.Message}");
            }
            finally
            {
                isCustomerUpdate = false;
            }

            //// Info tab
            //selectedMember.AccountName = editName.Trim();
            //selectedMember.Phone = editPhone.Trim();
            //selectedMember.NRIC = editIC?.Trim() ?? selectedMember.NRIC;
            //selectedMember.BirthdayDay = editBirthday ?? selectedMember.BirthdayDay;
            //selectedMember.Gender = editGender;
            //selectedMember.Ethnic = editEthnic?.Trim();
            //selectedMember.Source = editSource?.Trim();
            //selectedMember.Email = editEmail?.Trim();
            //selectedMember.Marital = editMarital;

            //// Details tab
            //selectedMember.MemberTier = editMemberTier;
            //selectedMember.Info1 = editInfo1?.Trim() ?? "";
            //selectedMember.Info2 = editInfo2?.Trim() ?? "";
            //selectedMember.Info3 = editInfo3?.Trim() ?? "";
            //selectedMember.Info4 = editInfo4?.Trim() ?? "";

            //// Save addresses back
            //selectedMember.Addresses = editAddresses
            //    .Select(ea => new MemberAddress
            //    {
            //        Line1 = ea.Line1?.Trim() ?? "",
            //        Line2 = ea.Line2?.Trim() ?? "",
            //        Line3 = ea.Line3?.Trim() ?? "",
            //        PostalCode = ea.PostalCode?.Trim() ?? "",
            //        City = ea.City?.Trim() ?? "",
            //        AddressType = ea.AddressType?.Trim() ?? ""
            //    })
            //    .ToList();

            //selectedMember.Avatar = GetInitials(selectedMember.Name);
            //ShowNotification("Profile updated successfully.");
            showProfileEditModal = false;
            // Trigger a UI refresh
            StateHasChanged();
        }

        //        private bool showProfilePhotoUploadModal = false;
        //        private List<PhotoPreview> pendingProfilePhotos = new();

        //        private void OpenProfilePhotoUploadModal()
        //        {
        //            pendingProfilePhotos.Clear();
        //            showProfilePhotoUploadModal = true;
        //        }

        //        private void CloseProfilePhotoModal()
        //        {
        //            pendingProfilePhotos.Clear();
        //            showProfilePhotoUploadModal = false;
        //        }

        //        private async void LoadProfileImageFiles(IEnumerable<IBrowserFile> files)
        //        {
        //            const long maxSizeBytes = 6 * 1024 * 1024;
        //            foreach (var file in files)
        //            {
        //                if (file.Size > maxSizeBytes)
        //                {
        //                    await JS.InvokeVoidAsync("alert", $"File {file.Name} is too large (max 6MB)");
        //                    continue;
        //                }
        //                if (!file.ContentType.StartsWith("image/"))
        //                {
        //                    await JS.InvokeVoidAsync("alert", $"File {file.Name} is not an image");
        //                    continue;
        //                }
        //                try
        //                {
        //                    using var stream = file.OpenReadStream(maxSizeBytes);
        //                    using var ms = new MemoryStream();
        //                    await stream.CopyToAsync(ms);
        //                    var base64 = Convert.ToBase64String(ms.ToArray());
        //                    var dataUrl = $"data:{file.ContentType};base64,{base64}";
        //                    pendingProfilePhotos.Add(new PhotoPreview
        //                    {
        //                        DataUrl = dataUrl,
        //                        FileName = file.Name,
        //                        Selected = true
        //                    });
        //                }
        //                catch (Exception ex)
        //                {
        //                    await JS.InvokeVoidAsync("alert", $"Error: {ex.Message}");
        //                }
        //            }
        //            pendingProfilePhotos.ForEach(p => p.Selected = false); // deselect old ones
        //                                                                   // The new one will be auto-selected in the UI loop or here
        //            StateHasChanged();
        //        }

        //        private void SaveProfilePhoto()
        //        {
        //            if (selectedMember == null || !pendingProfilePhotos.Any(p => p.Selected)) return;

        //            var selected = pendingProfilePhotos.First(p => p.Selected);

        //            // Set as profile picture
        //            selectedMember.ProfilePhoto = selected.DataUrl;

        //            // Optionally also add to gallery
        //            selectedMember.Photos.Add(selected.DataUrl);

        //            ShowNotification("Profile picture updated!");
        //            CloseProfilePhotoModal();
        //            StateHasChanged();
        //        }

        private void AddNewAddress()
        {
            editAddresses.Add(new MemberAddress());
            StateHasChanged();
        }

        private void RemoveAddress(int index)
        {
            if (index >= 0 && index < editAddresses.Count)
            {
                editAddresses.RemoveAt(index);
                StateHasChanged();
            }
        }

        protected string FormatDate(DateTime date, string formatType = "Full")
        {
            var lang = LangSvc.CurrentLanguage;
            if (lang == "Chinese")
            {
                string weekday = GetChineseWeekday(date.DayOfWeek);
                if (formatType == "Weekday")
                {
                    return weekday;
                }
                else if (formatType == "MonthDay")
                {
                    return $"{date.Month}月{date.Day}日";
                }
                else if (formatType == "Full")
                {
                    return $"{date.Year}年{date.Month}月{date.Day}日 {weekday}";
                }
                else
                {
                    return $"{date.Year}年{date.Month}月{date.Day}日";
                }
            }
            else if (lang == "Malay")
            {
                string weekday = GetMalayWeekday(date.DayOfWeek);
                string monthName = GetMalayMonth(date.Month, formatType == "MonthDay" || formatType == "Short");
                if (formatType == "Weekday")
                {
                    return weekday;
                }
                else if (formatType == "MonthDay")
                {
                    return $"{date.Day} {monthName}";
                }
                else if (formatType == "Full")
                {
                    return $"{weekday}, {date.Day} {monthName} {date.Year}";
                }
                else
                {
                    return $"{date.Day:00} {monthName} {date.Year}";
                }
            }
            else
            {
                if (formatType == "Weekday")
                {
                    return date.ToString("dddd");
                }
                else if (formatType == "MonthDay")
                {
                    return date.ToString("d MMM");
                }
                else if (formatType == "Full")
                {
                    return date.ToString("dddd, d MMM yyyy");
                }
                else
                {
                    return date.ToString("dd MMM yyyy");
                }
            }
        }

        private string GetChineseWeekday(DayOfWeek day)
        {
            return day switch
            {
                DayOfWeek.Monday => "星期一",
                DayOfWeek.Tuesday => "星期二",
                DayOfWeek.Wednesday => "星期三",
                DayOfWeek.Thursday => "星期四",
                DayOfWeek.Friday => "星期五",
                DayOfWeek.Saturday => "星期六",
                DayOfWeek.Sunday => "星期日",
                _ => ""
            };
        }

        private string GetMalayWeekday(DayOfWeek day)
        {
            return day switch
            {
                DayOfWeek.Monday => "Isnin",
                DayOfWeek.Tuesday => "Selasa",
                DayOfWeek.Wednesday => "Rabu",
                DayOfWeek.Thursday => "Khamis",
                DayOfWeek.Friday => "Jumaat",
                DayOfWeek.Saturday => "Sabtu",
                DayOfWeek.Sunday => "Ahad",
                _ => ""
            };
        }

        private string GetMalayMonth(int month, bool isShort)
        {
            if (isShort)
            {
                return month switch
                {
                    1 => "Jan",
                    2 => "Feb",
                    3 => "Mac",
                    4 => "Apr",
                    5 => "Mei",
                    6 => "Jun",
                    7 => "Jul",
                    8 => "Ogo",
                    9 => "Sep",
                    10 => "Okt",
                    11 => "Nov",
                    12 => "Dis",
                    _ => ""
                };
            }
            else
            {
                return month switch
                {
                    1 => "Januari",
                    2 => "Februari",
                    3 => "Mac",
                    4 => "April",
                    5 => "Mei",
                    6 => "Jun",
                    7 => "Julai",
                    8 => "Ogos",
                    9 => "September",
                    10 => "Oktober",
                    11 => "November",
                    12 => "Disember",
                    _ => ""
                };
            }
        }

        private void MergeMember()
        {
            ShowNotification(LangSvc.GetText("MemberMergeComingSoon"));
            // Future: open merge modal, select target member, etc.
        }

        //private void HideMember()
        //{            
        //    ShowNotification("Member hidden (visibility restricted)");
        //    // Future: set a IsHidden flag on member, refresh list
        //}

        private async Task HideMember()
        {
            if (selectedMember == null) return;

            bool confirm = await JS.InvokeAsync<bool>("confirm", $"Are you sure you want to deactivate {selectedMember.AccountName}?");
            if (!confirm) return;

            try
            {
                // 1. Create the DTO with the status change
                var hideRequest = new CustomerDM
                {
                    MasterAccountID = selectedMember.MasterAccountID,
                    AccountName = selectedMember.AccountName,
                    Phone = selectedMember.Phone,
                    Email = selectedMember.Email ?? "",

                    AccountStatus = "Inactive",
                    SaveAction = EntityState.Changed,
                    IsLoading = false
                };

                // Call the API
                var response = await _customerService.UpdateCustomer(hideRequest);

                if (response != null && response.statusCode == 200)
                {
                    // Update local memory so the UI refreshes
                    selectedMember.AccountStatus = "Inactive";
                    ShowNotification(string.Format(LangSvc.GetText("MemberDeactivatedSuccessMsg"), selectedMember.AccountName));

                    StateHasChanged();
                }
                else
                {
                    ShowNotification(LangSvc.GetText("MemberDeactivateFailed") + ": " + response?.message);
                }
            }
            catch (Exception ex)
            {
                ShowNotification(LangSvc.GetText("MemberDeactivateError"));
                Debug.WriteLine($"[HIDE ERROR]: {ex.Message}");
            }
        }

        private void DeleteMember()
        {
            if (selectedMember == null) return;
            isDeleteConfirmOpen = true;
        }

        private async Task ConfirmDeleteMember()
        {
            if (selectedMember == null) return;
            isDeleteConfirmOpen = false;

            try
            {
                var deleteRequest = new CustomerDM
                {
                    MasterAccountID = selectedMember.MasterAccountID,
                    AccountName = selectedMember.AccountName,
                    Phone = selectedMember.Phone,
                    Email = selectedMember.Email ?? "",
                    AccountStatus = "Inactive",
                    SaveAction = EntityState.Changed,
                    IsLoading = false
                };

                var response = await _customerService.UpdateCustomer(deleteRequest);

                if (response != null && response.statusCode == 200)
                {
                    selectedMember.AccountStatus = "Inactive";
                    ShowNotification(string.Format(LangSvc.GetText("MemberDeletedSuccessMsg"), selectedMember.AccountName));

                    // Clear selected member to close detail view/modal
                    selectedMember = null;
                    StateHasChanged();
                }
                else
                {
                    ShowNotification(LangSvc.GetText("MemberDeleteFailed") + ": " + (response?.message ?? "Unknown error."));
                }
            }
            catch (Exception ex)
            {
                ShowNotification(LangSvc.GetText("MemberDeleteError"));
                Debug.WriteLine($"[DELETE ERROR]: {ex.Message}");
            }
        }

        //        // Single selected avatar (URL or data URL)
        //        private string? selectedAvatarUrl = null;

        //        // Predefined avatar URLs (can host them or use placeholder URLs)
        //        private readonly string[] cutePeopleAvatars = new[]
        //        {
        //            "https://api.dicebear.com/9.x/avataaars/svg?seed=Cute1",
        //            "https://api.dicebear.com/9.x/avataaars/svg?seed=Cute2",
        //            "https://api.dicebear.com/9.x/avataaars/svg?seed=Cute3",
        //            "https://api.dicebear.com/9.x/avataaars/svg?seed=Cute4",
        //            "https://api.dicebear.com/9.x/avataaars/svg?seed=Cute5",
        //        };

        //        private readonly string[] livingSoulAvatars = new[]
        //        {
        //            "https://api.dicebear.com/9.x/avataaars/svg?seed=Soul1",
        //            "https://api.dicebear.com/9.x/avataaars/svg?seed=Soul2",
        //            "https://api.dicebear.com/9.x/avataaars/svg?seed=Soul3",
        //        };

        //        private readonly string[] professionalAvatars = new[]
        //        {
        //            "https://api.dicebear.com/9.x/avataaars/svg?seed=Prof1",
        //            "https://api.dicebear.com/9.x/avataaars/svg?seed=Prof2",
        //            "https://api.dicebear.com/9.x/avataaars/svg?seed=Prof3",
        //        };

        //        // Select only one photo/avatar at a time
        //        private void SelectSinglePhoto(PhotoPreview photo)
        //        {
        //            pendingProfilePhotos.ForEach(p => p.Selected = false);
        //            photo.Selected = true;
        //            selectedAvatarUrl = photo.DataUrl;
        //            StateHasChanged();
        //        }

        //        private void SelectPredefinedAvatar(string url)
        //        {
        //            pendingProfilePhotos.ForEach(p => p.Selected = false);
        //            selectedAvatarUrl = url;
        //            StateHasChanged();
        //        }

        //        // When saving (replace the old SaveProfilePhoto method)
        //        private void SaveSelectedProfilePicture()
        //        {
        //            if (selectedMember == null || string.IsNullOrEmpty(selectedAvatarUrl)) return;

        //            selectedMember.ProfilePhoto = selectedAvatarUrl;

        //            // Optional: also add to gallery if it's a user-uploaded photo
        //            if (pendingProfilePhotos.Any(p => p.Selected && p.DataUrl == selectedAvatarUrl))
        //            {
        //                selectedMember.Photos.Add(selectedAvatarUrl);
        //            }

        //            ShowNotification("Profile picture updated!");
        //            CloseProfilePhotoModal();
        //            StateHasChanged();
        //        }

        //        // Update camera & gallery handlers to support single selection
        //        private async void HandleProfileGalleryFiles(InputFileChangeEventArgs e)
        //        {
        //            var files = e.GetMultipleFiles(1); // limit to 1 for simplicity
        //            LoadProfileImageFiles(files);
        //        }

        //        private async void HandleProfileCameraFile(InputFileChangeEventArgs e)
        //        {
        //            if (e.File != null)
        //            {
        //                LoadProfileImageFiles(new[] { e.File });
        //            }
        //        }

        //        // Checklist and Notes methods
        //        private void AddChecklistItem()
        //        {
        //            if (string.IsNullOrWhiteSpace(newChecklistItem) || selectedMember == null) return;

        //            var newId = selectedMember.ChecklistItems.Any()
        //                ? selectedMember.ChecklistItems.Max(c => c.Id) + 1
        //                : 1;

        //            selectedMember.ChecklistItems.Add(new ChecklistItem
        //            {
        //                Id = newId,
        //                Title = newChecklistItem.Trim(),
        //                DueDate = newChecklistDueDate,
        //                CreatedDate = DateTime.Now
        //            });

        //            newChecklistItem = "";
        //            newChecklistDueDate = null;
        //            ShowNotification("Checklist item added!");
        //        }

        //        private void ToggleChecklistItem(ChecklistItem item)
        //        {
        //            item.IsCompleted = !item.IsCompleted;
        //            StateHasChanged();
        //        }

        //        private void DeleteChecklistItem(ChecklistItem item)
        //        {
        //            if (selectedMember != null)
        //            {
        //                selectedMember.ChecklistItems.Remove(item);
        //                ShowNotification("Item removed");
        //            }
        //        }

        //        private void AddNote()
        //        {
        //            if (string.IsNullOrWhiteSpace(newNoteContent) || selectedMember == null) return;

        //            var newId = selectedMember.Notes.Any()
        //                ? selectedMember.Notes.Max(n => n.Id) + 1
        //                : 1;

        //            selectedMember.Notes.Add(new MemberNote
        //            {
        //                Id = newId,
        //                Content = newNoteContent.Trim(),
        //                Category = newNoteCategory,
        //                CreatedDate = DateTime.Now
        //            });

        //            newNoteContent = "";
        //            newNoteCategory = "General";
        //            ShowNotification("Note added!");
        //        }

        //        private void DeleteNote(MemberNote note)
        //        {
        //            if (selectedMember != null)
        //            {
        //                selectedMember.Notes.Remove(note);
        //                ShowNotification("Note deleted");
        //            }
        //        }

        //        // Family Members methods
        //        private void CloseFamilyModal()
        //        {
        //            showFamilyModal = false;
        //            showAddFamilyForm = false;
        //            isEditingFamily = false;
        //            newFamilyMember = new FamilyMember();
        //        }

        //        private void ShowAddFamilyForm()
        //        {
        //            newFamilyMember = new FamilyMember();
        //            newFamilyMember.DateOfBirth = DateTime.Now.AddYears(-30); // Default to 30 years old
        //            showAddFamilyForm = true;
        //            isEditingFamily = false;
        //        }

        //        // Helper method to calculate age
        //        private int CalculateAge(DateTime birthDate)
        //        {
        //            var today = DateTime.Today;
        //            var age = today.Year - birthDate.Year;
        //            if (birthDate.Date > today.AddYears(-age)) age--;
        //            return age;
        //        }

        //        // Update CanAddFamilyMember to include IdentificationNumber
        //        private bool CanAddFamilyMember =>
        //            !string.IsNullOrWhiteSpace(newFamilyMember.Name) &&
        //            newFamilyMember.DateOfBirth != default &&
        //            !string.IsNullOrWhiteSpace(newFamilyMember.Relationship) &&
        //            !string.IsNullOrWhiteSpace(newFamilyMember.IdentificationNumber) &&
        //            !string.IsNullOrWhiteSpace(newFamilyMember.Gender);

        //        private void SaveFamilyMember()
        //        {
        //            if (!CanAddFamilyMember)
        //            {
        //                ShowNotification("Please fill in all required fields: Name, Date of Birth, Relationship, Identification Number, and Gender");
        //                return;
        //            }

        //            if (selectedMember == null) return;

        //            if (isEditingFamily)
        //            {
        //                // Update existing family member
        //                var existing = selectedMember.FamilyMembers.FirstOrDefault(f => f.Id == newFamilyMember.Id);
        //                if (existing != null)
        //                {
        //                    existing.Name = newFamilyMember.Name.Trim();
        //                    existing.DateOfBirth = newFamilyMember.DateOfBirth;
        //                    existing.Relationship = newFamilyMember.Relationship;
        //                    existing.IdentificationNumber = newFamilyMember.IdentificationNumber.Trim();
        //                    existing.Gender = newFamilyMember.Gender;
        //                    existing.ContactNumber = newFamilyMember.ContactNumber?.Trim();
        //                    existing.Email = newFamilyMember.Email?.Trim();
        //                    existing.Notes = newFamilyMember.Notes?.Trim();

        //                    ShowNotification($"Updated {existing.Name}'s information");
        //                }
        //            }
        //            else
        //            {
        //                // Add new family member
        //                var newId = selectedMember.FamilyMembers.Any()
        //                    ? selectedMember.FamilyMembers.Max(f => f.Id) + 1
        //                    : 1;

        //                var familyMember = new FamilyMember
        //                {
        //                    Id = newId,
        //                    Name = newFamilyMember.Name.Trim(),
        //                    DateOfBirth = newFamilyMember.DateOfBirth,
        //                    Relationship = newFamilyMember.Relationship,
        //                    IdentificationNumber = newFamilyMember.IdentificationNumber.Trim(),
        //                    Gender = newFamilyMember.Gender,
        //                    ContactNumber = newFamilyMember.ContactNumber?.Trim(),
        //                    Email = newFamilyMember.Email?.Trim(),
        //                    Notes = newFamilyMember.Notes?.Trim(),
        //                    CreatedDate = DateTime.Now
        //                };

        //                selectedMember.FamilyMembers.Add(familyMember);
        //                ShowNotification($"Added {familyMember.Name} as {familyMember.Relationship}");
        //            }

        //            // Reset form
        //            newFamilyMember = new FamilyMember();
        //            showAddFamilyForm = false;
        //            isEditingFamily = false;
        //            StateHasChanged();
        //        }

        //        private void EditFamilyMember(FamilyMember family)
        //        {
        //            newFamilyMember = new FamilyMember
        //            {
        //                Id = family.Id,
        //                Name = family.Name,
        //                DateOfBirth = family.DateOfBirth,
        //                Relationship = family.Relationship,
        //                IdentificationNumber = family.IdentificationNumber,
        //                Gender = family.Gender,
        //                ContactNumber = family.ContactNumber,
        //                Email = family.Email,
        //                Notes = family.Notes,
        //                CreatedDate = family.CreatedDate
        //            };

        //            showAddFamilyForm = true;
        //            isEditingFamily = true;
        //        }

        //        private async void DeleteFamilyMember(FamilyMember family)
        //        {
        //            if (selectedMember == null) return;

        //            var confirm = await JS.InvokeAsync<bool>(
        //                "confirm",
        //                $"Delete family member {family.Name} ({family.Relationship})?"
        //            );

        //            if (confirm)
        //            {
        //                selectedMember.FamilyMembers.Remove(family);
        //                ShowNotification($"Removed {family.Name} from family members");
        //                StateHasChanged();
        //            }
        //        }

        private bool showConsentFormModal = false;
        private ConsentForm currentConsentForm = new();
        private bool consentFormReadAndAccepted = false;

        // Add this class definition near your other classes (Member, FamilyMember, etc.)
        public class ConsentForm
        {
            public int Id { get; set; }
            public string Title { get; set; } = "";
            public string ConsultantName { get; set; } = "";
            public int? ConsultantId { get; set; }
            public string TermsAndConditions { get; set; } = "";
            public string SignatureDataUrl { get; set; } = "";
            public string SignedByName { get; set; } = "";
            public DateTime SignedDate { get; set; }
            public DateTime CreatedDate { get; set; } = DateTime.Now;
        }

        //        // Add these for staff search modal
        //        private bool showStaffSearchModal = false;
        //        private string staffSearchQuery = "";
        //        private List<StaffMember> staffMembers = new();
        //        private bool isLoadingStaff = false;

        //        // Staff member class
        //        public class StaffMember
        //        {
        //            public int Id { get; set; }
        //            public string Name { get; set; } = "";
        //            public string Position { get; set; } = "";
        //            public string Department { get; set; } = "";
        //            public string Phone { get; set; } = "";
        //            public string Email { get; set; } = "";
        //        }

        //        private string GetDefaultTermsAndConditions()
        //        {
        //            return @"I hereby give my consent for the proposed treatment/procedure as explained by the consultant.

        //        1. I understand the nature and purpose of the procedure, including potential risks and benefits.
        //        2. I have been given the opportunity to ask questions, which have been answered to my satisfaction.
        //        3. I understand that results may vary and cannot be guaranteed.
        //        4. I consent to the taking of photographs for medical records and treatment planning.
        //        5. I agree to follow all pre- and post-treatment instructions provided by the clinic.
        //        6. I understand that additional procedures or treatments may be recommended.
        //        7. I consent to the use of local anesthesia if required for the procedure.
        //        8. I acknowledge that I have disclosed all relevant medical history and medications.
        //        9. I understand the costs involved and agree to the payment terms.
        //        10. I have been informed of alternative treatments and their relative benefits/risks.";
        //        }

        //        private async void OpenStaffSearchModal()
        //        {
        //            showStaffSearchModal = true;
        //            staffSearchQuery = "";
        //            isLoadingStaff = true;

        //            staffMembers = new List<StaffMember>
        //            {
        //                new StaffMember { Id = 1, Name = "Dr. Sarah Lim", Position = "Senior Consultant", Department = "Aesthetics", Phone = "(6012) 345-6789", Email = "sarah@clinic.com" },
        //                new StaffMember { Id = 2, Name = "Dr. Alex Tan", Position = "Consultant", Department = "Dermatology", Phone = "(6013) 456-7890", Email = "alex@clinic.com" },
        //                new StaffMember { Id = 3, Name = "Nurse Jane Wong", Position = "Registered Nurse", Department = "Treatment", Phone = "(6014) 567-8901", Email = "jane@clinic.com" },
        //                new StaffMember { Id = 4, Name = "Dr. Raj Kumar", Position = "Medical Director", Department = "Surgery", Phone = "(6015) 678-9012", Email = "raj@clinic.com" },
        //                new StaffMember { Id = 5, Name = "Therapist Lisa Chen", Position = "Beauty Therapist", Department = "Wellness", Phone = "(6016) 789-0123", Email = "lisa@clinic.com" }
        //            };

        //            isLoadingStaff = false;
        //            StateHasChanged();
        //        }

        //        private void SelectStaffMember(StaffMember staff)
        //        {
        //            currentConsentForm.ConsultantName = staff.Name;
        //            currentConsentForm.ConsultantId = staff.Id;
        //            showStaffSearchModal = false;
        //            staffSearchQuery = "";
        //        }

        //        private List<StaffMember> FilteredStaffMembers =>
        //            staffMembers.Where(s => string.IsNullOrEmpty(staffSearchQuery) ||
        //                                   s.Name.Contains(staffSearchQuery, StringComparison.OrdinalIgnoreCase) ||
        //                                   s.Position.Contains(staffSearchQuery, StringComparison.OrdinalIgnoreCase) ||
        //                                   s.Department.Contains(staffSearchQuery, StringComparison.OrdinalIgnoreCase)).ToList();

        //        private async void StartSignatureCapture()
        //        {
        //            // In a real application, you would integrate with a proper signature pad component
        //            // For now, we'll simulate signature capture with a simple input
        //            var signatureName = await JS.InvokeAsync<string>("prompt", "Enter your full name for the signature:", selectedMember?.Name ?? "");

        //            if (!string.IsNullOrWhiteSpace(signatureName))
        //            {
        //                // Create a simple signature simulation (in real app, use a signature pad library)
        //                currentConsentForm.SignedByName = signatureName;
        //                currentConsentForm.SignatureDataUrl = GenerateSimpleSignatureImage(signatureName);
        //                currentConsentForm.SignedDate = DateTime.Now;

        //                ShowNotification("Signature captured successfully");
        //                StateHasChanged();
        //            }
        //        }

        //        private string GenerateSimpleSignatureImage(string name)
        //        {
        //            // This simulates a signature image - in a real app, use a signature pad library
        //            // For now, create a simple SVG with the name
        //            var svgContent = $@"
        //            <svg width='300' height='100' xmlns='http://www.w3.org/2000/svg'>
        //                <rect width='100%' height='100%' fill='white'/>
        //                <text x='50%' y='50%' 
        //                      font-family='Dancing Script, cursive' 
        //                      font-size='24' 
        //                      text-anchor='middle' 
        //                      fill='#333'
        //                      dominant-baseline='middle'>
        //                    {name}
        //                </text>
        //                <line x1='20' y1='70' x2='280' y2='70' 
        //                      stroke='#666' 
        //                      stroke-width='1' 
        //                      stroke-dasharray='5,5'/>
        //            </svg>";

        //            var base64Svg = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(svgContent));
        //            return $"data:image/svg+xml;base64,{base64Svg}";
        //        }

        //        private void ClearSignature()
        //        {
        //            currentConsentForm.SignatureDataUrl = "";
        //            currentConsentForm.SignedByName = "";
        //            currentConsentForm.SignedDate = default;
        //            StateHasChanged();
        //        }

        //        private bool showConsentFormsModal = false;
        //        private bool showConsentFormCreateModal = false;
        //        private string consentFormSearchQuery = "";

        //        private void OpenConsentForm()
        //        {
        //            if (selectedMember == null) return;

        //            // Reset current form
        //            currentConsentForm = new ConsentForm
        //            {
        //                Title = "Treatment Consent Form",
        //                TermsAndConditions = GetDefaultTermsAndConditions(),
        //                SignedByName = selectedMember.Name
        //            };

        //            consentFormReadAndAccepted = false;
        //            showConsentFormsModal = true;
        //            showConsentFormCreateModal = false;
        //            consentFormSearchQuery = "";
        //        }

        //        private void CloseConsentFormsModal()
        //        {
        //            showConsentFormsModal = false;
        //            showConsentFormCreateModal = false;
        //            consentFormSearchQuery = "";
        //        }

        //        private void OpenCreateConsentForm()
        //        {
        //            showConsentFormCreateModal = true;
        //        }

        //        private void CloseCreateConsentForm()
        //        {
        //            showConsentFormCreateModal = false;
        //            currentConsentForm = new ConsentForm
        //            {
        //                Title = "Treatment Consent Form",
        //                TermsAndConditions = GetDefaultTermsAndConditions(),
        //                SignedByName = selectedMember?.Name ?? ""
        //            };
        //            consentFormReadAndAccepted = false;
        //        }

        //        private void ViewConsentFormDetail(ConsentForm form)
        //        {
        //            currentConsentForm = new ConsentForm
        //            {
        //                Id = form.Id,
        //                Title = form.Title,
        //                ConsultantName = form.ConsultantName,
        //                ConsultantId = form.ConsultantId,
        //                TermsAndConditions = form.TermsAndConditions,
        //                SignatureDataUrl = form.SignatureDataUrl,
        //                SignedByName = form.SignedByName,
        //                SignedDate = form.SignedDate,
        //                CreatedDate = form.CreatedDate
        //            };

        //            showConsentFormsModal = false;
        //            showConsentFormModal = true;
        //        }

        //        private List<ConsentForm> FilteredConsentForms =>
        //            selectedMember?.ConsentForms
        //                .Where(cf => string.IsNullOrEmpty(consentFormSearchQuery) ||
        //                             cf.Title.Contains(consentFormSearchQuery, StringComparison.OrdinalIgnoreCase) ||
        //                             cf.ConsultantName.Contains(consentFormSearchQuery, StringComparison.OrdinalIgnoreCase) ||
        //                             cf.TermsAndConditions.Contains(consentFormSearchQuery, StringComparison.OrdinalIgnoreCase))
        //                .OrderByDescending(cf => cf.CreatedDate)
        //                .ToList() ?? new List<ConsentForm>();

        //        private void ShowConsentFormsModal()
        //        {
        //            showConsentFormsModal = true;
        //            showConsentFormModal = false;
        //            showConsentFormCreateModal = false;
        //        }

        //        private void EditConsentForm()
        //        {
        //            showConsentFormModal = false;
        //            showConsentFormCreateModal = true;
        //        }

        //        private void PrintConsentForm()
        //        {
        //            // Implement print functionality
        //            ShowNotification("Print functionality coming soon...");
        //        }

        //        private void SaveConsentForm()
        //        {
        //            if (selectedMember == null) return;

        //            if (string.IsNullOrWhiteSpace(currentConsentForm.Title))
        //            {
        //                ShowNotification("Please enter a title for the consent form");
        //                return;
        //            }

        //            if (string.IsNullOrWhiteSpace(currentConsentForm.ConsultantName))
        //            {
        //                ShowNotification("Please select a consultant");
        //                return;
        //            }

        //            if (!consentFormReadAndAccepted)
        //            {
        //                ShowNotification("Please acknowledge that you have read and accepted the terms and conditions");
        //                return;
        //            }

        //            if (string.IsNullOrWhiteSpace(currentConsentForm.SignatureDataUrl))
        //            {
        //                ShowNotification("Please provide a signature");
        //                return;
        //            }

        //            // Add the consent form to the member
        //            var newId = selectedMember.ConsentForms.Any()
        //                ? selectedMember.ConsentForms.Max(c => c.Id) + 1
        //                : 1;

        //            currentConsentForm.Id = newId;
        //            currentConsentForm.SignedDate = DateTime.Now;
        //            currentConsentForm.CreatedDate = DateTime.Now;

        //            selectedMember.ConsentForms.Add(currentConsentForm);

        //            ShowNotification($"Consent form '{currentConsentForm.Title}' saved successfully");

        //            // Close create modal and go back to list
        //            showConsentFormCreateModal = false;
        //            showConsentFormsModal = true;

        //            // Reset form
        //            currentConsentForm = new ConsentForm
        //            {
        //                Title = "Treatment Consent Form",
        //                TermsAndConditions = GetDefaultTermsAndConditions(),
        //                SignedByName = selectedMember.Name
        //            };
        //            consentFormReadAndAccepted = false;
        //        }

        //        private bool showDeleteConsentFormConfirmation = false;

        //        private void DeleteConsentFormConfirmation()
        //        {
        //            showDeleteConsentFormConfirmation = true;
        //        }

        //        private void CloseDeleteConsentFormConfirmation()
        //        {
        //            showDeleteConsentFormConfirmation = false;
        //        }

        //        private void DeleteConsentForm()
        //        {
        //            if (selectedMember == null || currentConsentForm == null) return;

        //            // Remove the consent form from the member's list
        //            selectedMember.ConsentForms.RemoveAll(cf => cf.Id == currentConsentForm.Id);

        //            ShowNotification($"Consent form '{currentConsentForm.Title}' has been deleted.");

        //            // Close both modals
        //            showDeleteConsentFormConfirmation = false;
        //            showConsentFormModal = false;

        //            // Show the consent forms list
        //            ShowConsentFormsModal();
        //        }

        private bool showMessageModal = false;
        private string activeMessageTab = "Message"; // "Message" or "Notification"
        private string newMessageText = "";
        private List<SentMessage> messageHistory = new(); // for demo - real app would load from backend

        private class SentMessage
        {
            public string Content { get; set; } = "";
            public DateTime SentAt { get; set; } = DateTime.Now;
            public bool IsOutgoing { get; set; } = true; // true = from staff, false = from customer
        }

        private void OpenMessage()
        {
            if (selectedMemberForUI == null) return;

            // Optional: load real message history from backend here
            // For demo we can pre-fill something
            if (!messageHistory.Any())
            {
                messageHistory.Add(new SentMessage
                {
                    Content = "Hi! Your package is ready for collection 😊",
                    SentAt = DateTime.Now.AddHours(-2),
                    IsOutgoing = true
                });
            }

            newMessageText = "";
            activeMessageTab = "Message";
            showMessageModal = true;
        }

        private void SendMessage()
        {
            if (string.IsNullOrWhiteSpace(newMessageText) || selectedMemberForUI == null) return;

            messageHistory.Add(new SentMessage
            {
                Content = newMessageText.Trim(),
                SentAt = DateTime.Now,
                IsOutgoing = true
            });

            newMessageText = "";
            // In real app → call API to send via WhatsApp / SMS / in-app push
            ShowNotification("Message sent!");
            // Optional: scroll to bottom → can be done with JS interop
        }

        //// Add these classes near your other class definitions
        //public class Referral
        //{
        //    public int Id { get; set; }
        //    public int ReferrerMemberId { get; set; } // The member who referred
        //    public int ReferredMemberId { get; set; } // The member who was referred (current selected member)
        //    public string ReferrerName { get; set; } = "";
        //    public string ReferralProgram { get; set; } = "Default Referral";
        //    public DateTime ReferralDate { get; set; } = DateTime.Now;
        //    public decimal? RewardAmount { get; set; }
        //    public string Status { get; set; } = "Active"; // Active, Completed, Expired
        //    public string? Notes { get; set; }
        //}

        //        public class ReferralProgram
        //        {
        //            public int Id { get; set; }
        //            public string Name { get; set; } = "";
        //            public string Description { get; set; } = "";
        //            public decimal ReferrerReward { get; set; }
        //            public decimal ReferredReward { get; set; }
        //            public bool IsActive { get; set; } = true;
        //            public bool IsDefault { get; set; } = false;
        //            public DateTime? ValidFrom { get; set; }
        //            public DateTime? ValidTo { get; set; }
        //        }

        //        private bool showReferralModal = false;
        //        private bool showReferralSelectModal = false;
        //        private string referralSearchTerm = "";
        //        private Member? selectedReferrer;
        //        private List<ReferralProgram> referralPrograms = new();
        //        private string selectedReferralProgram = "Default Referral";
        //        private List<Referral> memberReferrals = new();
        //        private bool showReferralEditModal = false;
        //        private Referral? currentReferral = null;
        //        private string referralNotes = "";

        //        protected override void OnInitialized()
        //        {
        //            // Initialize referral programs
        //            referralPrograms = new List<ReferralProgram>
        //        {
        //            new ReferralProgram
        //            {
        //                Id = 1,
        //                Name = "Default Referral",
        //                Description = "Standard referral program",
        //                ReferrerReward = 50.00m,
        //                ReferredReward = 25.00m,
        //                IsActive = true,
        //                IsDefault = true
        //            },
        //            new ReferralProgram
        //            {
        //                Id = 2,
        //                Name = "Premium Referral",
        //                Description = "Premium rewards for VIP members",
        //                ReferrerReward = 100.00m,
        //                ReferredReward = 50.00m,
        //                IsActive = true
        //            },
        //            new ReferralProgram
        //            {
        //                Id = 3,
        //                Name = "Seasonal Promotion",
        //                Description = "Limited time referral bonus",
        //                ReferrerReward = 75.00m,
        //                ReferredReward = 40.00m,
        //                IsActive = true,
        //                ValidFrom = DateTime.Now.AddDays(-30),
        //                ValidTo = DateTime.Now.AddDays(30)
        //            }
        //        };
        //            InitializeSampleBroadcasts();
        //            InitializeSampleTemplates();
        //            InitializeSampleAppointments();
        //            base.OnInitialized();
        //        }

        //        private void OpenReferral()
        //        {
        //            if (selectedMember == null) return;

        //            // Load member's referrals
        //            memberReferrals = selectedMember.Referrals;
        //            showReferralModal = true;

        //            // Reset selection
        //            selectedReferrer = null;
        //            selectedReferralProgram = "Default Referral";
        //            referralNotes = "";
        //        }

        //        private void OpenReferrerSearch()
        //        {
        //            showReferralSelectModal = true;
        //            referralSearchTerm = "";
        //        }

        //        private void SelectReferrer(Member member)
        //        {
        //            selectedReferrer = member;
        //            showReferralSelectModal = false;
        //        }

        //        private void ClearReferrer()
        //        {
        //            selectedReferrer = null;
        //        }

        //        private void SaveReferral()
        //        {
        //            if (selectedMember == null || selectedReferrer == null) return;

        //            // Check if this referral already exists
        //            var existingReferral = memberReferrals.FirstOrDefault(r =>
        //                r.ReferrerMemberId == selectedReferrer.Id);

        //            if (existingReferral != null)
        //            {
        //                ShowNotification("This member has already been set as a referrer.");
        //                return;
        //            }

        //            // Get selected program
        //            var program = referralPrograms.FirstOrDefault(p => p.Name == selectedReferralProgram);

        //            var newReferral = new Referral
        //            {
        //                Id = memberReferrals.Any() ? memberReferrals.Max(r => r.Id) + 1 : 1,
        //                ReferrerMemberId = selectedReferrer.Id,
        //                ReferredMemberId = selectedMember.Id,
        //                ReferrerName = selectedReferrer.Name,
        //                ReferralProgram = selectedReferralProgram,
        //                ReferralDate = DateTime.Now,
        //                RewardAmount = program?.ReferrerReward,
        //                Status = "Active",
        //                Notes = referralNotes
        //            };

        //            memberReferrals.Add(newReferral);
        //            selectedMember.Referrals = memberReferrals;

        //            ShowNotification($"Added {selectedReferrer.Name} as referrer");

        //            // Reset form
        //            selectedReferrer = null;
        //            selectedReferralProgram = "Default Referral";
        //            referralNotes = "";
        //        }

        //        private void EditReferral(Referral referral)
        //        {
        //            currentReferral = referral;
        //            referralNotes = referral.Notes ?? "";
        //            showReferralEditModal = true;
        //        }

        //        private void UpdateReferral()
        //        {
        //            if (currentReferral == null) return;

        //            currentReferral.Notes = referralNotes;

        //            // Update in the list
        //            var index = memberReferrals.FindIndex(r => r.Id == currentReferral.Id);
        //            if (index >= 0)
        //            {
        //                memberReferrals[index] = currentReferral;
        //            }

        //            ShowNotification("Referral updated successfully");
        //            showReferralEditModal = false;
        //            currentReferral = null;
        //            referralNotes = "";
        //        }

        //        private async void DeleteReferral(Referral referral)
        //        {
        //            if (selectedMember == null) return;

        //            var confirm = await JS.InvokeAsync<bool>(
        //                "confirm",
        //                $"Delete referral from {referral.ReferrerName}?"
        //            );

        //            if (confirm)
        //            {
        //                memberReferrals.Remove(referral);
        //                selectedMember.Referrals = memberReferrals;
        //                ShowNotification("Referral removed");
        //                StateHasChanged();
        //            }
        //        }

        //        private void CloseReferralModal()
        //        {
        //            showReferralModal = false;
        //            selectedReferrer = null;
        //            showReferralSelectModal = false;
        //        }

        //        private void CloseReferralSelectModal()
        //        {
        //            showReferralSelectModal = false;
        //        }

        //        private void CloseReferralEditModal()
        //        {
        //            showReferralEditModal = false;
        //            currentReferral = null;
        //            referralNotes = "";
        //        }

        //        // Filter members for referral search (exclude current member)
        //        private IEnumerable<Member> FilteredReferralMembers => members
        //            .Where(m => m.Id != selectedMember?.Id) // Exclude current member
        //            .Where(m => string.IsNullOrEmpty(referralSearchTerm) ||
        //                        m.Name.Contains(referralSearchTerm, StringComparison.OrdinalIgnoreCase) ||
        //                        m.Phone.Contains(referralSearchTerm) ||
        //                        m.MemberId.Contains(referralSearchTerm))
        //            .OrderBy(m => m.Name);

        private bool showBroadcastModal = false;
        private string activeBroadcastTab = "<30 Day"; // "<30 Day" or "All"
        private string broadcastSearchQuery = "";

        public class BroadcastMessage
        {
            public int Id { get; set; }
            public string Title { get; set; } = "";
            public string Content { get; set; } = "";
            public int RecipientCount { get; set; }
            public DateTime SentDate { get; set; }
            public string Status { get; set; } = "Sent"; // Sent, Draft, Failed, Scheduled
            public string CreatedBy { get; set; } = "";
            public List<string> Tags { get; set; } = new();
            public string Type { get; set; } = ""; // SMS, WhatsApp, Push, Email
            public List<BroadcastRecipient> Recipients { get; set; } = new();
        }

        public class BroadcastRecipient
        {
            public int MemberId { get; set; }
            public string Name { get; set; } = "";
            public string Phone { get; set; } = "";
            public string Status { get; set; } = "Delivered"; // Delivered, Read, Failed
            public DateTime? ReadAt { get; set; }
        }

        private List<BroadcastMessage> broadcastMessages = new();

        private void InitializeSampleBroadcasts()
        {
            broadcastMessages = new List<BroadcastMessage>
                    {
                        new BroadcastMessage
                        {
                            Id = 1,
                            Title = "Special Promotion - 20% Off",
                            Content = "Dear valued member, enjoy 20% off all facial treatments this weekend!",
                            RecipientCount = 45,
                            SentDate = DateTime.Now.AddDays(-5),
                            Status = "Sent",
                            CreatedBy = "Admin",
                            Tags = new List<string> { "Promotion", "Facial" },
                            Type = "SMS"
                        },
                        new BroadcastMessage
                        {
                            Id = 2,
                            Title = "Appointment Reminder",
                            Content = "Reminder: Your appointment is tomorrow at 2:30 PM",
                            RecipientCount = 8,
                            SentDate = DateTime.Now.AddDays(-15),
                            Status = "Sent",
                            CreatedBy = "Reception",
                            Tags = new List<string> { "Reminder", "Appointment" },
                            Type = "WhatsApp"
                        }
                    };
        }

        // Filter broadcasts based on active tab and search query
        private IEnumerable<BroadcastMessage> FilteredBroadcasts
        {
            get
            {
                var filtered = broadcastMessages.AsEnumerable();

                // Filter by tab
                if (activeBroadcastTab == "<30 Day")
                {
                    filtered = filtered.Where(b => b.SentDate >= DateTime.Now.AddDays(-30));
                }

                // Filter by search query
                if (!string.IsNullOrEmpty(broadcastSearchQuery))
                {
                    filtered = filtered.Where(b =>
                        b.Title.Contains(broadcastSearchQuery, StringComparison.OrdinalIgnoreCase) ||
                        b.Content.Contains(broadcastSearchQuery, StringComparison.OrdinalIgnoreCase) ||
                        b.Tags.Any(t => t.Contains(broadcastSearchQuery, StringComparison.OrdinalIgnoreCase)));
                }

                return filtered.OrderByDescending(b => b.SentDate);
            }
        }

        // Update the OpenBroadcast method
        private void OpenBroadcast()
        {
            showBroadcastModal = true;
            activeBroadcastTab = "<30 Day";
            broadcastSearchQuery = "";
        }

        private void CreateNewBroadcast()
        {
            // You can implement this to open a new broadcast creation modal
            ShowNotification("Create new broadcast feature coming soon...");
            // For now, just close and show a notification
            showBroadcastModal = false;
        }

        private void ViewBroadcastDetails(BroadcastMessage broadcast)
        {
            // You can implement this to show broadcast details
            ShowNotification($"Viewing details for broadcast: {broadcast.Title}");
        }

        private void ResendBroadcast(BroadcastMessage broadcast)
        {
            var confirm = JS.InvokeAsync<bool>(
                "confirm",
                $"Resend broadcast '{broadcast.Title}' to {broadcast.RecipientCount} members?"
            );

            // In a real app, you would await this and handle the result
            ShowNotification($"Resending broadcast '{broadcast.Title}'...");
        }

        //        public class MemberRemark
        //        {
        //            public int Id { get; set; }
        //            public string Category { get; set; } = "";     // "Appointment", "Sales", "Remarks"
        //            public string Content { get; set; } = "";
        //            public DateTime CreatedAt { get; set; } = DateTime.Now;
        //            public string CreatedBy { get; set; } = "Staff";   // or actual staff name/ID
        //            public string? RelatedId { get; set; }             // e.g. Appointment ID, Sale/Invoice ID
        //            public bool IsAutoGenerated { get; set; } = false;
        //        }

        //        private bool showRemarksModal = false;
        //        private string activeRemarksTab = "All";

        //        // Helper computed properties
        //        private IEnumerable<MemberRemark> FilteredRemarks =>
        //            selectedMember?.Remarks
        //                .Where(r => activeRemarksTab == "All" || r.Category == activeRemarksTab)
        //                .OrderByDescending(r => r.CreatedAt)
        //            ?? Enumerable.Empty<MemberRemark>();

        //        private void OpenRemarks()
        //        {
        //            if (selectedMember == null) return;
        //            activeRemarksTab = "All";
        //            showRemarksModal = true;
        //        }

        //        private void CloseRemarksModal()
        //        {
        //            showRemarksModal = false;
        //        }

        //        private string newRemarkContent = "";

        //        private async void AddManualRemark()
        //        {
        //            if (string.IsNullOrWhiteSpace(newRemarkContent) || selectedMember == null)
        //                return;

        //            var remark = new MemberRemark
        //            {
        //                Id = selectedMember.Remarks.Any()
        //                    ? selectedMember.Remarks.Max(r => r.Id) + 1
        //                    : 1,
        //                Category = "Remarks",
        //                Content = newRemarkContent.Trim(),
        //                CreatedAt = DateTime.Now,
        //                CreatedBy = "Staff",           // ← in real app: current user name
        //                IsAutoGenerated = false
        //            };

        //            selectedMember.Remarks.Add(remark);
        //            newRemarkContent = "";

        //            // Optional: small toast / notification
        //            await JS.InvokeVoidAsync("alert", "Remark added successfully.");
        //            StateHasChanged();
        //        }

        //        private void OnSaleCompleted(decimal amount, string invoiceNumber)
        //        {
        //            if (selectedMember == null) return;

        //            var remark = new MemberRemark
        //            {
        //                Id = selectedMember.Remarks.Any()
        //                    ? selectedMember.Remarks.Max(r => r.Id) + 1
        //                    : 1,
        //                Category = "Sales",
        //                Content = $"Completed sale – RM {amount:F2} (#{invoiceNumber})",
        //                CreatedAt = DateTime.Now,
        //                CreatedBy = "System",
        //                RelatedId = invoiceNumber,
        //                IsAutoGenerated = true
        //            };

        //            selectedMember.Remarks.Add(remark);

        //            // Optional: save to backend, show toast, etc.
        //        }

        private bool showBreakdownModal = false;

        private async Task OpenBreakdown()
        {
            if (selectedMember == null) return;

            showBreakdownModal = true;
            isLoading = true;
            StateHasChanged();

            try
            {
                totalApptCount = 0;
                firstApptDate = null;
                lastApptDate = null;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error fetching breakdown stats: {ex.Message}");
            }
            finally
            {
                isLoading = false;
                StateHasChanged();
            }
        }

        private async void ExportBreakdown()
        {
            // In a real application, this would generate and download a PDF/CSV report
            ShowNotification("Exporting member breakdown...");

            ShowNotification("Breakdown exported successfully!");
            showBreakdownModal = false;
        }

        //        private bool showDocumentModal = false;
        //        private List<DocumentFile> pendingDocuments = new();

        //        public class DocumentFile
        //        {
        //            public string DataUrl { get; set; } = "";
        //            public string FileName { get; set; } = "";
        //            public string FileType { get; set; } = ""; // "image", "pdf", "word", "excel", "other"
        //            public long FileSize { get; set; }
        //            public DateTime UploadedDate { get; set; } = DateTime.Now;
        //            public string? Description { get; set; }
        //            public string Category { get; set; } = "General";
        //            public bool Selected { get; set; } = true;
        //        }

        //        private void OpenDocument()
        //        {
        //            if (selectedMember == null) return;

        //            pendingDocuments.Clear();
        //            showDocumentModal = true;
        //        }

        //        private void CloseDocumentModal()
        //        {
        //            pendingDocuments.Clear();
        //            showDocumentModal = false;
        //        }

        //        private string documentSearchQuery = "";
        //        private string selectedDocumentCategory = "";
        //        private string newDocumentCategory = "";
        //        private string newDocumentDescription = "";
        //        private ElementReference documentCategoryInput;

        //        private IEnumerable<DocumentFile> FilteredDocuments =>
        //        selectedMember?.DocumentFiles
        //            .Where(d => string.IsNullOrEmpty(documentSearchQuery) ||
        //                       d.FileName.Contains(documentSearchQuery, StringComparison.OrdinalIgnoreCase) ||
        //                       d.Description?.Contains(documentSearchQuery, StringComparison.OrdinalIgnoreCase) == true)
        //            .Where(d => string.IsNullOrEmpty(selectedDocumentCategory) ||
        //                       d.Category == selectedDocumentCategory)
        //            .ToList() ?? new List<DocumentFile>();

        //        private async void HandleDocumentFiles(InputFileChangeEventArgs e)
        //        {
        //            const long maxSizeBytes = 10 * 1024 * 1024; // 10 MB

        //            foreach (var file in e.GetMultipleFiles())
        //            {
        //                if (file.Size > maxSizeBytes)
        //                {
        //                    await JS.InvokeVoidAsync("alert", $"File {file.Name} exceeds 10MB limit");
        //                    continue;
        //                }

        //                try
        //                {
        //                    using var stream = file.OpenReadStream(maxSizeBytes);
        //                    using var ms = new MemoryStream();
        //                    await stream.CopyToAsync(ms);

        //                    var base64 = Convert.ToBase64String(ms.ToArray());
        //                    var contentType = file.ContentType;
        //                    var dataUrl = $"data:{contentType};base64,{base64}";
        //                    var fileType = GetFileTypeFromContentType(contentType, file.Name);

        //                    pendingDocuments.Add(new DocumentFile
        //                    {
        //                        DataUrl = dataUrl,
        //                        FileName = file.Name,
        //                        FileType = fileType,
        //                        FileSize = file.Size,
        //                        Category = string.IsNullOrWhiteSpace(newDocumentCategory) ? "General" : newDocumentCategory,
        //                        Description = newDocumentDescription
        //                    });
        //                }
        //                catch (Exception ex)
        //                {
        //                    await JS.InvokeVoidAsync("alert", $"Error reading {file.Name}: {ex.Message}");
        //                }
        //            }

        //            StateHasChanged();
        //        }

        //        private string GetFileTypeFromContentType(string contentType, string fileName)
        //        {
        //            if (contentType.StartsWith("image/")) return "image";
        //            if (contentType == "application/pdf") return "pdf";
        //            if (contentType.Contains("word") || fileName.EndsWith(".doc") || fileName.EndsWith(".docx")) return "word";
        //            if (contentType.Contains("excel") || fileName.EndsWith(".xls") || fileName.EndsWith(".xlsx")) return "excel";
        //            if (contentType.Contains("text") || fileName.EndsWith(".txt")) return "text";
        //            return "other";
        //        }

        //        private string GetDocumentIcon(string fileType)
        //        {
        //            return fileType.ToLower() switch
        //            {
        //                "image" => "🖼️",
        //                "pdf" => "📕",
        //                "word" => "📝",
        //                "excel" => "📊",
        //                "text" => "📄",
        //                _ => "📎"
        //            };
        //        }

        //        private string FormatFileSize(long bytes)
        //        {
        //            if (bytes < 1024) return $"{bytes} B";
        //            if (bytes < 1024 * 1024) return $"{bytes / 1024.0:F1} KB";
        //            return $"{bytes / (1024.0 * 1024.0):F1} MB";
        //        }

        //        private void RemovePendingDocument(DocumentFile doc)
        //        {
        //            pendingDocuments.Remove(doc);
        //            StateHasChanged();
        //        }

        //        private void SaveDocuments()
        //        {
        //            if (selectedMember == null || !pendingDocuments.Any()) return;

        //            foreach (var doc in pendingDocuments)
        //            {
        //                selectedMember.DocumentFiles.Add(doc);
        //            }

        //            ShowNotification($"Uploaded {pendingDocuments.Count} document(s) successfully");
        //            pendingDocuments.Clear();
        //            newDocumentCategory = "";
        //            newDocumentDescription = "";
        //            StateHasChanged();
        //        }

        //        private async void ViewDocument(DocumentFile doc)
        //        {
        //            if (doc.FileType == "image")
        //            {
        //                // Show image in lightbox
        //                await JS.InvokeVoidAsync("window.open", doc.DataUrl, "_blank");
        //            }
        //            else
        //            {
        //                // For other files, download them
        //                DownloadDocument(doc);
        //            }
        //        }

        //        private async void DownloadDocument(DocumentFile doc)
        //        {
        //            try
        //            {
        //                // Convert data URL to blob and trigger download
        //                await JS.InvokeVoidAsync("downloadFile", doc.DataUrl, doc.FileName);
        //            }
        //            catch
        //            {
        //                ShowNotification($"Downloading {doc.FileName}...");
        //            }
        //        }

        //        private void OpenBeautyRecord()
        //        {
        //            if (selectedMember == null) return;

        //            // Load beauty records from selected member
        //            showBeautyRecordModal = true;
        //            showAddBeautyRecordModal = false;
        //        }

        //        private bool showBeautyRecordModal = false;
        //        private bool showAddBeautyRecordModal = false;
        //        private bool showBeautyRecordPhotoUploadModal = false;
        //        private bool showBeautyRecordStaffSearchModal = false;
        //        private string beautyRecordStaffSearchQuery = "";
        //        private BeautyRecord currentBeautyRecord = new();
        //        private List<BeautyRecordPhoto> pendingBeautyRecordPhotos = new();
        //        private string newBeautyRecordTag = "";
        //        private List<StaffMember> beautyRecordStaffMembers = new();
        //        private bool isLoadingBeautyRecordStaff = false;

        //        public class BeautyRecord
        //        {
        //            public int Id { get; set; }
        //            public string Title { get; set; } = "";
        //            public DateTime RecordDate { get; set; } = DateTime.Now;
        //            public int? StaffId { get; set; }
        //            public string StaffName { get; set; } = "";
        //            public List<string> Tags { get; set; } = new();
        //            public List<BeautyRecordPhoto> Photos { get; set; } = new();
        //            public string OverallRemarks { get; set; } = "";
        //            public DateTime CreatedDate { get; set; } = DateTime.Now;
        //            public DateTime? UpdatedDate { get; set; }
        //        }

        //        public class BeautyRecordPhoto
        //        {
        //            public int Id { get; set; }
        //            public string DataUrl { get; set; } = "";
        //            public string FileName { get; set; } = "";
        //            public string? Description { get; set; }
        //            public DateTime UploadedDate { get; set; } = DateTime.Now;
        //            public bool IsSelected { get; set; } = true;
        //        }

        //        private void OpenAddBeautyRecordModal()
        //        {
        //            currentBeautyRecord = new BeautyRecord
        //            {
        //                Title = "",
        //                RecordDate = DateTime.Now,
        //                StaffName = "",
        //                StaffId = null,
        //                Tags = new List<string>(),
        //                Photos = new List<BeautyRecordPhoto>(),
        //                OverallRemarks = ""
        //            };
        //            pendingBeautyRecordPhotos.Clear();
        //            newBeautyRecordTag = "";
        //            showAddBeautyRecordModal = true;
        //            showBeautyRecordModal = false;
        //        }

        //        private void CloseAddBeautyRecordModal()
        //        {
        //            showAddBeautyRecordModal = false;
        //            showBeautyRecordModal = true;
        //        }

        //        private void OpenBeautyRecordStaffSearchModal()
        //        {
        //            showBeautyRecordStaffSearchModal = true;
        //            beautyRecordStaffSearchQuery = "";
        //            isLoadingBeautyRecordStaff = true;

        //            // Load staff members (same list as consent form staff)
        //            beautyRecordStaffMembers = new List<StaffMember>
        //    {
        //        new StaffMember { Id = 1, Name = "Dr. Sarah Lim", Position = "Senior Consultant", Department = "Aesthetics", Phone = "(6012) 345-6789", Email = "sarah@clinic.com" },
        //        new StaffMember { Id = 2, Name = "Dr. Alex Tan", Position = "Consultant", Department = "Dermatology", Phone = "(6013) 456-7890", Email = "alex@clinic.com" },
        //        new StaffMember { Id = 3, Name = "Nurse Jane Wong", Position = "Registered Nurse", Department = "Treatment", Phone = "(6014) 567-8901", Email = "jane@clinic.com" },
        //        new StaffMember { Id = 4, Name = "Dr. Raj Kumar", Position = "Medical Director", Department = "Surgery", Phone = "(6015) 678-9012", Email = "raj@clinic.com" },
        //        new StaffMember { Id = 5, Name = "Therapist Lisa Chen", Position = "Beauty Therapist", Department = "Wellness", Phone = "(6016) 789-0123", Email = "lisa@clinic.com" }
        //    };

        //            isLoadingBeautyRecordStaff = false;
        //            StateHasChanged();
        //        }

        //        private void CloseBeautyRecordStaffSearchModal()
        //        {
        //            showBeautyRecordStaffSearchModal = false;
        //            beautyRecordStaffSearchQuery = "";
        //        }

        //        private void SelectBeautyRecordStaff(StaffMember staff)
        //        {
        //            currentBeautyRecord.StaffName = staff.Name;
        //            currentBeautyRecord.StaffId = staff.Id;
        //            showBeautyRecordStaffSearchModal = false;
        //            beautyRecordStaffSearchQuery = "";
        //            StateHasChanged();
        //        }

        //        private List<StaffMember> FilteredBeautyRecordStaffMembers =>
        //            beautyRecordStaffMembers.Where(s => string.IsNullOrEmpty(beautyRecordStaffSearchQuery) ||
        //                                           s.Name.Contains(beautyRecordStaffSearchQuery, StringComparison.OrdinalIgnoreCase) ||
        //                                           s.Position.Contains(beautyRecordStaffSearchQuery, StringComparison.OrdinalIgnoreCase) ||
        //                                           s.Department.Contains(beautyRecordStaffSearchQuery, StringComparison.OrdinalIgnoreCase)).ToList();

        //        private void AddBeautyRecordTag()
        //        {
        //            if (string.IsNullOrWhiteSpace(newBeautyRecordTag)) return;

        //            currentBeautyRecord.Tags.Add(newBeautyRecordTag.Trim());
        //            newBeautyRecordTag = "";
        //            StateHasChanged();
        //        }

        //        private void RemoveBeautyRecordTag(string tag)
        //        {
        //            currentBeautyRecord.Tags.Remove(tag);
        //            StateHasChanged();
        //        }

        //        private void OpenBeautyRecordPhotoUploadModal()
        //        {
        //            pendingBeautyRecordPhotos.Clear();
        //            showBeautyRecordPhotoUploadModal = true;
        //        }

        //        private void CloseBeautyRecordPhotoUploadModal()
        //        {
        //            pendingBeautyRecordPhotos.Clear();
        //            showBeautyRecordPhotoUploadModal = false;
        //        }

        //        private async void HandleBeautyRecordGalleryFiles(InputFileChangeEventArgs e)
        //        {
        //            var files = e.GetMultipleFiles();
        //            LoadBeautyRecordImageFiles(files);
        //        }

        //        private async void HandleBeautyRecordCameraFile(InputFileChangeEventArgs e)
        //        {
        //            if (e.File != null)
        //            {
        //                LoadBeautyRecordImageFiles(new[] { e.File });
        //            }
        //        }

        //        private async void LoadBeautyRecordImageFiles(IEnumerable<IBrowserFile> files)
        //        {
        //            const long maxSizeBytes = 6 * 1024 * 1024; // 6 MB

        //            foreach (var file in files)
        //            {
        //                if (file.Size > maxSizeBytes)
        //                {
        //                    await JS.InvokeVoidAsync("alert", $"File {file.Name} is too large (max 6MB)");
        //                    continue;
        //                }

        //                if (!file.ContentType.StartsWith("image/"))
        //                {
        //                    await JS.InvokeVoidAsync("alert", $"File {file.Name} is not an image");
        //                    continue;
        //                }

        //                try
        //                {
        //                    using var stream = file.OpenReadStream(maxSizeBytes);
        //                    using var ms = new MemoryStream();
        //                    await stream.CopyToAsync(ms);

        //                    var base64 = Convert.ToBase64String(ms.ToArray());
        //                    var dataUrl = $"data:{file.ContentType};base64,{base64}";

        //                    pendingBeautyRecordPhotos.Add(new BeautyRecordPhoto
        //                    {
        //                        Id = pendingBeautyRecordPhotos.Any() ? pendingBeautyRecordPhotos.Max(p => p.Id) + 1 : 1,
        //                        DataUrl = dataUrl,
        //                        FileName = file.Name,
        //                        UploadedDate = DateTime.Now,
        //                        IsSelected = true
        //                    });
        //                }
        //                catch (Exception ex)
        //                {
        //                    await JS.InvokeVoidAsync("alert", $"Error reading {file.Name}: {ex.Message}");
        //                }
        //            }

        //            StateHasChanged();
        //        }

        //        private void RemovePendingBeautyRecordPhoto(BeautyRecordPhoto photo)
        //        {
        //            pendingBeautyRecordPhotos.Remove(photo);
        //            StateHasChanged();
        //        }

        //        private void SaveBeautyRecordPhotos()
        //        {
        //            var selectedPhotos = pendingBeautyRecordPhotos.Where(p => p.IsSelected).ToList();

        //            foreach (var photo in selectedPhotos)
        //            {
        //                currentBeautyRecord.Photos.Add(photo);
        //            }

        //            ShowNotification($"Added {selectedPhotos.Count} photo(s) to beauty record");
        //            CloseBeautyRecordPhotoUploadModal();
        //        }

        //        private void SaveBeautyRecord()
        //        {
        //            if (selectedMember == null) return;

        //            if (string.IsNullOrWhiteSpace(currentBeautyRecord.Title))
        //            {
        //                ShowNotification("Please enter a title for the beauty record");
        //                return;
        //            }

        //            if (string.IsNullOrWhiteSpace(currentBeautyRecord.StaffName))
        //            {
        //                ShowNotification("Please select a staff member");
        //                return;
        //            }

        //            // Set ID for new record
        //            var newId = selectedMember.BeautyRecords.Any()
        //                ? selectedMember.BeautyRecords.Max(r => r.Id) + 1
        //                : 1;

        //            currentBeautyRecord.Id = newId;
        //            currentBeautyRecord.CreatedDate = DateTime.Now;

        //            selectedMember.BeautyRecords.Add(currentBeautyRecord);

        //            ShowNotification($"Beauty record '{currentBeautyRecord.Title}' saved successfully");

        //            // Close add modal and show list modal
        //            showAddBeautyRecordModal = false;
        //            showBeautyRecordModal = true;
        //        }

        //        private void ViewBeautyRecord(BeautyRecord record)
        //        {
        //            currentBeautyRecord = record;
        //            // You can implement a detail view modal if needed
        //            ShowNotification($"Viewing record: {record.Title}");
        //        }

        //        private async void DeleteBeautyRecord(BeautyRecord record)
        //        {
        //            if (selectedMember == null) return;

        //            var confirm = await JS.InvokeAsync<bool>(
        //                "confirm",
        //                $"Delete beauty record '{record.Title}'? This cannot be undone."
        //            );

        //            if (confirm)
        //            {
        //                selectedMember.BeautyRecords.Remove(record);
        //                ShowNotification("Beauty record deleted");
        //                StateHasChanged();
        //            }
        //        }

        //        private void HandleKeyPress(KeyboardEventArgs e)
        //        {
        //            if (e.Key == "Enter")
        //            {
        //                AddBeautyRecordTag();
        //            }
        //        }

        //        // Add these class definitions near your other class definitions

        //        public class FormTemplate
        //        {
        //            public int Id { get; set; }
        //            public string Name { get; set; } = "";
        //            public string Description { get; set; } = "";
        //            public List<FormSection> Sections { get; set; } = new();
        //            public PhotoRemarks? PhotoRemarks { get; set; }
        //            public string TermsAndConditions { get; set; } = "";
        //            public bool IncludeGallery { get; set; }
        //            public bool IncludeSignature { get; set; }
        //            public DateTime CreatedDate { get; set; } = DateTime.Now;
        //            public DateTime? ModifiedDate { get; set; }
        //            public bool IsActive { get; set; } = true;
        //        }

        //        public class FormSection
        //        {
        //            public int Id { get; set; }
        //            public string Name { get; set; } = "";
        //            public List<FormQuestion> Questions { get; set; } = new();
        //            public int Order { get; set; }
        //        }

        //        public class FormQuestion
        //        {
        //            public int Id { get; set; }
        //            public string Text { get; set; } = "";
        //            public AnswerType AnswerType { get; set; }
        //            public List<string> Options { get; set; } = new();
        //            public bool AllowMultipleChoice { get; set; }
        //            public bool IncludeRemarks { get; set; }
        //            public int Order { get; set; }
        //        }

        //        public enum AnswerType
        //        {
        //            Open,
        //            Choices
        //        }

        //        public class PhotoRemarks
        //        {
        //            public int Id { get; set; }
        //            public string BackdropName { get; set; } = "";
        //            public PhotoLayout Layout { get; set; }
        //            public List<PhotoItem> Photos { get; set; } = new();
        //        }

        //        public enum PhotoLayout
        //        {
        //            Grid,
        //            Custom
        //        }

        //        public class PhotoItem
        //        {
        //            public int Id { get; set; }
        //            public string DataUrl { get; set; } = "";
        //            public string FileName { get; set; } = "";
        //            public List<string> Remarks { get; set; } = new(); // For grid mode - 1-6 remarks
        //            public string? CustomRemark { get; set; } // For custom mode
        //            public int GridPosition { get; set; } // 1-6 for grid cells
        //        }

        //        public class FormResponse
        //        {
        //            public int Id { get; set; }
        //            public int TemplateId { get; set; }
        //            public string TemplateName { get; set; } = "";
        //            public int MemberId { get; set; }
        //            public DateTime SubmittedDate { get; set; } = DateTime.Now;
        //            public Dictionary<string, object> Answers { get; set; } = new(); // QuestionId -> Answer
        //            public List<PhotoResponse> PhotoResponses { get; set; } = new();
        //            public string? SignatureDataUrl { get; set; }
        //            public List<string> GalleryPhotos { get; set; } = new();
        //        }

        //        public class PhotoResponse
        //        {
        //            public int PhotoId { get; set; }
        //            public string BackdropName { get; set; } = "";
        //            public string? CustomRemark { get; set; }
        //            public Dictionary<int, string> GridRemarks { get; set; } = new(); // Grid position -> Remark
        //        }

        //        // Add these properties
        //        private bool showFormsModal = false;
        //        private bool showTemplateModal = false;
        //        private bool showQuestionModal = false;
        //        private string formsSearchQuery = "";
        //        private string activeFormsTab = "All"; // "All", "Recent", "Templates"

        //        // Templates
        //        private List<FormTemplate> formTemplates = new();
        //        private FormTemplate currentTemplate = new();
        //        private FormSection? currentSection = null;
        //        private FormQuestion? currentQuestion = null;
        //        private int? copySectionId = null;

        //        // Question builder
        //        private string questionText = "";
        //        private AnswerType questionAnswerType = AnswerType.Open;
        //        private List<string> questionOptions = new();
        //        private string newOption = "";
        //        private bool questionAllowMultipleChoice = false;
        //        private bool questionIncludeRemarks = false;

        //        // Photo remarks
        //        private bool showPhotoRemarksModal = false;
        //        private PhotoItem currentPhotoItem = new();
        //        private string photoRemarksText = "";
        //        private int selectedGridPosition = 1;

        //        // Form responses for selected member
        //        private List<FormResponse> memberFormResponses = new();

        //        private void InitializeSampleTemplates()
        //        {
        //            formTemplates = new List<FormTemplate>
        //    {
        //        new FormTemplate
        //        {
        //            Id = 1,
        //            Name = "Client Intake Form",
        //            Description = "Standard client information and consent",
        //            Sections = new List<FormSection>
        //            {
        //                new FormSection
        //                {
        //                    Id = 1,
        //                    Name = "Personal Information",
        //                    Order = 1,
        //                    Questions = new List<FormQuestion>
        //                    {
        //                        new FormQuestion { Id = 1, Text = "Full Name", AnswerType = AnswerType.Open, Order = 1 },
        //                        new FormQuestion { Id = 2, Text = "Date of Birth", AnswerType = AnswerType.Open, Order = 2 },
        //                        new FormQuestion { Id = 3, Text = "Contact Number", AnswerType = AnswerType.Open, Order = 3 }
        //                    }
        //                },
        //                new FormSection
        //                {
        //                    Id = 2,
        //                    Name = "Medical History",
        //                    Order = 2,
        //                    Questions = new List<FormQuestion>
        //                    {
        //                        new FormQuestion { Id = 4, Text = "Any allergies?", AnswerType = AnswerType.Open, Order = 1 },
        //                        new FormQuestion { Id = 5, Text = "Current medications", AnswerType = AnswerType.Open, Order = 2 }
        //                    }
        //                }
        //            },
        //            TermsAndConditions = "I confirm that the information provided is accurate...",
        //            IncludeGallery = true,
        //            IncludeSignature = true
        //        }
        //    };
        //        }

        //        private void OpenForm()
        //        {
        //            showFormsModal = true;
        //            activeFormsTab = "All";
        //            formsSearchQuery = "";

        //            // Load member's form responses
        //            if (selectedMember != null)
        //            {
        //                memberFormResponses = selectedMember.FormResponses ?? new List<FormResponse>();
        //            }
        //        }

        //        private void CloseFormsModal()
        //        {
        //            showFormsModal = false;
        //        }

        //        private List<FormTemplate> FilteredTemplates => formTemplates
        //            .Where(t => t.IsActive)
        //            .Where(t => string.IsNullOrEmpty(formsSearchQuery) ||
        //                        t.Name.Contains(formsSearchQuery, StringComparison.OrdinalIgnoreCase) ||
        //                        t.Description.Contains(formsSearchQuery, StringComparison.OrdinalIgnoreCase))
        //            .OrderBy(t => t.Name)
        //            .ToList();

        //        private List<FormResponse> FilteredFormResponses
        //        {
        //            get
        //            {
        //                var responses = memberFormResponses.AsEnumerable();

        //                if (!string.IsNullOrEmpty(formsSearchQuery))
        //                {
        //                    responses = responses.Where(r =>
        //                        r.TemplateName.Contains(formsSearchQuery, StringComparison.OrdinalIgnoreCase));
        //                }

        //                if (activeFormsTab == "Recent")
        //                {
        //                    responses = responses.Where(r => r.SubmittedDate >= DateTime.Now.AddDays(-30));
        //                }

        //                return responses.OrderByDescending(r => r.SubmittedDate).ToList();
        //            }
        //        }

        //        private void CreateNewForm()
        //        {
        //            // Reset current template
        //            currentTemplate = new FormTemplate
        //            {
        //                Name = "",
        //                Description = "",
        //                Sections = new List<FormSection>(),
        //                IncludeGallery = false,
        //                IncludeSignature = false
        //            };

        //            showTemplateModal = true;
        //        }

        //        private void EditTemplate(FormTemplate template)
        //        {
        //            currentTemplate = new FormTemplate
        //            {
        //                Id = template.Id,
        //                Name = template.Name,
        //                Description = template.Description,
        //                Sections = template.Sections.Select(s => new FormSection
        //                {
        //                    Id = s.Id,
        //                    Name = s.Name,
        //                    Order = s.Order,
        //                    Questions = s.Questions.Select(q => new FormQuestion
        //                    {
        //                        Id = q.Id,
        //                        Text = q.Text,
        //                        AnswerType = q.AnswerType,
        //                        Options = q.Options.ToList(),
        //                        AllowMultipleChoice = q.AllowMultipleChoice,
        //                        IncludeRemarks = q.IncludeRemarks,
        //                        Order = q.Order
        //                    }).ToList()
        //                }).ToList(),
        //                PhotoRemarks = template.PhotoRemarks != null ? new PhotoRemarks
        //                {
        //                    Id = template.PhotoRemarks.Id,
        //                    BackdropName = template.PhotoRemarks.BackdropName,
        //                    Layout = template.PhotoRemarks.Layout,
        //                    Photos = template.PhotoRemarks.Photos.Select(p => new PhotoItem
        //                    {
        //                        Id = p.Id,
        //                        DataUrl = p.DataUrl,
        //                        FileName = p.FileName,
        //                        Remarks = p.Remarks.ToList(),
        //                        CustomRemark = p.CustomRemark,
        //                        GridPosition = p.GridPosition
        //                    }).ToList()
        //                } : null,
        //                TermsAndConditions = template.TermsAndConditions,
        //                IncludeGallery = template.IncludeGallery,
        //                IncludeSignature = template.IncludeSignature,
        //                CreatedDate = template.CreatedDate,
        //                ModifiedDate = template.ModifiedDate,
        //                IsActive = template.IsActive
        //            };

        //            showTemplateModal = true;
        //        }

        //        private void CloseTemplateModal()
        //        {
        //            showTemplateModal = false;
        //            currentTemplate = new FormTemplate();
        //            currentSection = null;
        //        }

        //        private void AddSection()
        //        {
        //            var newSection = new FormSection
        //            {
        //                Id = currentTemplate.Sections.Any() ? currentTemplate.Sections.Max(s => s.Id) + 1 : 1,
        //                Name = $"Section {currentTemplate.Sections.Count + 1}",
        //                Order = currentTemplate.Sections.Count + 1,
        //                Questions = new List<FormQuestion>()
        //            };

        //            currentTemplate.Sections.Add(newSection);
        //            currentSection = newSection;
        //        }

        //        private void EditSection(FormSection section)
        //        {
        //            currentSection = section;
        //        }

        //        private void DeleteSection(FormSection section)
        //        {
        //            currentTemplate.Sections.Remove(section);

        //            // Reorder remaining sections
        //            for (int i = 0; i < currentTemplate.Sections.Count; i++)
        //            {
        //                currentTemplate.Sections[i].Order = i + 1;
        //            }
        //        }

        //        private void CopySection(FormSection section)
        //        {
        //            copySectionId = section.Id;
        //        }

        //        private void PasteSection()
        //        {
        //            if (copySectionId == null) return;

        //            var sourceSection = formTemplates
        //                .SelectMany(t => t.Sections)
        //                .FirstOrDefault(s => s.Id == copySectionId);

        //            if (sourceSection != null)
        //            {
        //                var newSection = new FormSection
        //                {
        //                    Id = currentTemplate.Sections.Any() ? currentTemplate.Sections.Max(s => s.Id) + 1 : 1,
        //                    Name = sourceSection.Name + " (Copy)",
        //                    Order = currentTemplate.Sections.Count + 1,
        //                    Questions = sourceSection.Questions.Select(q => new FormQuestion
        //                    {
        //                        Id = q.Id,
        //                        Text = q.Text,
        //                        AnswerType = q.AnswerType,
        //                        Options = q.Options.ToList(),
        //                        AllowMultipleChoice = q.AllowMultipleChoice,
        //                        IncludeRemarks = q.IncludeRemarks,
        //                        Order = q.Order
        //                    }).ToList()
        //                };

        //                currentTemplate.Sections.Add(newSection);
        //            }

        //            copySectionId = null;
        //        }

        //        private void OpenQuestionModal(FormSection section, FormQuestion? question = null)
        //        {
        //            currentSection = section;

        //            if (question != null)
        //            {
        //                currentQuestion = question;
        //                questionText = question.Text;
        //                questionAnswerType = question.AnswerType;
        //                questionOptions = question.Options.ToList();
        //                questionAllowMultipleChoice = question.AllowMultipleChoice;
        //                questionIncludeRemarks = question.IncludeRemarks;
        //            }
        //            else
        //            {
        //                currentQuestion = null;
        //                questionText = "";
        //                questionAnswerType = AnswerType.Open;
        //                questionOptions = new List<string>();
        //                questionAllowMultipleChoice = false;
        //                questionIncludeRemarks = false;
        //            }

        //            showQuestionModal = true;
        //        }

        //        private void CloseQuestionModal()
        //        {
        //            showQuestionModal = false;
        //            currentQuestion = null;
        //            questionText = "";
        //            questionOptions.Clear();
        //            newOption = "";
        //        }

        //        private void AddOption()
        //        {
        //            if (!string.IsNullOrWhiteSpace(newOption))
        //            {
        //                questionOptions.Add(newOption.Trim());
        //                newOption = "";
        //            }
        //        }

        //        private void RemoveOption(string option)
        //        {
        //            questionOptions.Remove(option);
        //        }

        //        private void SaveQuestion()
        //        {
        //            if (currentSection == null || string.IsNullOrWhiteSpace(questionText)) return;

        //            if (currentQuestion != null)
        //            {
        //                // Update existing question
        //                currentQuestion.Text = questionText;
        //                currentQuestion.AnswerType = questionAnswerType;
        //                currentQuestion.Options = questionOptions.ToList();
        //                currentQuestion.AllowMultipleChoice = questionAllowMultipleChoice;
        //                currentQuestion.IncludeRemarks = questionIncludeRemarks;
        //            }
        //            else
        //            {
        //                // Add new question
        //                var newQuestion = new FormQuestion
        //                {
        //                    Id = currentSection.Questions.Any() ? currentSection.Questions.Max(q => q.Id) + 1 : 1,
        //                    Text = questionText,
        //                    AnswerType = questionAnswerType,
        //                    Options = questionOptions.ToList(),
        //                    AllowMultipleChoice = questionAllowMultipleChoice,
        //                    IncludeRemarks = questionIncludeRemarks,
        //                    Order = currentSection.Questions.Count + 1
        //                };

        //                currentSection.Questions.Add(newQuestion);
        //            }

        //            CloseQuestionModal();
        //        }

        //        private void DeleteQuestion(FormSection section, FormQuestion question)
        //        {
        //            section.Questions.Remove(question);

        //            // Reorder remaining questions
        //            for (int i = 0; i < section.Questions.Count; i++)
        //            {
        //                section.Questions[i].Order = i + 1;
        //            }
        //        }

        //        private void OpenPhotoRemarksModal()
        //        {
        //            if (currentTemplate.PhotoRemarks == null)
        //            {
        //                currentTemplate.PhotoRemarks = new PhotoRemarks
        //                {
        //                    Id = 1,
        //                    BackdropName = "",
        //                    Layout = PhotoLayout.Grid,
        //                    Photos = new List<PhotoItem>()
        //                };
        //            }

        //            showPhotoRemarksModal = true;
        //        }

        //        private void ClosePhotoRemarksModal()
        //        {
        //            showPhotoRemarksModal = false;
        //        }

        //        private async void AddPhotoToRemarks(InputFileChangeEventArgs e)
        //        {
        //            const long maxSizeBytes = 6 * 1024 * 1024; // 6 MB

        //            foreach (var file in e.GetMultipleFiles())
        //            {
        //                if (file.Size > maxSizeBytes)
        //                {
        //                    await JS.InvokeVoidAsync("alert", $"File {file.Name} is too large (max 6MB)");
        //                    continue;
        //                }

        //                if (!file.ContentType.StartsWith("image/"))
        //                {
        //                    await JS.InvokeVoidAsync("alert", $"File {file.Name} is not an image");
        //                    continue;
        //                }

        //                try
        //                {
        //                    using var stream = file.OpenReadStream(maxSizeBytes);
        //                    using var ms = new MemoryStream();
        //                    await stream.CopyToAsync(ms);

        //                    var base64 = Convert.ToBase64String(ms.ToArray());
        //                    var dataUrl = $"data:{file.ContentType};base64,{base64}";

        //                    var newPhoto = new PhotoItem
        //                    {
        //                        Id = currentTemplate.PhotoRemarks.Photos.Any()
        //                            ? currentTemplate.PhotoRemarks.Photos.Max(p => p.Id) + 1
        //                            : 1,
        //                        DataUrl = dataUrl,
        //                        FileName = file.Name,
        //                        Remarks = new List<string>(),
        //                        GridPosition = 1
        //                    };

        //                    // Initialize remarks for grid mode
        //                    if (currentTemplate.PhotoRemarks.Layout == PhotoLayout.Grid)
        //                    {
        //                        for (int i = 0; i < 6; i++)
        //                        {
        //                            newPhoto.Remarks.Add("");
        //                        }
        //                    }

        //                    currentTemplate.PhotoRemarks.Photos.Add(newPhoto);
        //                    StateHasChanged();
        //                }
        //                catch (Exception ex)
        //                {
        //                    await JS.InvokeVoidAsync("alert", $"Error reading {file.Name}: {ex.Message}");
        //                }
        //            }
        //        }

        //        private void RemovePhoto(PhotoItem photo)
        //        {
        //            currentTemplate.PhotoRemarks.Photos.Remove(photo);
        //        }

        //        private void OpenPhotoRemarksEditor(PhotoItem photo)
        //        {
        //            currentPhotoItem = photo;
        //            selectedGridPosition = photo.GridPosition;

        //            if (currentTemplate.PhotoRemarks.Layout == PhotoLayout.Grid)
        //            {
        //                // Ensure remarks list has 6 items
        //                while (photo.Remarks.Count < 6)
        //                {
        //                    photo.Remarks.Add("");
        //                }
        //            }
        //            else
        //            {
        //                photoRemarksText = photo.CustomRemark ?? "";
        //            }
        //        }

        //        private void SavePhotoRemarks()
        //        {
        //            if (currentTemplate.PhotoRemarks.Layout == PhotoLayout.Grid)
        //            {
        //                // Remarks are already updated through two-way binding
        //            }
        //            else
        //            {
        //                currentPhotoItem.CustomRemark = photoRemarksText;
        //            }

        //            currentPhotoItem.GridPosition = selectedGridPosition;
        //            currentPhotoItem = new PhotoItem();
        //            photoRemarksText = "";
        //        }

        //        private void SaveTemplate()
        //        {
        //            if (string.IsNullOrWhiteSpace(currentTemplate.Name))
        //            {
        //                ShowNotification("Please enter a template name");
        //                return;
        //            }

        //            if (currentTemplate.Id == 0)
        //            {
        //                // New template
        //                currentTemplate.Id = formTemplates.Any() ? formTemplates.Max(t => t.Id) + 1 : 1;
        //                currentTemplate.CreatedDate = DateTime.Now;
        //                formTemplates.Add(currentTemplate);
        //            }
        //            else
        //            {
        //                // Update existing template
        //                var existing = formTemplates.FirstOrDefault(t => t.Id == currentTemplate.Id);
        //                if (existing != null)
        //                {
        //                    var index = formTemplates.IndexOf(existing);
        //                    currentTemplate.ModifiedDate = DateTime.Now;
        //                    formTemplates[index] = currentTemplate;
        //                }
        //            }

        //            ShowNotification($"Template '{currentTemplate.Name}' saved successfully");
        //            CloseTemplateModal();
        //        }

        //        private void UseTemplate(FormTemplate template)
        //        {
        //            if (selectedMember == null) return;

        //            // Create a new form response from template
        //            var response = new FormResponse
        //            {
        //                Id = memberFormResponses.Any() ? memberFormResponses.Max(r => r.Id) + 1 : 1,
        //                TemplateId = template.Id,
        //                TemplateName = template.Name,
        //                MemberId = selectedMember.Id,
        //                SubmittedDate = DateTime.Now,
        //                Answers = new Dictionary<string, object>(),
        //                PhotoResponses = new List<PhotoResponse>(),
        //                GalleryPhotos = new List<string>(),
        //                SignatureDataUrl = null
        //            };

        //            memberFormResponses.Add(response);
        //            selectedMember.FormResponses = memberFormResponses;

        //            // Here you would open the form for filling
        //            ShowNotification($"Opening form: {template.Name}");
        //            CloseFormsModal();
        //        }

        //        private void ViewFormResponse(FormResponse response)
        //        {
        //            // Implement form response viewer
        //            ShowNotification($"Viewing form response from {response.SubmittedDate:dd MMM yyyy}");
        //        }

        //        private void SetActiveFormsTabToTemplates()
        //        {
        //            activeFormsTab = "Templates";
        //        }

        // Recent Modal properties
        private bool showRecentModal = false;
        private string activeRecentTab = "Service";
        private bool showRecentDetailModal = false;
        private RecentActivityItem? selectedRecentItem = null;

        // Update the RecentActivityItem class with more fields
        public class RecentActivityItem
        {
            public string Name { get; set; } = "";
            public string Description { get; set; } = "";
            public decimal Amount { get; set; }
            public string Date { get; set; } = "";
            public string Time { get; set; } = "03:20 PM";
            public string Category { get; set; } = ""; // Service, Product, Package, Discount
            public string TransactionId { get; set; } = $"INV-{new Random().Next(10000, 99999)}";
            public string StaffName { get; set; } = "Sarah Chen";
            public string? Notes { get; set; }

            // Package specific
            public int RemainingSessions { get; set; } = 5;
            public string ExpiryDate { get; set; } = "31/12/2026";

            // Discount specific
            public string DiscountCode { get; set; } = "";
            public string ValidUntil { get; set; } = "";
        }

        // Sample recent activity data (updated with more details)
        private List<RecentActivityItem> recentActivities = new()
        {
            // Package items (like in your image)
            new RecentActivityItem {
                Name = "Top Up Package",
                Description = "500 free 50",
                Amount = 500.00m,
                Date = "13/01/2026",
                Time = "02:30 PM",
                Category = "Package",
                TransactionId = "INV-20260113-001",
                StaffName = "Sarah Chen",
                RemainingSessions = 8,
                ExpiryDate = "13/07/2026",
                Notes = "Package includes 10 sessions with 2 free bonus sessions"
            },
            new RecentActivityItem {
                Name = "Buy 5 Free 3",
                Description = "Package deal - Facial treatments",
                Amount = 500.00m,
                Date = "13/01/2026",
                Time = "11:15 AM",
                Category = "Package",
                TransactionId = "INV-20260113-002",
                StaffName = "Michelle Wong",
                RemainingSessions = 5,
                ExpiryDate = "13/01/2027",
                Notes = "Buy 5 sessions get 3 free. Valid for all facial treatments."
            },

            // Service items
            new RecentActivityItem {
                Name = "Facial Treatment",
                Description = "Hydrating Facial • 60min",
                Amount = 180.00m,
                Date = "13/01/2026",
                Time = "10:00 AM",
                Category = "Service",
                TransactionId = "SVC-20260113-001",
                StaffName = "Dr. Sarah Lim",
                Notes = "Client requested extra hydration. Used sensitive skin products."
            },
            new RecentActivityItem {
                Name = "Hair Cut & Style",
                Description = "Premium styling with wash",
                Amount = 120.00m,
                Date = "12/01/2026",
                Time = "04:45 PM",
                Category = "Service",
                TransactionId = "SVC-20260112-003",
                StaffName = "James Wong"
            },
            new RecentActivityItem {
                Name = "Massage Therapy",
                Description = "Swedish massage • 90min",
                Amount = 200.00m,
                Date = "10/01/2026",
                Time = "02:00 PM",
                Category = "Service",
                TransactionId = "SVC-20260110-008",
                StaffName = "Lisa Chen"
            },

            // Product items
            new RecentActivityItem {
                Name = "Skincare Set",
                Description = "Anti-aging cream + serum",
                Amount = 350.00m,
                Date = "13/01/2026",
                Time = "11:30 AM",
                Category = "Product",
                TransactionId = "PROD-20260113-001",
                StaffName = "Sarah Chen",
                Notes = "Bought as gift. Includes 3-month supply."
            },
            new RecentActivityItem {
                Name = "Hair Oil",
                Description = "Argan oil 100ml",
                Amount = 85.00m,
                Date = "11/01/2026",
                Time = "05:20 PM",
                Category = "Product",
                TransactionId = "PROD-20260111-004",
                StaffName = "James Wong"
            },

            // Discount items
            new RecentActivityItem {
                Name = "Member Discount",
                Description = "10% off total bill",
                Amount = 25.00m,
                Date = "13/01/2026",
                Time = "02:30 PM",
                Category = "Discount",
                TransactionId = "DISC-20260113-001",
                StaffName = "System",
                DiscountCode = "MEMBER10",
                ValidUntil = "31/12/2026",
                Notes = "Applied automatically for VIP members"
            },
            new RecentActivityItem {
                Name = "Promo Code",
                Description = "WELCOME20 - New member promo",
                Amount = 40.00m,
                Date = "08/01/2026",
                Time = "09:15 AM",
                Category = "Discount",
                TransactionId = "DISC-20260108-002",
                StaffName = "System",
                DiscountCode = "WELCOME20",
                ValidUntil = "31/03/2026"
            }
        };

        // Helper methods to filter by category
        private List<RecentActivityItem> GetRecentServiceItems() =>
            recentActivities.Where(r => r.Category == "Service").OrderByDescending(r => r.Date + " " + r.Time).ToList();

        private List<RecentActivityItem> GetRecentProductItems() =>
            recentActivities.Where(r => r.Category == "Product").OrderByDescending(r => r.Date + " " + r.Time).ToList();

        private List<RecentActivityItem> GetRecentPackageItems() =>
            recentActivities.Where(r => r.Category == "Package").OrderByDescending(r => r.Date + " " + r.Time).ToList();

        private List<RecentActivityItem> GetRecentDiscountItems() =>
            recentActivities.Where(r => r.Category == "Discount").OrderByDescending(r => r.Date + " " + r.Time).ToList();

        // Update the ViewRecentActivity method
        private void ViewRecentActivity()
        {
            if (selectedMemberForUI == null) return;
            activeRecentTab = "Service";
            showRecentModal = true;
        }

        // Open detail modal
        private void OpenRecentDetailModal(RecentActivityItem item)
        {
            selectedRecentItem = item;
            showRecentDetailModal = true;
        }

        // Action methods
        private void BookAgain()
        {
            ShowNotification($"Booking again: {selectedRecentItem?.Name}");
            showRecentDetailModal = false;
            showRecentModal = false;
        }

        private void ReorderProduct()
        {
            ShowNotification($"Reordering: {selectedRecentItem?.Name}");
            showRecentDetailModal = false;
            showRecentModal = false;
        }

        public class SalesRecord
        {
            public int Id { get; set; }
            public string OrderNumber { get; set; } = "";
            public DateTime TransactionDate { get; set; }
            public string StaffName { get; set; } = "";
            public string StaffCode { get; set; } = "";
            public decimal TotalAmount { get; set; }
            public int ItemCount { get; set; }
            public List<SalesItem> Items { get; set; } = new();
            public string PaymentMethod { get; set; } = "";
            public string Status { get; set; } = "Completed";
        }

        public class SalesItem
        {
            public int Id { get; set; }
            public string Name { get; set; } = "";
            public string Category { get; set; } = ""; // Service, Product, Package
            public decimal Price { get; set; }
            public int Quantity { get; set; }
            public decimal Subtotal { get; set; }
        }

        // Add these properties to your Members class
        private bool showSalesModal = false;
        private string activeSalesTab = "<30 days";
        private SalesRecord? selectedSalesRecord = null;
        private bool showSalesDetailModal = false;

        // Helper method to get filtered sales records
        private List<SalesRecord> FilteredSalesRecords
        {
            get
            {
                if (selectedMemberForUI == null) return new List<SalesRecord>();

                var records = selectedMemberForUI.SalesRecords;
                var today = DateTime.Today;

                switch (activeSalesTab)
                {
                    case "<30 days":
                        return records.Where(r => r.TransactionDate >= today.AddDays(-30))
                                      .OrderByDescending(r => r.TransactionDate)
                                      .ToList();
                    case "<180 days":
                        return records.Where(r => r.TransactionDate >= today.AddDays(-180) && r.TransactionDate < today.AddDays(-30))
                                      .OrderByDescending(r => r.TransactionDate)
                                      .ToList();
                    case ">180 days":
                        return records.Where(r => r.TransactionDate < today.AddDays(-180))
                                      .OrderByDescending(r => r.TransactionDate)
                                      .ToList();
                    case "All":
                    default:
                        return records.OrderByDescending(r => r.TransactionDate).ToList();
                }
            }
        }

        private void ViewSalesHistory()
        {
            if (selectedMemberForUI == null) return;
            activeSalesTab = "<30 days";
            showSalesModal = true;
        }

        private string FormatTransactionDate(DateTime date)
        {
            return date.ToString("hh:mm tt, dd/MM/yyyy");
        }

        // Helper method to get staff display
        private string GetStaffDisplay(SalesRecord record)
        {
            return $"{record.StaffName} (···{record.StaffCode})";
        }

        // Methods for sales detail
        private void OpenSalesDetail(SalesRecord record)
        {
            selectedSalesRecord = record;
            showSalesDetailModal = true;
        }

        private void CloseSalesDetail()
        {
            showSalesDetailModal = false;
            selectedSalesRecord = null;
        }

        public class AppointmentRecord
        {
            public int Id { get; set; }
            public string CustomerName { get; set; } = "";
            public string CustomerAvatar { get; set; } = "";
            public DateTime DateTime { get; set; }
            public string TimeRange { get; set; } = "";
            public string StaffName { get; set; } = "";
            public List<AppointmentService> Services { get; set; } = new();
            public string Status { get; set; } = "Upcoming"; // Upcoming, Past, No Show
            public string? Notes { get; set; }
            public decimal TotalAmount { get; set; }
            public bool IsPaid { get; set; }
            public string? PaymentMethod { get; set; }
            public DateTime? CheckInTime { get; set; }
            public DateTime? CheckOutTime { get; set; }
            public string? CancellationReason { get; set; }
            public DateTime? CancelledAt { get; set; }
        }

        public class AppointmentService
        {
            public int Id { get; set; }
            public string Name { get; set; } = "";
            public decimal Price { get; set; }
            public int DurationMinutes { get; set; }
            public string? StaffName { get; set; }
        }

        private bool showAppointmentModal = false;
        private string activeAppointmentTab = "Upcoming"; // Upcoming, Past, No Show
        private List<AppointmentRecord> appointmentRecords = new();
        private AppointmentDM? selectedAppointment = null;
        private bool showAppointmentDetailModal = false;

        private void InitializeSampleAppointments()
        {
            appointmentRecords = new List<AppointmentRecord>
                    {
                        new AppointmentRecord
                        {
                            Id = 1,
                            CustomerName = "Tan Wei Jie",
                            CustomerAvatar = "WJ",
                            DateTime = new DateTime(2026, 3, 1, 10, 30, 0),
                            TimeRange = "10:30 AM – 12:30 PM",
                            StaffName = "Sarah Chen",
                            Services = new List<AppointmentService>
                            {
                                new AppointmentService { Id = 1, Name = "Skin hydration facial", Price = 89.00m, DurationMinutes = 60, StaffName = "Sarah Chen" },
                                new AppointmentService { Id = 2, Name = "Antioxidant facial", Price = 99.00m, DurationMinutes = 60, StaffName = "Sarah Chen" }
                            },
                            Status = "Upcoming",
                            TotalAmount = 188.00m,
                            IsPaid = false,
                            Notes = "Client requested extra hydration. Sensitive skin.",
                            CheckInTime = null,
                            CheckOutTime = null
                        },
                        new AppointmentRecord
                        {
                            Id = 2,
                            CustomerName = "Ling",
                            CustomerAvatar = "LG",
                            DateTime = new DateTime(2026, 2, 28, 14, 0, 0),
                            TimeRange = "2:00 PM – 3:30 PM",
                            StaffName = "Michelle Wong",
                            Services = new List<AppointmentService>
                            {
                                new AppointmentService { Id = 3, Name = "Hair Cut & Style", Price = 120.00m, DurationMinutes = 90, StaffName = "Michelle Wong" }
                            },
                            Status = "Past",
                            TotalAmount = 120.00m,
                            IsPaid = true,
                            PaymentMethod = "Credit Card",
                            CheckInTime = new DateTime(2026, 2, 28, 13, 55, 0),
                            CheckOutTime = new DateTime(2026, 2, 28, 15, 20, 0),
                            Notes = "Regular customer. Used loyalty points for discount."
                        },
                        new AppointmentRecord
                        {
                            Id = 3,
                            CustomerName = "WJ",
                            CustomerAvatar = "WJ",
                            DateTime = new DateTime(2026, 2, 25, 11, 0, 0),
                            TimeRange = "11:00 AM – 12:30 PM",
                            StaffName = "Dr. Sarah Lim",
                            Services = new List<AppointmentService>
                            {
                                new AppointmentService { Id = 4, Name = "Acne Treatment", Price = 150.00m, DurationMinutes = 90, StaffName = "Dr. Sarah Lim" }
                            },
                            Status = "No Show",
                            TotalAmount = 150.00m,
                            IsPaid = false,
                            Notes = "Client didn't show up. No cancellation notice.",
                            CancellationReason = "No show",
                            CancelledAt = new DateTime(2026, 2, 25, 11, 15, 0)
                        },
                        new AppointmentRecord
                        {
                            Id = 4,
                            CustomerName = "Ethan",
                            CustomerAvatar = "ET",
                            DateTime = new DateTime(2026, 3, 5, 15, 30, 0),
                            TimeRange = "3:30 PM – 5:00 PM",
                            StaffName = "Lisa Chen",
                            Services = new List<AppointmentService>
                            {
                                new AppointmentService { Id = 5, Name = "Swedish Massage", Price = 180.00m, DurationMinutes = 90, StaffName = "Lisa Chen" }
                            },
                            Status = "Upcoming",
                            TotalAmount = 180.00m,
                            IsPaid = true,
                            PaymentMethod = "Online",
                            Notes = "Prefers firm pressure. Shoulder tension."
                        },
                        new AppointmentRecord
                        {
                            Id = 5,
                            CustomerName = "John Doe",
                            CustomerAvatar = "JD",
                            DateTime = new DateTime(2026, 2, 20, 9, 0, 0),
                            TimeRange = "9:00 AM – 10:00 AM",
                            StaffName = "James Wong",
                            Services = new List<AppointmentService>
                            {
                                new AppointmentService { Id = 6, Name = "Men's Haircut", Price = 65.00m, DurationMinutes = 60, StaffName = "James Wong" }
                            },
                            Status = "Past",
                            TotalAmount = 65.00m,
                            IsPaid = true,
                            PaymentMethod = "Cash",
                            CheckInTime = new DateTime(2026, 2, 20, 8, 55, 0),
                            CheckOutTime = new DateTime(2026, 2, 20, 10, 5, 0),
                            Notes = "First time customer. Recommended styling products."
                        }
                    };
        }

        private List<AppointmentRecord> FilteredAppointments
        {
            get
            {
                var filtered = appointmentRecords.Where(a => a.CustomerName == selectedMemberForUI?.Name ||
                                                             a.CustomerName == selectedMemberForUI?.Name);

                switch (activeAppointmentTab)
                {
                    case "Upcoming":
                        filtered = filtered.Where(a => a.Status == "Upcoming" && a.DateTime >= DateTime.Today);
                        break;
                    case "Past":
                        filtered = filtered.Where(a => a.Status == "Past" ||
                                                      (a.Status == "Upcoming" && a.DateTime < DateTime.Today));
                        break;
                    case "No Show":
                        filtered = filtered.Where(a => a.Status == "No Show");
                        break;
                }

                return filtered.OrderBy(a => a.Status == "Upcoming" ? a.DateTime : a.DateTime).ToList();
            }
        }

        private string GetAppointmentTabCount(string tab)
        {
            var count = tab switch
            {
                "Upcoming" => appointmentRecords.Count(a => a.Status == "Upcoming" && a.DateTime >= DateTime.Today),
                "Past" => appointmentRecords.Count(a => a.Status == "Past" || (a.Status == "Upcoming" && a.DateTime < DateTime.Today)),
                "No Show" => appointmentRecords.Count(a => a.Status == "No Show"),
                _ => 0
            };

            return count > 0 ? $"({count})" : "";
        }

        private string GetStatusBadgeClass(string status)
        {
            return status switch
            {
                "Upcoming" => "status-upcoming",
                "Past" => "status-past",
                "No Show" => "status-noshow",
                _ => ""
            };
        }

        private string GetStatusIcon(string status)
        {
            return status switch
            {
                "Upcoming" => "⏰",
                "Past" => "✅",
                "Cancelled" => "❌",
                _ => "📅"
            };
        }

        private string FormatAppointmentDuration(List<AppointmentService> services)
        {
            var totalMinutes = services.Sum(s => s.DurationMinutes);
            var hours = totalMinutes / 60;
            var minutes = totalMinutes % 60;

            if (hours > 0 && minutes > 0)
                return $"{hours} hour {minutes} min";
            else if (hours > 0)
                return $"{hours} hour{(hours > 1 ? "s" : "")}";
            else
                return $"{minutes} min";
        }

        private void OpenAppointmentDetail(AppointmentDM appointment)
        {
            selectedAppointment = appointment;
            showAppointmentDetailModal = true;
        }

        private void CloseAppointmentDetail()
        {
            showAppointmentDetailModal = false;
            selectedAppointment = null;
        }

        private void RescheduleAppointment()
        {
            ShowNotification($"Rescheduling appointment for {selectedAppointment?.CustomerName}...");
            // Implement reschedule logic
            showAppointmentDetailModal = false;
        }

        //private void CancelAppointment()
        //{
        //    if (selectedAppointment == null) return;

        //    ShowNotification($"Cancelling appointment...");
        //    // Implement cancellation logic
        //    selectedAppointment.Status = "No Show";
        //    selectedAppointment.CancellationReason = "Cancelled by staff";
        //    selectedAppointment.CancelledAt = DateTime.Now;
        //    showAppointmentDetailModal = false;
        //}

        //private void CheckInAppointment()
        //{
        //    if (selectedAppointment == null) return;

        //    selectedAppointment.CheckInTime = DateTime.Now;
        //    ShowNotification($"Checked in {selectedAppointment.CustomerName}");
        //    StateHasChanged();
        //}

        //private void MarkAsNoShow()
        //{
        //    if (selectedAppointment == null) return;

        //    selectedAppointment.Status = "No Show";
        //    selectedAppointment.CancellationReason = "No show";
        //    selectedAppointment.CancelledAt = DateTime.Now;
        //    ShowNotification($"Marked as no show");
        //    showAppointmentDetailModal = false;
        //}

        private string GetStatusColor(string status)
        {
            return status switch
            {
                "Upcoming" => "#e3f2fd",
                "Past" => "#e8f5e9",
                "No Show" => "#ffebee",
                _ => "#f5f5f5"
            };
        }

        private string GetStatusBackground(string status)
        {
            return status switch
            {
                "Upcoming" => "#e3f2fd",
                "Past" => "#e8f5e9",
                "No Show" => "#ffebee",
                _ => "#f5f5f5"
            };
        }

        private string GetStatusTextColor(string status)
        {
            return status switch
            {
                "Upcoming" => "#1976d2",
                "Past" => "#2e7d32",
                "No Show" => "#c62828",
                _ => "#666"
            };
        }

        private DateTime _selectedDate = DateTime.Today;
        private DateTime selectedDate
        {
            get => _selectedDate;
            set
            {
                if (_selectedDate != value)
                {
                    _selectedDate = value;
                    _ = LoadSales();
                }
            }
        }

        private string _searchAmount = "";
        private string searchAmount
        {
            get => _searchAmount;
            set
            {
                if (_searchAmount != value)
                {
                    _searchAmount = value;
                    showAmountError = !string.IsNullOrWhiteSpace(value) && !decimal.TryParse(value, out _);
                }
            }
        }
        private bool isLoading = false;
        private List<Doc_CashSalesDM> sales = new();

        private async Task LoadSales()
        {
            isLoading = true;
            StateHasChanged();

            try
            {
                string branchId = AppState.SelectedBranchID;
                if (string.IsNullOrEmpty(branchId))
                {
                    try
                    {
                        branchId = await JS.InvokeAsync<string>("localStorage.getItem", "currentBranch");

                        if (!string.IsNullOrEmpty(branchId))
                        {
                            AppState.SelectedBranchID = branchId;
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Error reading from localStorage: {ex.Message}");
                    }
                }
                sales = await SalesService.GetSalesHistoryAsync(branchId, selectedDate);
            }
            finally
            {
                isLoading = false;
                StateHasChanged();
            }
        }

        private List<Doc_CashSalesDM> FilteredSales()
        {
            if (selectedMember == null) return new List<Doc_CashSalesDM>();

            var memberSpecificSales = sales.Where(s => s.AccountID == selectedMember.MasterAccountID).ToList();

            if (string.IsNullOrWhiteSpace(searchAmount))
            {
                return memberSpecificSales;
            }

            if (decimal.TryParse(searchAmount, out var targetAmt))
            {
                return memberSpecificSales.Where(s => s.TotalAfterTax == targetAmt).ToList();
            }

            return new List<Doc_CashSalesDM>();
        }

        private void OnSearchInput(ChangeEventArgs e)
        {
            searchAmount = e.Value?.ToString() ?? "";
        }

        private bool showDeleteConfirm = false;
        private Doc_CashSalesDM? saleToDelete = null;
        private bool showAmountError = false;

        private void ConfirmDelete(Doc_CashSalesDM sale)
        {
            if (sale.eInvoiceStatus == "Valid")
            {
                ShowNotification("This record is linked to a Valid e-Invoice and cannot be deleted.");
                return;
            }
            saleToDelete = sale;
            showDeleteConfirm = true;
        }

        private void CancelDelete()
        {
            saleToDelete = null;
            showDeleteConfirm = false;
        }

        private async Task ExecuteDelete()
        {
            if (saleToDelete != null)
            {
                if (saleToDelete.eInvoiceStatus == "Valid")
                {
                    ShowNotification("This record is linked to a Valid e-Invoice and cannot be deleted.");
                    CancelDelete();
                    return;
                }
                isLoading = true;
                StateHasChanged();

                var result = await SalesService.DeleteSaleAsync(saleToDelete.DocumentID);

                if (result.Success)
                {
                    sales.Remove(saleToDelete);
                    ShowNotification("Record Deleted Successfully");
                }
                else
                {
                    ShowNotification("Error: " + result.Message);
                }

                isLoading = false;
            }
            CancelDelete();
        }


        private bool IsValidPhoneNumber
        {
            get
            {
                if (string.IsNullOrWhiteSpace(targetPhone)) return false;

                bool isNumeric = targetPhone.All(char.IsDigit);

                bool isMinLength = targetPhone.Length >= 10;

                return isNumeric && isMinLength;
            }
        }

        [Inject] private WhatsAppService WhatsApp { get; set; } = default!;

        private bool showWhatsAppModal = false;
        private bool isProcessing = false;
        private string targetPhone = "";
        private Doc_CashSalesDM? selectedSale;

        private void OpenWhatsAppPrompt(Doc_CashSalesDM sale)
        {
            selectedSale = sale;
            showWhatsAppModal = true;
        }

        private async Task ProcessWhatsApp()
        {
            if (selectedSale == null || string.IsNullOrWhiteSpace(targetPhone)) return;

            isProcessing = true;
            try
            {
                var (einvoice, bill) = await SalesService.GenerateInvoiceLinksAsync(selectedSale.DocumentID);

                if (string.IsNullOrEmpty(einvoice) && string.IsNullOrEmpty(bill))
                {
                    ShowNotification("Could not generate links");
                    return;
                }

                string companyName = AppState.CurrentBranch?.CompanyName ?? string.Empty;
                string message = WhatsApp.BuildMessage(companyName, einvoice, bill);

                await WhatsApp.OpenAsync(targetPhone, message);
                showWhatsAppModal = false;
            }
            catch (Exception ex)
            {
                ShowNotification("WhatsApp Error: " + ex.Message);
            }
            finally
            {
                isProcessing = false;
            }
        }

        private bool isDownloading = false;
        private string selectedDocId = "";

        private async Task DownloadPdf(Doc_CashSalesDM sale)
        {
            if (isDownloading) return;

            isDownloading = true;
            selectedDocId = sale.DocumentID;
            StateHasChanged();

            try
            {
                var success = await SalesService.DownloadReceiptPdfAsync(sale.DocumentID);

                if (success)
                {
                    //ShowNotification("Receipt downloaded successfully"
                    //);
                }
                else
                {
                    ShowNotification("Failed to generate PDF. Please try again.");
                }
            }
            catch (Exception ex)
            {
                ShowNotification("Error: " + ex.Message);
            }
            finally
            {
                isDownloading = false;
                selectedDocId = "";
                StateHasChanged();
            }
        }

        private bool isPrinting = false;

        private async Task PrintSale(Doc_CashSalesDM sale)
        {
            var printer = PrinterSvc.GetSelectedPrinter();
            if (printer == null)
            {
                ShowNotification("No printer selected. Please select a printer from the operation menu.");
                return;
            }

            try
            {
                isPrinting = true;
                selectedDocId = sale.DocumentID;
                StateHasChanged();

                var response = await _cashSalesAC.LoadRecordAsync(sale.DocumentID);
                if (response == null || response.StatusCode != 200 || response.Result == null)
                {
                    ShowNotification("Failed to load sales details from server.");
                    return;
                }

                var record = response.Result;
                var doc = record.objDoc_CashSales;

                CustomerDM? fullCustomer = null;
                if (!string.IsNullOrEmpty(doc?.AccountID))
                {
                    fullCustomer = await _customerService.GetSingleCustomer(doc.AccountID);
                }

                var branch = AppState.CurrentBranch;
                var receiptData = new ReceiptData
                {
                    CompanyName = branch?.CompanyName ?? "",
                    BranchName = branch?.Branch ?? "",
                    Address1 = branch?.Address1 ?? "",
                    Address2 = branch?.Address2 ?? "",
                    Address3 = branch?.Address3 ?? "",
                    Phone = branch?.Phone ?? "",
                    Email = branch?.Email ?? "",
                    CurrencyName = doc?.LocalCurrencyName ?? "",
                    CoRegistrationNo = branch?.CoRegistrationNo,
                    TIN = branch?.TIN,
                    CashierName = doc?.CashierName ?? "",
                    ReceiptNo = doc?.DisplayCode ?? "",
                    DateTimeOfSale = doc?.FinancialDate,
                    ReferenceNumber = doc?.ReferenceNumber ?? "",
                    CustomerName = doc?.AccountName ?? "",
                    CustomerID = doc?.AccountID ?? "",
                    CustomerAddress1 = fullCustomer?.Address1 ?? "",
                    CustomerAddress2 = fullCustomer?.Address2 ?? "",
                    CustomerCity = fullCustomer?.City ?? "",
                    CustomerPostcode = fullCustomer?.ZipCode ?? "",
                    CustomerState = fullCustomer?.CountryState ?? "",
                    CustomerCountry = fullCustomer?.Country ?? "",
                    CustomerPhone = fullCustomer?.Phone ?? "",

                    Items = record.lstDocumentLine.Select(i => new ReceiptLineItem
                    {
                        Name = i.Description,
                        Quantity = (int)i.Quantity,
                        Discount = i.Discount,
                        LineTotal = (i.UnitPrice * i.Quantity) - i.Discount,
                        Remarks = i.RefCompanyName
                    }).ToList(),

                    Payments = record.lstReceiptLines.Select(p => new ReceiptPaymentLine
                    {
                        Method = p.Description ?? "",
                        Amount = p.POSReceiptLineAmount
                    }).ToList(),

                    ChangeAmount = Math.Abs(record.lstReceiptLines
                        .FirstOrDefault(x => x.POSReceiptChangeAmount < 0)?.POSReceiptChangeAmount ?? 0m),

                    TaxSummary = record.lstDocumentLine
                        .Where(l => !string.IsNullOrEmpty(l.TaxCodeID))
                        .GroupBy(l => new { l.TaxCodeID, l.TaxPercentage })
                        .Select(g => new TaxSummaryLine
                        {
                            TaxCode = $"{g.Key.TaxCodeID} {g.Key.TaxPercentage * 100:0.#}%",
                            Amount = g.Sum(x => (x.UnitPrice * x.Quantity) - x.Discount),
                            Tax = g.Sum(x => x.TaxAmount)
                        }).ToList(),

                    Subtotal = doc?.TotalBeforeTax ?? 0,
                    RoundingAmount = doc?.RoundingAmount ?? 0,
                    GrandTotal = doc?.TotalAfterTax ?? 0,
                    PaidAmount = record.lstReceiptLines.Sum(p => p.POSReceiptLineAmount)
                };

                var (success, error) = await ReceiptPrinterSvc.PrintAsync(printer, receiptData);

                if (!success)
                {
                    ShowNotification($"Print failed: {error}");
                }
            }
            catch (Exception ex)
            {
                ShowNotification($"An error occurred during printing: {ex.Message}");
            }
            finally
            {
                isPrinting = false;
                selectedDocId = "";
                StateHasChanged();
            }
        }

        private List<AppointmentDM> _currentTabAppointments = new();
        private bool isApptLoading = false;

        private async Task SetAppointmentTab(string tab)
        {
            activeAppointmentTab = tab;
            await LoadAppointmentsByTab();
        }

        private async Task LoadAppointmentsByTab()
        {
            if (selectedMember == null) return;

            isApptLoading = true;
            _currentTabAppointments.Clear();
            StateHasChanged();

            DateTime start;
            DateTime end;
            var now = DateTime.Now;

            switch (activeAppointmentTab)
            {
                case "Upcoming":
                    start = DateTime.Today;
                    end = DateTime.Today.AddYears(1);
                    break;

                case "Past":
                    start = DateTime.Today.AddYears(-1);
                    end = DateTime.Today.AddTicks(-1);
                    break;

                case "Cancelled":
                    start = new DateTime(1900, 1, 1, 00, 00, 00);
                    end = new DateTime(1900, 1, 1, 00, 00, 00);
                    break;

                default:
                    return;
            }

            try
            {
                _currentTabAppointments = new List<AppointmentDM>();
            }
            catch (Exception ex)
            {
                ShowNotification($"Error: {ex.Message}");
            }
            finally
            {
                isApptLoading = false;
                StateHasChanged();
            }
        }

        private async Task ManageAppointments()
        {
            if (selectedMember == null) return;
            showAppointmentModal = true;
            await SetAppointmentTab("Upcoming");
        }

        private bool showUploadMemberModal = false;
        private string? selectedFileName;
        private MemoryStream? selectedFileStream;
        private string? selectedFileExtension;
        private bool IsProcessing = false;
        private string UploadStatus = "";
        private string? ErrorMessage;

        private void OpenUploadMemberModal()
        {
            showUploadMemberModal = true;
        }

        private void CloseUploadMemberModal()
        {
            showUploadMemberModal = false;
            // Optional: Reset state upon closing
            selectedFileStream?.Dispose();
            selectedFileStream = null;
            selectedFileName = null;
            selectedFileExtension = null;
            ErrorMessage = null;
        }

        private async Task DownloadTemplate()
        {
            try
            {
                var csvHeaders = new[]
                {
            "Name", "Contact", "Birthday Year", "Birthday Month", "Birthday Day",
            "Gender", "Email", "NRIC", "Ethnicity", "Marital Status", "Source",
            "Member Tier", "Remarks", "Address 1", "Address 2", "Postal Code",
            "City", "State", "Country"
        };

                await FileDownloadService.DownloadCsvTemplateAsync("member_upload_template.csv", csvHeaders);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error downloading template: {ex.Message}");
            }
        }

        private async Task HandleFileSelected(InputFileChangeEventArgs e)
        {
            var file = e.File;
            ErrorMessage = null;

            var ext = Path.GetExtension(file.Name).ToLower();

            if (ext != ".csv")
            {
                ErrorMessage = "Only CSV files are accepted. Please select a .csv file.";
                selectedFileName = null;
                selectedFileStream = null;
                selectedFileExtension = null;
                StateHasChanged();
                return;
            }

            selectedFileName = file.Name;
            selectedFileExtension = ext;
            selectedFileStream = new MemoryStream();

            using var stream = file.OpenReadStream(1024 * 1024 * 20); // 20 MB max
            await stream.CopyToAsync(selectedFileStream);
            selectedFileStream.Position = 0;
        }

        private async Task PickFileNativeAsync()
        {
            var result = await FilePickerService.PickFileAsync(new[] { ".csv" });

            if (result == null)
            {
                return;
            }

            selectedFileName = result.FileName;
            selectedFileExtension = result.Extension;
            selectedFileStream = result.Stream;
            ErrorMessage = null;
            StateHasChanged();
        }

        private async Task ConfirmUpload()
        {
            if (selectedFileStream == null || string.IsNullOrEmpty(selectedFileExtension)) return;

            if (selectedFileExtension != ".csv")
            {
                ErrorMessage = "Only CSV files are accepted.";
                return;
            }

            ErrorMessage = null;
            IsProcessing = true;
            UploadStatus = "Reading file content...";
            StateHasChanged();
            await Task.Yield();

            try
            {
                selectedFileStream.Position = 0;
                List<MemberUploadModel> records = new();

                var config = new CsvHelper.Configuration.CsvConfiguration(CultureInfo.InvariantCulture)
                {
                    HasHeaderRecord = true,
                    HeaderValidated = null,
                    MissingFieldFound = null,
                    TrimOptions = CsvHelper.Configuration.TrimOptions.Trim,
                    ShouldSkipRecord = args => args.Row.Parser.Record.All(string.IsNullOrWhiteSpace)
                };

                using var reader = new StreamReader(selectedFileStream, System.Text.Encoding.UTF8, leaveOpen: true);
                using var csv = new CsvReader(reader, config);

                if (!await csv.ReadAsync())
                {
                    ErrorMessage = "Import failed: The CSV file contains no rows.";
                    return;
                }

                csv.ReadHeader();
                var headers = csv.HeaderRecord ?? Array.Empty<string>();

                static bool HasAny(string[] hs, params string[] names) =>
                    names.Any(n => hs.Any(h => string.Equals(h?.Trim(), n, StringComparison.OrdinalIgnoreCase)));

                var looksLikeMemberTemplate = HasAny(headers, "Name") && HasAny(headers, "Contact");
                var looksLikeStaffTemplate = HasAny(headers, "MasterAccountID") && HasAny(headers, "AccountName") && HasAny(headers, "Phone");

                if (!looksLikeMemberTemplate && looksLikeStaffTemplate)
                {
                    ErrorMessage = "You uploaded the Staff CSV template into the Member import. Please use the Upload Staff option for this file.";
                    return;
                }

                if (!looksLikeMemberTemplate)
                {
                    ErrorMessage = "Import failed: CSV headers do not match the Member template. Please download the Member template and try again.";
                    return;
                }

                csv.Context.RegisterClassMap<MemberUploadMap>();
                await foreach (var record in csv.GetRecordsAsync<MemberUploadModel>())
                {
                    records.Add(record);
                }

                int successCount = 0;
                int total = records.Count;

                for (int i = 0; i < total; i++)
                {
                    var record = records[i];
                    if (string.IsNullOrWhiteSpace(record.AccountName) ||
                        string.IsNullOrWhiteSpace(record.Phone)) continue;

                    var customerDto = new CustomerDM
                    {
                        MasterAccountID = "string",
                        VisibleToBranch = "ALL",
                        AccountName = record.AccountName?.Trim() ?? "",
                        Phone = record.Phone?.Trim() ?? "",
                        Email = record.Email?.Trim() ?? "",
                        NRIC = record.NRIC?.Trim() ?? "",
                        Gender = record.Gender ?? "",
                        BirthdayDay = record.BirthdayDay ?? 0,
                        BirthdayMonth = record.BirthdayMonth ?? 0,
                        BirthdayYear = record.BirthdayYear ?? 0,
                        RaceName = record.RaceName ?? "",
                        MaritalStatus = record.MaritalStatus ?? "",
                        CustomerSourceName = record.CustomerSourceName ?? "",
                        MembershipTypeName = record.MembershipTypeName ?? "",
                        Comment = record.Comment ?? "",
                        Address1 = record.Address1 ?? "",
                        Address2 = record.Address2 ?? "",
                        ZipCode = record.ZipCode ?? "",
                        City = record.City ?? "",
                        CountryState = record.CountryState ?? "",
                        Country = record.Country ?? "",
                        SaveAction = EntityState.Added
                    };

                    var apiResult = await CustomerService.CreateCustomer(customerDto);
                    if (apiResult?.statusCode == 200 || apiResult?.statusCode == 201) successCount++;

                    if (i % 5 == 0)
                    {
                        UploadStatus = $"Uploading {i + 1} of {total}...";
                        StateHasChanged();
                    }
                }

                UploadStatus = $"Successfully imported {successCount} members.";
                StateHasChanged();
                await Task.Delay(2000);
                CloseUploadMemberModal();
            }
            catch (Exception ex)
            {
                ErrorMessage = $"Import failed: {ex.Message}";
            }
            finally
            {
                IsProcessing = false;
                selectedFileStream?.Dispose();
                selectedFileStream = null;
                selectedFileName = null;
                selectedFileExtension = null;
                StateHasChanged();
            }
        }

        // --- Properties & Fields for Package Redemption ---
        private Doc_CashSales? currentOrder;
        private Doc_CashSales? mobjDoc_CashSales => currentOrder;
        private bool showEditOrderItemModal = false;
        private bool showNumpadModal = false;
        private bool showAlertDialogModal = false;
        private string alertDialogTitle = "";
        private string alertDialogMessage = "";
        private DocumentLineTableDM? selectedOrderItem = null;
        private bool mblnAddingNewItemFlag = false;
        private bool isAddingNewItem = false;
        private string editDiscountPercent = "0.00";
        private string editDiscountAmount = "0.00";
        private string currentEditField = "";
        private string currentEditValue = "";
        private bool isFreshInput;
        private bool isRedeemingPackage = false;
        private string? selectedPackageAutoID = null;
        private CashSales_Series_UnconsumedItemDM? selectedPackageToRedeem = null;
        private bool isLoadingHistory = false;
        private List<CashSales_Series_UnconsumedItemDM> redemptionHistory = new();
        private bool isSalesMode = false;
        private List<StaffCommissionRow> commissionRows = new();
        private string? currentCommissionRowId;
        private bool isStaffLoading = false;
        private List<Doc_CashSales> heldOrders = new();
        private string? activeOrderId = null;
        private bool isLoadingPackages = false;
        private List<CashSales_Series_UnconsumedItemDM> customerPackages = new();
        private DateTime SchedulerLayoutAppointmentStartTime;
        private DateTime mPreSelectedStartTime;
        private DateTime mPreSelectedEndTime;
        private int mintNewDocumentLineID = 1;
        public bool mblnUseRounding { get; set; } = true;
        private List<ARAPOutstanding_MemberCreditDM> customerCreditDetails = new();
        private List<ARAPOutstandingDM> creditRedemptionHistory = new();
        private bool isLoadingCreditHistory = false;
        private string? selectedCreditArapID;
        private ARAPOutstanding_MemberCreditDM? selectedCreditToRedeem;

        public class StaffCommissionRow
        {
            public string id { get; set; } = Guid.NewGuid().ToString();
            public string StaffId { get; set; } = "";
            public string StaffName { get; set; } = "";
            public decimal Amount { get; set; } = 0;
            public string CommissionType { get; set; } = "%";
        }

        private IMembersPackageService MembersPackageService => _memberPackageService;

        private void ShowAlertDialog(string title, string message)
        {
            alertDialogTitle = title;
            alertDialogMessage = message;
            showAlertDialogModal = true;
            StateHasChanged();
        }

        private void OpenPackageModal()
        {
            if (selectedMember == null)
            {
                ShowNotification("Please select a customer first.");
                return;
            }

            _ = LoadCustomerPackageBalanceForModal();
            showVoucherModal = true;
            StateHasChanged();
        }

        private async Task LoadCustomerPackageBalanceForModal()
        {
            customerPackages.Clear();
            if (selectedMember == null || string.IsNullOrEmpty(selectedMember.MasterAccountID)) return;

            isLoadingPackages = true;
            StateHasChanged();
            try
            {
                var rawPackages = await _memberPackageService.GetPackageBalanceByCustomerIDAsync(selectedMember.MasterAccountID);
                if (rawPackages != null)
                {
                    customerPackages = rawPackages.Where(p => p.ExpiryDate >= DateTime.Today).ToList();
                }
            }
            finally
            {
                isLoadingPackages = false;
                StateHasChanged();
            }
        }

        private async Task LoadCustomerWalletDetails()
        {
            customerCreditDetails.Clear();
            if (selectedMember == null || string.IsNullOrEmpty(selectedMember.MasterAccountID)) return;

            isLoadingPackages = true;
            StateHasChanged();
            try
            {
                var allCredits = await MembersCreditService.GetCustomerCreditDetailsAsync(selectedMember.MasterAccountID);
                if (allCredits != null)
                {
                    customerCreditDetails = allCredits
                        .Where(c => c.DueDate >= DateTime.Now)
                        .OrderBy(c => c.ARAPOutstandingID)
                        .ToList();
                }
            }
            finally
            {
                isLoadingPackages = false;
                StateHasChanged();
            }
        }

        private async Task FetchCreditHistory(string arapId)
        {
            selectedCreditArapID = arapId;
            selectedCreditToRedeem = customerCreditDetails.FirstOrDefault(c => c.ARAPOutstandingID == arapId);

            isLoadingCreditHistory = true;
            creditRedemptionHistory.Clear();
            StateHasChanged();

            try
            {
                creditRedemptionHistory = await MembersCreditService.GetCreditRedemptionHistoryAsync(arapId);
            }
            finally
            {
                isLoadingCreditHistory = false;
                StateHasChanged();
            }
        }

        private void SelectPackageForRedemption(CashSales_Series_UnconsumedItemDM pkg)
        {
            selectedPackageAutoID = pkg.AutoID;
            selectedPackageToRedeem = pkg;
            _ = FetchHistory(pkg.AutoID);
        }

        private async Task FetchHistory(string autoId)
        {
            selectedPackageAutoID = autoId;
            isLoadingHistory = true;
            redemptionHistory.Clear();
            StateHasChanged();

            try
            {
                redemptionHistory = await _memberPackageService.GetRedemptionHistoryAsync(autoId);
            }
            finally
            {
                isLoadingHistory = false;
                StateHasChanged();
            }
        }

        private async Task ProceedToEditPackageRedemption()
        {
            if (selectedPackageToRedeem == null)
            {
                ShowNotification("Please select a package first.");
                return;
            }

            isRedeemingPackage = true;
            StateHasChanged();
            await ApplyPackageRedemption();
        }

        private async Task ApplyPackageRedemption()
        {
            if (selectedPackageToRedeem == null)
            {
                ShowNotification("Please select a package item from the list first.");
                isRedeemingPackage = false;
                StateHasChanged();
                return;
            }

            // Ensure order initialized/loaded
            if (currentOrder == null)
            {
                if (AppState.CurrentOrder != null && AppState.CurrentOrder.objDoc_CashSales.AccountID == selectedMember?.MasterAccountID)
                {
                    currentOrder = AppState.CurrentOrder;
                }
                else
                {
                    await LoadHeldBillsFromStorage();
                    var existingHeldOrder = heldOrders.FirstOrDefault(o => o.objDoc_CashSales.AccountID == selectedMember?.MasterAccountID);
                    if (existingHeldOrder != null)
                    {
                        currentOrder = existingHeldOrder;
                    }
                    else
                    {
                        currentOrder = new Doc_CashSales(52, Guid.NewGuid().ToString()); // 52 = Redemption Mode
                        currentOrder.objDoc_CashSales.AccountID = selectedMember?.MasterAccountID ?? string.Empty;
                        currentOrder.objDoc_CashSales.AccountName = selectedMember?.AccountName ?? "";
                        currentOrder.objDoc_CashSales.Phone = selectedMember?.Phone ?? "";
                        currentOrder.objDoc_CashSales.BranchID = AppState.SelectedBranchID;
                        currentOrder.objDoc_CashSales.EditBranchID = AppState.SelectedBranchID;
                        currentOrder.objDoc_CashSales.TransactionCurrencyID = AppState?.CurrentBranch?.CurrencyID ?? "MYR";
                        currentOrder.objDoc_CashSales.ExchangeRate = 1;
                    }
                }
            }

            // Set document mode to Redemption
            currentOrder.objDoc_CashSales.DocumentTypeID = 52;
            isSalesMode = false;

            decimal currentCartQty = currentOrder?.lstDocumentLine
                .Where(line => line.SaveAction != EntityState.Deleted &&
                               (line.ActivityTypeID == 2 || line.ActivityTypeID == 5) &&
                               (line.SourceDocumentLineID == selectedPackageToRedeem.DocumentLineID || line.SourceDocumentLineID == selectedPackageToRedeem.AutoID))
                .Sum(line => line.Quantity) ?? 0;

            if (currentCartQty + 1 > selectedPackageToRedeem.NetBalanceAfterUtilised)
            {
                ShowAlertDialog("Redemption Limit Reached", $"Cannot redeem. Total quantity in cart (<strong>{currentCartQty}</strong>) plus new redemption exceeds the available balance of <strong>{selectedPackageToRedeem.NetBalanceAfterUtilised}</strong>.");
                isRedeemingPackage = false;
                StateHasChanged();
                return;
            }

            try
            {
                var invItem = await InventoryService.LoadItemAsync(selectedPackageToRedeem.InventoryID);
                if (invItem != null)
                {
                    var unconsumedMock = new CashSales_Series_UnconsumedItemDM
                    {
                        AutoID = selectedPackageToRedeem.AutoID,
                        PackageID = selectedPackageToRedeem.PackageID,
                        InventoryID = selectedPackageToRedeem.InventoryID,
                        CurrentRedeemQuantity = 1,
                        UnitPrice = selectedPackageToRedeem.UnitPrice
                    };

                    await AddSelectedItemToBill_AfterCheckingforBundle(invItem, objUnconsumedItem: unconsumedMock, strOverrideSalesDescription: selectedPackageToRedeem.Description);
                    await AutoHoldSync();

                    var addedLine = mobjDoc_CashSales.lstDocumentLine.FirstOrDefault(x => x.SourceDocumentLineID == unconsumedMock.AutoID);
                    if (addedLine != null)
                    {
                        mblnAddingNewItemFlag = true;
                        OpenEditOrderItemModal(addedLine, isNew: false);
                    }
                }
                showVoucherModal = false;
                selectedPackageAutoID = null;
            }
            finally
            {
                isRedeemingPackage = false;
                StateHasChanged();
            }
        }

        private void OpenEditOrderItemModal(DocumentLineTableDM item, bool isNew = false)
        {
            selectedOrderItem = item;
            isAddingNewItem = isNew;

            editDiscountAmount = item.Discount > 0 ? item.Discount.ToString("F2", CultureInfo.InvariantCulture) : "0.00";
            editDiscountPercent = "0.00";
            editRemarks = !string.IsNullOrEmpty(item.RefCompanyName) ? item.RefCompanyName : (item.Memo ?? "");

            if (item.lstSalesCommissionByDocumentLine != null && item.lstSalesCommissionByDocumentLine.Any())
            {
                commissionRows = item.lstSalesCommissionByDocumentLine.Select(c => new StaffCommissionRow
                {
                    StaffId = c.EmployeeID,
                    StaffName = c.EmployeeCode,
                    Amount = c.AllocationAmount,
                    CommissionType = c.CommissionDetailTypeID == 1 ? "%" : "MYR"
                }).ToList();

                while (commissionRows.Count < 3)
                {
                    commissionRows.Add(new StaffCommissionRow());
                }
            }
            else
            {
                commissionRows = new List<StaffCommissionRow> { new(), new(), new() };
            }

            bool isRedemptionModeLine = (item.ActivityTypeID == 2 || item.ActivityTypeID == 5);
            if (isRedemptionModeLine && !string.IsNullOrEmpty(item.SourceDocumentLineID))
            {
                selectedPackageToRedeem = customerPackages.FirstOrDefault(p => p.DocumentLineID == item.SourceDocumentLineID || p.AutoID == item.SourceDocumentLineID);
            }

            showEditOrderItemModal = true;
            StateHasChanged();
        }

        private void CloseEditOrderItemModal()
        {
            if (mblnAddingNewItemFlag && selectedOrderItem != null && mobjDoc_CashSales != null)
            {
                mobjDoc_CashSales.lstDocumentLine.Remove(selectedOrderItem);
                _ = CalculateTotals();
            }
            mblnAddingNewItemFlag = false;
            showEditOrderItemModal = false;
            selectedOrderItem = null;
            StateHasChanged();
        }

        private void AddCommissionRow()
        {
            commissionRows.Add(new StaffCommissionRow());
        }

        private void RemoveCommissionRow(StaffCommissionRow row)
        {
            if (row != null)
            {
                commissionRows.Remove(row);
                StateHasChanged();
            }
        }

        private void OpenNumpadForCommission(StaffCommissionRow row)
        {
            currentCommissionRowId = row.id;
            OpenNumpadForField("StaffCommission", row.Amount.ToString("F2"));
        }

        private void OpenNumpadForField(string field, string currentValue)
        {
            currentEditField = field;
            if (decimal.TryParse(currentValue, out var val))
                currentEditValue = val == 0 ? "0" : val.ToString("0.##");
            else
                currentEditValue = "0";
            showNumpadModal = true;
            isFreshInput = true;
        }

        private void CloseNumpadModal()
        {
            showNumpadModal = false;
            currentEditField = "";
            currentEditValue = "";
            isFreshInput = false;
        }

        private string GetFriendlyFieldName(string field)
        {
            if (string.IsNullOrEmpty(field)) return "";
            return field switch
            {
                "DiscountAmount" => LangSvc.GetText("MemberDiscountAmount"),
                "DiscountPercent" => LangSvc.GetText("MemberDiscountPercent"),
                "StaffCommission" => LangSvc.GetText("MemberStaffCommission"),
                "PaymentAmount" => LangSvc.GetText("MemberPaymentAmount"),
                _ => field
            };
        }

        private void HandleNumpadEdit(string input)
        {
            if (isFreshInput)
            {
                isFreshInput = false;
                if (input != "⌫" && input != ".")
                {
                    currentEditValue = "0";
                }
            }

            if (input == "⌫")
            {
                currentEditValue = currentEditValue.Length > 1 ? currentEditValue[..^1] : "0";
            }
            else if (input == ".")
            {
                if (!currentEditValue.Contains("."))
                {
                    currentEditValue += ".";
                }
            }
            else if (char.IsDigit(input[0]))
            {
                if (currentEditValue == "0.00" || currentEditValue == "0")
                {
                    currentEditValue = input;
                }
                else
                {
                    currentEditValue += input;
                }
            }
        }

        private void SaveNumpadValue()
        {
            if (string.IsNullOrEmpty(currentEditValue))
            {
                CloseNumpadModal();
                return;
            }

            switch (currentEditField)
            {
                case "Price":
                    if (selectedOrderItem != null && decimal.TryParse(currentEditValue, out var newPrice))
                    {
                        selectedOrderItem.UnitPrice = newPrice;
                        decimal subtotal = selectedOrderItem.UnitPrice * selectedOrderItem.Quantity;
                        if (selectedOrderItem.Discount > subtotal)
                        {
                            selectedOrderItem.Discount = subtotal;
                        }
                    }
                    break;

                case "Quantity":
                    if (selectedOrderItem != null)
                    {
                        if (decimal.TryParse(currentEditValue, out var parsedVal))
                        {
                            bool isRedemptionLine = selectedOrderItem != null && (selectedOrderItem.ActivityTypeID == 2 || selectedOrderItem.ActivityTypeID == 5);
                            decimal maxAllowed = 999999m;
                            if (isRedemptionLine)
                            {
                                parsedVal = Math.Floor(parsedVal); // Remove decimals
                                var pkg = customerPackages.FirstOrDefault(p => p.DocumentLineID == selectedOrderItem.SourceDocumentLineID || p.AutoID == selectedOrderItem.SourceDocumentLineID);
                                if (pkg != null)
                                {
                                    decimal otherLinesQty = currentOrder?.lstDocumentLine
                                        .Where(line => line != selectedOrderItem &&
                                                       line.SaveAction != EntityState.Deleted &&
                                                       (line.ActivityTypeID == 2 || line.ActivityTypeID == 5) &&
                                                       (line.SourceDocumentLineID == selectedOrderItem.SourceDocumentLineID))
                                        .Sum(line => line.Quantity) ?? 0;

                                    maxAllowed = pkg.NetBalanceAfterUtilised - otherLinesQty;
                                    if (maxAllowed < 0) maxAllowed = 0;

                                    if (parsedVal > maxAllowed)
                                    {
                                        ShowAlertDialog("Limit Exceeded", $"Quantity cannot be more than the available balance of <strong>{maxAllowed}</strong>.");
                                        parsedVal = maxAllowed;
                                    }
                                }
                            }

                            if (parsedVal < 0.01m)
                            {
                                selectedOrderItem.Quantity = isRedemptionLine && maxAllowed == 0 ? 0 : 1;
                                ShowNotification(isRedemptionLine && maxAllowed == 0 ? "Quantity set to 0." : "Quantity set to 1.");
                            }
                            else
                            {
                                selectedOrderItem.Quantity = parsedVal;
                            }
                            decimal subtotal = selectedOrderItem.UnitPrice * selectedOrderItem.Quantity;
                            if (selectedOrderItem.Discount > subtotal)
                            {
                                selectedOrderItem.Discount = subtotal;
                            }
                        }
                    }
                    break;

                case "DiscountPercent":
                    editDiscountPercent = currentEditValue;
                    editDiscountAmount = "0.00";
                    break;

                case "DiscountAmount":
                    editDiscountAmount = currentEditValue;
                    editDiscountPercent = "0.00";
                    break;

                case "StaffCommission":
                    if (decimal.TryParse(currentEditValue, out var commAmt))
                    {
                        var row = commissionRows.FirstOrDefault(r => r.id == currentCommissionRowId);
                        if (row != null) row.Amount = commAmt;
                    }
                    break;
            }

            CloseNumpadModal();
            StateHasChanged();
        }

        private async Task SaveOrderItemChanges()
        {
            if (selectedOrderItem != null)
            {
                selectedOrderItem.RefCompanyName = editRemarks;
                selectedOrderItem.Memo = editRemarks;
                selectedOrderItem.Discount = ComputeDiscountForSelectedLine();

                selectedOrderItem.lstSalesCommissionByDocumentLine.Clear();
                foreach (var row in commissionRows.Where(r => !string.IsNullOrEmpty(r.StaffId)))
                {
                    var newCommissionLine = new SalesCommission_ByDocumentLineDM
                    {
                        DocumentID = selectedOrderItem.DocumentID,
                        DocumentLineID = selectedOrderItem.DocumentLineID,
                        EmployeeID = row.StaffId,
                        EmployeeCode = staffList.FirstOrDefault(s => s.MasterAccountID == row.StaffId)?.AccountName ?? row.StaffName,
                        AllocationAmount = row.Amount,
                        CommissionDetailTypeID = row.CommissionType == "%" ? 1 : 2,
                        SaveAction = isAddingNewItem ? EntityState.Added : EntityState.Changed
                    };
                    selectedOrderItem.lstSalesCommissionByDocumentLine.Add(newCommissionLine);
                }

                if (isAddingNewItem && mobjDoc_CashSales != null)
                {
                    mobjDoc_CashSales.lstDocumentLine.Insert(0, selectedOrderItem);
                }

                if (mobjDoc_CashSales != null)
                {
                    mobjDoc_CashSales.Recalculate();
                    await CalculateTotals();
                    await AutoHoldSync();
                }
            }

            mblnAddingNewItemFlag = false;
            isAddingNewItem = false;
            CloseEditOrderItemModal();

            if (currentOrder != null && currentOrder.lstDocumentLine.Count > 0)
            {
                AppState.CurrentOrder = currentOrder;
                await AutoHoldSync();
                Nav.NavigateTo("/orders");
            }
        }

        private decimal ComputeDiscountForSelectedLine()
        {
            if (selectedOrderItem == null) return 0;
            decimal subtotal = selectedOrderItem.UnitPrice * selectedOrderItem.Quantity;
            if (subtotal <= 0) return 0;

            decimal discountAmount = SafeParseDecimal(editDiscountAmount);
            decimal discountPercent = SafeParseDecimal(editDiscountPercent);

            if (discountAmount > 0) return Math.Min(discountAmount, subtotal);
            if (discountPercent > 0) return Math.Min(Math.Round(subtotal * (discountPercent / 100m), 2, MidpointRounding.AwayFromZero), subtotal);
            return 0;
        }

        private decimal CalculateItemTotal()
        {
            if (selectedOrderItem == null) return 0;
            decimal subtotal = selectedOrderItem.UnitPrice * selectedOrderItem.Quantity;
            return Math.Max(0, subtotal - ComputeDiscountForSelectedLine());
        }

        private decimal SafeParseDecimal(string input)
        {
            if (string.IsNullOrWhiteSpace(input)) return 0;
            string cleanInput = input.Replace(",", ".").Trim();
            if (decimal.TryParse(cleanInput, NumberStyles.Any, CultureInfo.InvariantCulture, out var val))
            {
                return val;
            }
            return 0;
        }

        private async Task LoadHeldBillsFromStorage()
        {
            try
            {
                var json = await JS.InvokeAsync<string>("localStorage.getItem", "held_bills_cache");
                if (!string.IsNullOrEmpty(json))
                {
                    heldOrders = JsonSerializer.Deserialize<List<Doc_CashSales>>(json) ?? new List<Doc_CashSales>();
                }
            }
            catch (Exception ex) when (ex is JSDisconnectedException || ex is TaskCanceledException) { }
            catch (Exception ex) { Console.WriteLine($"Error loading held bills: {ex.Message}"); }
        }

        private async Task SaveHeldBillsToStorage()
        {
            try
            {
                var json = JsonSerializer.Serialize(heldOrders);
                await JS.InvokeVoidAsync("localStorage.setItem", "held_bills_cache", json);
            }
            catch (Exception ex) when (ex is JSDisconnectedException || ex is TaskCanceledException) { }
        }

        private async Task AutoHoldSync()
        {
            try
            {
                if (currentOrder == null || !currentOrder.lstDocumentLine.Any()) return;

                var index = heldOrders.FindIndex(o => o.objDoc_CashSales.DocumentID == currentOrder.objDoc_CashSales.DocumentID);
                if (index != -1)
                    heldOrders[index] = currentOrder;
                else
                    heldOrders.Add(currentOrder);

                await SaveHeldBillsToStorage();
                await JS.InvokeVoidAsync("localStorage.setItem", "active_order_id", currentOrder.objDoc_CashSales.DocumentID);
            }
            catch (Exception ex) when (ex is JSDisconnectedException || ex is TaskCanceledException) { }
        }

        // --- Copied Redemption and Calculation logic from Orders.razor.cs ---

        private async Task AddSelectedItemToBill_AfterCheckingforBundle(InventoryDM objSelectedInventory, Inventory_SKUDM? objSelectedSKU = null, string strEmployeeID = "",
                                                                            string strSalesPersonCode = "", CashSales_Series_UnconsumedItemDM? objUnconsumedItem = null, DateTime? dtStartTime = null,
                                                                            DateTime? dtEndTime = null, string strSeatNo = "", decimal dclOverrideUnitPrice = 0, decimal dclOverrideQuantity = 1,
                                                                            string strOverrideCustomerName = "", bool blnShowCourseOrServiceSelection = true, string strOverrideSalesDescription = "",
                                                                            string strBatchNo = "", string strSerialNo = "", string strMatrix = "")
        {
            DocumentLineTableDM? drow = null;
            decimal dclQuantity = dclOverrideQuantity;
            decimal dclDefaultUnitPrice = objSelectedInventory.SalesPrice;

            if (mobjDoc_CashSales == null)
                return;

            if (objSelectedInventory.AvailableDateFrom > mobjDoc_CashSales.objDoc_CashSales.FinancialDate.Date || objSelectedInventory.AvailableDateTo < mobjDoc_CashSales.objDoc_CashSales.FinancialDate.Date)
            {
                ShowNotification($"Item is only available for order between Date: {objSelectedInventory.AvailableDateFrom:yyyy-MM-dd} and {objSelectedInventory.AvailableDateTo:yyyy-MM-dd}.");
                return;
            }

            if (objSelectedInventory.AvailableTimeFrom > DateTime.Now.TimeOfDay || objSelectedInventory.AvailableTimeTo < DateTime.Now.TimeOfDay)
            {
                ShowNotification($"Item is only available for order between Time: {objSelectedInventory.AvailableTimeFrom:HH:mm} and {objSelectedInventory.AvailableTimeTo:HH:mm}.");
                return;
            }

            if (mobjDoc_CashSales.objDoc_CashSales.DocumentTypeID == 52 && objSelectedInventory.InventoryTypeID == 7)
            {
                ShowNotification("Unable to add [Member Credit] item in [Redemption Mode]. Please switch to [Sales Mode] to add this item.");
                return;
            }

            if (mobjDoc_CashSales.objDoc_CashSales.DocumentTypeID == 52 && objSelectedInventory.InventoryTypeID == 5)
            {
                ShowNotification("Unable to add [Package] item in [Redemption Mode]. Please switch to [Sales Mode] to add this item.");
                return;
            }

            if (objUnconsumedItem != null && mobjDoc_CashSales.objDoc_CashSales.DocumentTypeID == 52)
            {
                dclQuantity = objUnconsumedItem.CurrentRedeemQuantity;
            }

            drow = new DocumentLineTableDM();
            drow.LineOrder = mobjDoc_CashSales.lstDocumentLine.Count + 1;
            drow.LineItemID = objSelectedInventory.MasterAccountID;
            if (objSelectedInventory.InventoryTypeID == 1)
            {
                drow.InventoryItemAccountID = string.IsNullOrEmpty(objSelectedInventory.StockDeductionSKUID) ? objSelectedInventory.MasterAccountID : objSelectedInventory.StockDeductionSKUID;
            }
            drow.LineItemDisplayCode = objSelectedInventory.DisplayCode;
            drow.Description = string.IsNullOrEmpty(strOverrideSalesDescription) ? objSelectedInventory.SalesDescription : strOverrideSalesDescription;
            drow.eInvoiceClassificationCode = objSelectedInventory.eInvoiceClassificationCode;
            drow.SerialNo = strSerialNo;
            drow.SeatNo = mobjDoc_CashSales.objDoc_CashSales.SeatNo;
            drow.TriggerWholeBillNoCommission = objSelectedInventory.TriggerWholeBillNoCommission;

            if (string.IsNullOrEmpty(strBatchNo) == false)
            {
                drow.BatchNo = strBatchNo;
            }

            drow.FinancialAccountID = AppState?.objDefaultAccountDM?.DefaultCashSalesFinancialAccountID;
            drow.BranchID = mobjDoc_CashSales.objDoc_CashSales.BranchID;
            drow.EditBranchID = mobjDoc_CashSales.objDoc_CashSales.EditBranchID;
            drow.ExchangeRate = mobjDoc_CashSales.objDoc_CashSales.ExchangeRate;
            drow.CurrencyID = mobjDoc_CashSales.objDoc_CashSales.TransactionCurrencyID;
            drow.DocumentID = mobjDoc_CashSales.objDoc_CashSales.DocumentID;
            drow.DocumentLineTypeID = 1;
            drow.OwnerDocumentTypeID = mobjDoc_CashSales.objDoc_CashSales.DocumentTypeID;
            if (mobjDoc_CashSales.objDoc_CashSales.DocumentTypeID == 5 && mobjDoc_CashSales.objDoc_CashSales.FinancialDate >= AppState?.CurrentBranch?.GSTStartDate)
            {
                drow.GSTTaxPointDate = mobjDoc_CashSales.objDoc_CashSales.FinancialDate;
                if (selectedMember != null && string.IsNullOrEmpty(selectedMember.MasterAccountID) == false && string.IsNullOrEmpty(selectedMember.TaxCodeID) == false && AppState.lstGSTTaxCode.ContainsKey(selectedMember.TaxCodeID))
                {
                    drow.TaxCodeID = selectedMember.TaxCodeID;
                }
                else
                {
                    if (string.IsNullOrEmpty(objSelectedInventory.TaxCodeID) == false && AppState.lstGSTTaxCode.ContainsKey(objSelectedInventory.TaxCodeID))
                    {
                        drow.TaxCodeID = objSelectedInventory.TaxCodeID;
                    }
                    else
                    {
                        drow.TaxCodeID = AppState.CurrentBranch.DefaultSalesTaxCodeID;
                    }
                }

                if (string.IsNullOrEmpty(drow.TaxCodeID) == false && AppState.lstGSTTaxCode.ContainsKey(drow.TaxCodeID))
                {
                    drow.GSTTypeID = AppState.lstGSTTaxCode[drow.TaxCodeID].TaxTypeID;
                    drow.TaxPercentage = AppState.lstGSTTaxCode[drow.TaxCodeID].TaxRate;
                    drow.TaxFinancialAccountID = AppState.lstGSTTaxCode[drow.TaxCodeID].FinancialAccountID;
                }
                drow.IsTaxInclusive = objSelectedInventory.IsTaxInclusive;
            }
            else
            {
                drow.GSTTypeID = "";
                drow.TaxCodeID = "";
                drow.TaxPercentage = 0;
            }
            drow.ItemTaxGroupID = objSelectedInventory.ItemTaxGroupID;
            drow.Quantity = dclQuantity;

            if (mobjDoc_CashSales.objDoc_CashSales.DocumentTypeID == 5)
                drow.ActivityTypeID = 1;
            else
                drow.ActivityTypeID = 6;

            if (string.IsNullOrEmpty(strSeatNo))
                drow.SeatNo = mobjDoc_CashSales.objDoc_CashSales.SeatNo;
            else
                drow.SeatNo = strSeatNo;

            drow.SKUName = objSelectedInventory.UnitOfMeasureID;

            if (objUnconsumedItem != null && mobjDoc_CashSales.objDoc_CashSales.DocumentTypeID == 52)
            {
                drow.UnitPrice = objUnconsumedItem.UnitPrice;
                drow.OriginalKitPrice = objUnconsumedItem.SourceUnitPrice;
                drow.SourceDocumentLineID = objUnconsumedItem.AutoID;
                drow.UnitActualValue = objUnconsumedItem.UnitActualValue;
                drow.SKUName = "";
                drow.SKUQuantity = 1;
                drow.KitMemberID = objUnconsumedItem.PackageID;
                if (objUnconsumedItem.ActivityTypeID == 1)
                    drow.ActivityTypeID = 2;
                else if (objUnconsumedItem.ActivityTypeID == 4)
                    drow.ActivityTypeID = 5;
                else
                    drow.ActivityTypeID = 2;
            }
            else
            {
                if (objSelectedSKU != null)
                {
                    drow.SKUName = objSelectedSKU.SKUName;
                    drow.SKUQuantity = (objSelectedSKU.SKUQuantity == 0 ? 1 : objSelectedSKU.SKUQuantity) / (objSelectedInventory.UOMBase == 0 ? 1 : objSelectedInventory.UOMBase);
                    drow.UnitPrice = await GetPriceAfterPriceGroup(objSelectedInventory.MasterAccountID, objSelectedSKU.SalesPrice);
                }
                else
                {
                    drow.SKUName = objSelectedInventory.UnitOfMeasureID;
                    drow.SKUQuantity = 1;

                    if (objSelectedInventory.InventoryTypeID != 9)
                    {
                        if (string.IsNullOrWhiteSpace(objSelectedInventory.HHTimePrice) == false)
                        {
                            objSelectedInventory.PopulateHappyHourCollection();
                            if (objSelectedInventory.lstHappyHour.Any(x => x.HHDateFrom <= DateTime.Now.Date && x.HHDateTo >= DateTime.Now.Date && x.HHWeekDays.Split("_").Contains(DateTime.Now.ToString("dddd")) && x.HHTimeFrom <= DateTime.Now.TimeOfDay && x.HHTimeTo >= DateTime.Now.TimeOfDay))
                            {
                                Inventory_HappyHourDM objHH = objSelectedInventory.lstHappyHour.First(x => x.HHDateFrom <= DateTime.Now.Date && x.HHDateTo >= DateTime.Now.Date && x.HHWeekDays.Split("_").Contains(DateTime.Now.ToString("dddd")) && x.HHTimeFrom <= DateTime.Now.TimeOfDay && x.HHTimeTo >= DateTime.Now.TimeOfDay);
                                if (objSelectedInventory.lstPriceGroup.Count > 0 && selectedMember != null && string.IsNullOrWhiteSpace(selectedMember?.PriceGroupID) == false && objSelectedInventory.lstPriceGroup.Any(x => x.Key == selectedMember?.PriceGroupID && (x.Value.PriceFactor > 0 | x.Value.FixedPrice > 0)))
                                {
                                    decimal dclHappyHourPrice = 0;
                                    decimal dclPriceGroupPrice = 0;

                                    if (objHH.HHDiscountPercentage > 0)
                                        dclHappyHourPrice = Math.Round(dclDefaultUnitPrice * (1 - objHH.HHDiscountPercentage), 2, MidpointRounding.AwayFromZero);
                                    else
                                        dclHappyHourPrice = objHH.HHFixedPrice;

                                    dclPriceGroupPrice = await GetPriceAfterPriceGroup(objSelectedInventory.MasterAccountID, objSelectedSKU.SalesPrice);

                                    if (dclHappyHourPrice < dclPriceGroupPrice)
                                    {
                                        drow.UnitPrice = dclHappyHourPrice;
                                        drow.Description = $"(HH) {objSelectedInventory.SalesDescription}";
                                    }
                                    else
                                    {
                                        drow.UnitPrice = dclPriceGroupPrice;
                                        drow.Description = objSelectedInventory.SalesDescription;
                                    }
                                }
                                else
                                {
                                    if (objHH.HHDiscountPercentage > 0)
                                    {
                                        drow.UnitPrice = Math.Round(dclDefaultUnitPrice * (1 - objHH.HHDiscountPercentage), 2, MidpointRounding.AwayFromZero);
                                        drow.Description = $"(HH) {objSelectedInventory.SalesDescription}";
                                    }
                                    else
                                    {
                                        drow.UnitPrice = objHH.HHFixedPrice;
                                        drow.Description = $"(HH) {objSelectedInventory.SalesDescription}";
                                    }
                                }
                            }
                            else
                            {
                                drow.UnitPrice = await GetPriceAfterPriceGroup(objSelectedInventory.MasterAccountID, dclDefaultUnitPrice);
                            }
                        }
                        else
                        {
                            if (dclOverrideUnitPrice != 0)
                                drow.UnitPrice = dclOverrideUnitPrice;
                            else
                                drow.UnitPrice = await GetPriceAfterPriceGroup(objSelectedInventory.MasterAccountID, dclDefaultUnitPrice);
                        }
                    }
                }
            }
            drow.InventoryTypeID = objSelectedInventory.InventoryTypeID;
            drow.Discount = 0;

            if (AppState?.objDefaultAccountDM?.IsServiceChargeEnabled == true)
            {
                drow.DiningType = "Dine-In";
            }

            if (objSelectedInventory.ServiceMinutes > 0)
            {
                drow.ServiceMinutes = (int)(dclQuantity * objSelectedInventory.ServiceMinutes);
                drow.ServiceBufferMinutes = objSelectedInventory.BufferMinutes;
                if (SchedulerLayoutAppointmentStartTime.Year > 2020)
                {
                    drow.ServiceTimeFrom = SchedulerLayoutAppointmentStartTime;
                }
                else
                {
                    var dtWorkingHourWithoutSeconds = new DateTime(DateTime.Now.Year, DateTime.Now.Month, DateTime.Now.Day, DateTime.Now.Hour, DateTime.Now.Minute, 0);
                    drow.ServiceTimeFrom = GetRoundToFiveMinutesTime(dtWorkingHourWithoutSeconds).AddMinutes(objSelectedInventory.BufferMinutes);
                }
                drow.ServiceTimeTo = drow.ServiceTimeFrom.AddMinutes(objSelectedInventory.ServiceMinutes);
            }
            if (mobjDoc_CashSales.objDoc_CashSales.DocumentTypeID == 5)
            {
                if (objSelectedInventory.MasterAccountID == AppState?.objDefaultAccountDM?.DirectCashTopUpDefaultInventoryID)
                    drow.CashTopUpCredit = drow.UnitPrice;
            }
            drow.ClassID = strEmployeeID;
            drow.ClassName = strSalesPersonCode;

            if (objSelectedInventory.IsMedication)
            {
                drow.IsMedication = objSelectedInventory.IsMedication;
                drow.objDoctorPrescriptionDM.Dosage = objSelectedInventory.DefaultDosage;
                drow.objDoctorPrescriptionDM.DrugDosageUnit = objSelectedInventory.DrugDosageUnitName;
                drow.objDoctorPrescriptionDM.DrugFrequencyName = objSelectedInventory.DrugFrequencyName;
                drow.objDoctorPrescriptionDM.DrugDurationName = objSelectedInventory.DrugDurationName;
                drow.objDoctorPrescriptionDM.DrugPurposeName = objSelectedInventory.DrugReasonName;
                drow.objDoctorPrescriptionDM.DrugInstructionName = objSelectedInventory.DrugInstructionName;
                drow.objDoctorPrescriptionDM.DrugNotes = objSelectedInventory.DrugNotes;
            }

            if (AppState?.objDefaultAccountDM?.CombineIdenticalItem == true && (drow.InventoryTypeID == 1 || drow.InventoryTypeID == 3))
            {
                if (mobjDoc_CashSales.lstDocumentLine.Any(x => x.LineItemID == drow.LineItemID && x.SKUQuantity == drow.SKUQuantity && x.Memo == drow.Memo && x.Matrix == drow.Matrix && x.ClassID == drow.ClassID && x.UnitPrice == drow.UnitPrice))
                {
                    var SelectedDocumentLine = mobjDoc_CashSales.lstDocumentLine.First(x => x.LineItemID == drow.LineItemID && x.SKUQuantity == drow.SKUQuantity && x.Memo == drow.Memo && x.Matrix == drow.Matrix && x.ClassID == drow.ClassID && x.UnitPrice == drow.UnitPrice);
                    SelectedDocumentLine.Quantity += drow.Quantity;
                    await UpdateLineAfterQuantityChanged(SelectedDocumentLine);
                }
                else
                {
                    mobjDoc_CashSales.lstDocumentLine.Insert(0, drow);
                }
            }
            else if (drow.InventoryTypeID == (int)EnumInventoryType.Package)
            {
                var objInventoryDM = AppState?.lstAllSalesItems.ContainsKey(drow.LineItemID) == true ? AppState.lstAllSalesItems[drow.LineItemID] : null;
                if (objInventoryDM != null)
                {
                    objInventoryDM.lstMembershipCredit = ParseMembershipCredit(objInventoryDM.MembershipCredit);
                    if (objInventoryDM.lstPackage.Count > 0)
                    {
                        await AddUnconsumedServicesToRow(drow, mobjDoc_CashSales.objDoc_CashSales, objInventoryDM);
                    }
                    if (objInventoryDM.lstMembershipCredit.Count > 0)
                    {
                        await AddMemberCreditToRow(drow, mobjDoc_CashSales.objDoc_CashSales, objInventoryDM);
                    }
                }
            }
            else if (drow.InventoryTypeID == (int)EnumInventoryType.TopUp)
            {
                var objInventoryDM = AppState?.lstAllSalesItems.ContainsKey(drow.LineItemID) == true ? AppState.lstAllSalesItems[drow.LineItemID] : null;
                if (objInventoryDM != null)
                {
                    objInventoryDM.lstMembershipCredit = ParseMembershipCredit(objInventoryDM.MembershipCredit);
                }
                if (objInventoryDM != null && objInventoryDM.lstMembershipCredit.Count > 0)
                {
                    await AddMemberCreditToRow(drow, mobjDoc_CashSales.objDoc_CashSales, objInventoryDM);
                }
            }
            else
            {
                mobjDoc_CashSales.lstDocumentLine.Insert(0, drow);
            }

            await CalculateTotals();
        }

        private async Task UpdateLineAfterQuantityChanged(DocumentLineTableDM SelectedDocumentLine)
        {
            if (AppState?.lstAllSalesItems.ContainsKey(SelectedDocumentLine.LineItemID) == true)
            {
                SelectedDocumentLine.ServiceMinutes = (int)SelectedDocumentLine.Quantity * AppState.lstAllSalesItems[SelectedDocumentLine.LineItemID].ServiceMinutes;
                SelectedDocumentLine.ServiceBufferMinutes = AppState.lstAllSalesItems[SelectedDocumentLine.LineItemID].ServiceMinutes;
                if (SelectedDocumentLine.ServiceBufferMinutes > 0 && SelectedDocumentLine.ServiceTimeFrom == DateTime.MinValue)
                {
                    SelectedDocumentLine.ServiceTimeFrom = GetRoundToFiveMinutesTime(DateTime.Now).AddMinutes(SelectedDocumentLine.ServiceBufferMinutes);
                }
            }

            if (AppState?.lstAllSalesItems.ContainsKey(SelectedDocumentLine.LineItemID) == true)
            {
                var objInventoryDM = AppState.lstAllSalesItems[SelectedDocumentLine.LineItemID];
                SelectedDocumentLine.lstSalesCommissionByDocumentLine = await ReviseCommission(SelectedDocumentLine, objInventoryDM, SelectedDocumentLine.ClassID, SelectedDocumentLine.SalesPersonCode);
                await UpdateCustomerUnconsumedServicesUponQuantityChanged(SelectedDocumentLine);
                await UpdatePromotionDetailItems(SelectedDocumentLine, objInventoryDM);
                await CalculateMemberPrice(SelectedDocumentLine, SelectedDocumentLine.MemberCreditAccountID);
            }

            await CalculateTotals();
        }

        private async Task UpdateCustomerUnconsumedServicesUponQuantityChanged(DocumentLineTableDM SelectedDocumentLine)
        {
            int iFound = 0;

            if (SelectedDocumentLine == null) return;
            if (SelectedDocumentLine.DocumentLineTypeID != 1) return;
            if (string.IsNullOrWhiteSpace(SelectedDocumentLine.KitMemberID) == false) return;
            if (string.IsNullOrWhiteSpace(SelectedDocumentLine.LineItemID) == true) return;
            if (AppState?.lstAllSalesItems == null) return;
            if (AppState?.lstAllSalesItems.ContainsKey(SelectedDocumentLine.LineItemID) == false) return;

            InventoryDM objInventoryDM = AppState?.lstAllSalesItems[SelectedDocumentLine.LineItemID] ?? new InventoryDM();
            if (objInventoryDM.InventoryTypeID == (int)EnumInventoryType.Package || objInventoryDM.InventoryTypeID == (int)EnumInventoryType.TopUp)
            {
                objInventoryDM.lstMembershipCredit = ParseMembershipCredit(objInventoryDM.MembershipCredit);
            }

            if (objInventoryDM.InventoryTypeID == (int)EnumInventoryType.Service)
            {
                if (SelectedDocumentLine.lstCashSales_Series_UnconsumedItem.Count > 0)
                {
                    foreach (var objUnconsumedItem in SelectedDocumentLine.lstCashSales_Series_UnconsumedItem)
                    {
                        objUnconsumedItem.QuantityPurchased = SelectedDocumentLine.Quantity * SelectedDocumentLine.SKUQuantity;
                    }
                }
            }
            else if (objInventoryDM.InventoryTypeID == (int)EnumInventoryType.Package)
            {
                iFound = 0;
                foreach (var drow in SelectedDocumentLine.lstCashSales_Series_UnconsumedItem)
                {
                    foreach (var objPackageItem in objInventoryDM.lstPackage)
                    {
                        if (objPackageItem.InventoryID == drow.InventoryID && objPackageItem.AutoID == drow.PackageItemID)
                        {
                            drow.QuantityPurchased = SelectedDocumentLine.Quantity * SelectedDocumentLine.SKUQuantity * objPackageItem.Quantity;
                            drow.TotalPrice = SelectedDocumentLine.Quantity * SelectedDocumentLine.SKUQuantity * objPackageItem.Quantity * drow.UnitPrice;
                            drow.TotalActualValue = SelectedDocumentLine.Quantity * SelectedDocumentLine.SKUQuantity * objPackageItem.Quantity * drow.UnitActualValue;
                            iFound += 1;
                        }
                    }
                }

                if (iFound == 0)
                {
                    await AddUnconsumedServicesToRow(SelectedDocumentLine, mobjDoc_CashSales.objDoc_CashSales, objInventoryDM);
                }

                iFound = 0;
                foreach (var drow in SelectedDocumentLine.lstARAPOutstanding_MemberCredit)
                {
                    foreach (var objPackageItem in objInventoryDM.lstMembershipCredit)
                    {
                        if (objPackageItem.MemberTypeID == drow.MemberTypeID)
                        {
                            drow.TotalAmount = SelectedDocumentLine.Quantity * objPackageItem.MemberCredit;
                            drow.InterOutletAmount = SelectedDocumentLine.Quantity * objPackageItem.MemberCredit * objInventoryDM.MemberCreditSettlementRatio;
                            iFound += 1;
                        }
                    }
                }

                if (iFound == 0)
                {
                    await AddMemberCreditToRow(SelectedDocumentLine, mobjDoc_CashSales.objDoc_CashSales, objInventoryDM);
                }
            }
            else if (objInventoryDM.InventoryTypeID == (int)EnumInventoryType.TopUp)
            {
                iFound = 0;
                foreach (var drow in SelectedDocumentLine.lstARAPOutstanding_MemberCredit)
                {
                    foreach (var objPackageItem in objInventoryDM.lstMembershipCredit)
                    {
                        if (objPackageItem.MemberTypeID == drow.MemberTypeID)
                        {
                            drow.TotalAmount = SelectedDocumentLine.Quantity * objPackageItem.MemberCredit;
                            drow.InterOutletAmount = SelectedDocumentLine.Quantity * objPackageItem.MemberCredit * objInventoryDM.MemberCreditSettlementRatio;
                            iFound += 1;
                        }
                    }
                }

                if (iFound == 0)
                {
                    await AddMemberCreditToRow(SelectedDocumentLine, mobjDoc_CashSales.objDoc_CashSales, objInventoryDM);
                }
            }
        }

        private async Task CalculateMemberPrice(DocumentLineTableDM DocLine, string strMembershipCreditAccountID)
        {
            decimal dclMemberDiscount = 0;
            MembershipType objMemberType;
            InventoryDM? objSelectedInventory;

            if (selectedMember != null && string.IsNullOrWhiteSpace(selectedMember.MembershipTypeID) == false && (selectedMember.MembershipValidFrom == DateTime.MinValue | selectedMember.MembershipValidFrom <= DateTime.Now.Date) && (selectedMember.MembershipValidTo == DateTime.MinValue | selectedMember.MembershipValidTo >= DateTime.Now.Date))
            {
                objMemberType = await MembershipTypeService.LoadRecord(selectedMember.MembershipTypeID ?? "-----");

                if (DateTime.Now.TimeOfDay >= objMemberType.objMembershipType.DiscountTimeFrom && DateTime.Now.TimeOfDay <= objMemberType.objMembershipType.DiscountTimeTo)
                {
                    objSelectedInventory = AppState?.lstAllSalesItems.ContainsKey(DocLine.LineItemID) == true ? AppState.lstAllSalesItems[DocLine.LineItemID] : null;
                    if (objSelectedInventory != null)
                    {
                        switch (objSelectedInventory.InventoryTypeID)
                        {
                            case (int)EnumInventoryType.Inventory:
                                dclMemberDiscount = DocLine.UnitPrice * DocLine.Quantity * objMemberType.objMembershipType.MemberDiscountOnInventory;
                                break;
                            case (int)EnumInventoryType.Service:
                                dclMemberDiscount = DocLine.UnitPrice * DocLine.Quantity * objMemberType.objMembershipType.MemberDiscountOnServices;
                                break;
                            case (int)EnumInventoryType.Bundle:
                                dclMemberDiscount = DocLine.UnitPrice * DocLine.Quantity * objMemberType.objMembershipType.MemberDiscountOnBundle;
                                break;
                            case (int)EnumInventoryType.Package:
                                dclMemberDiscount = DocLine.UnitPrice * DocLine.Quantity * objMemberType.objMembershipType.MemberDiscountOnPackage;
                                break;
                        }

                        if (objMemberType.lstMembershipTypeDiscount.Any(x => x.lstDiscountBrand.Contains(objSelectedInventory.PreferredVendorAccountID) || x.lstDiscountGroup.Contains(objSelectedInventory.ItemGroupID) || x.lstDiscountItems.Contains(objSelectedInventory.MasterAccountID)))
                        {
                            MembershipType_DiscountDM objMemberDiscount = objMemberType.lstMembershipTypeDiscount.First(x => x.lstDiscountBrand.Contains(objSelectedInventory.PreferredVendorAccountID) || x.lstDiscountGroup.Contains(objSelectedInventory.ItemGroupID) || x.lstDiscountItems.Contains(objSelectedInventory.MasterAccountID));
                            dclMemberDiscount = DocLine.UnitPrice * DocLine.Quantity * objMemberDiscount.DiscountPercentage;
                        }
                    }

                    if (dclMemberDiscount > 0 && objMemberType.objMembershipType.IsDiscountLimitToCreditRedemption == false)
                    {
                        DocLine.MemberTypeID = objMemberType.objMembershipType.MemberTypeID;
                        DocLine.MemberCreditAccountID = "";
                        DocLine.MemberDiscount = dclMemberDiscount;
                    }
                }
            }

            if (string.IsNullOrWhiteSpace(DocLine.CashDiscountID) == false && DocLine.CashDiscountID != "0")
            {
                CashDiscountDM objCashDiscountDM = await cashDiscountService.LoadRecord(DocLine.CashDiscountID);
                if (objCashDiscountDM != null && objCashDiscountDM.IsUseCost == false)
                {
                    if (objCashDiscountDM.CashDiscountTypeID == (int)EnumCashDiscountType.Percentage)
                    {
                        DocLine.Discount = Math.Round((DocLine.Quantity * DocLine.UnitPrice - DocLine.MemberDiscount) * objCashDiscountDM.CashDiscountPercentage, 2);
                    }
                }
            }
        }

        private static System.Collections.ObjectModel.ObservableCollection<Inventory_MembershipCreditDM> ParseMembershipCredit(string? membershipCreditStr)
        {
            var list = new System.Collections.ObjectModel.ObservableCollection<Inventory_MembershipCreditDM>();
            if (string.IsNullOrWhiteSpace(membershipCreditStr)) return list;
            var rows = membershipCreditStr.Split('|', StringSplitOptions.RemoveEmptyEntries);
            foreach (var row in rows)
            {
                var columns = row.Split(',');
                if (columns.Length >= 2)
                {
                    var memberTypeId = columns[0].Trim();
                    if (decimal.TryParse(columns[1], System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var creditAmount))
                    {
                        list.Add(new Inventory_MembershipCreditDM
                        {
                            MemberTypeID = memberTypeId,
                            MemberCredit = creditAmount,
                            SaveAction = EBI.Enum.EntityState.NotChanged,
                            IsDirty = true
                        });
                    }
                }
            }
            return list;
        }

        private static async Task AddMemberCreditToRow(DocumentLineTableDM DocLine, Doc_CashSalesDM mobjCashSalesDM, InventoryDM objPackageOrTopUp)
        {
            if (objPackageOrTopUp.lstMembershipCredit == null) return;
            foreach (var rowPackageItem in objPackageOrTopUp.lstMembershipCredit)
            {
                var objMemberCredit = new ARAPOutstanding_MemberCreditDM()
                {
                    AccountID = mobjCashSalesDM.AccountID,
                    FinancialDate = mobjCashSalesDM.FinancialDate,
                    DueDate = objPackageOrTopUp.MemberExpiryDays > 0 ? mobjCashSalesDM.FinancialDate.Date.AddDays(objPackageOrTopUp.MemberExpiryDays) : new DateTime(2049, 12, 31),
                    DocumentID = mobjCashSalesDM.DocumentID,
                    DisplayCode = mobjCashSalesDM.DisplayCode,
                    DocumentTypeID = mobjCashSalesDM.DocumentTypeID,
                    DocumentTypeName = mobjCashSalesDM.FriendlyDocumentName,
                    DocumentLineID = DocLine.DocumentLineID,
                    ItemDescription = $"{DocLine.Description}{(DocLine.Quantity == 1 ? "" : $"(x {DocLine.Quantity})")}",
                    CurrencyID = mobjCashSalesDM.TransactionCurrencyID,
                    CurrencyName = mobjCashSalesDM.TransactionCurrencyName,
                    ExchangeRate = mobjCashSalesDM.ExchangeRate,
                    InterOutletRatio = objPackageOrTopUp.MemberCreditSettlementRatio,
                    InterOutletAmount = rowPackageItem.MemberCredit * DocLine.Quantity * objPackageOrTopUp.MemberCreditSettlementRatio,
                    MGMTier = "",
                    TotalAmount = rowPackageItem.MemberCredit * DocLine.Quantity,
                    BranchID = mobjCashSalesDM.BranchID,
                    GroupID = mobjCashSalesDM.GroupID,
                    LineItemID = DocLine.LineItemID,
                    MemberTypeID = rowPackageItem.MemberTypeID
                };
                DocLine.lstARAPOutstanding_MemberCredit.Add(objMemberCredit);
            }
        }

        private static async Task AddUnconsumedServicesToRow(DocumentLineTableDM DocLine, Doc_CashSalesDM mobjCashSalesDM, InventoryDM objPackage)
        {
            foreach (var rowPackageItem in objPackage.lstPackage)
            {
                if (rowPackageItem.PromotionMethod != (int)EnumPromotionMethod.BundledDiscount)
                {
                    var objUnconsumedService = new CashSales_Series_UnconsumedItemDM()
                    {
                        DocumentID = mobjCashSalesDM.DocumentID,
                        CustomerAccountID = mobjCashSalesDM.AccountID,
                        PackageID = rowPackageItem.PackageID,
                        InventoryID = rowPackageItem.InventoryID,
                        Description = rowPackageItem.Description,
                        QuantityPurchased = DocLine.Quantity * DocLine.SKUQuantity * rowPackageItem.Quantity,
                        QuantityRedeemed = 0,
                        UnitPrice = rowPackageItem.UnitPrice,
                        TotalPrice = DocLine.Quantity * DocLine.SKUQuantity * rowPackageItem.TotalPrice,
                        PackageItemID = rowPackageItem.AutoID,
                        DocumentLineID = DocLine.DocumentLineID,
                        EmployeeID = DocLine.ClassID,
                        ActivityTypeID = DocLine.ActivityTypeID,
                        SourceUnitPrice = rowPackageItem.UnitPrice,
                        UnitActualValue = rowPackageItem.UnitActualValue,
                        TotalActualValue = rowPackageItem.TotalActualValue,
                        SourceUnitActualValue = rowPackageItem.UnitActualValue,
                        OptionItems = rowPackageItem.OptionItems,
                        OptionBrands = rowPackageItem.OptionBrands,
                        OptionGroups = rowPackageItem.OptionGroups,
                        ExpiryDate = objPackage.ValidityDays > 0 ? mobjCashSalesDM.FinancialDate.Date.AddDays(objPackage.ValidityDays).AddHours(23).AddMinutes(59) : new DateTime(2049, 12, 31, 23, 59, 0)
                    };

                    if (rowPackageItem.PackageQuantityTypeID == 0)
                        DocLine.lstCashSales_Series_UnconsumedItem.Add(objUnconsumedService);
                    else if (rowPackageItem.PackageQuantityTypeID == 1)
                        DocLine.lstCashSales_UnconsumedTime.Add(objUnconsumedService);
                }
            }
        }

        private async Task UpdatePromotionDetailItems(DocumentLineTableDM Docline, InventoryDM objInventoryDM)
        {
            if (Docline.InventoryTypeID == 9 && objInventoryDM != null && Docline.lstPackageItems.Count > 0)
            {
                foreach (var drow in Docline.lstPackageItems)
                {
                    if (drow.SaveAction != EntityState.Deleted)
                    {
                        foreach (var drowPackageItems in objInventoryDM.lstPackage)
                        {
                            if (drowPackageItems.AutoID == drow.PromotionDetailID)
                            {
                                drow.Quantity = Docline.Quantity * drowPackageItems.Quantity;
                            }
                        }
                    }
                }
            }
        }

        private async Task<ObservableCollection<SalesCommission_ByDocumentLineDM>> ReviseCommission(DocumentLineTableDM objDocline, InventoryDM objSelectedInventory, string strEmployeeID, string strSalesPersonCode)
        {
            int i;
            SalesCommission_ByDocumentLineDM objNewComm;
            SalesCommission_ByDocumentLineDM objNewCommLine;

            if (AppState?.objDefaultAccountDM?.IsKeepCommissionRecords == true)
            {
                ObservableCollection<SalesCommission_ByDocumentLineDM> lstTemp = new();
                lstTemp = await ComputeCommission(objDocline, objSelectedInventory, strEmployeeID, strSalesPersonCode, "");

                if (lstTemp.Count > 0)
                {
                    if (objDocline.lstSalesCommissionByDocumentLine.Count > 0)
                    {
                        for (i = objDocline.lstSalesCommissionByDocumentLine.Count - 1; i >= 0; i--)
                        {
                            if (lstTemp.Any(x => x.PresetAllocationDetailID == objDocline.lstSalesCommissionByDocumentLine[i].PresetAllocationDetailID && x.PromotionDetailAutoID == objDocline.lstSalesCommissionByDocumentLine[i].PromotionDetailAutoID))
                            {
                                objNewComm = lstTemp.First(x => x.PresetAllocationDetailID == objDocline.lstSalesCommissionByDocumentLine[i].PresetAllocationDetailID && x.PromotionDetailAutoID == objDocline.lstSalesCommissionByDocumentLine[i].PromotionDetailAutoID);
                                objDocline.lstSalesCommissionByDocumentLine[i].AllocatedSalesAmount = objNewComm.AllocatedSalesAmount;
                                objDocline.lstSalesCommissionByDocumentLine[i].CommissionDetailTypeID = objNewComm.CommissionDetailTypeID;
                                objDocline.lstSalesCommissionByDocumentLine[i].PresetAllocationDetailID = objNewComm.PresetAllocationDetailID;
                                objDocline.lstSalesCommissionByDocumentLine[i].CommissionSharingPercentage = objNewComm.CommissionSharingPercentage;
                                objDocline.lstSalesCommissionByDocumentLine[i].Notes = objNewComm.Notes;
                                objDocline.lstSalesCommissionByDocumentLine[i].IsAskFor = objNewComm.IsAskFor;
                                objDocline.lstSalesCommissionByDocumentLine[i].IsRepairJob = objNewComm.IsRepairJob;
                                objDocline.lstSalesCommissionByDocumentLine[i].AllocationTypeID = objNewComm.AllocationTypeID;
                                objDocline.lstSalesCommissionByDocumentLine[i].AllocationAmount = objNewComm.AllocationAmount;
                                objDocline.lstSalesCommissionByDocumentLine[i].IsBalance = objNewComm.IsBalance;
                                objDocline.lstSalesCommissionByDocumentLine[i].DiscountID = objNewComm.DiscountID;
                                objDocline.lstSalesCommissionByDocumentLine[i].DiscountAmount = objNewComm.DiscountAmount;
                                objDocline.lstSalesCommissionByDocumentLine[i].DiscountCommissionEntitlementTypeID = objNewComm.DiscountCommissionEntitlementTypeID;
                                objDocline.lstSalesCommissionByDocumentLine[i].DiscountCommissionEntitlement = objNewComm.DiscountCommissionEntitlement;
                                objDocline.lstSalesCommissionByDocumentLine[i].RepairJobCommissionDeduction = objNewComm.RepairJobCommissionDeduction;
                                objDocline.lstSalesCommissionByDocumentLine[i].DefaultXRange = objNewComm.DefaultXRange;
                                objDocline.lstSalesCommissionByDocumentLine[i].OriginalAmount = objNewComm.OriginalAmount;
                                objDocline.lstSalesCommissionByDocumentLine[i].CommissionAutoAllocationGroupID = objNewComm.CommissionAutoAllocationGroupID;
                                objDocline.lstSalesCommissionByDocumentLine[i].InventoryID = objNewComm.InventoryID;
                                objDocline.lstSalesCommissionByDocumentLine[i].ActivityTypeID = objNewComm.ActivityTypeID;
                                objDocline.lstSalesCommissionByDocumentLine[i].SalesValue = objNewComm.SalesValue;
                                objDocline.lstSalesCommissionByDocumentLine[i].PromotionDetailAutoID = objNewComm.PromotionDetailAutoID;
                                objDocline.lstSalesCommissionByDocumentLine[i].IsNotForEmployee = objNewComm.IsNotForEmployee;
                                objDocline.lstSalesCommissionByDocumentLine[i].LineQuantity = objNewComm.LineQuantity;
                            }
                            else
                            {
                                objDocline.lstSalesCommissionByDocumentLine.Remove(objDocline.lstSalesCommissionByDocumentLine[i]);
                            }
                        }

                        foreach (var obj in lstTemp)
                        {
                            if (objDocline.lstSalesCommissionByDocumentLine.Any(x => x.PresetAllocationDetailID == obj.PresetAllocationDetailID && x.PromotionDetailAutoID == obj.PromotionDetailAutoID) == false)
                            {
                                objNewCommLine = new SalesCommission_ByDocumentLineDM()
                                {
                                    DocumentID = objDocline.DocumentID,
                                    DocumentLineID = objDocline.DocumentLineID,
                                    AllocatedSalesAmount = obj.AllocatedSalesAmount,
                                    CommissionDetailTypeID = obj.CommissionDetailTypeID,
                                    PresetAllocationDetailID = obj.PresetAllocationDetailID,
                                    CommissionSharingPercentage = obj.CommissionSharingPercentage,
                                    Notes = obj.Notes,
                                    IsAskFor = obj.IsAskFor,
                                    IsRepairJob = obj.IsRepairJob,
                                    AllocationTypeID = obj.AllocationTypeID,
                                    AllocationAmount = obj.AllocationAmount,
                                    IsBalance = obj.IsBalance,
                                    DiscountID = obj.DiscountID,
                                    DiscountAmount = obj.DiscountAmount,
                                    DiscountCommissionEntitlementTypeID = obj.DiscountCommissionEntitlementTypeID,
                                    DiscountCommissionEntitlement = obj.DiscountCommissionEntitlement,
                                    RepairJobCommissionDeduction = obj.RepairJobCommissionDeduction,
                                    BranchID = obj.BranchID,
                                    EmployeeCode = obj.EmployeeCode,
                                    DefaultXRange = obj.DefaultXRange,
                                    OriginalAmount = obj.OriginalAmount,
                                    CommissionAutoAllocationGroupID = obj.CommissionAutoAllocationGroupID,
                                    InventoryID = obj.InventoryID,
                                    ActivityTypeID = obj.ActivityTypeID,
                                    SalesValue = obj.SalesValue,
                                    PromotionDetailAutoID = obj.PromotionDetailAutoID,
                                    IsNotForEmployee = obj.IsNotForEmployee,
                                    LineQuantity = obj.LineQuantity
                                };
                                objDocline.lstSalesCommissionByDocumentLine.Add(objNewCommLine);
                            }
                        }
                    }
                }
                else
                {
                    objDocline.lstSalesCommissionByDocumentLine.Clear();
                }
            }

            return objDocline.lstSalesCommissionByDocumentLine;
        }

        private async Task<ObservableCollection<SalesCommission_ByDocumentLineDM>> ComputeCommission(DocumentLineTableDM Docline, InventoryDM objInventoryDM, string strEmployeeID, string strSalesPersonCode, string strPromotionDetailAutoID, string strBranchCommissionGroupID = "")
        {
            ObservableCollection<SalesCommission_ByDocumentLineDM> lst = new();
            decimal dclSalesAmount = Docline.SubTotalBeforeGST;
            SalesCommission_ByDocumentLineDM? objCommLine;

            if (string.IsNullOrEmpty(objInventoryDM.StaffCommissionACourse) == false || string.IsNullOrEmpty(objInventoryDM.StaffCommissionA) == false || string.IsNullOrEmpty(objInventoryDM.StaffCommissionB) == false || string.IsNullOrEmpty(objInventoryDM.StaffCommissionC) == false || string.IsNullOrEmpty(objInventoryDM.StaffCommissionD) == false || string.IsNullOrEmpty(objInventoryDM.CashierCommission) == false)
            {
                if (Docline.ActivityTypeID == 4)
                {
                    if (string.IsNullOrEmpty(objInventoryDM.StaffCommissionACourse) == false)
                    {
                        objCommLine = GetCommissionLineBySimpleFormula(objInventoryDM.StaffCommissionACourse, Docline.ActivityTypeID, objInventoryDM.MasterAccountID, Docline.Quantity, Docline.SKUQuantity, dclSalesAmount, strEmployeeID, strSalesPersonCode, "StaffPackage", strPromotionDetailAutoID, strBranchCommissionGroupID);
                        if (objCommLine != null) lst.Add(objCommLine);
                    }
                }
                else if (Docline.ActivityTypeID == 1 || Docline.ActivityTypeID == 2 || Docline.ActivityTypeID == 3 || Docline.ActivityTypeID == 4 || Docline.ActivityTypeID == 6)
                {
                    if (string.IsNullOrEmpty(objInventoryDM.StaffCommissionA) == false)
                    {
                        objCommLine = GetCommissionLineBySimpleFormula(objInventoryDM.StaffCommissionA, Docline.ActivityTypeID, objInventoryDM.MasterAccountID, Docline.Quantity, Docline.SKUQuantity, dclSalesAmount, strEmployeeID, strSalesPersonCode, "StaffA", strPromotionDetailAutoID, strBranchCommissionGroupID);
                        if (objCommLine != null) lst.Add(objCommLine);
                    }

                    if (string.IsNullOrEmpty(objInventoryDM.StaffCommissionB) == false)
                    {
                        objCommLine = GetCommissionLineBySimpleFormula(objInventoryDM.StaffCommissionB, Docline.ActivityTypeID, objInventoryDM.MasterAccountID, Docline.Quantity, Docline.SKUQuantity, dclSalesAmount, strEmployeeID, strSalesPersonCode, "StaffB", strPromotionDetailAutoID, strBranchCommissionGroupID);
                        if (objCommLine != null) lst.Add(objCommLine);
                    }

                    if (string.IsNullOrEmpty(objInventoryDM.StaffCommissionC) == false)
                    {
                        objCommLine = GetCommissionLineBySimpleFormula(objInventoryDM.StaffCommissionC, Docline.ActivityTypeID, objInventoryDM.MasterAccountID, Docline.Quantity, Docline.SKUQuantity, dclSalesAmount, strEmployeeID, strSalesPersonCode, "StaffC", strPromotionDetailAutoID, strBranchCommissionGroupID);
                        if (objCommLine != null) lst.Add(objCommLine);
                    }

                    if (string.IsNullOrEmpty(objInventoryDM.StaffCommissionD) == false)
                    {
                        objCommLine = GetCommissionLineBySimpleFormula(objInventoryDM.StaffCommissionD, Docline.ActivityTypeID, objInventoryDM.MasterAccountID, Docline.Quantity, Docline.SKUQuantity, dclSalesAmount, strEmployeeID, strSalesPersonCode, "StaffD", strPromotionDetailAutoID, strBranchCommissionGroupID);
                        if (objCommLine != null) lst.Add(objCommLine);
                    }
                }
            }

            return lst;
        }

        private SalesCommission_ByDocumentLineDM? GetCommissionLineBySimpleFormula(string strSimpleCommissionFormula, int intActivityTypeID, string strInventoryID, decimal dclQuantity, decimal dclSKUQuantity, decimal dclSalesAmount, string strEmployeeID, string strSalesPersonCode, string strPresetAllocationDetailID, string strPromotionDetailAutoID, string strBranchCommissionGroupID = "")
        {
            string strCommissionFormulaFromInventory;
            string strStaticCommission;
            string strCommissionAutoAllocationGroupID = "";

            if (string.IsNullOrEmpty(strSimpleCommissionFormula) == false)
            {
                List<string> lstComm = strSimpleCommissionFormula.Split('|').ToList();
                Dictionary<string, string> lstDict = new();

                foreach (var obj in lstComm)
                {
                    if (obj.Contains(":"))
                    {
                        if (lstDict.ContainsKey(obj.Split(":").ToList()[0]) == false)
                        {
                            var lstStr = obj.Split(":");
                            lstDict.Add(lstStr[0], lstStr[1]);
                        }
                    }
                    else
                    {
                        if (lstDict.ContainsKey("ALL") == false)
                        {
                            lstDict.Add("ALL", obj);
                        }
                    }
                }

                if (lstDict.ContainsKey(strBranchCommissionGroupID))
                {
                    strCommissionFormulaFromInventory = lstDict[strBranchCommissionGroupID];
                }
                else if (lstDict.ContainsKey("ALL"))
                {
                    strCommissionFormulaFromInventory = lstDict["ALL"];
                }
                else
                {
                    strCommissionFormulaFromInventory = "";
                }

                if (string.IsNullOrEmpty(strCommissionFormulaFromInventory) == false)
                {
                    strStaticCommission = strCommissionFormulaFromInventory.Replace("T", "").Replace("F", "").Replace("A", "");

                    var drowCommission = new SalesCommission_ByDocumentLineDM();
                    drowCommission.EmployeeID = strEmployeeID;
                    drowCommission.EmployeeCode = strSalesPersonCode;
                    drowCommission.ActivityTypeID = intActivityTypeID;
                    if (strCommissionFormulaFromInventory.StartsWith("T"))
                        drowCommission.CommissionDetailTypeID = 1;
                    else if (strCommissionFormulaFromInventory.StartsWith("F"))
                        drowCommission.CommissionDetailTypeID = 2;
                    else
                        drowCommission.CommissionDetailTypeID = 2;

                    if (strCommissionFormulaFromInventory.EndsWith("A") && string.IsNullOrEmpty(strCommissionAutoAllocationGroupID) == false)
                    {
                        ShowNotification("Commission based on employee level is not supported");
                    }
                    else
                    {
                        if (string.IsNullOrEmpty(strStaticCommission) == false)
                        {
                            if (strStaticCommission.EndsWith("%"))
                                drowCommission.AllocatedSalesAmount = Math.Round(dclSalesAmount * Convert.ToDecimal(strStaticCommission.Replace("%", "")) / 100, 2, MidpointRounding.AwayFromZero);
                            else
                                drowCommission.AllocatedSalesAmount = Math.Round(Convert.ToDecimal(strStaticCommission) * dclQuantity * dclSKUQuantity, 2, MidpointRounding.AwayFromZero);
                        }
                        else
                        {
                            drowCommission.AllocatedSalesAmount = dclSalesAmount;
                        }
                    }
                    drowCommission.CommissionSharingPercentage = 1;
                    drowCommission.IsAskFor = 0;
                    drowCommission.IsRepairJob = false;
                    drowCommission.CommissionAutoAllocationGroupID = "01";
                    drowCommission.PresetAllocationDetailID = strPresetAllocationDetailID;
                    drowCommission.InventoryID = strInventoryID;
                    drowCommission.PromotionDetailAutoID = strPromotionDetailAutoID;
                    drowCommission.IsNotForEmployee = false;

                    return drowCommission;
                }
                else
                {
                    return null;
                }
            }
            else
            {
                return null;
            }
        }

        private async Task CalculateTotals()
        {
            List<string> lstDiningType = new();
            decimal ServiceCharges = 0;
            DocumentLineTableDM drow;

            if (AppState?.objDefaultAccountDM?.IsServiceChargeEnabled == true && string.IsNullOrEmpty(AppState.objDefaultAccountDM.ServiceChargeAccountID) == false && string.IsNullOrEmpty(AppState.objDefaultAccountDM.ServiceChargeDiningType) == false && AppState.objDefaultAccountDM.ServiceChargePercentage > 0)
            {
                lstDiningType = new List<string>(AppState.objDefaultAccountDM.ServiceChargeDiningType.Split(","));
                ServiceCharges = Math.Round(mobjDoc_CashSales?.lstDocumentLine.Where(c => c.SaveAction != EntityState.Deleted && lstDiningType.Contains(c.DiningType) && c.InventoryTypeID != 7 && c.InventoryTypeID != 5).Sum(x => x.SubTotalBeforeGST) ?? 0, 2);

                if (mobjDoc_CashSales?.lstDocumentLine.Any(x => x.LineItemID == AppState.objDefaultAccountDM.ServiceChargeAccountID) ?? false == true)
                {
                    mobjDoc_CashSales.lstDocumentLine.First(x => x.LineItemID == AppState.objDefaultAccountDM.ServiceChargeAccountID).UnitPrice = ServiceCharges;
                }
                else
                {
                    if (ServiceCharges != 0)
                    {
                        if (AppState.objServiceChargeItem != null)
                        {
                            drow = new DocumentLineTableDM();
                            drow.DocumentLineID = GetNewDocumentLineID();
                            drow.LineOrder = mobjDoc_CashSales.lstDocumentLine.Count + 1;
                            drow.LineItemID = AppState.objServiceChargeItem.MasterAccountID;
                            drow.LineItemDisplayCode = AppState.objServiceChargeItem.DisplayCode;
                            drow.Description = AppState.objServiceChargeItem.SalesDescription;
                            drow.eInvoiceClassificationCode = AppState.objServiceChargeItem.eInvoiceClassificationCode;
                            drow.FinancialAccountID = AppState?.objDefaultAccountDM.DefaultCashSalesFinancialAccountID;
                            drow.BranchID = mobjDoc_CashSales.objDoc_CashSales.BranchID;
                            drow.EditBranchID = mobjDoc_CashSales.objDoc_CashSales.EditBranchID;
                            drow.ExchangeRate = mobjDoc_CashSales.objDoc_CashSales.ExchangeRate;
                            drow.CurrencyID = mobjDoc_CashSales.objDoc_CashSales.TransactionCurrencyID;
                            drow.DocumentID = mobjDoc_CashSales.objDoc_CashSales.DocumentID;
                            drow.DocumentLineTypeID = 1;
                            drow.OwnerDocumentTypeID = mobjDoc_CashSales.objDoc_CashSales.DocumentTypeID;

                            if (mobjDoc_CashSales.objDoc_CashSales.DocumentTypeID == 5 && mobjDoc_CashSales.objDoc_CashSales.FinancialDate >= AppState?.CurrentBranch?.GSTStartDate)
                            {
                                drow.GSTTaxPointDate = mobjDoc_CashSales.objDoc_CashSales.FinancialDate;
                                if (selectedMember != null && string.IsNullOrEmpty(selectedMember.MasterAccountID) == false && string.IsNullOrEmpty(selectedMember.TaxCodeID) == false && AppState.lstGSTTaxCode.ContainsKey(selectedMember.TaxCodeID))
                                {
                                    drow.TaxCodeID = selectedMember.TaxCodeID;
                                }
                                else
                                {
                                    if (string.IsNullOrEmpty(AppState.objServiceChargeItem.TaxCodeID) == false && AppState.lstGSTTaxCode.ContainsKey(AppState.objServiceChargeItem.TaxCodeID))
                                        drow.TaxCodeID = AppState.objServiceChargeItem.TaxCodeID;
                                    else
                                        drow.TaxCodeID = AppState.CurrentBranch.DefaultSalesTaxCodeID;
                                }

                                if (string.IsNullOrEmpty(drow.TaxCodeID) == false && AppState.lstGSTTaxCode.ContainsKey(drow.TaxCodeID))
                                {
                                    drow.GSTTypeID = AppState.lstGSTTaxCode[drow.TaxCodeID].TaxTypeID;
                                    drow.TaxPercentage = AppState.lstGSTTaxCode[drow.TaxCodeID].TaxRate;
                                    drow.TaxFinancialAccountID = AppState.lstGSTTaxCode[drow.TaxCodeID].FinancialAccountID;
                                }
                                drow.IsTaxInclusive = AppState.objServiceChargeItem.IsTaxInclusive;
                            }
                            else
                            {
                                drow.TaxCodeID = "";
                                drow.GSTTypeID = "";
                                drow.TaxPercentage = 0;
                            }
                            drow.ItemTaxGroupID = AppState?.objServiceChargeItem.ItemTaxGroupID;
                            drow.Quantity = 1;
                            drow.SKUName = "";
                            drow.SKUQuantity = 1;
                            drow.UnitPrice = ServiceCharges;
                            drow.InventoryTypeID = AppState?.objServiceChargeItem?.InventoryTypeID ?? 3;
                            drow.Discount = 0;
                            drow.ActivityTypeID = 1;
                            drow.GSTTaxPointDate = mobjDoc_CashSales.objDoc_CashSales.DocumentTypeID == 52 ? default : mobjDoc_CashSales.objDoc_CashSales.FinancialDate;
                            drow.DiningType = "";

                            mobjDoc_CashSales.lstDocumentLine.Add(drow);
                        }
                    }
                }
            }

            mobjDoc_CashSales.objDoc_CashSales.TotalBeforeTax_NonServiceCharge = mobjDoc_CashSales.lstDocumentLine.Where(x => x.SaveAction != EntityState.Deleted && x.LineItemID != AppState?.objDefaultAccountDM?.ServiceChargeAccountID).Sum(y => y.SubTotalBeforeGST);
            mobjDoc_CashSales.objDoc_CashSales.TotalBeforeTax_ServiceCharge = mobjDoc_CashSales.lstDocumentLine.Where(x => x.SaveAction != EntityState.Deleted && x.LineItemID == AppState?.objDefaultAccountDM?.ServiceChargeAccountID).Sum(y => y.SubTotalBeforeGST);
            mobjDoc_CashSales.objDoc_CashSales.TotalBeforeTax = mobjDoc_CashSales.lstDocumentLine.Where(x => x.SaveAction != EntityState.Deleted).Sum(y => y.SubTotalBeforeGST);
            mobjDoc_CashSales.objDoc_CashSales.HeritageTax = mobjDoc_CashSales.lstDocumentLine.Where(x => x.SaveAction != EntityState.Deleted).Sum(y => y.HeritageTax);
            mobjDoc_CashSales.objDoc_CashSales.TaxAmount = mobjDoc_CashSales.lstDocumentLine.Where(x => x.SaveAction != EntityState.Deleted).Sum(y => y.TaxAmount);
            if (mobjDoc_CashSales.objDoc_CashSales.DocumentTypeID == 5)
            {
                if (mblnUseRounding == false)
                    mobjDoc_CashSales.objDoc_CashSales.RoundingAmount = 0;
                else
                    mobjDoc_CashSales.objDoc_CashSales.RoundingAmount = GetRoundingAmount_RoundToCent(mobjDoc_CashSales.objDoc_CashSales.TotalBeforeTax + mobjDoc_CashSales.objDoc_CashSales.TaxAmount);
            }
            else if (mobjDoc_CashSales.objDoc_CashSales.DocumentTypeID == 52)
            {
                mobjDoc_CashSales.objDoc_CashSales.RoundingAmount = 0;
            }
            mobjDoc_CashSales.objDoc_CashSales.TotalAfterTax = mobjDoc_CashSales.objDoc_CashSales.TotalBeforeTax + mobjDoc_CashSales.objDoc_CashSales.TaxAmount + mobjDoc_CashSales.objDoc_CashSales.TourismTax + mobjDoc_CashSales.objDoc_CashSales.HeritageTax + mobjDoc_CashSales.objDoc_CashSales.RoundingAmount;
            mobjDoc_CashSales.objDoc_CashSales.LocalTotalBeforeTax = mobjDoc_CashSales.lstDocumentLine.Where(x => x.SaveAction != EntityState.Deleted).Sum(y => y.ConvertedSubTotalBeforeGST);
            mobjDoc_CashSales.objDoc_CashSales.LocalTaxAmount = mobjDoc_CashSales.lstDocumentLine.Where(x => x.SaveAction != EntityState.Deleted).Sum(y => y.ConvertedTaxAmount);
            mobjDoc_CashSales.objDoc_CashSales.LocalRoundingAmount = Math.Round(mobjDoc_CashSales.objDoc_CashSales.RoundingAmount * mobjDoc_CashSales.objDoc_CashSales.ExchangeRate, 2);
            mobjDoc_CashSales.objDoc_CashSales.LocalTotalAfterTax = mobjDoc_CashSales.lstDocumentLine.Where(x => x.SaveAction != EntityState.Deleted).Sum(y => y.ConvertedAmount) + mobjDoc_CashSales.objDoc_CashSales.LocalRoundingAmount;
            mobjDoc_CashSales.objDoc_CashSales.LocalTaxableAmount = mobjDoc_CashSales.lstDocumentLine.Where(x => x.SaveAction != EntityState.Deleted).Sum(y => y.ConvertedTaxableAmount);

            if (mobjDoc_CashSales.objDoc_CashSales.DocumentTypeID == 52)
            {
                AddSeriesRedemptionPayment();
                AddTimeRedemptionPayment();
            }

            StateHasChanged();
        }

        private void AddTimeRedemptionPayment()
        {
            decimal dclTimeRedemptionAmount = 0;
            Doc_CashSales_POSReceiptLinesDM objDoc_Redemption_POSReceiptLinesDM;

            dclTimeRedemptionAmount = mobjDoc_CashSales?.lstDocumentLine.Where(x => x.SaveAction != EntityState.Deleted && string.IsNullOrWhiteSpace(x.RedeemedMinutes) == false).Sum(y => y.SubTotalBeforeGST) ?? 0;

            if (dclTimeRedemptionAmount > 0)
            {
                if (mobjDoc_CashSales?.lstReceiptLines.Any(x => x.POSPaymentTypeID == -4) ?? false == true)
                {
                    objDoc_Redemption_POSReceiptLinesDM = mobjDoc_CashSales?.lstReceiptLines.Where(x => x.POSPaymentTypeID == -4).First() ?? new Doc_CashSales_POSReceiptLinesDM();
                    objDoc_Redemption_POSReceiptLinesDM.POSReceiptLineAmount = dclTimeRedemptionAmount;
                }
                else
                {
                    objDoc_Redemption_POSReceiptLinesDM = new Doc_CashSales_POSReceiptLinesDM();
                    objDoc_Redemption_POSReceiptLinesDM.AccountID = mobjDoc_CashSales?.objDoc_CashSales.AccountID;
                    objDoc_Redemption_POSReceiptLinesDM.AccountTypeID = 3;
                    objDoc_Redemption_POSReceiptLinesDM.POSPaymentTypeID = -4;
                    objDoc_Redemption_POSReceiptLinesDM.Description = "Time Redemption";
                    objDoc_Redemption_POSReceiptLinesDM.CurrencyID = mobjDoc_CashSales?.objDoc_CashSales.TransactionCurrencyID;
                    objDoc_Redemption_POSReceiptLinesDM.CurrencyName = mobjDoc_CashSales?.objDoc_CashSales.TransactionCurrencyName;
                    objDoc_Redemption_POSReceiptLinesDM.POSReceiptLineAmount = dclTimeRedemptionAmount;
                    objDoc_Redemption_POSReceiptLinesDM.ExchangeRate = 1;

                    mobjDoc_CashSales?.lstReceiptLines.Add(objDoc_Redemption_POSReceiptLinesDM);
                }
            }
            else
            {
                if (mobjDoc_CashSales?.lstReceiptLines.Count > 0)
                {
                    for (int i = mobjDoc_CashSales.lstReceiptLines.Count - 1; i >= 0; i += -1)
                    {
                        if (mobjDoc_CashSales.lstReceiptLines[i].POSPaymentTypeID == -4)
                        {
                            mobjDoc_CashSales.lstReceiptLines.RemoveAt(i);
                        }
                    }
                }
            }
        }

        private void AddSeriesRedemptionPayment()
        {
            decimal dclSeriesRedemptionAmount = 0;
            Doc_CashSales_POSReceiptLinesDM objDoc_Redemption_POSReceiptLinesDM;

            dclSeriesRedemptionAmount = mobjDoc_CashSales?.lstDocumentLine.Where(x => x.SaveAction != EntityState.Deleted && string.IsNullOrWhiteSpace(x.KitMemberID) == false).Sum(y => y.SubTotal) ?? 0;

            if (dclSeriesRedemptionAmount > 0)
            {
                if (mobjDoc_CashSales?.lstReceiptLines.Any(x => x.POSPaymentTypeID == -5) ?? false == true)
                {
                    objDoc_Redemption_POSReceiptLinesDM = mobjDoc_CashSales?.lstReceiptLines.Where(x => x.POSPaymentTypeID == -5).First() ?? new Doc_CashSales_POSReceiptLinesDM();
                    objDoc_Redemption_POSReceiptLinesDM.POSReceiptLineAmount = dclSeriesRedemptionAmount;
                }
                else
                {
                    objDoc_Redemption_POSReceiptLinesDM = new Doc_CashSales_POSReceiptLinesDM();
                    objDoc_Redemption_POSReceiptLinesDM.AccountID = mobjDoc_CashSales?.objDoc_CashSales.AccountID;
                    objDoc_Redemption_POSReceiptLinesDM.AccountTypeID = 3;
                    objDoc_Redemption_POSReceiptLinesDM.POSPaymentTypeID = -5;
                    objDoc_Redemption_POSReceiptLinesDM.Description = "Series Redemption";
                    objDoc_Redemption_POSReceiptLinesDM.CurrencyID = mobjDoc_CashSales?.objDoc_CashSales.TransactionCurrencyID;
                    objDoc_Redemption_POSReceiptLinesDM.CurrencyName = mobjDoc_CashSales?.objDoc_CashSales.TransactionCurrencyName;
                    objDoc_Redemption_POSReceiptLinesDM.POSReceiptLineAmount = dclSeriesRedemptionAmount;
                    objDoc_Redemption_POSReceiptLinesDM.ExchangeRate = 1;

                    mobjDoc_CashSales?.lstReceiptLines.Add(objDoc_Redemption_POSReceiptLinesDM);
                }
            }
            else
            {
                if (mobjDoc_CashSales?.lstReceiptLines.Count > 0)
                {
                    for (int i = mobjDoc_CashSales.lstReceiptLines.Count - 1; i >= 0; i += -1)
                    {
                        if (mobjDoc_CashSales.lstReceiptLines[i].POSPaymentTypeID == -5)
                        {
                            mobjDoc_CashSales.lstReceiptLines.RemoveAt(i);
                        }
                    }
                }
            }
        }

        private decimal GetRoundingAmount_RoundToCent(decimal dclSalesAmount)
        {
            decimal RoundingAdjustment = 0;
            decimal RoundedAmount = 0;

            switch (AppState?.objDefaultAccountCentralisedDM?.RoundingMethod)
            {
                case "10-Cent":
                    RoundedAmount = Math.Round(dclSalesAmount, 1, MidpointRounding.AwayFromZero);
                    break;
                case "Dollar":
                    RoundedAmount = Math.Round(dclSalesAmount, 0, MidpointRounding.AwayFromZero);
                    break;
                default:
                    RoundedAmount = Math.Round(dclSalesAmount * 20, MidpointRounding.AwayFromZero) / 20;
                    break;
            }
            RoundingAdjustment = RoundedAmount - Math.Round(dclSalesAmount, 2);
            return RoundingAdjustment;
        }

        private string GetNewDocumentLineID()
        {
            string mstr = mintNewDocumentLineID.ToString("D2");
            mintNewDocumentLineID += 1;
            return mstr;
        }

        private DateTime GetRoundToFiveMinutesTime(DateTime dtDate)
        {
            DateTime dt = new DateTime(dtDate.Year, dtDate.Month, dtDate.Day, dtDate.Hour, dtDate.Minute, 0);
            int intMinutes = dtDate.Minute % 5;

            switch (intMinutes)
            {
                case 1: dt = dtDate.AddMinutes(4); break;
                case 2: dt = dtDate.AddMinutes(3); break;
                case 3: dt = dtDate.AddMinutes(2); break;
                case 4: dt = dtDate.AddMinutes(1); break;
            }
            return dt;
        }

        private async Task<decimal> GetPriceAfterPriceGroup(string strInventoryID, decimal dclUnitPrice)
        {
            InventoryDM? objSelectedInventory;
            Inventory_PriceGroupDM drowSelectedInventoryPriceGroup;
            decimal dclUnitPriceAfterPriceGroup = dclUnitPrice;

            // Check Price Group service for setup override
            try
            {
                var assignedPg = await PriceGroupService.GetPriceGroupByProductIdAsync(strInventoryID);
                if (assignedPg != null && assignedPg.Price > 0)
                {
                    dclUnitPriceAfterPriceGroup = assignedPg.Price;
                }
            }
            catch { }

            if (mobjDoc_CashSales?.objDoc_CashSales.DocumentTypeID == 52 && AppState?.objDefaultAccountCentralisedDM?.ApplyPriceGroupOnRedemption == false)
            {
                return dclUnitPriceAfterPriceGroup;
            }
            else
            {
                if (string.IsNullOrWhiteSpace(mobjDoc_CashSales?.objDoc_CashSales.AccountID) == false && mobjDoc_CashSales.objDoc_CashSales.AccountID != "0")
                {
                    objSelectedInventory = await InventoryService.LoadItemAsync(strInventoryID);
                    if (objSelectedInventory == null)
                        return dclUnitPriceAfterPriceGroup;

                    var strPriceGroupID = selectedMember?.PriceGroupID;
                    if (string.IsNullOrEmpty(strPriceGroupID) || strPriceGroupID == "0")
                        return dclUnitPriceAfterPriceGroup;

                    if (objSelectedInventory.lstPriceGroup.ContainsKey(strPriceGroupID))
                    {
                        drowSelectedInventoryPriceGroup = objSelectedInventory.lstPriceGroup[strPriceGroupID];
                        dclUnitPriceAfterPriceGroup = dclUnitPriceAfterPriceGroup * (1 - drowSelectedInventoryPriceGroup.PreDiscount);

                        if (drowSelectedInventoryPriceGroup.FixedPrice > 0)
                        {
                            dclUnitPriceAfterPriceGroup = drowSelectedInventoryPriceGroup.FixedPrice;
                        }
                        else
                        {
                            if (string.IsNullOrWhiteSpace(drowSelectedInventoryPriceGroup.BasisID))
                                return dclUnitPriceAfterPriceGroup;

                            switch (drowSelectedInventoryPriceGroup.BasisID)
                            {
                                case "RP":
                                    dclUnitPriceAfterPriceGroup = Math.Round(dclUnitPriceAfterPriceGroup - (objSelectedInventory.SalesPrice * drowSelectedInventoryPriceGroup.DiscountFactor) + drowSelectedInventoryPriceGroup.PriceFactor, 2, MidpointRounding.AwayFromZero);
                                    break;
                                case "NET":
                                    dclUnitPriceAfterPriceGroup = Math.Round(dclUnitPriceAfterPriceGroup * (1 - drowSelectedInventoryPriceGroup.DiscountFactor) + drowSelectedInventoryPriceGroup.PriceFactor, 2, MidpointRounding.AwayFromZero);
                                    break;
                                case "PV":
                                    dclUnitPriceAfterPriceGroup = Math.Round(dclUnitPriceAfterPriceGroup - (objSelectedInventory.PVValue * drowSelectedInventoryPriceGroup.DiscountFactor) + drowSelectedInventoryPriceGroup.PriceFactor, 2, MidpointRounding.AwayFromZero);
                                    break;
                            }
                        }
                    }
                }
            }
            return dclUnitPriceAfterPriceGroup;
        }

    }
}
