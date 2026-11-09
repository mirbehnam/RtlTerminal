using System.IO;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace RtlTerminal;

public partial class MainWindow
{
    private readonly MacroStore _macroStore = new();
    private MacroSettings _macroSettings = MacroSettings.Load();
    private MacroRecorder? _macroRecorder;
    private TerminalPane? _macroPane;
    private CancellationTokenSource? _macroCancellation;
    private bool _macroPreparing;
    private MacroPlayback? _macroPlayback;
    private bool _macroStepMode;

    private void SendUserInput(TerminalPane? pane, string input)
    {
        if (input.Any(c => char.IsControl(c) && c is not ('\t' or '\b'))) pane?.Buffer?.InvalidateQuickInput();
        if (pane?.Session is not { } session || pane.SessionEnded) return;
        if (_macroPane == pane && (_macroCancellation is not null || _macroPreparing)) return;
        if (_macroPane == pane && _macroRecorder is { } recorder)
        {
            try { recorder.Record(input, pane.Buffer is { } buffer ? MacroRecorder.Prompt(buffer.CaptureSnapshot()) : ""); }
            catch (Exception) { RecordingFailed(); }
        }
        if (input.Length > 0) pane.HasUserInput = true;
        session.Write(input);
    }

    private void RecordingFailed()
    {
        var recorder = _macroRecorder;
        _macroRecorder = null;
        if (_macroPane is { } pane) pane.Recorder = null;
        _macroPane = null; SetMacroStatus(null);
        Dispatcher.BeginInvoke(new Action(() =>
        {
            UiText.Show(this, "Recording stopped at its limit. Terminal input was still sent. You can save the captured steps.", "Macro recording");
            if (recorder?.Macro.Steps.Count > 0) EditMacro(recorder.Finish(), "");
        }));
    }

    private void SendUserKey(TerminalPane? pane, string input, Key key, ModifierKeys modifiers)
    {
        if (input.Any(c => char.IsControl(c) && c is not ('\t' or '\b'))) pane?.Buffer?.InvalidateQuickInput();
        if (pane?.Session is not { } session || pane.SessionEnded) return;
        if (_macroPane == pane && (_macroCancellation is not null || _macroPreparing)) return;
        var name = MacroKeys.Name(key, modifiers);
        if (_macroPane == pane && _macroRecorder is { } recorder && MacroKeys.ShouldRecord(name, _macroSettings))
        {
            try { recorder.RecordKey(input, pane.Buffer is { } buffer ? MacroRecorder.Prompt(buffer.CaptureSnapshot()) : "", name, MacroKeys.IsIndependent(name)); }
            catch { RecordingFailed(); }
        }
        if (input.Length > 0) pane.HasUserInput = true;
        session.Write(input);
    }

    private string EncodeMacroKey(TerminalPane pane, MacroStep step)
    {
        if (!MacroKeys.TryParse(step.KeyName, out var key, out var modifiers)) return step.Input;
        var control = modifiers.HasFlag(ModifierKeys.Control);
        if (control && key >= Key.A && key <= Key.Z) return ((char)(key - Key.A + 1)).ToString();
        if (control && key == Key.Space) return "\0";
        return GetTerminalKeySequence(key, modifiers.HasFlag(ModifierKeys.Shift), modifiers.HasFlag(ModifierKeys.Alt), control,
            pane.Buffer?.CaptureSnapshot().Modes.ApplicationCursorKeys == true) ?? step.Input;
    }

    private void SetMacroStatus(string? text)
    {
        MacroStatusButton.Content = _macroPlayback?.IsPaused == true ? UiText.Format("Ⅱ Paused · {0}", _macroPane?.Number) : UiText.Get(text ?? "");
        MacroStatusButton.Visibility = text is null ? Visibility.Collapsed : Visibility.Visible;
        MacroRecordMenuItem.SetCurrentValue(MenuItem.HeaderProperty, UiText.Get(_macroRecorder is null ? "_Start recording" : "_Stop recording..."));
        MacroRecordMenuItem.IsEnabled = _macroCancellation is null && !_macroPreparing;
        MacroStopMenuItem.IsEnabled = _macroCancellation is not null;
        MacroPauseButton.Visibility = MacroStepButton.Visibility = MacroStopButton.Visibility = _macroCancellation is null ? Visibility.Collapsed : Visibility.Visible;
        MacroPauseButton.Content = _macroPlayback?.IsPaused == true ? "▶" : "Ⅱ";
    }

    private void MacroRecord_Click(object sender, RoutedEventArgs e)
    {
        if (_macroCancellation is not null || _macroPreparing) return;
        if (_macroRecorder is null)
        {
            if (ActivePane?.Session is null || ActivePane.SessionEnded) return;
            _macroPane = ActivePane;
            _macroRecorder = new(_macroSettings.Mode, _macroSettings.DetectSensitive);
            _macroPane.Recorder = _macroRecorder;
            SetMacroStatus($"● Recording · pane {_macroPane.Number}");
            ActiveView.Focus();
            return;
        }
        if (_macroPane is { } recordingPane) recordingPane.Recorder = null;
        var macro = _macroRecorder.Finish();
        _macroRecorder = null; _macroPane = null; SetMacroStatus(null);
        if (macro.Steps.Count == 0) return;
        EditMacro(macro, "");
    }

