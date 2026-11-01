using Microsoft.Win32;
using System.Collections;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;
using RtlTerminal;

internal static partial class Program
{
    private static void CheckTabRename(bool live = false)
    {
        var registry = @"Software\RtlTerminal.Tests\Rename-" + Guid.NewGuid().ToString("N");
        var window = new MainWindow(new DirectoryHistoryStore(registry));
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
        object? Call(string method, params object?[] args) => typeof(MainWindow).GetMethod(method, flags)!.Invoke(window, args);
        var tabs = (IList)typeof(MainWindow).GetField("_tabs", flags)!.GetValue(window)!;
        var profile = typeof(MainWindow).GetNestedType("TerminalProfile", BindingFlags.NonPublic)!;
        var tabType = typeof(MainWindow).GetNestedType("TerminalTab", BindingFlags.NonPublic)!;
        var root = (FrameworkElement)window.Content;
        void Pump()
        {
            root.Measure(new Size(980,620)); root.Arrange(new Rect(0,0,980,620)); root.UpdateLayout();
            window.Dispatcher.Invoke(() => { }, DispatcherPriority.ContextIdle);
        }
        try
        {
            for (var i = 0; i < 2; i++)
            {
                var pane = Call("CreatePane", Enum.ToObject(profile, i))!;
                tabs.Add(Activator.CreateInstance(tabType, pane)!);
            }
            Call("ShowTab", tabs[0]); Pump();
            if (live)
            {
                var loaded = typeof(MainWindow).GetMethod("Window_Loaded", flags)!;
                window.Loaded -= (RoutedEventHandler)loaded.CreateDelegate(typeof(RoutedEventHandler), window);
                window.Show(); window.Activate(); Pump();
            }
            var firstTitle = window.Title;
            Call("BeginTabRename", tabs[0]); Pump();
            var editor = (TextBox)typeof(MainWindow).GetField("_tabRenameEditor", flags)!.GetValue(window)!;
            editor.Text = "سرور اصلی";
            if (live)
            {
                editor.Focus(); Pump();
                var key = new KeyEventArgs(Keyboard.PrimaryDevice, PresentationSource.FromVisual(window), Environment.TickCount, Key.Enter)
                    { RoutedEvent = Keyboard.PreviewKeyDownEvent };
                editor.RaiseEvent(key); Pump();
                if (!key.Handled) throw new Exception("Rename Enter reached the terminal.");
            }
            else Call("FinishTabRename", true, false, true);
            if (window.Title != "سرور اصلی" || (string)tabType.GetProperty("Title")!.GetValue(tabs[0])! != window.Title)
                throw new Exception("Rename did not update tab and window/taskbar title.");
            if (live)
            {
                var caption = new StringBuilder(512);
                GetWindowText(new WindowInteropHelper(window).Handle, caption, caption.Capacity);
                if (caption.ToString() != "سرور اصلی") throw new Exception("Native window/taskbar caption did not update.");
            }
            Call("BeginTabRename", tabs[0]); Pump();
            editor = (TextBox)typeof(MainWindow).GetField("_tabRenameEditor", flags)!.GetValue(window)!;
            editor.Text = "Discard this";
            if (live)
            {
                editor.Focus(); Pump();
                var key = new KeyEventArgs(Keyboard.PrimaryDevice, PresentationSource.FromVisual(window), Environment.TickCount, Key.Escape)
                    { RoutedEvent = Keyboard.PreviewKeyDownEvent };
                editor.RaiseEvent(key); Pump();
                if (!key.Handled) throw new Exception("Rename Escape reached the terminal.");
            }
            else Call("FinishTabRename", false, false, true);
            if (window.Title != "سرور اصلی") throw new Exception("Escape failed to cancel rename.");
            Call("ShowTab", tabs[1]); Pump();
            if (window.Title == "سرور اصلی") throw new Exception("Taskbar title did not follow active tab.");
            Call("ShowTab", tabs[0]); Pump();
            if (window.Title != "سرور اصلی") throw new Exception("Custom title was lost after switching tabs.");
            Call("BeginTabRename", tabs[0]); Pump();
            editor = (TextBox)typeof(MainWindow).GetField("_tabRenameEditor", flags)!.GetValue(window)!;
            editor.Text = "Name on blur";
            Call("ShowTab", tabs[1]); Pump();
            if ((string)tabType.GetProperty("Title")!.GetValue(tabs[0])! != "Name on blur")
                throw new Exception("Switching away lost the edited name.");
            Call("BeginTabRename", tabs[0]); Pump();
            editor = (TextBox)typeof(MainWindow).GetField("_tabRenameEditor", flags)!.GetValue(window)!;
            editor.Text = "   "; Call("FinishTabRename", true, false, true);
            if (window.Title != firstTitle) throw new Exception("Blank name did not restore automatic title.");
            var strip = (StackPanel)window.FindName("TabStrip");
            var title = Descendants(strip.Children[0]).OfType<TextBlock>().Single(t => t.Text == firstTitle);
            var click = new MouseButtonEventArgs(Mouse.PrimaryDevice, Environment.TickCount, MouseButton.Left)
                { RoutedEvent = UIElement.MouseLeftButtonDownEvent };
            typeof(MouseButtonEventArgs).GetProperty("ClickCount")!.SetValue(click, 2);
            title.RaiseEvent(click); Pump();
            if (!click.Handled || typeof(MainWindow).GetField("_tabRenameEditor", flags)!.GetValue(window) is not TextBox)
                throw new Exception("Double-clicking tab text did not start inline editing.");
            SaveChrome(root, "tab-rename.png", 980, 80);
            var cancelRename = Descendants(strip.Children[0]).OfType<Button>().Single(b => b.Name == "CancelTabRenameButton");
            var renameField = Descendants(strip.Children[0]).OfType<Border>().Single(b => b.Name == "TabRenameField");
            if (renameField.Background is not SolidColorBrush || renameField.ActualHeight != 24)
                throw new Exception("Rename editor must have its own visible field background.");
            var countBeforeCancel = tabs.Count;
            editor = (TextBox)typeof(MainWindow).GetField("_tabRenameEditor", flags)!.GetValue(window)!;
            editor.Text = "Must be discarded";
            cancelRename.RaiseEvent(new RoutedEventArgs(Button.ClickEvent)); Pump();
            if (tabs.Count != countBeforeCancel || window.Title != firstTitle || typeof(MainWindow).GetField("_renamingTab", flags)!.GetValue(window) is not null)
                throw new Exception("Inline cross must cancel rename without closing the tab or changing its name.");
            Call("BeginTabRename", tabs[0]); Pump();
            editor = (TextBox)typeof(MainWindow).GetField("_tabRenameEditor", flags)!.GetValue(window)!;
            editor.Text = "Windows PowerShell"; Pump();
            tabType.GetProperty("BackgroundColor")!.SetValue(tabs[0], Colors.Yellow);
            Call("RebuildTabStrip"); Pump();
            SaveChrome(root, "tab-rename-colored.png", 980, 80);
            AppTheme.Apply(AppThemeKind.Light); Call("RebuildTabStrip"); Pump();
            SaveChrome(root, "tab-rename-light.png", 980, 80);
            AppTheme.Apply(AppThemeKind.Dark); Call("RebuildTabStrip"); Pump();
            var badge = (Button)window.FindName("ActiveDirectoryButton"); badge.ApplyTemplate();
            var pill = (Border)badge.Template.FindName("Pill", badge);
            if (pill.BorderThickness != new Thickness(0) || ((TextBlock)window.FindName("ActiveDirectoryText")).FontSize != 12 ||
                ((SolidColorBrush)badge.Background).Color == ((SolidColorBrush)window.FindResource("ToolbarBrush")).Color)
                throw new Exception("Folder badge must be borderless, larger and distinct from the toolbar.");
            Call("FinishTabRename", false, false, true);
            Console.WriteLine(live ? "PASS live inline rename Enter/Escape and native taskbar caption" : "PASS inline rename, cancel/reset/blur, double-click, active tab title and borderless folder badge");
        }
        finally
        {
            var closing = typeof(MainWindow).GetMethod("Window_Closing", flags)!;
            window.Closing -= (System.ComponentModel.CancelEventHandler)closing.CreateDelegate(typeof(System.ComponentModel.CancelEventHandler), window);
            window.Close(); Registry.CurrentUser.DeleteSubKeyTree(registry, false);
        }
    }
    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int GetWindowText(IntPtr window, StringBuilder text, int capacity);
}
