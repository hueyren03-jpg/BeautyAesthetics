using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using SenangRetails.Shared.Models.DTOs.UserControl;
using SenangRetails.Shared.Services.UserControlService;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SenangRetails.Shared.Pages
{
    public partial class UserControl
    {
        [Inject] private IUserControlService UserControlSvc { get; set; } = default!;
        [Inject] private NavigationManager Navigation { get; set; } = default!;
        [Inject] private IJSRuntime JS { get; set; } = default!;

        [Parameter] public string DefaultRole { get; set; } = "";

        private bool isLeftPanelExpanded = false; // Changed to false by default
        private bool isLeftPanelHidden = false;
        private bool isLoadingGroups = false;
        private bool isLoadingFeatures = false;

        private List<Security_UserGroupDM> securityGroups = new();
        private List<SecurityDM> rawPermissions = new();
        private List<DynamicFeatureItem> dynamicFeatures = new();

        private string _selectedRole = "";
        private string selectedRole
        {
            get => _selectedRole;
            set
            {
                if (_selectedRole != value)
                {
                    _selectedRole = value;
                    // Trigger dynamic reloading of feature lists whenever a new group tab is selected
                    _ = LoadDynamicFeaturesForGroupAsync();
                }
            }
        }

        private string searchQuery = "";

        public class DynamicFeatureItem
        {
            public string Name { get; set; } = "";
            public string Category { get; set; } = "";
            public string Slug { get; set; } = "";
        }

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

        private async Task OnTabClick(string groupName)
        {
            // Set the selected role
            _selectedRole = groupName;
            
            // Auto-hide the side menu on all devices after clicking
            isLeftPanelHidden = true;
            isLeftPanelExpanded = false;
            
            // Load the features for the selected group
            await LoadDynamicFeaturesForGroupAsync();
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

                    if (securityGroups.Any())
                    {
                        if (!string.IsNullOrEmpty(DefaultRole))
                        {
                            var matchedGroup = securityGroups.FirstOrDefault(g =>
                                string.Equals(g.UserGroupName?.Trim(), DefaultRole.Trim(), StringComparison.OrdinalIgnoreCase));
                            if (matchedGroup != null)
                            {
                                _selectedRole = matchedGroup.UserGroupName ?? "";
                            }
                            else
                            {
                                _selectedRole = securityGroups.First().UserGroupName ?? "";
                            }
                        }
                        else
                        {
                            _selectedRole = securityGroups.First().UserGroupName ?? "";
                        }
                        await LoadDynamicFeaturesForGroupAsync();
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[ERROR] Failed to load security user groups: {ex.Message}");
            }
            finally
            {
                isLoadingGroups = false;
                StateHasChanged();
            }
        }

        protected override async Task OnParametersSetAsync()
        {
            if (securityGroups != null && securityGroups.Any())
            {
                if (!string.IsNullOrEmpty(DefaultRole))
                {
                    var matchedGroup = securityGroups.FirstOrDefault(g =>
                        string.Equals(g.UserGroupName?.Trim(), DefaultRole.Trim(), StringComparison.OrdinalIgnoreCase));
                    if (matchedGroup != null && _selectedRole != matchedGroup.UserGroupName)
                    {
                        _selectedRole = matchedGroup.UserGroupName ?? "";
                        await LoadDynamicFeaturesForGroupAsync();
                    }
                }
            }
            await base.OnParametersSetAsync();
        }

        private async Task LoadDynamicFeaturesForGroupAsync()
        {
            if (securityGroups == null || !securityGroups.Any()) return;

            var activeGroup = securityGroups.FirstOrDefault(g =>
                string.Equals(g.UserGroupName?.Trim(), selectedRole?.Trim(), StringComparison.OrdinalIgnoreCase));

            if (activeGroup == null || string.IsNullOrEmpty(activeGroup.UserGroupID))
            {
                dynamicFeatures = new List<DynamicFeatureItem>();
                StateHasChanged();
                return;
            }

            try
            {
                isLoadingFeatures = true;
                StateHasChanged();

                var response = await UserControlSvc.LoadProxyByParentIDAsync(activeGroup.UserGroupID);

                if (response != null && response.statusCode == 200 && response.result != null)
                {
                    rawPermissions = response.result;

                    dynamicFeatures = rawPermissions
                        .Where(p => !string.IsNullOrWhiteSpace(p.Category))
                        .GroupBy(p => p.Category!.Trim())
                        .Select(g => new DynamicFeatureItem
                        {
                            Name = g.Key,
                            Category = g.Key,
                            Slug = GenerateSlug(g.Key)
                        })
                        .OrderBy(f => f.Name)
                        .ToList();
                }
                else
                {
                    dynamicFeatures = new List<DynamicFeatureItem>();
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[ERROR] Failed to load features dynamically from proxy list: {ex.Message}");
                dynamicFeatures = new List<DynamicFeatureItem>();
            }
            finally
            {
                isLoadingFeatures = false;
                StateHasChanged();
            }
        }

        private IEnumerable<DynamicFeatureItem> GetFilteredFeatures()
        {
            if (string.IsNullOrWhiteSpace(searchQuery))
            {
                return dynamicFeatures;
            }

            return dynamicFeatures.Where(f =>
                f.Name.Contains(searchQuery, StringComparison.OrdinalIgnoreCase) ||
                f.Category.Contains(searchQuery, StringComparison.OrdinalIgnoreCase));
        }

        private void NavigateToDetail(DynamicFeatureItem feature)
        {
            Navigation.NavigateTo($"/usercontrol/{Uri.EscapeDataString(selectedRole)}/{Uri.EscapeDataString(feature.Category)}");
        }

        private string GenerateSlug(string source)
        {
            if (string.IsNullOrWhiteSpace(source)) return "general";
            return source.ToLower()
                .Replace(" & ", "-")
                .Replace(" ", "-")
                .Replace("/", "-");
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

        private void GoBack() => Navigation.NavigateTo("/home");
    }
}