    private void EditMacro(TerminalMacro macro, string password)
    {
        // Keep the recording available if saving fails; never discard it on an I/O error.
        while (true)
        {
            int[] occupied = [];
            try { occupied = _macroStore.List().Where(e => e.Id != macro.Id && e.Shortcut > 0).Select(e => e.Shortcut).ToArray(); }
            catch { /* Save reports storage errors without discarding this recording. */ }
            var editor = new MacroEditorWindow(macro, password, _macroSettings.AskSensitive, occupied) { Owner = this };
            if (editor.ShowDialog() != true) return;
            password = editor.Password;
            try { _macroStore.Save(macro, password, _macroSettings.AskSensitive); return; }
            catch (Exception ex) { UiText.Show(this, ex is System.Security.Cryptography.CryptographicException ? "Could not encrypt the macro." : ex.Message, "Save macro"); }
        }
    }

    private void MacroStop_Click(object sender, RoutedEventArgs e) => _macroCancellation?.Cancel();
    private void MacroPause_Click(object sender, RoutedEventArgs e)
    {
        if (_macroPlayback is not { } playback) return;
        if (playback.IsPaused) playback.Resume(); else playback.Pause();
        SetMacroStatus(playback.IsPaused ? "Ⅱ Paused" : "▶ Running");
    }
    private void MacroStep_Click(object sender, RoutedEventArgs e)
    { _macroPlayback?.Step(); SetMacroStatus("▶ One step"); }
    private void MacroStatus_Click(object sender, RoutedEventArgs e)
    {
        if (_macroRecorder is not null) MacroRecord_Click(sender, e);
        else MacroPause_Click(sender, e);
    }
    private void MacroSettings_Click(object sender, RoutedEventArgs e) => ShowSettings(false, true);

    private void MacroMenu_Opened(object sender, RoutedEventArgs e)
    {
        if (e.OriginalSource != sender) return;
        MacroRunMenu.Items.Clear();
        try
        {
            foreach (var entry in _macroStore.List().OrderBy(e => e.CreatedAt).ThenBy(e => e.Id))
            {
                var item = new MenuItem { Header = entry.ToString(), IsEnabled = _macroRecorder is null && _macroCancellation is null && !_macroPreparing };
                item.Click += async (_, _) => await RunMacro(entry);
                MacroRunMenu.Items.Add(item);
            }
            if (MacroRunMenu.Items.Count == 0) MacroRunMenu.Items.Add(new MenuItem { Header = UiText.Get("No recorded macros"), IsEnabled = false });
        }
        catch { MacroRunMenu.Items.Add(new MenuItem { Header = UiText.Get("Unable to read macros — open Manage"), IsEnabled = false }); }
    }

    private async Task RunMacro(MacroEntry entry)
    {
        var stepMode = _macroStepMode; _macroStepMode = false;
        if (_macroRecorder is not null || _macroCancellation is not null || _macroPreparing || ActivePane?.Session is null) return;
        var pane = ActivePane;
        var session = pane.Session;
        _macroPane = pane; _macroPreparing = true; SetMacroStatus("Unlocking macro…");
        try
        {
            var password = entry.PasswordProtected ? MacroEditorWindow.Unlock(this, entry.Name) : "";
            if (password is null) return;
            var macro = await Task.Run(() => _macroStore.Load(entry.Id, password));
            macro = MacroPrivacy.Prepare(macro, _macroSettings.AskSensitive);
            if (pane.Session != session || pane.SessionEnded) throw new IOException("The target terminal has closed.");
            _macroPane = pane;
            using var cancellation = new CancellationTokenSource();
            _macroCancellation = cancellation;
            var playback = new MacroPlayback(); pane.Playback = playback;
            if (pane.Buffer is { } currentBuffer) playback.Observe(currentBuffer.CaptureSnapshot());
            _macroPlayback = playback;
            if (stepMode) playback.Pause();
            SetMacroStatus($"▶ Running · pane {pane.Number}");
            await playback.RunAsync(macro, input =>
            {
                if (pane.Session != session || pane.SessionEnded) throw new IOException("The target terminal has closed.");
                if (input.Length > 0) pane.HasUserInput = true;
                if (input.Any(c => char.IsControl(c) && c is not ('\t' or '\b'))) pane.Buffer?.InvalidateQuickInput();
                session.Write(input);
            }, _macroSettings.TimeoutSeconds, (step, waiting) =>
                SetMacroStatus($"{(waiting ? "◷" : "▶")} {step}/{macro.Steps.Count} · {pane.Number}"), cancellation.Token,
                (number, step) =>
                {
                    SetMacroStatus($"! Step {number} needs review");
                    var result = MacroInteractionWindow.Recover(this, number, step.ExpectedPrompt);
                    if (result is null) return Task.FromResult(false);
                    step.ExpectedPrompt = result;
                    return Task.FromResult(true);
                }, step => Task.FromResult(MacroInteractionWindow.Request(this,
                    step.VariableName.Length > 0 ? step.VariableName : "Input for this step", step.IsSensitive)),
                step => EncodeMacroKey(pane, step));
        }
        catch (OperationCanceledException) { }
        catch (System.Security.Cryptography.CryptographicException) { UiText.Show(this, "Incorrect password or damaged macro file.", "Macro"); }
        catch (Exception ex) { UiText.Show(this, ex.Message, "Macro stopped"); }
        finally
        {
            pane.Playback = null; _macroPlayback = null; _macroCancellation = null; _macroPane = null; _macroPreparing = false; _macroStepMode = false; SetMacroStatus(null);
        }
    }

