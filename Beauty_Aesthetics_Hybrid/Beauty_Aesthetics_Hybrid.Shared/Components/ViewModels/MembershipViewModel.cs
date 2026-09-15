using System.Collections.Generic;

namespace Beauty_Aesthetics_WebPos.Components.ViewModels;

public sealed class MembershipViewModel
{
    public IReadOnlyList<MemberType> MemberTypes { get; } = new List<MemberType>
    {
        new("Gold", 150),
        new("Silver", 89),
        new("Bronze", 45),
        new("Platinum", 250),
        new("VIP", 500),
        new("Premium", 180),
        new("Standard", 75),
        new("Basic", 35),
        new("Elite", 320),
        new("Diamond", 420),
        new("Executive", 280),
        new("Regular", 60),
        new("Student", 25),
        new("Senior", 40),
        new("Corporate", 150),
        new("Family", 200),
        new("Loyalty", 95),
        new("Trial", 10),
        new("Annual", 380),
        new("Lifetime", 1000)
    };

    public IReadOnlyList<MemberPoint> MemberPoints { get; } = new List<MemberPoint>
    {
        new("Gold", 100.00m, 10.00m, 1, "NA"),
        new("Silver", 50.00m, 10.00m, 1, "Monthly"),
        new("Bronze", 0.00m, 20.00m, 1, "NA"),
        new("Platinum", 200.00m, 5.00m, 2, "Yearly"),
        new("VIP", 500.00m, 10.00m, 5, "NA"),
        new("Premium", 150.00m, 8.00m, 1, "Quarterly"),
        new("Standard", 30.00m, 15.00m, 1, "Monthly"),
        new("Basic", 0.00m, 25.00m, 1, "NA"),
        new("Elite", 300.00m, 5.00m, 3, "Yearly"),
        new("Diamond", 400.00m, 5.00m, 4, "NA"),
        new("Executive", 250.00m, 7.00m, 2, "Quarterly"),
        new("Regular", 20.00m, 20.00m, 1, "Monthly"),
        new("Student", 0.00m, 30.00m, 1, "NA"),
        new("Senior", 0.00m, 18.00m, 1, "NA"),
        new("Corporate", 100.00m, 10.00m, 1, "Yearly"),
        new("Family", 120.00m, 8.00m, 1, "Quarterly"),
        new("Loyalty", 40.00m, 12.00m, 1, "Monthly"),
        new("Trial", 0.00m, 50.00m, 1, "NA"),
        new("Annual", 350.00m, 5.00m, 3, "Yearly"),
        new("Lifetime", 0.00m, 5.00m, 5, "NA")
    };

    public IReadOnlyList<MemberCredit> MemberCredits { get; } = new List<MemberCredit>
    {
        new("Welcome Credit", "CRD-001", 50.00m, 45.00m),
        new("Referral Credit", "CRD-002", 100.00m, 90.00m),
        new("Loyalty Credit", "CRD-003", 150.00m, 140.00m),
        new("Birthday Credit", "CRD-004", 75.00m, 70.00m),
        new("Anniversary Credit", "CRD-005", 200.00m, 190.00m),
        new("Holiday Credit", "CRD-006", 60.00m, 58.00m),
        new("Promotional Credit", "CRD-007", 80.00m, 76.00m),
        new("Seasonal Credit", "CRD-008", 90.00m, 85.00m),
        new("New Member Credit", "CRD-009", 40.00m, 38.00m),
        new("Renewal Credit", "CRD-010", 120.00m, 115.00m),
        new("Upgrade Credit", "CRD-011", 180.00m, 175.00m),
        new("Referral Bonus", "CRD-012", 110.00m, 105.00m),
        new("Social Media Credit", "CRD-013", 35.00m, 33.00m),
        new("Review Credit", "CRD-014", 25.00m, 24.00m),
        new("Event Credit", "CRD-015", 70.00m, 68.00m),
        new("Flash Sale Credit", "CRD-016", 45.00m, 42.00m),
        new("Bundle Credit", "CRD-017", 130.00m, 125.00m),
        new("Premium Credit", "CRD-018", 250.00m, 240.00m),
        new("Special Offer Credit", "CRD-019", 55.00m, 52.00m),
        new("VIP Credit", "CRD-020", 300.00m, 285.00m)
    };

    public sealed record MemberType(
        string Name,
        int MembersAssigned
    );

    public sealed record MemberPoint(
        string MemberType,
        decimal MinAmount,
        decimal ForEveryAmount,
        decimal EqualToPoint,
        string ExpiryOption,
        string? PointId = null,
        string? MemberTypeId = null,
        decimal MoneyConversionRate = 0,
        bool ExcludeTaxAmount = false,
        bool RoundDownToInteger = false,
        int ExpiryMonth = 0,
        bool ConvertToVoucher = false,
        DateTime? StartDate = null,
        DateTime? EndDate = null,
        string VisibleToBranchId = "",
        int ExpiryOptionId = 0
    );

    public sealed record MemberCredit(
        string Name,
        string Code,
        decimal CreditValue,
        decimal Price,
        string? MasterAccountId = null,
        string Status = "Active",
        string BranchId = "",
        string InventoryTypeName = "",
        string SalesDescription = "",
        int ValidityDays = 0,
        decimal SettlementRatio = 0,
        DateTime? AvailableDateFrom = null,
        DateTime? AvailableDateTo = null,
        TimeSpan? AvailableTimeFrom = null,
        TimeSpan? AvailableTimeTo = null,
        string EInvoiceClassificationCode = ""
    );
}

