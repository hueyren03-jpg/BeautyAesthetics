#if ANDROID
using Android.Graphics;
using Com.Imin.Printer;
using SenangRetails.Platforms.Android; 
using SenangRetails.Shared.Services.ReceiptPrinterService;
#endif
using System.Text;

namespace SenangRetails.Services.ReceiptPrinterService
{
    public class IminPrinterHelperService
    {
        public async Task<(bool Success, string Error)> PrintReceiptAsync(SenangRetails.Shared.Services.ReceiptPrinterService.ReceiptData data)
        {
#if ANDROID
            try
            {
                var printer = PrinterHelper.Instance; // Your Android Hardware Helper
                if (printer == null) return (false, "iMin Printer not initialized.");

                var lstString = new List<string[]>();
                string width = "384"; // Default for 58mm iMin devices

                // 1. Header
                lstString.Add(new[] { data.CompanyName, "1", "1", "26", width });
                if (!string.IsNullOrEmpty(data.Address1)) lstString.Add(new[] { data.Address1, "1", "0", "22", width });
                if (!string.IsNullOrEmpty(data.Address2)) lstString.Add(new[] { data.Address2, "1", "0", "22", width });
                if (!string.IsNullOrEmpty(data.Address3)) lstString.Add(new[] { data.Address3, "1", "0", "22", width });
                if (!string.IsNullOrEmpty(data.Phone)) lstString.Add(new[] { $"Tel: {data.Phone}", "1", "0", "22", width });
                if (!string.IsNullOrEmpty(data.Email)) lstString.Add(new[] { $"Email: {data.Email}", "1", "0", "22", width });
                if (!string.IsNullOrEmpty(data.CoRegistrationNo)) lstString.Add(new[] { $"Co No: {data.CoRegistrationNo}", "1", "0", "22", width });
                if (!string.IsNullOrEmpty(data.TIN)) lstString.Add(new[] { $"Tax No: {data.TIN}", "1", "0", "22", width });
                lstString.Add(new[] { " ", "1", "0", "25", "384" });
                // Invoice header
                lstString.Add(new[] { "Invoice", "1", "1", "28", "384" }); // Bold Invoice
                lstString.Add(new[] { $"Date: {data.DateTimeOfSale:dd/MM/yyyy HH:mm}", "1", "0", "22", width });
                lstString.Add(new[] { $"Doc No: {data.ReceiptNo}", "1", "0", "22", width });
                lstString.Add(new[] { $"Ref No: {data.ReferenceNumber}", "1", "0", "22", width });
                lstString.Add(new[] { " ", "1", "0", "25", "384" });
                // Divider
                lstString.Add(new[] { "------------------------------------------------------", "1", "0", "25", "384" });
                // 3. Customer
                if (!string.IsNullOrEmpty(data.CustomerID))
                {
                    lstString.Add(new string[] { "    ", "1", "0", "25", "384" });
                    lstString.Add(new[] { $"{data.CustomerName}", "1", "0", "25", width });
                    if (!string.IsNullOrEmpty(data.CustomerAddress1)) lstString.Add(new[] { $"  {data.CustomerAddress1}", "1", "0", "25", width });
                    if (!string.IsNullOrEmpty(data.CustomerAddress2)) lstString.Add(new[] { $"  {data.CustomerAddress2}", "1", "0", "25", width });
                    if (!string.IsNullOrEmpty(data.CustomerPhone)) lstString.Add(new[] { $"  {data.CustomerPhone}", "1", "0", "25", width });
                    lstString.Add(new string[] { "    ", "1", "0", "25", "384" });
                    lstString.Add(new string[] { "------------------------------------------------------", "1", "0", "25", "384" });
                }

                // 4. Table Header
                lstString.Add(new[] { $"Item|Qty|Price|Total({data.CurrencyName})", "4,3,3,5", "0,2,2,2", "22,22,22,22", width });
                lstString.Add(new[] { "------------------------------------------------------", "1", "0", "25", "384" });
                // 5. Items
                foreach (var item in data.Items)
                {
                    lstString.Add(new[] { item.Name, "0", "0", "22", width });
                    if (!string.IsNullOrEmpty(item.Remarks))
                        lstString.Add(new[] { $"  *{item.Remarks}", "0", "0", "22", width });

                    string unitPrice = (item.LineTotal / Math.Max(1, item.Quantity)).ToString("F2");
                    lstString.Add(new[] { $" |{item.Quantity}|{unitPrice}|{item.LineTotal:F2}", "4,3,3,5", "0,2,2,2", "22,22,22,22", width });
                    // 4. Discount (Aligned under the numbers)
                    if (item.Discount > 0)
                    {
                        lstString.Add(new[]
                        {
                            $" | |(Disc: {item.Discount:#,##0.00})",
                            "4,1,4",      // Grouping Price/Total columns for the discount text
                            "0,0,2",
                            "22,22,22",
                            "384"
                        });
                    }
                }

                lstString.Add(new[] { "------------------------------------------------------", "1", "0", "25", "384" });
                lstString.Add(new[] { " ", "1", "0", "25", "384" });
                // Summary section
                var itemCount = 0m;
                var totalDiscount = 0m;
                foreach (var item in data.Items)
                {
                    itemCount += item.Quantity;
                    totalDiscount += item.Discount;
                }
                var itemCountText = ((int)itemCount).ToString();
                string totalDiscountText = totalDiscount <= 0 ? "-" : totalDiscount.ToString("0.00");
                var serviceChargeText = data.TotalBeforeTax_ServiceCharge <= 0 ? "-" : data.TotalBeforeTax_ServiceCharge.ToString("0.00");
                lstString.Add(new[]
                {
                        $"Item Count:|{itemCountText}",
                        "1,1", "0,2", "22,22", "384"
                    });
                lstString.Add(new[]
                {
                        $"Total Discount:|{totalDiscountText:#,##0.00}",
                        "1,1", "0,2", "22,22", "384"
                    });
                // 6. Totals
                lstString.Add(new[] { $"Subtotal:|{data.Subtotal:F2}", "1,1", "0,2", "22,22", width });
                lstString.Add(new[]
                {
                        $"Service Charge|{serviceChargeText:#,##0.00}",
                        "1,1", "0,2", "22,22", "384"
                    });
                lstString.Add(new[]
                {
                        $"Gov Tax|{data.TaxAmount:#,##0.00}",
                        "1,1", "0,2", "22,22", "384"
                    });
                lstString.Add(new[]
                {
                        $"Rounding Adj|{data.RoundingAmount:#,##0.00}",
                        "1,1", "0,2", "22,22", "384"
                    });
                lstString.Add(new[] { "------------------------------------------------------", "1", "0", "25", "384" });
                lstString.Add(new[] { $"GRAND TOTAL:|{data.GrandTotal:F2}", "1,1", "0,2", "26,26", width });
                lstString.Add(new[] { "------------------------------------------------------", "1", "0", "25", "384" });

                lstString.Add(new[] { " ", "1", "0", "25", "384" });
                // 7. Payments
                lstString.Add(new[] { $"Cashier:|{data.CashierName}", "1,1", "0,2", "22,22", width });
                foreach (var p in data.Payments)
                    lstString.Add(new[] { $"{p.Method}:|{p.Amount:F2}", "1,1", "0,2", "22,22", width });
               
                if (data.ChangeAmount > 0)
                {
                    var dclChangeAmount = Math.Abs(data.ChangeAmount);
                    lstString.Add(new[]
                    {
                        $"Change|({dclChangeAmount:#,##0.00})",
                        "1,1", "0,2", "22,22", "384"
                    });
                }
                lstString.Add(new[] { "------------------------------------------------------", "1", "0", "25", "384" });
                // 8. Tax Summary
                lstString.Add(new[] { "Tax Summary|Amount|Tax", "2,1,1", "0,2,2", "22,22,22", width });
                // Tax Summary
                lstString.Add(new[]
                {
                    $"|({data.CurrencyName})|({data.CurrencyName})",
                    "2,1,1",
                    "0,2,2",
                    "22,22,22",
                    "384"
                });
                foreach (var t in data.TaxSummary)
                    lstString.Add(new[] { $"{t.TaxCode}|{t.Amount:F2}|{t.Tax:F2}", "2,1,1", "0,2,2", "22,22,22", width });
                lstString.Add(new[] { "------------------------------------------------------", "1", "0", "25", "384" });
                lstString.Add(new[] { " ", "1", "0", "25", "384" });
                // 9. QR Code
                if (!string.IsNullOrEmpty(data.EInvoiceQrUrl))
                {
                    var saleDate = data.DateTimeOfSale ?? DateTime.Now;
                    var dueDate = new DateTime(saleDate.Year, saleDate.Month, 1)
                    .AddMonths(1).AddDays(-1);
                    lstString.Add(new[] { "eInvoice Request QR", "1", "1", "24", "384" });
                    lstString.Add(new[] { $"Request must be made by {dueDate:dd/MM/yyyy}", "1", "0", "25", "384" });
                    lstString.Add(new[] { $" ", "1", "0", "25", "384" });

                    lstString.Add(new[] { $"SCAN QR CODE", "1", "0", "25", "384" });


                    lstString.Add(new[] { "QRCODE" });
                    lstString.Add(new[] { data.EInvoiceQrUrl });
                }

                // Footer
                lstString.Add(new[] { " ", "1", "0", "25", "384" });
                lstString.Add(new[] { "Thank You And See You Soon", "1", "0", "22", "384" });
                lstString.Add(new[] { "Goods sold are non-returnable and non-exchangeable", "1", "0", "20", "384" });
                lstString.Add(new[] { " ", "1", "0", "25", "384" });
                lstString.Add(new[] { " ", "1", "0", "25", "384" });

                Console.WriteLine($"strEInvoiceRequestLink : {data.EInvoiceQrUrl}");

                return await ProcessIminJob(printer, lstString);
            }
            catch (Exception ex)
            {
                return (false, $"iMin Print Error: {ex.Message}");
            }
#else
            return (false, "iMin only supported on Android.");
#endif
        }

#if ANDROID
        private async Task<(bool Success, string Error)> ProcessIminJob(PrinterHelper printer, List<string[]> jobLines)
        {
            for (int i = 0; i < jobLines.Count; i++)
            {
                var line = jobLines[i];

                try
                {
                    // === QR CODE HANDLING ===
                    if (line.Length == 1 && line[0] == "QRCODE")
                    {
                        if (i + 1 < jobLines.Count)
                        {
                            string qrData = jobLines[i + 1][0];
                            Console.WriteLine("[IminPrinter] ========================================");
                            Console.WriteLine($"[IminPrinter] QR Data Length: {qrData.Length}");
                            Console.WriteLine($"[IminPrinter] Printing QR Code: {qrData.Substring(0, Math.Min(50, qrData.Length))}...");
                            Console.WriteLine("[IminPrinter] ========================================");

                            try
                            {
                                // --- Now print actual QR with same settings ---
                                Console.WriteLine("[IminPrinter] QR module initialized, printing actual data...");

                                // Keep same settings (already configured)
                                printer.SetQrCodeSize(5);
                                printer.SetQrCodeErrorCorrectionLev(1);
                                await Task.Delay(300);

                                printer.PrintQrCodeWithAlign(qrData.Trim(), 1, null);

                                // Wait longer for complex QR
                                int delayMs = qrData.Length > 400 ? 3000 : 2000;
                                await Task.Delay(delayMs);

                                // Check status after QR print
                                var iminStatusQR = IminPrinterInitializer.CheckPrinterHardwareStatus();
                                if (iminStatusQR != 0)
                                {
                                    Console.WriteLine("[IminPrinter] Printer hardware issue detected (possibly out of paper)");
                                    return (false, "Printer hardware issue detected (possibly out of paper)");
                                }

                                Console.WriteLine("[IminPrinter] ✓ QR Code printed successfully");
                            }
                            catch (Exception ex)
                            {
                                Console.WriteLine($"[IminPrinter] ✗ QR print error: {ex.Message}");

                                // --- Fallback ---
                                try
                                {
                                    Console.WriteLine("[IminPrinter] Trying fallback...");

                                    // Re-initialize
                                    printer.PrintText(" ", null);
                                    printer.PrintAndLineFeed();
                                    await Task.Delay(500);

                                    printer.SetQrCodeSize(6);
                                    printer.SetQrCodeErrorCorrectionLev(1);
                                    await Task.Delay(500);

                                    printer.PrintQrCode(qrData.Trim(), null);
                                    await Task.Delay(2500);

                                    printer.PrintAndLineFeed();
                                    printer.PrintAndLineFeed();
                                    Console.WriteLine("[IminPrinter] ✓ QR Code printed (fallback mode)");
                                }
                                catch (Exception ex2)
                                {
                                    Console.WriteLine($"[IminPrinter] ✗ Fallback also failed: {ex2.Message}");
                                    printer.PrintText("QR Code Error", null);
                                    printer.PrintAndLineFeed();
                                    printer.PrintText("Please request e-invoice manually", null);
                                    printer.PrintAndLineFeed();
                                }
                            }

                            i++; // skip next line (QR data)
                        }
                        continue;
                    }

                    // === IMAGE HANDLING (.png) ===
                    if (line[0].EndsWith(".png", StringComparison.OrdinalIgnoreCase)
                        || line[0].EndsWith(".jpeg", StringComparison.OrdinalIgnoreCase)
                        || line[0].EndsWith(".jpg", StringComparison.OrdinalIgnoreCase))
                    {
                        Bitmap? bitmap = null;
                        string imageName = line[0];

                        try
                        {
                            // Remote image (starts with http/https)
                            if (imageName.StartsWith("http", StringComparison.OrdinalIgnoreCase))
                            {
                                using var httpClient = new HttpClient();
                                var imageBytes = await httpClient.GetByteArrayAsync(imageName);
                                bitmap = BitmapFactory.DecodeByteArray(imageBytes, 0, imageBytes.Length);
                            }
                            else
                            {
                                // Local file in app data
                                string imagePath = System.IO.Path.Combine(FileSystem.AppDataDirectory, imageName);
                                if (File.Exists(imagePath))
                                {
                                    bitmap = BitmapFactory.DecodeFile(imagePath);
                                }

                                //  Fallback to embedded resource
                                if (bitmap == null)
                                {
                                    var assembly = System.Reflection.Assembly.GetExecutingAssembly();
                                    var resourceName = assembly.GetManifestResourceNames()
                                        .FirstOrDefault(r => r.EndsWith(imageName, StringComparison.OrdinalIgnoreCase));

                                    if (resourceName != null)
                                    {
                                        using var stream = assembly.GetManifestResourceStream(resourceName);
                                        if (stream != null)
                                            bitmap = BitmapFactory.DecodeStream(stream);
                                    }
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            System.Diagnostics.Debug.WriteLine($"[ImageLoader] Failed to load image '{imageName}': {ex.Message}");
                        }

                        if (bitmap != null)
                        {
                            // Do something with the bitmap in future
                        }
                        else
                        {
                            System.Diagnostics.Debug.WriteLine($"[ImageLoader] Could not load image: {imageName}");
                        }
                    }


                    // === SINGLE-LINE TEXT ===
                    if (!line[0].Contains("|"))
                    {
                        var text = line[0];
                        var align = int.Parse(line[1]);
                        var bold = line[2] == "1";
                        var fontSize = int.Parse(line[3]);

                        if (bold) fontSize += 2;

                        printer.PrintColumnsString(
                            new[] { text },
                            new[] { 384 },
                            new[] { align },
                            new[] { fontSize },
                            null
                        );
                        continue;
                    }

                    // === MULTI-COLUMN TEXT ===
                    string[] columns = line[0].Split('|');
                    string[] colRatios = line[1].Split(',');
                    string[] colAligns = line[2].Split(',');
                    string[] colFontSizes = line[3].Split(',');

                    int totalWidth = int.Parse(line[4]);
                    int totalRatio = colRatios.Sum(r => int.Parse(r));

                    int[] colWidths = new int[columns.Length];
                    int[] alignTypes = new int[colAligns.Length];
                    int[] fontSizes = new int[colFontSizes.Length];

                    for (int k = 0; k < columns.Length; k++)
                    {
                        var ratio = int.Parse(colRatios[k]);
                        colWidths[k] = (int)(totalWidth * (ratio / (float)totalRatio));
                        alignTypes[k] = int.Parse(colAligns[k]);
                        fontSizes[k] = int.Parse(colFontSizes[k]);
                    }

                    printer.PrintColumnsString(columns, colWidths, alignTypes, fontSizes, null);

                    // Periodically check status during long prints
                    if (i % 10 == 0) // Check every 10 lines
                    {
                        var iminStatusLast = IminPrinterInitializer.CheckPrinterHardwareStatus();
                        if (iminStatusLast != 0)
                        {
                            Console.WriteLine($"[IminPrinter] Hardware error at line {i}");
                            return (false, "Printer hardware issue detected (possibly out of paper)");
                        }
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[IminPrinter] Line error: {ex.Message}");
                }
            }
            printer.PrintAndLineFeed();
            printer.PartialCut();
            return (true, "");
        }
#endif
    }
}