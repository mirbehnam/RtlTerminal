using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using RtlTerminal;

internal static partial class Program
{
    private static void CheckTerminalSearchAndZoom()
    {
        const BindingFlags flags = BindingFlags.NonPublic | BindingFlags.Instance;

        // 1. Recommended Fonts Verification
        var recFontsField = typeof(FontSettingsWindow).GetField("RecommendedFontNames", BindingFlags.NonPublic | BindingFlags.Static);
        if (recFontsField?.GetValue(null) is not string[] recFonts)
            throw new Exception("FontSettingsWindow.RecommendedFontNames not found");

        string[] requiredBundledFonts =
        [
            "JetBrains Mono",
            "Cascadia Mono",
            "Fira Code",
            "Iosevka",
            "Hack",
            "Geist Mono",
            "Vazir Code",
            "Cascadia Mono NF",
            "JetBrains Mono NF"
        ];

        for (var i = 0; i < requiredBundledFonts.Length; i++)
        {
            if (recFonts.Length <= i || recFonts[i] != requiredBundledFonts[i])
                throw new Exception($"Expected bundled font '{requiredBundledFonts[i]}' at index {i}, but found '{recFonts.ElementAtOrDefault(i)}'");
        }

        if (!recFonts.Contains("Consolas") || !recFonts.Contains("Lucida Console") || !recFonts.Contains("JetBrains Mono"))
            throw new Exception("Classic terminal coding fonts missing from RecommendedFontNames");

        // 2. Localization tests for all new UI strings
        var testKeys = new[]
        {
            ("Find...", "جست‌وجو…", "بحث…"),
            ("Previous match", "تطابق قبلی", "المطابقة السابقة"),
            ("Next match", "تطابق بعدی", "المطابقة التالية"),
            ("Match case", "حساس به حروف بزرگ و کوچک", "مطابقة حالة الأحرف"),
            ("Close search", "بستن جست‌وجو", "إغلاق البحث"),
            ("No results", "نتیجه‌ای یافت نشد", "لا توجد نتائج"),
            ("Full screen", "تمام‌صفحه", "ملء الشاشة"),
            ("Zoom in", "بزرگ‌نمایی", "تكبير"),
            ("Zoom out", "کوچک‌نمایی", "تصغير"),
            ("Reset zoom", "بازنشانی بزرگ‌نمایی", "إعادة تعيين الحجم"),
            ("PowerShell 7", "پاورشل ۷", "PowerShell 7"),
            ("Git Bash", "گیت بش", "Git Bash"),
        };

        foreach (var (en, fa, ar) in testKeys)
        {
            UiText.Select(UiLanguage.English, persist: false);
            if (UiText.Get(en) != en) throw new Exception($"English translation mismatch for '{en}'");
            UiText.Select(UiLanguage.Persian, persist: false);
            if (UiText.Get(en) != fa) throw new Exception($"Persian translation mismatch for '{en}': got '{UiText.Get(en)}', expected '{fa}'");
            UiText.Select(UiLanguage.Arabic, persist: false);
            if (UiText.Get(en) != ar) throw new Exception($"Arabic translation mismatch for '{en}': got '{UiText.Get(en)}', expected '{ar}'");
        }

        // Test {0} of {1} template
        UiText.Select(UiLanguage.English, persist: false);
        if (UiText.Format("{0} of {1}", 3, 12) != "3 of 12") throw new Exception("English counter format mismatch");
        UiText.Select(UiLanguage.Persian, persist: false);
        if (UiText.Format("{0} of {1}", 3, 12) != "3 از 12") throw new Exception("Persian counter format mismatch");
        UiText.Select(UiLanguage.Arabic, persist: false);
        if (UiText.Format("{0} of {1}", 3, 12) != "3 من 12") throw new Exception("Arabic counter format mismatch");
        UiText.Select(UiLanguage.English, persist: false);

        // 3. UI and Terminal Integration Tests
        var window = new MainWindow();
        try
        {
            var searchBar = (Border)window.FindName("SearchBar");
            var searchBox = (TextBox)window.FindName("SearchTextBox");
            var searchCounter = (TextBlock)window.FindName("SearchCounterText");
            var caseButton = (ToggleButton)window.FindName("SearchCaseSensitiveButton");
            var prevButton = (Button)window.FindName("SearchPreviousButton");
            var nextButton = (Button)window.FindName("SearchNextButton");
            var closeButton = (Button)window.FindName("SearchCloseButton");
            var placeholder = (TextBlock)window.FindName("SearchPlaceholderText");
            var view = (TerminalView)window.FindName("TerminalTextBox");

            if (searchBar.Visibility != Visibility.Collapsed)
                throw new Exception("SearchBar should initially be collapsed");

            // Setup buffer with sample text (both English and Arabic/Persian)
            var buffer = new TerminalBuffer(80, 24);
            var snapshot = buffer.Process(
                "Line 0: First hello world\r\n" +
                "Line 1: سلام دنیا و آزمایش ترمینال\r\n" +
                "Line 2: Another hello occurrence in terminal\r\n" +
                "Line 3: متنی با سلام دوم برای جستجو\r\n" +
                "Line 4: HELLO uppercase test\r\n" +
                "Line 5: Final line without match");

            view.Present(snapshot, smartRtl: true, cellWidth: 9, lineHeight: 18, followOutput: false);

            var tabs = (IList)typeof(MainWindow).GetField("_tabs", flags)!.GetValue(window)!;
            var tabType = typeof(MainWindow).GetNestedType("TerminalTab", BindingFlags.NonPublic)!;
            var profile = typeof(MainWindow).GetNestedType("TerminalProfile", BindingFlags.NonPublic)!;
            object? Call(string name, params object?[] args) => typeof(MainWindow).GetMethods(flags).Single(m => m.Name == name && m.GetParameters().Length == args.Length).Invoke(window, args);

            var pane = Call("CreatePane", Enum.ToObject(profile, 0))!;
            pane.GetType().GetProperty("LastRenderedSnapshot")!.SetValue(pane, snapshot);
            pane.GetType().GetProperty("Buffer")!.SetValue(pane, buffer);
            var tab = Activator.CreateInstance(tabType, pane)!;
            tabs.Add(tab);
            Call("ShowTab", tab);

            // Test OpenSearchBar via Ctrl+F simulation
            Call("OpenSearchBar");
            if (searchBar.Visibility != Visibility.Visible)
                throw new Exception("SearchBar did not open");

            // Search for "hello" (case-insensitive by default)
            searchBox.Text = "hello";
            // Matches: Line 0 (start 8), Line 2 (start 8), Line 4 (start 8) -> 3 matches
            if (searchCounter.Text != "1 of 3")
                throw new Exception($"Expected '1 of 3' matches for 'hello', got '{searchCounter.Text}'");

            if (view.SelectionState.Anchor != (0, 14) || view.SelectionState.End != (0, 19))
                throw new Exception($"First match selection mismatch: got {view.SelectionState}");

            // Next match
            Call("FindNext");
            if (searchCounter.Text != "2 of 3")
                throw new Exception($"Expected '2 of 3' matches, got '{searchCounter.Text}'");
            if (view.SelectionState.Anchor != (2, 16) || view.SelectionState.End != (2, 21))
                throw new Exception($"Second match selection mismatch: got {view.SelectionState}");

            // Next match again
            Call("FindNext");
            if (searchCounter.Text != "3 of 3")
                throw new Exception($"Expected '3 of 3' matches, got '{searchCounter.Text}'");
            if (view.SelectionState.Anchor != (4, 8) || view.SelectionState.End != (4, 13))
                throw new Exception($"Third match selection mismatch: got {view.SelectionState}");

            // Next wrap-around
            Call("FindNext");
            if (searchCounter.Text != "1 of 3")
                throw new Exception($"Expected wrap-around to '1 of 3', got '{searchCounter.Text}'");
            if (view.SelectionState.Anchor != (0, 14))
                throw new Exception("Wrap-around failed to select first match");

            // Previous match wrap-around
            Call("FindPrevious");
            if (searchCounter.Text != "3 of 3")
                throw new Exception($"Expected previous wrap-around to '3 of 3', got '{searchCounter.Text}'");

            // Test Case Sensitivity (Aa button)
            caseButton.IsChecked = true;
            Call("SearchCaseSensitiveButton_Click", caseButton, new RoutedEventArgs());
            // With case sensitivity ON, "hello" should match 2 (Line 0, Line 2), but not Line 4 ("HELLO")
            if (searchCounter.Text != "1 of 2")
                throw new Exception($"Expected '1 of 2' case-sensitive matches for 'hello', got '{searchCounter.Text}'");

            caseButton.IsChecked = false;
            Call("SearchCaseSensitiveButton_Click", caseButton, new RoutedEventArgs());
            if (searchCounter.Text != "1 of 3")
                throw new Exception($"Expected '1 of 3' after turning off case-sensitivity, got '{searchCounter.Text}'");

            // Test Arabic / Persian search
            searchBox.Text = "سلام";
            // Matches: Line 1 (start 0), Line 3 (start 8) -> 2 matches
            if (searchCounter.Text != "1 of 2")
                throw new Exception($"Expected '1 of 2' matches for Arabic 'سلام', got '{searchCounter.Text}'");
            if (view.SelectionState.Anchor != (1, 8) || view.SelectionState.End != (1, 12))
                throw new Exception($"Arabic match selection mismatch: got {view.SelectionState}");

            Call("FindNext");
            if (searchCounter.Text != "2 of 2")
                throw new Exception($"Expected '2 of 2' matches for Arabic 'سلام', got '{searchCounter.Text}'");
            if (view.SelectionState.Anchor != (3, 16) || view.SelectionState.End != (3, 20))
                throw new Exception($"Arabic second match selection mismatch: got {view.SelectionState}");

            // Test No results
            searchBox.Text = "nonexistent_string_12345";
            if (searchCounter.Text != "No results")
                throw new Exception($"Expected 'No results', got '{searchCounter.Text}'");
            if (prevButton.IsEnabled || nextButton.IsEnabled)
                throw new Exception("Previous/Next buttons should be disabled when no matches");

            // Test Pre-fill from selection
            view.SelectionState = ((1, 8), (1, 12)); // "سلام"
            Call("OpenSearchBar");
            if (searchBox.Text != "سلام")
                throw new Exception($"Search box was not pre-filled with selection, got '{searchBox.Text}'");

            // Test Close search bar
            Call("CloseSearchBar");
            if (searchBar.Visibility != Visibility.Collapsed)
                throw new Exception("SearchBar did not collapse on close");

            // Test Tab switch closes search bar
            Call("OpenSearchBar");
            if (searchBar.Visibility != Visibility.Visible)
                throw new Exception("SearchBar failed to re-open");

            var secondPane = Call("CreatePane", Enum.ToObject(profile, 0))!;
            var secondTab = Activator.CreateInstance(tabType, secondPane)!;
            tabs.Add(secondTab);
            Call("ShowTab", secondTab);
            if (searchBar.Visibility != Visibility.Collapsed)
                throw new Exception("SearchBar did not hide when active tab changed");

            // 4. Font Zoom Tests
            var initialSize = view.FontSize;

            // Zoom in (+1)
            Call("ZoomFontSize", 1);
            if (Math.Abs(view.FontSize - (initialSize + 1)) > 0.01)
                throw new Exception($"Zoom in failed: expected {initialSize + 1}, got {view.FontSize}");

            // Zoom out (-1)
            Call("ZoomFontSize", -1);
            if (Math.Abs(view.FontSize - initialSize) > 0.01)
                throw new Exception($"Zoom out failed: expected {initialSize}, got {view.FontSize}");

            // Clamping at 48
            Call("ZoomFontSize", 100);
            if (view.FontSize > 48)
                throw new Exception($"Zoom in upper clamp failed: got {view.FontSize}");

            // Clamping at 8
            Call("ZoomFontSize", -100);
            if (view.FontSize < 8)
                throw new Exception($"Zoom out lower clamp failed: got {view.FontSize}");

            // Reset zoom
            Call("ResetFontSize");
            var defaultSaved = AppSettings.LoadFont()?.Size ?? 14.0;
            if (Math.Abs(view.FontSize - defaultSaved) > 0.01)
                throw new Exception($"Reset zoom failed: expected {defaultSaved}, got {view.FontSize}");

            // 5. Fullscreen Toggle (F11)
            var prevWindowState = window.WindowState;
            var prevWindowStyle = window.WindowStyle;
            var prevResizeMode = window.ResizeMode;
            var prevChrome = System.Windows.Shell.WindowChrome.GetWindowChrome(window);

            Call("ToggleFullScreen");
            window.Dispatcher.Invoke(() => { }, System.Windows.Threading.DispatcherPriority.ContextIdle);
            if (!window.IsFullScreen || window.WindowState != WindowState.Maximized || window.WindowStyle != WindowStyle.None || window.ResizeMode != ResizeMode.NoResize)
                throw new Exception("Fullscreen did not apply borderless maximized state");
            if (((FrameworkElement)window.FindName("TitleBarArea")).Visibility != Visibility.Collapsed ||
                ((FrameworkElement)window.FindName("MenuBarArea")).Visibility != Visibility.Collapsed ||
                ((Border)window.FindName("WindowFrame")).Padding != new Thickness(0) ||
                System.Windows.Shell.WindowChrome.GetWindowChrome(window) is not null || window.Topmost)
                throw new Exception($"Fullscreen chrome mismatch: title={((FrameworkElement)window.FindName("TitleBarArea")).Visibility}, menu={((FrameworkElement)window.FindName("MenuBarArea")).Visibility}, padding={((Border)window.FindName("WindowFrame")).Padding}");

            Call("ToggleFullScreen");
            window.Dispatcher.Invoke(() => { }, System.Windows.Threading.DispatcherPriority.ContextIdle);
            if (window.IsFullScreen || window.WindowState != prevWindowState || window.WindowStyle != prevWindowStyle || window.ResizeMode != prevResizeMode ||
                !ReferenceEquals(prevChrome, System.Windows.Shell.WindowChrome.GetWindowChrome(window)) ||
                ((FrameworkElement)window.FindName("TitleBarArea")).Visibility != Visibility.Visible ||
                ((FrameworkElement)window.FindName("MenuBarArea")).Visibility != Visibility.Visible)
                throw new Exception("Fullscreen toggle did not restore previous window state/style");

            Console.WriteLine("PASS terminal search bar navigation, RTL/Arabic search, case sensitivity, font zoom/reset, fullscreen toggle and bundled terminal font recommendations");
        }
        finally
        {
            window.Close();
        }
    }
}
