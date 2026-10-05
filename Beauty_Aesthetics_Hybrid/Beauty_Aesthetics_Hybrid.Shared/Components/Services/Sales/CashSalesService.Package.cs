using System.Text.Json;
using System.Text.Json.Nodes;
using Beauty_Aesthetics_WebPos.Components.Models;
using EBI.DM;
using EBI.Enum;

namespace Beauty_Aesthetics_WebPos.Components.Services.Sales;

public sealed partial class CashSalesService
{
    private async Task<(bool Success, string ErrorMessage, JsonArray Series, JsonArray Time)> BuildPurchasedPackagesAsync(
        Transaction transaction, JsonArray documentLines, CancellationToken cancellationToken)
    {
        var series = new JsonArray();
        var time = new JsonArray();
        if (transaction.DocumentTypeId == 52) return (true, string.Empty, series, time);

        var purchases = transaction.Items.Select((item, index) => (Item: item, Index: index))
            .Where(entry => entry.Item.InventoryTypeId == 5).ToList();
        if (purchases.Count > 0 && string.IsNullOrWhiteSpace(transaction.AccountId))
            return (false, "Select a customer before purchasing a package.", series, time);

        foreach (var purchase in purchases)
        {
            var result = await packageInventoryAC.LoadFullAsync(purchase.Item.InventoryId, cancellationToken);
            var inventory = result.Value?.ObjInventory;
            if (!result.Success || inventory is null)
                return (false, result.ErrorMessage ?? "Unable to load the package contents.", series, time);
            if (string.Equals(inventory.AccountStatus, "Inactive", StringComparison.OrdinalIgnoreCase))
                return (false, $"'{purchase.Item.Name}' is inactive.", series, time);
            if (inventory.InventoryTypeId != 0 && inventory.InventoryTypeId != 5)
                return (false, $"'{purchase.Item.Name}' is not a package.", series, time);

            var contents = (inventory.PackageLines ?? result.Value?.PackageLines ?? [])
                .Where(item => !item.IsVoided && !string.IsNullOrWhiteSpace(item.InventoryId)).ToList();
            if (contents.Count == 0)
                return (false, $"'{purchase.Item.Name}' has no included items. Edit the package first.", series, time);
            if (documentLines[purchase.Index] is not JsonObject line)
                return (false, "Unable to match the package to its sale line.", series, time);

            var lineSeries = new JsonArray();
            var lineTime = new JsonArray();
            foreach (var content in contents)
            {
                if (content.Quantity <= 0m || content.PackageQuantityTypeId is not (0 or 1))
                    return (false, $"Invalid quantity/type for '{content.Description}'. Edit the package first.", series, time);

                var purchased = new CashSales_Series_UnconsumedItemDM
                {
                    DocumentID = transaction.DocumentId,
                    DocumentLineID = Text(line, "DocumentLineID"),
                    CustomerAccountID = transaction.AccountId,
                    PackageID = string.IsNullOrWhiteSpace(content.PackageId) ? purchase.Item.InventoryId : content.PackageId,
                    PackageItemID = content.AutoId.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined
                        ? string.Empty : content.AutoId.ToString(),
                    InventoryID = content.InventoryId,
                    Description = content.Description,
                    QuantityPurchased = purchase.Item.Quantity * content.Quantity,
                    QuantityRedeemed = 0m,
                    UnitPrice = content.UnitPrice,
                    TotalPrice = purchase.Item.Quantity * content.Quantity * content.UnitPrice,
                    UnitActualValue = content.UnitActualValue,
                    TotalActualValue = purchase.Item.Quantity * content.Quantity * content.UnitActualValue,
                    SourceUnitPrice = content.UnitPrice,
                    SourceUnitActualValue = content.UnitActualValue,
                    ActivityTypeID = content.IsDeferred ? 4 : 1,
                    ExpiryDate = inventory.ValidityDays > 0
                        ? transaction.Date.Date.AddDays(inventory.ValidityDays).AddHours(23).AddMinutes(59)
                        : new DateTime(2049, 12, 31, 23, 59, 0),
                    FinancialDate = transaction.Date,
                    BranchID = transaction.BranchId,
                    GroupID = transaction.GroupId,
                    SaveAction = EntityState.Added,
                    IsDirty = true
                };
                var node = JsonSerializer.SerializeToNode(purchased)!;
                if (content.PackageQuantityTypeId == 0)
                {
                    lineSeries.Add(node);
                    series.Add(node.DeepClone());
                }
                else
                {
                    lineTime.Add(node);
                    time.Add(node.DeepClone());
                }
            }
            line["lstCashSales_Series_UnconsumedItem"] = lineSeries;
            line["lstCashSales_UnconsumedTime"] = lineTime;
        }
        return (true, string.Empty, series, time);
    }

