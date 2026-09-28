using Microsoft.JSInterop;
using System.Text;

namespace Beauty_Aesthetics_WebPos.Components.Services.WhatsApp;

public sealed class WhatsAppService
{
    private readonly IJSRuntime js;

    public Func<string, Task<bool>>? NativeOpenUrlHandler { get; set; }

    public WhatsAppService(IJSRuntime js)
    {
        this.js = js;
    }

    public string BuildMessage(string companyName, string eInvoiceUrl, string billUrl)
    {
        var sb = new StringBuilder();
        sb.AppendLine(companyName);
        sb.AppendLine();

        if (!string.IsNullOrWhiteSpace(eInvoiceUrl))
        {
            sb.AppendLine("eInvoice Request Link:");
            sb.AppendLine(eInvoiceUrl);
            sb.AppendLine();
        }

        if (!string.IsNullOrWhiteSpace(billUrl))
        {
            sb.AppendLine("Invoice Download Link:");
            sb.AppendLine(billUrl);
            sb.AppendLine();
        }

        return sb.ToString().TrimEnd();
    }

    public async Task OpenAsync(string phoneNumber, string message)
    {
        var cleanPhone = new string(phoneNumber.Where(char.IsDigit).ToArray());
        var encodedMessage = Uri.EscapeDataString(message);
        var url = $"https://wa.me/{cleanPhone}?text={encodedMessage}";

        if (NativeOpenUrlHandler is not null)
        {
            var success = await NativeOpenUrlHandler(url);
            if (success)
            {
                return;
            }
        }

        await js.InvokeVoidAsync("open", url, "_blank");
    }
}
