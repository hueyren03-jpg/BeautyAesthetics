using Beauty_Aesthetics_WebPos.Components.Models.Voucher;
using Beauty_Aesthetics_WebPos.Components.ViewModels;
using Beauty_Aesthetics_WebPos.Components.Services.Feedback;
using Beauty_Aesthetics_WebPos.ViewModels;
using Microsoft.AspNetCore.Components;

namespace Beauty_Aesthetics_WebPos.Components.Pages.Voucher
{
    public partial class GeneratedVoucher : ComponentBase
    {
        [Inject] NavigationManager Nav { get; set; } = default!;
        [Inject] AppFeedbackService Feedback { get; set; } = default!;
        [Parameter] public string Code { get; set; } = "";
        [Parameter][SupplyParameterFromQuery] public string VoucherName { get; set; } = "";

        public GeneratedVoucherViewModel ViewModel { get; set; } = new();

        private bool showBranch = false;
        private bool showStatus = false;
        private string VoucherBackUrl => string.IsNullOrWhiteSpace(ViewModel.Voucher?.Name)
            ? "/voucher"
            : $"/voucherdetails/{Uri.EscapeDataString(ViewModel.Voucher.Name)}";

        protected override void OnInitialized()
        {
            // If Code is passed, use it to load the voucher
            if (!string.IsNullOrWhiteSpace(Code))
            {
                // For now, simulate loading using Code
                ViewModel.Voucher = new Models.Voucher.Voucher
                {
                    Name = VoucherName,
                    VoucherNo = Code,
                    VoucherAmount = 50,
                    SalesAmount = 0,
                    DiscountType = "Percentage",
                    DiscountValue = 10,
                    ImmediateRedeemable = true,
                    IsBlock = false,
                    CanSellBranch = "ALL",
                    CanRedeemBranch = "ALL",
                    ExpiryDays = 30,
                    StartDate = DateTime.Today,
                    EndDate = DateTime.Today.AddMonths(1),
                    SalesOutlet = "",
                    RedeemOutlet = "",
                    SalesDate = null,
                    RedeemDate = null,
                    SalesValue = null,
                    RedeemValue = null,
                    SalesDocNo = "",
                    RedeemDocNo = ""
                };
            }
            else
            {
                // Default for UI testing
                ViewModel.Voucher = new Models.Voucher.Voucher
                {
                    Name = "DISCOUNT ABC",
                    VoucherNo = "A000001",
                    VoucherAmount = 50,
                    SalesAmount = 0,
                    DiscountType = "Percentage",
                    DiscountValue = 10,
                    ImmediateRedeemable = true,
                    IsBlock = false,
                    CanSellBranch = "ALL",
                    CanRedeemBranch = "ALL",
                    ExpiryDays = 30,
                    StartDate = DateTime.Today,
                    EndDate = DateTime.Today.AddMonths(1),
                    SalesOutlet = "",
                    RedeemOutlet = "",
                    SalesDate = null,
                    RedeemDate = null,
                    SalesValue = null,
                    RedeemValue = null,
                    SalesDocNo = "",
                    RedeemDocNo = ""
                };
            }

            base.OnInitialized();
        }



        // ========================
        //        BUTTON EVENT
        // ========================
        protected void Save()
        {
            // UI-only validation
            if (string.IsNullOrWhiteSpace(ViewModel.Voucher.Name))
            {
                ViewModel.ErrorMessage = "Voucher Name cannot be empty.";
                Feedback.Warning(ViewModel.ErrorMessage, "Voucher not saved");
                return;
            }

            ViewModel.ErrorMessage = string.Empty;
            Feedback.Success($"Voucher {ViewModel.Voucher.VoucherNo} updated successfully.", "Voucher updated");
            Nav?.NavigateTo($"/voucherdetails/{Uri.EscapeDataString(ViewModel.Voucher.Name)}");
        }

        protected void Cancel()
        {
            Feedback.Info("Voucher changes were not saved.", "Edit cancelled", 2000);
            var voucherName = ViewModel.Voucher?.Name;
            if (string.IsNullOrWhiteSpace(voucherName))
            {
                Nav.NavigateTo("/voucher");
                return;
            }

            Nav.NavigateTo($"/voucherdetails/{Uri.EscapeDataString(voucherName)}");
        }
    }
}
