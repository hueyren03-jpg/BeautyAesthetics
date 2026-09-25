using System.Text.Json;
using System.Text.Json.Nodes;
using Beauty_Aesthetics_WebPos.APIClient;
using EBI.Enum;
using Microsoft.JSInterop;

namespace Beauty_Aesthetics_WebPos.Components.Services.Inventory;

public interface IProductSetupAssignmentService
{
    Task<IReadOnlyList<ProductPriceGroupOption>> GetPriceGroupsAsync();
    Task<IReadOnlyList<ProductPromotionOption>> GetPromotionsAsync(CancellationToken cancellationToken = default);
    Task<string?> GetAssignedPriceGroupCodeAsync(string productId);
    Task<string?> GetAssignedPromotionCodeAsync(string productId, CancellationToken cancellationToken = default);
    Task<bool> AssignPriceGroupAsync(string productId, string? priceGroupCode);
    Task<(bool Success, string? Error)> AssignPromotionAsync(
        string productId,
        string? promotionCode,
        CancellationToken cancellationToken = default);
}

public sealed record ProductPriceGroupOption(
    string Code,
    string Name,
    decimal Price,
    decimal MlmCommRate,
    decimal MlmSalesTarget,
    List<string> VisibleBranchIds,
    List<string> AppliedProductIds);

public sealed record ProductPromotionOption(
    string MasterAccountId,
    string Code,
    string Name,
    bool IsActive);

public sealed class ProductSetupAssignmentService : IProductSetupAssignmentService
{
    private const string PriceGroupStorageKey = "beauty_price_groups_data";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly IJSRuntime js;
    private readonly InventoryAC inventoryAC;
    private readonly ServiceInventoryAC serviceInventoryAC;

    public ProductSetupAssignmentService(
        IJSRuntime js,
        InventoryAC inventoryAC,
        ServiceInventoryAC serviceInventoryAC)
    {
        this.js = js;
        this.inventoryAC = inventoryAC;
        this.serviceInventoryAC = serviceInventoryAC;
    }

    public async Task<IReadOnlyList<ProductPriceGroupOption>> GetPriceGroupsAsync()
    {
        try
        {
            var json = await js.InvokeAsync<string?>("localStorage.getItem", PriceGroupStorageKey);
            if (!string.IsNullOrWhiteSpace(json))
            {
                var stored = JsonSerializer.Deserialize<List<ProductPriceGroupOption>>(json, JsonOptions);
                if (stored is not null)
                {
                    return NormalizePriceGroups(stored);
                }
            }
        }
        catch
        {
            // Use the same starter groups as SenangRetails if browser storage is unavailable/empty.
        }

        var seed = new List<ProductPriceGroupOption>
        {
            new("PG-VIP", "VIP Member Price Group", 18.00m, 5.0m, 1000.0m, ["HQ", "B01"], []),
            new("PG-REG", "Standard Retail Price Group", 25.00m, 2.0m, 500.0m, ["HQ", "B01", "B02"], [])
        };

        await SavePriceGroupsAsync(seed);
        return seed;
    }

    public async Task<string?> GetAssignedPriceGroupCodeAsync(string productId)
    {
        if (string.IsNullOrWhiteSpace(productId))
        {
            return null;
        }

        var groups = await GetPriceGroupsAsync();
        return groups.FirstOrDefault(group =>
            group.AppliedProductIds.Any(id =>
                string.Equals(id, productId.Trim(), StringComparison.OrdinalIgnoreCase)))?.Code;
    }

    public async Task<bool> AssignPriceGroupAsync(string productId, string? priceGroupCode)
    {
        if (string.IsNullOrWhiteSpace(productId))
        {
            return false;
        }

        var groups = (await GetPriceGroupsAsync()).ToList();
        var selectedCode = priceGroupCode?.Trim() ?? string.Empty;

        if (!string.IsNullOrWhiteSpace(selectedCode) &&
            !groups.Any(group => string.Equals(group.Code, selectedCode, StringComparison.OrdinalIgnoreCase)))
        {
            return false;
        }

        var normalizedProductId = productId.Trim();
        var updated = new List<ProductPriceGroupOption>(groups.Count);

        foreach (var group in groups)
        {
            var ids = group.AppliedProductIds
                .Where(id => !string.Equals(id, normalizedProductId, StringComparison.OrdinalIgnoreCase))
                .ToList();

            if (!string.IsNullOrWhiteSpace(selectedCode) &&
                string.Equals(group.Code, selectedCode, StringComparison.OrdinalIgnoreCase))
            {
                ids.Add(normalizedProductId);
            }

            updated.Add(group with { AppliedProductIds = ids });
        }

        return await SavePriceGroupsAsync(updated);
    }

