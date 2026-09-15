using Microsoft.AspNetCore.Components;
using SenangRetails.Shared.ApiClient;
using SenangRetails.Shared.Models;
using SenangRetails.Shared.Models.DTOs;
using SenangRetails.Shared.Services;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;

using SenangRetails.Shared.Services.CommissionSetupService;

namespace SenangRetails.Shared.Pages
{
    public partial class Staff
    {
        [Inject] private StaffService StaffService { get; set; }
        [Inject] private NavigationManager Navigation { get; set; }
        [Inject] private ICommissionSetupService CommissionService { get; set; }

        // --- State ---
        private bool isLeftPanelExpanded = true;
        private bool isLeftPanelHidden = false;
        private string selectedLeftTab = "All Staff";
        private bool isLoading = true;
        private string errorMessage = string.Empty;
        private string validationMessage = string.Empty;

        // --- Paging State ---
        private int currentPage = 1;
        private int pageSize = 10;
        private int TotalPages => (int)Math.Ceiling((double)GetFilteredStaff().Count / pageSize);

        // --- Filter Properties (Replaces duplicate fields) ---
        private string _searchQuery = string.Empty;
        private string searchQuery
        {
            get => _searchQuery;
            set
            {
                if (_searchQuery != value)
                {
                    _searchQuery = value;
                    currentPage = 1; // Reset page on search
                }
            }
        }

        private string _selectedStatusTab = "Active";
        private string selectedStatusTab
        {
            get => _selectedStatusTab;
            set
            {
                if (_selectedStatusTab != value)
                {
                    _selectedStatusTab = value;
                    currentPage = 1;
                }
            }
        }

        // --- Data ---
        private List<StaffResponseDTO> staffList = new();
        private List<CommissionSchemeModel> availableSchemes = new();

        // --- Form/Modal State ---
        private bool showNewStaffForm = false;
        private bool showDetailsModal = false;
        private bool isEditMode = false;
        private string selectedFormTab = "Info";

        private bool isNumpadOpen;
        private string numpadInitialValue = "";
        private void OpenStaffPhoneNumpad()
        {
            numpadInitialValue = newStaff.Phone;
            isNumpadOpen = true;
        }
        private void OnNumpadSaved(string val)
        {
            newStaff.Phone = val;
        }
        private NewStaffModel newStaff = new();

        private class NewStaffModel
        {
            public string MasterAccountID { get; set; } = string.Empty;
            public string AccountName { get; set; } = string.Empty;
            public string Phone { get; set; } = string.Empty;
            public string Gender { get; set; } = string.Empty;
            public string JobTitle { get; set; } = string.Empty;
            public string SalesPersonCode { get; set; } = string.Empty;
            public string CommissionSchemeId { get; set; } = string.Empty;
            public string CommissionSchemeName { get; set; } = string.Empty;
            public string AccountStatus { get; set; } = "Active";
            public bool IsSalesPerson { get; set; } = true;
            public bool IsNotSalesPerson { get; set; } = false;
            public DateTime DateHired { get; set; } = DateTime.Now;
            public DateTime? DateResigned { get; set; }
            public string WorkAvailability { get; set; } = "Work";
        }

        protected override async Task OnInitializedAsync()
        {
            try
            {
                availableSchemes = await CommissionService.GetSchemesAsync() ?? new();
            }
            catch { }
            await LoadStaffDataAsync();
        }

        private async Task LoadStaffDataAsync()
        {
            isLoading = true;
            errorMessage = string.Empty;
            try
            {
                var response = await StaffService.GetStaffListAsync();
                if (response != null && response.StatusCode == 200)
                {
                    staffList = response.Result ?? new List<StaffResponseDTO>();
                }
                else
                {
                    errorMessage = response?.Message ?? "Failed to load staff data.";
                }
            }
            catch (Exception ex)
            {
                errorMessage = "An error occurred while fetching data.";
                Console.WriteLine(ex.Message);
            }
            finally
            {
                isLoading = false;
                StateHasChanged();
            }
        }

        // --- Logic Methods ---

        // This is the ONE definition of filtering
        private List<StaffResponseDTO> GetFilteredStaff()
        {
            var query = staffList.AsQueryable();

            if (selectedStatusTab != "All")
                query = query.Where(s => s.AccountStatus == selectedStatusTab);

            if (!string.IsNullOrWhiteSpace(searchQuery))
            {
                query = query.Where(s =>
                    (s.AccountName != null && s.AccountName.Contains(searchQuery, StringComparison.OrdinalIgnoreCase)) ||
                    (s.Phone != null && s.Phone.Contains(searchQuery, StringComparison.OrdinalIgnoreCase))
                );
            }

            return query.ToList();
        }

