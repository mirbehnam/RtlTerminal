using Microsoft.Win32;
using System.Windows;

namespace RtlTerminal;

public readonly record struct TerminalFontSettings(
    string Family,
    double Size,
    bool Bold,
    bool Italic);

public static class AppSettings
{
    private const string SettingsKey = @"Software\RtlTerminal";

    public static TerminalFontSettings? LoadFont()
    {
        using var key = Registry.CurrentUser.OpenSubKey(SettingsKey);
        var family = key?.GetValue("FontFamily") as string;

        if (string.IsNullOrWhiteSpace(family))
            return null;

        var size = key?.GetValue("FontSize") is string sizeText &&
            double.TryParse(
                sizeText,
                System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture,
                out var parsedSize)
            ? parsedSize
            : 15;

        return new TerminalFontSettings(
            family,
            Math.Clamp(size, 8, 72),
            key?.GetValue("FontBold") is int bold && bold == 1,
            key?.GetValue("FontItalic") is int italic && italic == 1);
    }

    public static void SaveFont(TerminalFontSettings settings)
    {
        using var key = Registry.CurrentUser.CreateSubKey(SettingsKey);
        key.SetValue("FontFamily", settings.Family);
        key.SetValue(
            "FontSize",
            settings.Size.ToString(
                System.Globalization.CultureInfo.InvariantCulture));
        key.SetValue(
            "FontBold",
            settings.Bold ? 1 : 0,
            RegistryValueKind.DWord);
        key.SetValue(
            "FontItalic",
            settings.Italic ? 1 : 0,
            RegistryValueKind.DWord);
    }
}