    private async void MacroManage_Click(object sender, RoutedEventArgs e)
    {
        if (_macroRecorder is not null || _macroCancellation is not null || _macroPreparing) return;
        var window = new Window { Owner = this, Title = UiText.Get("Recorded macros"), Width = 620, Height = 400, MinWidth = 450, MinHeight = 250, WindowStartupLocation = WindowStartupLocation.CenterOwner };
        window.Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(244,245,247));
        window.Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(24,32,51));
        var root = new DockPanel { Margin = new Thickness(16) }; window.Content = root;
        var searchPanel = new StackPanel(); DockPanel.SetDock(searchPanel, Dock.Top); root.Children.Add(searchPanel);
        searchPanel.Children.Add(new TextBlock { Text = UiText.Get("Recorded macros"), FontSize = 22, FontWeight = FontWeights.SemiBold });
        searchPanel.Children.Add(new TextBlock { Text = UiText.Get("Search by name"), Margin = new Thickness(0,12,0,4) });
        var search = new TextBox { Margin = new Thickness(0,0,0,12) }; searchPanel.Children.Add(search);
        var buttons = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0,12,0,0) }; DockPanel.SetDock(buttons, Dock.Bottom); root.Children.Add(buttons);
        var list = new ListBox(); root.Children.Add(list);
        bool Refresh()
        {
            try { list.ItemsSource = _macroStore.List().Where(m => m.Name.Contains(search.Text.Trim(), StringComparison.CurrentCultureIgnoreCase)).ToArray(); return true; }
            catch { UiText.Show(this, "Unable to read macro storage. A file may be damaged or belong to another Windows account.", "Macros"); return false; }
        }
        if (!Refresh()) return;
        search.TextChanged += (_, _) => Refresh();
        MacroEntry? selectedRun = null;
        foreach (var label in new[] { "Run", "Step through", "Edit / password…", "Delete" })
        {
            var button = new Button { Content = UiText.Get(label), Padding = new Thickness(12,6,12,6), Margin = new Thickness(0,0,8,0), IsEnabled = false }; buttons.Children.Add(button);
            list.SelectionChanged += (_, _) => button.IsEnabled = list.SelectedItem is MacroEntry;
            button.Click += (_, _) =>
            {
                if (list.SelectedItem is not MacroEntry entry) return;
                if (label is "Run" or "Step through") { _macroStepMode = label == "Step through"; selectedRun = entry; window.Close(); return; }
                try
                {
                    if (label == "Delete")
                    {
                        if (UiText.Show(window, $"Delete '{entry.Name}'?", "Delete macro", MessageBoxButton.YesNo) == MessageBoxResult.Yes) _macroStore.Delete(entry.Id);
                    }
                    else
                    {
                        var password = entry.PasswordProtected ? MacroEditorWindow.Unlock(window, entry.Name) : "";
                        if (password is null) return;
                        var macro = _macroStore.Load(entry.Id, password);
                        EditMacro(macro, password);
                    }
                    Refresh();
                }
                catch (System.Security.Cryptography.CryptographicException) { UiText.Show(window, "Incorrect password or damaged macro file.", "Macros"); }
                catch (Exception ex) { UiText.Show(window, ex.Message, "Macros"); }
            };
        }
        window.ShowDialog();
        if (selectedRun is not null) await RunMacro(selectedRun);
    }

    private bool HandleMacroKey(KeyEventArgs e)
    {
        if (_macroSettings.PlaybackShortcuts && Keyboard.Modifiers == (ModifierKeys.Control | ModifierKeys.Shift | ModifierKeys.Alt) && _macroCancellation is not null)
        {
            if (GetEffectiveKey(e) == Key.End) { _macroCancellation.Cancel(); return true; }
            if (GetEffectiveKey(e) == Key.P) { MacroPause_Click(this, new RoutedEventArgs()); return true; }
        }
        if (!_macroSettings.Shortcuts || Keyboard.Modifiers != (ModifierKeys.Control | ModifierKeys.Shift | ModifierKeys.Alt)) return false;
        var key = GetEffectiveKey(e);
        if (key < Key.D1 || key > Key.D9) return false;
        try
        {
            var entry = _macroStore.List().FirstOrDefault(m => m.Shortcut == key - Key.D0);
            if (entry is null) return false;
            _ = RunMacro(entry); return true;
        }
        catch { return false; }
    }
}
