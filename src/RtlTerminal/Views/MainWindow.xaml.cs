using System;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Controls;
using System.Windows.Threading;
using Microsoft.Win32;

namespace RtlTerminal;

public partial class MainWindow : Window
{
    private readonly object _renderLock = new();
    private readonly DispatcherTimer _renderTimer;
    private readonly DispatcherTimer _resizeTimer;
    private readonly List<TerminalTab> _tabs = [];
    private readonly Dictionary<PaneLayout, Grid> _paneLayoutGrids = [];
    private readonly List<string> _temporaryClipboardFiles = [];
    private TerminalTab? _activeTab;
    private bool _renderStartQueued;
    private bool _updatingContextMenuItem;
    private double _cellWidth = 8.5;
    private double _lineHeight = 18;
    private bool _restoringScrollPosition;
    private int _nextTabNumber = 1;
    private TerminalProfile _defaultProfile = TerminalProfile.CommandPrompt;
    private string? _defaultWslDistribution;
    private int _historySize = 2000;
    private PaneShortcutSettings _paneShortcuts = AppSettings.LoadPaneShortcuts();
    private HtmlPreviewScope _previewScope = AppSettings.LoadPreviewScope();
    private bool _updateCheckInProgress;
    private bool _suppressRightMouseUp;
    private Key? _pendingContextMenuKey;
    private (int X, int Y, int Button)? _lastReportedMouseCell;

    public MainWindow() : this(new DirectoryHistoryStore()) { }

    public MainWindow(DirectoryHistoryStore directoryHistory)
    {
        _directoryHistory = directoryHistory;
        InitializeComponent();
        InitializeDirectoryHistory();
        WireTerminalView(TerminalTextBox);
        UpdateShortcutLabels();
        LocationChanged += (_, _) => UpdatePaneSizing();
        SmartRtlMenuItem.IsChecked = true;
        _renderTimer = new DispatcherTimer(DispatcherPriority.Background)
        {
            Interval = TimeSpan.FromMilliseconds(16)
        };
        _renderTimer.Tick += RenderTimer_Tick;
        _resizeTimer = new DispatcherTimer(DispatcherPriority.Background) { Interval = TimeSpan.FromMilliseconds(80) };
        _resizeTimer.Tick += (_, _) => { _resizeTimer.Stop(); ApplyViewportResize(); };
        _defaultProfile = LoadDefaultProfile();
        _defaultWslDistribution = AppSettings.LoadWslDistribution() ?? WslProfiles.DefaultDistribution();
        _historySize = AppSettings.LoadHistorySize();
ApplySavedFontSettings();
        UpdateFontMetrics();
        InitializeUiLanguage();
        InitializeWindowStatus();
        AllowDrop = true;
        PreviewDragOver += Window_PreviewDragOver;
        PreviewDrop += Window_PreviewDrop;
    }

    private static TerminalPane? FindDropPane(DependencyObject? source)
    {
        while (source is not null)
        {
            if (source is TerminalView { Tag: TerminalPane pane }) return pane;
            source = source is Visual or System.Windows.Media.Media3D.Visual3D
                ? VisualTreeHelper.GetParent(source) : LogicalTreeHelper.GetParent(source);
        }
        return null;
    }

    private void Window_PreviewDragOver(object sender, DragEventArgs e)
    {
        var pane = FindDropPane(e.OriginalSource as DependencyObject);
        e.Effects = pane is { IsShown: true, Session: not null } && IsSupportedDrop(e.Data)
            ? e.AllowedEffects & DragDropEffects.Copy : DragDropEffects.None;
        e.Handled = true;
    }

