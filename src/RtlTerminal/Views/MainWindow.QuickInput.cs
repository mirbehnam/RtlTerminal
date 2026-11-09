using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;

namespace RtlTerminal;

public partial class MainWindow
{
    private readonly Dictionary<string, QuickInputDraft> _quickDrafts = [];
    private readonly QuickInputImages _quickImages = new();
    private QuickInputWindow? _quickWindow;
    private bool _quickOpenQueued;
    private readonly KeyGesture _quickGesture = new(Key.Space, ModifierKeys.Control | ModifierKeys.Shift);
    private void InitializeQuickInput()
    {
        UpdateQuickInputIcon();
        Closed += (_, _) => { RetireQuickInputImages(); _quickDrafts.Clear(); };
        PreProcessInputEventHandler handler = (_, e) =>
        {
            if (e.StagingItem.Input is not KeyEventArgs key || (key.RoutedEvent != Keyboard.PreviewKeyDownEvent && key.RoutedEvent != Keyboard.KeyDownEvent) ||
                key.IsRepeat || !_quickGesture.Matches(this, key) || _quickWindow != null) return;
            var focused = Keyboard.FocusedElement as DependencyObject;
            var window = focused == null ? null : Window.GetWindow(focused);
            if (window != this && !(window == null && IsActive) || !IsEnabled) return;
            key.Handled = true; if (_quickOpenQueued) return; _quickOpenQueued = true;
            Dispatcher.BeginInvoke(DispatcherPriority.Input, new Action(() => { _quickOpenQueued = false; if (!_windowClosed) OpenQuickInput(); }));
        };
        InputManager.Current.PreProcessInput += handler;
        Closed += (_, _) => InputManager.Current.PreProcessInput -= handler;
        var tip = new StackPanel(); tip.Children.Add(new TextBlock { Text = UiText.Get("Quick Input"), FontWeight = FontWeights.SemiBold });
        tip.Children.Add(new TextBlock { Text = UiText.Get("Write, edit and insert text into the active terminal."), Margin = new Thickness(0, 3, 0, 3) });
        tip.Children.Add(new TextBlock { Text = "Ctrl+Shift+Space", Opacity = .65 });
        var tooltip = new ToolTip { Content = tip, Padding = new Thickness(9), BorderThickness = new Thickness(1) };
        tooltip.SetResourceReference(Control.BackgroundProperty, "MenuBackgroundBrush"); tooltip.SetResourceReference(Control.ForegroundProperty, "ChromeForegroundBrush"); tooltip.SetResourceReference(Control.BorderBrushProperty, "ToolbarBorderBrush");
        QuickInputButton.ToolTip = tooltip; ToolTipService.SetInitialShowDelay(QuickInputButton, 200);
    }
    private void UpdateQuickInputIcon()
    {
        QuickInputButton.Visibility = Visibility.Visible;
        QuickInputButton.Opacity = 1;
    }
    private void QuickInput_Click(object sender, RoutedEventArgs e) => OpenQuickInput();
    private void OpenQuickInput()
    {
        if (_quickWindow != null) { _quickWindow.Activate(); return; }
        var pane = ActivePane; var session = pane?.Session; var id = session?.SessionId ?? "no-session";
        if (!_quickDrafts.TryGetValue(id, out var draft)) _quickDrafts[id] = draft = new QuickInputDraft();
        draft.Title = pane?.Title ?? "Quick Input";
        draft.Editor.FontSize = Math.Max(17, (pane?.View.FontSize ?? 16) + 1);
        bool Valid() => pane != null && session != null && ReferenceEquals(pane.Session, session) && !pane.SessionEnded && _tabs.Any(t => t.Panes.Contains(pane));
        var activeIds = _tabs.SelectMany(t => t.Panes).Select(p => p.Session?.SessionId).ToHashSet();
        var orphans = _quickDrafts.Where(p => p.Key != id && !activeIds.Contains(p.Key)).Select(p => KeyValuePair.Create(p.Value.Title, p.Value));
        var dialog = new QuickInputWindow(draft, async send =>
        {
            if (session == null) return new(false, false, "No terminal was selected. Open a terminal, then recover this draft.");
            if (!Valid()) return new(false, false, "The destination terminal is no longer available. Your draft was preserved.");
            if (_macroPane == pane && (_macroCancellation != null || _macroPreparing)) return new(false, false, "Stop macro playback before inserting text.");
            var paths = new Dictionary<QuickInputAttachment, string>();
            foreach (var attachment in draft.Attachments())
            {
                if (!File.Exists(attachment.Path) && !Directory.Exists(attachment.Path)) return new(false, false, "An attached file is no longer available.");
                ShellPath.Validate(attachment.Path);
                paths[attachment] = pane!.Profile == TerminalProfile.Wsl ? WslPath.Quote(await WslPathResolver.ConvertAsync(attachment.Path, pane.WslDistribution))
                    : pane.Profile == TerminalProfile.GitBash ? WslPath.Quote(attachment.Path)
                    : ShellPath.Windows(attachment.Path, pane.Profile is TerminalProfile.PowerShell or TerminalProfile.PowerShell7, escapePercent: pane.Buffer?.QuickInputMode.BracketedPaste != true);
            }
            var text = draft.Text(a => paths[a]);
            if (text.Length == 0) return new(false, false, "Write some text before inserting it.");
            var mode = pane!.Buffer!.QuickInputMode;
            if (!mode.BracketedPaste && QuickInputText.NeedsSingleLine(text))
            {
                if (!await _quickWindow!.ConfirmSingleLineAsync())
                    return new(false, false, "Nothing was inserted. Your draft was preserved.");
                text = QuickInputText.SingleLine(text);
            }
            var prepared = QuickInputText.Prepare(text, mode.BracketedPaste);
            if (!Valid() || pane.Buffer.QuickInputMode.BracketedPaste != mode.BracketedPaste) return new(false, false, "The destination changed. Review it and try again.");
            pane.Buffer.InvalidateQuickInput();
            var result = await session!.WriteInputAsync(prepared, send, CancellationToken.None);
            if (result.Success || result.MayHaveWritten) pane.HasUserInput = true;
            if (!Valid()) return new(false, result.MayHaveWritten, "The destination terminal closed during transfer. Some input may have reached it. Your draft was preserved.");
            if (result.Success && _macroPane == pane && _macroRecorder is { } recorder)
                try { recorder.Record(prepared + (send ? "\r" : ""), MacroRecorder.Prompt(pane.Buffer.CaptureSnapshot())); } catch { RecordingFailed(); }
            return result.Success ? result : result with { Error = result.EnterAttempted ? "The final Enter may already have reached the terminal. Your draft was preserved; check the terminal before retrying." : result.MayHaveWritten ? "Some text may already have reached the terminal. No further Enter was sent. Your draft was preserved; check the terminal before retrying." : result.Error };
        }, _quickImages, () => Application.Current.Windows.OfType<MainWindow>().SelectMany(w => w._quickDrafts.Values).SelectMany(d => d.RetainedImages).ToHashSet(StringComparer.OrdinalIgnoreCase), orphans)
        { Owner = this, PlacementTarget = pane?.Frame ?? (FrameworkElement)this };
        _quickWindow = dialog;
        try { dialog.ShowDialog(); }
        finally
        {
            _quickWindow = null; if (dialog.Transferred) _quickDrafts.Remove(id);
            if (Valid()) { var tab = _tabs.First(t => t.Panes.Contains(pane!)); ShowTab(tab); ActivatePane(pane!); pane!.View.Focus(); }
        }
    }
}
