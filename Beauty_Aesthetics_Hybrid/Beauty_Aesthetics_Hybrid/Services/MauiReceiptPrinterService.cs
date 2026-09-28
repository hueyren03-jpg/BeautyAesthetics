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
    [
        "printer", "print", "pos", "thermal", "receipt",
        "mpt", "rpp", "zj", "goojprt", "xprinter", "epson",
        "bixolon", "star", "citizen", "zebra", "tsc"
    ];

    public async Task<(bool Success, string Error)> PrintAsync(
        PrinterOption printer,
        ReceiptData data,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var width = printer.ReceiptMM == 80 ? 48 : 32;
            var receiptBytes = BuildEscPosReceipt(data, width);

            if (printer.IsNetworkPrinter)
            {
                return await PrintNetworkAsync(
                    printer.IpAddress,
                    receiptBytes,
                    cancellationToken);
            }

            if (printer.IsBluetoothPrinter)
            {
                return await PrintBluetoothAsync(
                    receiptBytes,
                    cancellationToken);
            }

            return (false, "No supported printer type is selected.");
        }
        catch (Exception ex)
        {
            return (false, ex.Message);
        }
    }

    private static async Task<(bool Success, string Error)> PrintNetworkAsync(
        string ipAddress,
        byte[] data,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(ipAddress))
        {
            return (false, "Network printer IP address is not configured.");
        }

        try
        {
            using var client = new TcpClient();
            var connectTask = client.ConnectAsync(ipAddress.Trim(), 9100);
            var timeoutTask = Task.Delay(TimeSpan.FromSeconds(5), cancellationToken);

            if (await Task.WhenAny(connectTask, timeoutTask) != connectTask)
            {
                return (false, $"Connection to {ipAddress}:9100 timed out.");
            }

            await connectTask;
            await using var stream = client.GetStream();
            await stream.WriteAsync(data, cancellationToken);
            await stream.FlushAsync(cancellationToken);
            return (true, string.Empty);
        }
        catch (OperationCanceledException)
        {
            return (false, "Network printing was cancelled.");
        }
        catch (Exception ex)
        {
            return (false, $"Network print error: {ex.Message}");
        }
    }

    private static async Task<(bool Success, string Error)> PrintBluetoothAsync(
        byte[] data,
        CancellationToken cancellationToken)
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
                return (false, "Bluetooth is not available on this device.");
            }

            if (!adapter.IsEnabled)
            {
                return (false, "Please turn on Bluetooth and try again.");
            }

            var device = FindPrinterDevice(adapter);
            if (device is null)
            {
                return (false, "No paired Bluetooth receipt printer was found. Pair the printer in Android Bluetooth settings first.");
            }

            var uuid = Java.Util.UUID.FromString("00001101-0000-1000-8000-00805F9B34FB");
            using var socket = device.CreateInsecureRfcommSocketToServiceRecord(uuid);
            if (socket is null)
            {
                return (false, "Unable to create a Bluetooth printer connection.");
            }

            await socket.ConnectAsync();
            var output = socket.OutputStream;
            if (output is null)
            {
                return (false, "Unable to open the Bluetooth printer output stream.");
            }

            await output.WriteAsync(data);
            await output.FlushAsync();
            output.Close();

            return (true, string.Empty);
        }
        catch (Java.IO.IOException ex)
        {
            return (false, $"Bluetooth connection failed: {ex.Message}");
        }
        catch (Exception ex)
        {
            return (false, $"Bluetooth print error: {ex.Message}");
        }
#else
        await Task.CompletedTask;
        return (false, "Bluetooth receipt printing is only supported on Android.");
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
            !string.IsNullOrWhiteSpace(device.Name) &&
            PrinterKeywords.Any(keyword =>
                device.Name!.Contains(keyword, StringComparison.OrdinalIgnoreCase)));

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
                if (uuids is not null && uuids.Any(item => item.Uuid?.Equals(spp) == true))
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
                ? [(Manifest.Permission.BluetoothConnect, true)]
                : [];
    }
