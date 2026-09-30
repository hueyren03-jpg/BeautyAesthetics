using System.Globalization;
using System.Text;

namespace Beauty_Aesthetics_WebPos.Components.Services.Printing;

public static class ReceiptPdfBuilder
{
    private const double PageWidth = 595;
    private const double PageHeight = 842;
    private const double Margin = 42;

    public static string BuildBase64(ReceiptData data)
    {
        var pages = BuildPages(data);
        var pdf = BuildPdf(pages);
        return Convert.ToBase64String(pdf);
    }

    private static List<string> BuildPages(ReceiptData data)
    {
        var pages = new List<string>();
        var page = new StringBuilder();
        var y = PageHeight - Margin;

        void StartPage(bool continuation = false)
        {
            page = new StringBuilder();
            y = PageHeight - Margin;

            DrawBrand(page, ref y, continuation);
        }

        void EndPage()
        {
            pages.Add(page.ToString());
        }

        void EnsureSpace(double needed)
        {
            if (y - needed >= Margin)
            {
                return;
            }

            EndPage();
            StartPage(true);
        }

        StartPage();

        DrawMeta(page, data, ref y);

        EnsureSpace(44);
        DrawRule(page, y);
        y -= 18;
        DrawText(page, Margin, y, "ITEM", 9, true, 0.28, 0.35, 0.45);
        DrawRight(page, 410, y, "QTY", 9, true, 0.28, 0.35, 0.45);
        DrawRight(page, 535, y, "TOTAL", 9, true, 0.28, 0.35, 0.45);
        y -= 10;
        DrawRule(page, y);
        y -= 16;

        foreach (var item in data.Items)
        {
            var itemName = Clean(item.Name);
            var remarks = Clean(item.Remarks);
            var lineHeight = string.IsNullOrWhiteSpace(remarks) ? 24 : 34;

            EnsureSpace(lineHeight + 8);

            DrawText(page, Margin, y, Truncate(itemName, 48), 10, true);
            DrawRight(page, 410, y, item.Quantity.ToString("0.##", CultureInfo.InvariantCulture), 10);
            DrawRight(page, 535, y, Money(data, item.LineTotal), 10, true);

            if (!string.IsNullOrWhiteSpace(remarks))
            {
                y -= 12;
                DrawText(page, Margin, y, Truncate(remarks, 64), 8, false, 0.40, 0.45, 0.52);
            }

            y -= 15;
            DrawRule(page, y, 0.90);
            y -= 12;
        }

        EnsureSpace(128);
        y -= 2;
        DrawAmountRow(page, ref y, "Subtotal", data.Subtotal, data, false);

        if (data.TaxAmount != 0m)
        {
            DrawAmountRow(page, ref y, "Tax", data.TaxAmount, data, false);
        }

        if (data.RoundingAmount != 0m)
        {
            DrawAmountRow(page, ref y, "Rounding", data.RoundingAmount, data, false);
        }

        y -= 3;
        DrawRule(page, y, 0.55);
        y -= 17;
        DrawAmountRow(page, ref y, "Total Paid", data.GrandTotal, data, true);

        EnsureSpace(90 + (data.Payments.Count * 16));
        y -= 7;
        DrawText(page, Margin, y, "PAYMENT BREAKDOWN", 9, true, 0.35, 0.40, 0.48);
        y -= 18;

        foreach (var payment in data.Payments.Where(p => p.Amount > 0m))
        {
            DrawText(page, Margin, y, Truncate(Clean(payment.Method), 48), 9);
            DrawRight(page, 535, y, Money(data, payment.Amount), 9, true);
            y -= 16;
        }

        y -= 2;
        DrawText(page, Margin, y, "Received", 9);
        DrawRight(page, 535, y, Money(data, data.PaidAmount), 9, true);
        y -= 16;

        DrawText(page, Margin, y, "Change", 9, true, 0.04, 0.45, 0.31);
        DrawRight(page, 535, y, Money(data, data.ChangeAmount), 9, true, 0.04, 0.45, 0.31);
        y -= 25;

        EnsureSpace(45);
        DrawRule(page, y, 0.78);
        y -= 22;
        DrawCentered(page, y, $"Thank you for visiting {ReceiptBranding.AppTitle}.", 9, false, 0.40, 0.45, 0.52);

        EndPage();
        return pages;
    }

