using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using RtlTerminal;

internal static partial class Program
{
    private static void CheckBundledFonts()
    {
        var settings = new FontSettingsWindow("Cascadia Mono", 14, FontWeights.Normal, FontStyles.Normal, 2000);
        try
        {
            var recommended = (ListBox)settings.FindName("StandardFontListBox");
            var all = (ListBox)settings.FindName("FontListBox");
            foreach (var name in BundledFonts.Names)
            {
                if (!recommended.Items.Contains(name) || !all.Items.Contains(name))
                    throw new Exception($"Bundled font {name} must be available regardless of Windows font installation");
                recommended.SelectedItem = name;
                var family = ((TextBlock)settings.FindName("PreviewTextBlock")).FontFamily;
                if (BundledFonts.DisplayName(family) != name) throw new Exception("Preview selected the wrong private font");
                foreach (var (weight, style) in new[] { (FontWeights.Normal, FontStyles.Normal), (FontWeights.Bold, FontStyles.Normal), (FontWeights.Normal, FontStyles.Italic), (FontWeights.Bold, FontStyles.Italic) })
                {
                    var face = new Typeface(family, style, weight, FontStretches.Normal);
                    if (!face.TryGetGlyphTypeface(out var glyph) || !glyph.FontUri.AbsoluteUri.Contains("/Assets/Fonts/"))
                        throw new Exception($"{name} fell back to a system font instead of its bundled face");
                    if (name.EndsWith(" NF") && (!glyph.CharacterToGlyphMap.ContainsKey(0xf120) || !glyph.CharacterToGlyphMap.ContainsKey(0xf0001)))
                        throw new Exception($"{name} is missing Nerd Font terminal icons");
                }
                var view = new TerminalView { Width = 800, Height = 420, FontFamily = family, FontSize = 18, Background = Brushes.Black };
                var buffer = new TerminalBuffer(70, 16);
                Render(view, buffer.Process("Terminal 123 →\r\nسلام دنیا؛ فارسی و English\r\n\x1b[1;3mBold italic\x1b[0m" +
                    (name.EndsWith(" NF") ? "\r\nNerd icons: \ue0b0 \uf120 \U000f0001" : "")), "bundled-" + name.Replace(' ', '-') + ".png");
            }
            if (all.Items.Cast<string>().Any(BundledFonts.IsExcluded) || recommended.Items.Cast<string>().Any(BundledFonts.IsExcluded))
                throw new Exception("Removed Arabic font families remain in the picker");
            if (BundledFonts.DisplayName(BundledFonts.Resolve("Amiri")) != "Cascadia Mono")
                throw new Exception("A saved removed font must resolve to an available terminal font");
            var html = TerminalHtmlExporter.Create([], "Geist Mono", 14, 20);
            if (!html.Contains("@font-face{font-family:'Geist Mono'") || !html.Contains("data:font/ttf;base64,") || !html.Contains("font-src data:"))
                throw new Exception("HTML preview must include its private font without network access");
        }
        finally { settings.Close(); }
        Console.WriteLine("PASS nine embedded terminal fonts, private regular/bold/italic faces, Nerd icons, picker/preview, removed families and self-contained HTML font data");
    }
}
