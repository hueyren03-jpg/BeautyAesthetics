using Beauty_Aesthetics_WebPos.ViewModels;
using Beauty_Aesthetics_WebPos.Components.Services.Feedback;
using Microsoft.AspNetCore.Components;


namespace Beauty_Aesthetics_WebPos.Components.Pages.Voucher
{
    public partial class VoucherForm : ComponentBase
    {
        private VoucherFormViewModel ViewModel = new();
        [Inject] private NavigationManager Navigation { get; set; } = default!;
        [Inject] private AppFeedbackService Feedback { get; set; } = default!;

        private bool showSales = true;
        private bool showGenerated = true;

        private bool HasVoucherName => !string.IsNullOrWhiteSpace(ViewModel.Voucher.Name);
        private bool HasQuantity => ViewModel.Voucher.TotalQuantity > 0;
        private bool HasValidDates => ViewModel.Voucher.EndDate.Date >= ViewModel.Voucher.StartDate.Date;

        protected override async Task OnInitializedAsync()
        {
            await ViewModel.LoadAsync();
        }

        private async Task Save()
        {
            if (await ViewModel.SaveAsync())
            {
                Feedback.Success("Voucher campaign saved successfully.", "Voucher saved");
                Navigation?.NavigateTo("/voucher");
                return;
            }

            Feedback.Warning(
                string.IsNullOrWhiteSpace(ViewModel.ErrorMessage) ? "Check the voucher details and try again." : ViewModel.ErrorMessage,
                "Voucher not saved");
        }

        private void Generate()
        {
            if (ViewModel.Voucher.TotalQuantity <= 0)
            {
                Feedback.Warning("Enter a voucher quantity greater than zero before generating codes.", "Nothing generated");
                return;
            }

            ViewModel.GenerateVouchers();
            Feedback.Success($"{ViewModel.GeneratedList.Count} voucher code{(ViewModel.GeneratedList.Count == 1 ? string.Empty : "s")} generated.", "Vouchers generated");
        }

        private void RemoveAll()
        {
            var count = ViewModel.GeneratedList.Count;
            ViewModel.RemoveAllGenerated();
            Feedback.Info(
                count == 0 ? "There were no generated voucher codes to remove." : $"{count} generated voucher code{(count == 1 ? string.Empty : "s")} removed.",
                "Generated vouchers cleared",
                2600);
        }

        private void Cancel()
        {
            Feedback.Info("Voucher changes were not saved.", "Voucher editing cancelled", 2200);
            Navigation.NavigateTo("/voucher");
        }

        private string GetPreviewType()
            => string.IsNullOrWhiteSpace(ViewModel.Voucher.DiscountType)
                ? "Voucher"
                : ViewModel.Voucher.DiscountType;

        private string GetPreviewName()
            => HasVoucherName ? ViewModel.Voucher.Name : "Voucher campaign name";

        private string GetPreviewValue()
        {
            if (ViewModel.Voucher.VoucherAmount > 0)
            {
                return $"MYR {ViewModel.Voucher.VoucherAmount:N2}";
            }

            return ViewModel.Voucher.DiscountValue > 0
                ? $"{ViewModel.Voucher.DiscountValue}% off"
                : "Offer not set";
        }

        private string GetPreviewValidity()
            => HasValidDates
                ? $"{ViewModel.Voucher.StartDate:dd MMM yyyy} – {ViewModel.Voucher.EndDate:dd MMM yyyy}"
                : "Select a valid date range";

        private string GetPreviewRange()
        {
            var prefix = ViewModel.Prefix?.Trim() ?? string.Empty;
            var start = Math.Max(0, ViewModel.RangeFrom);
            var end = ViewModel.RangeTo >= start
                ? ViewModel.RangeTo
                : start + Math.Max(0, ViewModel.Voucher.TotalQuantity - 1);

            return $"{prefix}{start}–{prefix}{end}";
        }
    }
}
