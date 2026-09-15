using Microsoft.AspNetCore.Components;
using Beauty_Aesthetics_WebPos.Components.ViewModels;

namespace Beauty_Aesthetics_WebPos.Components.Pages.Voucher
{
    public partial class VoucherDetails : ComponentBase
    {
        [Parameter] public string VoucherName { get; set; } = "";

        private VoucherDetailsViewModel VM;

        private bool showDeleteModal = false;
        private string? voucherCodeToDelete = null;

        protected void EditVoucher(string code, string voucherName)
        {
            if (!string.IsNullOrWhiteSpace(code) && !string.IsNullOrWhiteSpace(voucherName))
            {
                // Pass both as query parameters
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
        }

        // Confirm delete
        private async Task ConfirmDelete()
        {
            if (!string.IsNullOrEmpty(voucherCodeToDelete))
            {
                await VM.DeleteVoucherItemAsync(voucherCodeToDelete);
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
