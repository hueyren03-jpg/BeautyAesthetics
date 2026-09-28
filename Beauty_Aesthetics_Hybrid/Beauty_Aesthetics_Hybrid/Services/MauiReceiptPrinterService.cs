using Beauty_Aesthetics_WebPos.Components.Services.Printing;
using Microsoft.Maui.ApplicationModel;
using System.Net.Sockets;
using System.Text;

#if ANDROID
using Android;
using Android.Bluetooth;
#endif

namespace Beauty_Aesthetics_Hybrid.Services;

public sealed class MauiReceiptPrinterService : IReceiptPrinterService
{
    private static readonly string[] PrinterKeywords =
    {
        "printer", "print", "pos", "thermal", "receipt",
        "mpt", "rpp", "zj", "goojprt", "xprinter", "epson",
        "bixolon", "star", "citizen", "zebra", "tsc", "imin"
    };

    private readonly IminPrinterHelperService iminService = new();

    public async Task<(bool Success, string Error)> PrintAsync(
        PrinterOption printer,
        ReceiptData data)
    {
        try
        {
            if (printer.IsIminPrinter)
            {
                return await iminService.PrintReceiptAsync(data);
            }

            var receipt = BuildEscPosReceipt(data, printer.ReceiptMM == 80 ? 48 : 32);

            if (printer.IsWifiPrinter)
            {
                return await PrintNetworkAsync(printer.IpAddress, receipt);
            }

            if (printer.IsBluetoothPrinter || printer.IsIminPrinter)
            {
                return await PrintBluetoothAsync(receipt);
            }

            return (false, "No printer type selected.");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[MauiReceiptPrinterService] PrintAsync error: {ex.Message}");
            return (false, ex.Message);
        }
    }

    private static async Task<(bool Success, string Error)> PrintNetworkAsync(
        string ipAddress,
        byte[] data)
    {
        if (string.IsNullOrWhiteSpace(ipAddress))
        {
            return (false, "Network printer IP address is not configured.");
        }

        try
        {
            using var client = new TcpClient();
            var connectTask = client.ConnectAsync(ipAddress.Trim(), 9100);

            if (await Task.WhenAny(connectTask, Task.Delay(5000)) != connectTask)
            {
                return (false, $"Connection to {ipAddress}:9100 timed out.");
            }

            await connectTask;
            using var stream = client.GetStream();
            stream.WriteTimeout = 10000;
            await stream.WriteAsync(data);
            await stream.FlushAsync();
            await Task.Delay(1500);

            return (true, string.Empty);
        }
        catch (Exception ex)
        {
            return (false, $"Network print error: {ex.Message}");
        }
    }