    private static void DrawBrand(StringBuilder page, ref double y, bool continuation)
    {
        DrawCentered(page, y - 14, ReceiptBranding.BrandMark, 11, true, 0.03, 0.29, 0.58);
        y -= 34;
        DrawCentered(page, y, ReceiptBranding.AppTitle, 19, true, 0.03, 0.29, 0.58);
        y -= 16;
        DrawCentered(page, y, ReceiptBranding.AppSubtitle, 9, false, 0.40, 0.45, 0.52);
        y -= 16;
        DrawCentered(page, y, continuation ? "OFFICIAL RECEIPT - CONTINUED" : "OFFICIAL RECEIPT", 8, true, 0.28, 0.35, 0.45);
        y -= 15;
        DrawRule(page, y, 0.20, 0.45, 0.75);
        y -= 20;
    }

    private static void DrawMeta(StringBuilder page, ReceiptData data, ref double y)
    {
        var date = data.DateTimeOfSale ?? DateTime.Now;
        var customer = string.IsNullOrWhiteSpace(data.CustomerName) ? "Walk-in customer" : Clean(data.CustomerName);
        var branch = string.IsNullOrWhiteSpace(data.BranchName) ? "-" : Clean(data.BranchName);

        DrawText(page, Margin, y, "RECEIPT NO.", 7, true, 0.40, 0.45, 0.52);
        DrawText(page, 320, y, "DATE", 7, true, 0.40, 0.45, 0.52);
        y -= 13;
        DrawText(page, Margin, y, Clean(data.ReceiptNo), 10, true);
        DrawText(page, 320, y, date.ToString("dd/MM/yyyy hh:mm tt"), 10, true);
        y -= 23;

        DrawText(page, Margin, y, "CUSTOMER", 7, true, 0.40, 0.45, 0.52);
        DrawText(page, 320, y, "BRANCH", 7, true, 0.40, 0.45, 0.52);
        y -= 13;
        DrawText(page, Margin, y, Truncate(customer, 34), 10, true);
        DrawText(page, 320, y, Truncate(branch, 28), 10, true);
        y -= 23;

        if (!string.IsNullOrWhiteSpace(data.ReferenceNumber))
        {
            DrawText(page, Margin, y, "REFERENCE", 7, true, 0.40, 0.45, 0.52);
            y -= 13;
            DrawText(page, Margin, y, Truncate(Clean(data.ReferenceNumber), 60), 9);
            y -= 20;
        }
    }

    private static void DrawAmountRow(
        StringBuilder page,
        ref double y,
        string label,
        decimal amount,
        ReceiptData data,
        bool total)
    {
        var size = total ? 13 : 10;
        var blue = total;

        DrawText(
            page,
            320,
            y,
            label,
            size,
            total,
            blue ? 0.03 : 0.12,
            blue ? 0.29 : 0.16,
            blue ? 0.58 : 0.23);

        DrawRight(
            page,
            535,
            y,
            Money(data, amount),
            size,
            true,
            blue ? 0.03 : 0.12,
            blue ? 0.29 : 0.16,
            blue ? 0.58 : 0.23);

        y -= total ? 22 : 18;
    }

    private static byte[] BuildPdf(IReadOnlyList<string> pageStreams)
    {
        var objects = new List<byte[]>();

        objects.Add(Ascii("<< /Type /Catalog /Pages 2 0 R >>"));

        var kids = string.Join(" ", Enumerable.Range(0, pageStreams.Count).Select(i => $"{5 + (i * 2)} 0 R"));
        objects.Add(Ascii($"<< /Type /Pages /Kids [{kids}] /Count {pageStreams.Count} >>"));

        objects.Add(Ascii("<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica /Encoding /WinAnsiEncoding >>"));
        objects.Add(Ascii("<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica-Bold /Encoding /WinAnsiEncoding >>"));

        for (var i = 0; i < pageStreams.Count; i++)
        {
            var pageObjectId = 5 + (i * 2);
            var contentObjectId = pageObjectId + 1;
            var streamBytes = Ascii(pageStreams[i]);

            objects.Add(Ascii(
                $"<< /Type /Page /Parent 2 0 R /MediaBox [0 0 {F(PageWidth)} {F(PageHeight)}] " +
                $"/Resources << /Font << /F1 3 0 R /F2 4 0 R >> >> /Contents {contentObjectId} 0 R >>"));

            var streamPrefix = Ascii($"<< /Length {streamBytes.Length} >>\nstream\n");
            var streamSuffix = Ascii("\nendstream");
            var streamObject = new byte[streamPrefix.Length + streamBytes.Length + streamSuffix.Length];
            Buffer.BlockCopy(streamPrefix, 0, streamObject, 0, streamPrefix.Length);
            Buffer.BlockCopy(streamBytes, 0, streamObject, streamPrefix.Length, streamBytes.Length);
            Buffer.BlockCopy(streamSuffix, 0, streamObject, streamPrefix.Length + streamBytes.Length, streamSuffix.Length);
            objects.Add(streamObject);
        }

        using var output = new MemoryStream();
        Write(output, "%PDF-1.4\n%BA01\n");

        var offsets = new List<long> { 0 };

        for (var i = 0; i < objects.Count; i++)
        {
            offsets.Add(output.Position);
            Write(output, $"{i + 1} 0 obj\n");
            output.Write(objects[i], 0, objects[i].Length);
            Write(output, "\nendobj\n");
        }

        var xrefOffset = output.Position;
        Write(output, $"xref\n0 {objects.Count + 1}\n");
        Write(output, "0000000000 65535 f \n");

        for (var i = 1; i < offsets.Count; i++)
        {
            Write(output, $"{offsets[i]:D10} 00000 n \n");
        }

        Write(output,
            $"trailer\n<< /Size {objects.Count + 1} /Root 1 0 R >>\n" +
            $"startxref\n{xrefOffset}\n%%EOF");

        return output.ToArray();
    }

