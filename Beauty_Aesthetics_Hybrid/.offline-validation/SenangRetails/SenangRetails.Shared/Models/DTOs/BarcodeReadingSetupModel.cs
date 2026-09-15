namespace SenangRetails.Shared.Models.DTOs
{
    public class BarcodeReadingSetupModel
    {
        public string Prefix { get; set; } = "20"; // 2 chars
        public bool IsActive { get; set; } = true;
        public string BarcodeType { get; set; } = "EAN-13"; // EAN-13, Code-128, UPC-A, Custom Embedded
        public string SelectionType { get; set; } = "Barcode"; // Barcode or QRCode
        public int ItemCodeLength { get; set; } = 5;
        public int QuantityLength { get; set; } = 5;
        public bool IsQuantityFixedAsOne { get; set; } = false; // Yes / No
        public int PriceLength { get; set; } = 5;
        public int DecimalPlace { get; set; } = 2;
        public int ChecksumLength { get; set; } = 1;
        public bool BarcodeIncludesPrefix { get; set; } = true;
        public bool HasOverlappingBarcodeItem { get; set; } = false;
    }

    public class ParsedBarcodeResult
    {
        public bool Success { get; set; } = false;
        public string ItemCode { get; set; } = string.Empty;
        public decimal Quantity { get; set; } = 1;
        public decimal? Price { get; set; }
        public BarcodeReadingSetupModel? MatchedRule { get; set; }
        public string RawCode { get; set; } = string.Empty;
        public string StatusMessage { get; set; } = string.Empty;
    }
}
