using SenangRetails.Shared.Services.BluetoothPrinterService;
using SenangRetails.Shared.Services.ReceiptPrinterService;
using System.Net.Sockets;
using System.Text;
 using Microsoft.Maui.Controls.Shapes;
using static QuestPDF.Helpers.Colors;





#if ANDROID
using Android.Bluetooth;
#endif

namespace SenangRetails.Services.ReceiptPrinterService
{
    public class MauiReceiptPrinterService : IReceiptPrinterService
    {
        private static readonly string[] PrinterKeywords =
        {
            "printer", "print", "pos", "thermal", "receipt",
            "mpt", "rpp", "zj", "goojprt", "xprinter", "epson",
            "bixolon", "star", "citizen", "zebra", "tsc", "imin"
        };
        private readonly IminPrinterHelperService _iminService = new IminPrinterHelperService();
        public async Task<(bool Success, string Error)> PrintAsync(PrinterOption printer, ReceiptData data)
        {
            try
            {
                if (printer.IsIminPrinter)
                {
                    return await _iminService.PrintReceiptAsync(data);
                }
                byte[] receipt = BuildEscPosReceipt(data, printer.ReceiptMM == 80 ? 48 : 32);

                if (printer.IsWifiPrinter)
                    return await PrintNetworkAsync(printer.IpAddress, receipt);

                if (printer.IsBluetoothPrinter || printer.IsIminPrinter)
                    return await PrintBluetoothAsync(receipt);

                return (false, "No printer type selected.");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[MauiReceiptPrinterService] PrintAsync error: {ex.Message}");
                return (false, ex.Message);
            }
        }

        // ─── Network (TCP/IP) ────────────────────────────────────────────────

        private static async Task<(bool Success, string Error)> PrintNetworkAsync(string ipAddress, byte[] data)
        {
            try
            {
                using var client = new TcpClient();
                var connectTask = client.ConnectAsync(ipAddress, 9100);
                if (await Task.WhenAny(connectTask, Task.Delay(5000)) != connectTask)
                    return (false, $"Connection to {ipAddress}:9100 timed out.");

                await connectTask;
                using var stream = client.GetStream();
                stream.WriteTimeout = 10000;
                await stream.WriteAsync(data);
                await stream.FlushAsync();
                await Task.Delay(1500);
                return (true, "");
            }
            catch (Exception ex)
            {
                return (false, $"Network print error: {ex.Message}");
            }
        }

        // ─── Bluetooth ───────────────────────────────────────────────────────

        private static async Task<(bool Success, string Error)> PrintBluetoothAsync(byte[] data)
        {
#if ANDROID
            try
            {
#pragma warning disable CA1422
                var adapter = BluetoothAdapter.DefaultAdapter;
#pragma warning restore CA1422
                if (adapter == null)
                    return (false, "Bluetooth not available on this device.");
                if (!adapter.IsEnabled)
                    return (false, "Please turn on Bluetooth and try again.");

                adapter.CancelDiscovery();

                var device = FindPrinterDevice(adapter);
                if (device == null)
                    return (false, "No paired Bluetooth printer found. Please pair your printer in Android Bluetooth settings.");

                var uuid = Java.Util.UUID.FromString("00001101-0000-1000-8000-00805F9B34FB");
                var socket = device.CreateInsecureRfcommSocketToServiceRecord(uuid)
                    ?? throw new Exception("Failed to create Bluetooth socket.");

                try
                {
                    await socket.ConnectAsync();
                    var stream = socket.OutputStream
                        ?? throw new Exception("Could not open printer output stream.");

                    await stream.WriteAsync(data);
                    await stream.FlushAsync();
                    await Task.Delay(2000);

                    stream.Close();
                    return (true, "");
                }
                finally
                {
                    try { if (socket.IsConnected) socket.Close(); socket.Dispose(); } catch { }
                }
            }
            catch (Java.IO.IOException ex)
            {
                return (false, $"Bluetooth connection failed. Ensure printer is on and nearby. ({ex.Message})");
            }
            catch (Exception ex)
            {
                return (false, $"Bluetooth print error: {ex.Message}");
            }
#else
            await Task.CompletedTask;
            return (false, "Bluetooth printing is only supported on Android.");
#endif
        }

#if ANDROID
        private static BluetoothDevice? FindPrinterDevice(BluetoothAdapter adapter)
        {
            var devices = adapter.BondedDevices;
            if (devices == null || devices.Count == 0) return null;

            // Try name-matching first (fast)
            var byName = devices
                .Where(d => d.Name != null &&
                            PrinterKeywords.Any(k => d.Name!.Contains(k, StringComparison.OrdinalIgnoreCase)))
                .FirstOrDefault();

            if (byName != null) return byName;

            // Fallback: first SPP-capable device that's not a headset/phone
            var spp = Java.Util.UUID.FromString("00001101-0000-1000-8000-00805F9B34FB");
            foreach (var d in devices)
            {
                try
                {
                    var name = d.Name?.ToLower() ?? "";
                    if (name.Contains("phone") || name.Contains("headset") ||
                        name.Contains("speaker") || name.Contains("watch") ||
                        name.Contains("buds") || name.Contains("headphone"))
                        continue;

                    var uuids = d.GetUuids();
                    if (uuids != null && uuids.Any(u => u.Uuid.Equals(spp)))
                        return d;
                }
                catch { }
            }
            return null;
        }
#endif

