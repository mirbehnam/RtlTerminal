using System.Collections;
using System.Net;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using RtlTerminal;

internal static partial class Program
{
    private static void CheckWrappedLinks()
    {
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
        const string url = "https://github.com/mirbehnam/RtlTerminal/pull/17/commits";
        var view = new TerminalView { Width = 800, Height = 420, FontFamily = new FontFamily("Consolas"), FontSize = 18, Background = Brushes.Black };
        var hit = typeof(TerminalView).GetMethod("LinkAt", flags)!;
        TerminalSnapshot Displayed() => (TerminalSnapshot)typeof(TerminalView).GetField("_snapshot", flags)!.GetValue(view)!;
        Uri? At(int row, int offset)
        {
            var layouts = (IDictionary)typeof(TerminalView).GetField("_layouts", flags)!.GetValue(view)!;
            var layout = layouts[row]!;
            var cells = ((IEnumerable)layout.GetType().GetProperty("Cells")!.GetValue(layout)!).Cast<object>();
            double Number(object c, string name) => Convert.ToDouble(c.GetType().GetProperty(name)!.GetValue(c));
            var cell = cells.First(c => offset >= Number(c, "Start") && offset < Number(c, "Start") + Number(c, "Length"));
            return (Uri?)hit.Invoke(view, [new Point(Number(cell, "X") + Number(cell, "Width") / 2, row * 25 + 12)]);
        }
        void CheckAddress(string text, string address)
        {
            var lines = Displayed().Lines;
            var logical = string.Concat(lines.Select(l => l.Text));
            var first = logical.IndexOf(text, StringComparison.Ordinal);
            if (first < 0) throw new Exception("Wrapped URL text was lost");
            var start = 0; var pieces = 0;
            for (var row = 0; row < lines.Count; row++)
            {
                var from = Math.Max(first, start); var to = Math.Min(first + text.Length, start + lines[row].Text.Length);
                if (to > from)
                {
                    pieces++;
                    if (At(row, from - start)?.AbsoluteUri != address || At(row, to - start - 1)?.AbsoluteUri != address)
                        throw new Exception($"Wrapped URL fragment on row {row} did not open the entire address");
                }
                start += lines[row].Text.Length;
            }
            if (pieces < 2) throw new Exception("Wrapped link fixture did not span multiple rows");
            var html = view.CreateHtmlPreview(HtmlPreviewScope.EntireHistory);
            if (html.Split("href=\"" + WebUtility.HtmlEncode(address) + "\"").Length - 1 < pieces)
                throw new Exception("HTML preview lost full link targets on continuation rows");
        }
        foreach (var prefix in new[] { "", new string('x', 23) + " ", "متن فارسی قبل از لینک " })
        {
            var buffer = new TerminalBuffer(25, 16);
            Render(view, buffer.Process(prefix + "\x1b[31m" + url[..45] + "\x1b[32m" + url[45..] + "\x1b[0m"), "wrapped-link.png");
            view.ScrollToVerticalOffset(0); view.UpdateLayout();
            view.Dispatcher.Invoke(() => { }, System.Windows.Threading.DispatcherPriority.ContextIdle);
            CheckAddress(url, url);
        }
        var web = new TerminalBuffer(15, 16);
        const string www = "www.example.com/long-path?q=1&x=2";
        Render(view, web.Process(www), "wrapped-www.png"); CheckAddress(www, "https://" + www);
        var growing = new TerminalBuffer(20, 16);
        Render(view, growing.Process(url[..45]), "wrapped-link-prefix.png");
        Render(view, growing.Process(url[45..]), "wrapped-link-appended.png"); CheckAddress(url, url);
        Render(view, growing.Resize(15, 16), "wrapped-link-resized.png"); CheckAddress(url, url);
        var hard = new TerminalBuffer(80, 16);
        Render(view, hard.Process("https://example.com/path\r\nsuffix"), "hard-link.png");
        if (At(0, 1)?.AbsoluteUri != "https://example.com/path" || At(1, 1) is not null)
            throw new Exception("Explicit line breaks were incorrectly joined into a link");
        var hidden = new TerminalBuffer(20, 16);
        Render(view, hidden.Process("https://example.com/\x1b[8msecret\x1b[0m"), "wrapped-hidden-link.png");
        if (At(0, 1) is not null || view.CreateHtmlPreview(HtmlPreviewScope.EntireHistory).Contains("href="))
            throw new Exception("A visible prefix exposed the concealed part of a link");
        const string label = "مستندات مایکروسافت و راهنمای ترمینال";
        var explicitLink = new TerminalBuffer(15, 16);
        Render(view, explicitLink.Process("\x1b]8;id=docs;" + url + "\x1b\\" + label[..8] +
            "\x1b[31m" + label[8..16] + "\x1b[0m" + label[16..] + "\x1b]8;;\a"), "osc8-persian.png");
        CheckAddress(label, url);
        Render(view, explicitLink.Resize(12, 16), "osc8-persian-resized.png");
        CheckAddress(label, url);
        var hover = typeof(TerminalView).GetMethod("UpdateLinkHover", flags)!;
        var surface = (FrameworkElement)typeof(TerminalView).GetField("_surface", flags)!.GetValue(view)!;
        hover.Invoke(view, [At(0, 1)]);
        var tip = (ToolTip)surface.ToolTip;
        if (tip.Content is not TextBlock address || !address.Text.StartsWith(url + "\n", StringComparison.Ordinal) ||
            address.FlowDirection != FlowDirection.LeftToRight || address.TextWrapping != TextWrapping.Wrap)
            throw new Exception("OSC 8 hover did not show the full destination in a wrapping LTR tooltip");
        hover.Invoke(view, [new Uri("https://example.org/changed")]);
        if (!((TextBlock)tip.Content).Text.StartsWith("https://example.org/changed\n", StringComparison.Ordinal))
            throw new Exception("Hover kept a stale link destination");
        hover.Invoke(view, [null]);
        if (surface.ToolTip is not null || tip.IsOpen)
            throw new Exception("Hover tooltip remained after leaving a link");
        var precedence = new TerminalBuffer(80, 16);
        Render(view, precedence.Process("\x1b]8;;" + url + "\ahttps://example.org\x1b]8;;\a plain"), "osc8-precedence.png");
        if (At(0, 1)?.AbsoluteUri != url || At(0, 22) is not null)
            throw new Exception("OSC 8 did not override an address label or leaked after closing");
        var unsupported = new TerminalBuffer(80, 16);
        Render(view, unsupported.Process("\x1b]8;;javascript:alert(1)\ahttps://example.org\x1b]8;;\a"), "osc8-inert.png");
        if (At(0, 1) is not null || view.CreateHtmlPreview(HtmlPreviewScope.EntireHistory).Contains("href="))
            throw new Exception("Unsupported OSC 8 destination became clickable");
        var concealed = new TerminalBuffer(80, 16);
        Render(view, concealed.Process("\x1b]8;;" + url + "\a\x1b[8msecret\x1b]8;;\a\x1b[0m"), "osc8-hidden.png");
        if (At(0, 1) is not null || view.CreateHtmlPreview(HtmlPreviewScope.EntireHistory).Contains("href="))
            throw new Exception("Hidden OSC 8 label exposed a target");
        Console.WriteLine("PASS OSC 8 Persian labels, style reset, wrap, resize, HTML, explicit target precedence, close and inert destinations");
        Console.WriteLine("PASS full URL hit targets across soft wraps, split schemes, Persian/ANSI text, appended tails, resize, HTML, hard breaks and hidden text");
    }
}
