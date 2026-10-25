using System.Globalization;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;

namespace RtlTerminal;

/// <summary>A self-contained, inert, Unicode reading copy of terminal output.</summary>
public static class TerminalHtmlExporter
{
    public static string Create(IReadOnlyList<TerminalLine> lines, string fontFamily,
        double fontSize, double lineHeight, bool bold = false, bool italic = false)
    {
        static string H(string value) => WebUtility.HtmlEncode(value);
        static string Color(TerminalColor value) => $"#{value.Red:X2}{value.Green:X2}{value.Blue:X2}";
        // WPF accepts a comma-separated family list; CSS needs each family quoted separately.
        static string QuoteFont(string name) => "'" + name.Trim().Replace("\\", "\\\\").Replace("'", "\\'").Replace("\r", "").Replace("\n", "") + "'";
        var font = string.Join(",", fontFamily.Split(',').Where(name => !string.IsNullOrWhiteSpace(name)).Select(QuoteFont));
        var css = $"font-family:{font},'Segoe UI',Tahoma,Consolas,monospace;font-size:{fontSize.ToString(CultureInfo.InvariantCulture)}px;" +
            $"line-height:{lineHeight.ToString(CultureInfo.InvariantCulture)}px;font-weight:{(bold ? "bold" : "normal")};font-style:{(italic ? "italic" : "normal")};" +
            $"color:{Color(AppTheme.DefaultForeground)};background:{Color(AppTheme.DefaultBackground)}";
        var html = new StringBuilder("<!doctype html><html><head><meta charset=\"utf-8\"><meta name=\"viewport\" content=\"width=device-width,initial-scale=1\">");
        html.Append("<meta http-equiv=\"Content-Security-Policy\" content=\"default-src 'none'; style-src 'unsafe-inline'; font-src data:\">");
        var neededFaces = new HashSet<(bool Bold, bool Italic)> { (bold, italic) };
        foreach (var run in lines.SelectMany(line => line.Runs)) neededFaces.Add((bold || run.Style.Bold, italic || run.Style.Italic));
        var fontFaces = BundledFonts.FontFaceCss(fontFamily, neededFaces);
        if (fontFaces.Length > 0) html.Append("<style>").Append(fontFaces).Append("</style>");
        html.Append("<title>Rtl Terminal — Preview</title><style>body{margin:0;padding:24px}main{max-width:1200px;margin:auto}.line{white-space:pre-wrap;white-space:break-spaces;overflow-wrap:normal;word-break:normal;unicode-bidi:plaintext;min-height:1lh;margin:0}span{unicode-bidi:normal}::selection{background:#467dc8;color:white}</style></head><body style=\"");
        html.Append(H(css)).Append("\"><main>");
        var open = false;
        for (var i = 0; i < lines.Count; i++)
        {
            var line = lines[i];
            if (!open || !line.WrappedFromPrevious)
            {
                if (open) html.Append("</div>");
                html.Append("<div class=\"line\" dir=\"auto\">");
                open = true;
            }
            var lineText = string.Concat(line.Runs.Select(run => run.Text));
            var links = TerminalView.Links.Matches(lineText).Cast<Match>()
                .Select(match => (match.Index, Text: match.Value.TrimEnd('.', ',', ';', ':', '!', '?', ')')))
                .ToArray();
            var runOffset = 0;
            foreach (var run in line.Runs)
            {
                var style = run.Style;
                var (fg, bg) = TerminalView.Colors(style);
                var decoration = new List<string>();
                if (style.Underline) decoration.Add("underline");
                if (style.Strikethrough) decoration.Add("line-through");
                var runCss = $"color:{Color(fg)};background-color:{Color(bg)};" +
                    $"font-weight:{(style.Bold || bold ? "bold" : "normal")};font-style:{(style.Italic || italic ? "italic" : "normal")};" +
                    $"text-decoration:{(decoration.Count > 0 ? string.Join(" ", decoration) : "none")};" +
                    (style.Hidden ? "visibility:hidden;" : "");
                html.Append("<span style=\"").Append(H(runCss)).Append("\">");
                for (var offset = 0; offset < run.Text.Length;)
                {
                    var link = links.FirstOrDefault(item => runOffset + offset >= item.Index && runOffset + offset < item.Index + item.Text.Length);
                    if (link.Text is not null && !style.Hidden)
                    {
                        var length = Math.Min(run.Text.Length - offset, link.Index + link.Text.Length - runOffset - offset);
                        var address = link.Text.StartsWith("www.", StringComparison.OrdinalIgnoreCase) ? "https://" + link.Text : link.Text;
                        html.Append("<a style=\"color:#6495ED;text-decoration:underline\" href=\"").Append(H(address))
                            .Append("\">").Append(H(run.Text.Substring(offset, length))).Append("</a>");
                        offset += length;
                    }
                    else
                    {
                        var next = links.Where(item => item.Index > runOffset + offset).Select(item => item.Index - runOffset)
                            .DefaultIfEmpty(run.Text.Length).Min();
                        var length = Math.Min(run.Text.Length, next) - offset;
                        html.Append(H(run.Text.Substring(offset, length)));
                        offset += length;
                    }
                }
                html.Append("</span>");
                runOffset += run.Text.Length;
            }
        }
        if (open) html.Append("</div>");
        return html.Append("</main><div id=\"terminal-end\"></div></body></html>").ToString();
    }

    public static void DeleteOlderPreviews(string directory, string currentPath)
    {
        // Only this feature's own HTML exports are eligible; cleanup never blocks opening.
        try
        {
            foreach (var path in System.IO.Directory.EnumerateFiles(directory, "preview-*.html", System.IO.SearchOption.TopDirectoryOnly))
            {
                if (string.Equals(System.IO.Path.GetFullPath(path), System.IO.Path.GetFullPath(currentPath), StringComparison.OrdinalIgnoreCase)) continue;
                try { System.IO.File.Delete(path); }
                catch (System.IO.IOException) { }
                catch (UnauthorizedAccessException) { }
            }
        }
        catch (System.IO.IOException) { }
        catch (UnauthorizedAccessException) { }
    }

}
