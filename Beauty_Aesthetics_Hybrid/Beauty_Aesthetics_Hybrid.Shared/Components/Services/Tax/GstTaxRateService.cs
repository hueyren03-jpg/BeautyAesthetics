using Beauty_Aesthetics_WebPos.APIClient;
using Beauty_Aesthetics_WebPos.Components.Services;
using Beauty_Aesthetics_WebPos.Models.DTOs;
using System.Text.Json.Nodes;

namespace Beauty_Aesthetics_WebPos.Components.Services.Tax;

public sealed class GstTaxRateService : IGstTaxRateService
{
    private readonly GSTTaxCodeAC gstTaxCodeAC;
    private readonly BranchAC branchAC;
    private readonly AppState appState;
    private readonly Dictionary<string, IReadOnlyList<GstTaxCodeDTO>> cache =
        new(StringComparer.OrdinalIgnoreCase);

    public GstTaxRateService(GSTTaxCodeAC gstTaxCodeAC, BranchAC branchAC, AppState appState)
    {
        this.gstTaxCodeAC = gstTaxCodeAC;
        this.branchAC = branchAC;
        this.appState = appState;
    }

    public async Task<decimal> GetRateForTaxCodeAsync(
        string? taxCodeId,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(taxCodeId)) return 0m;

        var branchId = string.IsNullOrWhiteSpace(appState.SelectedBranchID)
            ? "HQ"
            : appState.SelectedBranchID!;

        var branch = await branchAC.LoadRecordAsync(branchId, cancellationToken);
        if (!branch.Success || branch.Value is null) return 0m;

        var taxTypeId = FindText(branch.Value, "TaxTypeID", "TaxTypeId", "taxTypeID", "taxTypeId");
        if (string.IsNullOrWhiteSpace(taxTypeId)) return 0m;

        if (!cache.TryGetValue(taxTypeId, out var codes))
        {
            var response = await gstTaxCodeAC.LoadProxyByParentIdAsync(taxTypeId, cancellationToken);
            if (!response.Success || response.Value is null) return 0m;
            codes = response.Value.Where(code => code.Active).ToList();
            cache[taxTypeId] = codes;
        }

        return codes.FirstOrDefault(code =>
            string.Equals(code.TaxCodeID, taxCodeId, StringComparison.OrdinalIgnoreCase))?.TaxRate ?? 0m;
    }

    private static string? FindText(JsonNode? node, params string[] names)
    {
        if (node is JsonObject obj)
        {
            foreach (var property in obj)
            {
                if (names.Any(name => string.Equals(name, property.Key, StringComparison.OrdinalIgnoreCase)) &&
                    property.Value is JsonValue value &&
                    value.TryGetValue<string>(out var text) &&
                    !string.IsNullOrWhiteSpace(text))
                    return text;
            }

            foreach (var property in obj)
            {
                var nested = FindText(property.Value, names);
                if (!string.IsNullOrWhiteSpace(nested)) return nested;
            }
        }
        else if (node is JsonArray array)
        {
            foreach (var child in array)
            {
                var nested = FindText(child, names);
                if (!string.IsNullOrWhiteSpace(nested)) return nested;
            }
        }

        return null;
    }
}
