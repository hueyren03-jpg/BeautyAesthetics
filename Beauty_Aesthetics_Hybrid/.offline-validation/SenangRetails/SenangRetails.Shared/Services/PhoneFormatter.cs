using System;
using System.Linq;

namespace SenangRetails.Shared.Services
{
    public static class PhoneFormatter
    {
        public static string Format(string? phone)
        {
            if (string.IsNullOrWhiteSpace(phone)) return "";
            
            // Clean digits
            var digits = new string(phone.Where(char.IsDigit).ToArray());
            
            if (digits.StartsWith("65"))
            {
                var body = digits.Substring(2);
                if (body.Length <= 4)
                    return $"(65) {body}";
                return $"(65) {body.Substring(0, 4)}-{body.Substring(4)}";
            }
            else if (digits.StartsWith("60"))
            {
                if (digits.Length >= 4)
                {
                    if (digits.StartsWith("6011"))
                    {
                        var pref = "6011";
                        var body = digits.Substring(4);
                        if (body.Length <= 3)
                            return $"({pref}) {body}";
                        return $"({pref}) {body.Substring(0, 3)}-{body.Substring(3)}";
                    }
                    else if (digits.StartsWith("601"))
                    {
                        var pref = digits.Substring(0, 4);
                        var body = digits.Substring(4);
                        if (body.Length <= 3)
                            return $"({pref}) {body}";
                        return $"({pref}) {body.Substring(0, 3)}-{body.Substring(3)}";
                    }
                }
                return digits;
            }
            else
            {
                // Fallback for raw numbers without country code
                if ((digits.StartsWith("8") || digits.StartsWith("9")) && digits.Length == 8)
                {
                    var converted = "65" + digits;
                    return Format(converted);
                }
                else if (digits.StartsWith("01"))
                {
                    var converted = "60" + digits.Substring(1);
                    return Format(converted);
                }
                else if (digits.StartsWith("1"))
                {
                    var converted = "60" + digits;
                    return Format(converted);
                }
                return digits;
            }
        }
    }
}
