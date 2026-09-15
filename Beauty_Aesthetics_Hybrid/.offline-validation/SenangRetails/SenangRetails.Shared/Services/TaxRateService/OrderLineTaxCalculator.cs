using System;

namespace SenangRetails.Shared.Services.TaxRateService
{
    public static class OrderLineTaxCalculator
    {
        public static decimal ComputeSubtotalBeforeTax(decimal unitPrice, decimal quantity, decimal discount, decimal taxRate, bool isTaxInclusive)
        {
            ComputeLineAmounts(unitPrice, quantity, discount, taxRate, isTaxInclusive, out decimal beforeTax, out _);
            return beforeTax;
        }

        public static decimal ComputeTaxAmount(decimal unitPrice, decimal quantity, decimal discount, decimal taxRate, bool isTaxInclusive)
        {
            ComputeLineAmounts(unitPrice, quantity, discount, taxRate, isTaxInclusive, out _, out decimal tax);
            return tax;
        }

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
            decimal gross = unitPrice * quantity;
            if (discount < 0m) discount = 0m;
            if (discount > gross) discount = gross;

            if (quantity <= 0)
            {
                subtotalBeforeTax = 0m;
                return;
            }

            if (taxRate <= 0m)
            {
                subtotalBeforeTax = Math.Round(gross - discount, 2, MidpointRounding.AwayFromZero);
                return;
            }

            if (!isTaxInclusive)
            {
                subtotalBeforeTax = Math.Round(gross - discount, 2, MidpointRounding.AwayFromZero);
            }
            else
            {
                subtotalBeforeTax = Math.Round((gross - discount) / (1 + taxRate), 2, MidpointRounding.AwayFromZero);
            }

            taxAmount = Math.Round(subtotalBeforeTax * taxRate, 2, MidpointRounding.AwayFromZero);
        }
    }
}
