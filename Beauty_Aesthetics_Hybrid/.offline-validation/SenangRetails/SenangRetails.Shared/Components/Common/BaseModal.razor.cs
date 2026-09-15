using Microsoft.AspNetCore.Components;
using System;
using System.Collections.Generic;
using System.Text;

namespace SenangRetails.Shared.Components.Common
{
    public partial class BaseModal
    {

        [Parameter] public bool Visible { get; set; }
        [Parameter] public EventCallback OnClose { get; set; }
        [Parameter] public RenderFragment ChildContent { get; set; }
        [Parameter] public ModalSize Size { get; set; } = ModalSize.Medium;

        string SizeClass => Size switch
        {
            ModalSize.Small => "modal-sm",
            ModalSize.Large => "modal-lg",
            _ => "modal-md"
        };

        async Task HandleOverlayClick()
        {
            await OnClose.InvokeAsync();
        }

        public enum ModalSize
        {
            Small,
            Medium,
            Large
        }
    }
}
