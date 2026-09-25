using Beauty_Aesthetics_WebPos.Components.ViewModels;
using Beauty_Aesthetics_WebPos.Components.Services.Feedback;
using Microsoft.AspNetCore.Components;

namespace Beauty_Aesthetics_WebPos.Components.Pages.Voucher
{
    public partial class VoucherPage : ComponentBase
    {
        [Inject] private AppFeedbackService Feedback { get; set; } = default!;

        private VoucherPageViewModel VM;
        private bool IsPageInitializing { get; set; } = true;

        public VoucherPage(VoucherPageViewModel vm)
        {
            VM = vm;
        }

        protected override async Task OnInitializedAsync()
        {
            IsPageInitializing = true;
            try
            {
                await VM.LoadVouchersAsync();
            }
            finally
            {
                IsPageInitializing = false;
            }
        }

        private void NavigateToDetails(string voucherName)
        {
            if (!string.IsNullOrWhiteSpace(voucherName))
            {
                var encodedName = Uri.EscapeDataString(voucherName);
                Feedback.Info($"Opening voucher campaign {voucherName}.", "Voucher details", 2200);
                NavManager.NavigateTo($"/voucherdetails/{encodedName}");
            }
        }

        private void NavigateToAddVoucher()
        {
            Feedback.Info("Create a new voucher campaign.", "New voucher", 2200);
            NavManager.NavigateTo("/voucherform");
        }

        private void SelectStatus(string status)
        {
            VM.SelectedStatus = status;
        }

        private void SelectAllStatuses() => SelectStatus(string.Empty);
        private void SelectAvailableStatus() => SelectStatus("Available");
        private void SelectSoldStatus() => SelectStatus("Sold");
        private void SelectRedeemedStatus() => SelectStatus("Redeemed");
        private void SelectExpiredStatus() => SelectStatus("Expired");

        private string GetStatusFilterClass(string status)
        {
            var isActive = string.Equals(VM.SelectedStatus, status, StringComparison.OrdinalIgnoreCase);
            return isActive ? "status-tab is-active" : "status-tab";
        }

        private async Task DeleteVoucherAsync(string voucherName)
        {
            if (string.IsNullOrWhiteSpace(voucherName))
            {
                return;
            }

            var confirmed = await JS.InvokeAsync<bool>(
                "confirm",
                new object?[] { $"Delete voucher campaign '{voucherName}'?" });

            if (confirmed)
            {
                VM.DeleteVoucher(voucherName);
                Feedback.Success($"Voucher campaign {voucherName} deleted.", "Voucher deleted");
            }
            else
            {
                Feedback.Info("Voucher deletion cancelled.", "Delete cancelled", 2000);
            }
        }

        private static string GetVoucherStatus(Beauty_Aesthetics_WebPos.Components.Models.Voucher.Voucher voucher)
            => VoucherPageViewModel.GetStatus(voucher);

        private static decimal GetAvailabilityPercentage(Beauty_Aesthetics_WebPos.Components.Models.Voucher.Voucher voucher)
            => voucher.TotalQuantity <= 0
                ? 0
                : Math.Clamp(decimal.Round(voucher.Available * 100m / voucher.TotalQuantity, 0), 0, 100);

        private int GetVisibleStart()
            => VM.FilteredCount == 0 ? 0 : ((VM.CurrentPage - 1) * VM.PageSize) + 1;

        private int GetVisibleEnd()
            => Math.Min(VM.CurrentPage * VM.PageSize, VM.FilteredCount);
    }
}
