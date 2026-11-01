using System.Collections;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using RtlTerminal;

internal static partial class Program
{
    private static void CheckWindowCloseConfirmation()
    {
        static void Check(bool value, string message) { if (!value) throw new Exception(message); }
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
        if (Application.Current == null) _ = new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
        var originalLanguage = UiText.Current; UiText.Select(UiLanguage.English, persist: false); AppTheme.Apply(AppThemeKind.Dark);
        var window = new MainWindow();
        Check(window.Width == 1120, "Initial window width was not increased");
        var loaded = typeof(MainWindow).GetMethod("Window_Loaded", flags)!;
        window.Loaded -= (RoutedEventHandler)loaded.CreateDelegate(typeof(RoutedEventHandler), window);
        var tabs = (IList)typeof(MainWindow).GetField("_tabs", flags)!.GetValue(window)!;
        var tabType = typeof(MainWindow).GetNestedType("TerminalTab", BindingFlags.NonPublic)!;
        var profileType = typeof(MainWindow).GetNestedType("TerminalProfile", BindingFlags.NonPublic)!;
        for (var i = 0; i < 10; i++)
        {
            var pane = typeof(MainWindow).GetMethod("CreatePane", flags)!.Invoke(window, new[] { Enum.ToObject(profileType, 0) });
            tabs.Add(Activator.CreateInstance(tabType, pane)!);
        }
        typeof(MainWindow).GetMethod("ShowTab", flags)!.Invoke(window, new[] { tabs[0] });
        void Pump() => window.Dispatcher.Invoke(() => { }, DispatcherPriority.ApplicationIdle);
        void Answer(Action<CloseTabsWindow> answer) => window.Dispatcher.BeginInvoke(DispatcherPriority.ContextIdle, new Action(() => answer(Application.Current.Windows.OfType<CloseTabsWindow>().Single())));
        try
        {
            window.Show(); Pump();
            var right = (Button)window.FindName("ScrollTabsRightButton");
            right.Focus(); right.RaiseEvent(new RoutedEventArgs(Button.ClickEvent)); Pump();
            var frame = (Border)right.Template.FindName("ArrowFrame", right);
            if (!right.IsMouseOver) Check(frame.Background is SolidColorBrush { Color.A: 0 }, "Arrow retained its focus/click background after release");
            var selected = typeof(MainWindow).GetField("_activeTab", flags)!.GetValue(window);
            foreach (var theme in new[] { AppThemeKind.Dark, AppThemeKind.Light })
            {
                AppTheme.Apply(theme);
                Answer(dialog =>
                {
                    Check(((Button)dialog.FindName("CancelButton")).IsDefault, "Close confirmation default is not Cancel");
                    CaptureOperation(dialog, "close-tabs-" + theme);
                    ((Button)dialog.FindName("CancelButton")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                });
                window.Close(); Pump();
                Check(window.IsVisible && tabs.Count == 10 && ReferenceEquals(selected, typeof(MainWindow).GetField("_activeTab", flags)!.GetValue(window)), "Cancel closed tabs or changed the selected tab");
            }
            Answer(dialog => dialog.Close()); window.Close(); Pump();
            Check(window.IsVisible && tabs.Count == 10, "Dismissal closed the terminal instead of cancelling");
            foreach (var language in new[] { UiLanguage.Persian, UiLanguage.Arabic, UiLanguage.English })
            {
                UiText.Select(language, persist: false);
                Answer(dialog =>
                {
                    Check(dialog.Title == UiText.Get("Close terminal") && ((Button)dialog.FindName("ConfirmButton")).Content.ToString() == UiText.Get("Yes"), "Confirmation is not localized");
                    ((Button)dialog.FindName("CancelButton")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                });
                typeof(MainWindow).GetMethod("ExitMenuItem_Click", flags)!.Invoke(window, new object?[] { window, new RoutedEventArgs() }); Pump();
                Check(window.IsVisible, "Exit bypassed Cancel");
            }
            var closed = false; window.Closed += (_, _) => closed = true;
            Answer(dialog => ((Button)dialog.FindName("ConfirmButton")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent)));
            typeof(MainWindow).GetMethod("CloseWindowButton_Click", flags)!.Invoke(window, new object?[] { window, new RoutedEventArgs() });
            Check(closed, "Yes did not close the window");
        }
        finally
        {
            foreach (var dialog in Application.Current!.Windows.OfType<CloseTabsWindow>().ToArray()) dialog.Close();
            var closing = typeof(MainWindow).GetMethod("Window_Closing", flags)!;
            window.Closing -= (System.ComponentModel.CancelEventHandler)closing.CreateDelegate(typeof(System.ComponentModel.CancelEventHandler), window);
            window.Close(); UiText.Select(originalLanguage, persist: false); AppTheme.Apply(AppThemeKind.Dark);
        }
        // No confirmation for a single tab, even in a visible window.
        var single = new MainWindow(); single.Loaded -= (RoutedEventHandler)loaded.CreateDelegate(typeof(RoutedEventHandler), single);
        var singleTabs = (IList)typeof(MainWindow).GetField("_tabs", flags)!.GetValue(single)!;
        var one = typeof(MainWindow).GetMethod("CreatePane", flags)!.Invoke(single, new[] { Enum.ToObject(profileType, 0) });
        singleTabs.Add(Activator.CreateInstance(tabType, one)!);
        single.Show(); single.Close(); Check(!single.IsVisible, "A single tab unexpectedly requires confirmation");
        Console.WriteLine("PASS wider default window, transient arrow feedback, multi-tab Cancel/Yes/dismissal, Exit and caption-close paths, three languages, both themes and single-tab direct close");
    }
}
