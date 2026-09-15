using System;
using System.Collections.Generic;
using System.Text;

namespace SenangRetails.Shared.Services
{
    public class EInvoiceSetupGuideService
    {
        public bool IsOpen { get; private set; }

        public event Action? OnChange;

        public void Open()
        {
            IsOpen = true;
            NotifyStateChanged();
        }

        public void Close()
        {
            IsOpen = false;
            NotifyStateChanged();
        }

        private void NotifyStateChanged()
        {
            OnChange?.Invoke();
        }
    }
}

