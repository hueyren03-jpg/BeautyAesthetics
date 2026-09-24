using System.Collections.Generic;

namespace Beauty_Aesthetics_WebPos.Components.ViewModels;

public sealed class ServiceViewModel
{
    public IReadOnlyList<ServiceItem> Items { get; } = new List<ServiceItem>
    {
        new("SRV-001", "Facial Treatment", "Beauty", "60 mins", 0, true, 150.00m),
        new("SRV-002", "Hair Styling", "Hair Care", "45 mins", 0, false, 80.00m),
        new("SRV-003", "Manicure & Pedicure", "Nail Care", "90 mins", 0, true, 120.00m),
        new("SRV-004", "Body Massage", "Wellness", "120 mins", 0, false, 200.00m),
        new("SRV-005", "Skin Rejuvenation", "Beauty", "75 mins", 0, false, 180.00m),
        new("SRV-006", "Hair Coloring", "Hair Care", "120 mins", 0, false, 250.00m),
        new("SRV-007", "Eyelash Extension", "Beauty", "90 mins", 0, true, 160.00m),
        new("SRV-008", "Aromatherapy", "Wellness", "60 mins", 0, false, 130.00m),
        new("SRV-009", "Deep Cleansing Facial", "Beauty", "90 mins", 0, true, 220.00m),
        new("SRV-010", "Haircut & Styling", "Hair Care", "60 mins", 0, false, 95.00m),
        new("SRV-011", "Gel Manicure", "Nail Care", "45 mins", 0, true, 85.00m),
        new("SRV-012", "Hot Stone Massage", "Wellness", "90 mins", 0, false, 180.00m),
        new("SRV-013", "Chemical Peel", "Beauty", "60 mins", 0, true, 280.00m),
        new("SRV-014", "Hair Treatment", "Hair Care", "75 mins", 0, false, 140.00m),
        new("SRV-015", "Acrylic Nails", "Nail Care", "120 mins", 0, true, 150.00m),
        new("SRV-016", "Swedish Massage", "Wellness", "60 mins", 0, false, 120.00m),
        new("SRV-017", "Microdermabrasion", "Beauty", "45 mins", 0, true, 200.00m),
        new("SRV-018", "Hair Highlights", "Hair Care", "150 mins", 0, false, 300.00m),
        new("SRV-019", "Pedicure Deluxe", "Nail Care", "75 mins", 0, true, 110.00m),
        new("SRV-020", "Thai Massage", "Wellness", "90 mins", 0, false, 160.00m),
        new("SRV-021", "Hydrafacial", "Beauty", "60 mins", 0, true, 350.00m),
        new("SRV-022", "Hair Perm", "Hair Care", "180 mins", 0, false, 280.00m),
        new("SRV-023", "Nail Art Design", "Nail Care", "30 mins", 0, true, 50.00m),
        new("SRV-024", "Reflexology", "Wellness", "60 mins", 0, false, 100.00m),
        new("SRV-025", "LED Light Therapy", "Beauty", "30 mins", 0, true, 120.00m)
    };

    public sealed record ServiceItem(
        string Sku,
        string ServiceName,
        string Category,
        string DurationSpend,
        int BufferTimeMinutes,
        bool IsBundle,
        decimal Price,
        string? MasterAccountId = null,
        string Status = "Active",
        string BranchId = "",
        string InventoryTypeName = "",
        string SalesDescription = "",
        DateTime? AvailableDateFrom = null,
        DateTime? AvailableDateTo = null,
        TimeSpan? AvailableTimeFrom = null,
        TimeSpan? AvailableTimeTo = null,
        string EInvoiceClassificationCode = "",
        string ImagePath = "",
        string ImageFileName = "",
        decimal Cost = 0m,
        string TaxCode = "",
        bool IsTaxInclusive = false,
        bool IsActive = true,
        string Barcode = "",
        string UnitOfMeasure = "unit",
        string Description = ""
    );
}

