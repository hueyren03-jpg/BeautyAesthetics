using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.JSInterop;
using SenangRetails.Shared.Models.DTOs;

namespace SenangRetails.Shared.Services.ThemeService
{
    public class ThemeService : IThemeService
    {
        private readonly IJSRuntime _js;
        private const string ThemeStorageKey = "senang_custom_theme";
        private const string SavedPalettesStorageKey = "senang_saved_custom_palettes";

        public ThemeService(IJSRuntime js)
        {
            _js = js;
        }

        public List<CustomThemeModel> GetPresetThemes()
        {
            return new List<CustomThemeModel>
            {
                CustomThemeModel.CreatePreset("⭐", "Vibrant Orange", "#F2600C", "#EA580C", "#2563EB", "#1E293B", "Default brand color - energetic & warm"),
                CustomThemeModel.CreatePreset("🥇", "Circuit", "#3D63E0", "#2048CA", "#00A883", "#14161B", "Modern, trustworthy, professional"),
                CustomThemeModel.CreatePreset("🥈", "Editorial Neutral", "#17181A", "#2A2C30", "#8A6E4B", "#17181A", "Premium, minimal, sophisticated"),
                CustomThemeModel.CreatePreset("🥉", "Field", "#2F5233", "#1C321F", "#E4A63A", "#1F2A1E", "Natural, calm, reliable"),
                CustomThemeModel.CreatePreset("4", "Harbor", "#087A87", "#075E68", "#E98B58", "#152327", "Friendly, fresh, modern"),
                CustomThemeModel.CreatePreset("5", "Signal", "#D6350F", "#A6290C", "#0B1E4B", "#12131A", "Energetic, sales-focused"),
                CustomThemeModel.CreatePreset("6", "Ledger", "#6B1F2A", "#43141A", "#B4863C", "#1A1512", "Mature, luxurious, traditional"),
                CustomThemeModel.CreatePreset("7", "Indigo", "#5547D7", "#4033B7", "#16A085", "#191827", "Digital, stylish, slightly futuristic"),
                CustomThemeModel.CreatePreset("8", "Bloom", "#D6236A", "#AA1C54", "#FFC83D", "#24101C", "Fashionable, youthful, vibrant")
            };
        }

        public async Task<CustomThemeModel> GetCurrentThemeAsync()
        {
            try
            {
                var json = await _js.InvokeAsync<string?>("localStorage.getItem", ThemeStorageKey);
                if (!string.IsNullOrWhiteSpace(json))
                {
                    var theme = JsonSerializer.Deserialize<CustomThemeModel>(json);
                    if (theme != null) return theme;
                }
            }
            catch
            {
            }

            // Default theme: Vibrant Orange
            return CustomThemeModel.CreatePreset("⭐", "Vibrant Orange", "#F2600C", "#EA580C", "#2563EB", "#1E293B", "Default brand color - energetic & warm");
        }

        public async Task<bool> SaveThemeAsync(CustomThemeModel theme)
        {
            try
            {
                var json = JsonSerializer.Serialize(theme);
                await _js.InvokeVoidAsync("localStorage.setItem", ThemeStorageKey, json);
                await ApplyThemeAsync(theme);
                return true;
            }
            catch
            {
                return false;
            }
        }

        public async Task<List<CustomThemeModel>> GetSavedCustomThemesAsync()
        {
            try
            {
                var json = await _js.InvokeAsync<string?>("localStorage.getItem", SavedPalettesStorageKey);
                if (!string.IsNullOrWhiteSpace(json))
                {
                    var list = JsonSerializer.Deserialize<List<CustomThemeModel>>(json);
                    if (list != null) return list;
                }
            }
            catch { }
            return new List<CustomThemeModel>();
        }

        public async Task<bool> AddSavedCustomThemeAsync(CustomThemeModel theme)
        {
            try
            {
                var list = await GetSavedCustomThemesAsync();
                list.RemoveAll(x => x.PresetName.Equals(theme.PresetName, StringComparison.OrdinalIgnoreCase)
                                 || x.PrimaryColor.Equals(theme.PrimaryColor, StringComparison.OrdinalIgnoreCase));
                list.Insert(0, theme);
                var json = JsonSerializer.Serialize(list);
                await _js.InvokeVoidAsync("localStorage.setItem", SavedPalettesStorageKey, json);
                return true;
            }
            catch
            {
                return false;
            }
        }

        public async Task<bool> DeleteSavedCustomThemeAsync(string presetName)
        {
            try
            {
                var list = await GetSavedCustomThemesAsync();
                list.RemoveAll(x => x.PresetName.Equals(presetName, StringComparison.OrdinalIgnoreCase));
                var json = JsonSerializer.Serialize(list);
                await _js.InvokeVoidAsync("localStorage.setItem", SavedPalettesStorageKey, json);
                return true;
            }
            catch
            {
                return false;
            }
        }

