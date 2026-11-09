using System.Collections;
using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using RtlTerminal;

internal static partial class Program
{
    private static void CheckTabHistoryFeatures()
    {
        static void Check(bool ok, string reason) { if (!ok) throw new Exception(reason); }
        Check(DirectoryOpenPolicy.Decide(DirectoryOpenMode.Smart, false) == DirectoryOpenAction.Replace, "Untouched smart tab must be reused");
        Check(DirectoryOpenPolicy.Decide(DirectoryOpenMode.Smart, true) == DirectoryOpenAction.NewTab, "Used smart tab must open separately");
        Check(DirectoryOpenPolicy.Decide(DirectoryOpenMode.AlwaysNewTab, false) == DirectoryOpenAction.NewTab, "Always new preference");
        Check(DirectoryOpenPolicy.Decide(DirectoryOpenMode.CurrentTab, true) == DirectoryOpenAction.Confirm && DirectoryOpenPolicy.Decide(DirectoryOpenMode.CurrentTab, false) == DirectoryOpenAction.Replace, "Current preference protects used session");
        Check(!MainWindow.ShouldDetachTab(new(30, 15), new(70, 15), new(100, 70)), "Inside drag must not detach");
        Check(MainWindow.ShouldDetachTab(new(30, 15), new(-20, 15), new(100, 70)), "Outside drag must detach");
        Check(!MainWindow.ShouldDetachTab(new(0, 0), new(-1, 0), new(100, 70)), "Subthreshold movement must not detach");

        var folder = Path.Combine(Environment.CurrentDirectory, "artifacts", "tab-history-" + Guid.NewGuid().ToString("N"));
        var store = new MacroStore(Path.Combine(folder, "macros"));
        var first = new TerminalMacro { Name = "Z first", Steps = [new MacroStep { Input = "echo first\r" }] };
        var second = new TerminalMacro { Name = "A second", Steps = [new MacroStep { Input = "echo second\r" }] };
        store.Save(first, null); store.Save(second, null);
        var ordered = store.List().OrderBy(e => e.CreatedAt).ThenBy(e => e.Id).ToArray();
        Check(ordered[0].Id == first.Id && ordered[1].Id == second.Id, "Creation date order must differ from name order");
        var date = ordered[0].CreatedAt; first.Name = "Renamed"; store.Save(first, null);
        Check(store.List().Single(e => e.Id == first.Id).CreatedAt == date, "Editing a macro must preserve its creation date");
        var legacyPath = Path.Combine(store.DirectoryPath, second.Id.ToString("N") + ".rtmacro");
        var clear = System.Security.Cryptography.ProtectedData.Unprotect(File.ReadAllBytes(legacyPath), null, System.Security.Cryptography.DataProtectionScope.CurrentUser);
        var legacy = System.Text.Json.Nodes.JsonNode.Parse(clear)!; legacy["Entry"]!.AsObject().Remove("CreatedAt");
        File.WriteAllBytes(legacyPath, System.Security.Cryptography.ProtectedData.Protect(System.Text.Encoding.UTF8.GetBytes(legacy.ToJsonString()), null, System.Security.Cryptography.DataProtectionScope.CurrentUser));
        System.Security.Cryptography.CryptographicOperations.ZeroMemory(clear);
        File.SetCreationTimeUtc(legacyPath, DateTime.UtcNow.AddDays(-5));
        var legacyDate = new DateTimeOffset(File.GetCreationTimeUtc(legacyPath));
        Check(store.List().Single(e => e.Id == second.Id).CreatedAt == legacyDate, "Legacy macro uses file creation date");
        store.Save(second, null);
        Check(store.List().Single(e => e.Id == second.Id).CreatedAt == legacyDate, "Legacy creation date is preserved on save");

        if (Application.Current == null) _ = new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
        UiText.Select(UiLanguage.English, false); AppTheme.Apply(AppThemeKind.Dark);
        foreach (var result in new[] { DirectoryOpenAction.Replace, DirectoryOpenAction.Cancel, DirectoryOpenAction.NewTab })
        {
            var dialog = new DirectoryReplaceWindow();
            var buttons = ((StackPanel)((Border)dialog.Content).Child).Children.OfType<StackPanel>().Single().Children.OfType<Button>().ToArray();
            Check(buttons.Length == 3 && buttons[1].IsCancel && buttons[1].IsDefault, "Replacement confirmation must offer Yes/No/New tab with safe default");
            dialog.Loaded += (_, _) => dialog.Dispatcher.BeginInvoke(new Action(() => buttons[Array.IndexOf(new[] { DirectoryOpenAction.Replace, DirectoryOpenAction.Cancel, DirectoryOpenAction.NewTab }, result)].RaiseEvent(new RoutedEventArgs(Button.ClickEvent))));
            dialog.ShowDialog(); Check(dialog.Action == result, "Replacement confirmation result");
        }
        CheckLiveTabDetach(folder);
        CheckLiveDirectoryNavigation(folder);
        Console.WriteLine("PASS macro creation order, stable edits, smart/current/new directory policy, three-choice confirmation and live split tab detachment");
    }

