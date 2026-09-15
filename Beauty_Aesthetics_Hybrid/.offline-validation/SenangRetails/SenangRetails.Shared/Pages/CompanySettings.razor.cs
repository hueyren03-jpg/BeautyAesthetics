using System;
using System.Collections.Generic;
using System.Text;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.JSInterop;
using SenangRetails.Shared.Models;

namespace SenangRetails.Shared.Pages
{
    public partial class CompanySettings : BasePage
    {
        private bool isSaving = false;
        private string saveMessage = "";
        private bool saveSuccess = false;
        private string logoError = "";

        private string companyLogo = "";
        private string brandName = "";
        private string contact = "";
        private string outletName = "";
        private string address1 = "";
        private string address2 = "";
        private string address3 = "";
        private string selectedTaxType = "";
        private string selectedSalesTaxCode = "";
        private bool isSalesTaxCodeEnabled = false;
        private bool isLoadingTaxCodes = false;
        private string taxCodesError = "";

        private List<TaxCodeItem> salesTaxCodes = new List<TaxCodeItem>();

        private bool _fieldsLoaded = false;

        protected override async Task OnAfterRenderAsync(bool firstRender)
        {
            if (!_fieldsLoaded && AppState.CurrentBranch != null)
            {
                _fieldsLoaded = true;
                LoadFieldsFromBranch();

                // Load tax codes if tax type is already selected
                if (!string.IsNullOrEmpty(selectedTaxType))
                {
                    await LoadTaxCodesByTypeAsync(selectedTaxType);
                }

                StateHasChanged();
            }
        }

        private void LoadFieldsFromBranch()
        {
            var branch = AppState.CurrentBranch!;
            companyLogo = branch.LogoPath ?? "";
            brandName = branch.CompanyName ?? "";
            contact = branch.Phone ?? "";
            outletName = branch.Branch ?? "";
            address1 = branch.Address1 ?? "";
            address2 = branch.Address2 ?? "";
            address3 = branch.Address3 ?? "";
            selectedTaxType = branch.TaxTypeID ?? "";
            selectedSalesTaxCode = branch.DefaultSalesTaxCodeID ?? "";

            // Enable sales tax code dropdown if tax type is selected
            isSalesTaxCodeEnabled = !string.IsNullOrEmpty(selectedTaxType);
        }

        private async Task OnTaxTypeChanged(ChangeEventArgs e)
        {
            selectedTaxType = e.Value?.ToString() ?? "";
            isSalesTaxCodeEnabled = !string.IsNullOrEmpty(selectedTaxType);

            // Reset sales tax code when tax type changes
            selectedSalesTaxCode = "";

            // Clear existing tax codes
            salesTaxCodes.Clear();

            // Load new tax codes based on selected tax type
            if (!string.IsNullOrEmpty(selectedTaxType))
            {
                await LoadTaxCodesByTypeAsync(selectedTaxType);
            }

            StateHasChanged();
        }

        private async Task LoadTaxCodesByTypeAsync(string taxType)
        {
            try
            {
                isLoadingTaxCodes = true;
                taxCodesError = "";
                StateHasChanged();

                // Call the API with the tax type ID (GST or SST)
                var response = await TaxService.LoadProxyByParentIDAsync(taxType);

                if (response != null && response.StatusCode == 200 && response.Result != null)
                {
                    // Filter only active tax codes
                    salesTaxCodes = response.Result.Where(tc => tc.Active == true).ToList();

                    if (!salesTaxCodes.Any())
                    {
                        taxCodesError = $"No active tax codes found for {taxType}";
                    }
                }
                else
                {
                    taxCodesError = response?.Message ?? $"Failed to load tax codes for {taxType}";
                    salesTaxCodes.Clear();
                }
            }
            catch (Exception ex)
            {
                taxCodesError = $"Error loading tax codes: {ex.Message}";
                salesTaxCodes.Clear();
            }
            finally
            {
                isLoadingTaxCodes = false;
                StateHasChanged();
            }
        }

        // ── Logo upload ────────────────────────────────────────────────────────
        private async Task TriggerLogoUpload()
        {
            await JS.InvokeVoidAsync("eval", "document.getElementById('logoFileInput').click()");
        }

        private async Task OnLogoFileChanged(InputFileChangeEventArgs e)
        {
            logoError = "";
            var file = e.File;
            const long maxBytes = 2 * 1024 * 1024;

            if (file.Size > maxBytes)
            {
                logoError = "Image must be smaller than 2 MB.";
                return;
            }

            try
            {
                using var stream = file.OpenReadStream(maxBytes);
                using var ms = new System.IO.MemoryStream();
                await stream.CopyToAsync(ms);
                companyLogo = $"data:{file.ContentType};base64,{Convert.ToBase64String(ms.ToArray())}";
            }
            catch
            {
                logoError = "Failed to read image. Please try again.";
            }
        }

        private void RemoveLogo()
        {
            companyLogo = "";
            logoError = "";
        }

        // ── Save ──────────────────────────────────────────────────────────────
        private async Task SaveCompanyInfo()
        {
            if (AppState.CurrentBranch == null)
            {
                saveMessage = "Branch data not loaded yet. Please wait.";
                saveSuccess = false;
                return;
            }

            isSaving = true;
            saveMessage = "";
            try
            {
                AppState.CurrentBranch.BranchID = AppState.SelectedBranchID;
                AppState.CurrentBranch.CompanyName = brandName;
                AppState.CurrentBranch.Phone = contact;
                AppState.CurrentBranch.LogoPath = companyLogo;
                AppState.CurrentBranch.Branch = outletName;
                AppState.CurrentBranch.Address1 = address1;
                AppState.CurrentBranch.Address2 = address2;
                AppState.CurrentBranch.Address3 = address3;
                AppState.CurrentBranch.TaxTypeID = selectedTaxType;
                AppState.CurrentBranch.DefaultSalesTaxCodeID = selectedSalesTaxCode;

                var (success, msg) = await BranchService.UpdateBranchAsync(AppState.CurrentBranch);
                if (!success)
                {
                    saveMessage = $"Failed to save: {msg}";
                    saveSuccess = false;
                    return;
                }

                // Update localStorage cache so data is fresh on next load
                var updatedJson = System.Text.Json.JsonSerializer.Serialize(AppState.CurrentBranch);
                await JS.InvokeVoidAsync("localStorage.setItem", "currentBranchDetails", updatedJson);

                NotificationSvc.Add(new AppNotification
                {
                    Icon = "⚙️",
                    TitleKey = "NotifCompanySettingsUpdatedTitle",
                    MessageKey = "NotifCompanySettingsUpdatedMsg"
                });
                await JS.InvokeVoidAsync("localStorage.setItem", "app_notifications", NotificationSvc.ToJson());

                saveMessage = "Saved successfully.";
                saveSuccess = true;
            }
            catch (Exception ex)
            {
                saveMessage = $"Save failed: {ex.Message}";
                saveSuccess = false;
            }
            finally
            {
                isSaving = false;
            }
        }

        public class TaxCodeItem
        {
            public bool IsLoading { get; set; }
            public string? TaxCodeID { get; set; }
            public string? TaxDescription { get; set; }
            public decimal TaxRate { get; set; }
            public string? TaxTypeID { get; set; }
            public string? TaxCategory { get; set; }
            public bool Active { get; set; }
            public string? FinancialAccountID { get; set; }
            public string? FinancialAccountName { get; set; }
            public int SaveAction { get; set; }
            public bool IsDirty { get; set; }
        }
    }
}
