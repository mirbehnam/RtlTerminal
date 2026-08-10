using System;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Controls;
using System.Windows.Threading;
using Microsoft.Win32;

namespace RtlTerminal;

public partial class MainWindow : Window
{
    private static readonly TerminalColor DefaultForeground = new(230, 230, 230);
    private static readonly TerminalColor DefaultBackground = new(12, 12, 12);
    private static readonly TerminalColor LinkForeground = new(86, 156, 214);
    private static readonly Regex LinkPattern = new(
        @"(?i)\b(?:https?://|www\.)[^\s<>{}\[\]""']+",
        RegexOptions.Compiled);
    private static readonly Dictionary<TerminalColor, SolidColorBrush> BrushCache = [];
    private readonly object _renderLock = new();
    private readonly DispatcherTimer _renderTimer;
    private readonly List<RenderedLine> _renderedLines = [];
    private readonly List<TerminalTab> _tabs = [];
    private readonly List<string> _temporaryClipboardFiles = [];
    private TerminalTab? _activeTab;
    private ConPtySession? _session;
    private TerminalBuffer? _terminalBuffer;
    private CancellationTokenSource? _cancellationTokenSource;
    private TerminalSnapshot? _pendingSnapshot;
    private bool _renderStartQueued;
    private bool _updatingContextMenuItem;
    private long _latestQueuedRevision;
    private TerminalSnapshot? _lastRenderedSnapshot;
    private FlowDocument? _terminalDocument;
    private double _cellWidth = 8.5;
    private double _lineHeight = 18;
    private bool _followOutput = true;
    private bool _restoringScrollPosition;
    private int _scrollRequestVersion;
    private int _nextTabNumber = 1;
    private int _renderedScrollbackCount;
    private long _renderedScrollbackStartIndex;
    private bool _renderedSmartRtlEnabled = true;
    private TerminalProfile _defaultProfile = TerminalProfile.CommandPrompt;
    private int _historySize = 2000;
    private bool _updateCheckInProgress;

    public MainWindow()
    {
        InitializeComponent();
        SmartRtlMenuItem.IsChecked = true;
        _renderTimer = new DispatcherTimer(DispatcherPriority.Background)
        {
            Interval = TimeSpan.FromMilliseconds(50)
        };
        _renderTimer.Tick += RenderTimer_Tick;
        _defaultProfile = LoadDefaultProfile();
        _historySize = AppSettings.LoadHistorySize();
        ApplySavedFontSettings();
        UpdateFontMetrics();
    }

    private void Window_Loaded(object sender, RoutedEventArgs e)
    {
        RefreshContextMenuIntegrationState();
        PromptForContextMenuIntegration();
        TerminalTextBox.Focus();
        TerminalTextBox.UpdateLayout();
        Dispatcher.BeginInvoke(
            DispatcherPriority.Loaded,
            () => CreateTerminalTab(_defaultProfile));
        _ = CheckForUpdatesOnStartupAsync();
    }

    private void CreateTerminalTab(
        TerminalProfile profile,
        string? requestedStartupDirectory = null)
    {
        SaveActiveTabState();
        var startupDirectory = ResolveStartupDirectory(
            requestedStartupDirectory);

        var tab = new TerminalTab(
            _nextTabNumber++,
            profile,
            GetProfileTitle(profile));
        _tabs.Add(tab);
        _activeTab = tab;
        LoadTabState(tab);
        RebuildTabStrip();

        try
        {
            var columns = GetColumns();
            var rows = GetRows();
            _terminalBuffer = new TerminalBuffer(
                columns,
                rows,
                _historySize);
            _cancellationTokenSource = new CancellationTokenSource();
            _session = new ConPtySession(columns, rows);
            _session.Start(
                GetProfileCommand(profile),
                startupDirectory);

            if (profile == TerminalProfile.CommandPrompt &&
                startupDirectory is not null)
            {
                AppSettings.RememberCmdDirectory(startupDirectory);
            }

            SaveActiveTabState();
            _ = Task.Run(() => ReadOutputLoop(tab));
        }
        catch (Exception exception)
        {
            TerminalTextBox.Document.Blocks.Clear();
            TerminalTextBox.Document.Blocks.Add(
                new Paragraph(new Run(
                    "خطا در اجرای ConPTY:" +
                    Environment.NewLine +
                    exception))
                {
                    FlowDirection = FlowDirection.RightToLeft
                });
            tab.LastRenderedSnapshot = null;
        }

        TerminalTextBox.Focus();
    }

    private void Window_Closed(object? sender, EventArgs e)
    {
        SaveActiveTabState();

        foreach (var tab in _tabs)
            _ = tab.DisposeAsync();

        CleanupTemporaryClipboardFiles();
    }

    private void Window_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        MaximizeButton.Content =
            WindowState == WindowState.Maximized ? "❐" : "□";

        if (_session is null || _terminalBuffer is null)
            return;

