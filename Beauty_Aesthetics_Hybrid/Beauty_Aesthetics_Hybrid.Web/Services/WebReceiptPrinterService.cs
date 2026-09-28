using Beauty_Aesthetics_WebPos.Components.Services.Printing;
using Microsoft.JSInterop;
using System.Net;
using System.Text;

namespace Beauty_Aesthetics_Hybrid.Web.Services;

public sealed class WebReceiptPrinterService(IJSRuntime jsRuntime) : IReceiptPrinterService
{
    public async Task<(bool Success, string Error)> PrintAsync(
        PrinterOption printer,
        ReceiptData data,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var html = BuildReceiptHtml(data, printer.ReceiptMM == 58 ? 58 : 80);
            var opened = await jsRuntime.InvokeAsync<bool>(
                "printReceiptHtml",
                cancellationToken,
                html);

            return opened
                ? (true, string.Empty)
                : (false, "The browser could not open the system print dialog.");
        }
        catch (JSException ex)
        {
            return (false, $"Browser print error: {ex.Message}");
        }
        catch (Exception ex)
        {
            return (false, ex.Message);
        }
    }

    private static string BuildReceiptHtml(ReceiptData data, int widthMm)
    {
        static string E(string? value) => WebUtility.HtmlEncode(value ?? string.Empty);

        var sb = new StringBuilder();
        sb.Append($"""
<!doctype html>
<html>
<head>
<meta charset="utf-8">
<title>Receipt {E(data.ReceiptNo)}</title>
<style>
@page {{ size: {widthMm}mm auto; margin: 4mm; }}
* {{ box-sizing: border-box; }}
body {{
  margin: 0;
  color: #111;
  font-family: Arial, Helvetica, sans-serif;
  font-size: 11px;
  line-height: 1.35;
}}
.receipt {{ width: 100%; }}
.center {{ text-align: center; }}
.bold {{ font-weight: 700; }}
.rule {{ border-top: 1px dashed #111; margin: 8px 0; }}
.row {{ display: flex; justify-content: space-between; gap: 8px; }}
.item {{ margin: 7px 0; }}
.item-name {{ font-weight: 600; overflow-wrap: anywhere; }}
.item-meta {{ display: flex; justify-content: space-between; gap: 8px; }}
.payment {{ display: flex; justify-content: space-between; gap: 8px; }}
.total {{ font-size: 13px; font-weight: 700; }}
.muted {{ color: #444; }}
@media print {{
  body {{ width: {widthMm}mm; }}
}}
</style>
</head>
<body>
<div class="receipt">
""");

        if (!string.IsNullOrWhiteSpace(data.CompanyName))
            sb.Append($"<div class='center bold'>{E(data.CompanyName)}</div>");
        if (!string.IsNullOrWhiteSpace(data.BranchName))
            sb.Append($"<div class='center'>{E(data.BranchName)}</div>");

        sb.Append("<div class='rule'></div>");
        sb.Append($"<div>Receipt: {E(data.ReceiptNo)}</div>");
        sb.Append($"<div>Date: {data.DateTimeOfSale:dd/MM/yyyy HH:mm}</div>");

        if (!string.IsNullOrWhiteSpace(data.CustomerName))
            sb.Append($"<div>Customer: {E(data.CustomerName)}</div>");
        if (!string.IsNullOrWhiteSpace(data.CustomerPhone))
            sb.Append($"<div>Phone: {E(data.CustomerPhone)}</div>");

        sb.Append("<div class='rule'></div>");

        foreach (var item in data.Items)
        {
            sb.Append("<div class='item'>");
            sb.Append($"<div class='item-name'>{E(item.Name)}</div>");
            sb.Append($"<div class='item-meta'><span>{item.Quantity:0.##} × {item.UnitPrice:0.00}</span><span>{item.LineTotal:0.00}</span></div>");
            if (item.Discount > 0)
                sb.Append($"<div class='item-meta muted'><span>Discount</span><span>-{item.Discount:0.00}</span></div>");
            if (!string.IsNullOrWhiteSpace(item.Remarks))
                sb.Append($"<div class='muted'>{E(item.Remarks)}</div>");
            sb.Append("</div>");
        }

        sb.Append("<div class='rule'></div>");
        sb.Append($"<div class='row'><span>Subtotal</span><span>{data.Subtotal:0.00}</span></div>");
        if (data.Discount > 0)
            sb.Append($"<div class='row'><span>Discount</span><span>-{data.Discount:0.00}</span></div>");
        sb.Append($"<div class='row'><span>Tax</span><span>{data.TaxAmount:0.00}</span></div>");
        sb.Append($"<div class='row total'><span>TOTAL</span><span>{data.GrandTotal:0.00}</span></div>");

        sb.Append("<div class='rule'></div>");
        if (!string.IsNullOrWhiteSpace(data.CashierName))
            sb.Append($"<div>Cashier: {E(data.CashierName)}</div>");

        foreach (var payment in data.Payments)
            sb.Append($"<div class='payment'><span>{E(payment.Method)}</span><span>{payment.Amount:0.00}</span></div>");

        if (data.ChangeAmount > 0)
            sb.Append($"<div class='payment'><span>Change</span><span>{data.ChangeAmount:0.00}</span></div>");

        sb.Append("<div class='rule'></div>");
        sb.Append("<div class='center'>Thank you</div>");
        sb.Append("</div></body></html>");

        return sb.ToString();
    }
}
