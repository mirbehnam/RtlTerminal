using System.Windows;
using System.Windows.Media;

namespace RtlTerminal;

public enum AppThemeKind
{
    Dark,
    Light
}

public static class AppTheme
{
    private static readonly TerminalColor[] LightForegrounds =
    [
        new(36, 41, 47),
        new(207, 34, 46),
        new(17, 99, 41),
        new(125, 78, 0),
        new(5, 80, 174),
        new(130, 80, 223),
        new(27, 124, 131),
        new(87, 96, 106),
        new(110, 119, 129),
        new(164, 14, 38),
        new(26, 127, 55),
        new(99, 60, 1),
        new(9, 105, 218),
        new(130, 80, 223),
        new(49, 146, 170),
        new(36, 41, 47)
    ];

    // Applications pick colors assuming a dark screen, so explicit
    // backgrounds become light tints that keep the remapped text readable.
    private static readonly TerminalColor[] LightBackgrounds =
    [
        new(246, 248, 250),
        new(255, 235, 233),
        new(218, 251, 225),
        new(255, 248, 197),
        new(221, 244, 255),
        new(251, 239, 255),
        new(216, 243, 246),
        new(208, 215, 222),
        new(208, 215, 222),
        new(255, 206, 203),
        new(172, 238, 187),
        new(255, 236, 153),
        new(182, 227, 255),
        new(236, 215, 255),
        new(179, 233, 239),
        new(234, 238, 242)
    ];

    private static readonly Dictionary<TerminalColor, int> PaletteIndexes =
        TerminalBuffer.AnsiColors
            .Select((color, index) => (color, index))
            .GroupBy(entry => entry.color)
            .ToDictionary(group => group.Key, group => group.First().index);

    public static AppThemeKind Current { get; private set; } = AppThemeKind.Dark;

    public static bool IsLight => Current == AppThemeKind.Light;

    public static TerminalColor DefaultForeground =>
        IsLight ? new(36, 41, 47) : new(230, 230, 230);

    public static TerminalColor DefaultBackground =>
        IsLight ? new(255, 255, 255) : new(12, 12, 12);

    public static TerminalColor MapForeground(TerminalColor color) =>
        IsLight && PaletteIndexes.TryGetValue(color, out var index)
            ? LightForegrounds[index]
            : color;

    public static TerminalColor MapBackground(TerminalColor color) =>
        IsLight && PaletteIndexes.TryGetValue(color, out var index)
            ? LightBackgrounds[index]
            : color;

    public static Color TabAccent => IsLight ? Rgb(15, 118, 110) : Rgb(114, 214, 197);
    public static Color TabIcon => IsLight ? Rgb(101, 109, 118) : Rgb(139, 148, 158);
    public static Color ActiveTabText => IsLight ? Rgb(31, 35, 40) : Rgb(240, 244, 248);
    public static Color InactiveTabText => IsLight ? Rgb(87, 96, 106) : Rgb(165, 174, 184);
    public static Color ActiveTab => IsLight ? Rgb(246, 248, 250) : Rgb(48, 49, 52);
    public static Color HoverTab => IsLight ? Rgb(215, 220, 226) : Rgb(42, 43, 47);
    public static Color TabSeparator => IsLight ? Rgb(175, 184, 193) : Rgb(78, 80, 84);
    public static Color CursorFill => IsLight ? Color.FromArgb(90, 36, 41, 47) : Color.FromArgb(90, 230, 230, 230);
    public static Color ScrollTrack => IsLight ? Rgb(246, 248, 250) : Rgb(17, 17, 17);
    public static Color ScrollThumb => IsLight ? Rgb(190, 196, 203) : Rgb(85, 85, 85);
    public static Color ScrollThumbHover => IsLight ? Rgb(150, 158, 167) : Rgb(136, 136, 136);
    public static Color ScrollThumbPressed => IsLight ? Rgb(110, 119, 129) : Rgb(170, 170, 170);
    public static Color CursorBorder => IsLight ? Rgb(110, 119, 129) : Colors.LightGray;

    public static void Apply(AppThemeKind theme)
    {
        Current = theme;
        TerminalBuffer.DefaultForeground = DefaultForeground;
        TerminalBuffer.DefaultBackground = DefaultBackground;
        var light = theme == AppThemeKind.Light;
        var resources = Application.Current.Resources;

        void Set(string key, byte r, byte g, byte b, byte lr, byte lg, byte lb)
        {
            resources[key] = Frozen(light ? Rgb(lr, lg, lb) : Rgb(r, g, b));
        }

        Set("TerminalBackgroundBrush", 12, 12, 12, 255, 255, 255);
        Set("TerminalForegroundBrush", 230, 230, 230, 36, 41, 47);
        Set("TitleBarBrush", 32, 33, 36, 222, 226, 231);
        Set("ToolbarBrush", 48, 49, 52, 246, 248, 250);
        Set("ToolbarBorderBrush", 60, 64, 67, 208, 215, 222);
        Set("ChromeForegroundBrush", 240, 240, 240, 31, 35, 40);
        Set("ChromeStrongForegroundBrush", 255, 255, 255, 0, 0, 0);
        Set("MutedForegroundBrush", 139, 148, 158, 87, 96, 106);
        Set("AccentBrush", 114, 214, 197, 15, 118, 110);
        Set("FocusBrush", 168, 199, 250, 11, 87, 208);
        Set("MenuBackgroundBrush", 37, 37, 38, 255, 255, 255);
        Set("MenuBorderBrush", 63, 63, 70, 208, 215, 222);
        Set("MenuHighlightBrush", 58, 58, 58, 234, 238, 242);
        Set("MenuOpenBrush", 65, 65, 65, 220, 225, 230);
        Set("SeparatorBrush", 69, 70, 74, 216, 222, 228);
        Set("CaptionHoverBrush", 64, 64, 64, 205, 210, 216);
        Set("CaptionPressedBrush", 80, 80, 80, 190, 196, 203);
        Set("ButtonPressedBrush", 70, 70, 70, 208, 215, 222);
        Set("RoundButtonHoverBrush", 64, 65, 69, 205, 210, 216);
        Set("RoundButtonPressedBrush", 81, 82, 86, 190, 196, 203);
        Set("RoundButtonFocusBrush", 69, 79, 96, 196, 214, 240);

        // Menus in the window share the terminal scrollbar style.
        resources["ScrollTrackBrush"] = Frozen(ScrollTrack);
        resources["ScrollThumbBrush"] = Frozen(ScrollThumb);
        resources["ScrollThumbHoverBrush"] = Frozen(ScrollThumbHover);
        resources["ScrollThumbPressedBrush"] = Frozen(ScrollThumbPressed);
    }

    private static SolidColorBrush Frozen(Color color)
    {
        var brush = new SolidColorBrush(color);
        brush.Freeze();
        return brush;
    }

    private static Color Rgb(byte red, byte green, byte blue) =>
        Color.FromRgb(red, green, blue);
}