    public async Task<IReadOnlyList<ProductPromotionOption>> GetPromotionsAsync(
        CancellationToken cancellationToken = default)
    {
        var result = await inventoryAC.LoadProxyAsync(null, cancellationToken);
        if (!result.Success || result.Value is null)
        {
            return [];
        }

        return result.Value
            .Where(item => item.InventoryTypeID == 8 && !string.IsNullOrWhiteSpace(item.MasterAccountID))
            .Select(item => new ProductPromotionOption(
                item.MasterAccountID ?? string.Empty,
                item.DisplayCode ?? item.MasterAccountID ?? string.Empty,
                item.AccountName ?? item.DisplayCode ?? "Promotion",
                item.IsSold && !string.Equals(item.AccountStatus, "Inactive", StringComparison.OrdinalIgnoreCase)))
            .OrderBy(item => item.Name)
            .ThenBy(item => item.Code)
            .ToList();
    }

    public async Task<string?> GetAssignedPromotionCodeAsync(
        string productId,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(productId))
        {
            return null;
        }

        foreach (var promotion in await GetPromotionsAsync(cancellationToken))
        {
            var raw = await serviceInventoryAC.LoadFullRawAsync(promotion.MasterAccountId, cancellationToken);
            if (!raw.Success)
            {
                continue;
            }

            if (GetAssignedProductIds(raw.Value).Contains(productId.Trim(), StringComparer.OrdinalIgnoreCase))
            {
                return promotion.Code;
            }
        }