    private static async Task<(bool Success, string Error)> PrintBluetoothAsync(byte[] data)
    {
#if ANDROID
        try
        {
            if (OperatingSystem.IsAndroidVersionAtLeast(31))
            {
                var permission = await Permissions.RequestAsync<BluetoothConnectPermission>();
                if (permission != PermissionStatus.Granted)
                {
                    return (false, "Bluetooth permission is required to print receipts.");
                }
            }

#pragma warning disable CA1422
            var adapter = BluetoothAdapter.DefaultAdapter;
#pragma warning restore CA1422

            if (adapter is null)
            {
                return (false, "Bluetooth not available on this device.");
            }

            if (!adapter.IsEnabled)
            {
                return (false, "Please turn on Bluetooth and try again.");
            }

            var device = FindPrinterDevice(adapter);
            if (device is null)
            {
                return (false, "No paired Bluetooth printer found. Please pair your printer in Android Bluetooth settings.");
            }

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
                return (true, string.Empty);
            }
            finally
            {
                try
                {
                    if (socket.IsConnected)
                    {
                        socket.Close();
                    }

                    socket.Dispose();
                }
                catch
                {
                }
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
        if (devices is null || devices.Count == 0)
        {
            return null;
        }

        var byName = devices.FirstOrDefault(device =>
            device.Name is not null &&
            PrinterKeywords.Any(keyword =>
                device.Name.Contains(keyword, StringComparison.OrdinalIgnoreCase)));

        if (byName is not null)
        {
            return byName;
        }

        var spp = Java.Util.UUID.FromString("00001101-0000-1000-8000-00805F9B34FB");

        foreach (var device in devices)
        {
            try
            {
                var name = device.Name?.ToLowerInvariant() ?? string.Empty;
                if (name.Contains("phone") ||
                    name.Contains("headset") ||
                    name.Contains("speaker") ||
                    name.Contains("watch") ||
                    name.Contains("buds") ||
                    name.Contains("headphone"))
                {
                    continue;
                }

                var uuids = device.GetUuids();
                if (uuids is not null &&
                    uuids.Any(item => item.Uuid?.Equals(spp) == true))
                {
                    return device;
                }
            }
            catch
            {
            }
        }

        return null;
    }

    private sealed class BluetoothConnectPermission : Permissions.BasePlatformPermission
    {
        public override (string androidPermission, bool isRuntime)[] RequiredPermissions =>
            OperatingSystem.IsAndroidVersionAtLeast(31)
                ? new[] { (Manifest.Permission.BluetoothConnect, true) }
                : Array.Empty<(string androidPermission, bool isRuntime)>();
    }
#endif

    private static byte[] BuildEscPosReceipt(ReceiptData data, int width)
    {
        Encoding encoding;
        try
        {
            encoding = Encoding.GetEncoding("GBK");
        }
        catch
        {
            encoding = Encoding.UTF8;
        }

        var buffer = new List<byte>();
        var saleDate = data.DateTimeOfSale ?? DateTime.Now;

        buffer.AddRange(new byte[] { 0x1B, 0x40 });

        if (!string.IsNullOrWhiteSpace(data.CompanyName))
        {
            buffer.AddRange(Center());
            buffer.AddRange(Bold(true));
            WriteLine(buffer, encoding, Truncate(data.CompanyName, width));
            buffer.AddRange(Bold(false));
        }

        foreach (var addressLine in new[] { data.Address1, data.Address2, data.Address3 })
        {
            if (!string.IsNullOrWhiteSpace(addressLine))
            {
                buffer.AddRange(Center());
                WriteLine(buffer, encoding, Truncate(addressLine, width));
            }
        }

        if (!string.IsNullOrWhiteSpace(data.Phone))
        {
            buffer.AddRange(Center());
            WriteLine(buffer, encoding, $"Tel: {data.Phone}");
        }

        if (!string.IsNullOrWhiteSpace(data.Email))
        {
            buffer.AddRange(Center());
            WriteLine(buffer, encoding, $"Email: {data.Email}");
        }

        if (!string.IsNullOrWhiteSpace(data.CoRegistrationNo))
        {
            buffer.AddRange(Center());
            WriteLine(buffer, encoding, $"Co No: {data.CoRegistrationNo}");
        }

        if (!string.IsNullOrWhiteSpace(data.TIN))
        {
            buffer.AddRange(Center());
            WriteLine(buffer, encoding, $"Tax No: {data.TIN}");
        }

        WriteRule(buffer, encoding, width);
        buffer.AddRange(Center());
        buffer.AddRange(Bold(true));
        WriteLine(buffer, encoding, "Invoice");
        buffer.AddRange(Bold(false));

        buffer.AddRange(Left());
        WriteLine(buffer, encoding, $"Date: {saleDate:dd/MM/yyyy HH:mm}");
        WriteLine(buffer, encoding, $"Doc No: {data.ReceiptNo}");
        WriteLine(buffer, encoding, $"Ref No: {data.ReferenceNumber}");
        WriteRule(buffer, encoding, width);

        if (!string.IsNullOrWhiteSpace(data.CustomerID))
        {
            if (!string.IsNullOrWhiteSpace(data.CustomerName))
            {
                PrintLeft(buffer, encoding, $"  {data.CustomerName}", width);
            }

            foreach (var customerLine in new[]
            {
                data.CustomerAddress1,
                data.CustomerAddress2,
                data.CustomerAddress3,
                data.CustomerCountry,
                data.CustomerPhone
            })
            {
                if (!string.IsNullOrWhiteSpace(customerLine))
                {
                    PrintLeft(buffer, encoding, $"  {customerLine}", width);
                }
            }

            WriteRule(buffer, encoding, width);
        }

        var qtyWidth = 3;
        var priceWidth = 8;
        var totalWidth = 10;
        var nameWidth = width - qtyWidth - priceWidth - totalWidth - 3;

        var header =
            PadRightDisplay("Item", nameWidth) + " " +
            PadLeftDisplay("Qty", qtyWidth) + " " +
            PadLeftDisplay("Price", priceWidth) + " " +
            PadLeftDisplay("Total", totalWidth);

        WriteLine(buffer, encoding, header);
        WriteRule(buffer, encoding, width);

        decimal itemCount = 0;
        decimal totalDiscount = 0;

        foreach (var item in data.Items)
        {
            itemCount += item.Quantity;
            totalDiscount += item.Discount;

            foreach (var namePart in WrapDisplay(item.Name, width))
            {
                WriteLine(buffer, encoding, namePart);
            }

            if (!string.IsNullOrWhiteSpace(item.Remarks))
            {
                foreach (var remarkPart in WrapDisplay(item.Remarks, Math.Max(1, width - 2)))
                {
                    WriteLine(buffer, encoding, $"  {remarkPart}");
                }
            }

            var unitPrice = item.LineTotal / Math.Max(1m, item.Quantity);
            var metrics =
                new string(' ', nameWidth) + " " +
                PadLeftDisplay(item.Quantity.ToString("0.##"), qtyWidth) + " " +
                PadLeftDisplay(unitPrice.ToString("F2"), priceWidth) + " " +
                PadLeftDisplay(item.LineTotal.ToString("F2"), totalWidth);

            WriteLine(buffer, encoding, metrics);

            if (item.Discount > 0)
            {
                WriteLine(
                    buffer,
                    encoding,
                    PadLeftDisplay($"(Disc: {item.Discount:F2})", width));
            }

            buffer.Add(0x0A);
        }

        WriteRule(buffer, encoding, width);

        var labelWidth = width - totalWidth;
        var serviceChargeText = data.TotalBeforeTax_ServiceCharge <= 0
            ? "-"
            : data.TotalBeforeTax_ServiceCharge.ToString("0.00");

        WriteLine(buffer, encoding, PadRightDisplay("Item Count:", labelWidth) + PadLeftDisplay(itemCount.ToString("0.##"), totalWidth));
        WriteLine(buffer, encoding, PadRightDisplay("Total Discount:", labelWidth) + PadLeftDisplay(totalDiscount.ToString("F2"), totalWidth));
        WriteLine(buffer, encoding, PadRightDisplay("Subtotal:", labelWidth) + PadLeftDisplay(data.Subtotal.ToString("F2"), totalWidth));
        WriteLine(buffer, encoding, PadRightDisplay("Service Charge:", labelWidth) + PadLeftDisplay(serviceChargeText, totalWidth));
        WriteLine(buffer, encoding, PadRightDisplay("Gov Tax:", labelWidth) + PadLeftDisplay(data.TaxAmount.ToString("F2"), totalWidth));
        WriteLine(buffer, encoding, PadRightDisplay("Rounding Adj:", labelWidth) + PadLeftDisplay(data.RoundingAmount.ToString("F2"), totalWidth));

        WriteRule(buffer, encoding, width);
        buffer.AddRange(Bold(true));
        WriteLine(buffer, encoding, PadRightDisplay("GRAND TOTAL:", labelWidth) + PadLeftDisplay(data.GrandTotal.ToString("F2"), totalWidth));
        buffer.AddRange(Bold(false));
        WriteRule(buffer, encoding, width);

        WriteLine(buffer, encoding, PadRightDisplay("Cashier:", labelWidth) + PadLeftDisplay(data.CashierName, totalWidth));

        foreach (var payment in data.Payments)
        {
            WriteLine(
                buffer,
                encoding,
                PadRightDisplay(payment.Method + ":", width - 12) +
                PadLeftDisplay(payment.Amount.ToString("F2"), 12));
        }

        if (data.ChangeAmount > 0)
        {
            WriteLine(
                buffer,
                encoding,
                PadRightDisplay("Change:", width - 12) +
                PadLeftDisplay($"({data.ChangeAmount:F2})", 12));
        }

        WriteRule(buffer, encoding, width);

        if (data.TaxSummary.Count > 0)
        {
            var taxColumnWidth = width == 48 ? 14 : 12;
            var valueColumnWidth = (width - taxColumnWidth) / 2;

            WriteLine(
                buffer,
                encoding,
                PadRightDisplay("Tax Summary", taxColumnWidth) +
                PadLeftDisplay("Amount", valueColumnWidth) +
                PadLeftDisplay("Tax", valueColumnWidth));

            foreach (var tax in data.TaxSummary)
            {
                WriteLine(
                    buffer,
                    encoding,
                    PadRightDisplay(tax.TaxCode, taxColumnWidth) +
                    PadLeftDisplay(tax.Amount.ToString("F2"), valueColumnWidth) +
                    PadLeftDisplay(tax.Tax.ToString("F2"), valueColumnWidth));
            }

            WriteRule(buffer, encoding, width);
        }

        if (!string.IsNullOrWhiteSpace(data.EInvoiceQrUrl))
        {
            var lastDayOfMonth = new DateTime(saleDate.Year, saleDate.Month, 1)
                .AddMonths(1)
                .AddDays(-1);

            buffer.AddRange(Center());
            buffer.AddRange(Bold(true));
            WriteLine(buffer, encoding, "eInvoice Request QR");
            buffer.AddRange(Bold(false));
            WriteLine(buffer, encoding, "Request must be made by");
            WriteLine(buffer, encoding, lastDayOfMonth.ToString("dd/MM/yyyy"));
            buffer.Add(0x0A);
            WriteLine(buffer, encoding, "SCAN QR CODE");
            buffer.Add(0x0A);

            AddQrCode(buffer, data.EInvoiceQrUrl);
            buffer.Add(0x0A);
        }

        buffer.AddRange(Center());
        WriteLine(buffer, encoding, "Thank You And See You Soon");
        WriteLine(buffer, encoding, "Goods sold are non-returnable");
        WriteLine(buffer, encoding, "and non-exchangeable");

        buffer.AddRange(new byte[] { 0x0A, 0x0A, 0x0A, 0x0A, 0x0A, 0x0A });
        buffer.AddRange(new byte[] { 0x1D, 0x56, 0x01 });

        return buffer.ToArray();
    }

    private static void AddQrCode(List<byte> buffer, string value)
    {
        var qrData = Encoding.UTF8.GetBytes(value);
        buffer.AddRange(new byte[] { 0x1D, 0x28, 0x6B, 0x04, 0x00, 0x31, 0x41, 0x32, 0x00 });
        buffer.AddRange(new byte[] { 0x1D, 0x28, 0x6B, 0x03, 0x00, 0x31, 0x43, 0x06 });
        buffer.AddRange(new byte[] { 0x1D, 0x28, 0x6B, 0x03, 0x00, 0x31, 0x45, 0x30 });

        var length = qrData.Length + 3;
        buffer.AddRange(new byte[]
        {
            0x1D, 0x28, 0x6B,
            (byte)(length % 256),
            (byte)(length / 256),
            0x31, 0x50, 0x30
        });
        buffer.AddRange(qrData);
        buffer.AddRange(new byte[] { 0x1D, 0x28, 0x6B, 0x03, 0x00, 0x31, 0x51, 0x30 });
    }

    private static byte[] Center() => new byte[] { 0x1B, 0x61, 0x01 };
    private static byte[] Left() => new byte[] { 0x1B, 0x61, 0x00 };
    private static byte[] Bold(bool on) => new byte[] { 0x1B, 0x45, (byte)(on ? 1 : 0) };

    private static void WriteLine(List<byte> buffer, Encoding encoding, string text)
    {
        buffer.AddRange(encoding.GetBytes(text));
        buffer.Add(0x0A);
    }

    private static void WriteRule(List<byte> buffer, Encoding encoding, int width) =>
        WriteLine(buffer, encoding, new string('-', width));

    private static string Truncate(string text, int width) =>
        text.Length <= width ? text : text[..Math.Max(1, width - 1)] + "~";

    private static bool IsWideChar(char c) =>
        (c >= 0x4E00 && c <= 0x9FFF) ||
        (c >= 0x3400 && c <= 0x4DBF);

    private static int GetDisplayWidth(string text)
    {
        var width = 0;
        foreach (var c in text)
        {
            width += IsWideChar(c) ? 2 : 1;
        }

        return width;
    }

    private static string PadRightDisplay(string text, int totalWidth)
    {
        var pad = totalWidth - GetDisplayWidth(text);
        return text + new string(' ', Math.Max(0, pad));
    }

    private static string PadLeftDisplay(string text, int totalWidth)
    {
        var pad = totalWidth - GetDisplayWidth(text);
        return new string(' ', Math.Max(0, pad)) + text;
    }

    private static List<string> WrapDisplay(string text, int maxWidth)
    {
        var lines = new List<string>();
        var current = new StringBuilder();
        var width = 0;

        foreach (var c in text ?? string.Empty)
        {
            var charWidth = IsWideChar(c) ? 2 : 1;
            if (width + charWidth > maxWidth)
            {
                lines.Add(current.ToString());
                current.Clear();
                width = 0;
            }

            current.Append(c);
            width += charWidth;
        }

        if (current.Length > 0)
        {
            lines.Add(current.ToString());
        }

        return lines;
    }

    private static void PrintLeft(
        List<byte> buffer,
        Encoding encoding,
        string text,
        int width)
    {
        foreach (var line in WrapDisplay(text, width))
        {
            WriteLine(buffer, encoding, line);
        }
    }
}
