using System;
using System.Collections.Generic;

namespace SenangRetails.Shared.Models.DTOs
{
    public class CustomThemeModel
    {
        public string Rank { get; set; } = "⭐";
        public string PresetName { get; set; } = "Vibrant Orange";
        public string PrimaryColor { get; set; } = "#F2600C";
        public string HoverColor { get; set; } = "#EA580C";
        public string ActiveColor { get; set; } = "#C2410C";
        public string AccentColor { get; set; } = "#2563EB";
        public string TextColor { get; set; } = "#1E293B";
        public string SurfaceColor { get; set; } = "#FFF7ED";
        public string BadgeColor { get; set; } = "#FFEDD5";
        public string BorderColor { get; set; } = "#FED7AA";
        public string TextOnPrimary { get; set; } = "#FFFFFF";
        public string Character { get; set; } = "Energetic, vibrant, warm brand";

        public string PrimaryRgb => $"{HexToRgb(PrimaryColor).r}, {HexToRgb(PrimaryColor).g}, {HexToRgb(PrimaryColor).b}";
        public string AccentRgb => $"{HexToRgb(AccentColor).r}, {HexToRgb(AccentColor).g}, {HexToRgb(AccentColor).b}";

        public static CustomThemeModel CreatePreset(
            string rank,
            string presetName,
            string primaryHex,
            string hoverHex,
            string accentHex,
            string textHex,
            string character)
        {
            primaryHex = NormalizeHex(primaryHex);
            hoverHex = NormalizeHex(hoverHex);
            accentHex = NormalizeHex(accentHex);
            textHex = NormalizeHex(textHex);

            var (r, g, b) = HexToRgb(primaryHex);
            var active = AdjustBrightness(r, g, b, -0.25f);
            var surface = MixWithWhite(r, g, b, 0.94f);
            var badge = MixWithWhite(r, g, b, 0.86f);
            var border = MixWithWhite(r, g, b, 0.76f);
            var textOnPrimary = GetContrastingTextColor(r, g, b);

            return new CustomThemeModel
            {
                Rank = rank,
                PresetName = presetName,
                PrimaryColor = primaryHex,
                HoverColor = hoverHex,
                ActiveColor = active,
                AccentColor = accentHex,
                TextColor = textHex,
                SurfaceColor = surface,
                BadgeColor = badge,
                BorderColor = border,
                TextOnPrimary = textOnPrimary,
                Character = character
            };
        }

        public static CustomThemeModel CreateFromPrimary(string primaryHex, string presetName = "Custom")
        {
            primaryHex = NormalizeHex(primaryHex);
            var (r, g, b) = HexToRgb(primaryHex);

            var hover = AdjustBrightness(r, g, b, -0.12f);
            var active = AdjustBrightness(r, g, b, -0.25f);
            var surface = MixWithWhite(r, g, b, 0.94f);
            var badge = MixWithWhite(r, g, b, 0.86f);
            var border = MixWithWhite(r, g, b, 0.76f);
            var textOnPrimary = GetContrastingTextColor(r, g, b);

            return new CustomThemeModel
            {
                Rank = "",
                PresetName = presetName,
                PrimaryColor = primaryHex,
                HoverColor = hover,
                ActiveColor = active,
                AccentColor = "#2563EB",
                TextColor = "#1E293B",
                SurfaceColor = surface,
                BadgeColor = badge,
                BorderColor = border,
                TextOnPrimary = textOnPrimary,
                Character = "Custom Brand Palette"
            };
        }

        private static string NormalizeHex(string hex)
        {
            if (string.IsNullOrWhiteSpace(hex)) return "#F2600C";
            hex = hex.Trim();
            if (!hex.StartsWith("#")) hex = "#" + hex;
            return hex.Length == 7 ? hex.ToUpperInvariant() : "#F2600C";
        }

        private static (int r, int g, int b) HexToRgb(string hex)
        {
            try
            {
                if (hex.StartsWith("#")) hex = hex.Substring(1);
                if (hex.Length == 6)
                {
                    int r = Convert.ToInt32(hex.Substring(0, 2), 16);
                    int g = Convert.ToInt32(hex.Substring(2, 2), 16);
                    int b = Convert.ToInt32(hex.Substring(4, 2), 16);
                    return (r, g, b);
                }
            }
            catch { }
            return (242, 96, 12);
        }

        private static string RgbToHex(int r, int g, int b)
        {
            r = Math.Clamp(r, 0, 255);
            g = Math.Clamp(g, 0, 255);
            b = Math.Clamp(b, 0, 255);
            return $"#{r:X2}{g:X2}{b:X2}";
        }

        private static string AdjustBrightness(int r, int g, int b, float factor)
        {
            int newR = factor > 0 ? (int)(r + (255 - r) * factor) : (int)(r * (1 + factor));
            int newG = factor > 0 ? (int)(g + (255 - g) * factor) : (int)(g * (1 + factor));
            int newB = factor > 0 ? (int)(b + (255 - b) * factor) : (int)(b * (1 + factor));
            return RgbToHex(newR, newG, newB);
        }

        private static string MixWithWhite(int r, int g, int b, float whiteWeight)
        {
            int newR = (int)(r * (1 - whiteWeight) + 255 * whiteWeight);
            int newG = (int)(g * (1 - whiteWeight) + 255 * whiteWeight);
            int newB = (int)(b * (1 - whiteWeight) + 255 * whiteWeight);
            return RgbToHex(newR, newG, newB);
        }

        private static string GetContrastingTextColor(int r, int g, int b)
        {
            double luminance = (0.299 * r + 0.587 * g + 0.114 * b) / 255;
            return luminance > 0.65 ? "#1E293B" : "#FFFFFF";
        }
    }
}