        return null;
    }

    public async Task<(bool Success, string? Error)> AssignPromotionAsync(
        string productId,
        string? promotionCode,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(productId))
        {
            return (false, "Product ID is required.");
        }

        var promotions = await GetPromotionsAsync(cancellationToken);
        var selectedCode = promotionCode?.Trim() ?? string.Empty;

        if (!string.IsNullOrWhiteSpace(selectedCode) &&
            !promotions.Any(promotion =>
                string.Equals(promotion.Code, selectedCode, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(promotion.MasterAccountId, selectedCode, StringComparison.OrdinalIgnoreCase)))
        {
            return (false, "Promotion was not found.");
        }

        foreach (var promotion in promotions)
        {
            var shouldContain = !string.IsNullOrWhiteSpace(selectedCode) &&
                (string.Equals(promotion.Code, selectedCode, StringComparison.OrdinalIgnoreCase) ||
                 string.Equals(promotion.MasterAccountId, selectedCode, StringComparison.OrdinalIgnoreCase));

            var raw = await serviceInventoryAC.LoadFullRawAsync(promotion.MasterAccountId, cancellationToken);
            if (!raw.Success)
            {
                return (false, raw.ErrorMessage ?? $"Unable to load promotion {promotion.Name}.");
            }

            var currentIds = GetAssignedProductIds(raw.Value);
            var contains = currentIds.Contains(productId.Trim(), StringComparer.OrdinalIgnoreCase);
            if (contains == shouldContain)
            {
                continue;
            }

            var updated = BuildPromotionAssignmentPayload(raw.Value, productId.Trim(), shouldContain);
            if (updated is null)
            {
                if (shouldContain)
                {
                    return (false, $"Promotion {promotion.Name} has no active rule to update.");
                }

                continue;
            }

            var save = await serviceInventoryAC.UpdateFullRawAsync(updated.Value, cancellationToken);
            if (!save.Success)
            {
                return (false, save.ErrorMessage ?? $"Unable to update promotion {promotion.Name}.");
            }
        }

        return (true, null);
    }

    private async Task<bool> SavePriceGroupsAsync(List<ProductPriceGroupOption> groups)
    {
        try
        {
            var normalized = NormalizePriceGroups(groups);
            var json = JsonSerializer.Serialize(normalized, JsonOptions);
            await js.InvokeVoidAsync("localStorage.setItem", PriceGroupStorageKey, json);
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static List<ProductPriceGroupOption> NormalizePriceGroups(
        IEnumerable<ProductPriceGroupOption> groups) =>
        groups
            .Where(group => !string.IsNullOrWhiteSpace(group.Code))
            .Select(group => group with
            {
                Code = group.Code.Trim().ToUpperInvariant(),
                Name = group.Name?.Trim() ?? string.Empty,
                Price = Math.Max(0m, group.Price),
                MlmCommRate = Math.Clamp(group.MlmCommRate, 0m, 100m),
                MlmSalesTarget = Math.Max(0m, group.MlmSalesTarget),
                VisibleBranchIds = (group.VisibleBranchIds ?? [])
                    .Where(value => !string.IsNullOrWhiteSpace(value))
                    .Select(value => value.Trim())
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToList(),
                AppliedProductIds = (group.AppliedProductIds ?? [])
                    .Where(value => !string.IsNullOrWhiteSpace(value))
                    .Select(value => value.Trim())
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToList()
            })
            .OrderBy(group => group.Code)
            .ToList();

    private static HashSet<string> GetAssignedProductIds(JsonElement raw)
    {
        var result = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var node = JsonNode.Parse(raw.GetRawText()) as JsonObject;
        if (node is null)
        {
            return result;
        }

        foreach (var rules in GetPackageRuleArrays(node))
        {
            foreach (var ruleNode in rules)
            {
                if (ruleNode is not JsonObject rule || GetBoolean(rule, "isVoided"))
                {
                    continue;
                }

                foreach (var id in ParseOptionValues(GetString(rule, "optionItems")))
                {
                    result.Add(id);
                }

                var inventoryId = GetString(rule, "inventoryID");
                if (!string.IsNullOrWhiteSpace(inventoryId))
                {
                    result.Add(inventoryId);
                }
            }
        }

        return result;
    }

    private static JsonElement? BuildPromotionAssignmentPayload(
        JsonElement raw,
        string productId,
        bool shouldContain)
    {
        var root = JsonNode.Parse(raw.GetRawText()) as JsonObject;
        if (root is null)
        {
            return null;
        }

        var arrays = GetPackageRuleArrays(root).ToList();
        if (arrays.Count == 0)
        {
            return null;
        }

        var changedAny = false;
        foreach (var rules in arrays)
        {
            foreach (var ruleNode in rules)
            {
                if (ruleNode is not JsonObject rule || GetBoolean(rule, "isVoided"))
                {
                    continue;
                }

                var ids = ParseOptionValues(GetString(rule, "optionItems")).ToList();
                var inventoryId = GetString(rule, "inventoryID");
                if (ids.Count == 0 && !string.IsNullOrWhiteSpace(inventoryId))
                {
                    ids.Add(inventoryId);
                }

                var contains = ids.Contains(productId, StringComparer.OrdinalIgnoreCase);
                if (contains == shouldContain)
                {
                    continue;
                }

                if (shouldContain)
                {
                    ids.Add(productId);
                }
                else
                {
                    ids.RemoveAll(id => string.Equals(id, productId, StringComparison.OrdinalIgnoreCase));
                }

                ids = ids
                    .Where(id => !string.IsNullOrWhiteSpace(id))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToList();

                SetValue(rule, "optionItems", string.Join(",", ids));
                SetValue(rule, "inventoryID", ids.FirstOrDefault() ?? string.Empty);
                SetValue(rule, "isDirty", true);
                SetValue(rule, "saveAction", (int)EntityState.Changed);
                changedAny = true;
            }
        }

        if (!changedAny)
        {
            return null;
        }

        if (GetObject(root, "objInventory") is { } inventory)
        {
            SetValue(inventory, "hasPackage", true);
            SetValue(inventory, "isDirty", true);
            SetValue(inventory, "saveAction", (int)EntityState.Changed);
        }

        using var document = JsonDocument.Parse(root.ToJsonString(JsonOptions));
        return document.RootElement.Clone();
    }

    private static IEnumerable<JsonArray> GetPackageRuleArrays(JsonObject root)
    {
        if (GetObject(root, "objInventory") is { } inventory &&
            GetNode(inventory, "lstPackage") is JsonArray nestedRules)
        {
            yield return nestedRules;
        }

        if (GetNode(root, "lstPackage") is JsonArray rootRules)
        {
            yield return rootRules;
        }
    }

    private static IReadOnlyList<string> ParseOptionValues(string? rawValue) =>
        string.IsNullOrWhiteSpace(rawValue)
            ? []
            : rawValue
                .Split([',', ';', '|', '_'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(value => value.Trim(' ', '[', ']', '"', '\''))
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

    private static JsonNode? GetNode(JsonObject source, string name)
    {
        foreach (var pair in source)
        {
            if (string.Equals(pair.Key, name, StringComparison.OrdinalIgnoreCase))
            {
                return pair.Value;
            }
        }

        return null;
    }

    private static JsonObject? GetObject(JsonObject source, string name) =>
        GetNode(source, name) as JsonObject;

    private static string GetString(JsonObject source, string name)
    {
        var node = GetNode(source, name);
        if (node is null)
        {
            return string.Empty;
        }

        try
        {
            return node.GetValue<string>() ?? string.Empty;
        }
        catch
        {
            return node.ToJsonString().Trim('"');
        }
    }

    private static bool GetBoolean(JsonObject source, string name)
    {
        var node = GetNode(source, name);
        if (node is null)
        {
            return false;
        }

        try
        {
            return node.GetValue<bool>();
        }
        catch
        {
            return bool.TryParse(node.ToJsonString().Trim('"'), out var result) && result;
        }
    }

    private static void SetValue<T>(JsonObject source, string name, T value)
    {
        var actualName = source.Select(pair => pair.Key)
            .FirstOrDefault(key => string.Equals(key, name, StringComparison.OrdinalIgnoreCase))
            ?? name;
        source[actualName] = JsonValue.Create(value);
    }
}
