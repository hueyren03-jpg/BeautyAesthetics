using Microsoft.Maui.Storage;
using System;
using System.Globalization;
using Beauty_Aesthetics_WebPos.Components.Services;

namespace Beauty_Aesthetics_Hybrid.Services
{
    public class CultureSettings : ICultureSettings
    {
        public string GetCulture()
        {
            return Preferences.Default.Get("selected_culture", "en");
        }

        public void SetCulture(string culture)
        {
            Preferences.Default.Set("selected_culture", culture);

            try
            {
                var cultureInfo = new CultureInfo(culture);
                CultureInfo.DefaultThreadCurrentCulture = cultureInfo;
                CultureInfo.DefaultThreadCurrentUICulture = cultureInfo;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to set thread culture: {ex.Message}");
            }
        }
    }
}
