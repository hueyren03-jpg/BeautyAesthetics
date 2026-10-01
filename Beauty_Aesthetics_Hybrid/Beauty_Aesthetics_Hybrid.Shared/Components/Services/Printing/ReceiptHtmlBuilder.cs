using System.Net;
using System.Text;

namespace Beauty_Aesthetics_WebPos.Components.Services.Printing;

public static class ReceiptHtmlBuilder
{
    public static string Build(ReceiptData data)
    {
        var currency = string.IsNullOrWhiteSpace(data.CurrencyName)
            ? "MYR"
            : data.CurrencyName.Trim();
        var companyName = string.IsNullOrWhiteSpace(data.CompanyName)
            ? ReceiptBranding.AppTitle
            : data.CompanyName.Trim();
        var saleDate = data.DateTimeOfSale ?? DateTime.Now;

        var itemRows = new StringBuilder();
        foreach (var item in data.Items)
        {
            itemRows.Append("""
                <tr>
            """);
            itemRows.Append($"<td><strong>{Encode(item.Name)}</strong>");
            if (!string.IsNullOrWhiteSpace(item.Remarks))
            {
                itemRows.Append($"<small>{Encode(item.Remarks)}</small>");
            }
            itemRows.Append("</td>");
            itemRows.Append($"<td class=\"number\">{item.Quantity:0.##}</td>");
            itemRows.Append($"<td class=\"number\">{FormatMoney(currency, item.Discount)}</td>");
            itemRows.Append($"<td class=\"number\">{FormatMoney(currency, item.LineTotal)}</td>");
            itemRows.Append("</tr>");
        }

        var paymentRows = new StringBuilder();
        foreach (var payment in data.Payments.Where(payment => payment.Amount > 0m))
        {
            paymentRows.Append(
                $"<div class=\"line\"><span>{Encode(payment.Method)}</span><strong>{FormatMoney(currency, payment.Amount)}</strong></div>");
        }

        var taxSummaryRows = new StringBuilder();
        foreach (var tax in data.TaxSummary)
        {
            taxSummaryRows.Append("<tr>");
            taxSummaryRows.Append($"<td>{Encode(tax.TaxCode)}</td>");
            taxSummaryRows.Append($"<td class=\"number\">{FormatMoney(currency, tax.Amount)}</td>");
            taxSummaryRows.Append($"<td class=\"number\">{FormatMoney(currency, tax.Tax)}</td>");
            taxSummaryRows.Append("</tr>");
        }

        var branchLine = string.IsNullOrWhiteSpace(data.BranchName)
            ? string.Empty
            : $"<div><span>Branch</span><strong>{Encode(data.BranchName)}</strong></div>";

        var referenceLine = string.IsNullOrWhiteSpace(data.ReferenceNumber)
            ? string.Empty
            : $"<div><span>Reference</span><strong>{Encode(data.ReferenceNumber)}</strong></div>";

        var customerLine = string.IsNullOrWhiteSpace(data.CustomerName)
            ? "-"
            : data.CustomerName;

        var addressLines = new[] { data.Address1, data.Address2, data.Address3 }
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => $"<div>{Encode(value)}</div>");

        var contactLines = new List<string>();
        if (!string.IsNullOrWhiteSpace(data.Phone))
        {
            contactLines.Add($"Tel: {Encode(data.Phone)}");
        }
        if (!string.IsNullOrWhiteSpace(data.Email))
        {
            contactLines.Add(Encode(data.Email));
        }

