using System.ComponentModel.DataAnnotations;

namespace SenangRetails.Shared.Enums;

public enum EnumCashDiscountType
{
    [Display(Name = "Percentage %")]
    Percentage = 1,

    [Display(Name = "Amount")]
    Amount = 2,

    [Display(Name = "Compound")]
    Compound = 3
}