    private static void CheckLiveDirectoryNavigation(string folder)
    {
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
        static object? Call(MainWindow window, string name, params object?[] args) => typeof(MainWindow).GetMethod(name, flags)!.Invoke(window, args);
        static object? Get(object obj, string property) => obj.GetType().GetProperty(property)!.GetValue(obj);
        static void Check(bool ok, string reason) { if (!ok) throw new Exception("History: " + reason); }
        using var settings = Microsoft.Win32.Registry.CurrentUser.CreateSubKey(@"Software\RtlTerminal");
        var previous = settings.GetValue("DirectoryOpenMode");
        var kind = previous == null ? Microsoft.Win32.RegistryValueKind.String : settings.GetValueKind("DirectoryOpenMode");
        var registry = @"Software\RtlTerminal.Tests\Navigate-" + Guid.NewGuid().ToString("N");
        var window = new MainWindow(new DirectoryHistoryStore(registry));
        typeof(MainWindow).GetField("_receivingDetachedTab", flags)!.SetValue(window, true);
        var tabs = (IList)typeof(MainWindow).GetField("_tabs", flags)!.GetValue(window)!;
        var profile = Enum.Parse(typeof(MainWindow).GetNestedType("TerminalProfile", BindingFlags.NonPublic)!, "CommandPrompt");
        var target = Directory.CreateDirectory(Path.Combine(folder, "recent with space")).FullName;
        var entry = new DirectoryHistoryEntry(target, "CommandPrompt", null, null, null);
        object ActivePane() => Get(tabs[tabs.Count - 1]!, "ActivePane")!;
        void Answer(int index) => window.Dispatcher.BeginInvoke(DispatcherPriority.Loaded, new Action(() =>
        {
            var dialog = Application.Current.Windows.OfType<DirectoryReplaceWindow>().Single();
            var buttons = ((StackPanel)((Border)dialog.Content).Child).Children.OfType<StackPanel>().Single().Children.OfType<Button>().ToArray();
            buttons[index].RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        }));
        try
        {
            AppSettings.SaveDirectoryOpenMode(DirectoryOpenMode.Smart);
            Call(window, "CreateTerminalTab", profile, folder, null); window.Show();
            var untouched = ActivePane();
            Call(window, "OpenHistoryDirectory", entry);
            Check(tabs.Count == 1 && !ReferenceEquals(untouched, ActivePane()), "Smart mode must replace untouched pane");
            Check((string?)Get(ActivePane(), "CurrentDirectory") == target, "Replacement must start in selected directory");
            var used = ActivePane(); Call(window, "SendUserInput", used, "x"); Call(window, "SendUserInput", used, "\b");
            Check((bool)Get(used, "HasUserInput")!, "Deleted input must still count");
            Call(window, "OpenHistoryDirectory", entry);
            Check(tabs.Count == 2 && !ReferenceEquals(used, ActivePane()), "Smart mode protects used pane");
            AppSettings.SaveDirectoryOpenMode(DirectoryOpenMode.AlwaysNewTab);
            Call(window, "OpenHistoryDirectory", entry); Check(tabs.Count == 3, "Always new opens even for untouched tab");
            AppSettings.SaveDirectoryOpenMode(DirectoryOpenMode.CurrentTab);
            Call(window, "OpenHistoryDirectory", entry); Check(tabs.Count == 3, "Current mode reuses untouched pane");
            var pane = ActivePane(); var session = Get(pane, "Session"); Call(window, "SendUserInput", pane, "x");
            Answer(1); Call(window, "OpenHistoryDirectory", entry);
            Check(tabs.Count == 3 && ReferenceEquals(pane, ActivePane()) && ReferenceEquals(session, Get(pane, "Session")), "No must preserve current process and input");
            Answer(2); Call(window, "OpenHistoryDirectory", entry);
            Check(tabs.Count == 4 && ReferenceEquals(session, Get(pane, "Session")), "New tab choice must preserve old session");
            var active = ActivePane(); Call(window, "SendUserInput", active, "x");
            Answer(0); Call(window, "OpenHistoryDirectory", entry);
            Check(tabs.Count == 4 && !ReferenceEquals(active, ActivePane()), "Yes must replace only after confirmation");
            var currentTab = tabs[3]!; var neighbor = ActivePane(); var neighborSession = Get(neighbor, "Session");
            var split = Call(window, "AddSplitPane", currentTab, neighbor, Enum.Parse(typeof(MainWindow).GetNestedType("PaneSplitDirection", BindingFlags.NonPublic)!, "SideBySide"))!;
            Call(window, "StartPane", split, folder, false);
            Call(window, "OpenHistoryDirectory", entry);
            Check(((IList)Get(currentTab, "Panes")!).Count == 2 && ReferenceEquals(neighborSession, Get(neighbor, "Session")), "History replacement must preserve neighboring split session");
        }
        finally
        {
            window.Hide(); window.Close();
            if (previous == null) settings.DeleteValue("DirectoryOpenMode", false); else settings.SetValue("DirectoryOpenMode", previous, kind);
            Microsoft.Win32.Registry.CurrentUser.DeleteSubKeyTree(registry, false);
        }
    }

