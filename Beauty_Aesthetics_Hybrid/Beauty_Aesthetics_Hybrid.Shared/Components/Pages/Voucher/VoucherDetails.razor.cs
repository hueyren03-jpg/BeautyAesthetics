using Microsoft.AspNetCore.Components;
using Beauty_Aesthetics_WebPos.Components.ViewModels;
using Beauty_Aesthetics_WebPos.Components.Services.Feedback;

namespace Beauty_Aesthetics_WebPos.Components.Pages.Voucher
{
    public partial class VoucherDetails : ComponentBase
    {
        [Inject] private AppFeedbackService Feedback { get; set; } = default!;
        [Parameter] public string VoucherName { get; set; } = "";

        private VoucherDetailsViewModel VM;

        private bool showDeleteModal = false;
        private string? voucherCodeToDelete = null;

        protected void EditVoucher(string code, string voucherName)
        {
            if (!string.IsNullOrWhiteSpace(code) && !string.IsNullOrWhiteSpace(voucherName))
            {
                // Pass both as query parameters
                Feedback.Info($"Editing voucher {code}.", "Edit voucher", 2200);
                Nav.NavigateTo($"/generatedvoucher/{code}?voucherName={Uri.EscapeDataString(voucherName)}");
            }
        }


        // Open modal for a specific item
        private void ShowDeleteModal(string code)
        {
            voucherCodeToDelete = code;
            showDeleteModal = true;
        }

        // Cancel delete
        private void CancelDelete()
        {
            showDeleteModal = false;
            voucherCodeToDelete = null;
            Feedback.Info("Voucher deletion cancelled.", "Delete cancelled", 1800);
        }

        // Confirm delete
        private async Task ConfirmDelete()
        {
            if (!string.IsNullOrEmpty(voucherCodeToDelete))
            {
                var deletedCode = voucherCodeToDelete;
                await VM.DeleteVoucherItemAsync(deletedCode);
                Feedback.Success($"Voucher {deletedCode} deleted.", "Voucher deleted");
            }
            showDeleteModal = false;
            voucherCodeToDelete = null;
        }

        private void ShowDeleteModal()
        {
            showDeleteModal = true;
        }

        public VoucherDetails(VoucherDetailsViewModel vm)
        {
            VM = vm;
        }

        protected override async Task OnInitializedAsync()
        {
            await VM.LoadVoucherDetailsAsync(VoucherName);
        }

    }
}
