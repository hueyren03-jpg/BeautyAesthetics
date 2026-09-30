namespace Beauty_Aesthetics_WebPos.Components.Services.Printing;

public static class ReceiptBranding
{
    public const string AppTitle = "Beauty Aesthetics";
    public const string AppSubtitle = "Clinic Management";
    public const string BrandMark = "BA";

    // Do not use the legacy EBI/Senang image in receipts.
    // ReceiptHtmlBuilder renders the Beauty monogram directly.
    public const string LogoUrl = "";
}
