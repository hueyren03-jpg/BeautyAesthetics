namespace Beauty_Aesthetics_WebPos.Components.Services.Tax;

public static class OrderLineTaxCalculator
{
    public static void ComputeLineAmounts(
        decimal unitPrice,
        decimal quantity,
        decimal discount,
        decimal taxRate,
        bool isTaxInclusive,
        out decimal subtotalBeforeTax,
        out decimal taxAmount)
    {
        taxAmount = 0m;
        var gross = unitPrice * quantity;
        discount = Math.Clamp(discount, 0m, Math.Max(0m, gross));

        if (quantity <= 0m)
        {
            subtotalBeforeTax = 0m;
            return;
        }

        if (taxRate <= 0m)
        {
            subtotalBeforeTax = Math.Round(gross - discount, 2, MidpointRounding.AwayFromZero);
            return;
        }

        subtotalBeforeTax = isTaxInclusive
            ? Math.Round((gross - discount) / (1m + taxRate), 2, MidpointRounding.AwayFromZero)
            : Math.Round(gross - discount, 2, MidpointRounding.AwayFromZero);

        taxAmount = Math.Round(subtotalBeforeTax * taxRate, 2, MidpointRounding.AwayFromZero);
    }
}
