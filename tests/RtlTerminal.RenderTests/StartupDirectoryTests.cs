using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;
using RtlTerminal;

internal static partial class Program
{
    private static void CheckStartupDirectories()
    {
        var root = Path.Combine(Path.GetTempPath(), "rtl-startup-" + Guid.NewGuid().ToString("N"));
        var app = Path.Combine(root, "Rtl Terminal");
        var child = Path.Combine(app, "ConPty");
        var sibling = Path.Combine(root, "Rtl Terminal Projects");
        var home = Path.Combine(root, "User");
        foreach (var directory in new[] { app, child, sibling, home }) Directory.CreateDirectory(directory);
        try
        {
            void Expect(string expected, string? requested, string? argument, string cwd)
            {
                var actual = StartupDirectories.Resolve(requested, argument, cwd, app, home);
                if (actual != expected) throw new Exception($"Startup directory: expected '{expected}', got '{actual}'.");
            }
            Expect(home, null, null, app);
            Expect(home, null, null, child);
            Expect(sibling, null, null, sibling);
            Expect(app, app, null, home); // Explicit folder choices must win, including the installation itself.
            Expect(sibling, null, sibling, app);
            Expect(home, Path.Combine(root, "missing"), null, app);
            Expect(home, null, null, Path.Combine(root, "missing"));
            if (StartupDirectories.ResolveLaunchDirectory(null, app, app, home) is not null ||
                StartupDirectories.ResolveLaunchDirectory(null, home, app, home) is not null ||
                StartupDirectories.ResolveLaunchDirectory(null, sibling, app, home) != sibling ||
                StartupDirectories.ResolveLaunchDirectory(home, sibling, app, home) != home)
                throw new Exception("A normal executable launch and an explicit folder launch were not distinguished.");
            CheckStartupPicker(home, child, sibling);
        }
        finally { Directory.Delete(root, recursive: true); }
    }

    private static void CheckStartupPicker(string home, string older, string newer)
    {
        if (Application.Current is null) _ = new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
        AppTheme.Apply(AppThemeKind.Dark);
        var registry = @"Software\RtlTerminal.Tests\StartupPicker-" + Guid.NewGuid().ToString("N");
        var history = new DirectoryHistoryStore(registry);
        history.Visit(older, "PowerShell", null);
        history.Visit(newer, "CommandPrompt", null);
        history.Visit(home, "CommandPrompt", null);
        var window = new MainWindow(history);
        try
        {
            var show = typeof(MainWindow).GetMethod("ShowStartupDirectoryPicker", BindingFlags.NonPublic | BindingFlags.Instance)!;
            show.Invoke(window, [home]);
            var panel = (FrameworkElement)window.FindName("StartupDirectoryPicker");
            var list = (ItemsControl)window.FindName("StartupRecentDirectories");
            var stay = (Button)window.FindName("StartupStayButton");
            var root = (FrameworkElement)window.Content;
            root.Measure(new Size(window.MinWidth, window.MinHeight));
            root.Arrange(new Rect(0, 0, window.MinWidth, window.MinHeight));
            root.UpdateLayout();
            if (panel.Visibility != Visibility.Visible || panel.HorizontalAlignment != HorizontalAlignment.Right ||
                list.Parent is not ScrollViewer { ActualHeight: > 20 } scroll ||
                Grid.GetRow(stay) >= Grid.GetRow(scroll) || list.Items.Count != 2 ||
                (string?)list.Items[0].GetType().GetProperty("Path")!.GetValue(list.Items[0]) != newer)
                throw new Exception("The startup chooser must be on the right and show Stay before the most recent projects.");
            history.SetEnabled(false);
            show.Invoke(window, [home]);
            if (list.Items.Count != 0 || stay.Visibility != Visibility.Visible)
                throw new Exception("The chooser exposed disabled history or removed the Stay action.");
            var tabs = (System.Collections.IList)typeof(MainWindow).GetField("_tabs", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(window)!;
            stay.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            if (panel.Visibility != Visibility.Collapsed || tabs.Count != 0)
                throw new Exception("Stay did not dismiss the chooser without opening a duplicate tab.");
            show.Invoke(window, [home]);
            ((Button)window.FindName("StartupPickerCloseButton")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            if (panel.Visibility != Visibility.Collapsed || tabs.Count != 0)
                throw new Exception("The close button did not dismiss the chooser.");
            show.Invoke(window, [home]);
            var area = (FrameworkElement)window.FindName("TerminalArea");
            area.RaiseEvent(new System.Windows.Input.MouseButtonEventArgs(System.Windows.Input.Mouse.PrimaryDevice, 0, System.Windows.Input.MouseButton.Left)
            { RoutedEvent = System.Windows.Input.Mouse.PreviewMouseDownEvent });
            if (panel.Visibility != Visibility.Collapsed || tabs.Count != 0)
                throw new Exception("Clicking outside did not dismiss the chooser.");
            Console.WriteLine("PASS startup path precedence, recent order, disabled history and all chooser dismissal routes");
        }
        finally
        {
            window.Close();
            Registry.CurrentUser.DeleteSubKeyTree(registry, false);
        }
    }
}
