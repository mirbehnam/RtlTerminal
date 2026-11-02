using System.IO;
using System.Text.Json;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using Microsoft.Win32;
using Path = System.Windows.Shapes.Path;

namespace RtlTerminal;

public sealed class QuickInputWindow : Window
{
    public const string ClipboardFormat = "RtlTerminal.QuickInput.v1";
    public const string IconData = "M2,2 H14 V14 H2 Z M5,5 H11 M5,8 H11 M5,11 H8";
    private readonly QuickInputDraft draft;
    private readonly Func<bool, Task<TerminalInputResult>> transfer;
    private readonly QuickInputImages images;
    private readonly Func<IReadOnlySet<string>> pinned;
    private readonly TextBlock error = new() { TextWrapping = TextWrapping.Wrap, Foreground = Brushes.IndianRed, Visibility = Visibility.Collapsed, Margin = new Thickness(10, 4, 10, 4) };
    private readonly List<Button> actions = [];
    private readonly Border editorHost;
    private bool busy;
    private bool pasting;
    private readonly DockPanel contentRoot;
    private readonly Border confirmationOverlay;
    private readonly Button rtlButton;
    private readonly Button sendAction, insertAction;
    private TaskCompletionSource<bool>? confirmation;
    private bool rtl = true;
    private readonly TextBlock placeholder = new() { Text = "Write your text here\nInsert adds text without submitting\nSend adds text and presses Enter", FontSize = 15, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(14, 12, 28, 0), VerticalAlignment = VerticalAlignment.Top, IsHitTestVisible = false, Opacity = .7 };
    private readonly TextBlock statistics = new() { FontSize = 11, VerticalAlignment = VerticalAlignment.Bottom, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(3, 0, 10, 3) };
    private readonly System.Windows.Threading.DispatcherTimer statisticsTimer = new(System.Windows.Threading.DispatcherPriority.Background) { Interval = TimeSpan.FromMilliseconds(180) };
    public bool Transferred { get; private set; }
    public QuickInputWindow(QuickInputDraft draft, Func<bool, Task<TerminalInputResult>> transfer, QuickInputImages images, Func<IReadOnlySet<string>> pinned,
        IEnumerable<KeyValuePair<string, QuickInputDraft>>? recover = null)
    {
        this.draft = draft; this.transfer = transfer; this.images = images; this.pinned = pinned;
        Title = "Quick Input"; Width = 800; Height = 420; MinWidth = 380; MinHeight = 240; ShowInTaskbar = false;
        WindowStyle = WindowStyle.None; ResizeMode = ResizeMode.CanResizeWithGrip; AllowsTransparency = true; Background = Brushes.Transparent;
        WindowStartupLocation = WindowStartupLocation.CenterOwner; UseLayoutRounding = true;
        using (var settings = Registry.CurrentUser.OpenSubKey(@"Software\RtlTerminal"))
        { if (settings?.GetValue("QuickInputWidth") is int w && w >= 380) Width = w; if (settings?.GetValue("QuickInputHeight") is int h && h >= 240) Height = h; rtl = settings?.GetValue("QuickInputRtl") is not int direction || direction != 0; }
        Resources.MergedDictionaries.Add(new() { Source = new Uri("/RtlTerminal;component/Views/ProxyManagerStyles.xaml", UriKind.Relative) });
        var frame = new Border { CornerRadius = new CornerRadius(10), BorderThickness = new Thickness(1), Padding = new Thickness(1),
            Effect = new System.Windows.Media.Effects.DropShadowEffect { BlurRadius = 12, ShadowDepth = 2, Opacity = .22 } };
        frame.SetResourceReference(Border.BackgroundProperty, "MenuBackgroundBrush"); frame.SetResourceReference(Border.BorderBrushProperty, "ToolbarBorderBrush"); Content = frame;
        var layers = new Grid(); frame.Child = layers;
        var root = new DockPanel(); contentRoot = root; layers.Children.Add(root);
        confirmationOverlay = new Border { Visibility = Visibility.Collapsed, Padding = new Thickness(30), CornerRadius = new CornerRadius(9) };
        confirmationOverlay.SetResourceReference(Border.BackgroundProperty, "MenuBackgroundBrush"); layers.Children.Add(confirmationOverlay);
        var header = new DockPanel { Height = 36, LastChildFill = true, Background = Brushes.Transparent, Margin = new Thickness(10, 0, 5, 0) };
        header.MouseLeftButtonDown += (_, e) => { if (e.OriginalSource is TextBlock or DockPanel) DragMove(); };
        DockPanel.SetDock(header, Dock.Top); root.Children.Add(header);
        var close = Button("", () => Close()); close.MinWidth = 28; close.Width = 28; close.Height = 26; close.Padding = new Thickness(7); close.VerticalAlignment = VerticalAlignment.Center;
        var cross = new Path { Data = Geometry.Parse("M1,1 L9,9 M9,1 L1,9"), StrokeThickness = 1.35, StrokeStartLineCap = PenLineCap.Round, StrokeEndLineCap = PenLineCap.Round, Stretch = Stretch.Uniform, Width = 10, Height = 10 };
        cross.SetBinding(Shape.StrokeProperty, new System.Windows.Data.Binding("Foreground") { Source = close }); close.Content = cross;
        close.SetResourceReference(Control.BackgroundProperty, "MenuBackgroundBrush"); close.BorderThickness = new Thickness(0);
        System.Windows.Automation.AutomationProperties.SetName(close, UiText.Get("Close")); DockPanel.SetDock(close, Dock.Right); header.Children.Add(close);
        close.ToolTip = "Escape";
        rtlButton = Button("RTL", ToggleRtl); rtlButton.MinWidth = 42; rtlButton.Height = 24; rtlButton.FontSize = 11;
        rtlButton.Padding = new Thickness(7, 1, 7, 1); rtlButton.VerticalAlignment = VerticalAlignment.Center;
        rtlButton.ToolTip = "Right-to-left writing direction"; DockPanel.SetDock(rtlButton, Dock.Right); header.Children.Add(rtlButton);
        ApplyDirection();
        var orphaned = recover?.ToArray() ?? [];
        if (orphaned.Length > 0)
        {
            var recovery = Button("Recover draft", () => { }); DockPanel.SetDock(recovery, Dock.Right); header.Children.Add(recovery);
            var menu = new ContextMenu();
            foreach (var item in orphaned)
            {
                var entry = new MenuItem { Header = item.Key };
                entry.Click += (_, _) => { try { draft.Recover(item.Value); } catch (Exception ex) when (ex is ArgumentException or IOException) { ShowError(ex.Message); } };
                menu.Items.Add(entry);
            }
            recovery.Click += (_, _) => { recovery.ContextMenu = menu; menu.IsOpen = true; };
        }
        var icon = new Path { Data = Geometry.Parse(IconData), Width = 16, Height = 16, Stretch = Stretch.Uniform, Margin = new Thickness(0, 0, 8, 0) };
        icon.SetResourceReference(Shape.StrokeProperty, "ChromeForegroundBrush"); DockPanel.SetDock(icon, Dock.Left); header.Children.Add(icon);
        var title = new TextBlock { Text = "Quick Input", FontWeight = FontWeights.SemiBold, VerticalAlignment = VerticalAlignment.Center };
        title.SetResourceReference(TextBlock.ForegroundProperty, "ChromeForegroundBrush"); header.Children.Add(title);
        title.ToolTip = draft.Title;
        var footer = new StackPanel { Margin = new Thickness(8, 4, 8, 8) }; DockPanel.SetDock(footer, Dock.Bottom); root.Children.Add(footer); footer.Children.Add(error);
        var footerRow = new Grid(); footerRow.ColumnDefinitions.Add(new()); footerRow.ColumnDefinitions.Add(new() { Width = GridLength.Auto }); footer.Children.Add(footerRow);
        statistics.SetResourceReference(TextBlock.ForegroundProperty, "MutedForegroundBrush"); footerRow.Children.Add(statistics);
        var buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right }; Grid.SetColumn(buttons, 1); footerRow.Children.Add(buttons);
        buttons.Children.Add(FooterAction("Close", "Escape", Close));
        var insertGroup = FooterAction("Insert", "Ctrl+Shift+Enter", () => _ = Transfer(false)); insertAction = (Button)insertGroup.Children[0]; buttons.Children.Add(insertGroup);
        var sendGroup = FooterAction("Send", "Ctrl+Enter", () => _ = Transfer(true)); var send = (Button)sendGroup.Children[0];
        sendAction = send;
        send.SetResourceReference(Control.BackgroundProperty, "AccentBrush"); send.SetResourceReference(Control.ForegroundProperty, "TerminalBackgroundBrush"); buttons.Children.Add(sendGroup);
        var editorLayer = new Grid(); editorLayer.Children.Add(draft.Editor); editorLayer.Children.Add(placeholder);
        placeholder.SetResourceReference(TextBlock.ForegroundProperty, "MutedForegroundBrush");
        draft.Editor.SetResourceReference(Control.BackgroundProperty, "DirectoryBadgeBrush");
        editorHost = new Border { Margin = new Thickness(8, 0, 8, 0), CornerRadius = new CornerRadius(6), Child = editorLayer }; root.Children.Add(editorHost);
        statisticsTimer.Tick += (_, _) => { statisticsTimer.Stop(); UpdateStatistics(); };
        draft.Editor.TextChanged += EditorTextChanged; UpdateStatistics();
        UiText.Changed += UpdateStatistics;
        DataObject.AddPastingHandler(draft.Editor, Paste);
        CommandManager.AddPreviewExecutedHandler(draft.Editor, CopyCut);
        PreviewKeyDown += HandleEditorKey;
        Closing += (_, e) => { if (busy) e.Cancel = true; };
        Loaded += (_, _) =>
        {
            if (Owner != null) WindowWorkArea.PlaceDialog(this, PlacementTarget ?? Owner);
            draft.Editor.Focus();
        };
        Closed += (_, _) =>
        {
            statisticsTimer.Stop(); draft.Editor.TextChanged -= EditorTextChanged;
            UiText.Changed -= UpdateStatistics;
            editorLayer.Children.Remove(draft.Editor); editorHost.Child = null; DataObject.RemovePastingHandler(draft.Editor, Paste); CommandManager.RemovePreviewExecutedHandler(draft.Editor, CopyCut);
            try { if (ActualWidth >= MinWidth && ActualHeight >= MinHeight) { using var key = Registry.CurrentUser.CreateSubKey(@"Software\RtlTerminal"); key.SetValue("QuickInputWidth", (int)ActualWidth); key.SetValue("QuickInputHeight", (int)ActualHeight); } } catch (UnauthorizedAccessException) { } catch (IOException) { }
        };
        ProxyUiLanguage.Attach(this);
    }
    public FrameworkElement? PlacementTarget { get; set; }
    private StackPanel FooterAction(string label, string shortcut, Action click)
    {
        var group = new StackPanel { Margin = new Thickness(5, 2, 0, 0) };
        var button = Button(label, click); button.MinWidth = 96; button.Height = 36; button.FontSize = 13; button.Padding = new Thickness(17, 7, 17, 7); button.Margin = new Thickness(0);
        group.Children.Add(button);
        var keys = new TextBlock { Text = shortcut, FontSize = 10, HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0, 4, 0, 0), FlowDirection = FlowDirection.LeftToRight };
        keys.SetResourceReference(TextBlock.ForegroundProperty, "MutedForegroundBrush"); group.Children.Add(keys); return group;
    }
    private void EditorTextChanged(object sender, TextChangedEventArgs e)
    { placeholder.Visibility = Visibility.Collapsed; statisticsTimer.Stop(); statisticsTimer.Start(); }
    private void UpdateStatistics()
    {
        var text = draft.Text(_ => "\ufffc"); var characters = 0; var words = 0; var inWord = false;
        foreach (var rune in text.EnumerateRunes())
        {
            characters++;
            if (System.Text.Rune.IsWhiteSpace(rune)) inWord = false;
            else if (!inWord) { words++; inWord = true; }
        }
        statistics.Text = characters.ToString("N0") + " " + UiText.Get("characters") + " · " + words.ToString("N0") + " " + UiText.Get("words");
        placeholder.Visibility = text.Length == 0 ? Visibility.Visible : Visibility.Collapsed;
    }
    private Button Button(string text, Action click)
    {
        var button = new Button { Content = text, Padding = new Thickness(14, 5, 14, 5), Margin = new Thickness(4, 0, 0, 0), MinWidth = 68 };
        button.Click += (_, _) => click(); actions.Add(button);
        button.ToolTip = text switch { "Send" => "Ctrl+Enter", "Insert" => "Ctrl+Shift+Enter", "Close" => "Escape", _ => null };
        ToolTipService.SetInitialShowDelay(button, 200); return button;
    }
    private void ToggleRtl()
    {
        rtl = !rtl; ApplyDirection();
        try { using var key = Registry.CurrentUser.CreateSubKey(@"Software\RtlTerminal"); key.SetValue("QuickInputRtl", rtl ? 1 : 0); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Security.SecurityException) { ShowError("The writing direction could not be saved."); }
        draft.Editor.Focus();
    }
    private void ApplyDirection()
    {
        var direction = rtl ? FlowDirection.RightToLeft : FlowDirection.LeftToRight;
        placeholder.FlowDirection = direction; placeholder.TextAlignment = rtl ? TextAlignment.Right : TextAlignment.Left;
        draft.Editor.FlowDirection = direction; draft.Editor.Document.FlowDirection = direction;
        foreach (var paragraph in draft.Editor.Document.Blocks.OfType<Paragraph>()) paragraph.FlowDirection = direction;
        rtlButton.SetResourceReference(Control.BackgroundProperty, rtl ? "AccentBrush" : "DirectoryBadgeBrush");
        rtlButton.SetResourceReference(Control.ForegroundProperty, rtl ? "TerminalBackgroundBrush" : "ChromeForegroundBrush");
        System.Windows.Automation.AutomationProperties.SetName(rtlButton, "RTL " + (rtl ? "On" : "Off"));
    }
    public Task<bool> ConfirmSingleLineAsync()
    {
        if (confirmation != null) return confirmation.Task;
        confirmation = new(TaskCreationOptions.RunContinuationsAsynchronously);
        var body = new StackPanel { VerticalAlignment = VerticalAlignment.Center, MaxWidth = 600 };
        body.Children.Add(new TextBlock { Text = "Multiline input", FontSize = 20, FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 0, 0, 16), TextWrapping = TextWrapping.Wrap });
        body.Children.Add(new TextBlock { Text = "This terminal has not enabled safe multiline paste. Insert the text as one line? Line breaks become spaces and tabs become four spaces. Your original draft is preserved.", FontSize = 15, TextWrapping = TextWrapping.Wrap });
        var buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 24, 0, 0) };
        var cancel = new Button { Content = "✕  Cancel", Padding = new Thickness(16, 8, 16, 8), Margin = new Thickness(0, 0, 10, 0) };
        var accept = new Button { Content = "✓  Confirm", Padding = new Thickness(16, 8, 16, 8) };
        accept.SetResourceReference(Control.BackgroundProperty, "AccentBrush"); accept.SetResourceReference(Control.ForegroundProperty, "TerminalBackgroundBrush");
        cancel.Click += (_, _) => CompleteConfirmation(false); accept.Click += (_, _) => CompleteConfirmation(true);
        buttons.Children.Add(cancel); buttons.Children.Add(accept); body.Children.Add(buttons);
        body.SetResourceReference(TextBlock.ForegroundProperty, "ChromeForegroundBrush");
        KeyboardNavigation.SetTabNavigation(confirmationOverlay, KeyboardNavigationMode.Cycle);
        confirmationOverlay.Child = new ScrollViewer { Content = body, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
        contentRoot.IsEnabled = false; confirmationOverlay.Visibility = Visibility.Visible; cancel.Focus();
        return confirmation.Task;
    }
    private void CompleteConfirmation(bool accepted)
    {
        var pending = confirmation; confirmation = null;
        confirmationOverlay.Visibility = Visibility.Collapsed; confirmationOverlay.Child = null; contentRoot.IsEnabled = true;
        pending?.TrySetResult(accepted); draft.Editor.Focus();
    }
    public void ShowError(string text) { error.Text = text; error.Visibility = Visibility.Visible; }
    private async Task Transfer(bool send)
    {
        if (busy || pasting) return; busy = true;
        foreach (var button in actions) button.IsEnabled = false; draft.Editor.IsReadOnly = true;
        try
        {
            var result = await transfer(send);
            if (result.Success) { Transferred = true; busy = false; Close(); }
            else ShowError(result.Error ?? "Could not transfer text. Your draft was preserved.");
        }
        catch (Exception ex) when (ex is IOException or ArgumentException or InvalidOperationException or UnauthorizedAccessException)
        { ShowError(ex.Message); }
        finally { busy = false; draft.Editor.IsReadOnly = false; foreach (var button in actions) button.IsEnabled = true; }
    }
    private void HandleEditorKey(object sender, KeyEventArgs e)
    {
        if (confirmation != null)
        {
            if (e.Key == Key.Escape) { e.Handled = true; CompleteConfirmation(false); }
            else if (e.Key == Key.Enter && Keyboard.Modifiers.HasFlag(ModifierKeys.Control) || e.Key == Key.Space && Keyboard.Modifiers == (ModifierKeys.Control | ModifierKeys.Shift)) e.Handled = true;
            return;
        }
        if (e.Key == Key.Escape) { e.Handled = true; if (!busy) Close(); return; }
        if (InvokeTransferShortcut(e.Key == Key.System ? e.SystemKey : e.Key, e.KeyboardDevice.Modifiers)) { e.Handled = true; return; }
        if (e.Key == Key.Space && Keyboard.Modifiers == (ModifierKeys.Control | ModifierKeys.Shift)) { e.Handled = true; draft.Editor.Focus(); }
        if (e.Key == Key.L && Keyboard.Modifiers == (ModifierKeys.Control | ModifierKeys.Shift)) e.Handled = true;
    }
    private bool InvokeTransferShortcut(Key key, ModifierKeys modifiers)
    {
        if (key != Key.Enter) return false;
        var action = modifiers == ModifierKeys.Control ? sendAction : modifiers == (ModifierKeys.Control | ModifierKeys.Shift) ? insertAction : null;
        if (action == null) return false;
        if (!busy && !pasting && confirmation == null && action.IsEnabled)
            action.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Primitives.ButtonBase.ClickEvent));
        return true;
    }
    private void CopyCut(object sender, ExecutedRoutedEventArgs e)
    {
        if (e.Command != ApplicationCommands.Copy && e.Command != ApplicationCommands.Cut || draft.Editor.Selection.IsEmpty || busy) return;
        e.Handled = true;
        try
        {
            var values = new Dictionary<string, QuickInputAttachment>();
            var marked = draft.Text(a => { var marker = "[rtl-" + Guid.NewGuid().ToString("N") + "]"; values[marker] = a; return marker; }, draft.Editor.Selection.Start, draft.Editor.Selection.End);
            var data = new DataObject(); data.SetText(draft.Text(a => a.Path, draft.Editor.Selection.Start, draft.Editor.Selection.End));
            data.SetData(ClipboardFormat, JsonSerializer.Serialize(new ClipboardParts(marked, values))); Clipboard.SetDataObject(data, true);
            if (e.Command == ApplicationCommands.Cut) draft.Editor.Selection.Text = "";
        }
        catch (System.Runtime.InteropServices.COMException) { ShowError("The clipboard is busy. Please try again."); }
    }
    private sealed record ClipboardParts(string Text, Dictionary<string, QuickInputAttachment> Attachments);
    private async void Paste(object sender, DataObjectPastingEventArgs e)
    {
        e.CancelCommand(); if (busy || pasting) return;
        pasting = true; draft.Editor.IsReadOnly = true; foreach (var button in actions) button.IsEnabled = false;
        try
        {
            if (e.SourceDataObject.GetData(ClipboardFormat) is string internalClip && internalClip.Length <= 2_000_000)
            {
                var clip = JsonSerializer.Deserialize<ClipboardParts>(internalClip) ?? throw new ArgumentException("Invalid clipboard content.");
                if (clip.Text == null || clip.Attachments == null || clip.Attachments.Count > 1000 ||
                    clip.Attachments.Keys.Any(k => !System.Text.RegularExpressions.Regex.IsMatch(k, @"^\[rtl-[a-f0-9]{32}\]$")))
                    throw new ArgumentException("Invalid clipboard content.");
                foreach (var value in clip.Attachments.Values)
                {
                    if (value == null) throw new ArgumentException("Invalid clipboard content.");
                    ShellPath.Validate(value.Path);
                    if (!File.Exists(value.Path) && !Directory.Exists(value.Path)) throw new IOException("An attached file is no longer available.");
                }
                var remaining = clip.Text;
                while (remaining.Length > 0)
                {
                    var next = clip.Attachments.Select(p => (Pair: p, Index: remaining.IndexOf(p.Key, StringComparison.Ordinal))).Where(p => p.Index >= 0).OrderBy(p => p.Index).FirstOrDefault();
                    if (next.Pair.Key == null) { draft.InsertText(remaining); break; }
                    draft.InsertText(remaining[..next.Index]); ShellPath.Validate(next.Pair.Value.Path);
                    if (!File.Exists(next.Pair.Value.Path) && !Directory.Exists(next.Pair.Value.Path)) throw new IOException("An attached file is no longer available.");
                    draft.InsertAttachment(next.Pair.Value.Path, next.Pair.Value.Image); remaining = remaining[(next.Index + next.Pair.Key.Length)..];
                }
            }
            else if (e.SourceDataObject.GetData(DataFormats.FileDrop) is string[] files)
            {
                foreach (var file in files) { ShellPath.Validate(file); draft.InsertAttachment(System.IO.Path.GetFullPath(file), false); }
            }
            else if (e.SourceDataObject.GetData(DataFormats.Bitmap) is System.Windows.Media.Imaging.BitmapSource bitmap)
            {
                if ((long)bitmap.PixelWidth * bitmap.PixelHeight > 50_000_000) throw new ArgumentException("The clipboard image is too large. Use an image file instead.");
                var frozen = bitmap.Clone(); frozen.Freeze(); var retained = pinned();
                var path = await Task.Run(() => images.Save(frozen, retained));
                if (IsVisible) draft.InsertAttachment(path, true);
            }
            else if (e.SourceDataObject.GetData(DataFormats.UnicodeText) is string text) draft.InsertText(text);
        }
        catch (Exception ex) when (ex is IOException or ArgumentException or JsonException or System.Runtime.InteropServices.COMException or UnauthorizedAccessException or NotSupportedException)
        { ShowError(ex is System.Runtime.InteropServices.COMException ? "The clipboard is busy. Please try again." : ex.Message); }
        finally { pasting = false; draft.Editor.IsReadOnly = false; foreach (var button in actions) button.IsEnabled = true; }
    }
}
