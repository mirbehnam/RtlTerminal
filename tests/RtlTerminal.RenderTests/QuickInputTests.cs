using System.IO;
using System.Reflection;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using RtlTerminal;

internal static partial class Program
{
    private static void CheckQuickInput()
    {
        static void Check(bool ok, string message) { if (!ok) throw new Exception(message); }
        if (Application.Current == null) _ = new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
        UiText.Select(UiLanguage.English, persist: false); AppTheme.Apply(AppThemeKind.Dark);
        const string original = "hello فارسی \"quoted\"\r\n\n\t  end  \n";
        Check(QuickInputText.Prepare(original, true) == "\x1b[200~" + original + "\x1b[201~", "Bracketed paste changed text");
        Check(QuickInputText.SingleLine("a\r\nb\nc\rd\te") == "a b c d    e", "Single-line confirmation transform changed text incorrectly");
        foreach (var bad in new[] { "a\0b", "\x1b[201~inject", "\u007f", "\u0085", new string('x', QuickInputText.MaximumBytes + 1) })
        { try { QuickInputText.Prepare(bad, true); throw new Exception("Unsafe input accepted"); } catch (ArgumentException) { } }
        try { QuickInputText.Prepare("a\nb", false); throw new Exception("Unconfirmed multiline input allowed"); } catch (ArgumentException) { }
        var buffer = new TerminalBuffer(80, 20);
        buffer.Process("prompt > \x1b[?2004h"); Check(!buffer.QuickInputMode.Ready, "Cursor or bracketed paste falsely implied input readiness");
        buffer.Process("\x1b]133;B\x07"); Check(buffer.QuickInputMode.Ready, "Explicit input marker not recognized");
        buffer.InvalidateQuickInput(); Check(!buffer.QuickInputMode.Ready, "Submission did not invalidate readiness");
        buffer.Process("\x1b]633;B\x07\x1b]633;C\x07"); Check(!buffer.QuickInputMode.Ready, "Running command retained input readiness");
        var draft = new QuickInputDraft();
        var editorHost = new Window { Width = 500, Height = 250, Content = draft.Editor }; editorHost.Show();
        draft.InsertText(original);
        Check(draft.Text(a => a.Path) == original, "Native editor did not preserve pasted line endings/spaces");
        draft.Editor.Undo(); Check(draft.Text(a => a.Path) == "", "Paste undo lost its boundary: " + System.Text.Json.JsonSerializer.Serialize(draft.Text(a => a.Path)));
        draft.Editor.Redo(); Check(draft.Text(a => a.Path) == original, "Redo changed pasted text");
        editorHost.Content = null; editorHost.Close();
        draft.InsertAttachment(@"C:\folder with space\image.png", true);
        Check(draft.Attachments().Count() == 1 && draft.Text(a => a.Path).Contains(@"C:\folder with space\image.png"), "Attachment path expansion failed");
        Check(draft.Text(a => a.Path).EndsWith("image.png"), "Editor appended an unwanted newline");
        var recovered = new QuickInputDraft(); recovered.Recover(draft);
        Check(recovered.Text(a => a.Path) == draft.Text(a => a.Path) && recovered.Attachments().Count() == 1, "Draft recovery lost attachment identity or text");
        var typed = new QuickInputDraft(); typed.Editor.Document.Blocks.Clear(); typed.Editor.Document.Blocks.Add(new Paragraph(new Run("one"))); typed.Editor.Document.Blocks.Add(new Paragraph(new Run("two")));
        Check(typed.Text(a => a.Path) == "one\ntwo", "Typed paragraph separators were lost");
        using var images = new QuickInputImages();
        var bitmap = BitmapSource.Create(2, 2, 96, 96, PixelFormats.Bgra32, null, new byte[16], 8); bitmap.Freeze();
        var pinned = new HashSet<string>();
        for (var i = 0; i < 20; i++) pinned.Add(images.Save(bitmap, pinned));
        try { images.Save(bitmap, pinned); throw new Exception("Image cap exceeded"); } catch (IOException) { }
        var old = pinned.First(); pinned.Remove(old); var replacement = images.Save(bitmap, pinned);
        Check(!File.Exists(old) && File.Exists(replacement), "Temporary image eviction removed a pinned image or failed");
        foreach (var theme in new[] { AppThemeKind.Dark, AppThemeKind.Light })
        {
            AppTheme.Apply(theme);
            var window = new QuickInputWindow(draft, _ => Task.FromResult(new TerminalInputResult(false, false, "The destination terminal is no longer available. Your draft was preserved.")), images, () => pinned);
            window.Show();
            CheckQuickInputRefinements(window, draft);
            var pasteData = new DataObject(); pasteData.SetText(" PASTED فارسی\nnext ");
            var pasteArgs = new DataObjectPastingEventArgs(pasteData, false, DataFormats.UnicodeText);
            typeof(QuickInputWindow).GetMethod("Paste", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(window, new object[] { draft.Editor, pasteArgs });
            Check(draft.Text(a => a.Path).Contains(" PASTED فارسی\nnext "), "Native paste handler lost text while editor was temporarily read-only");
            var marker = "[rtl-" + Guid.NewGuid().ToString("N") + "]";
            var clipData = new DataObject(); clipData.SetData(QuickInputWindow.ClipboardFormat, System.Text.Json.JsonSerializer.Serialize(new { Text = marker, Attachments = new Dictionary<string, QuickInputAttachment> { [marker] = new(replacement, false, 1) } }));
            typeof(QuickInputWindow).GetMethod("Paste", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(window, new object[] { draft.Editor, new DataObjectPastingEventArgs(clipData, false, QuickInputWindow.ClipboardFormat) });
            Check(draft.Attachments().Any(a => a.Path == replacement && !a.Image), "Internal clipboard lost file token identity");
            var beforeUndo = draft.Text(a => a.Path);
            draft.Editor.Undo(); Check(draft.Text(a => a.Path) != beforeUndo, "Attachment paste was not undoable");
            draft.Editor.Redo(); Check(draft.Text(a => a.Path) == beforeUndo, "Attachment redo lost its path token");
            CaptureOperation(window, "quick-input-" + theme); window.Close();
            Check(draft.Text(a => a.Path).Contains("hello فارسی"), "Closing discarded draft");
        }
        LiveQuickInput().GetAwaiter().GetResult();
        CheckQuickInputShortcutActions();
        CheckFailedQuickInputWrite();
        CheckQuickInputTargeting();
        AppTheme.Apply(AppThemeKind.Dark);
        Console.WriteLine("PASS Quick Input: exact text and attachments, explicit readiness, control/size checks, image cap, reusable draft, themes, native Insert/Send and closed connection");
    }
    private static void CheckQuickInputShortcutActions()
    {
        using var images = new QuickInputImages();
        foreach (var (modifiers, expected) in new[] { (ModifierKeys.Control, true), (ModifierKeys.Control | ModifierKeys.Shift, false) })
        {
            bool? called = null;
            var draft = new QuickInputDraft(); draft.InsertText("shortcut فارسی");
            var window = new QuickInputWindow(draft, send => { called = send; return Task.FromResult(new TerminalInputResult(true, true, null)); }, images, () => new HashSet<string>());
            window.Show();
            var method = typeof(QuickInputWindow).GetMethod("InvokeTransferShortcut", BindingFlags.Instance | BindingFlags.NonPublic)!;
            if (!(bool)method.Invoke(window, new object[] { Key.Enter, modifiers })! || called != expected || !window.Transferred)
                throw new Exception("Keyboard shortcut did not invoke the same Send/Insert button action");
        }
        bool invoked = false;
        var editor = new QuickInputWindow(new QuickInputDraft(), _ => { invoked = true; return Task.FromResult(new TerminalInputResult(true, true, null)); }, images, () => new HashSet<string>());
        var invoke = typeof(QuickInputWindow).GetMethod("InvokeTransferShortcut", BindingFlags.Instance | BindingFlags.NonPublic)!;
        foreach (var modifiers in new[] { ModifierKeys.None, ModifierKeys.Shift, ModifierKeys.Control | ModifierKeys.Alt })
            if ((bool)invoke.Invoke(editor, new object[] { Key.Enter, modifiers })! || invoked) throw new Exception("Newline or unrelated shortcut triggered Send");
        editor.Close();
        Console.WriteLine("PASS Quick Input keyboard and mouse share Send/Insert actions; plain/Shift Enter never transfer");
    }
    private static void CheckQuickInputRefinements(QuickInputWindow window, QuickInputDraft draft)
    {
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
        var actions = (List<Button>)typeof(QuickInputWindow).GetField("actions", flags)!.GetValue(window)!;
        var statistics = (TextBlock)typeof(QuickInputWindow).GetField("statistics", flags)!.GetValue(window)!;
        typeof(QuickInputWindow).GetMethod("UpdateStatistics", flags)!.Invoke(window, null);
        if (!statistics.Text.Contains("characters") || !statistics.Text.Contains("words")) throw new Exception("Character/word counters are missing");
        foreach (var (name, shortcut) in new[] { ("Send", "Ctrl+Enter"), ("Insert", "Ctrl+Shift+Enter"), ("Close", "Escape") })
        {
            var action = actions.Single(b => b.Content?.ToString() == name);
            if (action.ToolTip?.ToString() != shortcut || action.Height < 36 || action.Parent is not StackPanel group || !group.Children.OfType<TextBlock>().Any(t => t.Text == shortcut)) throw new Exception("Quick Input shortcut label/size missing: " + name);
        }
        var before = draft.Text(a => a.Path);
        var pending = window.ConfirmSingleLineAsync();
        var overlay = (Border)typeof(QuickInputWindow).GetField("confirmationOverlay", flags)!.GetValue(window)!;
        var root = (DockPanel)typeof(QuickInputWindow).GetField("contentRoot", flags)!.GetValue(window)!;
        if (pending.IsCompleted || overlay.Visibility != Visibility.Visible || root.IsEnabled) throw new Exception("Inline confirmation did not cover/disable the editor");
        CaptureOperation(window, "quick-input-inline-confirm");
        typeof(QuickInputWindow).GetMethod("CompleteConfirmation", flags)!.Invoke(window, new object[] { false });
        if (pending.GetAwaiter().GetResult() || overlay.Visibility != Visibility.Collapsed || !root.IsEnabled || draft.Text(a => a.Path) != before)
            throw new Exception("Inline cancel failed to preserve the draft");
        pending = window.ConfirmSingleLineAsync();
        typeof(QuickInputWindow).GetMethod("CompleteConfirmation", flags)!.Invoke(window, new object[] { true });
        if (!pending.GetAwaiter().GetResult() || draft.Text(a => a.Path) != before) throw new Exception("Inline confirmation mutated the original draft");
        using var settings = Microsoft.Win32.Registry.CurrentUser.CreateSubKey(@"Software\RtlTerminal");
        var original = settings.GetValue("QuickInputRtl");
        var originalKind = original == null ? Microsoft.Win32.RegistryValueKind.DWord : settings.GetValueKind("QuickInputRtl");
        var direction = draft.Editor.FlowDirection;
        try
        {
            if (original == null && direction != FlowDirection.RightToLeft) throw new Exception("RTL was not enabled by default");
            var toggle = typeof(QuickInputWindow).GetMethod("ToggleRtl", flags)!; toggle.Invoke(window, null);
            if (draft.Editor.FlowDirection == direction || settings.GetValue("QuickInputRtl") is not int value || value != (direction == FlowDirection.RightToLeft ? 0 : 1))
                throw new Exception("RTL toggle did not change and persist its mode");
            using var images = new QuickInputImages(); var secondDraft = new QuickInputDraft();
            var second = new QuickInputWindow(secondDraft, _ => Task.FromResult(new TerminalInputResult(false, false, "test")), images, () => new HashSet<string>());
            if (secondDraft.Editor.FlowDirection != draft.Editor.FlowDirection) throw new Exception("Last RTL preference was not restored");
            second.Close(); toggle.Invoke(window, null);
        }
        finally { if (original == null) settings.DeleteValue("QuickInputRtl", false); else settings.SetValue("QuickInputRtl", original, originalKind); }
        using var emptyImages = new QuickInputImages();
        var emptyDraft = new QuickInputDraft();
        var emptyWindow = new QuickInputWindow(emptyDraft, _ => Task.FromResult(new TerminalInputResult(false, false, "test")), emptyImages, () => new HashSet<string>());
        var hint = (TextBlock)typeof(QuickInputWindow).GetField("placeholder", flags)!.GetValue(emptyWindow)!;
        if (hint.Visibility != Visibility.Visible) throw new Exception("Empty editor hint was not visible");
        emptyDraft.InsertText("hello دنیا 😀");
        typeof(QuickInputWindow).GetMethod("UpdateStatistics", flags)!.Invoke(emptyWindow, null);
        var count = (TextBlock)typeof(QuickInputWindow).GetField("statistics", flags)!.GetValue(emptyWindow)!;
        if (count.Text != "12 characters · 3 words" || hint.Visibility != Visibility.Collapsed) throw new Exception("Unicode counters or placeholder state are incorrect: " + count.Text);
        emptyWindow.Close();
    }
    private static void CheckFailedQuickInputWrite()
    {
        var path = Path.GetFullPath(Path.Combine("artifacts", "quick-input-write-" + Guid.NewGuid().ToString("N") + ".tmp"));
        using var session = new ConPtySession(80, 20);
        var field = typeof(ConPtySession).GetField("_inputWriter", BindingFlags.Instance | BindingFlags.NonPublic)!;
        var original = (FileStream)field.GetValue(session)!;
        using var failing = new QuickInputFailingStream(path);
        field.SetValue(session, failing);
        try
        {
            var result = session.WriteInputAsync(new string('x', 40000), true, default).GetAwaiter().GetResult();
            if (result.Success || !result.MayHaveWritten || result.EnterAttempted || failing.EnterWrites != 0) throw new Exception("Partial write incorrectly proceeded to Enter or success");
            using var cancelled = new CancellationTokenSource(); cancelled.Cancel();
            var before = failing.Attempts;
            var stopped = session.WriteInputAsync("cancelled", true, cancelled.Token).GetAwaiter().GetResult();
            if (stopped.Success || stopped.EnterAttempted || failing.Attempts != before) throw new Exception("Cancelled input still wrote or submitted");
        }
        finally { field.SetValue(session, original); }
    }
    private sealed class QuickInputFailingStream(string path) : FileStream(path, FileMode.Create, FileAccess.ReadWrite, FileShare.None)
    {
        public int Attempts, EnterWrites;
        public override void Write(byte[] bytes, int offset, int count)
        { if (++Attempts > 1) throw new IOException("Synthetic partial transfer failure"); base.Write(bytes, offset, count); }
        public override void WriteByte(byte value) { EnterWrites++; base.WriteByte(value); }
    }
    private static void CheckQuickInputTargeting()
    {
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
        var window = new MainWindow();
        var loaded = typeof(MainWindow).GetMethod("Window_Loaded", flags)!;
        window.Loaded -= (RoutedEventHandler)loaded.CreateDelegate(typeof(RoutedEventHandler), window);
        var tabs = (System.Collections.IList)typeof(MainWindow).GetField("_tabs", flags)!.GetValue(window)!;
        var profile = typeof(MainWindow).GetNestedType("TerminalProfile", BindingFlags.NonPublic)!;
        var tabType = typeof(MainWindow).GetNestedType("TerminalTab", BindingFlags.NonPublic)!;
        object Call(string method, params object?[] args) => typeof(MainWindow).GetMethod(method, flags)!.Invoke(window, args)!;
        var panes = new List<object>(); var sessions = new List<ConPtySession>();
        try
        {
            for (var i = 0; i < 2; i++)
            {
                var pane = Call("CreatePane", Enum.ToObject(profile, 0)); var tab = Activator.CreateInstance(tabType, pane)!; tabs.Add(tab); panes.Add(pane);
                var session = new ConPtySession(80, 20); sessions.Add(session);
                pane.GetType().GetProperty("Session")!.SetValue(pane, session); pane.GetType().GetProperty("Buffer")!.SetValue(pane, new TerminalBuffer(80, 20));
            }
            Call("ShowTab", tabs[0]); window.Show();
            var readyBuffer = (TerminalBuffer)panes[0].GetType().GetProperty("Buffer")!.GetValue(panes[0])!;
            var quickButton = (Button)window.FindName("QuickInputButton");
            readyBuffer.Process("\x1b]133;B\x07"); Call("UpdateQuickInputIcon");
            if (quickButton.Visibility != Visibility.Visible) throw new Exception("Ready signal did not reveal the Quick Input icon");
            readyBuffer.Process("\x1b]133;C\x07"); Call("UpdateQuickInputIcon");
            if (quickButton.Visibility != Visibility.Visible || quickButton.Opacity != 1) throw new Exception("Quick Input icon must remain visible regardless of CLI readiness");
            void Open(Action<QuickInputWindow> action)
            {
                window.Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.ApplicationIdle, new Action(() => action((QuickInputWindow)typeof(MainWindow).GetField("_quickWindow", flags)!.GetValue(window)!)));
                Call("OpenQuickInput");
            }
            var drafts = (Dictionary<string, QuickInputDraft>)typeof(MainWindow).GetField("_quickDrafts", flags)!.GetValue(window)!;
            Open(dialog => { drafts[sessions[0].SessionId].InsertText("pane-one فارسی"); dialog.Close(); });
            Call("ShowTab", tabs[1]);
            Open(dialog => { if (drafts[sessions[1].SessionId].Text(a => a.Path) != "") throw new Exception("Draft leaked to another pane"); dialog.Close(); });
            Call("ShowTab", tabs[0]);
            Open(dialog =>
            {
                if (drafts[sessions[0].SessionId].Text(a => a.Path) != "pane-one فارسی") throw new Exception("Reopening lost the pane's draft");
                Call("ShowTab", tabs[1]); panes[0].GetType().GetProperty("Session")!.SetValue(panes[0], null);
                var task = (Task)typeof(QuickInputWindow).GetMethod("Transfer", flags)!.Invoke(dialog, new object[] { true })!;
                PumpUntil(() => task.IsCompleted); task.GetAwaiter().GetResult();
                if (dialog.Transferred || !dialog.IsVisible || drafts[sessions[0].SessionId].Text(a => a.Path) != "pane-one فارسی")
                    throw new Exception("Closed target redirected input or cleared the draft");
                dialog.Close();
            });
        }
        finally
        {
            window.Closing -= (System.ComponentModel.CancelEventHandler)typeof(MainWindow).GetMethod("Window_Closing", flags)!.CreateDelegate(typeof(System.ComponentModel.CancelEventHandler), window);
            window.Close(); foreach (var session in sessions) session.Dispose();
        }
        Console.WriteLine("PASS Quick Input per-session draft isolation, modal target identity, target closure without fallback, retained errors and reopening");
    }
    private static async Task LiveQuickInput()
    {
        using var session = new ConPtySession(90, 20);
        var buffer = new TerminalBuffer(90, 20); var output = new StringBuilder(); var gate = new object();
        session.Start(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "cmd.exe") + " /d /q", Environment.CurrentDirectory);
        var reader = Task.Run(() =>
        {
            try
            {
                var bytes = new byte[8192]; var chars = new char[8192]; var decoder = Encoding.UTF8.GetDecoder(); int count;
                while ((count = session.Read(bytes)) > 0)
                { var text = new string(chars, 0, decoder.GetChars(bytes, 0, count, chars, 0)); buffer.Process(text); lock (gate) output.Append(text); }
            }
            catch (IOException) { } catch (ObjectDisposedException) { }
        });
        await Task.Delay(250);
        var inserted = await session.WriteInputAsync("echo QI_INSERT_OK", false, default);
        if (!inserted.Success || inserted.EnterAttempted) throw new Exception("Native Insert failed");
        await Task.Delay(150);
        if (buffer.CaptureSnapshot().Lines.Any(l => l.Text.Trim() == "QI_INSERT_OK")) throw new Exception("Insert executed a command without Enter");
        var submitTiming = System.Diagnostics.Stopwatch.StartNew();
        var sent = await session.WriteInputAsync("", true, default);
        if (submitTiming.ElapsedMilliseconds < 175) throw new Exception("Final Enter did not wait for paste processing");
        if (!sent.Success || !sent.EnterAttempted) throw new Exception("Native Send failed");
        for (var i = 0; i < 100 && !buffer.CaptureSnapshot().Lines.Any(l => l.Text.Trim() == "QI_INSERT_OK"); i++) await Task.Delay(20);
        if (!buffer.CaptureSnapshot().Lines.Any(l => l.Text.Trim() == "QI_INSERT_OK")) throw new Exception("Final Enter did not execute the command");
        session.Dispose(); await reader.WaitAsync(TimeSpan.FromSeconds(5));
        var closed = await session.WriteInputAsync("do not execute", true, default);
        if (closed.Success || closed.EnterAttempted) throw new Exception("Closed session reported success or attempted Enter");
    }
}
