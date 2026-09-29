using System.Globalization;

namespace Beauty_Aesthetics_WebPos.Models.DTOs;

public sealed class CashDiscountRuleDTO
{
    public string? SupportingTableID { get; set; }
    public string? SupportingTableName { get; set; }
    public int SupportingTableTypeID { get; set; }
    public string? BranchID { get; set; }
    public bool Active { get; set; }
    public decimal NumberField { get; set; }
    public decimal CashDiscountPercentage { get; set; }
    public int CashDiscountTypeID { get; set; }
    public bool IsUseCost { get; set; }
    public string? CashDiscountFormula { get; set; }
    public int FrequencyType { get; set; }
    public decimal Frequency { get; set; }
    public DateTime? StartDate { get; set; }
    public DateTime? EndDate { get; set; }
    public string? WeekDays { get; set; }
    public bool IsOpenDiscount { get; set; }

    public string Id => SupportingTableID?.Trim() ?? string.Empty;
    public string Description => string.IsNullOrWhiteSpace(SupportingTableName) ? "Discount" : SupportingTableName.Trim();

    public bool IsCheckoutSupported =>
        !IsUseCost && (IsOpenDiscount || CashDiscountTypeID is 1 or 2 or 4);

    public string DisplayLabel => IsOpenDiscount
        ? $"{Description} (Open)"
        : CashDiscountTypeID switch
        {
            1 => $"{Description} ({CashDiscountPercentage * 100m:0.##}%)",
            2 => $"{Description} (RM {CashDiscountPercentage:0.00})",
            4 => string.IsNullOrWhiteSpace(CashDiscountFormula)
                ? $"{Description} (Compound)"
                : $"{Description} ({CashDiscountFormula})",
            _ => Description
        };

    public bool IsAvailable(string branchId, DateTime at)
    {
        if (!Active || !IsCheckoutSupported) return false;
        if (!string.IsNullOrWhiteSpace(BranchID) &&
            !BranchID.Trim().Equals(branchId?.Trim(), StringComparison.OrdinalIgnoreCase)) return false;
        if (StartDate.HasValue && at.Date < StartDate.Value.Date) return false;
        if (EndDate.HasValue && at.Date > EndDate.Value.Date) return false;
        if (string.IsNullOrWhiteSpace(WeekDays)) return true;

        return WeekDays
            .Split(['_', ',', ';', '|'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Any(value => IsSameDay(value, at.DayOfWeek));
    }

    public bool TryCalculate(decimal gross, decimal openAmount, out decimal discount, out string? error)
    {
        gross = Math.Max(0m, gross);
        discount = 0m;
        error = null;

        if (gross <= 0m) return true;
        if (!IsCheckoutSupported)
        {
            error = "This discount type is not supported at checkout.";
            return false;
        }

        if (IsOpenDiscount)
        {
            discount = Math.Clamp(openAmount, 0m, gross);
            return true;
        }

        switch (CashDiscountTypeID)
        {
            case 1:
                discount = Math.Clamp(
                    Math.Round(gross * Math.Clamp(CashDiscountPercentage, 0m, 1m), 2, MidpointRounding.AwayFromZero),
                    0m, gross);
                return true;
            case 2:
                discount = Math.Clamp(CashDiscountPercentage, 0m, gross);
                return true;
            case 4:
                if (!TryCompoundRate(CashDiscountFormula, out var rate))
                {
                    error = "The compound discount formula is invalid.";
                    return false;
                }
                discount = Math.Clamp(Math.Round(gross * rate, 2, MidpointRounding.AwayFromZero), 0m, gross);
                return true;
            default:
                error = "This discount type is not supported at checkout.";
                return false;
        }
    }

    private static bool TryCompoundRate(string? formula, out decimal rate)
    {
        rate = 0m;
        if (string.IsNullOrWhiteSpace(formula)) return false;

        var parts = formula.Split('+', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length == 0) return false;

        var remaining = 1m;
        foreach (var part in parts)
        {
            var value = part.Trim().TrimEnd('%').Trim();
            if (!decimal.TryParse(value, NumberStyles.Number, CultureInfo.InvariantCulture, out var percent) ||
                percent < 0m || percent > 100m)
            {
                return false;
            }
            remaining *= 1m - percent / 100m;
        }

        rate = 1m - remaining;
        return rate is >= 0m and <= 1m;
    }

    private static bool IsSameDay(string value, DayOfWeek day)
    {
        var v = value.Trim().ToLowerInvariant();
        return day switch
        {
            DayOfWeek.Monday => v is "monday" or "mon",
            DayOfWeek.Tuesday => v is "tuesday" or "tue" or "tues",
            DayOfWeek.Wednesday => v is "wednesday" or "wed",
            DayOfWeek.Thursday => v is "thursday" or "thu" or "thurs",
            DayOfWeek.Friday => v is "friday" or "fri",
            DayOfWeek.Saturday => v is "saturday" or "sat" or "satur",
            DayOfWeek.Sunday => v is "sunday" or "sun",
            _ => false
        };
    }
}
