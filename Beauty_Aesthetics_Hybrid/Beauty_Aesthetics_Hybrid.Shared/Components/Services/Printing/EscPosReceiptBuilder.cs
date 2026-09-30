using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Beauty_Aesthetics_WebPos.Components.Services.Printing
{
    public static class EscPosReceiptBuilder
    {
        private static bool _encodingRegistered = false;

        private static void EnsureEncodingRegistered()
        {
            if (!_encodingRegistered)
            {
                try
                {
                    Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
                    _encodingRegistered = true;
                }
                catch
                {
                    // Fallback
                }
            }
        }

        private static Encoding GetChineseEncoding()
        {
            EnsureEncodingRegistered();
            string[] encodingNames = { "GBK", "GB2312", "GB18030", "UTF-8" };
            foreach (var encodingName in encodingNames)
            {
                try
                {
                    return Encoding.GetEncoding(encodingName);
                }
                catch
                {
                    // Continue
                }
            }
            return Encoding.UTF8;
        }

        public static byte[] BuildEscPosReceipt(ReceiptData data, int width)
        {
            Encoding enc = GetChineseEncoding();
            var buf = new List<byte>();

            // ESC @ — initialize printer
            buf.AddRange(new byte[] { 0x1B, 0x40 });
            // Select character code table
            buf.AddRange(new byte[] { 0x1B, 0x74, 0x1C });
            // Font A
            buf.AddRange(new byte[] { 0x1B, 0x4D, 0x00 });

            // Header
            // Raster logos are not portable across ESC/POS models. Use the EBI
            // text mark for direct printer bytes; HTML printing uses the image.
            PrintCenteredWithWordWrap(buf, enc, ReceiptBranding.BrandMark, width, bold: true);
            if (!string.IsNullOrEmpty(data.OutletName))
            {
                PrintCenteredWithWordWrap(buf, enc, data.OutletName, width, bold: true);
                PrintCenteredWithWordWrap(buf, enc, $"Dimiliki oleh: {data.CompanyName}", width, bold: true);
                if (!string.IsNullOrEmpty(data.CoRegistrationNo))
                    PrintCentered(buf, enc, $"Co No: {data.CoRegistrationNo}", width);
            }
            else
            {
                PrintCenteredWithWordWrap(buf, enc, data.CompanyName, width, bold: true);
                if (!string.IsNullOrEmpty(data.CoRegistrationNo))
                    PrintCentered(buf, enc, $"Co No: {data.CoRegistrationNo}", width);
            }

            if (!string.IsNullOrEmpty(data.Address1))
                PrintCenteredWithWordWrap(buf, enc, data.Address1, maxWidth: width);
            if (!string.IsNullOrEmpty(data.Address2))
                PrintCenteredWithWordWrap(buf, enc, data.Address2, maxWidth: width);
            if (!string.IsNullOrEmpty(data.Address3))
                PrintCenteredWithWordWrap(buf, enc, data.Address3, maxWidth: width);
            if (!string.IsNullOrEmpty(data.Phone))
                PrintCentered(buf, enc, $"Tel: {data.Phone}", width);
            if (!string.IsNullOrEmpty(data.Email))
                PrintCenteredWithWordWrap(buf, enc, $"Email: {data.Email}", maxWidth: width);
            if (!string.IsNullOrEmpty(data.TIN))
                PrintCentered(buf, enc, $"Tax No: {data.TIN}", width);

            buf.AddRange(enc.GetBytes("\n"));
            buf.AddRange(enc.GetBytes(new string('-', width) + "\n"));

            // Transaction Info
            PrintCentered(buf, enc, "Invoice", width, bold: true);
            PrintLeft(buf, enc, $"Date: {data.DateTimeOfSale:dd/MM/yyyy HH:mm}", width);
            PrintLeft(buf, enc, $"Doc No: {data.ReceiptNo}", width);
            PrintLeft(buf, enc, $"Ref No: {data.ReferenceNumber}", width);

            buf.AddRange(enc.GetBytes("\n"));
            buf.AddRange(enc.GetBytes(new string('-', width) + "\n"));

            // Customer Info
            if (!string.IsNullOrWhiteSpace(data.CustomerID))
            {
                var addressParts = new[] { data.CustomerPostcode, data.CustomerCity, data.CustomerState };
                string joinedAddress = string.Join(", ", addressParts.Where(s => !string.IsNullOrWhiteSpace(s)));
                string address3 = string.IsNullOrWhiteSpace(joinedAddress) ? "" : $"  {joinedAddress}";

                if (!string.IsNullOrWhiteSpace(data.CustomerName))
                    PrintLeft(buf, enc, $"  {data.CustomerName}", width);

                if (!string.IsNullOrWhiteSpace(data.CustomerAddress1))
                    PrintLeft(buf, enc, $"  {data.CustomerAddress1}", width);

                if (!string.IsNullOrWhiteSpace(data.CustomerAddress2))
                    PrintLeft(buf, enc, $"  {data.CustomerAddress2}", width);

                if (!string.IsNullOrWhiteSpace(address3))
                    PrintLeft(buf, enc, address3, width);

                if (!string.IsNullOrWhiteSpace(data.CustomerCountry))
                    PrintLeft(buf, enc, $"  {data.CustomerCountry}", width);

                if (!string.IsNullOrWhiteSpace(data.CustomerPhone))
                    PrintLeft(buf, enc, $"  {data.CustomerPhone}", width);

                buf.AddRange(enc.GetBytes("\n"));
                buf.AddRange(enc.GetBytes(new string('-', width) + "\n"));
            }

            // Table Header
            int qHeaderWidth = 3;
            int pHeaderWidth = 9;
            int tHeaderWidth = 12;
            int iHeaderWidth = width - qHeaderWidth - pHeaderWidth - tHeaderWidth;
            string currency = data.CurrencyName ?? "MYR";

            string headerRow = PadRightDisplay("Item", iHeaderWidth) +
                               PadLeftDisplay("Qty", qHeaderWidth) +
                               PadLeftDisplay("Price", pHeaderWidth) +
                               PadLeftDisplay($"Total({currency})", tHeaderWidth);

            PrintLeft(buf, enc, headerRow, width);
            buf.AddRange(enc.GetBytes(new string('-', width) + "\n"));

            // Items loop
            decimal itemCount = 0;
            decimal totalDiscount = 0;

            foreach (var item in data.Items)
            {
                itemCount += item.Quantity;
                totalDiscount += item.Discount;

                decimal calculatedUnitPrice = item.Quantity > 0 ? ((item.LineTotal + item.Discount) / item.Quantity) : 0m;
                decimal calculatedTotal = item.Quantity * calculatedUnitPrice;

                PrintItemDetailedLine(
                    buf,
                    enc,
                    item.Quantity.ToString("#,##0.00"),
                    item.Name,
                    calculatedUnitPrice.ToString("#,##0.00"),
                    item.Discount.ToString("#,##0.00"),
                    calculatedTotal.ToString("#,##0.00"),
                    item.Remarks,
                    width
                );
            }

            buf.AddRange(enc.GetBytes(new string('-', width) + "\n"));
            buf.AddRange(enc.GetBytes("\n"));

            // Summary Section
            string itemCountText = ((int)itemCount).ToString();
            string totalDiscountText = totalDiscount <= 0 ? "-" : totalDiscount.ToString("0.00");
            string serviceChargeText = data.TotalBeforeTax_ServiceCharge <= 0 ? "-" : data.TotalBeforeTax_ServiceCharge.ToString("0.00");

            PrintColumns(buf, enc, "Item Count:", itemCountText, width);
            PrintColumns(buf, enc, "Total Discount:", totalDiscountText, width);

            PrintColumns(buf, enc, "SubTotal", $"{data.Subtotal:#,##0.00}", width);
            PrintColumns(buf, enc, "Service Charge", $"{serviceChargeText}", width);
            PrintColumns(buf, enc, "Gov Tax", $"{data.TaxAmount:#,##0.00}", width);
            PrintColumns(buf, enc, "Rounding Adj", $"{data.RoundingAmount:#,##0.00}", width);

            buf.AddRange(enc.GetBytes(new string('-', width) + "\n"));
            PrintColumns(buf, enc, "GRAND TOTAL", $"{data.GrandTotal:#,##0.00}", width, bold: true);
            buf.AddRange(enc.GetBytes(new string('-', width) + "\n"));
            buf.AddRange(enc.GetBytes("\n"));

            // Payment section
            PrintColumns(buf, enc, "Cashier:", $"{data.CashierName ?? string.Empty}", width);

            foreach (var p in data.Payments)
            {
                PrintColumns(buf, enc, p.Method, $"{p.Amount:#,##0.00}", width);
            }

            if (data.ChangeAmount > 0)
            {
                PrintColumns(buf, enc, "Change", $"({data.ChangeAmount:#,##0.00})", width);
            }

            buf.AddRange(enc.GetBytes(new string('-', width) + "\n"));

            // Tax Summary Table
            PrintTaxTableHeader(data.CurrencyName ?? "MYR", buf, enc, width);

            foreach (var t in data.TaxSummary)
            {
                PrintTaxTableLine(
                    buf,
                    enc,
                    t.TaxCode,
                    t.Amount.ToString("#,##0.00"),
                    t.Tax.ToString("#,##0.00"),
                    width
                );
            }

            buf.AddRange(enc.GetBytes(new string('-', width) + "\n"));
            buf.AddRange(enc.GetBytes("\n"));

            // Footer
            PrintCentered(buf, enc, "Thank You And See You Soon", width);
            PrintCenteredWithWordWrap(buf, enc, "Goods sold are non-returnable and non-exchangeable", maxWidth: width);

            buf.Add(0x0A);
            buf.Add(0x0A);
            buf.Add(0x0A);
            buf.Add(0x0A);

            // Cut paper
            buf.AddRange(new byte[] { 0x1D, 0x56, 0x42, 0x10 });

            return buf.ToArray();
        }

        public static string BuildHtmlReceipt(ReceiptData data, decimal paperMm)
        {
            var sb = new StringBuilder();
            string widthPx = paperMm == 80 ? "78mm" : "54mm";

            sb.Append($@"
<div class=""thermal-receipt"" style=""width:{widthPx}; font-family:'Courier New', monospace; font-size:12px; color:#000; background:#fff; margin:0 auto; padding:4px;"">
    <div style=""text-align:center; margin-bottom:5px;""><img src=""{ReceiptBranding.LogoUrl}"" alt=""EBI logo"" style=""display:block; width:90px; height:34px; margin:0 auto; object-fit:cover; object-position:center;"" /></div>
    <div style=""text-align:center; font-weight:bold; font-size:14px;"">{(string.IsNullOrEmpty(data.OutletName) ? data.CompanyName : data.OutletName)}</div>");

            if (!string.IsNullOrEmpty(data.OutletName))
                sb.Append($@"<div style=""text-align:center; font-size:11px;"">Dimiliki oleh: {data.CompanyName}</div>");

            if (!string.IsNullOrEmpty(data.CoRegistrationNo))
                sb.Append($@"<div style=""text-align:center; font-size:11px;"">Co No: {data.CoRegistrationNo}</div>");

            if (!string.IsNullOrEmpty(data.Address1)) sb.Append($@"<div style=""text-align:center; font-size:11px;"">{data.Address1}</div>");
            if (!string.IsNullOrEmpty(data.Address2)) sb.Append($@"<div style=""text-align:center; font-size:11px;"">{data.Address2}</div>");
            if (!string.IsNullOrEmpty(data.Address3)) sb.Append($@"<div style=""text-align:center; font-size:11px;"">{data.Address3}</div>");
            if (!string.IsNullOrEmpty(data.Phone)) sb.Append($@"<div style=""text-align:center; font-size:11px;"">Tel: {data.Phone}</div>");
            if (!string.IsNullOrEmpty(data.TIN)) sb.Append($@"<div style=""text-align:center; font-size:11px;"">Tax No: {data.TIN}</div>");

            sb.Append(@"<hr style=""border:none; border-top:1px dashed #000; margin:6px 0;"" />");
            sb.Append($@"
    <div style=""text-align:center; font-weight:bold; margin-bottom:4px;"">Invoice</div>
    <div>Date: {data.DateTimeOfSale:dd/MM/yyyy HH:mm}</div>
    <div>Doc No: {data.ReceiptNo}</div>
    <div>Ref No: {data.ReferenceNumber}</div>");

            if (!string.IsNullOrWhiteSpace(data.CustomerName))
            {
                sb.Append(@"<hr style=""border:none; border-top:1px dashed #000; margin:6px 0;"" />");
                sb.Append($@"<div>Customer: {data.CustomerName}</div>");
                if (!string.IsNullOrWhiteSpace(data.CustomerPhone))
                    sb.Append($@"<div>Tel: {data.CustomerPhone}</div>");
            }

            sb.Append(@"<hr style=""border:none; border-top:1px dashed #000; margin:6px 0;"" />");
            sb.Append(@"
    <table style=""width:100%; border-collapse:collapse; font-size:11px; font-family:inherit;"">
        <thead>
            <tr style=""border-bottom:1px dashed #000; text-align:left;"">
                <th style=""text-align:left;"">Item</th>
                <th style=""text-align:right;"">Qty</th>
                <th style=""text-align:right;"">Price</th>
                <th style=""text-align:right;"">Total</th>
            </tr>
        </thead>
        <tbody>");

            foreach (var item in data.Items)
            {
                decimal unitPrice = item.Quantity > 0 ? ((item.LineTotal + item.Discount) / item.Quantity) : 0m;
                decimal lineTotal = item.Quantity * unitPrice;

                sb.Append($@"
            <tr>
                <td colspan=""4"" style=""font-weight:bold; padding-top:4px;"">{item.Name}</td>
            </tr>
            <tr>
                <td></td>
                <td style=""text-align:right;"">{item.Quantity:0.00}</td>
                <td style=""text-align:right;"">{unitPrice:0.00}</td>
                <td style=""text-align:right;"">{lineTotal:0.00}</td>
            </tr>");
                if (!string.IsNullOrWhiteSpace(item.Remarks))
                {
                    sb.Append($@"<tr><td colspan=""4"" style=""font-size:10px; color:#444; padding-left:8px;"">{item.Remarks}</td></tr>");
                }
            }

            sb.Append(@"
        </tbody>
    </table>
    <hr style=""border:none; border-top:1px dashed #000; margin:6px 0;"" />");

            sb.Append($@"
    <div style=""display:flex; justify-content:space-between;""><span>SubTotal:</span><span>{data.Subtotal:#,##0.00}</span></div>
    <div style=""display:flex; justify-content:space-between;""><span>Gov Tax:</span><span>{data.TaxAmount:#,##0.00}</span></div>
    <div style=""display:flex; justify-content:space-between;""><span>Rounding:</span><span>{data.RoundingAmount:#,##0.00}</span></div>
    <hr style=""border:none; border-top:1px dashed #000; margin:4px 0;"" />
    <div style=""display:flex; justify-content:space-between; font-weight:bold; font-size:13px;""><span>GRAND TOTAL:</span><span>{data.GrandTotal:#,##0.00}</span></div>
    <hr style=""border:none; border-top:1px dashed #000; margin:4px 0;"" />");

            if (!string.IsNullOrEmpty(data.CashierName))
                sb.Append($@"<div>Cashier: {data.CashierName}</div>");

            foreach (var p in data.Payments)
            {
                sb.Append($@"<div style=""display:flex; justify-content:space-between;""><span>{p.Method}:</span><span>{p.Amount:#,##0.00}</span></div>");
            }

            if (data.ChangeAmount > 0)
            {
                sb.Append($@"<div style=""display:flex; justify-content:space-between;""><span>Change:</span><span>({data.ChangeAmount:#,##0.00})</span></div>");
            }

            sb.Append(@"
    <div style=""text-align:center; margin-top:12px; font-size:11px;"">Thank You And See You Soon</div>
</div>");

            return sb.ToString();
        }

        // Helpers
        private static bool IsWideChar(char c)
        {
            return (c >= 0x4E00 && c <= 0x9FFF)
                || (c >= 0x3400 && c <= 0x4DBF)
                || (c >= 0x20000 && c <= 0x2A6DF);
        }

        private static int GetDisplayWidth(string s)
        {
            int w = 0;
            foreach (var c in s) w += IsWideChar(c) ? 2 : 1;
            return w;
        }

        private static string PadRightDisplay(string s, int totalWidth)
        {
            int pad = totalWidth - GetDisplayWidth(s);
            return pad <= 0 ? s : s + new string(' ', pad);
        }

        private static string PadLeftDisplay(string s, int totalWidth)
        {
            int pad = totalWidth - GetDisplayWidth(s);
            return pad <= 0 ? s : new string(' ', pad) + s;
        }

        private static List<string> WrapDisplay(string text, int maxWidth)
        {
            var lines = new List<string>();
            var current = new StringBuilder();
            int width = 0;

            foreach (char c in text)
            {
                int w = IsWideChar(c) ? 2 : 1;
                if (width + w > maxWidth)
                {
                    lines.Add(current.ToString());
                    current.Clear();
                    width = 0;
                }
                current.Append(c);
                width += w;
            }
            if (current.Length > 0) lines.Add(current.ToString());
            return lines;
        }

        private static List<string> WrapText(string text, int maxWidth)
        {
            var lines = new List<string>();
            var words = text.Split(' ');
            var currentLine = new StringBuilder();

            foreach (var word in words)
            {
                if (word.Length > maxWidth)
                {
                    if (currentLine.Length > 0)
                    {
                        lines.Add(currentLine.ToString());
                        currentLine.Clear();
                    }
                    for (int i = 0; i < word.Length; i += maxWidth)
                    {
                        int length = Math.Min(maxWidth, word.Length - i);
                        lines.Add(word.Substring(i, length));
                    }
                    continue;
                }

                if (currentLine.Length + word.Length + 1 > maxWidth)
                {
                    if (currentLine.Length > 0)
                    {
                        lines.Add(currentLine.ToString());
                        currentLine.Clear();
                    }
                }

                if (currentLine.Length > 0) currentLine.Append(' ');
                currentLine.Append(word);
            }

            if (currentLine.Length > 0) lines.Add(currentLine.ToString());
            return lines;
        }

        private static void PrintLeft(List<byte> buf, Encoding enc, string text, int width, bool bold = false)
        {
            buf.AddRange(new byte[] { 0x1B, 0x61, 0x00, 0x1B, 0x4D, 0x00, 0x1D, 0x21, 0x00 });
            if (bold) buf.AddRange(new byte[] { 0x1B, 0x45, 0x01 });
            foreach (var line in WrapDisplay(text, width))
                buf.AddRange(enc.GetBytes(line + "\n"));
            if (bold) buf.AddRange(new byte[] { 0x1B, 0x45, 0x00 });
        }

        private static void PrintCentered(List<byte> buf, Encoding enc, string text, int width, bool bold = false)
        {
            buf.AddRange(new byte[] { 0x1B, 0x61, 0x01 });
            if (bold) buf.AddRange(new byte[] { 0x1B, 0x45, 0x01 });
            foreach (var line in WrapText(text, width))
                buf.AddRange(enc.GetBytes(line + "\n"));
            if (bold) buf.AddRange(new byte[] { 0x1B, 0x45, 0x00 });
            buf.AddRange(new byte[] { 0x1B, 0x61, 0x00 });
        }

        private static void PrintCenteredWithWordWrap(List<byte> buf, Encoding enc, string text, int maxWidth, bool bold = false)
        {
            if (string.IsNullOrEmpty(text)) return;
            foreach (var line in WrapText(text, maxWidth))
                PrintCentered(buf, enc, line, maxWidth, bold);
        }

        private static void PrintColumns(List<byte> buf, Encoding enc, string left, string right, int lineWidth = 32, bool bold = false)
        {
            buf.AddRange(new byte[] { 0x1B, 0x61, 0x00 });
            if (bold) buf.AddRange(new byte[] { 0x1B, 0x45, 0x01 });
            int rightLength = GetDisplayWidth(right);
            int leftLength = lineWidth - rightLength;
            string formatted = PadRightDisplay(left, leftLength) + right;
            buf.AddRange(enc.GetBytes(formatted + "\n"));
            if (bold) buf.AddRange(new byte[] { 0x1B, 0x45, 0x00 });
        }

        private static void PrintItemDetailedLine(List<byte> buf, Encoding enc, string qty, string itemName, string unitPrice, string discount, string total, string remark, int lineWidth = 32)
        {
            foreach (var part in WrapDisplay(itemName, lineWidth))
                PrintLeft(buf, enc, part, lineWidth);

            if (!string.IsNullOrWhiteSpace(remark))
            {
                foreach (var part in WrapDisplay(remark, lineWidth - 2))
                    PrintLeft(buf, enc, "  " + part, lineWidth);
            }

            int qCol = 6, pCol = 9, tCol = 12;
            int leftPadding = lineWidth - qCol - pCol - tCol;
            string numbersRow = new string(' ', Math.Max(0, leftPadding)) +
                               PadLeftDisplay(qty, qCol) +
                               PadLeftDisplay(unitPrice, pCol) +
                               PadLeftDisplay(total, tCol);

            PrintLeft(buf, enc, numbersRow, lineWidth);

            if (decimal.TryParse(discount, out decimal dclDiscount) && dclDiscount != 0)
            {
                string discStr = $"  (Disc: {dclDiscount:#,##0.00})";
                PrintLeft(buf, enc, PadLeftDisplay(discStr, lineWidth), lineWidth);
            }
            buf.AddRange(enc.GetBytes("\n"));
        }

        private static void PrintTaxTableHeader(string currencyName, List<byte> buf, Encoding enc, int lineWidth = 32)
        {
            int amountWidth = lineWidth >= 42 ? 14 : 10;
            int taxWidth = lineWidth >= 42 ? 14 : 9;
            int taxCodeWidth = lineWidth - amountWidth - taxWidth;

            string headerLine1 = "Tax Summary".PadRight(taxCodeWidth) + "Amount".PadLeft(amountWidth) + "Tax".PadLeft(taxWidth);
            string headerLine2 = "".PadRight(taxCodeWidth) + $"({currencyName})".PadLeft(amountWidth) + $" ({currencyName})".PadLeft(taxWidth);

            PrintLeft(buf, enc, headerLine1, lineWidth);
            PrintLeft(buf, enc, headerLine2, lineWidth);
        }

        private static void PrintTaxTableLine(List<byte> buf, Encoding enc, string taxCode, string amount, string tax, int lineWidth = 32)
        {
            int amountWidth = lineWidth >= 42 ? 14 : 10;
            int taxWidth = lineWidth >= 42 ? 14 : 9;
            int taxCodeWidth = lineWidth - amountWidth - taxWidth;

            var taxCodeParts = WrapText(taxCode, taxCodeWidth);
            var amountParts = WrapText(amount, amountWidth);
            var taxParts = WrapText(tax, taxWidth);

            int maxLines = Math.Max(taxCodeParts.Count, Math.Max(amountParts.Count, taxParts.Count));
            for (int i = 0; i < maxLines; i++)
            {
                string tCode = i < taxCodeParts.Count ? taxCodeParts[i] : "";
                string amt = i < amountParts.Count ? amountParts[i] : "";
                string tx = i < taxParts.Count ? taxParts[i] : "";

                string line = tCode.PadRight(taxCodeWidth) + amt.PadLeft(amountWidth) + tx.PadLeft(taxWidth);
                PrintLeft(buf, enc, line, lineWidth);
            }
        }
    }
}
