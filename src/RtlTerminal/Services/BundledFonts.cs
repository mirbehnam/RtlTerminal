using System.IO;
using System.Windows;
using System.Windows.Media;

namespace RtlTerminal;

/// <summary>Private fonts shipped with the app; no Windows font installation required.</summary>
public static class BundledFonts
{
    private sealed record Entry(string Id, string Name, string Family, string Stem, string[] Styles);
    private static readonly string[] FullStyles = ["Regular", "Bold", "Italic", "BoldItalic"];
    private static readonly Entry[] Entries =
    [
        new("JetBrainsMono", "JetBrains Mono", "JetBrains Mono", "JetBrainsMono-", FullStyles),
        new("CascadiaMono", "Cascadia Mono", "Cascadia Mono", "CascadiaMono-", FullStyles),
        new("FiraCode", "Fira Code", "Fira Code", "FiraCode-", ["Regular", "Bold"]),
        new("Iosevka", "Iosevka", "Iosevka", "Iosevka-", FullStyles),
        new("Hack", "Hack", "Hack", "Hack-", FullStyles),
        new("GeistMono", "Geist Mono", "Geist Mono", "GeistMono-", FullStyles),
        new("VazirCode", "Vazir Code", "Vazir Code", "Vazir-Code", [""]),
        new("CascadiaMonoNF", "Cascadia Mono NF", "Cascadia Mono NF", "CascadiaMonoNF-", FullStyles),
        new("JetBrainsMonoNF", "JetBrains Mono NF", "JetBrainsMono NFM", "JetBrainsMonoNerdFontMono-", FullStyles)
    ];
    public static IReadOnlyList<string> Names { get; } = Entries.Select(e => e.Name).ToArray();
    public static FontFamily DefaultFamily => Resolve("Cascadia Mono");
    private static Entry? Find(string value) => Entries.FirstOrDefault(e =>
        string.Equals(value, e.Name, StringComparison.OrdinalIgnoreCase) ||
        string.Equals(value, e.Family, StringComparison.OrdinalIgnoreCase) ||
        string.Equals(value, "./#" + e.Family, StringComparison.OrdinalIgnoreCase) ||
        e.Id == "JetBrainsMonoNF" && string.Equals(value, "JetBrainsMono Nerd Font Mono", StringComparison.OrdinalIgnoreCase));

    public static FontFamily Resolve(string name)
    {
        var first = name.Split(',')[0].Trim();
        var entry = Find(IsExcluded(first) ? "Cascadia Mono" : first);
        return entry is null ? new FontFamily(name) : new FontFamily(
            new Uri($"pack://application:,,,/RtlTerminal;component/Assets/Fonts/{entry.Id}/"), "./#" + entry.Family);
    }
    public static string DisplayName(FontFamily family) => Find(family.Source)?.Name ?? family.Source;
    public static string CanonicalName(string name) => Find(name)?.Name ?? name;
    public static bool IsExcluded(string name) =>
        name.Equals("Amiri", StringComparison.OrdinalIgnoreCase) || name.StartsWith("Amiri ", StringComparison.OrdinalIgnoreCase) ||
        name.Equals("Simplified Arabic Fixed", StringComparison.OrdinalIgnoreCase);

    // Make the local HTML preview independent of the browser's installed fonts too.
    public static string FontFaceCss(string name, IReadOnlySet<(bool Bold, bool Italic)>? needed = null)
    {
        var entry = Find(name);
        if (entry is null) return string.Empty;
        var css = new System.Text.StringBuilder();
        var styles = needed is null ? entry.Styles : needed.Select(pair =>
        {
            var style = pair.Bold ? (pair.Italic ? "BoldItalic" : "Bold") : pair.Italic ? "Italic" : "Regular";
            return entry.Styles.Contains(style) ? style : pair.Bold && entry.Styles.Contains("Bold") ? "Bold" : entry.Styles[0];
        }).Distinct();
        foreach (var style in styles)
        {
            var path = $"pack://application:,,,/RtlTerminal;component/Assets/Fonts/{entry.Id}/{entry.Stem}{style}.ttf";
            using var stream = Application.GetResourceStream(new Uri(path))!.Stream;
            using var bytes = new MemoryStream(); stream.CopyTo(bytes);
            css.Append("@font-face{font-family:'").Append(entry.Name).Append("';font-weight:")
                .Append(style.Contains("Bold") ? "700" : "400").Append(";font-style:")
                .Append(style.Contains("Italic") ? "italic" : "normal")
                .Append(";src:url(data:font/ttf;base64,").Append(Convert.ToBase64String(bytes.ToArray()))
                .Append(") format('truetype');}");
        }
        return css.ToString();
    }
}