        var columns = GetColumns();
        var rows = GetRows();
        _session.Resize(columns, rows);
        QueueRender(_terminalBuffer.Resize(columns, rows));
    }

    private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        var controlPressed = (Keyboard.Modifiers & ModifierKeys.Control) != 0;
        var shiftPressed = (Keyboard.Modifiers & ModifierKeys.Shift) != 0;

        if (controlPressed && shiftPressed && e.Key == Key.T)
        {
            CreateTerminalTab(_defaultProfile);
            e.Handled = true;
            return;
        }

        if (controlPressed && e.Key == Key.Tab)
        {
            SelectRelativeTab(shiftPressed ? -1 : 1);
            e.Handled = true;
            return;
        }

        if (controlPressed && e.Key == Key.W)
        {
            CloseTab(_activeTab);
            e.Handled = true;
        }
    }

    private void NewTabButton_Click(object sender, RoutedEventArgs e)
    {
        CreateTerminalTab(_defaultProfile);
    }

    private void ProfileMenuButton_Click(object sender, RoutedEventArgs e)
    {
        var menu = new ContextMenu
        {
            Style = (Style)FindResource("DarkContextMenuStyle")
        };

        AddProfileMenuItem(menu, "Command Prompt", TerminalProfile.CommandPrompt);
        AddProfileMenuItem(menu, "PowerShell", TerminalProfile.PowerShell);

        if (IsWslAvailable())
            AddProfileMenuItem(menu, "WSL", TerminalProfile.Wsl);

        menu.PlacementTarget = sender as UIElement;
        menu.IsOpen = true;
    }

    private void AddProfileMenuItem(
        ItemsControl menu,
        string header,
        TerminalProfile profile)
    {
        var item = new MenuItem { Header = header };
        item.Click += (_, _) => SelectTerminalProfile(profile);
        menu.Items.Add(item);
    }

    private void SelectTerminalProfile(TerminalProfile profile)
    {
        _defaultProfile = profile;
        AppSettings.SaveTerminalProfile(profile.ToString());
        CreateTerminalTab(profile);
    }

    private void MinimizeButton_Click(object sender, RoutedEventArgs e)
    {
        WindowState = WindowState.Minimized;
    }

    private void MaximizeButton_Click(object sender, RoutedEventArgs e)
    {
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
        if (_terminalBuffer is null)
        {
            MessageBox.Show(
                this,
                "There is no active terminal session to export.",
                "Export session",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
            return;
        }

        var dialog = new SaveFileDialog
        {
            Title = "Export terminal session",
            Filter = "Text files (*.txt)|*.txt|All files (*.*)|*.*",
            DefaultExt = ".txt",
            AddExtension = true,
            FileName = $"RtlTerminal-{DateTime.Now:yyyyMMdd-HHmmss}.txt"
        };

        if (dialog.ShowDialog(this) != true)
            return;

        try
        {
            var snapshot = _terminalBuffer.CaptureSnapshot();
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
            MessageBox.Show(
                this,
                $"The session could not be exported.{Environment.NewLine}{exception.Message}",
                "Export session",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
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
        LastDirectoriesMenuItem.Items.Clear();

        foreach (var directory in AppSettings.LoadLastCmdDirectories())
        {
            if (!Directory.Exists(directory))
                continue;

            var item = new MenuItem
            {
                Header = new TextBlock
                {
                    Text = directory,
                    MaxWidth = 520,
                    TextTrimming = TextTrimming.CharacterEllipsis,
                    FlowDirection = FlowDirection.LeftToRight
                },
                ToolTip = directory
            };
            item.Click += (_, _) => CreateTerminalTab(
                TerminalProfile.CommandPrompt,
                directory);
            LastDirectoriesMenuItem.Items.Add(item);
        }

        if (LastDirectoriesMenuItem.Items.Count == 0)
        {
            LastDirectoriesMenuItem.Items.Add(new MenuItem
            {
                Header = "No recent directories",
                IsEnabled = false
            });
        }
    }

    private void TerminalTextBox_PreviewTextInput(
        object sender,
        TextCompositionEventArgs e)
    {
        if (_session is null || string.IsNullOrEmpty(e.Text))
            return;

        _session.Write(e.Text);
        e.Handled = true;
    }

    private void TerminalTextBox_ScrollChanged(
        object sender,
        ScrollChangedEventArgs e)
    {
        if (_restoringScrollPosition)
            return;

        if (Math.Abs(e.VerticalChange) < 0.01)
            return;

        _followOutput =
            e.ExtentHeight <= e.ViewportHeight ||
            e.VerticalOffset >= e.ExtentHeight - e.ViewportHeight - 2;
    }

    private void TerminalTextBox_PreviewMouseRightButtonDown(
        object sender,
        MouseButtonEventArgs e)
    {
        if (!TerminalTextBox.Selection.IsEmpty)
            return;

        PasteClipboard();
        e.Handled = true;
        TerminalTextBox.Focus();
    }

    private void SmartRtlMenuItem_Changed(object sender, RoutedEventArgs e)
    {
        if (_lastRenderedSnapshot is not null)
            Render(_lastRenderedSnapshot);

        TerminalTextBox.Focus();
    }

    private void FontSettingsMenuItem_Click(object sender, RoutedEventArgs e)
    {
        var settingsWindow = new FontSettingsWindow(
            TerminalTextBox.FontFamily.Source,
            TerminalTextBox.FontSize,
            TerminalTextBox.FontWeight,
            TerminalTextBox.FontStyle,
            _historySize)
        {
            Owner = this
        };

        if (settingsWindow.ShowDialog() != true)
            return;

        ApplyHistorySize(settingsWindow.SelectedHistorySize);
        ApplyFontSettings(settingsWindow.SelectedSettings);
        AppSettings.SaveFont(settingsWindow.SelectedSettings);
        AppSettings.SaveHistorySize(settingsWindow.SelectedHistorySize);
        TerminalTextBox.Focus();
    }

    private void ApplyHistorySize(int historySize)
    {
        _historySize = historySize;
        SaveActiveTabState();

        foreach (var tab in _tabs)
        {
            if (tab.Buffer is not { } buffer)
                continue;

            QueueRender(
                tab,
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
                MessageBox.Show(
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
        var originalHeader = CheckForUpdatesMenuItem.Header;

        if (manual)
            CheckForUpdatesMenuItem.Header = "Checking for updates...";

        try
        {
            var result = await UpdateService.CheckAsync();

            if (!result.IsUpdateAvailable)
            {
                if (manual)
                {
                    MessageBox.Show(
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
                MessageBox.Show(
                    this,
                    "Rtl Terminal could not check GitHub for updates.\n\n" +
                    exception.Message,
                    "Check for updates",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
            }
        }
        finally
        {
            CheckForUpdatesMenuItem.Header = originalHeader;
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
            MessageBox.Show(
                this,
                "The update page could not be opened.\n\n" + exception.Message,
                "Rtl Terminal update",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
        }
    }

    private void AboutMenuItem_Click(object sender, RoutedEventArgs e)
    {
        MessageBox.Show(
            this,
            """
            Rtl Terminal
            by behnamapps

            Developer: behnam tajadini
            YouTube: aka_techno

            تقدیم به همه فارسی زبانان
            """,
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
            MessageBox.Show(
                this,
                "تغییر منوی راست‌کلیک انجام نشد." +
                Environment.NewLine +
                exception.Message,
                "RtlTerminal",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
        finally
        {
            RefreshContextMenuIntegrationState();
            TerminalTextBox.Focus();
        }
    }

    private void PromptForContextMenuIntegration()
    {
        if (ContextMenuIntegration.HasAnsweredInitialPrompt())
            return;

        var result = MessageBox.Show(
            this,
            "آیا گزینه «Open in RtlTerminal» به منوی راست‌کلیک پوشه‌ها اضافه شود؟",
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
            MessageBox.Show(
                this,
                "افزودن منوی راست‌کلیک انجام نشد." +
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
        ContextMenuIntegrationMenuItem.Header =
            ContextMenuIntegrationMenuItem.IsChecked
                ? "Remove _Open in RtlTerminal"
                : "Add _Open in RtlTerminal";
        _updatingContextMenuItem = false;
    }

    private static string? ResolveStartupDirectory(
        string? requestedDirectory = null)
    {
        if (TryResolveDirectory(requestedDirectory, out var directory))
            return directory;

        var arguments = Environment.GetCommandLineArgs();

        if (arguments.Length >= 2 &&
            TryResolveDirectory(arguments[1], out directory))
        {
            return directory;
        }

        return TryResolveDirectory(Environment.CurrentDirectory, out directory)
            ? directory
            : null;
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
            TerminalProfile.PowerShell => "PowerShell",
            TerminalProfile.Wsl => "WSL",
            _ => "Command Prompt"
        };

    private static string GetProfileCommand(TerminalProfile profile) =>
        profile switch
        {
            TerminalProfile.PowerShell =>
                """
                C:\Windows\System32\WindowsPowerShell\v1.0\powershell.exe -NoLogo -NoExit -Command "$lines=@('+--------------------------------------------------------+','| RtlTerminal v1.0.3                                     |','|                                                        |','| Author : Behnam Tajadini                               |','| Source : github.com/mirbehnam/RtlTerminal              |','| YouTube: @aka_techno                                   |','+--------------------------------------------------------+','','  پشتیبانی کامل از زبان فارسی و راست‌به‌چپ',''); $lines | ForEach-Object { Write-Host $_ }"
                """,
            TerminalProfile.Wsl =>
                """
                C:\Windows\System32\wsl.exe --exec sh -lc "printf '%s\n' '+--------------------------------------------------------+' '| RtlTerminal v1.0.3                                     |' '|                                                        |' '| Author : Behnam Tajadini                               |' '| Source : github.com/mirbehnam/RtlTerminal              |' '| YouTube: @aka_techno                                   |' '+--------------------------------------------------------+' '' '  پشتیبانی کامل از زبان فارسی و راست‌به‌چپ' ''; exec \"${SHELL:-/bin/bash}\" -l"
                """,
            _ =>
                """
                C:\Windows\System32\cmd.exe /D /Q /K "chcp 65001>nul & echo +--------------------------------------------------------+& echo ^| RtlTerminal v1.0.3                                     ^|& echo ^|                                                        ^|& echo ^| Author : Behnam Tajadini                               ^|& echo ^| Source : github.com/mirbehnam/RtlTerminal              ^|& echo ^| YouTube: @aka_techno                                   ^|& echo +--------------------------------------------------------+& echo.& echo   پشتیبانی کامل از زبان فارسی و راست‌به‌چپ& echo."
                """
        };

    private static bool IsWslAvailable()
    {
        var windowsDirectory =
            Environment.GetFolderPath(Environment.SpecialFolder.Windows);
        return File.Exists(Path.Combine(windowsDirectory, "System32", "wsl.exe"));
    }

    private static TerminalProfile LoadDefaultProfile()
    {
        var savedProfile = AppSettings.LoadTerminalProfile();

        if (!Enum.TryParse(savedProfile, out TerminalProfile profile) ||
            !Enum.IsDefined(profile) ||
            profile == TerminalProfile.Wsl && !IsWslAvailable())
        {
            return TerminalProfile.CommandPrompt;
        }

        return profile;
    }

    private void SaveActiveTabState()
    {
        if (_activeTab is null)
            return;

        _activeTab.Session = _session;
        _activeTab.Buffer = _terminalBuffer;
        _activeTab.CancellationTokenSource = _cancellationTokenSource;
        _activeTab.PendingSnapshot = _pendingSnapshot;
        _activeTab.RenderStartQueued = _renderStartQueued;
        _activeTab.LatestQueuedRevision = _latestQueuedRevision;
        _activeTab.LastRenderedSnapshot = _lastRenderedSnapshot;
        _activeTab.Document = _terminalDocument;
        _activeTab.RenderedScrollbackCount = _renderedScrollbackCount;
        _activeTab.RenderedScrollbackStartIndex =
            _renderedScrollbackStartIndex;
        _activeTab.RenderedSmartRtlEnabled = _renderedSmartRtlEnabled;
        _activeTab.FollowOutput = _followOutput;
        _activeTab.RenderedLines.Clear();
        _activeTab.RenderedLines.AddRange(_renderedLines);

        var scrollViewer = FindVisualChild<ScrollViewer>(TerminalTextBox);
        _activeTab.VerticalOffset = scrollViewer?.VerticalOffset ?? 0;
    }

    private void LoadTabState(TerminalTab tab)
    {
        lock (_renderLock)
        {
            _session = tab.Session;
            _terminalBuffer = tab.Buffer;
            _cancellationTokenSource = tab.CancellationTokenSource;
            _pendingSnapshot = tab.PendingSnapshot;
            _renderStartQueued = tab.RenderStartQueued;
            _latestQueuedRevision = tab.LatestQueuedRevision;
        }

        _lastRenderedSnapshot = tab.LastRenderedSnapshot;
        _terminalDocument = tab.Document;
        _renderedScrollbackCount = tab.RenderedScrollbackCount;
        _renderedScrollbackStartIndex = tab.RenderedScrollbackStartIndex;
        _renderedSmartRtlEnabled = tab.RenderedSmartRtlEnabled;
        _followOutput = tab.FollowOutput;
        _renderedLines.Clear();
        _renderedLines.AddRange(tab.RenderedLines);

        if (_terminalDocument is null)
        {
            TerminalTextBox.Document = new FlowDocument();
        }
        else
        {
            TerminalTextBox.Document = _terminalDocument;
            ScheduleScrollRestore(
                _terminalDocument,
                followOutput: false,
                verticalOffset: tab.VerticalOffset);
        }
    }

    private void SelectTab(TerminalTab tab)
    {
        if (ReferenceEquals(tab, _activeTab))
            return;

        SaveActiveTabState();
        _activeTab = tab;
        LoadTabState(tab);
        RebuildTabStrip();

        if (_pendingSnapshot is not null)
            StartRenderTimer();
        else if (_lastRenderedSnapshot is not null &&
            (_terminalDocument is null ||
                _renderedSmartRtlEnabled != SmartRtlMenuItem.IsChecked))
        {
            Render(_lastRenderedSnapshot);
        }

        TerminalTextBox.Focus();
    }

    private void SelectRelativeTab(int direction)
    {
        if (_activeTab is null || _tabs.Count < 2)
            return;

        var currentIndex = _tabs.IndexOf(_activeTab);
        var nextIndex = (currentIndex + direction + _tabs.Count) % _tabs.Count;
        SelectTab(_tabs[nextIndex]);
    }

    private void CloseTab(TerminalTab? tab)
    {
        if (tab is null)
            return;

        var index = _tabs.IndexOf(tab);

        if (index < 0)
            return;

        if (ReferenceEquals(tab, _activeTab))
            SaveActiveTabState();

        var wasActive = ReferenceEquals(tab, _activeTab);
        _tabs.RemoveAt(index);

        if (_tabs.Count == 0)
        {
            _activeTab = null;
            _session = null;
            _terminalBuffer = null;
            _cancellationTokenSource = null;
            _ = tab.DisposeAsync();
            Close();
            return;
        }

        if (wasActive)
        {
            _activeTab = _tabs[Math.Min(index, _tabs.Count - 1)];
            LoadTabState(_activeTab);
        }

        RebuildTabStrip();
        TerminalTextBox.Focus();
        _ = tab.DisposeAsync();
    }

    private void RebuildTabStrip()
    {
        TabStrip.Children.Clear();

        foreach (var tab in _tabs)
        {
            var isActive = ReferenceEquals(tab, _activeTab);
            var panel = new DockPanel
            {
                Width = 210,
                Height = 34
            };

            var closeButton = new Button
            {
                Width = 32,
                Content = "×",
                FontSize = 16,
                Foreground = Brushes.White,
                Background = Brushes.Transparent,
                BorderThickness = new Thickness(0),
                Tag = tab,
                Style = (Style)FindResource("DarkTitleButtonStyle")
            };
            closeButton.Click += TabCloseButton_Click;
            DockPanel.SetDock(closeButton, Dock.Right);
            panel.Children.Add(closeButton);

            var selectButton = new Button
            {
                Content = tab.Title,
                HorizontalContentAlignment = HorizontalAlignment.Left,
                Padding = new Thickness(18, 0, 6, 0),
                Foreground = Brushes.White,
                Background = isActive
                    ? new SolidColorBrush(Color.FromRgb(24, 24, 24))
                    : Brushes.Transparent,
                BorderThickness = new Thickness(0),
                Tag = tab,
                Style = (Style)FindResource("DarkTitleButtonStyle")
            };
            selectButton.Click += TabButton_Click;
            panel.Children.Add(selectButton);
            var tabBorder = new Border
            {
                Width = 210,
                Height = 34,
                Margin = new Thickness(4, 3, 0, 3),
                CornerRadius = new CornerRadius(9),
                Background = isActive
                    ? new SolidColorBrush(Color.FromRgb(24, 24, 24))
                    : Brushes.Transparent,
                ClipToBounds = true,
                Child = panel
            };
            TabStrip.Children.Add(tabBorder);
        }

        var addButton = new Button
        {
            Width = 38,
            Height = 32,
            Margin = new Thickness(5, 4, 0, 4),
            Content = "+",
            FontSize = 20,
            ToolTip = "New terminal (Ctrl+Shift+T)",
            Style = (Style)FindResource("DarkTitleButtonStyle")
        };
        addButton.Click += NewTabButton_Click;
        TabStrip.Children.Add(addButton);

        var profileButton = new Button
        {
            Width = 30,
            Height = 32,
            Margin = new Thickness(2, 4, 0, 4),
            Content = "⌄",
            FontSize = 13,
            ToolTip = "Terminal profiles",
            Style = (Style)FindResource("DarkTitleButtonStyle")
        };
        profileButton.Click += ProfileMenuButton_Click;
        TabStrip.Children.Add(profileButton);
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
        if (_session is null)
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
            if (!TerminalTextBox.Selection.IsEmpty)
                CopySelection();
            else
                _session.Write("\x03");

            e.Handled = true;
            return;
        }

        if (controlPressed && e.Key == Key.V)
        {
            PasteClipboard();
            e.Handled = true;
            return;
        }

        if (controlPressed && e.Key >= Key.A && e.Key <= Key.Z)
        {
            var controlCharacter = (char)(e.Key - Key.A + 1);
            _session.Write(controlCharacter.ToString());
            e.Handled = true;
            return;
        }

        if (!controlPressed &&
            shiftPressed &&
            GetEffectiveKey(e) == Key.OemQuestion &&
            InputLanguageManager.Current.CurrentInputLanguage
                .TwoLetterISOLanguageName == "fa")
        {
            _session.Write("\u061f");
            e.Handled = true;
            return;
        }

        string? sequence = e.Key switch
        {
            Key.Enter => "\r",
            Key.Space => " ",
            Key.Back => "\x7f",
            Key.Tab => "\t",
            Key.Up => "\x1b[A",
            Key.Down => "\x1b[B",
            Key.Right => "\x1b[C",
            Key.Left => "\x1b[D",
            Key.Home => "\x1b[H",
            Key.End => "\x1b[F",
            Key.Delete => "\x1b[3~",
            Key.PageUp => "\x1b[5~",
            Key.PageDown => "\x1b[6~",
            Key.Insert => "\x1b[2~",
            Key.Escape => "\x1b",
            _ => null
        };

        if (sequence is null)
            return;

        _session.Write(sequence);
        e.Handled = true;
    }

    private static Key GetEffectiveKey(KeyEventArgs e) =>
        e.Key switch
        {
            Key.System => e.SystemKey,
            Key.ImeProcessed => e.ImeProcessedKey,
            _ => e.Key
        };

    private void CopySelection()
    {
        if (TerminalTextBox.Selection.IsEmpty)
            return;

        var selectionEnd = TerminalTextBox.Selection.End;
        TerminalTextBox.Copy();
        TerminalTextBox.Selection.Select(
            selectionEnd,
            selectionEnd);
        TerminalTextBox.CaretPosition = selectionEnd;
    }

    private void PasteClipboard()
    {
        if (_session is null)
            return;

        if (Clipboard.ContainsFileDropList())
        {
            var fileDropList = Clipboard.GetFileDropList();
            var paths = new List<string>(fileDropList.Count);

            for (var index = 0; index < fileDropList.Count; index++)
            {
                if (!string.IsNullOrWhiteSpace(fileDropList[index]))
                    paths.Add(fileDropList[index]!);
            }

            WriteClipboardPaths(paths);
            return;
        }

        if (Clipboard.ContainsImage())
        {
            var imagePath = SaveClipboardImage();

            if (imagePath is not null)
                WriteClipboardPaths([imagePath]);

            return;
        }

        if (!Clipboard.ContainsText())
            return;

        var text = Clipboard.GetText()
            .Replace("\r\n", "\r")
            .Replace("\n", "\r");

        _session.Write(text);
    }

    private void WriteClipboardPaths(IReadOnlyList<string> paths)
    {
        if (_session is null || paths.Count == 0)
            return;

        var formattedPaths = new StringBuilder();

        foreach (var path in paths)
        {
            if (formattedPaths.Length > 0)
                formattedPaths.Append(' ');

            formattedPaths.Append(FormatClipboardPath(path));
        }

        _session.Write(formattedPaths.ToString());
    }

    private string FormatClipboardPath(string path)
    {
        return Path.GetFullPath(path);
    }

    private string? SaveClipboardImage()
    {
        var image = Clipboard.GetImage();

        if (image is null)
            return null;

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

    private void ReadOutputLoop(TerminalTab tab)
    {
        var session = tab.Session;
        var buffer = tab.Buffer;
        var cancellationTokenSource = tab.CancellationTokenSource;

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

            QueueRender(tab, buffer.Process(output));
        }
    }

    private void QueueRender(TerminalSnapshot snapshot)
    {
        if (_activeTab is not null)
            QueueRender(_activeTab, snapshot);
    }

    private void QueueRender(TerminalTab tab, TerminalSnapshot snapshot)
    {
        lock (_renderLock)
        {
            if (snapshot.Revision < tab.LatestQueuedRevision)
                return;

            tab.LatestQueuedRevision = snapshot.Revision;
            tab.PendingSnapshot = snapshot;

            if (!ReferenceEquals(tab, _activeTab))
                return;

            _latestQueuedRevision = tab.LatestQueuedRevision;
            _pendingSnapshot = tab.PendingSnapshot;

            if (_renderTimer.IsEnabled || _renderStartQueued)
                return;

            _renderStartQueued = true;
            tab.RenderStartQueued = true;
        }

        Dispatcher.BeginInvoke(
            DispatcherPriority.Background,
            StartRenderTimer);
    }

    private void StartRenderTimer()
    {
        lock (_renderLock)
        {
            _renderStartQueued = false;
            if (_activeTab is not null)
                _activeTab.RenderStartQueued = false;
        }

        if (!_renderTimer.IsEnabled)
            _renderTimer.Start();
    }

    private void RenderTimer_Tick(object? sender, EventArgs e)
    {
        TerminalSnapshot? snapshot;

        lock (_renderLock)
        {
            snapshot = _pendingSnapshot;
            _pendingSnapshot = null;

            if (_activeTab is not null)
                _activeTab.PendingSnapshot = null;
        }

        if (snapshot is not null)
            Render(snapshot);

        lock (_renderLock)
        {
            if (_pendingSnapshot is null)
                _renderTimer.Stop();
        }
    }

    private void Render(TerminalSnapshot snapshot)
    {
        _lastRenderedSnapshot = snapshot;
        TerminalTextBox.IsReadOnlyCaretVisible = snapshot.CursorVisible;
        var smartRtlEnabled = SmartRtlMenuItem.IsChecked;
        var scrollViewer = FindVisualChild<ScrollViewer>(TerminalTextBox);
        var verticalOffset = scrollViewer?.VerticalOffset ?? 0;
        var shouldFollowOutput = _followOutput;

        if (_terminalDocument is null)
        {
            _terminalDocument = new FlowDocument
            {
                PagePadding = new Thickness(0),
                ColumnWidth = double.PositiveInfinity,
                LineHeight = _lineHeight,
                FontFamily = TerminalTextBox.FontFamily,
                FontSize = TerminalTextBox.FontSize,
                FontWeight = TerminalTextBox.FontWeight,
                FontStyle = TerminalTextBox.FontStyle,
                Foreground = ToBrush(DefaultForeground),
                Background = ToBrush(DefaultBackground)
            };
            TerminalTextBox.Document = _terminalDocument;
            _renderedScrollbackCount = 0;
            _renderedScrollbackStartIndex = snapshot.ScrollbackStartIndex;
        }

        if (snapshot.ScrollbackStartIndex <
            _renderedScrollbackStartIndex)
        {
            _terminalDocument.Blocks.Clear();
            _renderedLines.Clear();
            _renderedScrollbackCount = 0;
            _renderedScrollbackStartIndex =
                snapshot.ScrollbackStartIndex;
        }

        var trimmedRowCount = (int)Math.Min(
            snapshot.ScrollbackStartIndex -
                _renderedScrollbackStartIndex,
            _renderedLines.Count);

        for (var row = 0; row < trimmedRowCount; row++)
            _terminalDocument.Blocks.Remove(_renderedLines[row].Paragraph);

        if (trimmedRowCount > 0)
        {
            _renderedLines.RemoveRange(0, trimmedRowCount);
            _renderedScrollbackCount = Math.Max(
                0,
                _renderedScrollbackCount - trimmedRowCount);
        }

        _renderedScrollbackStartIndex = snapshot.ScrollbackStartIndex;

        while (_renderedLines.Count > snapshot.Lines.Count)
        {
            var lastLine = _renderedLines[^1];
            _terminalDocument.Blocks.Remove(lastLine.Paragraph);
            _renderedLines.RemoveAt(_renderedLines.Count - 1);
        }

        var firstRowToRender =
            _renderedSmartRtlEnabled == smartRtlEnabled
                ? Math.Min(
                    Math.Min(
                        _renderedScrollbackCount,
                        snapshot.ScrollbackCount),
                    _renderedLines.Count)
                : 0;

        for (var row = firstRowToRender;
             row < snapshot.Lines.Count;
             row++)
        {
            var line = snapshot.Lines[row];
            var isRightToLeft =
                smartRtlEnabled && SmartRtl.IsRightToLeft(line);

            if (row < _renderedLines.Count &&
                ReferenceEquals(_renderedLines[row].Source, line) &&
                _renderedLines[row].IsRightToLeft == isRightToLeft)
            {
                continue;
            }

            var key = CreateLineKey(line, isRightToLeft);

            if (row < _renderedLines.Count &&
                _renderedLines[row].Key == key)
            {
                continue;
            }

            var paragraph = new Paragraph
            {
                Margin = new Thickness(0),
                Padding = new Thickness(0),
                LineHeight = _lineHeight,
                FlowDirection = isRightToLeft
                    ? FlowDirection.RightToLeft
                    : FlowDirection.LeftToRight,
                TextAlignment = isRightToLeft
                    ? TextAlignment.Right
                    : TextAlignment.Left
            };

            var runPositions = new List<RunPosition>();
            var renderSegments = CreateRenderSegments(line);
            var lineText = string.Concat(
                renderSegments.Select(segment => segment.Text));
            IReadOnlyList<DirectionalSpan> directionalSpans = isRightToLeft
                ? SmartRtl.GetDirectionalSpans(lineText, true)
                : string.IsNullOrEmpty(lineText)
                    ? []
                    : [new DirectionalSpan(0, lineText.Length, false)];

            foreach (var directionalSpan in directionalSpans)
            {
                var inlineSpan = new Span
                {
                    FlowDirection = directionalSpan.IsRightToLeft
                        ? FlowDirection.RightToLeft
                        : FlowDirection.LeftToRight
                };
                var directionalEnd =
                    directionalSpan.Start + directionalSpan.Length;

                foreach (var segment in renderSegments)
                {
                    var segmentEnd = segment.Start + segment.Text.Length;

                    if (segmentEnd <= directionalSpan.Start)
                        continue;

                    if (segment.Start >= directionalEnd)
                        break;

                    var overlapStart = Math.Max(
                        segment.Start,
                        directionalSpan.Start);
                    var overlapEnd = Math.Min(segmentEnd, directionalEnd);
                    var text = segment.Text.Substring(
                        overlapStart - segment.Start,
                        overlapEnd - overlapStart);
                    var run = CreateRun(text, segment.Style);

                    if (segment.Uri is not null)
                    {
                        var hyperlink = new Hyperlink(run)
                        {
                            Foreground = ToBrush(LinkForeground),
                            TextDecorations = TextDecorations.Underline,
                            Cursor = Cursors.Hand,
                            ToolTip = "Ctrl را نگه دارید و کلیک کنید"
                        };
                        hyperlink.Click += Link_Click;
                        hyperlink.Tag = segment.Uri;
                        inlineSpan.Inlines.Add(hyperlink);
                    }
                    else
                    {
                        inlineSpan.Inlines.Add(run);
                    }

                    runPositions.Add(
                        new RunPosition(run, overlapStart, text.Length));
                }

                paragraph.Inlines.Add(inlineSpan);
            }

            var renderedLine = new RenderedLine(
                paragraph,
                key,
                runPositions,
                line,
                isRightToLeft);

            if (row < _renderedLines.Count)
            {
                var oldParagraph = _renderedLines[row].Paragraph;
                _terminalDocument.Blocks.InsertBefore(oldParagraph, paragraph);
                _terminalDocument.Blocks.Remove(oldParagraph);
                _renderedLines[row] = renderedLine;
            }
            else
            {
                _terminalDocument.Blocks.Add(paragraph);
                _renderedLines.Add(renderedLine);
            }
        }

        _renderedScrollbackCount = snapshot.ScrollbackCount;
        _renderedSmartRtlEnabled = smartRtlEnabled;

        if (_renderedLines.Count == 0)
        {
            var paragraph = new Paragraph();
            _terminalDocument.Blocks.Add(paragraph);
            _renderedLines.Add(
                new RenderedLine(
                    paragraph,
                    string.Empty,
                    [],
                    null,
                    false));
        }

        if (shouldFollowOutput)
        {
            var cursorRow = Math.Clamp(
                snapshot.CursorRow,
                0,
                _renderedLines.Count - 1);
            var cursorLine = _renderedLines[cursorRow];
            TerminalTextBox.CaretPosition =
                FindCaretPosition(cursorLine, snapshot.CursorColumn);
        }

        ScheduleScrollRestore(
            _terminalDocument,
            shouldFollowOutput,
            verticalOffset);
    }

    private void ScheduleScrollRestore(
        FlowDocument document,
        bool followOutput,
        double verticalOffset)
    {
        var requestVersion = ++_scrollRequestVersion;

        Dispatcher.BeginInvoke(
            DispatcherPriority.Background,
            () =>
            {
                if (requestVersion != _scrollRequestVersion ||
                    !ReferenceEquals(TerminalTextBox.Document, document))
                {
                    return;
                }

                _restoringScrollPosition = true;

                try
                {
                    if (followOutput)
                    {
                        TerminalTextBox.ScrollToEnd();
                    }
                    else
                    {
                        FindVisualChild<ScrollViewer>(TerminalTextBox)?
                            .ScrollToVerticalOffset(verticalOffset);
                    }
                }
                finally
                {
                    _restoringScrollPosition = false;
                }
            });
    }

    private void ApplySavedFontSettings()
    {
        var settings = AppSettings.LoadFont();

        if (settings is { } savedSettings)
            ApplyFontSettings(savedSettings);
    }

    private void ApplyFontSettings(TerminalFontSettings settings)
    {
        TerminalTextBox.FontFamily = new FontFamily(settings.Family);
        TerminalTextBox.FontSize = settings.Size;
        TerminalTextBox.FontWeight = settings.Bold
            ? FontWeights.Bold
            : FontWeights.Normal;
        TerminalTextBox.FontStyle = settings.Italic
            ? FontStyles.Italic
            : FontStyles.Normal;
        UpdateFontMetrics();

        _terminalDocument = null;
        _renderedLines.Clear();

        if (_session is not null && _terminalBuffer is not null)
        {
            var columns = GetColumns();
            var rows = GetRows();
            _session.Resize(columns, rows);
            QueueRender(_terminalBuffer.Resize(columns, rows));
            return;
        }

        if (_lastRenderedSnapshot is not null)
            Render(_lastRenderedSnapshot);
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

        _cellWidth = Math.Max(4, measurement.WidthIncludingTrailingWhitespace);
        _lineHeight = Math.Max(
            TerminalTextBox.FontSize + 3,
            measurement.Height + 2);
    }

    private static string CreateLineKey(
        TerminalLine line,
        bool isRightToLeft)
    {
        var key = new StringBuilder(isRightToLeft ? "R|" : "L|");

        foreach (var run in line.Runs)
        {
            key.Append(run.Text)
                .Append('\u001f')
                .Append(run.Style.Foreground)
                .Append(run.Style.Background)
                .Append(run.Style.Bold)
                .Append(run.Style.Italic)
                .Append(run.Style.Dim)
                .Append('\u001e');
        }

        return key.ToString();
    }

    private static TextPointer FindCaretPosition(
        RenderedLine line,
        int cursorColumn)
    {
        var textOffset = GetTextOffsetForColumn(line, cursorColumn);
        TextPointer? lastRunEnd = null;

        foreach (var position in line.Runs)
        {
            if (textOffset >= position.Start + position.Length)
            {
                lastRunEnd = position.Run.ContentEnd;
                continue;
            }

            var offset = Math.Clamp(
                textOffset - position.Start,
                0,
                position.Length);
            var positionAtOffset =
                position.Run.ContentStart.GetPositionAtOffset(
                    offset,
                    LogicalDirection.Forward);
            return positionAtOffset?.GetInsertionPosition(
                    LogicalDirection.Forward) ??
                position.Run.ContentEnd.GetInsertionPosition(
                    LogicalDirection.Backward);
        }

        return (lastRunEnd ?? line.Paragraph.ContentEnd)
            .GetInsertionPosition(LogicalDirection.Backward);
    }

    private static int GetTextOffsetForColumn(
        RenderedLine line,
        int cursorColumn)
    {
        if (line.Source is null || cursorColumn <= 0)
            return 0;

        var column = 0;
        var textOffset = 0;

        foreach (var run in line.Source.Runs)
        {
            foreach (var rune in run.Text.EnumerateRunes())
            {
                var width = TerminalBuffer.GetCellWidth(rune);

                if (width > 0 && column >= cursorColumn)
                    return textOffset;

                column += width;
                textOffset += rune.Utf16SequenceLength;
            }
        }

        return textOffset;
    }

    private Run CreateRun(string text, TerminalStyle style)
    {
        return new Run(text)
        {
            Foreground = ToBrush(GetForeground(style)),
            Background = ToBrush(style.Background ?? DefaultBackground),
            FontWeight = style.Bold ||
                TerminalTextBox.FontWeight >= FontWeights.Bold
                    ? FontWeights.Bold
                    : FontWeights.Normal,
            FontStyle = style.Italic ||
                TerminalTextBox.FontStyle != FontStyles.Normal
                    ? FontStyles.Italic
                    : FontStyles.Normal
        };
    }

    private static IReadOnlyList<RenderSegment> CreateRenderSegments(
        TerminalLine line)
    {
        var segments = new List<RenderSegment>();
        var start = 0;

        foreach (var terminalRun in line.Runs)
        {
            foreach (var linkSegment in SplitLinks(terminalRun.Text))
            {
                segments.Add(new RenderSegment(
                    linkSegment.Text,
                    terminalRun.Style,
                    linkSegment.Uri,
                    start));
                start += linkSegment.Text.Length;
            }
        }

        return segments;
    }

    private static IEnumerable<LinkSegment> SplitLinks(string text)
    {
        var start = 0;

        foreach (Match match in LinkPattern.Matches(text))
        {
            if (match.Index > start)
                yield return new LinkSegment(text[start..match.Index], null);

            var linkText = match.Value.TrimEnd('.', ',', ';', ':', '!', '?', ')');
            var trailingText = match.Value[linkText.Length..];
            var uriText = linkText.StartsWith(
                "www.",
                StringComparison.OrdinalIgnoreCase)
                ? $"https://{linkText}"
                : linkText;

            if (Uri.TryCreate(uriText, UriKind.Absolute, out var uri) &&
                uri.Scheme is "http" or "https")
            {
                yield return new LinkSegment(linkText, uri);
            }
            else
            {
                yield return new LinkSegment(linkText, null);
            }

            if (trailingText.Length > 0)
                yield return new LinkSegment(trailingText, null);

            start = match.Index + match.Length;
        }

        if (start < text.Length)
            yield return new LinkSegment(text[start..], null);
    }

    private void Link_Click(object sender, RoutedEventArgs e)
    {
        e.Handled = true;

        if ((Keyboard.Modifiers & ModifierKeys.Control) == 0 ||
            sender is not Hyperlink { Tag: Uri uri })
        {
            return;
        }

        try
        {
            Process.Start(new ProcessStartInfo(uri.AbsoluteUri)
            {
                UseShellExecute = true
            });
        }
        catch (Exception exception)
        {
            MessageBox.Show(
                this,
                "بازکردن لینک انجام نشد." +
                Environment.NewLine +
                exception.Message,
                "Rtl Terminal",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
    }

    private static SolidColorBrush ToBrush(TerminalColor color)
    {
        if (BrushCache.TryGetValue(color, out var cachedBrush))
            return cachedBrush;

        var brush = new SolidColorBrush(
            Color.FromRgb(color.Red, color.Green, color.Blue));
        brush.Freeze();
        BrushCache[color] = brush;
        return brush;
    }

    private static TerminalColor GetForeground(TerminalStyle style)
    {
        var foreground = style.Foreground ?? DefaultForeground;

        if (!style.Dim)
            return foreground;

        var background = style.Background ?? DefaultBackground;
        return new TerminalColor(
            Blend(foreground.Red, background.Red),
            Blend(foreground.Green, background.Green),
            Blend(foreground.Blue, background.Blue));
    }

    private static byte Blend(byte foreground, byte background)
    {
        return (byte)((foreground * 0.55) + (background * 0.45));
    }

    private short GetColumns()
    {
        var scrollViewer = FindVisualChild<ScrollViewer>(TerminalTextBox);
        var viewportWidth = scrollViewer?.ViewportWidth ?? 0;
        var width = viewportWidth > 0
            ? viewportWidth
            : TerminalTextBox.ActualWidth;
        var horizontalPadding =
            TerminalTextBox.Padding.Left +
            TerminalTextBox.Padding.Right +
            6;
        var usableWidth = Math.Max(80, width - horizontalPadding);
        return (short)Math.Clamp(
            (int)Math.Floor(usableWidth / _cellWidth),
            10,
            300);
    }

    private short GetRows()
    {
        var height = Math.Max(120, TerminalTextBox.ActualHeight - 25);
        return (short)Math.Clamp((int)(height / _lineHeight), 10, 100);
    }

    private static T? FindVisualChild<T>(DependencyObject parent)
        where T : DependencyObject
    {
        for (var index = 0;
             index < VisualTreeHelper.GetChildrenCount(parent);
             index++)
        {
            var child = VisualTreeHelper.GetChild(parent, index);

            if (child is T match)
                return match;

            var descendant = FindVisualChild<T>(child);

            if (descendant is not null)
                return descendant;
        }

        return null;
    }

    private readonly record struct LinkSegment(string Text, Uri? Uri);

    private sealed record RenderedLine(
        Paragraph Paragraph,
        string Key,
        IReadOnlyList<RunPosition> Runs,
        TerminalLine? Source,
        bool IsRightToLeft);

    private readonly record struct RunPosition(
        Run Run,
        int Start,
        int Length);

    private readonly record struct RenderSegment(
        string Text,
        TerminalStyle Style,
        Uri? Uri,
        int Start);

    private enum TerminalProfile
    {
        CommandPrompt,
        PowerShell,
        Wsl
    }

    private sealed class TerminalTab(
        int number,
        TerminalProfile profile,
        string profileTitle)
    {
        private int _disposeStarted;

        public int Number { get; } = number;
        public TerminalProfile Profile { get; } = profile;
        public string Title { get; } = $"{profileTitle} {number}";
        public ConPtySession? Session { get; set; }
        public TerminalBuffer? Buffer { get; set; }
        public CancellationTokenSource? CancellationTokenSource { get; set; }
        public TerminalSnapshot? PendingSnapshot { get; set; }
        public bool RenderStartQueued { get; set; }
        public long LatestQueuedRevision { get; set; }
        public TerminalSnapshot? LastRenderedSnapshot { get; set; }
        public FlowDocument? Document { get; set; }
        public int RenderedScrollbackCount { get; set; }
        public long RenderedScrollbackStartIndex { get; set; }
        public bool RenderedSmartRtlEnabled { get; set; } = true;
        public List<RenderedLine> RenderedLines { get; } = [];
        public bool FollowOutput { get; set; } = true;
        public double VerticalOffset { get; set; }

        public Task DisposeAsync()
        {
            if (Interlocked.Exchange(ref _disposeStarted, 1) != 0)
                return Task.CompletedTask;

            var session = Session;
            var cancellationTokenSource = CancellationTokenSource;
            CancellationTokenSource = null;
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
