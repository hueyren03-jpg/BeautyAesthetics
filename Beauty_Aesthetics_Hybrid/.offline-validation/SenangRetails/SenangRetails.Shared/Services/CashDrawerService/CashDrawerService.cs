using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.JSInterop;
using SenangRetails.Shared.Models.DTOs;

namespace SenangRetails.Shared.Services.CashDrawerService
{
    public class CashDrawerService : ICashDrawerService
    {
        private readonly IJSRuntime _js;
        private const string StorageKey = "senang_cash_drawer_logs";

        private static readonly List<CashDrawerLogModel> SeedLogs = new()
        {
            new CashDrawerLogModel
            {
                Type = "Cash In",
                Amount = 200.00m,
                Reason = "Starting Float",
                Notes = "Morning register float top up",
                PerformedBy = "Manager",
                Timestamp = DateTime.Now.AddHours(-4)
            }
        };

        public CashDrawerService(IJSRuntime js)
        {
            _js = js;
        }

        public async Task<List<CashDrawerLogModel>> GetLogsAsync()
        {
            try
            {
                var json = await _js.InvokeAsync<string?>("localStorage.getItem", StorageKey);
                if (!string.IsNullOrWhiteSpace(json))
                {
                    var logs = JsonSerializer.Deserialize<List<CashDrawerLogModel>>(json);
                    if (logs != null && logs.Any())
                        return logs;
                }
            }
            catch
            {
            }
            return SeedLogs.ToList();
        }

        public async Task<CashDrawerSummaryModel> GetSummaryAsync()
        {
            var logs = await GetLogsAsync();
            var todayLogs = logs.Where(l => l.Timestamp.Date == DateTime.Today).ToList();

            var summary = new CashDrawerSummaryModel
            {
                OpeningFloat = 300.00m,
                TotalCashInToday = todayLogs.Where(l => l.Type == "Cash In").Sum(l => l.Amount),
                TotalCashOutToday = todayLogs.Where(l => l.Type == "Cash Out").Sum(l => l.Amount)
            };

            return summary;
        }

        public async Task<bool> RecordTransactionAsync(CashDrawerLogModel log)
        {
            var logs = await GetLogsAsync();
            logs.Insert(0, log);

            try
            {
                var json = JsonSerializer.Serialize(logs);
                await _js.InvokeVoidAsync("localStorage.setItem", StorageKey, json);
                return true;
            }
            catch
            {
                return false;
            }
        }
    }
}
