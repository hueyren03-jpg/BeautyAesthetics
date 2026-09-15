using Microsoft.AspNetCore.Components;
using System;
using System.Collections.Generic;
using System.Text;
using SenangRetails.Shared.Models;

namespace SenangRetails.Shared.Components.Modals.Work
{
    public partial class CustomerDetailsModal
    {
        [Parameter] public bool Visible { get; set; }
        [Parameter] public EventCallback OnClose { get; set; }

        [Parameter] public MemberDetailsDM MemberModel { get; set; } = new MemberDetailsDM();

        [Parameter] public List<ReceiptDM> ReceiptList { get; set; } = new();

        [Parameter] public EventCallback<ReceiptDM> OnOpenReceiptHistory { get; set; }

        private async Task HandleReceiptClick(ReceiptDM receipt)
        {
            if (OnOpenReceiptHistory.HasDelegate)
            {
                await OnOpenReceiptHistory.InvokeAsync(receipt);
            }
        }

    }
}
