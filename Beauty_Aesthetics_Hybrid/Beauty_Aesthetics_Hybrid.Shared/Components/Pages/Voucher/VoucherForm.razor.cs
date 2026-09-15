using Beauty_Aesthetics_WebPos.ViewModels;
using Microsoft.AspNetCore.Components;


namespace Beauty_Aesthetics_WebPos.Components.Pages.Voucher
{
    public partial class VoucherForm : ComponentBase
    {
        private VoucherFormViewModel ViewModel = new();
        [Inject] private NavigationManager Navigation { get; set; } = default!;

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
                Navigation?.NavigateTo("/voucher");
            }
        }

        private void Generate()
        {
            ViewModel.GenerateVouchers();
        }

        private void RemoveAll()
        {
            ViewModel.RemoveAllGenerated();
        }

        private void Cancel()
        {
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
