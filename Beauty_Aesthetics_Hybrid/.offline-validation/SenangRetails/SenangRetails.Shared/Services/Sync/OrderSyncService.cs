using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using EBI.UC;
using SenangRetails.Shared.ApiClient;
using SenangRetails.Shared.Services.DataLayer.Offline;

namespace SenangRetails.Shared.Services.Sync
{
    public class OrderSyncService : IOrderSyncService, IDisposable
    {
        private readonly IOfflineCashSalesStorage _offlineStorage;
        private readonly CashSalesAC _ac;
        private bool _isSyncing = false;
        private readonly Timer? _autoSyncTimer;

        public bool IsSyncing => _isSyncing;
        public event Action? OnSyncStatusChanged;

        public OrderSyncService(IOfflineCashSalesStorage offlineStorage, CashSalesAC ac)
        {
            _offlineStorage = offlineStorage;
            _ac = ac;

            // Automatically check and sync every 8 seconds if pending orders exist
            _autoSyncTimer = new Timer(async _ =>
            {
                await TryAutoSyncAsync();
            }, null, TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(8));
        }

        public async Task TryAutoSyncAsync()
        {
            if (_isSyncing) return;
            try
            {
                var count = await _offlineStorage.GetPendingCountAsync();
                if (count > 0)
                {
                    await SyncPendingOrdersAsync();
                }
            }
            catch
            {
                // Silently wait for the next cycle if offline
            }
        }

        public async Task<int> GetPendingCountAsync()
        {
            try
            {
                return await _offlineStorage.GetPendingCountAsync();
            }
            catch
            {
                return 0;
            }
        }

        public async Task<(int TotalSynced, int TotalFailed, List<string> ErrorMessages)> SyncPendingOrdersAsync()
        {
            if (_isSyncing)
            {
                return (0, 0, new List<string> { "Sync is already in progress." });
            }

            _isSyncing = true;
            OnSyncStatusChanged?.Invoke();

            int synced = 0;
            int failed = 0;
            var errors = new List<string>();

            try
            {
                var pendingSales = await _offlineStorage.GetPendingOfflineSalesAsync();
                if (pendingSales.Count == 0)
                {
                    return (0, 0, errors);
                }

                foreach (var sale in pendingSales)
                {
                    try
                    {
                        var request = JsonSerializer.Deserialize<Doc_CashSales>(sale.OrderPayloadJson);
                        if (request == null)
                        {
                            await _offlineStorage.MarkSaleFailedAsync(sale.LocalId, "Failed to deserialize offline order payload.");
                            failed++;
                            continue;
                        }

                        var response = await _ac.CreateCashSalesRecordAsync(request);

                        if (response?.StatusCode == 200 && response.Result != null)
                        {
                            await _offlineStorage.MarkSaleSyncedAsync(
                                sale.LocalId,
                                response.Result.Id ?? "",
                                response.Result.DisplayCode ?? sale.LocalDisplayCode);
                            synced++;
                        }
                        else
                        {
                            var msg = response?.Message ?? "Server rejected order.";
                            await _offlineStorage.MarkSaleFailedAsync(sale.LocalId, msg);
                            errors.Add($"Order {sale.LocalDisplayCode}: {msg}");
                            failed++;
                        }
                    }
                    catch (Exception ex)
                    {
                        var msg = ex.Message;
                        await _offlineStorage.MarkSaleFailedAsync(sale.LocalId, msg);
                        errors.Add($"Order {sale.LocalDisplayCode}: {msg}");
                        failed++;
                    }
                }
            }
            catch (Exception ex)
            {
                errors.Add($"Sync process error: {ex.Message}");
            }
            finally
            {
                _isSyncing = false;
                OnSyncStatusChanged?.Invoke();
            }

            return (synced, failed, errors);
        }

        public void Dispose()
        {
            _autoSyncTimer?.Dispose();
        }
    }
}
