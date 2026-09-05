using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using RtlTerminal;

internal static class Program
{
    [STAThread]
    private static void Main()
    {
        if (Environment.GetCommandLineArgs().Contains("--window-smoke"))
        {
            WindowStateSmoke.Run();
            return;
        }
        var view = new TerminalView
        {
            Width = 800, Height = 420, FontFamily = new FontFamily("Consolas"),
            FontSize = 18, Background = Brushes.Black
        };
        var buffer = new TerminalBuffer(70, 16);
        var snapshot = buffer.Process("╔══════════════════════════════════╗\r\n" +
            "║ OpenCode | Codex | Claude        ║\r\n" +
            "╚══════════════════════════════════╝\r\n" +
            "status: سلام دنیا 123\r\n" +
            "متن فارسی با English و اعداد ۱۲۳\r\n" +
            "\x1b[31mقرمز\x1b[32m سبز\x1b[0m\r\n" +
            "\x1b[44m      \x1b[0m colored spaces\r\n" +
            "👨‍💻 🇮🇷 emoji / https://github.com\r\n" +
            "\x1b[1;3;4mBold italic underline\x1b[0m\r\n" +
            "\x1b[9mStrike\x1b[0m \x1b[7mInverse\x1b[0m");
        Render(view, snapshot, "normal.png");
        var stableDrawing = RowDrawing(view, 3);
        Render(view, buffer.Process(""), "unchanged.png");
        if (!ReferenceEquals(stableDrawing, RowDrawing(view, 3)))
            throw new Exception("Unchanged screen rows were rebuilt");
        var changedLines = snapshot.Lines.ToArray();
        changedLines[3] = changedLines[3] with
        {
            Runs = changedLines[3].Runs.Select(run => run with
            { Style = run.Style with { Underline = true } }).ToArray()
        };
        Render(view, snapshot with { Lines = changedLines }, "style-change.png");
        if (ReferenceEquals(stableDrawing, RowDrawing(view, 3)))
            throw new Exception("Style-only updates did not invalidate row drawing");
        Render(view, snapshot, "normal.png");
        view.SelectAll();
        if (!view.HasSelection) throw new Exception("Select all failed");
        if (!view.GetSelectedText().Contains("status: سلام دنیا 123"))
            throw new Exception("RTL selection did not preserve logical Unicode order");
        Render(view, snapshot, "selected.png");
        view.ClearSelection();
        Render(view, buffer.Process("\x1b[?1049h" +
            "┌──────────────────────────────────────┐\r\n" +
            "│ Hello سلام دنیا                      │\r\n" +
            "└──────────────────────────────────────┘"), "alternate.png");
        Render(view, buffer.Process("\x1b[?1049l"), "restored.png");
        for (int i = 0; i < 5000; i++) snapshot = buffer.Process($"\r\nline {i}");
        Render(view, snapshot, "scrollback.png");
        view.ScrollToVerticalOffset(20000);
        Render(view, snapshot, "scrolled.png");
        if (view.VerticalOffset <= 0) throw new Exception("History scrolling failed");
        Console.WriteLine("PASS renderer layout, RTL fixtures, selection, alternate screen and scrollback smoke tests");
        Console.WriteLine("PASS unchanged-row drawing reuse and style-only cache invalidation");
        CheckGraphicsAndRedraw();
        CheckWindowHeader();
        CheckLinksAndMenu();
    }
    private static object RowDrawing(TerminalView view, int row)
    {
        var layouts = (System.Collections.IDictionary)typeof(TerminalView)
            .GetField("_layouts", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!
            .GetValue(view)!;
        var layout = layouts[row] ?? throw new Exception("Visible row not cached");
        return layout.GetType().GetProperty("Drawing")!.GetValue(layout)
            ?? throw new Exception("Row drawing not cached");
    }
    private static void CheckLinksAndMenu()
    {
        const System.Reflection.BindingFlags flags = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
        var window = new MainWindow();
        var view = (TerminalView)window.FindName("TerminalTextBox");
        view.Width = 800; view.Height = 420; view.Padding = new Thickness(0);
        var buffer = new TerminalBuffer(70, 16);
        Render(view, buffer.Process("https://example.com  \x1b[8mhttps://hidden.test\x1b[0m"), "links.png");
        var hit = typeof(TerminalView).GetMethod("LinkAt", flags)!;
        if (hit.Invoke(view, new object[] { new Point(2, 12) }) is not Uri)
            throw new Exception("Link first cell was not detected");
        if (hit.Invoke(view, new object[] { new Point(210, 12) }) is not null ||
            hit.Invoke(view, new object[] { new Point(235, 12) }) is not null)
            throw new Exception("Blank or hidden text was detected as a link");
        var factory = typeof(MainWindow).GetMethod("CreateTerminalContextMenu", flags)!;
        var menu = (System.Windows.Controls.ContextMenu)factory.Invoke(window, null)!;
        if (menu.Items.Count != 4 || ((System.Windows.Controls.MenuItem)menu.Items[0]).IsEnabled)
            throw new Exception("Context menu copy state is incorrect without selection");
        view.SelectAll();
        menu = (System.Windows.Controls.ContextMenu)factory.Invoke(window, null)!;
        if (!((System.Windows.Controls.MenuItem)menu.Items[0]).IsEnabled)
            throw new Exception("Context menu copy is disabled with selected text");
        var guide = new GuideWindow();
        if (!((System.Windows.Controls.TextBlock)guide.FindName("DirectionText")).Text.Contains("Smart RTL"))
            throw new Exception("Guide does not describe current direction setting");
        Console.WriteLine("PASS link hit testing, hidden-link exclusion, context-menu selection state and guide");
    }

