using Microsoft.AspNetCore.Components;
using System;
using System.Collections.Generic;
using System.Text;
using SenangRetails.Shared.Models;

namespace SenangRetails.Shared.Components.Modals.Work
{
    public partial class ItemDetailsModal
    {
        [Parameter] public bool Visible { get; set; }
        [Parameter] public EventCallback OnClose { get; set; }

        [Parameter] public ServiceItemDM ItemModel { get; set; } = new();

        [Parameter] public ReceiptDM ReceiptModel { get; set; } = new();

        [Parameter] public EventCallback<ReceiptDM> OnOpenReceiptHistory { get; set; }

        private async Task HandleReceiptClick()
        {
            if (OnOpenReceiptHistory.HasDelegate)
            {
                await OnOpenReceiptHistory.InvokeAsync(ReceiptModel);
            }
        }
    }
}