    private static void DrawText(
        StringBuilder page,
        double x,
        double y,
        string text,
        double size,
        bool bold = false,
        double r = 0.10,
        double g = 0.13,
        double b = 0.20)
    {
        page.AppendLine("BT");
        page.AppendLine($"/{(bold ? "F2" : "F1")} {F(size)} Tf");
        page.AppendLine($"{F(r)} {F(g)} {F(b)} rg");
        page.AppendLine($"1 0 0 1 {F(x)} {F(y)} Tm");
        page.AppendLine($"({Escape(Clean(text))}) Tj");
        page.AppendLine("ET");
    }

    private static void DrawCentered(
        StringBuilder page,
        double y,
        string text,
        double size,
        bool bold,
        double r,
        double g,
        double b)
    {
        var width = EstimateTextWidth(text, size, bold);
        DrawText(page, Math.Max(Margin, (PageWidth - width) / 2d), y, text, size, bold, r, g, b);
    }

    private static void DrawRight(
        StringBuilder page,
        double right,
        double y,
        string text,
        double size,
        bool bold = false,
        double r = 0.10,
        double g = 0.13,
        double b = 0.20)
    {
        var width = EstimateTextWidth(text, size, bold);
        DrawText(page, right - width, y, text, size, bold, r, g, b);
    }

    private static void DrawRule(
        StringBuilder page,
        double y,
        double gray = 0.82)
    {
        page.AppendLine($"{F(gray)} {F(gray)} {F(gray)} RG");
        page.AppendLine("0.7 w");
        page.AppendLine($"{F(Margin)} {F(y)} m {F(PageWidth - Margin)} {F(y)} l S");
    }

    private static void DrawRule(
        StringBuilder page,
        double y,
        double r,
        double g,
        double b)
    {
        page.AppendLine($"{F(r)} {F(g)} {F(b)} RG");
        page.AppendLine("1.2 w");
        page.AppendLine($"{F(Margin)} {F(y)} m {F(PageWidth - Margin)} {F(y)} l S");
    }

    private static double EstimateTextWidth(string? text, double size, bool bold)
    {
        var length = string.IsNullOrEmpty(text) ? 0 : text.Length;
        return length * size * (bold ? 0.56 : 0.52);
    }

    private static string Money(ReceiptData data, decimal value)
    {
        var currency = string.IsNullOrWhiteSpace(data.CurrencyName) ? "MYR" : Clean(data.CurrencyName);
        return $"{currency} {value:N2}";
    }

    private static string Truncate(string? value, int max)
    {
        var clean = Clean(value);
        return clean.Length <= max ? clean : clean[..Math.Max(0, max - 3)] + "...";
    }

    private static string Clean(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return "-";
        }

        var normalized = value
            .Replace("\r", " ")
            .Replace("\n", " ")
            .Replace("\t", " ")
            .Trim();

        var chars = normalized.Select(ch => ch is >= ' ' and <= '~' ? ch : '?').ToArray();
        return new string(chars);
    }

    private static string Escape(string value) =>
        value.Replace("\\", "\\\\").Replace("(", "\\(").Replace(")", "\\)");

    private static string F(double value) =>
        value.ToString("0.###", CultureInfo.InvariantCulture);

    private static byte[] Ascii(string value) => Encoding.ASCII.GetBytes(value);

    private static void Write(Stream stream, string value)
    {
        var bytes = Ascii(value);
        stream.Write(bytes, 0, bytes.Length);
    }
}
