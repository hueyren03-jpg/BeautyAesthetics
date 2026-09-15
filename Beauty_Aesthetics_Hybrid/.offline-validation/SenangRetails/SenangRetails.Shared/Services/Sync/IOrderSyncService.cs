using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace SenangRetails.Shared.Services.Sync
{
    public interface IOrderSyncService
    {
        bool IsSyncing { get; }
        event Action? OnSyncStatusChanged;
        Task<(int TotalSynced, int TotalFailed, List<string> ErrorMessages)> SyncPendingOrdersAsync();
        Task<int> GetPendingCountAsync();
        Task TryAutoSyncAsync();
    }
}