    private static void CheckWindowHeader()
    {
        // Instantiate chrome without showing a window or starting any shell.
        var window = new MainWindow();
        const System.Reflection.BindingFlags flags = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
        var tabs = (System.Collections.IList)typeof(MainWindow).GetField("_tabs", flags)!.GetValue(window)!;
        var tabType = typeof(MainWindow).GetNestedType("TerminalTab", System.Reflection.BindingFlags.NonPublic)!;
        var profileType = typeof(MainWindow).GetNestedType("TerminalProfile", System.Reflection.BindingFlags.NonPublic)!;
        foreach (var count in new[] { 3, 8 })
        {
            tabs.Clear();
            for (var i = 0; i < count; i++)
                tabs.Add(Activator.CreateInstance(tabType, i + 1, Enum.ToObject(profileType, i % 3),
                    i % 3 == 0 ? "Command Prompt" : i % 3 == 1 ? "PowerShell" : "WSL"));
            typeof(MainWindow).GetField("_activeTab", flags)!.SetValue(window, tabs[count - 1]);
            typeof(MainWindow).GetMethod("RebuildTabStrip", flags)!.Invoke(window, null);
            var root = (FrameworkElement)window.Content;
            var width = count == 3 ? 980 : 480;
            root.Measure(new Size(width, 620)); root.Arrange(new Rect(0, 0, width, 620)); root.UpdateLayout();
            root.Dispatcher.Invoke(() => { }, DispatcherPriority.Loaded);
            root.UpdateLayout();
            var add = (FrameworkElement)window.FindName("NewTabButton");
            var location = add.TranslatePoint(new Point(), root);
            if (location.X < 0 || location.X + add.ActualWidth > width - 138)
                throw new Exception("New-tab control is hidden by overflow or caption buttons");
            var scroller = (System.Windows.Controls.ScrollViewer)window.FindName("TabScroller");
            if (count == 8 && scroller.ScrollableWidth <= 0) throw new Exception("Tab overflow is not scrollable");
            var bitmap = new RenderTargetBitmap(width, 80, 96, 96, PixelFormats.Pbgra32);
            bitmap.Render(root);
            var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap));
            using var stream = File.Create($"bin/render-checks/header-{width}.png"); encoder.Save(stream);
        }
        Console.WriteLine("PASS window header layout and persistent new-tab control with tab overflow");
    }
    private static void CheckGraphicsAndRedraw()
    {
        var view = new TerminalView
        {
            Width = 800, Height = 420, FontFamily = new FontFamily("Consolas"),
            FontSize = 18, Background = Brushes.Black
        };
        foreach (var dpi in new[] { 96.0, 120.0, 144.0 })
        {
            var blocks = new TerminalBuffer(70, 16);
            var snapshot = blocks.Process("\x1b[?25l\x1b[38;2;255;0;0m" +
                new string('█', 20) + "\r\n" + new string('█', 20) +
                "\r\n▀▄▌▐▖▗▘▙▚▛▜▝▞▟░▒▓");
            var bitmap = Render(view, snapshot, $"blocks-{dpi}.png", dpi);
            var stride = bitmap.PixelWidth * 4;
            var pixels = new byte[stride * bitmap.PixelHeight];
            bitmap.CopyPixels(pixels, stride, 0);
            // Check cell boundaries as well as interiors, including the boundary between rows.
            for (var y = 1; y < (int)(49 * dpi / 96); y++)
                for (var x = 1; x < (int)(215 * dpi / 96); x++)
                    if (pixels[y * stride + x * 4 + 2] != 255 || pixels[y * stride + x * 4] != 0)
                        throw new Exception($"Block seam at {x},{y}, DPI {dpi}");
        }
        var status = new TerminalBuffer(70, 16);
        Render(view, status.Process("\x1b[?25lWWorking (0s - esc to interrupt)\r\nold footer"), "status-before.png");
        var updated = status.Process("\x1b[1A\r\x1b[2KWorking 3\x1b[1B\r\x1b[2Knew footer");
        var incremental = Render(view, updated, "status-after.png");
        var widthBefore = view.ViewportWidth;
        view.Clear();
        var fresh = Render(view, updated, "status-fresh.png");
        var first = new byte[incremental.PixelWidth * incremental.PixelHeight * 4];
        var second = new byte[first.Length];
        incremental.CopyPixels(first, incremental.PixelWidth * 4, 0);
        fresh.CopyPixels(second, fresh.PixelWidth * 4, 0);
        if (!first.SequenceEqual(second)) throw new Exception("Incremental redraw retained stale pixels");
        for (var i = 0; i < 50; i++) updated = status.Process("\r\nnext line");
        Render(view, updated, "stable-gutter.png");
        if (view.ViewportWidth != widthBefore) throw new Exception("History changed the terminal viewport width");
        Console.WriteLine("PASS seamless blocks at 96/120/144 DPI, stale-pixel redraw and stable viewport width");
    }

    private static RenderTargetBitmap Render(TerminalView view, TerminalSnapshot snapshot, string name, double dpi = 96)
    {
        view.Present(snapshot, true, 10.8, 25, false);
        view.Measure(new Size(800, 420)); view.Arrange(new Rect(0, 0, 800, 420));
        view.UpdateLayout();
        view.Dispatcher.Invoke(() => { }, DispatcherPriority.Render);
        var bitmap = new RenderTargetBitmap((int)(800 * dpi / 96), (int)(420 * dpi / 96), dpi, dpi, PixelFormats.Pbgra32);
        bitmap.Render(view);
        var pixels = new byte[bitmap.PixelWidth * bitmap.PixelHeight * 4]; bitmap.CopyPixels(pixels, bitmap.PixelWidth * 4, 0);
        if (!pixels.Where((_, i) => i % 4 != 3).Any(value => value > 40))
            throw new Exception("Renderer produced an empty image");
        Directory.CreateDirectory("bin/render-checks");
        var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using var stream = File.Create("bin/render-checks/" + name); encoder.Save(stream);
        return bitmap;
    }
}
