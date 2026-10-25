using System.IO;
using System.Net;
using System.Reflection;
using System.Windows.Media;
using RtlTerminal;

internal static partial class Program
{
    private static void CheckPreviewAndScrollCache()
    {
        var view = new TerminalView { Width = 800, Height = 420, FontFamily = new FontFamily("Consolas"), FontSize = 18, Background = Brushes.Black };
        var buffer = new TerminalBuffer(80, 16);
        var snapshot = buffer.Process("\x1b[?25l" + string.Join("\r\n", Enumerable.Range(0, 180).Select(i => $"ROW{i:D3} متن فارسی")));
        Render(view, snapshot, "preview-range.png");
        var lineHeight = (double)typeof(TerminalView).GetField("_lineHeight", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(view)!;
        view.ScrollToVerticalOffset(90 * lineHeight);
        Render(view, snapshot, "preview-middle.png");
        var first = (int)Math.Floor(view.VerticalOffset / lineHeight);
        var last = Math.Min(snapshot.Lines.Count, (int)Math.Ceiling((view.VerticalOffset + view.ViewportHeight) / lineHeight));
        var recent = Math.Max(0, (int)Math.Floor((view.VerticalOffset - 2 * view.ViewportHeight) / lineHeight));
        foreach (var scope in Enum.GetValues<HtmlPreviewScope>())
        {
            var html = view.CreateHtmlPreview(scope);
            var start = scope == HtmlPreviewScope.CurrentPage ? first : scope == HtmlPreviewScope.RecentToEnd ? recent : 0;
            var end = scope == HtmlPreviewScope.CurrentPage ? last : snapshot.Lines.Count;
            for (var i = 0; i < 180; i++)
                if (html.Contains($"ROW{i:D3}") != (i >= start && i < end)) throw new Exception($"Preview range {scope} includes wrong row {i}");
            if (!html.Contains("id=\"terminal-end\"")) throw new Exception("Preview bottom anchor missing");
        }
        var original = RowDrawing(view, first);
        view.ScrollToVerticalOffset((first + 8) * lineHeight);
        Render(view, snapshot, "scroll-cache-away.png");
        view.ScrollToVerticalOffset(first * lineHeight);
        Render(view, snapshot, "scroll-cache-back.png");
        if (!ReferenceEquals(original, RowDrawing(view, first))) throw new Exception("Scrolling back rebuilt cached Persian text");
        var surface = typeof(TerminalView).GetField("_surface", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(view)!;
        int RowRecordings() => (int)surface.GetType().GetProperty("RowDrawCount")!.GetValue(surface)!;
        void ScrollAndPump(int row)
        {
            view.ScrollToVerticalOffset(row * lineHeight);
            view.UpdateLayout();
            view.Dispatcher.Invoke(() => { }, System.Windows.Threading.DispatcherPriority.ContextIdle);
        }
        var recordingsBefore = RowRecordings();
        for (var i = 0; i < 20; i++) { ScrollAndPump(first + 8); ScrollAndPump(first); }
        if (RowRecordings() != recordingsBefore)
            throw new Exception("Warm scroll reversals must reuse WPF row visuals without recording them again");
        ScrollAndPump(0); ScrollAndPump(170);
        var retained = (System.Collections.IDictionary)surface.GetType().GetField("_rows", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(surface)!;
        var visibleRows = (int)Math.Ceiling(view.ViewportHeight / lineHeight) + 2;
        if (retained.Count > visibleRows + 2 * Math.Max(64, 3 * visibleRows))
            throw new Exception("Scroll visual cache exceeded its viewport-relative memory bound");
        var fontHtml = WebUtility.HtmlDecode(TerminalHtmlExporter.Create([], "Cascadia Mono, Consolas", 18, 24));
        if (!fontHtml.Contains("font-family:'Cascadia Mono','Consolas','Segoe UI',Tahoma")) throw new Exception("Browser font families or Persian fallback missing");
        var directory = Path.Combine("artifacts", "render-checks", "preview-cleanup-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        var current = Path.Combine(directory, "preview-current.html");
        var older = Path.Combine(directory, "preview-old.html");
        var unrelated = Path.Combine(directory, "keep.html");
        File.WriteAllText(current, "current"); File.WriteAllText(older, "old"); File.WriteAllText(unrelated, "keep");
        TerminalHtmlExporter.DeleteOlderPreviews(directory, current);
        if (!File.Exists(current) || File.Exists(older) || !File.Exists(unrelated)) throw new Exception("Preview cleanup deleted the wrong files");
        File.Delete(current); File.Delete(unrelated); Directory.Delete(directory);
        Console.WriteLine("PASS preview ranges, font stack, bottom anchor, safe cleanup and Persian scroll cache reuse");
    }
}
