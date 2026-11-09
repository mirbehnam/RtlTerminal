using Microsoft.Win32;
using System.Windows;

namespace RtlTerminal;

public readonly record struct TerminalFontSettings(
    string Family,
    double Size,
    bool Bold,
    bool Italic);

public enum HtmlPreviewScope { CurrentPage, RecentToEnd, EntireHistory }

public static class AppSettings
{
    private const string SettingsKey = @"Software\RtlTerminal";
    private static readonly int[] SupportedHistorySizes = [2000, 5000, 10000];

    public static DirectoryOpenMode LoadDirectoryOpenMode()
    {
        using var key = Registry.CurrentUser.OpenSubKey(SettingsKey);
        return Enum.TryParse<DirectoryOpenMode>(key?.GetValue("DirectoryOpenMode") as string, out var mode) && Enum.IsDefined(mode) ? mode : DirectoryOpenMode.Smart;
    }

    public static void SaveDirectoryOpenMode(DirectoryOpenMode mode)
    {
        using var key = Registry.CurrentUser.CreateSubKey(SettingsKey);
        key.SetValue("DirectoryOpenMode", (Enum.IsDefined(mode) ? mode : DirectoryOpenMode.Smart).ToString());
    }

    public static TerminalBellSettings LoadBellSettings()
    {
        using var key = Registry.CurrentUser.OpenSubKey(SettingsKey);
        return TerminalBellSettings.Read(key);
    }

    public static void SaveBellSettings(TerminalBellSettings settings)
    {
        using var key = Registry.CurrentUser.CreateSubKey(SettingsKey);
        settings.Write(key);
    }

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

    public static string? LoadWslDistribution()
    {
        using var key = Registry.CurrentUser.OpenSubKey(SettingsKey);
        return key?.GetValue("WslDistribution") as string;
    }

    public static void SaveWslDistribution(string? distribution)
    {
        using var key = Registry.CurrentUser.CreateSubKey(SettingsKey);
        if (string.IsNullOrWhiteSpace(distribution)) key.DeleteValue("WslDistribution", false);
        else key.SetValue("WslDistribution", distribution, RegistryValueKind.String);
    }

    public static PaneShortcutSettings LoadPaneShortcuts()
    {
        using var key = Registry.CurrentUser.OpenSubKey(SettingsKey);
        bool Enabled(string name) => key?.GetValue(name) is not int value || value != 0;
        return new(Enabled("PaneSplitShortcut"), Enabled("PaneNavigateShortcut"), Enabled("PaneCloseShortcut"));
    }

    public static void SavePaneShortcuts(PaneShortcutSettings settings)
    {
        using var key = Registry.CurrentUser.CreateSubKey(SettingsKey);
        key.SetValue("PaneSplitShortcut", settings.Split ? 1 : 0, RegistryValueKind.DWord);
        key.SetValue("PaneNavigateShortcut", settings.Navigate ? 1 : 0, RegistryValueKind.DWord);
        key.SetValue("PaneCloseShortcut", settings.Close ? 1 : 0, RegistryValueKind.DWord);
    }

    public static HtmlPreviewScope LoadPreviewScope()
    {
        using var key = Registry.CurrentUser.OpenSubKey(SettingsKey);
        return Enum.TryParse<HtmlPreviewScope>(key?.GetValue("HtmlPreviewScope") as string, out var scope) && Enum.IsDefined(scope)
            ? scope : HtmlPreviewScope.CurrentPage;
    }

    public static void SavePreviewScope(HtmlPreviewScope scope)
    {
        using var key = Registry.CurrentUser.CreateSubKey(SettingsKey);
        key.SetValue("HtmlPreviewScope", scope.ToString(), RegistryValueKind.String);
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

    public static string? LoadSkippedUpdateVersion()
    {
        using var key = Registry.CurrentUser.OpenSubKey(SettingsKey);
        return key?.GetValue("SkippedUpdateVersion") as string;
    }

    public static void SaveSkippedUpdateVersion(string? version)
    {
        using var key = Registry.CurrentUser.CreateSubKey(SettingsKey);

        if (string.IsNullOrWhiteSpace(version))
            key.DeleteValue("SkippedUpdateVersion", throwOnMissingValue: false);
        else
            key.SetValue(
                "SkippedUpdateVersion",
                version,
                RegistryValueKind.String);
    }

    public static AppThemeKind LoadTheme()
    {
        using var key = Registry.CurrentUser.OpenSubKey(SettingsKey);
        return Enum.TryParse(key?.GetValue("Theme") as string, out AppThemeKind theme)
            ? theme
            : AppThemeKind.Dark;
    }

    public static void SaveTheme(AppThemeKind theme)
    {
        using var key = Registry.CurrentUser.CreateSubKey(SettingsKey);
        key.SetValue("Theme", theme.ToString(), RegistryValueKind.String);
    }

    public static string? LoadColorScheme()
    {
        using var key = Registry.CurrentUser.OpenSubKey(SettingsKey);
        return key?.GetValue("ColorScheme") as string;
    }

    public static void SaveColorScheme(string? name)
    {
        using var key = Registry.CurrentUser.CreateSubKey(SettingsKey);
        key.SetValue("ColorScheme", name ?? "", RegistryValueKind.String);
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
            : 14;

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
