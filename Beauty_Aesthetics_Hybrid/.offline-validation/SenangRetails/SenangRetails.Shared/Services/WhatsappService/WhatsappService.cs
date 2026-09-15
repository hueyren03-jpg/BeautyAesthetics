using Microsoft.JSInterop;
using System;
using System.Collections.Generic;
using System.Text;

namespace SenangRetails.Shared.Services.WhatsappService
{
    public class WhatsAppService
    {
        private readonly IJSRuntime _js;
        public Func<string, Task<bool>>? NativeOpenUrlHandler { get; set; }

        public WhatsAppService(IJSRuntime js) => _js = js;

        public string BuildMessage(string companyName, string einvoiceUrl, string billUrl)
        {
            var sb = new StringBuilder();
            sb.AppendLine(companyName);
            sb.AppendLine();

            if (!string.IsNullOrEmpty(einvoiceUrl))
            {
                sb.AppendLine("eInvoice Request Link:");
                sb.AppendLine(einvoiceUrl);
                sb.AppendLine();
            }

            if (!string.IsNullOrEmpty(billUrl))
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
            var encodedMsg = Uri.EscapeDataString(message);

            var url = $"https://wa.me/{cleanPhone}?text={encodedMsg}";

            if (NativeOpenUrlHandler != null)
            {
                var success = await NativeOpenUrlHandler(url);
                if (success) return;
            }

            await _js.InvokeVoidAsync("open", url, "_blank");
        }
    }
}