#endif

    private static byte[] BuildEscPosReceipt(ReceiptData data, int width)
    {
        var encoding = Encoding.UTF8;
        var bytes = new List<byte>();

        bytes.AddRange([0x1B, 0x40]);
        bytes.AddRange(Center());

        if (!string.IsNullOrWhiteSpace(data.CompanyName))
        {
            bytes.AddRange(Bold(true));
            WriteLine(bytes, encoding, Truncate(data.CompanyName, width));
            bytes.AddRange(Bold(false));
        }

        if (!string.IsNullOrWhiteSpace(data.BranchName))
        {
            WriteLine(bytes, encoding, Truncate(data.BranchName, width));
        }

        WriteLine(bytes, encoding, new string('-', width));
        bytes.AddRange(Left());
        WriteLine(bytes, encoding, $"Receipt: {data.ReceiptNo}");
        WriteLine(bytes, encoding, $"Date: {data.DateTimeOfSale:dd/MM/yyyy HH:mm}");

        if (!string.IsNullOrWhiteSpace(data.CustomerName))
        {
            WriteLine(bytes, encoding, $"Customer: {data.CustomerName}");
        }

        if (!string.IsNullOrWhiteSpace(data.CustomerPhone))
        {
            WriteLine(bytes, encoding, $"Phone: {data.CustomerPhone}");
        }

        WriteLine(bytes, encoding, new string('-', width));

        foreach (var item in data.Items)
        {
            foreach (var part in Wrap(item.Name, width))
            {
                WriteLine(bytes, encoding, part);
            }

            var qty = item.Quantity.ToString("0.##");
            var amount = item.LineTotal.ToString("0.00");
            WriteLine(bytes, encoding, AlignPair($"{qty} x {item.UnitPrice:0.00}", amount, width));

            if (item.Discount > 0)
            {
                WriteLine(bytes, encoding, AlignPair("Discount", $"-{item.Discount:0.00}", width));
            }

            if (!string.IsNullOrWhiteSpace(item.Remarks))
            {
                foreach (var part in Wrap($"  {item.Remarks}", width))
                {
                    WriteLine(bytes, encoding, part);
                }
            }
        }

        WriteLine(bytes, encoding, new string('-', width));
        WriteLine(bytes, encoding, AlignPair("Subtotal", data.Subtotal.ToString("0.00"), width));
        if (data.Discount > 0)
        {
            WriteLine(bytes, encoding, AlignPair("Discount", $"-{data.Discount:0.00}", width));
        }
        WriteLine(bytes, encoding, AlignPair("Tax", data.TaxAmount.ToString("0.00"), width));

        bytes.AddRange(Bold(true));
        WriteLine(bytes, encoding, AlignPair("TOTAL", data.GrandTotal.ToString("0.00"), width));
        bytes.AddRange(Bold(false));

        WriteLine(bytes, encoding, new string('-', width));
        if (!string.IsNullOrWhiteSpace(data.CashierName))
        {
            WriteLine(bytes, encoding, $"Cashier: {data.CashierName}");
        }

        foreach (var payment in data.Payments)
        {
            WriteLine(bytes, encoding, AlignPair(payment.Method, payment.Amount.ToString("0.00"), width));
        }

        if (data.ChangeAmount > 0)
        {
            WriteLine(bytes, encoding, AlignPair("Change", data.ChangeAmount.ToString("0.00"), width));
        }

        if (!string.IsNullOrWhiteSpace(data.EInvoiceQrUrl))
        {
            WriteLine(bytes, encoding, new string('-', width));
            bytes.AddRange(Center());
            WriteLine(bytes, encoding, "e-Invoice");
            AddQrCode(bytes, data.EInvoiceQrUrl);
            bytes.AddRange(Left());
        }

        WriteLine(bytes, encoding, new string('-', width));
        bytes.AddRange(Center());
        WriteLine(bytes, encoding, "Thank you");
        bytes.AddRange([0x0A, 0x0A, 0x0A, 0x0A]);
        bytes.AddRange([0x1D, 0x56, 0x01]);

        return bytes.ToArray();
    }

    private static void AddQrCode(List<byte> bytes, string value)
    {
        var qrData = Encoding.UTF8.GetBytes(value);
        bytes.AddRange([0x1D, 0x28, 0x6B, 0x04, 0x00, 0x31, 0x41, 0x32, 0x00]);
        bytes.AddRange([0x1D, 0x28, 0x6B, 0x03, 0x00, 0x31, 0x43, 0x05]);
        bytes.AddRange([0x1D, 0x28, 0x6B, 0x03, 0x00, 0x31, 0x45, 0x30]);

        var length = qrData.Length + 3;
        bytes.AddRange([
            0x1D, 0x28, 0x6B,
            (byte)(length % 256),
            (byte)(length / 256),
            0x31, 0x50, 0x30
        ]);
        bytes.AddRange(qrData);
        bytes.AddRange([0x1D, 0x28, 0x6B, 0x03, 0x00, 0x31, 0x51, 0x30]);
        bytes.Add(0x0A);
    }

    private static byte[] Center() => [0x1B, 0x61, 0x01];
    private static byte[] Left() => [0x1B, 0x61, 0x00];
    private static byte[] Bold(bool enabled) => [0x1B, 0x45, (byte)(enabled ? 1 : 0)];

    private static void WriteLine(List<byte> bytes, Encoding encoding, string text)
    {
        bytes.AddRange(encoding.GetBytes(text));
        bytes.Add(0x0A);
    }

    private static string AlignPair(string left, string right, int width)
    {
        var spaces = Math.Max(1, width - left.Length - right.Length);
        return left + new string(' ', spaces) + right;
    }

    private static string Truncate(string value, int width) =>
        value.Length <= width ? value : value[..Math.Max(0, width - 1)] + "~";

    private static IEnumerable<string> Wrap(string value, int width)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            yield break;
        }

        var remaining = value.Trim();
        while (remaining.Length > width)
        {
            var take = remaining[..width];
            var split = take.LastIndexOf(' ');
            if (split <= 0)
            {
                split = width;
            }

            yield return remaining[..split].TrimEnd();
            remaining = remaining[split..].TrimStart();
        }

        if (remaining.Length > 0)
        {
            yield return remaining;
        }
    }
}