        return $$"""
            <!doctype html>
            <html>
            <head>
                <meta charset="utf-8">
                <title>Receipt {{Encode(data.ReceiptNo)}}</title>
                <style>
                    * { box-sizing: border-box; }
                    html, body { margin: 0; padding: 0; background: #fff; }
                    body { color: #172033; font-family: Arial, Helvetica, sans-serif; font-size: 12px; }
                    .receipt { width: 100%; max-width: 720px; margin: 0 auto; padding: 22px; background: #fff; }
                    .brand { text-align: center; padding-bottom: 16px; border-bottom: 2px solid #0759ad; }
                    .brand-mark {
                        display: block;
                        width: 112px;
                        height: 42px;
                        margin: 0 auto 9px;
                        object-fit: cover;
                        object-position: center;
                    }
                    .brand h1 { margin: 0; color: #064a94; font-size: 23px; line-height: 1.15; }
                    .brand p { margin: 4px 0 0; color: #64748b; }
                    .brand .label { margin-top: 9px; color: #475569; font-size: 10px; font-weight: 700; letter-spacing: .09em; }
                    .company-meta { margin-top: 8px; color: #64748b; font-size: 10px; line-height: 1.4; }
                    .meta { display: grid; grid-template-columns: 1fr 1fr; gap: 9px 16px; padding: 16px 0; border-bottom: 1px solid #dbe4ee; }
                    .meta span { display: block; color: #64748b; font-size: 9px; font-weight: 700; text-transform: uppercase; letter-spacing: .05em; }
                    .meta strong { display: block; margin-top: 2px; color: #172033; overflow-wrap: anywhere; }
                    table { width: 100%; margin-top: 14px; border-collapse: collapse; }
                    th { padding: 8px 4px; border-block: 1px solid #cbd5e1; color: #475569; font-size: 10px; text-align: left; text-transform: uppercase; }
                    td { padding: 9px 4px; border-bottom: 1px solid #e2e8f0; vertical-align: top; }
                    td small { display: block; margin-top: 2px; color: #64748b; }
                    .number { text-align: right; white-space: nowrap; }
                    .totals { margin: 14px 0 0 auto; width: min(100%, 340px); }
                    .line { display: flex; justify-content: space-between; gap: 14px; padding: 4px 0; }
                    .line.total { margin-top: 6px; padding-top: 10px; border-top: 2px solid #0759ad; color: #064a94; font-size: 15px; }
                    .payments { margin-top: 15px; padding: 12px; border-radius: 7px; background: #eff6ff; }
                    .payments .heading { margin-bottom: 5px; color: #64748b; font-size: 9px; font-weight: 700; letter-spacing: .07em; }
                    .tax-summary { margin-top: 15px; }
                    .tax-summary h2 { margin: 0 0 6px; color: #475569; font-size: 9px; letter-spacing: .07em; }
                    .tax-summary table { margin-top: 0; }
                    .change { color: #087552; }
                    footer { margin-top: 20px; color: #64748b; text-align: center; font-size: 10px; }
                    @media print {
                        body { padding: 0; }
                        .receipt { max-width: none; padding: 8mm; }
                    }
                </style>
            </head>
            <body>
                <main class="receipt">
                    <header class="brand">
                        <img class="brand-mark" src="{{Encode(ReceiptBranding.LogoUrl)}}" alt="EBI logo">
                        <h1>{{Encode(ReceiptBranding.AppTitle)}}</h1>
                        <p>{{Encode(ReceiptBranding.AppSubtitle)}}</p>
                        <div class="label">OFFICIAL RECEIPT</div>
                        <div class="company-meta">
                            {{string.Join(string.Empty, addressLines)}}
                            {{(contactLines.Count == 0 ? string.Empty : $"<div>{string.Join(" · ", contactLines)}</div>")}}
                        </div>
                    </header>

                    <section class="meta">
                        <div><span>Receipt No.</span><strong>{{Encode(data.ReceiptNo)}}</strong></div>
                        <div><span>Date</span><strong>{{saleDate:dd/MM/yyyy hh:mm tt}}</strong></div>
                        <div><span>Customer</span><strong>{{Encode(customerLine)}}</strong></div>
                        {{branchLine}}
                        {{referenceLine}}
                    </section>

                    <table>
                        <thead>
                            <tr>
                                <th>Item</th>
                                <th class="number">Qty</th>
                                <th class="number">Discount</th>
                                <th class="number">Total</th>
                            </tr>
                        </thead>
                        <tbody>{{itemRows}}</tbody>
                    </table>

                    <section class="totals">
                        <div class="line"><span>Subtotal</span><strong>{{FormatMoney(currency, data.Subtotal)}}</strong></div>
                        <div class="line"><span>Tax</span><strong>{{FormatMoney(currency, data.EffectiveTaxAmount)}}</strong></div>
                        {{(data.RoundingAmount != 0m ? $"<div class=\"line\"><span>Rounding</span><strong>{FormatMoney(currency, data.RoundingAmount)}</strong></div>" : string.Empty)}}
                        <div class="line total"><span>Total</span><strong>{{FormatMoney(currency, data.GrandTotal)}}</strong></div>
                    </section>

                    {{(data.TaxSummary.Count == 0 ? string.Empty : $"""
                    <section class="tax-summary">
                        <h2>TAX SUMMARY</h2>
                        <table>
                            <thead><tr><th>Tax Code</th><th class="number">Taxable Amount</th><th class="number">Tax</th></tr></thead>
                            <tbody>{taxSummaryRows}</tbody>
                        </table>
                    </section>
                    """)}}

                    <section class="payments">
                        <div class="heading">PAYMENT BREAKDOWN</div>
                        {{paymentRows}}
                        <div class="line"><span>Received</span><strong>{{FormatMoney(currency, data.PaidAmount)}}</strong></div>
                        <div class="line change"><span>Change</span><strong>{{FormatMoney(currency, data.ChangeAmount)}}</strong></div>
                    </section>

                    <footer>Thank you for visiting {{Encode(ReceiptBranding.AppTitle)}}.</footer>
                </main>
            </body>
            </html>
            """;
    }

    private static string FormatMoney(string currency, decimal amount) =>
        $"{Encode(currency)} {amount:N2}";

    private static string Encode(string? value) =>
        WebUtility.HtmlEncode(string.IsNullOrWhiteSpace(value) ? "-" : value);
}
