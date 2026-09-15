using SenangRetails.Shared.ApiClient;
using SenangRetails.Shared.Models.DTOs;
using SenangRetails.Shared.Data;
using SenangRetails.Shared.Services.Connectivity;

namespace SenangRetails.Shared.Services.SupportingTableService
{
    public class SupportingTableService : ISupportingTableService
    {
        private readonly SupportingTableAC _ac;
        private readonly INetworkStatusService _network;

        public SupportingTableService(SupportingTableAC ac, INetworkStatusService network)
        {
            _ac = ac;
            _network = network;
        }

        public async Task<List<SupportingTableItem>?> LoadListByTypeAsync(int typeId)
        {
            var cacheKey = $"supporting-table:{typeId}";
            if (_network.IsInternetAvailable)
            {
                try
                {
                    var response = await _ac.LoadListByTypeAsync(typeId);
                    if (response?.statusCode == 200 && response.result != null)
                    {
                        await LocalDataCacheStore.SetAsync(cacheKey, response.result);
                        return response.result;
                    }
                    System.Diagnostics.Debug.WriteLine($"[SupportingTableService] LoadListByType({typeId}) failed: {response?.message}");
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[SupportingTableService] Online load failed: {ex.Message}");
                }
            }

            return await LocalDataCacheStore.GetAsync<List<SupportingTableItem>>(cacheKey) ?? new();
        }

        public async Task<(bool Success, string Message)> CreateAsync(SupportingTableModel model)
        {
            if (!_network.IsInternetAvailable)
                return (false, "Internet connection is required to create categories.");
            var response = await _ac.CreateAsync(model);
            if (response?.statusCode == 200)
                return (true, response.message ?? "Category created successfully.");
            return (false, response?.message ?? "Failed to create category.");
        }

        public async Task<(bool Success, string Message)> UpdateAsync(SupportingTableModel model)
        {
            if (!_network.IsInternetAvailable)
                return (false, "Internet connection is required to update categories.");
            var response = await _ac.UpdateAsync(model);
            if (response?.statusCode == 200)
                return (true, response.message ?? "Category updated successfully.");
            return (false, response?.message ?? "Failed to update category.");
        }

        public async Task<(bool Success, string Message)> DeleteAsync(string supportingTableId)
        {
            if (!_network.IsInternetAvailable)
                return (false, "Internet connection is required to delete categories.");
            var response = await _ac.DeleteAsync(supportingTableId);
            if (response?.statusCode == 200)
                return (true, response.message ?? "Category deleted successfully.");
            return (false, response?.message ?? "Failed to delete category.");
        }
    }
}
