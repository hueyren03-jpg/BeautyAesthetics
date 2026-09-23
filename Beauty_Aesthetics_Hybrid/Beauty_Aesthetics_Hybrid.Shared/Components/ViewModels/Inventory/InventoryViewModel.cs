using System.Collections.Generic;

namespace Beauty_Aesthetics_WebPos.Components.ViewModels;

public sealed class InventoryViewModel
{
    public IReadOnlyList<InventoryItem> Items { get; } = new List<InventoryItem>
    {
        new("SKU-001", "Facial cleanser", "3317056127757", "1 box = 10 bottles", 89.90m, "Brand A", "Skincare", "Supplier X", false, "240 bottles", "24 boxes", "A1", "50 bottles", 15.0m),
        new("SKU-002", "Toner", "6975331730857", "1 box = 12 bottles", 129.90m, "Brand B", "Treatment", "Supplier Y", false, "216 bottles", "18 boxes", "B2", "60 bottles", 20.0m),
        new("SKU-003", "Moisturizer", "2186462963171", "1 carton = 20 pieces", 159.90m, "Brand C", "Serum", "Supplier Z", true, "640 pieces", "32 cartons", "C3", "100 pieces", 10.0m),
        new("SKU-004", "Sunscreen", "5579797283804", "1 box = 8 pieces", 79.90m, "Brand A", "Mask", "Supplier X", false, "120 pieces", "15 boxes", "D4", "40 pieces", 25.0m),
        new("SKU-005", "Serum", "8847714813692", "1 box = 6 bottles", 99.90m, "Brand D", "Toner", "Supplier Y", false, "240 bottles", "40 boxes", "E5", "30 bottles", 18.0m),
        new("SKU-006", "Face mask", "9327566408853", "1 box = 10 bottles", 75.50m, "L'Oreal", "Cleanser", "Supplier X", false, "180 bottles", "18 boxes", "A2", "45 bottles", 12.0m),
        new("SKU-007", "Eye cream", "7062734413966", "1 box = 8 jars", 145.00m, "Estee Lauder", "Moisturizer", "Supplier Y", false, "160 jars", "20 boxes", "B3", "50 jars", 22.0m),
        new("SKU-008", "Lip balm", "1648320672288", "1 box = 12 tubes", 95.00m, "Clinique", "Sunscreen", "Supplier Z", false, "144 tubes", "12 boxes", "C4", "40 tubes", 16.0m),
        new("SKU-009", "Foundation", "5129463482115", "1 box = 15 pieces", 65.00m, "MAC Cosmetics", "Exfoliant", "Supplier X", false, "225 pieces", "15 boxes", "D5", "60 pieces", 14.0m),
        new("SKU-010", "Concealer", "3802779571822", "1 box = 10 bottles", 110.00m, "Maybelline", "Eye Care", "Supplier Y", false, "200 bottles", "20 boxes", "E6", "55 bottles", 19.0m),
        new("SKU-011", "Compact powder", "6075113632954", "1 box = 20 pieces", 45.00m, "Revlon", "Lip Care", "Supplier Z", false, "400 pieces", "20 boxes", "A3", "80 pieces", 10.0m),
        new("SKU-012", "Blush", "8249467519085", "1 box = 6 bottles", 125.00m, "Covergirl", "Hair Care", "Supplier X", false, "120 bottles", "20 boxes", "B4", "35 bottles", 21.0m),
        new("SKU-013", "Eyeshadow palette", "1496075308579", "1 box = 8 jars", 88.00m, "NYX", "Body Care", "Supplier Y", false, "160 jars", "20 boxes", "C5", "50 jars", 15.0m),
        new("SKU-014", "Eyeliner", "2998417623456", "1 box = 10 bottles", 135.00m, "Urban Decay", "Fragrance", "Supplier Z", false, "100 bottles", "10 boxes", "D6", "30 bottles", 24.0m),
        new("SKU-015", "Mascara", "978055194613", "1 box = 12 pieces", 55.00m, "Too Faced", "Makeup", "Supplier X", false, "240 pieces", "20 boxes", "E7", "70 pieces", 11.0m),
        new("SKU-016", "Lipstick", "4736288197058", "1 box = 5 bottles", 165.00m, "Benefit", "Tools", "Supplier Y", false, "100 bottles", "20 boxes", "A4", "25 bottles", 28.0m),
        new("SKU-017", "Makeup remover", "7509143068271", "1 box = 15 pieces", 72.00m, "NARS", "Accessories", "Supplier Z", false, "180 pieces", "12 boxes", "B5", "45 pieces", 13.0m),
        new("SKU-018", "Facial steamer", "5021660871942", "1 box = 10 bottles", 98.00m, "Bobbi Brown", "Professional", "Supplier X", false, "240 bottles", "24 boxes", "C6", "60 bottles", 17.0m),
        new("SKU-019", "Derma roller", "8934725613059", "1 box = 8 jars", 115.00m, "Shiseido", "Retail", "Supplier Y", false, "128 jars", "16 boxes", "D7", "40 jars", 20.0m),
        new("SKU-020", "LED facial therapy device", "6705829341773", "1 box = 12 tubes", 82.00m, "SK-II", "Skincare", "Supplier Z", false, "240 tubes", "20 boxes", "E8", "65 tubes", 14.0m),
        new("SKU-021", "Hair serum", "1234567890125", "1 box = 6 bottles", 155.00m, "La Mer", "Treatment", "Supplier X", false, "60 bottles", "10 boxes", "A5", "20 bottles", 30.0m),
        new("SKU-022", "Body lotion", "2345678901236", "1 box = 10 pieces", 68.00m, "Chanel", "Serum", "Supplier Y", false, "160 pieces", "16 boxes", "B6", "50 pieces", 12.0m),
        new("SKU-023", "Facial cleanser", "3456789012347", "1 box = 8 bottles", 105.00m, "Dior", "Mask", "Supplier Z", false, "144 bottles", "18 boxes", "C7", "40 bottles", 18.0m),
        new("SKU-024", "Toner", "4567890123458", "1 box = 12 jars", 92.00m, "Brand A", "Toner", "Supplier X", false, "336 jars", "28 boxes", "D8", "80 jars", 16.0m),
        new("SKU-025", "Moisturizer", "5678901234569", "1 box = 15 bottles", 78.00m, "Brand B", "Cleanser", "Supplier Y", false, "300 bottles", "20 boxes", "E9", "75 bottles", 13.0m)
    };

    public sealed record InventoryItem(
        string Sku,
        string Type,
        string Barcode,
        string ConversionFactor,
        decimal Price,
        string Brand,
        string Category,
        string Supplier,
        bool Locked,
        string StockUom1,
        string StockUom2,
        string Location,
        string LowAlertCount,
        decimal DiscountCap
    )
    {
        public string MasterAccountId { get; init; } = string.Empty;
        public string UnitOfMeasurementId { get; init; } = string.Empty;
        public string SupplierAccountId { get; init; } = string.Empty;
        public string CategoryId { get; init; } = string.Empty;
        public int InventoryTypeId { get; init; } = 1;
        public decimal? StockQuantity { get; init; }
        public string ImagePath { get; init; } = string.Empty;
        public string ImageFileName { get; init; } = string.Empty;
    }
}
