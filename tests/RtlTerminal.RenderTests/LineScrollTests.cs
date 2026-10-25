using System.Collections;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using RtlTerminal;

internal static partial class Program
{
    private static void CheckLineScrollAndWordCache()
    {
        const BindingFlags flags = BindingFlags.NonPublic | BindingFlags.Instance;
        const double height = 25;
        var view = new TerminalView { Width = 800, Height = 420, FontFamily = new FontFamily("Consolas"), FontSize = 18, Background = Brushes.Black };
        var buffer = new TerminalBuffer(80, 16);
        var snapshot = buffer.Process(string.Join("\r\n", Enumerable.Range(0, 300).Select(i => $"row {i} سلام دنیا")));
        Render(view, snapshot, "line-scroll.png");
        void Pump() { view.UpdateLayout(); view.Dispatcher.Invoke(() => { }, DispatcherPriority.ContextIdle); }
        void Position(double row) { view.ScrollToVerticalOffset(row * height); Pump(); }
        void Wheel(int delta, int lines = 3) => typeof(TerminalView).GetMethod("QueueWheelScroll", flags)!.Invoke(view, [delta, lines]);
        void Expect(double row)
        {
            Pump();
            if (Math.Abs(view.VerticalOffset - row * height) > .01)
                throw new Exception($"Line wheel scroll expected row {row}, got {view.VerticalOffset / height}");
        }
        Position(100); Wheel(120); Expect(97);
        Wheel(-120); Expect(100);
        Position(100.25); // Direct dragging/search positioning remains pixel-based.
        if (Math.Abs(view.VerticalOffset / height - 100.25) > .01) throw new Exception("Direct scrollbar positioning was snapped");
        Wheel(120); Expect(97);
        Position(100); Wheel(60); Expect(100); Wheel(60); Expect(97);
        Position(100); Wheel(60); Wheel(-60); Expect(100);
        Position(100); Wheel(-120); Wheel(-120); Wheel(-120);
        if (Math.Abs(view.VerticalOffset - 100 * height) > .01) throw new Exception("Wheel burst should be applied in one queued update");
        Expect(109);
        Position(100); Wheel(120); Wheel(-120); Expect(100);
        Position(100); Wheel(-120, -1); Expect(100 + Math.Max(1, Math.Floor(view.ViewportHeight / height)));
        Position(100); Wheel(-120, 0); Expect(100);
        Wheel(-120); Position(20); Expect(20); // Explicit navigation cancels stale wheel work.
        Position(0); Wheel(120); Expect(0);
        Wheel(-120000); Pump();
        var scroll = (ScrollViewer)view.Content;
        if (Math.Abs(view.VerticalOffset - scroll.ScrollableHeight) > .01) throw new Exception("Wheel scrolling cannot reach the bottom");
        Position(100);
        var host = new Grid(); host.Children.Add(view);
        host.PreviewMouseWheel += (_, e) => e.Handled = true; // Application mouse reporting / parent zoom wins.
        scroll.RaiseEvent(new MouseWheelEventArgs(Mouse.PrimaryDevice, 0, -120) { RoutedEvent = Mouse.PreviewMouseWheelEvent });
        Expect(100); host.Children.Remove(view);
        Position(0);

        var words = new TerminalBuffer(80, 16);
        var textView = new TerminalView { Width = 800, Height = 420, FontFamily = new FontFamily("Consolas"), FontSize = 18, Background = Brushes.Black };
        Render(textView, words.Process("سلام دنیا\r\nprefix سلام دنیا\r\n\x1b[31mسلام\x1b[0m دنیا\r\nس\x1b[32mلام\x1b[0m دنیا"), "shared-persian-shaping.png");
        var layouts = (IDictionary)typeof(TerminalView).GetField("_layouts", flags)!.GetValue(textView)!;
        object Word(int row) => ((IEnumerable)layouts[row]!.GetType().GetProperty("Glyphs")!.GetValue(layouts[row])!)
            .Cast<object>().Single(g => (string)g.GetType().GetProperty("Text")!.GetValue(g)! == "سلام");
        object? Property(object glyph, string name) => glyph.GetType().GetProperty(name)!.GetValue(glyph);
        var first = Word(0); var repeated = Word(1); var colored = Word(2); var mixed = Word(3);
        if (!ReferenceEquals(Property(first, "Formatted"), Property(repeated, "Formatted")) ||
            !ReferenceEquals(Property(first, "HighlightBounds"), Property(repeated, "HighlightBounds")))
            throw new Exception("Repeated Persian words should share shaping and relative character bounds");
        if (ReferenceEquals(Property(first, "Formatted"), Property(colored, "Formatted")) ||
            ReferenceEquals(Property(first, "Formatted"), Property(mixed, "Formatted")))
            throw new Exception("Persian word cache mixed independent ANSI formatting");
        var cache = (IDictionary)typeof(TerminalView).GetField("_wordCache", flags)!.GetValue(textView)!;
        var oldShapes = cache.Values.Cast<object>().ToArray();
        textView.FontSize += 2;
        Render(textView, words.CaptureSnapshot(), "shared-persian-font.png");
        if (cache.Values.Cast<object>().Any(current => oldShapes.Any(old => ReferenceEquals(old, current))))
            throw new Exception("Persian shaping cache survived a font change");
        textView.RefreshTheme();
        if (cache.Count != 0) throw new Exception("Persian shaping cache survived a theme change");
        Console.WriteLine("PASS whole-line wheel steps, accumulated deltas, coalescing, Windows page/disabled settings, boundaries, parent interception and shared Persian shaping with style/font/theme isolation");
    }
}
