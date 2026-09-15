using SenangRetails.Shared.Services;

namespace SenangRetails.Services
{
    /// <summary>
    /// MAUI-specific language service. Persists the chosen language via Preferences
    /// so it survives app restarts and logout/login cycles.
    /// </summary>
    public class MauiLanguageService : LanguageService
    {
        private const string PreferenceKey = "app_language";

        protected override string LoadSavedLanguage()
            => Microsoft.Maui.Storage.Preferences.Get(PreferenceKey, "English");

        protected override void SaveLanguage(string language)
            => Microsoft.Maui.Storage.Preferences.Set(PreferenceKey, language);
    }
}