        // ─── ESC/POS Receipt Builder ─────────────────────────────────────────

        private static byte[] BuildEscPosReceipt(ReceiptData data, int width)
        {
            Encoding enc;
            try { enc = Encoding.GetEncoding("GBK"); }
            catch { enc = Encoding.UTF8; }

            var buf = new List<byte>();

            // ESC @ — initialize printer
            buf.AddRange(new byte[] { 0x1B, 0x40 });

            // ── Header ──────────────────────────────────────────────────────
            if (!string.IsNullOrWhiteSpace(data.CompanyName))
            {
                buf.AddRange(Center());
                buf.AddRange(Bold(true));
                buf.AddRange(enc.GetBytes(Truncate(data.CompanyName, width)));
                buf.Add(0x0A);
                buf.AddRange(Bold(false));
            }
            //if (!string.IsNullOrWhiteSpace(data.BranchName))
            //{
            //    buf.AddRange(Center());
            //    buf.AddRange(enc.GetBytes(Truncate(data.BranchName, width)));
            //    buf.Add(0x0A);
            //}
            if (!string.IsNullOrWhiteSpace(data.Address1))
            {
                buf.AddRange(Center());
                buf.AddRange(enc.GetBytes(Truncate(data.Address1, width)));
                buf.Add(0x0A);
            }
            if (!string.IsNullOrWhiteSpace(data.Address2))
            {
                buf.AddRange(Center());
                buf.AddRange(enc.GetBytes(Truncate(data.Address2, width)));
                buf.Add(0x0A);
            }
            if (!string.IsNullOrWhiteSpace(data.Address3))
            {
                buf.AddRange(Center());
                buf.AddRange(enc.GetBytes(Truncate(data.Address3, width)));
                buf.Add(0x0A);
            }
            if (!string.IsNullOrWhiteSpace(data.Phone))
            {
                buf.AddRange(Center());
                buf.AddRange(enc.GetBytes($"Tel: {data.Phone}"));
                buf.Add(0x0A);
            }
            if (!string.IsNullOrWhiteSpace(data.Email))
            {
                buf.AddRange(Center());
                buf.AddRange(enc.GetBytes($"Email: {data.Email}"));
                buf.Add(0x0A);
            }
            if (!string.IsNullOrWhiteSpace(data.CoRegistrationNo))
            {
                buf.AddRange(Center());
                buf.AddRange(enc.GetBytes($"Co No: {data.CoRegistrationNo}"));
                buf.Add(0x0A);
            }
            if (!string.IsNullOrWhiteSpace(data.TIN))
            {
                buf.AddRange(Center());
                buf.AddRange(enc.GetBytes($"Tax No: {data.TIN}"));
                buf.Add(0x0A);
                buf.Add(0x0A);
            }
            buf.AddRange(enc.GetBytes(new string('-', width)));
            buf.Add(0x0A);

            // ── Transaction Info ──────────────────────────────────────────────
            buf.AddRange(Center()); 
            buf.AddRange(Bold(true));
            buf.AddRange(enc.GetBytes("Invoice\n"));
            buf.AddRange(Bold(false));

            buf.AddRange(Left());
            buf.AddRange(enc.GetBytes($"Date: {data.DateTimeOfSale:dd/MM/yyyy HH:mm}\n"));
            buf.AddRange(enc.GetBytes($"Doc No: {data.ReceiptNo}\n"));
            buf.AddRange(enc.GetBytes($"Ref No: {data.ReferenceNumber}\n"));
            buf.Add(0x0A);

            // ── Customer Info ──────────────────────────────────────────────
            buf.AddRange(enc.GetBytes(new string('-', width)));
            buf.Add(0x0A);
            if (!string.IsNullOrWhiteSpace(data.CustomerID))
            {
                buf.AddRange(Left());
                if(!string.IsNullOrWhiteSpace(data.CustomerName))
                 PrintLeft(buf, enc, $"  {data.CustomerName}", width);

                // Print Address lines if they exist
                if (!string.IsNullOrWhiteSpace(data.CustomerAddress1))
                     PrintLeft(buf, enc, $"  {data.CustomerAddress1}", width);

                if (!string.IsNullOrWhiteSpace(data.CustomerAddress2))
                     PrintLeft(buf, enc, $"  {data.CustomerAddress2}", width);
                
                // Combine Postcode, City, State
                //string cityLine = $"{data.CustomerPostcode} {data.CustomerCity} {data.CustomerState}".Trim();
                //if (!string.IsNullOrWhiteSpace(cityLine))
                //     PrintLeft(buf, enc, $"  {cityLine}", width);

                if (!string.IsNullOrWhiteSpace(data.CustomerCountry))
                     PrintLeft(buf, enc, $"  {data.CustomerCountry}", width);

                if (!string.IsNullOrWhiteSpace(data.CustomerPhone))
                     PrintLeft(buf, enc, $"  {data.CustomerPhone}", width);
                buf.Add(0x0A);
                buf.AddRange(enc.GetBytes(new string('-', width) + "\n"));
                
            }
            
            // ── Table Header ──────────────────────────────────────────────────
            // Widths: Qty(4), Price(10), Total(10). Remainder is Item Name.
            int qW = 3, pW = 8, tW = 10;
            int nW = width - qW - pW - tW - 3;

            string tableHeader = PadRightDisplay("Item", nW) +
                     " " + PadLeftDisplay("Qty", qW) +
                     " " + PadLeftDisplay("Price", pW) +
                     " " + PadLeftDisplay($"Total", tW);
            buf.AddRange(enc.GetBytes(tableHeader + "\n"));
            buf.AddRange(enc.GetBytes(new string('-', width) + "\n"));

            // ── Items ─────────────────────────────────────────────────────────
            decimal itemCount = 0;
            decimal totalDiscount = 0;
            foreach (var item in data.Items)
            {
                itemCount += item.Quantity;
                totalDiscount += item.Discount;
                // Row 1: Item Name (Full wrap)
                foreach (var namePart in WrapDisplay(item.Name, width))
                {
                    buf.AddRange(enc.GetBytes(namePart + "\n"));
                }

                if (!string.IsNullOrWhiteSpace(item.Remarks))
                {
                    string indent = "  "; 
                                          
                    foreach (var remarkPart in WrapDisplay(item.Remarks, width - 2))
                    {
                        buf.AddRange(enc.GetBytes(indent + remarkPart + "\n\n"));
                    }
                }

                // Row 2: Qty, Price, Total (Aligned right)
                string price = (item.LineTotal / Math.Max(1, item.Quantity)).ToString("F2");
                string metricsRow = new string(' ', nW) +
                        " " + PadLeftDisplay($"{item.Quantity}", qW) +
                        " " + PadLeftDisplay(price, pW) +
                        " " + PadLeftDisplay(item.LineTotal.ToString("F2"), tW);
                buf.AddRange(enc.GetBytes(metricsRow + "\n"));

                if (item.Discount > 0)
                {
                    string discRow = PadLeftDisplay($"(Disc: {item.Discount:F2})", width);
                    buf.AddRange(enc.GetBytes(discRow + "\n"));
                }
                buf.AddRange(enc.GetBytes("\n"));
            }
            buf.Add(0x0A);
            buf.AddRange(enc.GetBytes(new string('-', width) + "\n"));
            buf.Add(0x0A);

            // ── Totals ───────────────────────────────────────────────────────
            int labelW = width - tW;
            var serviceChargeText = data.TotalBeforeTax_ServiceCharge <= 0 ? "-" : data.TotalBeforeTax_ServiceCharge.ToString("0.00");

            buf.AddRange(enc.GetBytes(PadRightDisplay("Item Count:", labelW) + PadLeftDisplay(itemCount.ToString(), tW) + "\n"));
            buf.AddRange(enc.GetBytes(PadRightDisplay("Total Discount:", labelW) + PadLeftDisplay(totalDiscount.ToString("F2"), tW) + "\n"));
            buf.AddRange(enc.GetBytes(PadRightDisplay("Subtotal:", labelW) + PadLeftDisplay(data.Subtotal.ToString("F2"), tW) + "\n"));
            buf.AddRange(enc.GetBytes(PadRightDisplay("Service Charge:", labelW) + PadLeftDisplay(serviceChargeText, tW) + "\n"));
            buf.AddRange(enc.GetBytes(PadRightDisplay("Gov Tax:", labelW) + PadLeftDisplay(data.TaxAmount.ToString("F2"), tW) + "\n"));
            buf.AddRange(enc.GetBytes(PadRightDisplay("Rounding Adj:", labelW) + PadLeftDisplay($"{data.RoundingAmount:F2}", tW) + "\n"));

            buf.AddRange(enc.GetBytes(new string('-', width) + "\n"));
            buf.AddRange(Bold(true));
            buf.AddRange(enc.GetBytes(PadRightDisplay("GRAND TOTAL:", labelW) + PadLeftDisplay(data.GrandTotal.ToString("F2"), tW) + "\n"));
            buf.AddRange(Bold(false));

            buf.AddRange(enc.GetBytes(new string('-', width) + "\n\n"));
            buf.AddRange(enc.GetBytes(PadRightDisplay("Cashier:", labelW) + PadLeftDisplay(data.CashierName, tW) + "\n"));

            // multiple payment method
            foreach (var p in data.Payments)
            {
                string line = PadRightDisplay(p.Method + ":", width - 12) + PadLeftDisplay(p.Amount.ToString("F2"), 12);
                buf.AddRange(enc.GetBytes(line + "\n"));
            }

            if (data.ChangeAmount > 0)
            {
                string changeLine = PadRightDisplay("Change:", width - 12) + PadLeftDisplay($"({data.ChangeAmount:F2})", 12);
                buf.AddRange(enc.GetBytes(changeLine + "\n"));
            }

            buf.AddRange(enc.GetBytes(new string('-', width) + "\n"));

            // --- Tax Summary Row ---
            int taxColW = width == 48 ? 14 : 12; // Adjusted for 80mm vs 58mm
            int valColW = (width - taxColW) / 2;

            string taxHeader = PadRightDisplay("Tax Summary", taxColW) +
                               PadLeftDisplay("Amount", valColW) +
                               PadLeftDisplay("Tax", valColW);
            buf.AddRange(enc.GetBytes(taxHeader + "\n"));
            string currencyTag = $"({data.CurrencyName})";
            string taxSubHeader = PadRightDisplay("", taxColW) +
                      PadLeftDisplay(currencyTag, valColW) +
                      PadLeftDisplay(currencyTag, valColW);
            buf.AddRange(enc.GetBytes(taxSubHeader + "\n"));
            foreach (var t in data.TaxSummary)
            {
                string taxLine = PadRightDisplay(t.TaxCode, taxColW) +
                                 PadLeftDisplay(t.Amount.ToString("F2"), valColW) +
                                 PadLeftDisplay(t.Tax.ToString("F2"), valColW);
                buf.AddRange(enc.GetBytes(taxLine + "\n"));
            }

            buf.AddRange(enc.GetBytes(new string('-', width) + "\n\n\n"));

            if (!string.IsNullOrWhiteSpace(data.EInvoiceQrUrl))
            {
                var saleDate = data.DateTimeOfSale ?? DateTime.Now;
                var lastDayOfMonth = new DateTime(saleDate.Year, saleDate.Month, 1)
                    .AddMonths(1).AddDays(-1);

                buf.AddRange(Center());
                buf.AddRange(Bold(true));
                buf.AddRange(enc.GetBytes("eInvoice Request QR\n"));
                buf.AddRange(Bold(false));
                buf.AddRange(enc.GetBytes("Request must be made by\n"));
                buf.AddRange(enc.GetBytes($"{lastDayOfMonth:dd/MM/yyyy}\n"));
                
                // Spacing
                buf.Add(0x0A);
                buf.AddRange(enc.GetBytes("SCAN QR CODE" + "\n\n"));

                // QR Code Hardware Commands
                byte[] qrData = Encoding.UTF8.GetBytes(data.EInvoiceQrUrl);
                // 1. Set QR Model (Model 2)
                buf.AddRange(new byte[] { 0x1D, 0x28, 0x6B, 0x04, 0x00, 0x31, 0x41, 0x32, 0x00 });
                // 2. Set QR Size (Adjust 0x03 to 0x08 depending on preference)
                buf.AddRange(new byte[] { 0x1D, 0x28, 0x6B, 0x03, 0x00, 0x31, 0x43, 0x06 });
                // 3. Set Error correction (Level L)
                buf.AddRange(new byte[] { 0x1D, 0x28, 0x6B, 0x03, 0x00, 0x31, 0x45, 0x30 });
                // 4. Store Data
                int len = qrData.Length + 3;
                buf.AddRange(new byte[] { 0x1D, 0x28, 0x6B, (byte)(len % 256), (byte)(len / 256), 0x31, 0x50, 0x30 });
                buf.AddRange(qrData);
                // 5. Print QR
                buf.AddRange(new byte[] { 0x1D, 0x28, 0x6B, 0x03, 0x00, 0x31, 0x51, 0x30 });

                buf.Add(0x0A);
                buf.Add(0x0A);
            }

            // ── Footer ───────────────────────────────────────────────────────
            buf.AddRange(Center());
            buf.AddRange(enc.GetBytes("Thank You And See You Soon\n"));
            buf.AddRange(enc.GetBytes("Goods sold are non-returnable\n"));
            buf.AddRange(enc.GetBytes("and non-exchangeable\n"));
            
            buf.Add(0x0A);
            buf.Add(0x0A);
            buf.Add(0x0A);
            buf.Add(0x0A);
            buf.Add(0x0A);
            buf.Add(0x0A);
            // GS V 1 — partial cut
            buf.AddRange(new byte[] { 0x1D, 0x56, 0x01 });

            return buf.ToArray();
        }

