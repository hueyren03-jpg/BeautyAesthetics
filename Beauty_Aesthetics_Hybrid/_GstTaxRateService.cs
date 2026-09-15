using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using SenangRetails.Shared.ApiClient;
using SenangRetails.Shared.Models.DTOs;
using SenangRetails.Shared.Services.BranchService;
using SenangRetails.Shared.Data;
using SenangRetails.Shared.Services.Connectivity;

namespace SenangRetails.Shared.Services.TaxRateService
{
    public class GstTaxRateService : IGstTaxRateService
    {
        private readonly GSTTaxCodeAC _gstTaxCodeAc;
        private readonly IBranchService _branchService;
        private readonly AppState _appState;
        private readonly INetworkStatusService _network;

        private readonly Dictionary<string, IReadOnlyList<GstTaxCodeDto>> _codesByTaxType = new(StringComparer.OrdinalIgnoreCase);
        private readonly SemaphoreSlim _loadLock = new(1, 1);

        public GstTaxRateService(GSTTaxCodeAC gstTaxCodeAc, IBranchService branchService, AppState appState, INetworkStatusService network)
        {
            _gstTaxCodeAc = gstTaxCodeAc;
            _branchService = branchService;
            _appState = appState;
            _network = network;
        }

        public async Task<decimal> GetRateForTaxCodeAsync(string? taxCodeId, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(taxCodeId)) return 0m;

            var branchId = string.IsNullOrEmpty(_appState.SelectedBranchID) ? "HQ" : _appState.SelectedBranchID;

            var (branchItem, message) = await _branchService.GetBranchDetailsAsync(branchId).ConfigureAwait(false);

            var taxTypeId = branchItem?.TaxTypeID;

            if (string.IsNullOrWhiteSpace(taxTypeId))
            {
                Console.WriteLine(message);
                return 0m;
            }

            var codes = await EnsureCodesAsync(taxTypeId, cancellationToken).ConfigureAwait(false);
            var row = codes.FirstOrDefault(c => string.Equals(c.TaxCodeID, taxCodeId, StringComparison.OrdinalIgnoreCase));
            return row?.TaxRate ?? 0m;
        }

        private async Task<IReadOnlyList<GstTaxCodeDto>> EnsureCodesAsync(string taxTypeId, CancellationToken cancellationToken)
        {
            if (_codesByTaxType.TryGetValue(taxTypeId, out var cached))
                return cached;

            await _loadLock.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                if (_codesByTaxType.TryGetValue(taxTypeId, out cached))
                    return cached;

                IReadOnlyList<GstTaxCodeDto> list = Array.Empty<GstTaxCodeDto>();
                var cacheKey = $"tax-codes:{taxTypeId}";

                if (_network.IsInternetAvailable)
                {
                    try
                    {
                        var resp = await _gstTaxCodeAc.LoadProxyByParentID(taxTypeId).ConfigureAwait(false);
                        if (resp?.statusCode == 200 && resp.result != null)
                        {
                            list = resp.result;
                            await LocalDataCacheStore.SetAsync(cacheKey, resp.result).ConfigureAwait(false);
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"[GstTaxRateService] Online load failed: {ex.Message}");
                    }
                }

                if (list.Count == 0)
                    list = await LocalDataCacheStore.GetAsync<List<GstTaxCodeDto>>(cacheKey).ConfigureAwait(false)
                        ?? new List<GstTaxCodeDto>();

                _codesByTaxType[taxTypeId] = list;
                return list;
            }
            finally
            {
                _loadLock.Release();
            }
        }
    }
}