    private static void CheckLiveTabDetach(string folder)
    {
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
        static object? Call(MainWindow window, string name, params object?[] args) => typeof(MainWindow).GetMethod(name, flags)!.Invoke(window, args);
        static object? Get(object obj, string property) => obj.GetType().GetProperty(property)!.GetValue(obj);
        static void Set(object obj, string property, object? value) => obj.GetType().GetProperty(property)!.SetValue(obj, value);
        static void Check(bool ok, string reason) { if (!ok) throw new Exception("Detach: " + reason); }
        var registry = @"Software\RtlTerminal.Tests\Detach-" + Guid.NewGuid().ToString("N");
        var source = new MainWindow(new DirectoryHistoryStore(registry));
        typeof(MainWindow).GetField("_receivingDetachedTab", flags)!.SetValue(source, true);
        var windows = new List<MainWindow> { source }; var readers = new List<Task>(); var sessions = new List<ConPtySession>();
        var tabType = typeof(MainWindow).GetNestedType("TerminalTab", BindingFlags.NonPublic)!;
        var profile = Enum.Parse(typeof(MainWindow).GetNestedType("TerminalProfile", BindingFlags.NonPublic)!, "CommandPrompt");
        var tabs = (IList)typeof(MainWindow).GetField("_tabs", flags)!.GetValue(source)!;
        object Pane()
        {
            var pane = Call(source, "CreatePane", profile)!;
            var session = new ConPtySession(100, 28); sessions.Add(session);
            session.Start(ShellPath.WindowsArgument(Path.Combine(Environment.SystemDirectory, "cmd.exe")) + " /d /q", folder);
            Set(pane, "Session", session); Set(pane, "Buffer", new TerminalBuffer(100, 28)); Set(pane, "CancellationTokenSource", new CancellationTokenSource());
            readers.Add(Task.Run(() => Call(source, "ReadOutputLoop", pane)));
            return pane;
        }
        string Text(object pane) => string.Join("\n", ((TerminalBuffer)Get(pane, "Buffer")!).CaptureSnapshot().Lines.Select(l => l.Text));
        void Wait(Func<bool> condition) => PumpUntil(condition);
        try
        {
            var first = Pane(); var tab = Activator.CreateInstance(tabType, first)!; tabs.Add(tab);
            var neighbor = Pane(); var otherTab = Activator.CreateInstance(tabType, neighbor)!; tabs.Add(otherTab);
            Call(source, "ShowTab", tab); source.Show();
            var strip = (StackPanel)source.FindName("TabStrip");
            var tile = (Grid)strip.Children[0];
            var tabButton = tile.Children.OfType<DockPanel>().Single().Children.OfType<Button>().Single(b => ReferenceEquals(b.Tag, tab) && b.Name != "CloseTabButton");
            var opacity = tile.Opacity; var transform = tile.RenderTransform;
            tabButton.Cursor = System.Windows.Input.Cursors.Hand;
            Call(source, "BeginTabDragFeedback", tabButton, tab);
            Check(tile.Opacity == .7 && tile.RenderTransform is TranslateTransform { Y: -2 } && tabButton.Cursor == System.Windows.Input.Cursors.SizeAll, "Dragging must show faded/lifted tab and move cursor");
            Call(source, "BeginTabDragFeedback", tabButton, tab);
            Call(source, "ClearTabDrag"); Call(source, "ClearTabDrag");
            Check(tile.Opacity == opacity && ReferenceEquals(tile.RenderTransform, transform) && tabButton.Cursor == System.Windows.Input.Cursors.Hand, "Release/cancel must restore tab appearance without sticky cursor");
            var second = Call(source, "AddSplitPane", tab, first, Enum.Parse(typeof(MainWindow).GetNestedType("PaneSplitDirection", BindingFlags.NonPublic)!, "SideBySide"))!;
            var splitSession = new ConPtySession(100, 28); sessions.Add(splitSession);
            splitSession.Start(ShellPath.WindowsArgument(Path.Combine(Environment.SystemDirectory, "cmd.exe")) + " /d /q", folder);
            Set(second, "Session", splitSession); Set(second, "Buffer", new TerminalBuffer(100, 28)); Set(second, "CancellationTokenSource", new CancellationTokenSource());
            readers.Add(Task.Run(() => Call(source, "ReadOutputLoop", second)));
            Call(source, "SendUserInput", first, "set DETACH_KEEP=still_alive\r");
            Check((bool)Get(first, "HasUserInput")!, "Terminal input must permanently mark pane used");
            Set(tab, "CustomTitle", "Live detached tab"); Set(tab, "BackgroundColor", Colors.DarkSlateBlue);
            var view = Get(first, "View"); var buffer = Get(first, "Buffer"); var sessionId = ((ConPtySession)Get(first, "Session")!).SessionId;
            var drafts = (Dictionary<string, QuickInputDraft>)typeof(MainWindow).GetField("_quickDrafts", flags)!.GetValue(source)!;
            var draft = new QuickInputDraft(); draft.InsertText("unsent draft"); drafts[sessionId] = draft;
            var image = System.Windows.Media.Imaging.BitmapSource.Create(1, 1, 96, 96, PixelFormats.Bgra32, null, new byte[4], 4);
            var temporaryImage = (string)Call(source, "SaveTemporaryImage", image)!;
            var quickImages = (QuickInputImages)typeof(MainWindow).GetField("_quickImages", flags)!.GetValue(source)!;
            var quickImage = quickImages.Save(image, new HashSet<string>());
            draft.InsertAttachment(quickImage, true);
            var moved = Call(source, "DetachTabToWindow", tab, source.PointToScreen(new Point(200, 100))) as MainWindow ?? throw new Exception("Tab transfer failed"); windows.Add(moved);
            Check(tabs.Count == 1, "Source must retain its other tab");
            Check(ReferenceEquals(view, Get(first, "View")) && ReferenceEquals(buffer, Get(first, "Buffer")) && ((ConPtySession)Get(first, "Session")!).SessionId == sessionId, "Moving must preserve view, buffer and session");
            Check(ReferenceEquals(first.GetType().GetField("Window")!.GetValue(first), moved), "Pane owner must follow destination");
            Check(((IList)Get(tab, "Panes")!).Count == 2 && (string)Get(tab, "Title")! == "Live detached tab", "Splits and custom title must survive");
            var destinationDrafts = (Dictionary<string, QuickInputDraft>)typeof(MainWindow).GetField("_quickDrafts", flags)!.GetValue(moved)!;
            Check(ReferenceEquals(destinationDrafts[sessionId], draft) && !drafts.ContainsKey(sessionId), "Quick Input draft follows session");
            var fresh = Call(source, "CreatePane", profile)!;
            Check(!ReferenceEquals(Get(fresh, "View"), view), "New source panes cannot reclaim a moved XAML view");
            Call(moved, "SendUserInput", first, "echo DETACHED_%DETACH_KEEP%\r");
            Wait(() => Text(first).Contains("DETACHED_still_alive", StringComparison.Ordinal));
            Call(moved, "SendUserInput", second, "echo SPLIT_AFTER_DETACH\r");
            Wait(() => Text(second).Contains("SPLIT_AFTER_DETACH", StringComparison.Ordinal));
            // Moving the last source tab closes only that window, without terminating its shell.
            var last = Call(source, "DetachTabToWindow", otherTab, source.PointToScreen(new Point(300, 100))) as MainWindow ?? throw new Exception("Last tab transfer failed"); windows.Add(last);
            Check((bool)typeof(MainWindow).GetField("_windowClosed", flags)!.GetValue(source)!, "Empty source window should close");
            Check(File.Exists(temporaryImage) && File.Exists(quickImage), "Closing source must retain images referenced by moved sessions");
            Call(last, "SendUserInput", neighbor, "echo SOURCE_CLOSED_SHELL_ALIVE\r");
            Wait(() => Text(neighbor).Contains("SOURCE_CLOSED_SHELL_ALIVE", StringComparison.Ordinal));
            Call(moved, "SendUserInput", first, "echo ORIGINAL_SPLIT_STILL_ALIVE\r");
            Wait(() => Text(first).Contains("ORIGINAL_SPLIT_STILL_ALIVE", StringComparison.Ordinal));
        }
        finally
        {
            foreach (var window in windows) { window.Hide(); window.Close(); }
            foreach (var session in sessions) session.Dispose();
            Task.WhenAll(readers).Wait(TimeSpan.FromSeconds(10));
            Microsoft.Win32.Registry.CurrentUser.DeleteSubKeyTree(registry, false);
        }
    }
}
