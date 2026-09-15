using Microsoft.AspNetCore.Components;
using System;
using System.Collections.Generic;
using System.Text;

namespace SenangRetails.Shared.Components.Modals.Work
{
    public partial class StaffDetailModal
    {

        [Parameter] public bool Visible { get; set; }
        [Parameter] public EventCallback OnClose { get; set; }
        [Parameter] public string StaffName { get; set; } = "Effie";

        private string activeTab = "Item";
        private decimal TotalEffort = 209.00m;
        private decimal SaleEffort = 209.00m;
    }
}
