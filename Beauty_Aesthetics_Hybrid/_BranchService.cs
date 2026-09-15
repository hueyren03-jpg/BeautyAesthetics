using SenangRetails.Shared.ApiClient;
using SenangRetails.Shared.Models.DTOs;
using SenangRetails.Shared.Data;
using SenangRetails.Shared.Services.Connectivity;
using System;
using System.Collections.Generic;
using System.Text;

namespace SenangRetails.Shared.Services.BranchService
{
    public class BranchService : IBranchService
    {
        private readonly BranchAC _ac;
        private readonly INetworkStatusService _network;

        public BranchService(BranchAC ac, INetworkStatusService network)
        {
            _ac = ac;
            _network = network;
        }

        public async Task<(BranchItem? item, string message)> GetBranchDetailsAsync(string branchId)
        {
            var cacheKey = $"branch:{branchId}";
            if (_network.IsInternetAvailable)
            {
                try
                {
                    var response = await _ac.LoadRecordAsync(branchId);
                    if (response?.statusCode == 200 && response.result != null)
                    {
                        await LocalDataCacheStore.SetAsync(cacheKey, response.result);
                        return (response.result, "");
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[BranchService] Online load failed: {ex.Message}");
                }
            }

            var cached = await LocalDataCacheStore.GetAsync<BranchItem>(cacheKey);
            return cached != null
                ? (cached, "Loaded from offline cache.")
                : (null, "Branch details are not available offline yet. Connect once to download them.");
        }

        public async Task<(bool success, string message)> UpdateBranchAsync(BranchItem item)
        {
            if (!_network.IsInternetAvailable)
                return (false, "Internet connection is required to update branch settings.");

            var response = await _ac.UpdateRecordAsync(item);
            if (response == null) return (false, "No response from server.");
            if (response.statusCode == 200)
                await LocalDataCacheStore.SetAsync($"branch:{item.BranchID}", item);
            return (response.statusCode == 200, response.message ?? "Unknown error.");
        }
    }
}