    private async void Window_PreviewDrop(object sender, DragEventArgs e)
    {
        e.Handled = true;
        var pane = FindDropPane(e.OriginalSource as DependencyObject);
        if (pane is not { IsShown: true, Session: not null }) return;
        Activate(); ActivatePane(pane);
        try { await DropIntoPaneAsync(pane, e.Data); }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or
            System.Runtime.InteropServices.COMException or ArgumentException or NotSupportedException or System.ComponentModel.Win32Exception)
        { UiText.Show(this, exception.Message, "Drop", MessageBoxButton.OK, MessageBoxImage.Error); }
        if (ReferenceEquals(ActivePane, pane)) pane.View.Focus();
    }

    private async Task DropIntoPaneAsync(TerminalPane pane, IDataObject data)
    {
        // Read IDataObject on the UI thread before awaiting; preserve this pane across conversion.
        if (data.GetData(DataFormats.FileDrop) is string[] files && files.Length > 0)
            await WriteClipboardPathsAsync(pane, files.Where(f => !string.IsNullOrWhiteSpace(f)).ToArray());
        else if (data.GetDataPresent(DataFormats.Bitmap) && data.GetData(DataFormats.Bitmap) is BitmapSource bitmap)
            await WriteClipboardPathsAsync(pane, [SaveTemporaryImage(bitmap)]);
        else if (data.GetData(DataFormats.UnicodeText) is string text && text.Length > 0)
            WritePanePaste(pane, text.Replace("\r\n", "\r").Replace("\n", "\r"));
    }

    private static bool IsSupportedDrop(IDataObject data)
    {
        try { return data.GetDataPresent(DataFormats.FileDrop) || data.GetDataPresent(DataFormats.Bitmap) || data.GetDataPresent(DataFormats.UnicodeText); }
        catch (System.Runtime.InteropServices.COMException) { return false; }
    }

    private void Window_Loaded(object sender, RoutedEventArgs e)
    {
        RefreshContextMenuIntegrationState();
        PromptForContextMenuIntegration();
        ActiveView.Focus();
        ActiveView.UpdateLayout();
        Dispatcher.BeginInvoke(
            DispatcherPriority.Loaded,
            () => CreateTerminalTab(_defaultProfile));
        _ = CheckForUpdatesOnStartupAsync();
    }

    private void CreateTerminalTab(
        TerminalProfile profile,
        string? requestedStartupDirectory = null,
        string? wslDistribution = null)
    {
        var pane = CreatePane(profile);
        if (profile == TerminalProfile.Wsl) pane.WslDistribution = wslDistribution ?? _defaultWslDistribution;
        var tab = new TerminalTab(pane);
        _tabs.Add(tab);
        ShowTab(tab);
        StartPane(pane, requestedStartupDirectory);
        ActiveView.Focus();
    }

    // Prefer side by side; narrow panes split into a top and bottom pair.
    private void SplitActivePane()
    {
        if (_activeTab is null || ActivePane is not { } current)
        {
            CreateTerminalTab(_defaultProfile);
            return;
        }
        var direction = GetSplitDirection(current);
        if (direction == PaneSplitDirection.None) { ShowPaneSpaceMessage(); return; }
        var pane = AddSplitPane(_activeTab, current, direction);
        StartPane(pane, null, showIntroduction: false);
        QueueViewportResize();
        ActiveView.Focus();
    }

    private PaneSplitDirection GetSplitDirection(TerminalPane pane)
    {
        if (pane.Frame.ActualWidth >= 2 * MinimumPaneWidth + 5 - .5) return PaneSplitDirection.SideBySide;
        if (pane.Frame.ActualHeight >= 2 * MinimumPaneHeight + 5 - .5) return PaneSplitDirection.Stacked;
        return PaneSplitDirection.None;
    }

    private TerminalPane AddSplitPane(TerminalTab tab, TerminalPane current, PaneSplitDirection direction)
    {
        StorePaneSizes();
        var pane = CreatePane(current.Profile);
        pane.WslDistribution = current.WslDistribution;
        tab.LayoutRoot = tab.LayoutRoot.Replace(current,
            new PaneLayout(new PaneLayout(current), new PaneLayout(pane), direction == PaneSplitDirection.Stacked));
        tab.Panes.Insert(tab.Panes.IndexOf(current) + 1, pane);
        tab.ActivePane = pane;
        LayoutPanes();
        RebuildTabStrip();
        return pane;
    }

    private TerminalPane CreatePane(TerminalProfile profile)
    {
        var pane = new TerminalPane(_nextTabNumber++, profile, GetProfileTitle(profile));
        var inUse = _tabs.SelectMany(tab => tab.Panes).Any(other => ReferenceEquals(other.View, TerminalTextBox));
        pane.View = inUse ? CreateTerminalView() : TerminalTextBox;
        // A closed first pane may still own the XAML template through its old frame.
        if (pane.View.Parent is Border previousFrame) previousFrame.Child = null;
        pane.View.Clear();
        pane.View.Tag = pane;
        return pane;
    }

    private void StartPane(TerminalPane pane, string? requestedStartupDirectory, bool showIntroduction = true)
    {
        var startupDirectory = ResolveStartupDirectory(
            requestedStartupDirectory);
        var wslDirectory = pane.Profile == TerminalProfile.Wsl ? ResolveWslStartupDirectory(requestedStartupDirectory) : null;
        PaneHost.UpdateLayout();

        try
        {
            var columns = GetColumns(pane.View);
            var rows = GetRows(pane.View);
            pane.Buffer = new TerminalBuffer(
                columns,
                rows,
                _historySize);
            pane.GridSize = (columns, rows);
            pane.CancellationTokenSource = new CancellationTokenSource();
            pane.Session = new ConPtySession(columns, rows);
            pane.SessionEnded = false;
            pane.Session.Start(
                GetProfileCommand(pane.Profile, showIntroduction, pane.WslDistribution, wslDirectory),
                pane.Profile == TerminalProfile.Wsl ? wslDirectory ?? Environment.GetFolderPath(Environment.SpecialFolder.UserProfile) : startupDirectory);

            if (pane.Profile != TerminalProfile.Wsl && startupDirectory is not null)
                ObserveDirectory(pane, startupDirectory);

            _ = Task.Run(() => ReadOutputLoop(pane));
            UpdateSelectedDirectory();
        }
        catch (Exception exception)
        {
            var errorBuffer = new TerminalBuffer(GetColumns(pane.View), GetRows(pane.View));
            Render(pane, errorBuffer.Process("خطا در اجرای ConPTY:\r\n" + exception.Message));
        }
    }

    // The XAML view is the template: new panes copy its font and get the same handlers.
    private TerminalView CreateTerminalView()
    {
        var view = new TerminalView
        {
            BorderThickness = TerminalTextBox.BorderThickness,
            Padding = TerminalTextBox.Padding,
            FontFamily = TerminalTextBox.FontFamily,
            FontSize = TerminalTextBox.FontSize,
            FontWeight = TerminalTextBox.FontWeight,
            FontStyle = TerminalTextBox.FontStyle,
            Cursor = Cursors.IBeam,
            FlowDirection = FlowDirection.LeftToRight
        };
        view.SetResourceReference(BackgroundProperty, "TerminalBackgroundBrush");
        view.SetResourceReference(ForegroundProperty, "TerminalForegroundBrush");
        TextOptions.SetTextFormattingMode(view, TextFormattingMode.Display);
        TextOptions.SetTextRenderingMode(view, TextRenderingMode.ClearType);
        System.Windows.Documents.Typography.SetStandardLigatures(view, false);
        System.Windows.Documents.Typography.SetContextualLigatures(view, false);
        System.Windows.Documents.Typography.SetDiscretionaryLigatures(view, false);
        System.Windows.Documents.Typography.SetKerning(view, false);
        System.Windows.Documents.Typography.SetNumeralAlignment(view, FontNumeralAlignment.Tabular);
        WireTerminalView(view);
        return view;
    }

    private void WireTerminalView(TerminalView view)
    {
        view.LinkRequested += uri =>
        {
            try { Process.Start(new ProcessStartInfo(uri.AbsoluteUri) { UseShellExecute = true }); }
            catch (Exception exception)
            { UiText.Show(this, exception.Message, "Open link", MessageBoxButton.OK, MessageBoxImage.Error); }
        };
        view.AddHandler(ScrollViewer.ScrollChangedEvent, new ScrollChangedEventHandler(TerminalTextBox_ScrollChanged));
        view.PreviewKeyDown += TerminalTextBox_PreviewKeyDown;
        view.PreviewKeyUp += TerminalTextBox_PreviewKeyUp;
        view.PreviewMouseDown += TerminalTextBox_PreviewMouseDown;
        view.PreviewMouseUp += TerminalTextBox_PreviewMouseUp;
        view.PreviewMouseMove += TerminalTextBox_PreviewMouseMove;
        view.PreviewMouseWheel += TerminalTextBox_PreviewMouseWheel;
        view.PreviewMouseRightButtonDown += TerminalTextBox_PreviewMouseRightButtonDown;
        view.GotKeyboardFocus += TerminalTextBox_GotKeyboardFocus;
        view.LostKeyboardFocus += TerminalTextBox_LostKeyboardFocus;
        view.PreviewTextInput += TerminalTextBox_PreviewTextInput;
    }

    private void Window_Closed(object? sender, EventArgs e)
    {
        UiText.Changed -= ApplyUiLanguage;
        _directoryPollTimer.Stop();
        _macroCancellation?.Cancel();
        _macroRecorder = null;
        foreach (var pane in _tabs.SelectMany(tab => tab.Panes))
        {
            pane.IsShown = false;
            _ = pane.DisposeAsync();
        }

        CleanupTemporaryClipboardFiles();
    }

    private void Window_SourceInitialized(object? sender, EventArgs e) => WindowWorkArea.Attach(this, () => IsFullScreen);

    private void Window_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        MaximizeGlyph.Data = Geometry.Parse(WindowState == WindowState.Maximized
            ? "M2.5,0.5 L9.5,0.5 9.5,7.5 M0.5,2.5 L7.5,2.5 7.5,9.5 0.5,9.5 Z"
            : "M0.5,0.5 L9.5,0.5 9.5,9.5 0.5,9.5 Z");

        QueueViewportResize();
    }

    private void QueueViewportResize()
    {
        if (_resizeTimer is null) return;
        _resizeTimer.Stop();
        _resizeTimer.Interval = TimeSpan.FromMilliseconds(Math.Max(80, _zoomReflowDue - Environment.TickCount64));
        if (WindowState != WindowState.Minimized) _resizeTimer.Start();
    }

    private void ApplyViewportResize()
    {
        if (_zoomReflowDue > Environment.TickCount64) { QueueViewportResize(); return; }
        _zoomReflowDue = 0;
        if (WindowState == WindowState.Minimized || _activeTab is null) return;

        foreach (var pane in _activeTab.Panes)
        {
            if (pane.Session is not { } session || pane.Buffer is not { } buffer ||
                pane.View.ViewportWidth <= 0 || pane.View.ViewportHeight <= 0) continue;
            var columns = GetColumns(pane.View);
            var rows = GetRows(pane.View);
            if (pane.GridSize == (columns, rows)) continue;
            // Use the settled viewport, not transient window sizes during minimize/restore.
            var snapshot = buffer.Resize(columns, rows);
            session.Resize(columns, rows);
            pane.GridSize = (columns, rows);
            QueueRender(pane, snapshot);
        }
    }

    private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (HandleTabRenameKey(e)) return;
        if (HandleMacroKey(e)) { e.Handled = true; return; }

        if (e.Key == Key.F11)
        {
            ToggleFullScreen();
            e.Handled = true;
            return;
        }

        var controlPressed = (Keyboard.Modifiers & ModifierKeys.Control) != 0;
        var shiftPressed = (Keyboard.Modifiers & ModifierKeys.Shift) != 0;
        var key = GetEffectiveKey(e);

        if (controlPressed && (key == Key.OemPlus || key == Key.Add))
        {
            ZoomFontSize(1);
            e.Handled = true;
            return;
        }

        if (controlPressed && (key == Key.OemMinus || key == Key.Subtract))
        {
            ZoomFontSize(-1);
            e.Handled = true;
            return;
        }

        if (controlPressed && (key == Key.D0 || key == Key.NumPad0))
        {
            ResetFontSize();
            e.Handled = true;
            return;
        }

        if (controlPressed && key == Key.F)
        {
            OpenSearchBar();
            e.Handled = true;
            return;
        }

        if (key == Key.Escape && SearchBar.Visibility == Visibility.Visible)
        {
            CloseSearchBar();
            e.Handled = true;
            return;
        }

        if (controlPressed && shiftPressed)
        {
            if (e.Key == Key.T)
            {
                CreateTerminalTab(_defaultProfile);
                e.Handled = true;
                return;
            }

            int? profileIndex = e.Key switch
            {
                Key.D1 or Key.NumPad1 => 0,
                Key.D2 or Key.NumPad2 => 1,
                Key.D3 or Key.NumPad3 => 2,
                Key.D4 or Key.NumPad4 => 3,
                Key.D5 or Key.NumPad5 => 4,
                Key.D6 or Key.NumPad6 => 5,
                Key.D7 or Key.NumPad7 => 6,
                Key.D8 or Key.NumPad8 => 7,
                _ => null
            };

            if (profileIndex is { } index)
            {
                var available = GetAvailableProfiles();
                if (index < available.Count)
                {
                    var target = available[index];
                    CreateTerminalTab(target.Profile, wslDistribution: target.WslDistribution);
                    e.Handled = true;
                    return;
                }
            }
        }

        if (controlPressed && e.Key == Key.Tab)
        {
            SelectRelativeTab(shiftPressed ? -1 : 1);
            e.Handled = true;
            return;
        }

        var action = PaneShortcuts.Match(GetEffectiveKey(e), Keyboard.Modifiers, _paneShortcuts);
        if (action == PaneShortcutAction.None) return;
        switch (action)
        {
            case PaneShortcutAction.Split: SplitActivePane(); break;
            case PaneShortcutAction.Previous: SelectRelativePane(-1); break;
            case PaneShortcutAction.Next: SelectRelativePane(1); break;
            case PaneShortcutAction.Close: ClosePane(ActivePane); break;
        }
        e.Handled = true;
    }

    private void SplitPaneMenuItem_Click(object sender, RoutedEventArgs e) => SplitActivePane();

    private void ClosePaneMenuItem_Click(object sender, RoutedEventArgs e) => ClosePane(ActivePane);

    private void NewTabButton_Click(object sender, RoutedEventArgs e)
    {
        CreateTerminalTab(_defaultProfile);
    }

    private void ProfileMenuButton_Click(object sender, RoutedEventArgs e)
    {
        var menu = CreateProfileMenu();
        menu.PlacementTarget = sender as UIElement;
        menu.IsOpen = true;
    }

    private readonly record struct ShellMenuItem(string Header, TerminalProfile Profile, string? WslDistribution = null);

    private List<ShellMenuItem> GetAvailableProfiles()
    {
        var profiles = new List<ShellMenuItem>();
        if (ShellProfiles.IsPowerShell7Available)
            profiles.Add(new("PowerShell 7", TerminalProfile.PowerShell7));
        profiles.Add(new("Windows PowerShell", TerminalProfile.PowerShell));
        profiles.Add(new("Command Prompt", TerminalProfile.CommandPrompt));
        if (ShellProfiles.IsGitBashAvailable)
            profiles.Add(new("Git Bash", TerminalProfile.GitBash));
        if (IsWslAvailable())
        {
            foreach (var distribution in WslProfiles.Discover())
                profiles.Add(new(distribution, TerminalProfile.Wsl, distribution));
        }
        return profiles;
    }

    private ContextMenu CreateProfileMenu()
    {
        var menu = new ContextMenu { Style = (Style)FindResource("DarkContextMenuStyle") };
        var profiles = GetAvailableProfiles();
        for (var i = 0; i < profiles.Count; i++)
        {
            var p = profiles[i];
            var shortcutNumber = i < 8 ? i + 1 : (int?)null;
            AddProfileMenuItem(menu, p.Header, p.Profile, shortcutNumber, p.WslDistribution);
        }
        return menu;
    }

    private void AddProfileMenuItem(
        ItemsControl menu,
        string header,
        TerminalProfile profile,
        int? shortcutNumber = null,
        string? wslDistribution = null)
    {
        var item = new MenuItem
        {
            Header = new TextBlock { Text = header },
            Icon = CreateProfileIcon(profile),
            InputGestureText = shortcutNumber.HasValue ? $"Ctrl+Shift+{shortcutNumber.Value}" : string.Empty
        };
        item.Click += (_, _) => SelectTerminalProfile(profile, wslDistribution);
        menu.Items.Add(item);
    }

    private void SelectTerminalProfile(TerminalProfile profile, string? wslDistribution = null)
    {
        _defaultProfile = profile;
        _defaultWslDistribution = profile == TerminalProfile.Wsl ? wslDistribution : _defaultWslDistribution;
        AppSettings.SaveTerminalProfile(profile.ToString());
        if (profile == TerminalProfile.Wsl) AppSettings.SaveWslDistribution(wslDistribution);
        CreateTerminalTab(profile, wslDistribution: wslDistribution);
    }

    private void MinimizeButton_Click(object sender, RoutedEventArgs e)
    {
        WindowState = WindowState.Minimized;
    }

    private void MaximizeButton_Click(object sender, RoutedEventArgs e)
    {
        if (IsFullScreen)
        {
            ToggleFullScreen();
            return;
        }
        WindowState = WindowState == WindowState.Maximized
            ? WindowState.Normal
            : WindowState.Maximized;
    }

    private void CloseWindowButton_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }

    private void ExitMenuItem_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }

    private void ExportSessionMenuItem_Click(
        object sender,
        RoutedEventArgs e)
    {
        if (ActiveBuffer is null)
        {
            UiText.Show(
                this,
                "There is no active terminal session to export.",
                "Export session",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
            return;
        }

        var dialog = new SaveFileDialog
        {
            Title = UiText.Get("Export terminal session"),
            Filter = "Text files (*.txt)|*.txt|All files (*.*)|*.*",
            DefaultExt = ".txt",
            AddExtension = true,
            FileName = $"RtlTerminal-{DateTime.Now:yyyyMMdd-HHmmss}.txt"
        };

        if (dialog.ShowDialog(this) != true)
            return;

        try
        {
            var snapshot = ActiveBuffer.CaptureSnapshot();
            File.WriteAllText(
                dialog.FileName,
                CreateSessionText(snapshot),
                new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
        }
        catch (Exception exception) when (
            exception is IOException or
                UnauthorizedAccessException or
                NotSupportedException)
        {
            UiText.Show(
                this,
                $"The session could not be exported.{Environment.NewLine}{exception.Message}",
                "Export session",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
    }

    private void HtmlPreviewButton_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var directory = Path.Combine(Path.GetTempPath(), "RtlTerminal", "Previews");
            Directory.CreateDirectory(directory);
            var path = Path.Combine(directory, $"preview-{DateTime.Now:yyyyMMdd-HHmmss}-{Guid.NewGuid():N}.html");
            File.WriteAllText(path, ActiveView.CreateHtmlPreview(_previewScope), new UTF8Encoding(false));
            Process.Start(new ProcessStartInfo(new Uri(path).AbsoluteUri + "#terminal-end") { UseShellExecute = true });
            TerminalHtmlExporter.DeleteOlderPreviews(directory, path);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or System.ComponentModel.Win32Exception)
        {
            UiText.Show(this, exception.Message, "HTML preview", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private static string CreateSessionText(TerminalSnapshot snapshot)
    {
        var text = new StringBuilder();

        foreach (var line in snapshot.Lines)
        {
            foreach (var run in line.Runs)
                text.Append(run.Text);

            text.AppendLine();
        }

        return text.ToString();
    }

    private void LastDirectoriesMenuItem_SubmenuOpened(
        object sender,
        RoutedEventArgs e)
    {
        if (e.OriginalSource != sender) return;
        LastDirectoriesMenuItem.Items.Clear();
        var entries = _directoryHistory.Snapshot();
        foreach (var entry in entries.Take(DirectoryHistoryStore.MenuLimit))
        {
            var item = new MenuItem
            {
                Header = new TextBlock
                {
                    Text = entry.Path,
                    MaxWidth = 520,
                    TextTrimming = TextTrimming.CharacterEllipsis,
                    FlowDirection = FlowDirection.LeftToRight
                },
                ToolTip = entry.Details
            };
            item.Click += (_, _) => OpenHistoryDirectory(entry);
            LastDirectoriesMenuItem.Items.Add(item);
        }
        if (LastDirectoriesMenuItem.Items.Count == 0)
        {
            LastDirectoriesMenuItem.Items.Add(new MenuItem
            {
                Header = UiText.Get("No recent directories"),
                IsEnabled = false
            });
        }
        LastDirectoriesMenuItem.Items.Add(new Separator());
        var history = new MenuItem { Header = entries.Count > DirectoryHistoryStore.MenuLimit ? $"More… ({entries.Count} directories)" : "Manage history…" };
        history.Click += (_, _) => ShowDirectoryHistory();
        LastDirectoriesMenuItem.Items.Add(history);
    }

    private void TerminalTextBox_PreviewTextInput(
        object sender,
        TextCompositionEventArgs e)
    {
        if (ActiveSession is null || string.IsNullOrEmpty(e.Text))
            return;

        SendUserInput(ActivePane, e.Text);
        e.Handled = true;
    }

    private void TerminalTextBox_ScrollChanged(
        object sender,
        ScrollChangedEventArgs e)
    {
        if (sender is not TerminalView { Tag: TerminalPane pane } view)
            return;

        if ((e.ViewportWidthChange != 0 || e.ViewportHeightChange != 0) &&
            pane.Session is not null && pane.Buffer is not null)
        {
            QueueViewportResize();
        }

        if (_restoringScrollPosition || view.IsUpdatingScroll)
            return;

        if (Math.Abs(e.VerticalChange) < 0.01)
            return;

        pane.FollowOutput =
            e.ExtentHeight <= e.ViewportHeight ||
            e.VerticalOffset >= e.ExtentHeight - e.ViewportHeight - 2;
    }

    private void TerminalTextBox_PreviewMouseRightButtonDown(
        object sender,
        MouseButtonEventArgs e)
    {
        ActivatePaneOf(sender);
        if (IsSgrMouseTrackingEnabled() && !ActiveView.HasSelection)
            return;

        e.Handled = true;
        _suppressRightMouseUp = true;
        if (ActiveView.HasSelection) CopySelection();
        else PasteClipboard();
        ActiveView.Focus();
    }

    private ContextMenu CreateTerminalContextMenu()
    {
        var menu = new ContextMenu { Style = (Style)FindResource("DarkContextMenuStyle"), PlacementTarget = ActiveView };
        var split = new MenuItem { Header = UiText.Get("Split pane"), InputGestureText = _paneShortcuts.Split ? "Ctrl+Shift+D" : "" };
        split.Click += SplitPaneMenuItem_Click;
        var closePane = new MenuItem { Header = UiText.Get("Close pane"), InputGestureText = _paneShortcuts.Close ? "Ctrl+Shift+W" : "", IsEnabled = _activeTab?.Panes.Count > 1 };
        closePane.Click += ClosePaneMenuItem_Click;
        var movePane = new MenuItem { Header = UiText.Get("Move pane to a new tab"), IsEnabled = _activeTab?.Panes.Count > 1 };
        movePane.Click += MovePaneMenuItem_Click;
        var copy = new MenuItem { Header = UiText.Get("_Copy"), IsEnabled = ActiveView.HasSelection };
        copy.Click += (_, _) => CopySelection();
        var paste = new MenuItem { Header = UiText.Get("_Paste"), IsEnabled = ActiveSession is not null };
        paste.Click += (_, _) => PasteClipboard();
        var select = new MenuItem { Header = UiText.Get("Select _all") };
        select.Click += (_, _) => ActiveView.SelectAll();
        menu.Items.Add(copy); menu.Items.Add(paste); menu.Items.Add(new Separator()); menu.Items.Add(select);
        menu.Items.Add(new Separator()); menu.Items.Add(split); menu.Items.Add(movePane); menu.Items.Add(closePane);
        return menu;
    }

    private void TerminalTextBox_PreviewMouseDown(
        object sender,
        MouseButtonEventArgs e)
    {
        ActivatePaneOf(sender);
        if (e.ChangedButton == MouseButton.Right && ActiveView.HasSelection) return;
        var button = GetMouseButtonCode(e.ChangedButton);

        if (button < 0 || !SendMouseEvent(e, button, released: false))
            return;

        _lastReportedMouseCell = null;
        Mouse.Capture(ActiveView);
        ActiveView.Focus();
        e.Handled = true;
    }

    private void TerminalTextBox_PreviewMouseUp(
        object sender,
        MouseButtonEventArgs e)
    {
        if (e.ChangedButton == MouseButton.Right && _suppressRightMouseUp)
        {
            _suppressRightMouseUp = false;
            e.Handled = true;
            return;
        }
        if (!ReferenceEquals(sender, ActiveView))
            return;
        var button = GetMouseButtonCode(e.ChangedButton);

        if (button < 0 || !SendMouseEvent(e, button, released: true))
            return;

        _lastReportedMouseCell = null;
        Mouse.Capture(null);
        e.Handled = true;
    }

    private void TerminalTextBox_PreviewMouseMove(
        object sender,
        MouseEventArgs e)
    {
        var mode = ActiveSnapshot?.Modes.MouseTrackingMode ?? 0;

        if (!ReferenceEquals(sender, ActiveView) ||
            !IsSgrMouseTrackingEnabled() ||
            mode == 1000 ||
            mode == 1002 && e.LeftButton != MouseButtonState.Pressed &&
                e.MiddleButton != MouseButtonState.Pressed &&
                e.RightButton != MouseButtonState.Pressed)
        {
            return;
        }

        var button = e.LeftButton == MouseButtonState.Pressed
            ? 0
            : e.MiddleButton == MouseButtonState.Pressed
                ? 1
                : e.RightButton == MouseButtonState.Pressed
                    ? 2
                    : 3;

        if (!TryGetMouseCell(e, out var x, out var y) ||
            _lastReportedMouseCell == (x, y, button))
        {
            return;
        }

        _lastReportedMouseCell = (x, y, button);
        SendSgrMouse(button + 32, x, y, released: false);
        e.Handled = true;
    }

    private void TerminalTextBox_PreviewMouseWheel(
        object sender,
        MouseWheelEventArgs e)
    {
        // Other panes scroll their own history; only the active one talks to its program.
        if (!ReferenceEquals(sender, ActiveView))
            return;

        if (!IsSgrMouseTrackingEnabled() ||
            !TryGetMouseCell(e, out var x, out var y))
        {
            return;
        }

        SendSgrMouse(e.Delta > 0 ? 64 : 65, x, y, released: false);
        e.Handled = true;
    }

    private void TerminalTextBox_GotKeyboardFocus(
        object sender,
        KeyboardFocusChangedEventArgs e)
    {
        ActivatePaneOf(sender);
        if (sender is TerminalView { Tag: TerminalPane pane } &&
            pane.LastRenderedSnapshot?.Modes.FocusReporting == true)
            pane.Session?.Write("\x1b[I");
    }

    private void TerminalTextBox_LostKeyboardFocus(
        object sender,
        KeyboardFocusChangedEventArgs e)
    {
        _pendingContextMenuKey = null;
        // The pane losing focus may no longer be the active one after a click.
        if (sender is TerminalView { Tag: TerminalPane pane } &&
            pane.LastRenderedSnapshot?.Modes.FocusReporting == true)
            pane.Session?.Write("\x1b[O");
    }

    private bool SendMouseEvent(
        MouseEventArgs e,
        int button,
        bool released)
    {
        if (button == 0 && (Keyboard.Modifiers & ModifierKeys.Control) != 0 && ActiveView.IsLinkAt(e))
            return false;
        if (!IsSgrMouseTrackingEnabled() ||
            !TryGetMouseCell(e, out var x, out var y))
        {
            return false;
        }

        SendSgrMouse(button, x, y, released);
        return true;
    }

    private void SendSgrMouse(int button, int x, int y, bool released)
    {
        if (ActiveSession is null)
            return;

        if ((Keyboard.Modifiers & ModifierKeys.Shift) != 0)
            button += 4;

        if ((Keyboard.Modifiers & ModifierKeys.Alt) != 0)
            button += 8;

        if ((Keyboard.Modifiers & ModifierKeys.Control) != 0)
            button += 16;

        ActiveSession.Write($"\x1b[<{button};{x};{y}{(released ? 'm' : 'M')}");
    }

    private bool TryGetMouseCell(MouseEventArgs e, out int x, out int y) =>
        ActiveView.TryGetGridCell(e, out x, out y);

    private bool IsSgrMouseTrackingEnabled() =>
        !ActiveView.IsSelecting &&
        (Keyboard.Modifiers & ModifierKeys.Shift) == 0 && ActiveSnapshot?.Modes is
        {
            MouseTrackingMode: > 0,
            SgrMouse: true
        };

    private static int GetMouseButtonCode(MouseButton button) =>
        button switch
        {
            MouseButton.Left => 0,
            MouseButton.Middle => 1,
            MouseButton.Right => 2,
            _ => -1
        };

    private void SmartRtlMenuItem_Changed(object sender, RoutedEventArgs e)
    {
        RenderVisiblePanes();

        ActiveView.Focus();
    }

    private void FontSettingsMenuItem_Click(object sender, RoutedEventArgs e) => ShowSettings(openThemeTab: false);

    private void ShowSettings(bool openThemeTab, bool openMacroTab = false)
    {
        var settingsWindow = new FontSettingsWindow(
            BundledFonts.DisplayName(TerminalTextBox.FontFamily),
            TerminalTextBox.FontSize,
            TerminalTextBox.FontWeight,
            TerminalTextBox.FontStyle,
            _historySize, _previewScope, _paneShortcuts)
        {
            Owner = this
        };

        if (openThemeTab) settingsWindow.ShowThemeTab();
        settingsWindow.SetDirectoryHistoryEnabled(_directoryHistory.Enabled);
        if (openMacroTab) settingsWindow.ShowMacroTab();

        if (settingsWindow.ShowDialog() != true)
            return;

        _macroSettings = settingsWindow.SelectedMacroSettings;
        _directoryHistory.SetEnabled(settingsWindow.SelectedDirectoryHistoryEnabled);
        _macroSettings.Save();
        ApplyHistorySize(settingsWindow.SelectedHistorySize);
        ApplyFontSettings(settingsWindow.SelectedSettings);
        AppSettings.SaveFont(settingsWindow.SelectedSettings);
        UpdateZoomMenuVisibility();
        AppSettings.SaveHistorySize(settingsWindow.SelectedHistorySize);
        _previewScope = settingsWindow.SelectedPreviewScope;
        AppSettings.SavePreviewScope(_previewScope);
        _paneShortcuts = settingsWindow.SelectedPaneShortcuts;
        AppSettings.SavePaneShortcuts(_paneShortcuts);
        ApplyColors(settingsWindow.SelectedTheme.Theme, settingsWindow.SelectedTheme.Scheme);
        UpdateShortcutLabels();
        ActiveView.Focus();
    }

    private void ApplyHistorySize(int historySize)
    {
        _historySize = historySize;

        foreach (var pane in _tabs.SelectMany(tab => tab.Panes))
        {
            if (pane.Buffer is not { } buffer)
                continue;

            QueueRender(
                pane,
                buffer.SetMaximumScrollbackRows(historySize));
        }
    }

    private void GuideMenuItem_Click(object sender, RoutedEventArgs e)
    {
        var guideWindow = new GuideWindow
        {
            Owner = this
        };
        guideWindow.Show();
    }

    private void ThemeMenuItem_Click(object sender, RoutedEventArgs e) => ShowSettings(openThemeTab: true);

    private void ApplyColors(AppThemeKind theme, ColorScheme? scheme)
    {
        AppTheme.Apply(theme, scheme);
        AppSettings.SaveTheme(AppTheme.Current);
        AppSettings.SaveColorScheme(scheme?.Name);
        RefreshColorViews();
    }

    private void RefreshColorViews()
    {
        RebuildTabStrip();
        foreach (var view in AllViews())
        {
            // Detached views in hidden tabs may retain a previously resolved resource.
            view.SetResourceReference(BackgroundProperty, "TerminalBackgroundBrush");
            view.SetResourceReference(ForegroundProperty, "TerminalForegroundBrush");
            view.RefreshTheme();
        }
        ActiveView.Focus();
    }

    private async void CheckForUpdatesMenuItem_Click(
        object sender,
        RoutedEventArgs e)
    {
        await CheckForUpdatesAsync(manual: true);
    }

    private async Task CheckForUpdatesOnStartupAsync()
    {
        // Let terminal startup finish before performing optional network I/O.
        await Task.Delay(TimeSpan.FromSeconds(1.5));

        if (!IsLoaded)
            return;

        await CheckForUpdatesAsync(manual: false);
    }

    private async Task CheckForUpdatesAsync(bool manual)
    {
        if (_updateCheckInProgress)
        {
            if (manual)
            {
                UiText.Show(
                    this,
                    "An update check is already in progress.",
                    "Check for updates",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
            }

            return;
        }

        _updateCheckInProgress = true;
        CheckForUpdatesMenuItem.IsEnabled = false;

        if (manual)
            CheckForUpdatesMenuItem.SetCurrentValue(MenuItem.HeaderProperty, UiText.Get("Checking for updates..."));

        try
        {
            var result = await UpdateService.CheckAsync();

            if (!result.IsUpdateAvailable)
            {
                if (manual)
                {
                    UiText.Show(
                        this,
                        $"Rtl Terminal {result.CurrentVersion.ToString(3)} is up to date.",
                        "Check for updates",
                        MessageBoxButton.OK,
                        MessageBoxImage.Information);
                }

                return;
            }

            if (!manual && string.Equals(
                    AppSettings.LoadSkippedUpdateVersion(),
                    result.LatestTag,
                    StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            ShowUpdateAvailable(result);
        }
        catch (Exception exception) when (
            exception is HttpRequestException or
                TaskCanceledException or
                InvalidDataException or
                JsonException)
        {
            if (manual)
            {
                UiText.Show(
                    this,
                    UiText.Get("Rtl Terminal could not check GitHub for updates.") + "\n\n" +
                    exception.Message,
                    "Check for updates",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
            }
        }
        finally
        {
            CheckForUpdatesMenuItem.SetCurrentValue(MenuItem.HeaderProperty, UiText.Get("Check for _updates..."));
            CheckForUpdatesMenuItem.IsEnabled = true;
            _updateCheckInProgress = false;
        }
    }

    private void ShowUpdateAvailable(UpdateCheckResult result)
    {
        var updateWindow = new UpdateAvailableWindow(
            result.CurrentVersion,
            result.LatestVersion)
        {
            Owner = this
        };

        updateWindow.ShowDialog();

        if (updateWindow.DontShowAgain)
        {
            AppSettings.SaveSkippedUpdateVersion(result.LatestTag);
        }
        else if (string.Equals(
                     AppSettings.LoadSkippedUpdateVersion(),
                     result.LatestTag,
                     StringComparison.OrdinalIgnoreCase))
        {
            AppSettings.SaveSkippedUpdateVersion(null);
        }

        if (!updateWindow.OpenUpdateRequested)
            return;

        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = result.ReleasePage.AbsoluteUri,
                UseShellExecute = true
            });
        }
        catch (Exception exception) when (
            exception is InvalidOperationException or
                System.ComponentModel.Win32Exception)
        {
            UiText.Show(
                this,
                UiText.Get("The update page could not be opened.") + "\n\n" + exception.Message,
                "Rtl Terminal update",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
        }
    }

    private void AboutMenuItem_Click(object sender, RoutedEventArgs e)
    {
        UiText.Show(
            this,
            "Rtl Terminal\nby behnamapps\n\n" + UiText.Get("Developer") + ": behnam tajadini\nYouTube: aka_techno\n\n" + UiText.Get("Dedicated to all Persian speakers"),
            "About Rtl Terminal",
            MessageBoxButton.OK,
            MessageBoxImage.Information);
    }

    private void ContextMenuIntegrationMenuItem_Click(
        object sender,
        RoutedEventArgs e)
    {
        if (_updatingContextMenuItem)
            return;

        try
        {
            if (ContextMenuIntegrationMenuItem.IsChecked)
                ContextMenuIntegration.Install();
            else
                ContextMenuIntegration.Uninstall();
        }
        catch (Exception exception)
        {
            UiText.Show(
                this,
                UiText.Get("Could not change the folder context menu.") +
                Environment.NewLine +
                exception.Message,
                "RtlTerminal",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
        finally
        {
            RefreshContextMenuIntegrationState();
            ActiveView.Focus();
        }
    }

    private void PromptForContextMenuIntegration()
    {
        if (ContextMenuIntegration.HasAnsweredInitialPrompt())
            return;

        var result = UiText.Show(
            this,
            "Add 'Open in RtlTerminal' to the folder context menu?",
            "RtlTerminal",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);

        try
        {
            if (result == MessageBoxResult.Yes)
                ContextMenuIntegration.Install();
        }
        catch (Exception exception)
        {
            UiText.Show(
                this,
                UiText.Get("Could not add the folder context menu.") +
                Environment.NewLine +
                exception.Message,
                "RtlTerminal",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
        finally
        {
            ContextMenuIntegration.MarkInitialPromptAnswered();
            RefreshContextMenuIntegrationState();
        }
    }

    private void RefreshContextMenuIntegrationState()
    {
        _updatingContextMenuItem = true;
        ContextMenuIntegrationMenuItem.IsChecked =
            ContextMenuIntegration.IsInstalled();
        ContextMenuIntegrationMenuItem.SetCurrentValue(MenuItem.HeaderProperty, UiText.Get(
            ContextMenuIntegrationMenuItem.IsChecked
                ? "Remove _Open in RtlTerminal"
                : "Add _Open in RtlTerminal"));
        _updatingContextMenuItem = false;
    }

    private static string? ResolveWslStartupDirectory(string? requestedDirectory)
    {
        if (TryResolveDirectory(requestedDirectory, out var directory)) return directory;
        var arguments = Environment.GetCommandLineArgs();
        return arguments.Length >= 2 && TryResolveDirectory(arguments[1], out directory) ? directory : null;
    }

    private static string? ResolveStartupDirectory(
        string? requestedDirectory = null)
    {
        var arguments = Environment.GetCommandLineArgs();
        return StartupDirectories.Resolve(requestedDirectory, arguments.Length >= 2 ? arguments[1] : null,
            Environment.CurrentDirectory, AppContext.BaseDirectory, Environment.GetFolderPath(Environment.SpecialFolder.UserProfile));
    }

    private static bool TryResolveDirectory(
        string? candidate,
        out string? directory)
    {
        directory = null;

        if (string.IsNullOrWhiteSpace(candidate) ||
            !Directory.Exists(candidate))
        {
            return false;
        }

        try
        {
            directory = Path.GetFullPath(candidate);
            return true;
        }
        catch (Exception exception) when (
            exception is ArgumentException or
                NotSupportedException or
                PathTooLongException)
        {
            return false;
        }
    }

    private static string GetProfileTitle(TerminalProfile profile) =>
        profile switch
        {
            TerminalProfile.PowerShell7 => "PowerShell 7",
            TerminalProfile.PowerShell => "PowerShell",
            TerminalProfile.GitBash => "Git Bash",
            TerminalProfile.Wsl => "WSL",
            _ => "Command Prompt"
        };

    private static string GetProfileCommand(TerminalProfile profile, bool showIntroduction = true, string? wslDistribution = null, string? wslDirectory = null)
    {
        if (profile == TerminalProfile.Wsl) return WslProfiles.CommandLine(wslDistribution, wslDirectory);
        if (profile == TerminalProfile.GitBash)
        {
            var bashPath = ShellProfiles.GitBashPath ?? "bash.exe";
            return ShellPath.WindowsArgument(bashPath) + " --login -i";
        }
        const string powerShellDirectoryPrompt = "$global:__RtlOriginalPrompt=$function:prompt; function global:prompt { [Console]::Write(([char]27).ToString()+']9;9;'+$executionContext.SessionState.Path.CurrentFileSystemLocation.ProviderPath+[char]7); & $global:__RtlOriginalPrompt }; ";
        var pwshPath = ShellProfiles.PowerShell7Path ?? "pwsh.exe";
        var command =
        !showIntroduction ? profile switch
        {
            TerminalProfile.PowerShell7 => ShellPath.WindowsArgument(pwshPath) + @" -NoLogo -NoExit -Command """ + powerShellDirectoryPrompt + "\"",
            TerminalProfile.PowerShell => @"C:\Windows\System32\WindowsPowerShell\v1.0\powershell.exe -NoLogo -NoExit -Command """ + powerShellDirectoryPrompt + "\"",
            _ => @"C:\Windows\System32\cmd.exe /D /Q /V:OFF /K ""chcp 65001>nul"""
        } : (profile switch
        {
            TerminalProfile.PowerShell7 =>
                ShellPath.WindowsArgument(pwshPath) +
                """
                 -NoLogo -NoExit -Command "$lines=@('RtlTerminal v{APP_VERSION}','','Author : Behnam Tajadini','Source : github.com/mirbehnam/RtlTerminal','YouTube: @aka_techno',''); $lines | ForEach-Object { Write-Host $_ }"
                """,
            TerminalProfile.PowerShell =>
                """
                C:\Windows\System32\WindowsPowerShell\v1.0\powershell.exe -NoLogo -NoExit -Command "$lines=@('RtlTerminal v{APP_VERSION}','','Author : Behnam Tajadini','Source : github.com/mirbehnam/RtlTerminal','YouTube: @aka_techno',''); $lines | ForEach-Object { Write-Host $_ }"
                """,
            _ =>
                """
                C:\Windows\System32\cmd.exe /D /Q /V:OFF /K "chcp 65001>nul & echo RtlTerminal v{APP_VERSION}& echo.& echo Author : Behnam Tajadini& echo Source : github.com/mirbehnam/RtlTerminal& echo YouTube: @aka_techno& echo."
                """
        }).Replace("{APP_VERSION}", AppVersion.Display);
        if (showIntroduction)
            command = command.Replace("YouTube: @aka_techno", StartupSocialLine(Random.Shared.Next(2) == 0));
        if ((profile == TerminalProfile.PowerShell || profile == TerminalProfile.PowerShell7) && showIntroduction)
            command = command.Replace("-Command \"", "-Command \"" + powerShellDirectoryPrompt);
        return command;
    }

    private static bool IsWslAvailable() => WslProfiles.IsAvailable;
    private static bool IsPowerShell7Available() => ShellProfiles.IsPowerShell7Available;
    private static bool IsGitBashAvailable() => ShellProfiles.IsGitBashAvailable;

    private static TerminalProfile LoadDefaultProfile()
    {
        var savedProfile = AppSettings.LoadTerminalProfile();

        if (string.Equals(savedProfile, "PowerShellCore", StringComparison.OrdinalIgnoreCase))
            savedProfile = nameof(TerminalProfile.PowerShell7);

        if (!Enum.TryParse(savedProfile, out TerminalProfile profile) ||
            !Enum.IsDefined(profile) ||
            profile == TerminalProfile.Wsl && !IsWslAvailable() ||
            profile == TerminalProfile.PowerShell7 && !IsPowerShell7Available() ||
            profile == TerminalProfile.GitBash && !IsGitBashAvailable())
        {
            return TerminalProfile.CommandPrompt;
        }

        return profile;
    }

    private TerminalPane? ActivePane => _activeTab?.ActivePane;

    private TerminalPane? ExplorerTargetPane() => _activeTab is not { } tab ? null :
        tab.Panes.Contains(tab.ActivePane) ? tab.ActivePane : tab.Panes.MaxBy(pane => pane.Number);

    private async Task<string?> ReadExplorerDirectoryAsync(TerminalPane pane)
    {
        var reported = TerminalDirectory.FromReport(pane.Buffer?.CaptureSnapshot().WorkingDirectory, pane.WslDistribution);
        if (pane.Profile == TerminalProfile.Wsl)
        {
            var current = pane.Session is { } session
                ? await TerminalDirectory.ReadWslAsync(session.SessionId, pane.WslDistribution) : null;
            return current ?? reported;
        }
        return reported ?? pane.Session?.ReadWorkingDirectory();
    }

    private async void OpenDirectoryMenuItem_Click(object sender, RoutedEventArgs e)
        => await OpenPaneDirectoryAsync(ExplorerTargetPane());

    private async Task OpenPaneDirectoryAsync(TerminalPane? pane)
    {
        if (pane is null) return;
        OpenDirectoryMenuItem.IsEnabled = false;
        ActiveDirectoryButton.IsEnabled = false;
        try
        {
            var directory = await ReadExplorerDirectoryAsync(pane);
            SetPaneDirectory(pane, directory);
            if (directory is null || !await Task.Run(() => Directory.Exists(directory)))
                throw new IOException("The current directory is unavailable. Return to the shell prompt and try again.");
            Process.Start(new ProcessStartInfo(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "explorer.exe"))
            {
                Arguments = ShellPath.WindowsArgument(directory), UseShellExecute = true
            });
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or ArgumentException or System.ComponentModel.Win32Exception)
        {
            UiText.Show(this, exception.Message, "Open directory", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        finally { OpenDirectoryMenuItem.IsEnabled = true; RefreshActiveDirectory(); }
    }
    private TerminalView ActiveView => ActivePane?.View ?? TerminalTextBox;
    private ConPtySession? ActiveSession => ActivePane?.Session;
    private TerminalBuffer? ActiveBuffer => ActivePane?.Buffer;
    private TerminalSnapshot? ActiveSnapshot => ActivePane?.LastRenderedSnapshot;

    private IEnumerable<TerminalView> AllViews() =>
        _tabs.SelectMany(tab => tab.Panes).Select(pane => pane.View)
            .Append(TerminalTextBox).Distinct();

    private void ShowTab(TerminalTab tab)
    {
        CloseSearchBar();
        _searchMatches.Clear();
        _currentSearchIndex = -1;
        if (_renamingTab is not null && !ReferenceEquals(_renamingTab, tab)) FinishTabRename(true, false, false);
        StorePaneSizes();
        _activeTab = tab;
        UpdateSelectedDirectory();
        LayoutPanes();
        RebuildTabStrip();
        RenderVisiblePanes();
        QueueViewportResize();
    }

    // Save both column and row proportions after splitter drags and tab switches.
    private void StorePaneSizes()
    {
        foreach (var (node, grid) in _paneLayoutGrids)
        {
            var first = node.Stacked ? grid.RowDefinitions[0].ActualHeight : grid.ColumnDefinitions[0].ActualWidth;
            var second = node.Stacked ? grid.RowDefinitions[2].ActualHeight : grid.ColumnDefinitions[2].ActualWidth;
            if (first + second > 0) node.Ratio = Math.Clamp(first / (first + second), .001, .999);
        }
    }

    private double MinimumPaneWidth => Math.Ceiling(24 * _cellWidth + 40);
    private double MinimumPaneHeight => Math.Ceiling(10 * _lineHeight + 24);
    private Size MinimumLayoutSize(PaneLayout node) => node.MinimumSize(MinimumPaneWidth, MinimumPaneHeight);

    private bool CanFitLayout(PaneLayout layout)
    {
        var required = MinimumLayoutSize(layout);
        var width = PaneScroller.ViewportWidth > 0 ? PaneScroller.ViewportWidth : PaneScroller.ActualWidth;
        var height = PaneScroller.ViewportHeight > 0 ? PaneScroller.ViewportHeight : PaneScroller.ActualHeight;
        return required.Width <= width + .5 && required.Height <= height + .5;
    }

    private void ShowPaneSpaceMessage() => UiText.Show(this,
        "There is not enough room for all panes. Enlarge the window and try again.",
        "Split terminals", MessageBoxButton.OK, MessageBoxImage.Information);

    private void PaneScroller_SizeChanged(object sender, SizeChangedEventArgs e) => UpdatePaneSizing();

    private void PaneScroller_ScrollChanged(object sender, ScrollChangedEventArgs e)
    {
        if (e.ViewportWidthChange != 0 || e.ViewportHeightChange != 0) UpdatePaneSizing();
    }

    private void UpdatePaneSizing()
    {
        var required = _activeTab is null ? new Size(MinimumPaneWidth, MinimumPaneHeight) : MinimumLayoutSize(_activeTab.LayoutRoot);
        var width = PaneScroller.ViewportWidth > 0 ? PaneScroller.ViewportWidth : PaneScroller.ActualWidth;
        var height = PaneScroller.ViewportHeight > 0 ? PaneScroller.ViewportHeight : PaneScroller.ActualHeight;
        PaneHost.Width = Math.Max(required.Width, Math.Max(0, width));
        PaneHost.Height = Math.Max(required.Height, Math.Max(0, height));
        var chrome = Math.Max(0, ActualWidth - PaneScroller.ActualWidth);
        MinWidth = Math.Max(480, Math.Min(required.Width + chrome, WindowWorkArea.GetAvailableWidth(this)));
        QueueViewportResize();
    }

    private void UpdateShortcutLabels()
    {
        SplitPaneMenuItem.InputGestureText = _paneShortcuts.Split ? "Ctrl+Shift+D" : "";
        ClosePaneMenuItem.InputGestureText = _paneShortcuts.Close ? "Ctrl+Shift+W" : "";
    }

    // A split tree lets one narrow pane stack vertically without moving its neighbours.
    private void LayoutPanes()
    {
        foreach (var tab in _tabs)
            foreach (var pane in tab.Panes)
            {
                if (pane.IsShown) pane.VerticalOffset = pane.View.VerticalOffset;
                lock (_renderLock) pane.IsShown = ReferenceEquals(tab, _activeTab);
            }
        PaneHost.Children.Clear();
        _paneLayoutGrids.Clear();
        if (_activeTab is null) return;
        PaneHost.Children.Add(BuildPaneLayout(_activeTab.LayoutRoot));
        UpdatePaneSizing();
        UpdatePaneFrames();
    }

    private FrameworkElement BuildPaneLayout(PaneLayout node)
    {
        if (node.Pane is { } pane)
        {
            if (pane.Frame.Parent is Panel oldParent) oldParent.Children.Remove(pane.Frame);
            if (pane.View.Parent is Panel panel) panel.Children.Remove(pane.View);
            pane.Frame.Child = pane.View;
            pane.Frame.BorderThickness = new Thickness(_activeTab!.Panes.Count > 1 ? 1 : 0);
            pane.Frame.MinWidth = MinimumPaneWidth;
            pane.Frame.MinHeight = MinimumPaneHeight;
            Grid.SetRow(pane.Frame, 0); Grid.SetColumn(pane.Frame, 0);
            if (!pane.FollowOutput)
            {
                var offset = pane.VerticalOffset;
                Dispatcher.BeginInvoke(DispatcherPriority.Loaded, () => pane.View.ScrollToVerticalOffset(offset));
            }
            return pane.Frame;
        }
        var firstSize = MinimumLayoutSize(node.First!);
        var secondSize = MinimumLayoutSize(node.Second!);
        var grid = new Grid();
        _paneLayoutGrids.Add(node, grid);
        if (node.Stacked)
        {
            grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(node.Ratio, GridUnitType.Star), MinHeight = firstSize.Height });
            grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(5) });
            grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1 - node.Ratio, GridUnitType.Star), MinHeight = secondSize.Height });
        }
        else
        {
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(node.Ratio, GridUnitType.Star), MinWidth = firstSize.Width });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(5) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1 - node.Ratio, GridUnitType.Star), MinWidth = secondSize.Width });
        }
        var first = BuildPaneLayout(node.First!);
        var second = BuildPaneLayout(node.Second!);
        Grid.SetRow(first, 0); Grid.SetColumn(first, 0);
        Grid.SetRow(second, node.Stacked ? 2 : 0); Grid.SetColumn(second, node.Stacked ? 0 : 2);
        var splitter = new GridSplitter
        {
            HorizontalAlignment = HorizontalAlignment.Stretch, VerticalAlignment = VerticalAlignment.Stretch,
            ResizeBehavior = GridResizeBehavior.PreviousAndNext,
            ResizeDirection = node.Stacked ? GridResizeDirection.Rows : GridResizeDirection.Columns,
            Focusable = false
        };
        if (node.Stacked) splitter.Height = 5; else splitter.Width = 5;
        splitter.SetResourceReference(BackgroundProperty, "ToolbarBorderBrush");
        splitter.DragCompleted += (_, _) => { StorePaneSizes(); QueueViewportResize(); };
        Grid.SetRow(splitter, node.Stacked ? 1 : 0); Grid.SetColumn(splitter, node.Stacked ? 0 : 1);
        grid.Children.Add(first); grid.Children.Add(splitter); grid.Children.Add(second);
        return grid;
    }

    private void UpdatePaneFrames()
    {
        if (_activeTab is null)
            return;

        foreach (var pane in _activeTab.Panes)
        {
            pane.Frame.SetResourceReference(Border.BorderBrushProperty,
                ReferenceEquals(pane, _activeTab.ActivePane) ? "AccentBrush" : "TerminalBackgroundBrush");
        }
    }

    private void ActivatePaneOf(object sender)
    {
        if (sender is TerminalView { Tag: TerminalPane pane })
            ActivatePane(pane);
    }

    private void ActivatePane(TerminalPane pane)
    {
        if (_activeTab is null || ReferenceEquals(pane, _activeTab.ActivePane) ||
            !_activeTab.Panes.Contains(pane))
            return;

        _activeTab.ActivePane = pane;
        UpdateSelectedDirectory();
        pane.Frame.BringIntoView();
        UpdatePaneFrames();
        RebuildTabStrip();
        if (!pane.View.IsKeyboardFocusWithin)
            pane.View.Focus();
    }

    private void SelectRelativePane(int direction)
    {
        if (_activeTab is not { Panes.Count: > 1 } tab)
            return;

        var index = tab.Panes.IndexOf(tab.ActivePane);
        ActivatePane(tab.Panes[(index + direction + tab.Panes.Count) % tab.Panes.Count]);
    }

    private void SelectTab(TerminalTab tab)
    {
        if (ReferenceEquals(tab, _activeTab))
            return;

        ShowTab(tab);
        ActiveView.Focus();
    }

    private void SelectRelativeTab(int direction)
    {
        if (_activeTab is null || _tabs.Count < 2)
            return;

        var currentIndex = _tabs.IndexOf(_activeTab);
        var nextIndex = (currentIndex + direction + _tabs.Count) % _tabs.Count;
        SelectTab(_tabs[nextIndex]);
    }

    private void ClosePane(TerminalPane? pane)
    {
        if (pane is null || _tabs.FirstOrDefault(tab => tab.Panes.Contains(pane)) is not { } tab)
            return;

        if (tab.Panes.Count == 1)
        {
            CloseTab(tab);
            return;
        }

        DetachPane(tab, pane);
        RebuildTabStrip();
        _ = pane.DisposeAsync();
    }

    private void DetachPane(TerminalTab tab, TerminalPane pane)
    {
        StorePaneSizes();
        var index = tab.Panes.IndexOf(pane);
        tab.Panes.RemoveAt(index);
        tab.LayoutRoot = tab.LayoutRoot.Without(pane)!;
        if (ReferenceEquals(tab.ActivePane, pane))
            tab.ActivePane = tab.Panes[Math.Max(0, index - 1)];

        if (ReferenceEquals(tab, _activeTab))
        {
            LayoutPanes();
            QueueViewportResize();
            ActiveView.Focus();
        }
    }

    // Moves every pane of another tab to the right of the current tab's panes.
    private void JoinTab(TerminalTab? source) => TryJoinTab(source, showMessage: true);

    private bool TryJoinTab(TerminalTab? source, bool showMessage)
    {
        if (source is null || _activeTab is not { } target || ReferenceEquals(source, target))
            return false;

        if (!_tabs.Contains(source)) return false;
        var joinedLayout = new PaneLayout(target.LayoutRoot, source.LayoutRoot, stacked: false);
        if (!CanFitLayout(joinedLayout))
        {
            if (showMessage) ShowPaneSpaceMessage();
            return false;
        }
        StorePaneSizes();
        target.LayoutRoot = joinedLayout;
        target.Panes.AddRange(source.Panes);

        _tabs.Remove(source);
        LayoutPanes();
        RebuildTabStrip();
        RenderVisiblePanes();
        QueueViewportResize();
        ActiveView.Focus();
        return true;
    }

    private void MovePaneToNewTab(TerminalPane? pane)
    {
        if (pane is null || _activeTab is not { Panes.Count: > 1 } tab || !tab.Panes.Contains(pane))
            return;

        DetachPane(tab, pane);
        var newTab = new TerminalTab(pane);
        _tabs.Insert(_tabs.IndexOf(tab) + 1, newTab);
        ShowTab(newTab);
        ActiveView.Focus();
    }

    private void SplitTabMenuItem_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not MenuItem { Tag: TerminalTab tab } || !_tabs.Contains(tab)) return;
        SelectTab(tab);
        SplitActivePane();
    }

    private void SetTabColor(TerminalTab tab, Color? color)
    {
        if (!_tabs.Contains(tab)) return;
        tab.BackgroundColor = color;
        RebuildTabStrip();
    }

    private MenuItem CreateTabColorMenu(TerminalTab tab)
    {
        var colorMenu = new MenuItem { Header = UiText.Get("Color...") };
        var palette = new System.Windows.Controls.Primitives.UniformGrid { Columns = 6, Margin = new Thickness(2) };
        foreach (var (name, color) in TabColors.Palette)
        {
            var swatch = new Button
            {
                Width = 28, Height = 28, Margin = new Thickness(3), Padding = new Thickness(0),
                Background = new SolidColorBrush(color), ToolTip = name, Tag = color,
                BorderBrush = new SolidColorBrush(tab.BackgroundColor == color ? AppTheme.TabAccent : AppTheme.TabSeparator),
                BorderThickness = new Thickness(tab.BackgroundColor == color ? 3 : 1),
                Content = tab.BackgroundColor == color ? "✓" : "",
                Foreground = new SolidColorBrush(TabColors.TextColor(color))
            };
            System.Windows.Automation.AutomationProperties.SetName(swatch, $"{name} tab color");
            swatch.Click += (_, e) =>
            {
                e.Handled = true;
                if (colorMenu.Parent is ContextMenu context) context.IsOpen = false;
                SetTabColor(tab, color);
            };
            palette.Children.Add(swatch);
        }
        colorMenu.Items.Add(new MenuItem { Header = palette, StaysOpenOnClick = true, Focusable = false });
        colorMenu.Items.Add(new Separator());
        var custom = new MenuItem { Header = UiText.Get("Custom color...") };
        custom.Click += (_, _) =>
        {
            // Open after the menu finishes closing so focus returns to the terminal cleanly.
            Dispatcher.BeginInvoke(DispatcherPriority.Input, () =>
            {
                if (!_tabs.Contains(tab)) return;
                var color = TabColors.ChooseCustom(this, tab.BackgroundColor ?? AppTheme.ActiveTab);
                if (color is { } selected) SetTabColor(tab, selected);
            });
        };
        var reset = new MenuItem { Header = UiText.Get("Reset color"), IsEnabled = tab.BackgroundColor is not null };
        reset.Click += (_, _) => SetTabColor(tab, null);
        colorMenu.Items.Add(custom); colorMenu.Items.Add(reset);
        return colorMenu;
    }

    private ContextMenu CreateTabContextMenu(TerminalTab tab)
    {
        var menu = new ContextMenu { Style = (Style)FindResource("DarkContextMenuStyle") };
        var split = new MenuItem
        {
            Header = UiText.Get("Split tab"), Tag = tab,
            InputGestureText = _paneShortcuts.Split ? "Ctrl+Shift+D" : ""
        };
        split.Click += SplitTabMenuItem_Click;
        var join = new MenuItem
        {
            Header = UiText.Get("Show side by side with the current tab"),
            IsEnabled = !ReferenceEquals(tab, _activeTab)
        };
        join.Click += (_, _) => JoinTab(tab);
        var close = new MenuItem { Header = UiText.Get("Close tab") };
        close.Click += (_, _) => CloseTab(tab);
        menu.Items.Add(split); menu.Items.Add(join);
        menu.Items.Add(new Separator()); menu.Items.Add(CreateTabColorMenu(tab));
        menu.Items.Add(new Separator()); menu.Items.Add(close);
        return menu;
    }

    private void JoinTabMenuItem_SubmenuOpened(object sender, RoutedEventArgs e)
    {
        JoinTabMenuItem.Items.Clear();

        foreach (var tab in _tabs.Where(tab => !ReferenceEquals(tab, _activeTab)))
        {
            var item = new MenuItem { Header = tab.Title };
            item.Click += (_, _) => JoinTab(tab);
            JoinTabMenuItem.Items.Add(item);
        }

        if (JoinTabMenuItem.Items.Count == 0)
            JoinTabMenuItem.Items.Add(new MenuItem { Header = UiText.Get("No other tabs"), IsEnabled = false });
    }

    private void MovePaneMenuItem_Click(object sender, RoutedEventArgs e) => MovePaneToNewTab(ActivePane);

    private void CloseTab(TerminalTab? tab)
    {
        if (tab is null)
            return;

        var index = _tabs.IndexOf(tab);

        if (index < 0)
            return;

        var wasActive = ReferenceEquals(tab, _activeTab);
        foreach (var pane in tab.Panes) pane.IsShown = false;
        _tabs.RemoveAt(index);

        if (_tabs.Count == 0)
        {
            _activeTab = null;
            foreach (var pane in tab.Panes)
                _ = pane.DisposeAsync();
            Close();
            return;
        }

        if (wasActive)
        {
            _activeTab = null;
            ShowTab(_tabs[Math.Min(index, _tabs.Count - 1)]);
        }
        else
            RebuildTabStrip();

        ActiveView.Focus();
        foreach (var pane in tab.Panes)
            _ = pane.DisposeAsync();
    }

    private void RebuildTabStrip()
    {
        _tabRenameEditor = null;
        if (_renamingTab is not null && !_tabs.Contains(_renamingTab)) { _renamingTab = null; _tabRenameDraft = ""; }
        Title = _activeTab?.Title ?? "RtlTerminal";
        MovePaneMenuItem.IsEnabled = _activeTab is { Panes.Count: > 1 } tabGroup && tabGroup.Panes.Contains(tabGroup.ActivePane);
        TabStrip.Children.Clear();
        foreach (var tab in _tabs)
        {
            var isActive = ReferenceEquals(tab, _activeTab);
            var chromeColor = ((SolidColorBrush)FindResource("TitleBarBrush")).Color;
            var displayColor = tab.BackgroundColor is { } tabColor ? TabColors.DisplayColor(tabColor, isActive, chromeColor) : (Color?)null;
            var customText = displayColor is { } background ? new SolidColorBrush(TabColors.TextColor(background)) : null;
            var panel = new DockPanel { Height = 28 };
            var closeButton = new Button
            {
                Name = "CloseTabButton", Width = 24, Height = 24, Margin = new Thickness(0, 0, 5, 0),
                HorizontalContentAlignment = HorizontalAlignment.Center,
                ToolTip = $"Close {tab.Title}", Tag = tab,
                Style = (Style)FindResource("TabCloseButtonStyle")
            };
            var closeGlyph = new System.Windows.Shapes.Path
            {
                Width = 8, Height = 8, Data = Geometry.Parse("M0,0 L8,8 M8,0 L0,8"), StrokeThickness = 1
            };
            closeGlyph.SetBinding(System.Windows.Shapes.Shape.StrokeProperty, new System.Windows.Data.Binding("Foreground") { Source = closeButton });
            closeButton.Content = closeGlyph;
            System.Windows.Automation.AutomationProperties.SetName(closeButton, $"Close {tab.Title}");
            if (customText is not null) closeButton.Foreground = customText;
            closeButton.Click += TabCloseButton_Click;
            DockPanel.SetDock(closeButton, Dock.Right);
            panel.Children.Add(closeButton);
            var label = new DockPanel();
            var icon = CreateProfileIcon(tab.Profile);
            icon.Margin = new Thickness(0,0,8,0);
            DockPanel.SetDock(icon, Dock.Left);
            label.Children.Add(icon);
            var tabTitle = new TextBlock
            {
                Text = tab.Title, FontFamily = new FontFamily("Segoe UI"), FontSize = 12,
                FontWeight = isActive ? FontWeights.SemiBold : FontWeights.Normal,
                TextTrimming = TextTrimming.CharacterEllipsis,
                VerticalAlignment = VerticalAlignment.Center
            };
            tabTitle.MouseLeftButtonDown += (_, e) =>
            {
                if (e.ClickCount != 2) return;
                e.Handled = true; BeginTabRename(tab);
            };
            label.Children.Add(ReferenceEquals(_renamingTab, tab) ? CreateTabRenameEditor(tab) : tabTitle);
            var selectButton = new Button
            {
                Content = label, HorizontalContentAlignment = HorizontalAlignment.Stretch,
                Padding = new Thickness(12, 0, 4, 0), ToolTip = tab.Title, Tag = tab,
                Foreground = customText ?? new SolidColorBrush(isActive
                    ? AppTheme.ActiveTabText : AppTheme.InactiveTabText),
                Style = (Style)FindResource("ChromeTabButtonStyle")
            };
            System.Windows.Automation.AutomationProperties.SetName(selectButton, tab.Title);
            selectButton.Click += TabButton_Click;
            selectButton.ContextMenu = CreateTabContextMenu(tab);
            panel.Children.Add(selectButton);
            var grid = new Grid { Width = 248, Height = 32, Margin = new Thickness(-4, 8, -4, 0), Background = Brushes.Transparent, Tag = tab };
            Panel.SetZIndex(grid, isActive ? 10 : 0);
            grid.ContextMenu = selectButton.ContextMenu;
            var activeBrush = new SolidColorBrush(displayColor ?? AppTheme.ActiveTab);
            var shape = new System.Windows.Shapes.Path
            {
                Data = isActive
                    ? Geometry.Parse("M0,32 Q4,32 4,28 L4,8 A8,8 0 0 1 12,0 L236,0 A8,8 0 0 1 244,8 L244,28 Q244,32 248,32 Z")
                    : Geometry.Parse("M4,32 L4,8 A8,8 0 0 1 12,0 L236,0 A8,8 0 0 1 244,8 L244,32 Z"),
                Fill = tab.BackgroundColor is not null || isActive ? activeBrush : Brushes.Transparent, IsHitTestVisible = false,
                Stroke = null, StrokeThickness = 0
            };
            grid.Children.Add(shape);
            if (!isActive && tab.BackgroundColor is null) grid.Children.Add(new Border
            {
                Width = 1, Height = 18, Background = new SolidColorBrush(AppTheme.TabSeparator),
                HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0,0,4,0), IsHitTestVisible = false
            });
            panel.Margin = new Thickness(0, 0, 4, 0);
            grid.Children.Add(panel);
            grid.MouseEnter += (_, _) => shape.Fill = tab.BackgroundColor is not null || isActive ? activeBrush : new SolidColorBrush(AppTheme.HoverTab);
            grid.MouseLeave += (_, _) => shape.Fill = tab.BackgroundColor is not null || isActive ? activeBrush : Brushes.Transparent;
            TabStrip.Children.Add(grid);
            if (isActive)
                Dispatcher.BeginInvoke(DispatcherPriority.Loaded, () => grid.BringIntoView());
        }
    }

    private void TitleTabsHost_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        TabScroller.MaxWidth = Math.Max(0, e.NewSize.Width - 72 - (AdministratorBadge?.Visibility == Visibility.Visible ? 30 : 0));
    }

    private void TabScroller_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
    {
        TabScroller.ScrollToHorizontalOffset(TabScroller.HorizontalOffset - e.Delta);
        e.Handled = true;
    }

    private void TabButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: TerminalTab tab })
            SelectTab(tab);
    }

    private void TabCloseButton_Click(object sender, RoutedEventArgs e)
    {
        e.Handled = true;

        if (sender is Button { Tag: TerminalTab tab })
            CloseTab(tab);
    }

    private void TerminalTextBox_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (GetEffectiveKey(e) == Key.Apps ||
            GetEffectiveKey(e) == Key.F10 && (Keyboard.Modifiers & ModifierKeys.Shift) != 0)
        {
            // Do not transfer focus into the popup while its opening key is held:
            // ContextMenu handles Apps key-up itself and would immediately close.
            _pendingContextMenuKey = GetEffectiveKey(e);
            e.Handled = true;
            return;
        }
        if (ActiveSession is null)
            return;

        var controlPressed = (Keyboard.Modifiers & ModifierKeys.Control) != 0;
        var shiftPressed = (Keyboard.Modifiers & ModifierKeys.Shift) != 0;

        if (controlPressed && shiftPressed && e.Key == Key.C)
        {
            CopySelection();
            e.Handled = true;
            return;
        }

        if (controlPressed && shiftPressed && e.Key == Key.V)
        {
            PasteClipboard();
            e.Handled = true;
            return;
        }

        if (controlPressed && e.Key == Key.C)
        {
            if (ActiveView.HasSelection)
                CopySelection();
            else
                SendUserKey(ActivePane, "\x03", Key.C, Keyboard.Modifiers);

            e.Handled = true;
            return;
        }

        if (controlPressed && e.Key == Key.V)
        {
            PasteClipboard();
            e.Handled = true;
            return;
        }

        if (controlPressed && GetEffectiveKey(e) == Key.Space)
        {
            SendUserKey(ActivePane, "\0", Key.Space, Keyboard.Modifiers);
            e.Handled = true;
            return;
        }

        if (controlPressed && shiftPressed && e.Key == Key.A)
        {
            ActiveView.SelectAll();
            e.Handled = true;
            return;
        }

        if (controlPressed && e.Key >= Key.A && e.Key <= Key.Z)
        {
            var controlCharacter = (char)(e.Key - Key.A + 1);
            SendUserKey(ActivePane, controlCharacter.ToString(), e.Key, Keyboard.Modifiers);
            e.Handled = true;
            return;
        }

        if (!controlPressed &&
            shiftPressed &&
            GetEffectiveKey(e) == Key.OemQuestion &&
            InputLanguageManager.Current.CurrentInputLanguage
                .TwoLetterISOLanguageName == "fa")
        {
            SendUserInput(ActivePane, "\u061f");
            e.Handled = true;
            return;
        }

        var key = GetEffectiveKey(e);
        var altPressed = (Keyboard.Modifiers & ModifierKeys.Alt) != 0;
        var sequence = GetTerminalKeySequence(
            key,
            shiftPressed,
            altPressed,
            controlPressed,
            ActiveSnapshot?.Modes.ApplicationCursorKeys == true);

        if (sequence is null)
            return;

        SendUserKey(ActivePane, sequence, key, Keyboard.Modifiers);
        e.Handled = true;
    }

    private static Key GetEffectiveKey(KeyEventArgs e) =>
        e.Key switch
        {
            Key.System => e.SystemKey,
            Key.ImeProcessed => e.ImeProcessedKey,
            _ => e.Key
        };

    private void TerminalTextBox_PreviewKeyUp(object sender, KeyEventArgs e)
    {
        if (_pendingContextMenuKey != GetEffectiveKey(e)) return;
        _pendingContextMenuKey = null;
        e.Handled = true;
        Dispatcher.BeginInvoke(DispatcherPriority.Input, () =>
        {
            if (!ActiveView.IsKeyboardFocusWithin) return;
            var menu = CreateTerminalContextMenu();
            menu.Placement = System.Windows.Controls.Primitives.PlacementMode.Center;
            menu.Closed += (_, _) => ActiveView.Focus();
            menu.IsOpen = true;
        });
    }

    private static string? GetTerminalKeySequence(
        Key key,
        bool shift,
        bool alt,
        bool control,
        bool applicationCursorKeys)
    {
        if (key == Key.Tab)
            return shift ? "\x1b[Z" : "\t";

        var modifier = 1 + (shift ? 1 : 0) + (alt ? 2 : 0) +
            (control ? 4 : 0);
        var hasModifier = modifier > 1;
        var final = key switch
        {
            Key.Up => 'A',
            Key.Down => 'B',
            Key.Right => 'C',
            Key.Left => 'D',
            Key.Home => 'H',
            Key.End => 'F',
            _ => '\0'
        };

        if (final != '\0')
        {
            if (hasModifier)
                return $"\x1b[1;{modifier}{final}";

            return applicationCursorKeys
                ? $"\x1bO{final}"
                : $"\x1b[{final}";
        }

        var functionFinal = key switch
        {
            Key.F1 => 'P',
            Key.F2 => 'Q',
            Key.F3 => 'R',
            Key.F4 => 'S',
            _ => '\0'
        };

        if (functionFinal != '\0')
            return hasModifier
                ? $"\x1b[1;{modifier}{functionFinal}"
                : $"\x1bO{functionFinal}";

        var tildeCode = key switch
        {
            Key.Insert => 2,
            Key.Delete => 3,
            Key.PageUp => 5,
            Key.PageDown => 6,
            Key.F5 => 15,
            Key.F6 => 17,
            Key.F7 => 18,
            Key.F8 => 19,
            Key.F9 => 20,
            Key.F10 => 21,
            Key.F11 => 23,
            Key.F12 => 24,
            _ => 0
        };

        if (tildeCode != 0)
            return hasModifier
                ? $"\x1b[{tildeCode};{modifier}~"
                : $"\x1b[{tildeCode}~";

        return key switch
        {
            Key.Enter => alt ? "\x1b\r" : "\r",
            Key.Space => alt ? "\x1b " : " ",
            Key.Back => alt ? "\x1b\x7f" : "\x7f",
            Key.Escape => "\x1b",
            _ => null
        };
    }

    private void CopySelection()
    {
        if (!ActiveView.HasSelection)
            return;

        try { ActiveView.CopySelection(); }
        catch (System.Runtime.InteropServices.ExternalException)
        {
            UiText.Show(this, "The clipboard is busy. Please try copying again.", "Clipboard", MessageBoxButton.OK, MessageBoxImage.Information);
        }
    }

    private async void PasteClipboard()
    {
        try { await PasteClipboardCoreAsync(); }
        catch (System.Runtime.InteropServices.ExternalException)
        {
            UiText.Show(this, "The clipboard is busy. Please try pasting again.", "Clipboard", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or ArgumentException or System.ComponentModel.Win32Exception)
        { UiText.Show(this, exception.Message, "Paste", MessageBoxButton.OK, MessageBoxImage.Error); }
    }

    private async Task PasteClipboardCoreAsync()
    {
        var pane = ActivePane;
        if (pane?.Session is null) return;
        if (Clipboard.ContainsFileDropList())
        {
            var paths = Clipboard.GetFileDropList().Cast<string>().Where(p => !string.IsNullOrWhiteSpace(p)).ToArray();
            await WriteClipboardPathsAsync(pane, paths);
        }
        else if (Clipboard.ContainsImage())
        {
            var imagePath = SaveClipboardImage();
            if (imagePath is not null) await WriteClipboardPathsAsync(pane, [imagePath]);
        }
        else if (Clipboard.ContainsText())
            WritePanePaste(pane, Clipboard.GetText().Replace("\r\n", "\r").Replace("\n", "\r"));
    }

    private async Task WriteClipboardPathsAsync(TerminalPane pane, IReadOnlyList<string> paths)
    {
        if (pane.Session is null || paths.Count == 0) return;
        var formatted = new List<string>();
        foreach (var path in paths)
        {
            ShellPath.Validate(path);
            var fullPath = Path.GetFullPath(path);
            formatted.Add(pane.Profile == TerminalProfile.Wsl
                ? WslPath.Quote(await WslPathResolver.ConvertAsync(fullPath, pane.WslDistribution))
                : pane.Profile == TerminalProfile.GitBash
                    ? WslPath.Quote(fullPath)
                    : ShellPath.Windows(fullPath, pane.Profile == TerminalProfile.PowerShell || pane.Profile == TerminalProfile.PowerShell7, escapePercent: pane.LastRenderedSnapshot?.Modes.BracketedPaste != true));
        }
        // Conversion is atomic: no partial paste if any file cannot be resolved.
        WritePanePaste(pane, string.Join(" ", formatted));
    }

    private void WritePanePaste(TerminalPane pane, string text)
    {
        if (pane.Session is not { } session || string.IsNullOrEmpty(text)) return;
        SendUserInput(pane, pane.LastRenderedSnapshot?.Modes.BracketedPaste == true ? $"\x1b[200~{text}\x1b[201~" : text);
    }

    private string? SaveClipboardImage()
    {
        var image = Clipboard.GetImage();

        return image is null ? null : SaveTemporaryImage(image);
    }

    private string SaveTemporaryImage(BitmapSource image)
    {
        var directory = Path.Combine(
            Path.GetTempPath(),
            "RtlTerminal",
            "Clipboard");
        Directory.CreateDirectory(directory);

        var path = Path.Combine(
            directory,
            $"clipboard-{DateTime.UtcNow:yyyyMMdd-HHmmssfff}-{Guid.NewGuid():N}.png");
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(image));

        using (var stream = File.Create(path))
            encoder.Save(stream);

        _temporaryClipboardFiles.Add(path);
        return path;
    }

    private void CleanupTemporaryClipboardFiles()
    {
        foreach (var path in _temporaryClipboardFiles)
        {
            try
            {
                File.Delete(path);
            }
            catch (IOException)
            {
            }
            catch (UnauthorizedAccessException)
            {
            }
        }

        _temporaryClipboardFiles.Clear();
    }

    private void ReadOutputLoop(TerminalPane pane)
    {
        var session = pane.Session;
        var buffer = pane.Buffer;
        var cancellationTokenSource = pane.CancellationTokenSource;

        if (session is null ||
            buffer is null ||
            cancellationTokenSource is null)
        {
            return;
        }

        var bytes = new byte[8192];
        var characters = new char[Encoding.UTF8.GetMaxCharCount(bytes.Length)];
        var decoder = Encoding.UTF8.GetDecoder();
        var cancellationToken = cancellationTokenSource.Token;

        while (!cancellationToken.IsCancellationRequested)
        {
            int byteCount;

            try
            {
                byteCount = session.Read(bytes);
            }
            catch (ObjectDisposedException)
            {
                break;
            }
            catch
            {
                break;
            }

            if (byteCount <= 0)
                break;

            var characterCount = decoder.GetChars(
                bytes,
                0,
                byteCount,
                characters,
                0,
                flush: false);

            var output = new string(characters, 0, characterCount);

            var snapshot = buffer.Process(output);
            pane.Playback?.Observe(snapshot);
            pane.Recorder?.Observe(snapshot);
            ObserveDirectory(pane, TerminalDirectory.FromReport(snapshot.WorkingDirectory, pane.WslDistribution));

            foreach (var response in snapshot.Responses)
                session.Write(response);

            if (snapshot.Modes.SynchronizedOutput)
                SuspendRendering(pane, snapshot.Revision);
            else
                QueueRender(pane, snapshot);
        }
        pane.SessionEnded = true;
        pane.Playback?.Close();
    }

    private void SuspendRendering(TerminalPane pane, long revision)
    {
        lock (_renderLock)
        {
            pane.LatestQueuedRevision = Math.Max(
                pane.LatestQueuedRevision,
                revision);
            pane.PendingSnapshot = null;
        }
    }

    private void QueueRender(TerminalPane pane, TerminalSnapshot snapshot)
    {
        lock (_renderLock)
        {
            if (snapshot.Revision < pane.LatestQueuedRevision)
                return;

            pane.LatestQueuedRevision = snapshot.Revision;
            pane.PendingSnapshot = snapshot;

            // Panes of hidden tabs are drawn when their tab is shown again.
            if (!pane.IsShown)
                return;

            if (_renderTimer.IsEnabled || _renderStartQueued)
                return;

            _renderStartQueued = true;
        }

        Dispatcher.BeginInvoke(
            DispatcherPriority.Background,
            StartRenderTimer);
    }

    private void StartRenderTimer()
    {
        lock (_renderLock)
            _renderStartQueued = false;

        if (!_renderTimer.IsEnabled)
            _renderTimer.Start();
    }

    private void RenderTimer_Tick(object? sender, EventArgs e)
    {
        var panes = _activeTab?.Panes.ToArray() ?? [];

        foreach (var pane in panes)
        {
            TerminalSnapshot? snapshot;

            lock (_renderLock)
            {
                snapshot = pane.PendingSnapshot;
                pane.PendingSnapshot = null;
            }

            if (snapshot is not null)
                Render(pane, snapshot);
        }

        lock (_renderLock)
        {
            if (panes.All(pane => pane.PendingSnapshot is null))
                _renderTimer.Stop();
        }
    }

    // Draws pending output, or redraws the last frame so view settings take effect.
    private void RenderVisiblePanes()
    {
        if (_activeTab is null)
            return;

        foreach (var pane in _activeTab.Panes)
        {
            bool pending;
            lock (_renderLock)
                pending = pane.PendingSnapshot is not null;

            if (pending)
                StartRenderTimer();
            else if (pane.LastRenderedSnapshot is { } snapshot)
                Render(pane, snapshot);
        }
    }

    private void Render(TerminalPane pane, TerminalSnapshot snapshot)
    {
        pane.LastRenderedSnapshot = snapshot;
        _restoringScrollPosition = true;
        pane.View.Present(snapshot, SmartRtlMenuItem.IsChecked,
            _cellWidth, _lineHeight, pane.FollowOutput, RowRtlMenuItem.IsChecked);
        _restoringScrollPosition = false;
    }

    private void ApplySavedFontSettings()
    {
        var settings = AppSettings.LoadFont();

        if (settings is { } savedSettings)
            ApplyFontSettings(savedSettings);
    }

    private long _zoomReflowDue;

    private void ApplyFontSettings(TerminalFontSettings settings)
    {
        foreach (var view in AllViews())
        {
            view.FontFamily = BundledFonts.Resolve(settings.Family);
            view.FontSize = settings.Size;
            view.FontWeight = settings.Bold
                ? FontWeights.Bold
                : FontWeights.Normal;
            view.FontStyle = settings.Italic
                ? FontStyles.Italic
                : FontStyles.Normal;
        }

        UpdateFontMetrics();
        UpdateZoomMenuVisibility();
        // Hidden tabs get the new grid size when they are shown again.
        foreach (var pane in _tabs.SelectMany(tab => tab.Panes))
            pane.GridSize = default;

        // Font changes need new measurements, not removal/recreation of pane views.
        UpdatePaneSizing();
        QueueViewportResize();
        RenderVisiblePanes();
    }

    private void UpdateFontMetrics()
    {
        var typeface = new Typeface(
            TerminalTextBox.FontFamily,
            TerminalTextBox.FontStyle,
            TerminalTextBox.FontWeight,
            TerminalTextBox.FontStretch);
        var pixelsPerDip = VisualTreeHelper.GetDpi(this).PixelsPerDip;
        var measurement = new FormattedText(
            "M",
            System.Globalization.CultureInfo.InvariantCulture,
            FlowDirection.LeftToRight,
            typeface,
            TerminalTextBox.FontSize,
            Brushes.White,
            pixelsPerDip);

        _cellWidth = Math.Ceiling(Math.Max(4, measurement.WidthIncludingTrailingWhitespace) * pixelsPerDip) / pixelsPerDip;
        _lineHeight = Math.Ceiling(Math.Max(
            TerminalTextBox.FontSize + 3,
            measurement.Height + 2) * pixelsPerDip) / pixelsPerDip;
    }

    private short GetColumns(TerminalView view)
    {
        var width = view.ViewportWidth > 0
            ? view.ViewportWidth
            : view.ActualWidth - view.Padding.Left - view.Padding.Right - 18;
        return (short)Math.Clamp((int)Math.Floor(Math.Max(80, width) / _cellWidth), 10, 300);
    }

    private short GetRows(TerminalView view)
    {
        var height = view.ViewportHeight > 0
            ? view.ViewportHeight
            : view.ActualHeight - view.Padding.Top - view.Padding.Bottom;
        return (short)Math.Clamp((int)Math.Floor(Math.Max(120, height) / _lineHeight), 10, 100);
    }

    private enum TerminalProfile
    {
        CommandPrompt,
        PowerShell,
        Wsl,
        PowerShell7,
        GitBash
    }

    private enum PaneSplitDirection { None, SideBySide, Stacked }

    private sealed class PaneLayout
    {
        public TerminalPane? Pane { get; }
        public PaneLayout? First { get; private set; }
        public PaneLayout? Second { get; private set; }
        public bool Stacked { get; }
        public double Ratio { get; set; } = .5;
        public PaneLayout(TerminalPane pane) => Pane = pane;
        public PaneLayout(PaneLayout first, PaneLayout second, bool stacked)
        { First = first; Second = second; Stacked = stacked; }
        public Size MinimumSize(double width, double height)
        {
            if (Pane is not null) return new Size(width, height);
            var a = First!.MinimumSize(width, height); var b = Second!.MinimumSize(width, height);
            return Stacked ? new Size(Math.Max(a.Width, b.Width), a.Height + b.Height + 5)
                : new Size(a.Width + b.Width + 5, Math.Max(a.Height, b.Height));
        }
        public PaneLayout Replace(TerminalPane pane, PaneLayout replacement)
        {
            if (Pane is not null) return ReferenceEquals(Pane, pane) ? replacement : this;
            First = First!.Replace(pane, replacement); Second = Second!.Replace(pane, replacement);
            return this;
        }
        public PaneLayout? Without(TerminalPane pane)
        {
            if (Pane is not null) return ReferenceEquals(Pane, pane) ? null : this;
            First = First!.Without(pane); Second = Second!.Without(pane);
            if (First is null) return Second;
            if (Second is null) return First;
            return this;
        }
    }

    // A tab in the title bar: one or more panes shown side by side.
    private sealed class TerminalTab(TerminalPane pane)
    {
        public PaneLayout LayoutRoot { get; set; } = new(pane);
        public List<TerminalPane> Panes { get; } = [pane];
        public TerminalPane ActivePane { get; set; } = pane;
        public Color? BackgroundColor { get; set; }
        public string? CustomTitle { get; set; }
        public TerminalProfile Profile => ActivePane.Profile;
        public string Title => CustomTitle ?? (Panes.Count > 1 ? $"{ActivePane.Title} (+{Panes.Count - 1})" : ActivePane.Title);
    }

    private sealed class TerminalPane(
        int number,
        TerminalProfile profile,
        string profileTitle)
    {
        private int _disposeStarted;

        public int Number { get; } = number;
        public TerminalProfile Profile { get; } = profile;
        public string Title => $"{(Profile == TerminalProfile.Wsl ? WslDistribution ?? profileTitle : profileTitle)} {Number}";
        public TerminalView View { get; set; } = null!;
        public Border Frame { get; } = new();
        public bool IsShown { get; set; }
        public string? WslDistribution { get; set; }
        public ConPtySession? Session { get; set; }
        public volatile MacroPlayback? Playback;
        public volatile MacroRecorder? Recorder;
        public object DirectoryHistoryGate { get; } = new();
        public string? CurrentDirectory { get; set; }
        public string? LastHistoryDirectory { get; set; }
        public bool DirectoryPollPending { get; set; }
        public volatile bool SessionEnded;
        public TerminalBuffer? Buffer { get; set; }
        public CancellationTokenSource? CancellationTokenSource { get; set; }
        public TerminalSnapshot? PendingSnapshot { get; set; }
        public long LatestQueuedRevision { get; set; }
        public TerminalSnapshot? LastRenderedSnapshot { get; set; }
        public bool FollowOutput { get; set; } = true;
        public (short Columns, short Rows) GridSize { get; set; }
        public double VerticalOffset { get; set; }

        public Task DisposeAsync()
        {
            if (Interlocked.Exchange(ref _disposeStarted, 1) != 0)
                return Task.CompletedTask;

            var session = Session;
            var cancellationTokenSource = CancellationTokenSource;
            CancellationTokenSource = null;
            Playback?.Close();
            Session = null;

            return Task.Run(() =>
            {
                try
                {
                    // Keep ReadOutputLoop draining until ClosePseudoConsole
                    // completes and closes the output channel.
                    session?.Dispose();
                }
                finally
                {
                    cancellationTokenSource?.Cancel();
                    cancellationTokenSource?.Dispose();
                }
            });
        }
    }
}
