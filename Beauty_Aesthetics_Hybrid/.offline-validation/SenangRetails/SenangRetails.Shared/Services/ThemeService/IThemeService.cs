using System.Collections.Generic;
using System.Threading.Tasks;
using SenangRetails.Shared.Models.DTOs;

namespace SenangRetails.Shared.Services.ThemeService
{
    public interface IThemeService
    {
        Task<CustomThemeModel> GetCurrentThemeAsync();
        Task<bool> SaveThemeAsync(CustomThemeModel theme);
        Task ApplyThemeAsync(CustomThemeModel theme);
        Task ResetToDefaultAsync();
        List<CustomThemeModel> GetPresetThemes();
        Task InitializeThemeAsync();
        Task<List<CustomThemeModel>> GetSavedCustomThemesAsync();
        Task<bool> AddSavedCustomThemeAsync(CustomThemeModel theme);
        Task<bool> DeleteSavedCustomThemeAsync(string presetName);
    }
}
