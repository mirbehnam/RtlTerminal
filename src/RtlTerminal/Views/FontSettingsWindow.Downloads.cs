using System.IO;
using System.Net.Http;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace RtlTerminal;

public partial class FontSettingsWindow
{
    private readonly Dictionary<string, CancellationTokenSource> _fontDownloads = [];
    private readonly Dictionary<string, int> _fontDownloadProgress = [];
    private bool _fontDownloadsClosed;
    private static bool NeedsDownload(string name) => OptionalFonts.Find(name) is { } package && !OptionalFonts.IsInstalled(package);
    private void FontDownloadRow_Loaded(object sender, RoutedEventArgs e) => UpdateDownloadRow((DockPanel)sender);
    private void UpdateDownloadRow(DockPanel row)
    {
        if (row.DataContext is not string name) return;
        var button = (Button)row.Children[0]; var label = (TextBlock)row.Children[1];
        var package = OptionalFonts.Find(name); var missing = package != null && !OptionalFonts.IsInstalled(package);
        label.Opacity = missing ? .48 : 1;
        button.Visibility = missing ? Visibility.Visible : Visibility.Collapsed;
        if (package == null) return;
        var downloading = _fontDownloads.ContainsKey(package.Id);
        button.Content = downloading ? UiText.Get("Cancel") + " " + (_fontDownloadProgress.GetValueOrDefault(package.Id) is var value && value >= 0 ? value + "%" : "…") : UiText.Get("Download");
        button.ToolTip = UiText.Get("Only required font files are downloaded from GitHub.") + " · " + package.Version;
        System.Windows.Automation.AutomationProperties.SetName(button, (downloading ? UiText.Get("Cancel") : UiText.Get("Download")) + " " + name);
    }
    private void RefreshDownloadRows()
    {
        void Visit(DependencyObject element)
        {
            if (element is DockPanel { DataContext: string } row && row.Children.Count == 2 && row.Children[0] is Button && row.Children[1] is TextBlock) UpdateDownloadRow(row);
            for (var i = 0; i < VisualTreeHelper.GetChildrenCount(element); i++) Visit(VisualTreeHelper.GetChild(element, i));
        }
        Visit(StandardFontListBox); Visit(FontListBox);
    }
    private async void DownloadFont_Click(object sender, RoutedEventArgs e)
    {
        e.Handled = true;
        if (sender is not Button { DataContext: string name } || OptionalFonts.Find(name) is not { } package) return;
        if (_fontDownloads.TryGetValue(package.Id, out var running)) { running.Cancel(); return; }
        using var cancellation = new CancellationTokenSource();
        _fontDownloads[package.Id] = cancellation; _fontDownloadProgress[package.Id] = 0; RefreshDownloadRows();
        try
        {
            var progress = new Progress<int>(value =>
            {
                if (_fontDownloadsClosed || !_fontDownloads.ContainsKey(package.Id)) return;
                _fontDownloadProgress[package.Id] = value; RefreshDownloadRows();
            });
            await Task.Run(() => OptionalFonts.InstallAsync(package, progress, cancellation.Token));
            if (_fontDownloadsClosed) return;
            RefreshDownloadRows();
            FontListBox.SelectedItem = package.Name;
            StandardFontListBox.SelectedItem = package.Name;
            UpdatePreview();
        }
        catch (OperationCanceledException)
        {
            if (!_fontDownloadsClosed && !cancellation.IsCancellationRequested) UiText.Show(this, "The font download timed out. Check your connection and try again.", "Font download", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
        catch (Exception ex) when (ex is HttpRequestException or IOException or InvalidDataException or UnauthorizedAccessException or System.Security.Cryptography.CryptographicException)
        {
            if (!_fontDownloadsClosed) UiText.Show(this, ex.Message == "This server or proxy does not support partial font downloads. No full archive was downloaded." ? ex.Message : ex is InvalidDataException ? "Font verification failed. Nothing was installed. Please try again."
                : ex is UnauthorizedAccessException ? "The font folder is not writable. Check its permissions and try again."
                : "The font could not be downloaded or installed. Check your connection, available disk space, or another running download, and try again.", "Font download", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
        finally { _fontDownloads.Remove(package.Id); _fontDownloadProgress.Remove(package.Id); if (!_fontDownloadsClosed) RefreshDownloadRows(); }
    }
}
