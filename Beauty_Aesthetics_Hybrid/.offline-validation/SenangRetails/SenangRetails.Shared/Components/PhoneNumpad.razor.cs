using Microsoft.AspNetCore.Components;
using System.Linq;
using System.Threading.Tasks;

namespace SenangRetails.Shared.Components
{
    public partial class PhoneNumpad
    {
        [Parameter] public bool IsOpen { get; set; }
        [Parameter] public string InitialValue { get; set; } = string.Empty;
        [Parameter] public EventCallback<string> OnSave { get; set; }
        [Parameter] public EventCallback OnClose { get; set; }
        [Parameter] public int ZIndexBackdrop { get; set; } = 7100;

        private string selectedPrefix = "60";
        private string bodyDigits = string.Empty;
        private bool isOpenPrev;
        private bool showDropdown = false;

        protected override void OnParametersSet()
        {
            if (IsOpen && !isOpenPrev)
            {
                ParsePhone(InitialValue);
                showDropdown = false;
            }
            isOpenPrev = IsOpen;
        }

        private void ParsePhone(string rawPhone)
        {
            if (string.IsNullOrWhiteSpace(rawPhone))
            {
                bodyDigits = "";
                selectedPrefix = "60";
                return;
            }

            var digits = new string(rawPhone.Where(char.IsDigit).ToArray());

            if (digits.StartsWith("65"))
            {
                selectedPrefix = "65";
                bodyDigits = digits.Substring(2);
            }
            else if (digits.StartsWith("60"))
            {
                selectedPrefix = "60";
                bodyDigits = digits.Substring(2);
            }
            else if ((digits.StartsWith("8") || digits.StartsWith("9")) && digits.Length == 8)
            {
                selectedPrefix = "65";
                bodyDigits = digits;
            }
            else
            {
                selectedPrefix = "60";
                if (digits.StartsWith("01"))
                {
                    digits = "60" + digits.Substring(1);
                }
                else if (digits.StartsWith("1"))
                {
                    digits = "60" + digits;
                }
                else if (digits.StartsWith("0"))
                {
                    digits = "60" + digits.Substring(1);
                }
                
                if (digits.StartsWith("60"))
                {
                    bodyDigits = digits.Substring(2);
                }
                else
                {
                    bodyDigits = digits;
                }
            }

            // Max digits restriction
            int maxLength = selectedPrefix == "65" ? 8 : 10;
            if (bodyDigits.Length > maxLength)
            {
                bodyDigits = bodyDigits.Substring(0, maxLength);
            }
        }

        private string GetCountryName()
        {
            return selectedPrefix == "65" ? "Singapore" : "Malaysia";
        }

        private void ToggleDropdown()
        {
            showDropdown = !showDropdown;
        }

        private void SelectCountry(string prefix)
        {
            if (selectedPrefix != prefix)
            {
                bodyDigits = string.Empty;
            }
            selectedPrefix = prefix;
            showDropdown = false;
        }

        private string GetFormattedDisplay()
        {
            if (selectedPrefix == "65")
            {
                if (string.IsNullOrEmpty(bodyDigits))
                {
                    return selectedPrefix;
                }
                if (bodyDigits.Length <= 4)
                    return $"({selectedPrefix}) {bodyDigits}";
                return $"({selectedPrefix}) {bodyDigits.Substring(0, 4)}-{bodyDigits.Substring(4)}";
            }
            else
            {
                var digits = selectedPrefix + bodyDigits; // E.g. "60166299810"
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
        }

        private void AppendDigit(string digit)
        {
            showDropdown = false;
            int maxLength = selectedPrefix == "65" ? 8 : 10;
            if (bodyDigits.Length >= maxLength)
            {
                return;
            }
            bodyDigits += digit;
        }

        private void ClearDigits()
        {
            showDropdown = false;
            bodyDigits = string.Empty;
        }

        private void BackspaceDigit()
        {
            showDropdown = false;
            if (!string.IsNullOrEmpty(bodyDigits))
            {
                bodyDigits = bodyDigits[..^1];
            }
        }

        private bool IsValidPhone()
        {
            if (selectedPrefix == "65")
            {
                return IsValidSingaporePhone();
            }
            return IsValidMalaysiaPhone();
        }

        private bool IsValidSingaporePhone()
        {
            if (string.IsNullOrEmpty(bodyDigits) || (!bodyDigits.StartsWith("8") && !bodyDigits.StartsWith("9")))
            {
                return false;
            }
            return bodyDigits.Length == 8;
        }

        private bool IsValidMalaysiaPhone()
        {
            if (string.IsNullOrEmpty(bodyDigits) || !bodyDigits.StartsWith("1"))
            {
                return false;
            }
            return bodyDigits.Length == 9 || bodyDigits.Length == 10;
        }

        private async Task Save()
        {
            var finalValue = selectedPrefix + bodyDigits;
            await OnSave.InvokeAsync(finalValue);
            await Close();
        }

        private async Task Close()
        {
            await OnClose.InvokeAsync();
        }
    }
}