    private async Task<(bool Success, string ErrorMessage)> ValidatePackageRedemptionsAsync(
        Transaction transaction, CancellationToken cancellationToken)
    {
        var redemptions = transaction.Items.Where(item => item.IsPackageRedemption).ToList();
        if (redemptions.Count == 0) return (true, string.Empty);
        if (transaction.DocumentTypeId != 52 || string.IsNullOrWhiteSpace(transaction.AccountId))
            return (false, "Select a customer and use Redemption mode for package sessions.");

        var result = await customerService.GetPackagesAsync(transaction.AccountId, cancellationToken);
        if (!result.Success || result.Value is null)
            return (false, result.ErrorMessage ?? "Unable to confirm the package balance.");

        foreach (var group in redemptions.GroupBy(item => item.SourceDocumentLineId, StringComparer.OrdinalIgnoreCase))
        {
            var balance = result.Value.FirstOrDefault(item => string.Equals(item.AutoID, group.Key, StringComparison.OrdinalIgnoreCase));
            if (balance is null || balance.IsRedeemable == false ||
                (balance.ExpiryDate.Year > 1900 && balance.ExpiryDate.Date < transaction.Date.Date))
                return (false, "This package item is unavailable or expired. Refresh the customer's packages.");
            if ((!string.IsNullOrWhiteSpace(balance.CustomerAccountID) &&
                !string.Equals(balance.CustomerAccountID, transaction.AccountId, StringComparison.OrdinalIgnoreCase)) ||
                group.Any(item => item.Quantity <= 0 ||
                !string.Equals(item.InventoryId, balance.InventoryID, StringComparison.OrdinalIgnoreCase) ||
                !string.Equals(item.KitMemberId, balance.PackageID, StringComparison.OrdinalIgnoreCase) ||
                item.MemberCreditAllocations.Count > 0 || !string.IsNullOrWhiteSpace(item.MemberCreditAccountId) || item.Points > 0m))
                return (false, "Package redemption does not match the customer's purchased item.");
            if (group.Sum(item => item.Quantity) > balance.NetBalanceAfterUtilised)
                return (false, $"Only {balance.NetBalanceAfterUtilised:0.##} session(s) remain for '{balance.Description}'.");
        }
        return (true, string.Empty);
    }

    private static (bool Success, string ErrorMessage) ValidateFinalPackageRequest(Transaction transaction, JsonObject document)
    {
        var items = transaction.Items.Where(item => item.IsPackageRedemption).ToList();
        var lines = (document["lstDocumentLine"] as JsonArray ?? new JsonArray())
            .OfType<JsonObject>().Where(line => Integer(line, "ActivityTypeID") is 2 or 5).ToList();
        if (lines.Count != items.Count)
            return (false, "Package redemption lines are missing or incomplete.");
        var receipts = (document["lstReceiptLines"] as JsonArray ?? new JsonArray())
            .OfType<JsonObject>().Where(line => Integer(line, "POSPaymentTypeID") == -5).ToList();
        foreach (var group in items.GroupBy(item => item.SourceDocumentLineId, StringComparer.OrdinalIgnoreCase))
        {
            var sourceLines = lines.Where(line => string.Equals(Text(line, "SourceDocumentLineID"), group.Key, StringComparison.OrdinalIgnoreCase)).ToList();
            var receipt = receipts.SingleOrDefault(line => string.Equals(Text(line, "SourceDocumentLineID"), group.Key, StringComparison.OrdinalIgnoreCase));
            if (sourceLines.Count != group.Count() || sourceLines.Sum(line => Number(line, "Quantity")) != group.Sum(item => item.Quantity) ||
                sourceLines.Any(line => string.IsNullOrWhiteSpace(Text(line, "KitMemberID"))) ||
                receipt is null || Number(receipt, "QuantityRedeemed") != group.Sum(item => item.Quantity) ||
                Math.Abs(Number(receipt, "POSReceiptLineAmount") - group.Sum(item => item.TotalPrice)) > 0.009m)
                return (false, "Package redemption quantity or settlement does not match the selected sessions.");
        }
        return (true, string.Empty);
    }

    private static void ApplyPackageActualValues(Transaction transaction, JsonArray lines, JsonArray series, JsonArray time, JsonArray credits)
    {
        // Senang spreads the paid package value across its included benefits.
        foreach (var entry in transaction.Items.Select((item, index) => (Item: item, Index: index)).Where(entry => entry.Item.InventoryTypeId == 5))
        {
            var line = (JsonObject)lines[entry.Index]!;
            var benefits = new[] { "lstCashSales_Series_UnconsumedItem", "lstCashSales_UnconsumedTime" }
                .SelectMany(key => (line[key] as JsonArray ?? new JsonArray()).OfType<JsonObject>()).ToList();
            var lineCredits = (line["lstARAPOutstanding_MemberCredit"] as JsonArray ?? new JsonArray()).OfType<JsonObject>().ToList();
            var paidBeforeTax = Number(line, "SubTotalBeforeGST");
            var faceValue = benefits.Sum(benefit => Number(benefit, "TotalPrice")) + lineCredits.Sum(credit => Number(credit, "TotalAmount"));
            var ratio = paidBeforeTax != 0m && faceValue != 0m ? faceValue / paidBeforeTax : 1m;
            line["ActualValueRatio"] = ratio;
            foreach (var benefit in benefits)
            {
                var deferred = Integer(benefit, "ActivityTypeID") == 4;
                benefit["UnitActualValue"] = deferred ? paidBeforeTax / Math.Max(1, entry.Item.Quantity) : Number(benefit, "UnitPrice") / ratio;
                benefit["TotalActualValue"] = deferred ? paidBeforeTax : Number(benefit, "TotalPrice") / ratio;
            }
            foreach (var credit in lineCredits)
            {
                credit["InterOutletRatio"] = ratio;
                credit["InterOutletAmount"] = Number(credit, "TotalAmount") / ratio;
            }
        }
        series.Clear(); time.Clear(); credits.Clear();
        foreach (var line in lines.OfType<JsonObject>())
        {
            foreach (var node in line["lstCashSales_Series_UnconsumedItem"] as JsonArray ?? new JsonArray()) series.Add(node?.DeepClone());
            foreach (var node in line["lstCashSales_UnconsumedTime"] as JsonArray ?? new JsonArray()) time.Add(node?.DeepClone());
            foreach (var node in line["lstARAPOutstanding_MemberCredit"] as JsonArray ?? new JsonArray()) credits.Add(node?.DeepClone());
        }
    }
}
