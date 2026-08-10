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
    private static readonly int[] SupportedHistorySizes = [2000, 5000, 10000];

    public static string? LoadTerminalProfile()
    {
        using var key = Registry.CurrentUser.OpenSubKey(SettingsKey);
        return key?.GetValue("TerminalProfile") as string;
    }

    public static void SaveTerminalProfile(string profile)
    {
        using var key = Registry.CurrentUser.CreateSubKey(SettingsKey);
        key.SetValue("TerminalProfile", profile, RegistryValueKind.String);
    }

    public static IReadOnlyList<string> LoadLastCmdDirectories()
    {
        using var key = Registry.CurrentUser.OpenSubKey(SettingsKey);
        var directories = key?.GetValue("LastCmdDirectories") switch
        {
            string[] values => values,
            string value => [value],
            _ => []
        };

        return directories
            .Where(directory => !string.IsNullOrWhiteSpace(directory))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(10)
            .ToArray();
    }

    public static void RememberCmdDirectory(string directory)
    {
        var directories = LoadLastCmdDirectories()
            .Where(savedDirectory => !string.Equals(
                savedDirectory,
                directory,
                StringComparison.OrdinalIgnoreCase))
            .Prepend(directory)
            .Take(10)
            .ToArray();

        using var key = Registry.CurrentUser.CreateSubKey(SettingsKey);
        key.SetValue(
            "LastCmdDirectories",
            directories,
            RegistryValueKind.MultiString);
    }

    public static int LoadHistorySize()
    {
        using var key = Registry.CurrentUser.OpenSubKey(SettingsKey);
        var savedSize = key?.GetValue("HistorySize") is int value
            ? value
            : 2000;
        return SupportedHistorySizes.Contains(savedSize)
            ? savedSize
            : 2000;
    }

    public static void SaveHistorySize(int historySize)
    {
        if (!SupportedHistorySizes.Contains(historySize))
            historySize = 2000;

        using var key = Registry.CurrentUser.CreateSubKey(SettingsKey);
        key.SetValue(
            "HistorySize",
            historySize,
            RegistryValueKind.DWord);
    }

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
