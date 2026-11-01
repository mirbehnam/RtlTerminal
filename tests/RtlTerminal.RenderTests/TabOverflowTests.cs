using System.Collections;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using RtlTerminal;

internal static partial class Program
{
    private static void CheckTabOverflow()
    {
        static void Check(bool value, string message) { if (!value) throw new Exception(message); }
        if (Application.Current == null) _ = new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
        UiText.Select(UiLanguage.English, persist: false); AppTheme.Apply(AppThemeKind.Dark);
        var window = new MainWindow();
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
        var tabs = (IList)typeof(MainWindow).GetField("_tabs", flags)!.GetValue(window)!;
        var tabType = typeof(MainWindow).GetNestedType("TerminalTab", BindingFlags.NonPublic)!;
        var paneType = typeof(MainWindow).GetNestedType("TerminalPane", BindingFlags.NonPublic)!;
        var profileType = typeof(MainWindow).GetNestedType("TerminalProfile", BindingFlags.NonPublic)!;
        var root = (FrameworkElement)window.Content;
        var scroller = (ScrollViewer)window.FindName("TabScroller");
        var strip = (StackPanel)window.FindName("TabStrip");
        var left = (Button)window.FindName("ScrollTabsLeftButton"); var right = (Button)window.FindName("ScrollTabsRightButton");
        var active = typeof(MainWindow).GetField("_activeTab", flags)!;
        void Rebuild() => typeof(MainWindow).GetMethod("RebuildTabStrip", flags)!.Invoke(window, null);
        void Layout(double width)
        {
            root.Measure(new Size(width, 620)); root.Arrange(new Rect(0, 0, width, 620));
            for (var i = 0; i < 3; i++) { root.UpdateLayout(); root.Dispatcher.Invoke(() => { }, DispatcherPriority.ApplicationIdle); }
        }
        void Fill(params string[] titles)
        {
            tabs.Clear();
            for (var i = 0; i < titles.Length; i++) tabs.Add(Activator.CreateInstance(tabType, Activator.CreateInstance(paneType, i + 1, Enum.ToObject(profileType, 0), titles[i])));
            active.SetValue(window, tabs[0]); Rebuild();
        }
        void Click(Button button, double width) { button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent)); Layout(width); }
        Fill("A", "B", "C", "D", "E", "F"); Layout(980);
        Check(left.Visibility == Visibility.Collapsed && right.Visibility == Visibility.Collapsed, "Arrows appeared before short titles needed overflow");
        Check(strip.Children.OfType<Grid>().All(g => g.Width < 248 && g.Width >= 80), "Tab widths did not shrink first");
        Check(scroller.ScrollableWidth <= .5, $"Short tabs are clipped without arrows: extent {scroller.ExtentWidth}, viewport {scroller.ViewportWidth}, frame widths {string.Join(",", strip.Children.OfType<Grid>().Select(g => g.Width))}");
        Fill(Enumerable.Range(1, 12).Select(i => i % 2 == 0 ? "پاورشل شماره " + i : "PowerShell " + i).ToArray()); Layout(700);
        var selected = active.GetValue(window);
        Check(left.Visibility == Visibility.Visible && right.Visibility == Visibility.Visible && !left.IsEnabled && right.IsEnabled, "Overflow arrows have wrong initial states");
        Check(scroller.TranslatePoint(new Point(), root).X - (left.TranslatePoint(new Point(), root).X + left.ActualWidth) >= 5.9
            && right.TranslatePoint(new Point(), root).X - (scroller.TranslatePoint(new Point(), root).X + scroller.ActualWidth) >= 5.9, "Arrow padding is too close to tab edges");
        var iterations = 0;
        while (right.IsEnabled && iterations++ < 30)
        {
            var before = scroller.HorizontalOffset; Click(right, 700);
            Check(scroller.HorizontalOffset > before + .1, "Right arrow did not reveal a hidden tab");
            Check(ReferenceEquals(active.GetValue(window), selected), "Scrolling selected another terminal");
        }
        Check(iterations < 30 && left.IsEnabled && !right.IsEnabled && Math.Abs(scroller.HorizontalOffset - scroller.ScrollableWidth) <= .5, "Last tab cannot be reached");
        var endOffset = scroller.HorizontalOffset; Rebuild(); Layout(700);
        Check(Math.Abs(scroller.HorizontalOffset - endOffset) < .5, "Unrelated rebuild jumped to the selected tab");
        iterations = 0;
        while (left.IsEnabled && iterations++ < 30) Click(left, 700);
        Check(iterations < 30 && !left.IsEnabled && right.IsEnabled && scroller.HorizontalOffset <= .5, "First tab cannot be reached");
        scroller.ScrollToHorizontalOffset(2); Layout(700); Check(left.IsEnabled, "Partial hidden tab did not enable left arrow");
        active.SetValue(window, tabs[tabs.Count - 1]); Rebuild(); Layout(700);
        Check(scroller.HorizontalOffset > 0 && !right.IsEnabled, "Selecting a hidden last tab did not reveal it");
        typeof(MainWindow).GetMethod("BeginTabRename", flags)!.Invoke(window, new[] { tabs[tabs.Count - 1] }); Layout(700);
        var editorField = typeof(MainWindow).GetField("_tabRenameEditor", flags)!; var editor = editorField.GetValue(window);
        Layout(760); Check(editor != null && ReferenceEquals(editorField.GetValue(window), editor), "Resize recreated the rename editor");
        typeof(MainWindow).GetMethod("FinishTabRename", flags)!.Invoke(window, new object[] { false, false, true }); Layout(700);
        Fill("A very long terminal title " + new string('W', 150), "کوتاه"); Layout(980);
        Check(strip.Children.OfType<Grid>().All(g => g.Width <= 248.01) && left.Visibility == Visibility.Collapsed, "Long title exceeded the width cap or unnecessarily enabled arrows");
        var badge = (FrameworkElement)window.FindName("AdministratorBadge"); badge.Visibility = Visibility.Visible;
        Fill(Enumerable.Range(1, 10).Select(i => "PowerShell " + i).ToArray()); Layout(480);
        var add = (FrameworkElement)window.FindName("NewTabButton"); var addPosition = add.TranslatePoint(new Point(), root);
        Check(addPosition.X + add.ActualWidth <= 480 - 138 && right.TranslatePoint(new Point(), root).X < addPosition.X, "Overflow hid new-terminal controls or misplaced the right arrow");
        foreach (var theme in new[] { AppThemeKind.Dark, AppThemeKind.Light })
        {
            AppTheme.Apply(theme); Rebuild(); Layout(700);
            foreach (var dpi in new[] { 96, 144 })
            {
                var bitmap = new RenderTargetBitmap(700 * dpi / 96, 80 * dpi / 96, dpi, dpi, PixelFormats.Pbgra32); bitmap.Render(root);
                var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap));
                using var file = System.IO.File.Create($"artifacts/render-checks/tab-overflow-{theme}-{dpi}.png"); encoder.Save(file);
            }
        }
        Fill("A", "B"); Layout(980);
        Check(left.Visibility == Visibility.Collapsed && right.Visibility == Visibility.Collapsed && scroller.HorizontalOffset <= .5, "Removing tabs or widening left stale arrows/offset");
        window.Close();
        Console.WriteLine("PASS shrink-before-overflow, both scroll boundaries, partial tabs, unchanged selection, rebuild anchor, selected-tab reveal, rename identity, long/Persian titles, admin/new controls and light/dark DPI previews");
    }
}
