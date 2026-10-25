using System.Windows.Media;

namespace RtlTerminal;

public sealed record ThemeOption(string Name, AppThemeKind Theme, ColorScheme? Scheme)
{
    public string ModeLabel => Theme == AppThemeKind.Dark ? "Dark" : "Light";
    public TerminalColor Background => Scheme?.DefaultBackground ?? (Theme == AppThemeKind.Dark ? new(12,12,12) : new(255,255,255));
    public TerminalColor Foreground => Scheme?.DefaultForeground ?? (Theme == AppThemeKind.Dark ? new(230,230,230) : new(36,41,47));
    public Brush Swatch => Brush(Background);
    public TerminalColor Ansi(int index) => Scheme?.Text(index) ?? AppTheme.DefaultAnsiForeground(Theme,index);
    public static SolidColorBrush Brush(TerminalColor c) => new(Color.FromRgb(c.Red,c.Green,c.Blue));

    public static IReadOnlyList<ThemeOption> All { get; } = new[]
    {
        new ThemeOption("Default Dark", AppThemeKind.Dark, null),
        new ThemeOption("Default Light", AppThemeKind.Light, null)
    }.Concat(ColorScheme.All.Select(s => new ThemeOption(s.Name,s.Theme,s))).ToArray();
}
