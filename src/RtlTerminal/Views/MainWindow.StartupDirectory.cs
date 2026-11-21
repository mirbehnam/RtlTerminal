using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;

namespace RtlTerminal;

public partial class MainWindow
{
    private bool _initialStartupTasksQueued;
    private bool _checkingStartupDirectory;
    private TerminalPane? _startupHomePane;

    private async Task OpenInitialDirectoryAsync()
    {
        if (_tabs.Count != 0)
        {
            QueueInitialStartupTasks();
            return;
        }

        var args = Environment.GetCommandLineArgs();
        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        var explicitPath = StartupDirectories.ResolveLaunchDirectory(args.Length > 1 ? args[1] : null,
            Environment.CurrentDirectory, AppContext.BaseDirectory, home);
        var mode = App.PerformanceProbe ? StartupDirectoryMode.Default : AppSettings.LoadStartupDirectoryMode();
        if (explicitPath is null && mode == StartupDirectoryMode.ChooseDirectory)
        {
            StartAtDirectory(null);
            ShowStartupDirectoryPicker(home);
            return;
        }

        DirectoryHistoryEntry? lastDirectory = null;
        if (explicitPath is null && mode == StartupDirectoryMode.LastDirectory)
        {
            var recent = RecentStartupDirectories();
            try
            {
                lastDirectory = await Task.Run(() => recent.FirstOrDefault(entry => Directory.Exists(entry.Path)))
                    .WaitAsync(TimeSpan.FromSeconds(2));
            }
            catch (TimeoutException) { }
        }

        if (_windowClosed) return;
        if (_tabs.Count == 0)
            StartAtDirectory(lastDirectory?.Path ?? explicitPath, lastDirectory);
        QueueInitialStartupTasks();
    }

    private IReadOnlyList<DirectoryHistoryEntry> RecentStartupDirectories() =>
        _directoryHistory.Enabled ? _directoryHistory.Snapshot() : [];

    private void ShowStartupDirectoryPicker(string home)
    {
        _startupHomePane = ActivePane;
        var normalizedHome = DirectoryHistoryStore.Normalize(home);
        StartupRecentDirectories.ItemsSource = RecentStartupDirectories()
            .Where(entry => !string.Equals(entry.Path, normalizedHome, StringComparison.OrdinalIgnoreCase))
            .Take(DirectoryHistoryStore.MenuLimit)
            .Select(entry => new StartupDirectoryChoice(entry)).ToArray();
        StartupDirectoryPicker.Visibility = Visibility.Visible;
        _ = Dispatcher.BeginInvoke(DispatcherPriority.Input, new Action(() =>
        {
            if (StartupDirectoryPicker.IsVisible) StartupStayButton.Focus();
        }));
    }

    private void StartAtDirectory(string? path, DirectoryHistoryEntry? entry = null, bool fromPicker = false)
    {
        var profile = entry is null ? _defaultProfile : GetHistoryProfile(entry);
        if ((profile == TerminalProfile.Wsl && !IsWslAvailable()) ||
            (profile == TerminalProfile.PowerShell7 && !IsPowerShell7Available()) ||
            (profile == TerminalProfile.GitBash && !IsGitBashAvailable()))
            profile = _defaultProfile;
        var distribution = profile == TerminalProfile.Wsl ? entry?.Distribution : null;
        if (fromPicker && path is not null && _activeTab is { Panes.Count: 1 } tab &&
            _startupHomePane is { HasUserInput: false } homePane && ReferenceEquals(tab.ActivePane, homePane))
        {
            DismissStartupDirectoryPicker(restoreFocus: false);
            ReplaceDirectoryPane(tab, homePane, profile, path, distribution, backgroundStart: true);
            return;
        }
        CreateTerminalTabCore(profile, path, distribution, backgroundStart: true);
    }

    private void QueueInitialStartupTasks()
    {
        if (_initialStartupTasksQueued) return;
        _initialStartupTasksQueued = true;
        _ = Dispatcher.BeginInvoke(DispatcherPriority.Background, new Action(() =>
        {
            if (_windowClosed) return;
            RefreshContextMenuIntegrationState();
            if (App.PerformanceProbe) return;
            PromptForContextMenuIntegration();
            _ = CheckForUpdatesOnStartupAsync();
        }));
    }

    private void DismissStartupDirectoryPicker(bool restoreFocus = true)
    {
        if (StartupDirectoryPicker.Visibility != Visibility.Visible) return;
        StartupDirectoryPicker.Visibility = Visibility.Collapsed;
        _startupHomePane = null;
        if (restoreFocus) ActiveView.Focus();
        QueueInitialStartupTasks();
    }

    private async void StartupRecentDirectory_Click(object sender, RoutedEventArgs e)
    {
        if (_checkingStartupDirectory || sender is not Button { Tag: StartupDirectoryChoice choice }) return;
        _checkingStartupDirectory = true;
        try
        {
            StartupDirectoryStatus.Visibility = Visibility.Collapsed;
            var exists = false;
            try { exists = await Task.Run(() => Directory.Exists(choice.Path)).WaitAsync(TimeSpan.FromSeconds(2)); }
            catch (TimeoutException) { }
            if (_windowClosed || StartupDirectoryPicker.Visibility != Visibility.Visible) return;
            if (exists) StartAtDirectory(choice.Path, choice.Entry, fromPicker: true);
            else
            {
                StartupDirectoryStatus.Text = UiText.Get("This directory is currently unavailable.");
                StartupDirectoryStatus.Visibility = Visibility.Visible;
            }
        }
        finally { _checkingStartupDirectory = false; }
    }

    private void StartupHomeDirectory_Click(object sender, RoutedEventArgs e) => DismissStartupDirectoryPicker();

    private void TerminalArea_PreviewMouseDown(object sender, MouseButtonEventArgs e)
    {
        if (StartupDirectoryPicker.Visibility == Visibility.Visible && !StartupDirectoryPicker.IsMouseOver)
            DismissStartupDirectoryPicker();
    }

    private void StartupDirectoryPicker_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Escape) return;
        e.Handled = true;
        DismissStartupDirectoryPicker();
    }

    private sealed record StartupDirectoryChoice(DirectoryHistoryEntry Entry)
    {
        public string Path => Entry.Path;
        public string Name => System.IO.Path.GetFileName(Path) is { Length: > 0 } name ? name : Path;
    }
}
