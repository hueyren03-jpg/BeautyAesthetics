using System.Globalization;
using Beauty_Aesthetics_WebPos.Models.DTOs;
using EBI.DM;
using EBI.Enum;

namespace Beauty_Aesthetics_WebPos.Components.Services.Inventory;

internal static class InventoryMembershipCreditMapper
{
    public static IReadOnlyList<InventoryMembershipCreditSummary> Read(
        InventoryPackageLoadDTO? fullRecord,
        InventoryDM header,
        bool allowLegacyPrimaryCredit = true)
    {
        var inventory = fullRecord?.ObjInventory;
        var configured = inventory?.MembershipCredits is { Count: > 0 }
            ? inventory.MembershipCredits
            : fullRecord?.MembershipCredits;
        if (configured is { Count: > 0 } && (allowLegacyPrimaryCredit ||
            (inventory?.MembershipCredit is null && header.MembershipCredit is null)))
        {
            return configured
                .Where(credit => !string.IsNullOrWhiteSpace(credit.MemberTypeId))
                .GroupBy(credit => credit.MemberTypeId.Trim(), StringComparer.OrdinalIgnoreCase)
                .Select(group => group.Last())
                .Where(credit => !IsDeleted(credit.SaveAction))
                .Select(credit => new InventoryMembershipCreditSummary(
                    credit.MemberTypeId.Trim(), Math.Max(0m, credit.MemberCredit)))
                .ToList();
        }

        // Senang reads this API field as MemberTypeID,Amount|MemberTypeID,Amount.
        var encoded = inventory?.MembershipCredit ?? header.MembershipCredit;
        var parsed = new List<InventoryMembershipCreditSummary>();
        foreach (var row in (encoded ?? string.Empty).Split(
                     '|', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var columns = row.Split(',', StringSplitOptions.TrimEntries);
            if (columns.Length >= 2 && !string.IsNullOrWhiteSpace(columns[0]) &&
                decimal.TryParse(columns[1], NumberStyles.Any, CultureInfo.InvariantCulture, out var amount))
            {
                parsed.Add(new InventoryMembershipCreditSummary(columns[0], Math.Max(0m, amount)));
            }
        }

        if (parsed.Count > 0)
        {
            return parsed
                .GroupBy(credit => credit.MemberTypeId, StringComparer.OrdinalIgnoreCase)
                .Select(group => group.Last())
                .ToList();
        }

        // Packages keep the non-credit membership trigger separate from credit grants.
        if (!allowLegacyPrimaryCredit)
        {
            return [];
        }

        // Older top-up API responses expose only the primary credit assignment.
        var memberTypeId = inventory?.TriggeredMemberTypeId
            ?? header.GetType().GetProperty("TriggeredMemberTypeID")?.GetValue(header)?.ToString();
        return string.IsNullOrWhiteSpace(memberTypeId)
            ? []
            : [new InventoryMembershipCreditSummary(memberTypeId.Trim(),
                Math.Max(0m, inventory?.MemberMainAccountCredit ?? Convert.ToDecimal(header.MemberMainAccountCredit)))];
    }

    public static int ReadExpiryDays(InventoryPackageLoadDTO? fullRecord, InventoryDM header) =>
        Math.Max(0, fullRecord?.ObjInventory?.MemberExpiryDays
            ?? Convert.ToInt32(header.GetType().GetProperty("MemberExpiryDays")?.GetValue(header) ?? 0));

    public static string Encode(IEnumerable<InventoryMembershipCreditDTO> credits) =>
        string.Join("|", credits
            .Where(credit => !string.IsNullOrWhiteSpace(credit.MemberTypeId))
            .GroupBy(credit => credit.MemberTypeId.Trim(), StringComparer.OrdinalIgnoreCase)
            .Select(group => group.Last())
            .Where(credit => !IsDeleted(credit.SaveAction) && credit.MemberCredit > 0m)
            .Select(credit =>
                $"{credit.MemberTypeId.Trim()},{Math.Max(0m, credit.MemberCredit).ToString("0.##", CultureInfo.InvariantCulture)}"));

    public static bool IsDeleted(string? saveAction) =>
        string.Equals(saveAction, "Deleted", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(saveAction, ((int)EntityState.Deleted).ToString(CultureInfo.InvariantCulture), StringComparison.Ordinal);
}