        // Use this in your @foreach in the HTML
        private IEnumerable<StaffResponseDTO> GetPagedStaff()
        {
            // Calling GetFilteredStaff() ensures search and status filters are applied first
            return GetFilteredStaff()
                .Skip((currentPage - 1) * pageSize)
                .Take(pageSize)
                .ToList(); // Ensure it's materialized
        }

        private void ChangePage(int newPage)
        {
            currentPage = newPage;
            StateHasChanged();
        }

        // --- Save/Update Logic ---
        private async Task SaveNewStaff()
        {
            if (string.IsNullOrWhiteSpace(newStaff.AccountName) || string.IsNullOrWhiteSpace(newStaff.Phone))
            {
                validationMessage = "Name and Mobile Phone are required.";
                return;
            }

            validationMessage = string.Empty;
            isLoading = true;

            try
            {
                DateTime sqlMinDate = new DateTime(1753, 1, 1);
                var now = DateTime.Now;

                var request = new StaffRequestDTO
                {
                    // --- Identity/State (Derived from the Form) ---
                    MasterAccountId = null, // Backend prefers null over ""
                    AccountStatus = newStaff.AccountStatus ?? "Active",
                    AccountName = newStaff.AccountName,
                    SalesPersonCode = newStaff.SalesPersonCode ?? "",
                    CommissionSchemeId = newStaff.CommissionSchemeId,
                    CommissionSchemeName = availableSchemes.FirstOrDefault(s => s.Id == newStaff.CommissionSchemeId)?.Name ?? newStaff.CommissionSchemeName ?? "",
                    IsSalesPerson = newStaff.IsSalesPerson,
                    IsNotSalesPerson = newStaff.IsNotSalesPerson,
                    JobTitle = newStaff.JobTitle ?? "",
                    Phone = newStaff.Phone,
                    Gender = newStaff.Gender ?? "",

                    // --- Numbers (Mirroring CSV logic) ---
                    BirthdayYear = 0,
                    BirthdayMonth = 0,
                    BirthdayDay = 0,
                    NumericCode = 0,
                    AccountTypeId = 0,
                    MaxDiscountLimit = 0,
                    MonthlyPurchaseLimit = 0,
                    BasicPay = 0,

                    // --- Dates ---
                    DateHired = newStaff.DateHired < sqlMinDate ? sqlMinDate : newStaff.DateHired,
                    DateResigned = newStaff.DateResigned ?? now,

                    // --- Required Backend Defaults (The "Fixes 500 Error" section from your Import) ---
                    IsSelected = true,
                    IsLoading = true, // Set to true as seen in your working trace
                    IsDirty = true,
                    SaveAction = "Added",

                    // CRITICAL: Use null for these to match the working database record
                    AlphaCode = null,
                    DisplayCode = null,
                    BranchId = "HQ",
                    Createdby = null,
                    Modifiedby = null,
                    Nric = null,
                    ImagePath = null,
                    EmployeeTypeId = null,
                    Remarks = null,
                    FingerPrint = null,

                    // Timestamps
                    CreatedDateTime = now,
                    ModifiedDateTime = now,
                    UpdateTimeStamp = now,
                    AppFirstLoginDate = now
                };

                var response = await StaffService.CreateStaffAsync(request);

                if (response != null && response.statusCode == 200)
                {
                    showNewStaffForm = false;
                    await LoadStaffDataAsync();
                }
                else
                {
                    validationMessage = response?.message ?? "Error saving staff record.";
                }
            }
            catch (Exception ex)
            {
                validationMessage = "An unexpected error occurred.";
                Console.WriteLine(ex.Message);
            }
            finally
            {
                isLoading = false;
                StateHasChanged();
            }
        }

