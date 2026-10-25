using System.Collections;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using RtlTerminal;

internal static partial class Program
{
    private static void CheckShortcutGuide()
    {
        var initial = UiText.Current;
        try
        {
            foreach (var language in new[] { UiLanguage.English, UiLanguage.Persian, UiLanguage.Arabic })
            {
                UiText.Select(language, persist: false);
                var guide = new GuideWindow();
                try
                {
                    guide.ShowShortcuts();
                    var rows = (ItemsControl)guide.FindName("ShortcutRows");
                    var search = (TextBox)guide.FindName("ShortcutSearchBox");
                    if (((TabControl)guide.FindName("GuideTabs")).SelectedIndex != 1) throw new Exception("Shortcuts deep link did not select its tab.");
                    if (((IEnumerable)rows.ItemsSource).Cast<ShortcutEntry>().Count() < 30) throw new Exception("Shortcut reference is incomplete.");
                    search.Text = "Ctrl+0";
                    if (((IEnumerable)rows.ItemsSource).Cast<ShortcutEntry>().Single().Keys != "Ctrl+0") throw new Exception("Shortcut filtering failed.");
                    search.Text = "no-such-shortcut-xyz";
                    if (((TextBlock)guide.FindName("ShortcutEmptyText")).Visibility != Visibility.Visible) throw new Exception("Missing no-results hint.");
                    search.Text = "";
                    if (language == UiLanguage.Persian && Environment.GetEnvironmentVariable("RTL_CAPTURE_SHORTCUTS") == "1") Capture(guide, "guide-shortcuts-persian.png", 780, 690);
                }
                finally { guide.Close(); }
            }
            UiText.Select(UiLanguage.English, false);
            var window = new MainWindow();
            try
            {
                var viewMenu = (MenuItem)window.FindName("ViewMenuItem");
                if (viewMenu.Items.OfType<MenuItem>().Any(m => m.Header?.ToString() is "Zoom in" or "Zoom out")) throw new Exception("View still exposes redundant zoom controls.");
                var reset = (MenuItem)window.FindName("ResetZoomMenuItem");
                var separator = (Separator)window.FindName("ResetZoomSeparator");
                if (reset.Visibility != Visibility.Collapsed) throw new Exception("Reset zoom must be hidden at baseline.");
                var flags = BindingFlags.Instance | BindingFlags.NonPublic;
                var fontSize = ((TerminalView)window.FindName("TerminalTextBox")).FontSize;
                typeof(MainWindow).GetMethod("ZoomFontSize", flags)!.Invoke(window, [fontSize >= 48 ? -1 : 1]);
                if (reset.Visibility != Visibility.Visible || separator.Visibility != Visibility.Visible) throw new Exception("Zoom reset was not revealed after zooming.");
                typeof(MainWindow).GetMethod("ResetFontSize", flags)!.Invoke(window, null);
                if (reset.Visibility != Visibility.Collapsed || separator.Visibility != Visibility.Collapsed) throw new Exception("Reset zoom must hide again after resetting.");
            }
            finally { window.Close(); }
            Console.WriteLine("PASS searchable shortcut guide in three languages and contextual Reset zoom menu");
        }
        finally { UiText.Select(initial, persist: false); }
    }
}