        // ─── ESC/POS helpers ─────────────────────────────────────────────────

        private static byte[] Center() => new byte[] { 0x1B, 0x61, 0x01 };
        private static byte[] Left()   => new byte[] { 0x1B, 0x61, 0x00 };
        private static byte[] Bold(bool on) => new byte[] { 0x1B, 0x45, (byte)(on ? 1 : 0) };

        private static string PadRight(string s, int w) =>
            s.Length >= w ? s[..w] : s + new string(' ', w - s.Length);

        private static string PadLeft(string s, int w) =>
            s.Length >= w ? s[..w] : new string(' ', w - s.Length) + s;

        private static string Truncate(string s, int w) =>
            s.Length <= w ? s : s[..(w - 1)] + "~";

        // Checks if a character is "Wide" (occupies 2 columns on thermal printer)
        private static bool IsWideChar(char c)
        {
            return (c >= 0x4E00 && c <= 0x9FFF) || (c >= 0x3400 && c <= 0x4DBF) ||
                   (c >= 0x20000 && c <= 0x2A6DF) || (c >= 0x2A700 && c <= 0x2B73F);
        }

        private static int GetDisplayWidth(string s)
        {
            int width = 0;
            foreach (var c in s) width += IsWideChar(c) ? 2 : 1;
            return width;
        }

        private static string PadRightDisplay(string s, int totalWidth)
        {
            int pad = totalWidth - GetDisplayWidth(s);
            return s + new string(' ', Math.Max(0, pad));
        }

        private static string PadLeftDisplay(string s, int totalWidth)
        {
            int pad = totalWidth - GetDisplayWidth(s);
            return new string(' ', Math.Max(0, pad)) + s;
        }

        private static string PadCentered(string s, int totalWidth)
        {
            int sw = GetDisplayWidth(s);
            if (sw >= totalWidth) return s;
            int left = (totalWidth - sw) / 2;
            return new string(' ', left) + s;
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

        private static void PrintLeft(List<byte> buf, Encoding enc, string text, int width)
        {
            foreach (var line in WrapDisplay(text, width))
            {
                buf.AddRange(enc.GetBytes(line + "\n"));
            }
        }
    }
}
