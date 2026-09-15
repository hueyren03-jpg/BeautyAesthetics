using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using SenangRetails.Shared.Models.DTOs;
using SenangRetails.Shared.Models.DTOs.UserControl;
using SenangRetails.Shared.Services.UserControlService;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace SenangRetails.Shared.Pages
{
    public partial class UserControlDetail
    {
        [Inject] private IUserControlService UserControlSvc { get; set; } = default!;

        [Parameter] public string RoleType { get; set; } = "";
        [Parameter] public string Feature { get; set; } = ""; // Selected Category context string

        private bool isLeftPanelExpanded = false; // Changed to false by default - sidebar hidden on first load
        private bool isLeftPanelHidden = false;
        private bool isLoadingGroups = true;
        private bool isLoadingPermissions = true;

        private List<Security_UserGroupDM> securityGroups = new();

        // Track the original clones of the records to accurately find modifications
        private List<SecurityDM> permissionRecords = new();
        private List<string> originalRecordsSnapshotJson = new();

        protected override async Task OnInitializedAsync()
        {
            await LoadUserGroupsAsync();
            await base.OnInitializedAsync();
        }

        protected override async Task OnAfterRenderAsync(bool firstRender)
        {
            if (firstRender)
            {
                // Sidebar is already hidden by default (isLeftPanelExpanded = false)
                // No need to check width anymore since we want it hidden on all screens initially
                StateHasChanged();
            }
            await base.OnAfterRenderAsync(firstRender);
        }

        protected override async Task OnParametersSetAsync()
        {
            searchQuery = "";
            _saveMessage = null;
            await FetchPermissionsForActiveGroupAsync();
        }

        private async Task LoadUserGroupsAsync()
        {
            try
            {
                isLoadingGroups = true;
                var response = await UserControlSvc.GetAllSecurityGroupsAsync();

                if (response != null && response.statusCode == 200 && response.result != null)
                {
                    securityGroups = response.result;
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[ERROR] Failed to load user groups on detail page: {ex.Message}");
            }
            finally
            {
                isLoadingGroups = false;
            }
        }

        private async Task FetchPermissionsForActiveGroupAsync()
        {
            if (securityGroups == null || !securityGroups.Any()) return;

            var activeGroup = securityGroups.FirstOrDefault(g =>
                string.Equals(g.UserGroupName?.Trim(), RoleType?.Trim(), StringComparison.OrdinalIgnoreCase));

            if (activeGroup == null || string.IsNullOrEmpty(activeGroup.UserGroupID))
            {
                permissionRecords = new List<SecurityDM>();
                originalRecordsSnapshotJson = new List<string>();
                return;
            }

            try
            {
                isLoadingPermissions = true;
                StateHasChanged();

                var response = await UserControlSvc.LoadProxyByParentIDAsync(activeGroup.UserGroupID);

                if (response != null && response.statusCode == 200 && response.result != null)
                {
                    // Filter records so ONLY modules belonging to the selected Category are fetched
                    permissionRecords = response.result
                        .Where(p => string.Equals(p.Category?.Trim(), Feature?.Trim(), StringComparison.OrdinalIgnoreCase))
                        .ToList();

                    // Take a precise deep snapshot mapping to detect local manual modifications on save click
                    originalRecordsSnapshotJson = permissionRecords
                        .Select(p => JsonSerializer.Serialize(p))
                        .ToList();
                }
                else
                {
                    permissionRecords = new List<SecurityDM>();
                    originalRecordsSnapshotJson = new List<string>();
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[ERROR] Failed to load granular proxy permissions matrix: {ex.Message}");
                permissionRecords = new List<SecurityDM>();
                originalRecordsSnapshotJson = new List<string>();
            }
            finally
            {
                isLoadingPermissions = false;
                StateHasChanged();
            }
        }

        private IEnumerable<IGrouping<string, SecurityDM>> GetFilteredGroupedPermissions()
        {
            var dataQuery = permissionRecords.AsEnumerable();

            if (!string.IsNullOrWhiteSpace(searchQuery))
            {
                dataQuery = dataQuery.Where(p =>
                    (p.ModuleName != null && p.ModuleName.Contains(searchQuery, StringComparison.OrdinalIgnoreCase)) ||
                    (p.FormName != null && p.FormName.Contains(searchQuery, StringComparison.OrdinalIgnoreCase)));
            }

            return dataQuery
                .GroupBy(p => string.IsNullOrWhiteSpace(p.Category) ? "Other Access Control" : p.Category)
                .OrderBy(g => g.Key);
        }

        private void HideLeftPanel()
        {
            isLeftPanelHidden = true;
            isLeftPanelExpanded = false;
        }

        private void ShowLeftPanel()
        {
            isLeftPanelHidden = false;
            isLeftPanelExpanded = true;
        }

        private string searchQuery = "";
        private bool _isSaving = false;
        private string? _saveMessage;
        private bool _saveSuccess;

        private void SwitchRole(string userGroupName)
        {
            isLeftPanelHidden = true;
            isLeftPanelExpanded = false;
            Navigation.NavigateTo($"/usercontrol/{Uri.EscapeDataString(userGroupName)}/{Uri.EscapeDataString(Feature)}");
        }

        private void GoBack() => Navigation.NavigateTo($"/usercontrol/{Uri.EscapeDataString(RoleType)}");

        private async Task SaveAsync()
        {
            _saveMessage = null;
            var modifiedRowsList = new List<SecurityDM>();

            // 1. Identify rows that contain variations compared to the initial loading layout states
            for (int i = 0; i < permissionRecords.Count; i++)
            {
                var currentRecord = permissionRecords[i];
                var originalRecord = JsonSerializer.Deserialize<SecurityDM>(originalRecordsSnapshotJson[i]);

                if (originalRecord != null && (
                    currentRecord.CanViewRecord != originalRecord.CanViewRecord ||
                    currentRecord.CanAddRecord != originalRecord.CanAddRecord ||
                    currentRecord.CanEditRecord != originalRecord.CanEditRecord ||
                    currentRecord.CanDeleteRecord != originalRecord.CanDeleteRecord ||
                    currentRecord.CanSearch != originalRecord.CanSearch))
                {
                    modifiedRowsList.Add(currentRecord);
                }
            }

            if (!modifiedRowsList.Any())
            {
                _saveSuccess = true;
                _saveMessage = "No parameter changes detected to update.";
                return;
            }

            try
            {
                _isSaving = true;
                _saveMessage = "Saving changed configuration settings...";
                StateHasChanged();

                int updateSuccessCount = 0;

                // 2. Loop through and persist modified blocks row-by-row using your explicit Update API
                foreach (var row in modifiedRowsList)
                {
                    var response = await UserControlSvc.UpdateSecurityRecordAsync(row);
                    if (response != null && response.statusCode == 200)
                    {
                        updateSuccessCount++;
                    }
                }

                if (updateSuccessCount == modifiedRowsList.Count)
                {
                    _saveSuccess = true;
                    _saveMessage = "All security configuration changes saved successfully.";

                    // Refresh snapshots to establish a new pristine baseline setup
                    originalRecordsSnapshotJson = permissionRecords
                        .Select(p => JsonSerializer.Serialize(p))
                        .ToList();
                }
                else if (updateSuccessCount > 0)
                {
                    _saveSuccess = false;
                    _saveMessage = $"Partial Save: Updated {updateSuccessCount} of {modifiedRowsList.Count} rules.";
                }
                else
                {
                    _saveSuccess = false;
                    _saveMessage = "Failed to update changed configuration settings onto endpoint.";
                }
            }
            catch (Exception ex)
            {
                _saveSuccess = false;
                _saveMessage = $"Save execution interrupted: {ex.Message}";
            }
            finally
            {
                _isSaving = false;
                StateHasChanged();
            }
        }
    }
}