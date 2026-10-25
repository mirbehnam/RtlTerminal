using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;

namespace RtlTerminal;

public partial class MainWindow
{
    private readonly DirectoryHistoryStore _directoryHistory;
    private static string StartupSocialLine(bool instagram) => instagram ? "Instagram: @BehnamTjd" : "YouTube: @aka_techno";
    private readonly DispatcherTimer _directoryPollTimer = new() { Interval = TimeSpan.FromSeconds(3) };

    private void InitializeDirectoryHistory()
    {
        _directoryPollTimer.Tick += async (_, _) =>
        {
            var panes = _directoryHistory.Enabled ? _tabs.SelectMany(t => t.Panes).ToArray() :
                ExplorerTargetPane() is { } active ? [active] : Array.Empty<TerminalPane>();
            await Task.WhenAll(panes.Select(PollDirectoryAsync));
        };
        _directoryPollTimer.Start();
    }

    private void ObserveDirectory(TerminalPane pane, string? path)
    {
        SetPaneDirectory(pane, path);
        if (!_directoryHistory.Enabled || DirectoryHistoryStore.Normalize(path) is not { } normalized) return;
        lock (pane.DirectoryHistoryGate)
        {
            var comparison = pane.Profile == TerminalProfile.Wsl ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase;
            if (string.Equals(pane.LastHistoryDirectory, normalized, comparison)) return;
            try
            {
                if (_directoryHistory.Visit(normalized, pane.Profile.ToString(), pane.Profile == TerminalProfile.Wsl ? pane.WslDistribution : null))
                    pane.LastHistoryDirectory = normalized;
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Security.SecurityException) { }
        }
    }

    private async Task PollDirectoryAsync(TerminalPane pane)
    {
        if (pane.DirectoryPollPending || pane.Session is not { } session || pane.SessionEnded) return;
        pane.DirectoryPollPending = true;
        try
        {
            var path = await Task.Run(async () => await ReadExplorerDirectoryAsync(pane));
            if (pane.Session == session && !pane.SessionEnded) ObserveDirectory(pane, path);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ObjectDisposedException or System.ComponentModel.Win32Exception) { }
        finally { pane.DirectoryPollPending = false; }
    }

    private void OpenHistoryDirectory(DirectoryHistoryEntry entry)
    {
        // Use the recorded shell and distribution. Do not substitute the active pane's profile.
        try
        {
            if (!Directory.Exists(entry.Path)) { UiText.Show(this, "This directory is currently unavailable.", "Directory history"); return; }
            var profile = entry.Profile switch
            {
                "Wsl" => TerminalProfile.Wsl,
                "PowerShell7" or "PowerShellCore" => TerminalProfile.PowerShell7,
                "GitBash" => TerminalProfile.GitBash,
                "PowerShell" => TerminalProfile.PowerShell,
                _ => TerminalProfile.CommandPrompt
            };
            CreateTerminalTab(profile, entry.Path, entry.Distribution);
        }
        catch (Exception ex) { UiText.Show(this, ex.Message, "Directory history"); }
    }

    private void ShowDirectoryHistory()
    {
        var window = new DirectoryHistoryWindow(_directoryHistory) { Owner = this };
        if (window.ShowDialog() == true && window.SelectedDirectory is { } entry) OpenHistoryDirectory(entry);
    }
}
