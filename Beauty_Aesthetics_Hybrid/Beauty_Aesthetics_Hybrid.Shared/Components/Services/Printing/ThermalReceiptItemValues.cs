using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using Beauty_Aesthetics_WebPos.Components.Models;

namespace Beauty_Aesthetics_WebPos.Components.Services.Printing;

public sealed record ThermalReceiptItemValues(
    [property: JsonPropertyName("sku")] string Sku,
    [property: JsonPropertyName("description")] string Description,
    [property: JsonPropertyName("quantity")] decimal Quantity,
    [property: JsonPropertyName("unitPrice")] decimal? UnitPrice,
    [property: JsonPropertyName("amount")] decimal? Amount)
{
    // Read the original document only. Downloading must not modify a sale or
    // substitute today's inventory price for a historical selling price.
    public static IReadOnlyList<ThermalReceiptItemValues> FromDocument(
        JsonObject? document, Transaction? originalSale)
    {
        if (document?["lstDocumentLine"] is not JsonArray lines)
            return FromOriginalSale(originalSale);

        var items = lines.OfType<JsonObject>()
            .Where(line => Number(line, "DocumentLineTypeID") == 1 &&
                           Number(line, "SaveAction") != 3 &&
                           !string.Equals(Text(line, "SaveAction"), "Deleted", StringComparison.OrdinalIgnoreCase))
            .OrderBy(line => Number(line, "LineOrder"))
            .ToList();
        var values = new List<ThermalReceiptItemValues>();

        for (var index = 0; index < items.Count; index++)
        {
            var line = items[index];
            var quantity = Number(line, "Quantity");
            var sku = Text(line, "LineItemDisplayCode");
            var description = Text(line, "Description");
            var inventoryId = Text(line, "LineItemID");
            // Preserve ordering for repeated SKUs with different selling prices.
            // Never borrow a price from an unrelated line just by list position.
            var original = originalSale is not null && index < originalSale.Items.Count
                ? originalSale.Items[index]
                : null;
            if (original is not null &&
                !((!string.IsNullOrWhiteSpace(inventoryId) &&
                   string.Equals(inventoryId, original.InventoryId, StringComparison.OrdinalIgnoreCase)) ||
                  (!string.IsNullOrWhiteSpace(sku) &&
                   string.Equals(sku, original.Sku, StringComparison.OrdinalIgnoreCase))))
                original = null;
            if (original is not null && Math.Abs(quantity - original.Quantity) > 0.0001m)
                original = null;

            var exchangeRate = Number(line, "ExchangeRate");
            if (exchangeRate <= 0m) exchangeRate = 1m;
            var beforeTax = FirstNonZero(Number(line, "SubTotalBeforeGST"),
                Number(line, "ConvertedSubTotalBeforeGST") / exchangeRate);
            var tax = FirstNonZero(Number(line, "TaxAmount"),
                Number(line, "ConvertedTaxAmount") / exchangeRate);
            var discount = FirstNonZero(Number(line, "Discount"), Number(line, "DiscountAmount"));
            var storedPrice = Number(line, "UnitPrice");
            var storedAmount = FirstNonZero(Number(line, "SubTotal"), Number(line, "Amount"),
                Number(line, "ConvertedAmount") / exchangeRate);
            decimal? price = storedPrice != 0m ? storedPrice : original?.UnitPrice;
            decimal? amount = storedAmount != 0m ? storedAmount : original?.TotalPrice;
            var pointPaid = Number(line, "Points") > 0m;

            if (pointPaid)
            {
                // Points deliberately have zero monetary price and subtotal.
                price = 0m;
                amount = 0m;
            }
            else
            {
                if (amount is null && beforeTax != 0m &&
                    (tax != 0m || Number(line, "TaxPercentage") == 0m))
                    amount = beforeTax + tax;

                if (price is null && quantity != 0m)
                {
                    // The saved Amount includes tax; UnitPrice follows the saved
                    // IsTaxInclusive flag. Discount is an absolute line amount.
                    decimal? priceBasis = Flag(line, "IsTaxInclusive") ? amount :
                        beforeTax != 0m ? beforeTax :
                        Number(line, "TaxPercentage") == 0m ? amount :
                        tax != 0m && amount.HasValue ? amount.Value - tax : null;
                    if (priceBasis.HasValue)
                        price = (priceBasis.Value + discount) / quantity;
                }

                if (amount is null && price.HasValue && Number(line, "TaxPercentage") == 0m)
                    amount = quantity * price.Value - discount;
            }

            values.Add(new ThermalReceiptItemValues(sku, description, quantity,
                price.HasValue ? Math.Round(price.Value, 2, MidpointRounding.AwayFromZero) : null,
                amount.HasValue ? Math.Round(amount.Value, 2, MidpointRounding.AwayFromZero) : null));
        }

        return values.Count > 0 ? values : FromOriginalSale(originalSale);
    }

    private static IReadOnlyList<ThermalReceiptItemValues> FromOriginalSale(Transaction? sale) =>
        sale?.Items.Select(item => new ThermalReceiptItemValues(item.Sku ?? string.Empty,
            item.Description ?? item.Name ?? string.Empty, item.Quantity, item.UnitPrice, item.TotalPrice))
            .ToList() ?? new List<ThermalReceiptItemValues>();

    private static JsonNode? Field(JsonObject line, string name) =>
        line.FirstOrDefault(field => string.Equals(field.Key, name, StringComparison.OrdinalIgnoreCase)).Value;

    private static decimal Number(JsonObject line, string name) =>
        Field(line, name) is JsonValue value && value.TryGetValue<decimal>(out var number) ? number : 0m;

    private static string Text(JsonObject line, string name) =>
        Field(line, name) is JsonValue value && value.TryGetValue<string>(out var text) ? text : string.Empty;

    private static bool Flag(JsonObject line, string name) =>
        Field(line, name) is JsonValue value && value.TryGetValue<bool>(out var flag) && flag;

    private static decimal FirstNonZero(params decimal[] values) => values.FirstOrDefault(value => value != 0m);
}