        public async Task ApplyThemeAsync(CustomThemeModel theme)
        {
            try
            {
                var cssContent = $@"
                    :root {{
                        --primary: {theme.PrimaryColor} !important;
                        --primary-rgb: {theme.PrimaryRgb} !important;
                        --primary-dark: {theme.HoverColor} !important;
                        --primary-darker: {theme.ActiveColor} !important;
                        --primary-rose: {theme.PrimaryColor} !important;
                        --accent: {theme.AccentColor} !important;
                        --accent-rgb: {theme.AccentRgb} !important;
                        --color-accent: {theme.AccentColor} !important;
                        --color-primary: {theme.PrimaryColor} !important;
                        --color-base-1: {theme.PrimaryColor} !important;
                        --color-base-2: {theme.BorderColor} !important;
                        --color-base-3: {theme.PrimaryColor} !important;
                        --color-base-4: {theme.SurfaceColor} !important;
                        --color-base-5: {theme.BadgeColor} !important;
                        --color-secondary: {theme.SurfaceColor} !important;
                        --color-surface: #FFFFFF !important;
                        --color-background: #FAFAFA !important;
                        --color-background-secondary: #FFFFFF !important;
                        --c1: {theme.PrimaryColor} !important;
                        --c2: {theme.HoverColor} !important;
                        --c3: {theme.SurfaceColor} !important;
                        --c4: {theme.BadgeColor} !important;
                        --c5: #FAFAFA !important;
                        --bg-body: #FAFAFA !important;
                        --bg-surface: #FAFAFA !important;
                        --bg-card: #FFFFFF !important;
                        --bg-surface-2: {theme.BadgeColor} !important;
                        --beige-bg: {theme.SurfaceColor} !important;
                        --pink-light: {theme.SurfaceColor} !important;
                        --off-white-bg: #EDEAE3 !important;
                        --border: #EEEEEE !important;
                        --color-border: #EEEEEE !important;
                        --color-border-focus: {theme.PrimaryColor} !important;
                        --numpad-bg: {theme.SurfaceColor} !important;
                        --numpad-hover: {theme.BadgeColor} !important;
                        --text-primary: {theme.TextColor} !important;
                        --color-text: {theme.TextColor} !important;
                        --text-secondary: {theme.TextOnPrimary} !important;
                        --text-on-primary: {theme.TextOnPrimary} !important;
                        --shadow: 0 4px 12px rgba(0, 0, 0, 0.08) !important;
                    }}

                    /* Global Primary Buttons Across All Pages */
                    .btn-primary, .add-btn, .add-btn-sm, .save-btn, .btn-save, .pg-add-btn, .pg-add-btn-sm,
                    .pg-btn-save, .kp-submit, .submit-btn, .primary-btn, .btn-brand,
                    .checkout-btn, .action-btn-primary, .confirm-btn, .payment-btn, .sign-in-btn,
                    .orders-btn-primary, .cts-btn-primary, .add-payment-btn {{
                        background-color: {theme.PrimaryColor} !important;
                        color: {theme.TextOnPrimary} !important;
                        border-color: {theme.PrimaryColor} !important;
                        transition: all 0.18s cubic-bezier(0.4, 0, 0.2, 1) !important;
                    }}

                    /* Global Primary Buttons Hover */
                    .btn-primary:hover:not(:disabled), .add-btn:hover:not(:disabled), .add-btn-sm:hover:not(:disabled),
                    .save-btn:hover:not(:disabled), .btn-save:hover:not(:disabled),
                    .pg-add-btn:hover:not(:disabled), .pg-add-btn-sm:hover:not(:disabled), .pg-btn-save:hover:not(:disabled),
                    .kp-submit:hover:not(:disabled), .submit-btn:hover:not(:disabled), .primary-btn:hover:not(:disabled),
                    .btn-brand:hover:not(:disabled), .checkout-btn:hover:not(:disabled), .action-btn-primary:hover:not(:disabled),
                    .confirm-btn:hover:not(:disabled), .payment-btn:hover:not(:disabled), .sign-in-btn:hover:not(:disabled),
                    .orders-btn-primary:hover:not(:disabled), .cts-btn-primary:hover:not(:disabled),
                    .add-payment-btn:hover:not(:disabled) {{
                        background-color: {theme.HoverColor} !important;
                        border-color: {theme.HoverColor} !important;
                        color: {theme.TextOnPrimary} !important;
                        transform: translateY(-1px) !important;
                        box-shadow: 0 4px 14px rgba({theme.PrimaryRgb}, 0.3) !important;
                    }}

                    /* Global Primary Buttons Active / Pressed */
                    .btn-primary:active:not(:disabled), .add-btn:active:not(:disabled), .save-btn:active:not(:disabled),
                    .btn-save:active:not(:disabled), .pg-btn-save:active:not(:disabled), .kp-submit:active:not(:disabled),
                    .submit-btn:active:not(:disabled), .checkout-btn:active:not(:disabled), .action-btn-primary:active:not(:disabled),
                    .confirm-btn:active:not(:disabled), .payment-btn:active:not(:disabled), .sign-in-btn:active:not(:disabled),
                    .orders-btn-primary:active:not(:disabled), .cts-btn-primary:active:not(:disabled) {{
                        background-color: {theme.ActiveColor} !important;
                        border-color: {theme.ActiveColor} !important;
                        transform: translateY(0) !important;
                        box-shadow: 0 2px 6px rgba({theme.PrimaryRgb}, 0.2) !important;
                    }}

                    /* Interactive Cards & Items Hover States (SenangApp Design) */
                    .service-card:hover, .customer-list-item:hover, .order-item:hover,
                    .orders-btn-secondary:hover, .branch-card:hover, .tab:hover:not(.active),
                    .orders-btn-tertiary:hover {{
                        background-color: {theme.SurfaceColor} !important;
                    }}

                    /* Accent Highlights & Badges */
                    .accent-tag, .badge-accent, .highlight-accent {{
                        background-color: {theme.AccentColor} !important;
                        color: #FFFFFF !important;
                    }}

                    /* Global Active Navigation Items & Tabs */
                    .category-item.active, .nav-link.active, .tab-item.active, .nav-pills .nav-link.active,
                    .sidebar-item.active, .tab-btn.active, .tab.active, .type-tab.active {{
                        background-color: {theme.PrimaryColor} !important;
                        color: {theme.TextOnPrimary} !important;
                        border-color: {theme.PrimaryColor} !important;
                    }}

                    /* Active Category / Nav Left Accent */
                    .category-item.active i, .category-item.active span,
                    .nav-link.active i, .tab-item.active i, .tab.active span {{
                        color: {theme.TextOnPrimary} !important;
                    }}

                    /* Brand & Highlight Elements */
                    .text-orange, .text-brand, .brand-primary, .text-primary-color {{
                        color: {theme.PrimaryColor} !important;
                    }}
                    .brand:hover .brand-name {{
                        color: {theme.PrimaryColor} !important;
                    }}

                    /* Focus Ring for Inputs across All Pages */
                    input:focus, select:focus, textarea:focus, .form-control:focus, .pg-input:focus {{
                        border-color: {theme.PrimaryColor} !important;
                        box-shadow: 0 0 0 3px {theme.BadgeColor} !important;
                    }}

                    /* Checkboxes & Radio Accents */
                    input[type=""checkbox""], input[type=""radio""] {{
                        accent-color: {theme.PrimaryColor} !important;
                    }}
                    input[type=""checkbox""].custom-checkbox:checked {{
                        background-color: {theme.PrimaryColor} !important;
                        border-color: {theme.PrimaryColor} !important;
                    }}

                    /* Badges & Soft Highlight Pills */
                    .badge-primary, .tag-primary, .promo-tag, .discount-badge {{
                        background-color: {theme.BadgeColor} !important;
                        color: {theme.PrimaryColor} !important;
                        border-color: {theme.BorderColor} !important;
                    }}

                    /* Keypad Display & Highlighting */
                    .keypad-display {{
                        color: {theme.PrimaryColor} !important;
                        background: {theme.SurfaceColor} !important;
                        border-color: {theme.BorderColor} !important;
                    }}
                ";

                var escapedCss = cssContent.Replace("\r", "").Replace("\n", " ").Replace("\"", "\\\"");

                var script = $@"
                    (function() {{
                        let styleTag = document.getElementById('senang-dynamic-theme-style');
                        if (!styleTag) {{
                            styleTag = document.createElement('style');
                            styleTag.id = 'senang-dynamic-theme-style';
                            document.head.appendChild(styleTag);
                        }}
                        styleTag.innerHTML = ""{escapedCss}"";
                    }})();
                ";
                await _js.InvokeVoidAsync("eval", script);
            }
            catch
            {
            }
        }

        public async Task ResetToDefaultAsync()
        {
            var defaultTheme = CustomThemeModel.CreatePreset("⭐", "Vibrant Orange", "#F2600C", "#EA580C", "#2563EB", "#1E293B", "Default brand color - energetic & warm");
            await SaveThemeAsync(defaultTheme);
        }

        public async Task InitializeThemeAsync()
        {
            var theme = await GetCurrentThemeAsync();
            await ApplyThemeAsync(theme);
        }
    }
}