        private async Task UpdateStaff()
        {
            if (string.IsNullOrWhiteSpace(newStaff.AccountName) || string.IsNullOrWhiteSpace(newStaff.Phone))
            {
                validationMessage = "Name and Mobile Phone are required.";
                return;
            }

            validationMessage = string.Empty;
            isLoading = true;

            try
            {
                DateTime sqlMinDate = new DateTime(1753, 1, 1);
                var now = DateTime.Now;
                // The working payload uses this specific "zero" date string
                DateTime zeroDate = new DateTime(1, 1, 1, 0, 0, 0);

                var request = new StaffRequestDTO
                {
                    // --- Primary Identifiers ---
                    IsSelected = false,
                    IsLoading = false,
                    MasterAccountId = newStaff.MasterAccountID, // Ensure this is "HQ0000000051062" format
                    AlphaCode = null,
                    NumericCode = 0,
                    DisplayCode = null,
                    AccountName = newStaff.AccountName,
                    AccountTypeId = 0,

                    // --- Audit Info ---
                    Createdby = null,
                    CreatedDateTime = now, // Match the current timestamp
                    Modifiedby = null,
                    ModifiedDateTime = now, // Matches "0001-01-01T00:00:00"

                    // --- Personal Info ---
                    BirthdayYear = 0,
                    BirthdayMonth = 0,
                    BirthdayDay = 0,
                    Gender = string.IsNullOrWhiteSpace(newStaff.Gender) ? null : newStaff.Gender,
                    Nric = null, // Ensure your DTO property maps to "NRIC" in JSON
                    SalesPersonCode = string.IsNullOrWhiteSpace(newStaff.SalesPersonCode) ? null : newStaff.SalesPersonCode,
                    CommissionSchemeId = newStaff.CommissionSchemeId,
                    CommissionSchemeName = availableSchemes.FirstOrDefault(s => s.Id == newStaff.CommissionSchemeId)?.Name ?? newStaff.CommissionSchemeName ?? "",
                    JobTitle = string.IsNullOrWhiteSpace(newStaff.JobTitle) ? null : newStaff.JobTitle,

                    // --- Employment Dates ---
                    DateHired = newStaff.DateHired < sqlMinDate ? sqlMinDate : newStaff.DateHired,
                    DateResigned = newStaff.DateResigned ?? newStaff.DateHired,

                    // --- Settings ---
                    ImagePath = null,
                    EmployeeTypeId = null,
                    MaxDiscountLimit = 0,
                    MonthlyPurchaseLimit = 0,
                    AccountStatus = newStaff.AccountStatus ?? "Active",
                    BranchId = "HQ",

                    // --- Logic Flags (Matching working JSON exactly) ---
                    IsNotSalesPerson = newStaff.IsNotSalesPerson,
                    IsSalesPerson = newStaff.IsSalesPerson,
                    FingerPrint = "", // Working payload had ""
                    BasicPay = 0,
                    Phone = newStaff.Phone,
                    Remarks = null,

                    // --- App/Device State ---
                    UpdateTimeStamp = now,
                    AppFirstLoginDate = now, // Matches "0001-01-01T00:00:00"
                    SaveAction = "Changed",
                    IsDirty = false // Working payload had false
                };

                var response = await StaffService.EditStaffAsync(request);

                if (response != null && response.statusCode == 200)
                {
                    showDetailsModal = false;
                    isEditMode = false;
                    await LoadStaffDataAsync();
                }
                else
                {
                    validationMessage = response?.message ?? "Update failed.";
                }
            }
            catch (Exception ex)
            {
                validationMessage = "Error updating staff.";
                Console.WriteLine($"Update Error: {ex.Message}");
            }
            finally
            {
                isLoading = false;
                StateHasChanged();
            }
        }

        // --- UI Helpers ---
        private void ViewStaffDetails(StaffResponseDTO staff)
        {
            validationMessage = string.Empty;
            isEditMode = true;
            newStaff = new NewStaffModel
            {
                MasterAccountID = staff.MasterAccountID,
                AccountName = staff.AccountName,
                Phone = staff.Phone,
                Gender = staff.Gender,
                JobTitle = staff.JobTitle,
                SalesPersonCode = staff.SalesPersonCode,
                CommissionSchemeId = staff.CommissionSchemeID ?? "",
                CommissionSchemeName = staff.CommissionSchemeName ?? "",
                AccountStatus = staff.AccountStatus,
                IsSalesPerson = staff.IsSalesPerson,
                IsNotSalesPerson = staff.IsNotSalesPerson,
                DateHired = staff.DateHired
            };
            showDetailsModal = true;
        }

        private void EnableEditMode() => isEditMode = true;
        private void OpenNewStaffForm() { showNewStaffForm = true; selectedFormTab = "Info"; newStaff = new NewStaffModel(); }
        private void CloseNewStaffForm() => showNewStaffForm = false;
        private void CloseDetailsModal() => showDetailsModal = false;
        private void SelectFormTab(string tab) => selectedFormTab = tab;
        private void GoBack() => Navigation.NavigateTo("/home");
        private void HideLeftPanel() => isLeftPanelHidden = true;
        private void ShowLeftPanel() { isLeftPanelHidden = false; isLeftPanelExpanded = true; }
        private void SelectLeftTab(string tab) => selectedLeftTab = tab;

        // --- UI Helper Methods (avoids double-quote issues in Blazor attributes) ---
        private string GetTabClass(string tab) => selectedStatusTab == tab ? "active" : "";
        private string SearchPlaceholder => selectedStatusTab == "All" ? "Search staff..." : $"Search {selectedStatusTab.ToLower()} staff...";
        private bool IsNewStaffActive => newStaff.AccountStatus == "Active";
        private void OnNewStaffActiveChanged(ChangeEventArgs e) =>
            newStaff.AccountStatus = (bool)e.Value! ? "Active" : "Inactive";
        private string GetStatusBadgeClass() =>
            newStaff.AccountStatus == "Active" ? "badge-active" : "badge-inactive";
    }
}
