using System.Collections.ObjectModel;
using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Microsoft.Win32;

namespace RtlTerminal.CliLab;

internal sealed class CliTestLabWindow : Window
{
    private readonly ObservableCollection<CliLabInstallation> installations = [];
    private readonly ListBox list = new() { DisplayMemberPath = "Label", MaxHeight = 145, MinHeight = 70 };
    private readonly ComboBox kind = new() { ItemsSource = Enum.GetValues<CliLabTool>(), SelectedIndex = 0, MinWidth = 95 };
    private readonly TextBox response = new() { AcceptsReturn = true, AcceptsTab = true, TextWrapping = TextWrapping.Wrap, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, MinHeight = 140, FontFamily = new FontFamily("Consolas"), FontSize = 14, Padding = new Thickness(10), Text = LocalCliServer.DefaultResponse, MaxLength = 65536 };
    private readonly TextBlock status = new() { TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 8, 0, 0) };
    private readonly TextBlock detail = new() { TextWrapping = TextWrapping.Wrap, FontSize = 12, Opacity = .7, Margin = new Thickness(0, 5, 0, 10) };
    private readonly Button launch = new() { Content = "Open Local Test Tab", Padding = new Thickness(16, 8, 16, 8), IsEnabled = false };
    private readonly Button refresh = new() { Content = "Refresh", Padding = new Thickness(12, 5, 12, 5) };
    private readonly Button add = new() { Content = "Add executable…", Padding = new Thickness(12, 5, 12, 5) };
    private CancellationTokenSource? detection;
    private bool closed, launching;
    private readonly Func<CliLabInstallation, string, Task> open;
    private static string InventoryFile => Path.Combine(CliLabSession.StorageRoot, "executables.json");

    public CliTestLabWindow(Func<CliLabInstallation, string, Task> open)
    {
        this.open = open;
        Title = "CLI Test Lab · Preview builds only"; Width = 800; Height = 670; MinWidth = 600; MinHeight = 500;
        WindowStartupLocation = WindowStartupLocation.CenterOwner; ShowInTaskbar = false;
        FontFamily = new FontFamily("Segoe UI"); FontSize = 13; UseLayoutRounding = true;
        Resources.MergedDictionaries.Add(new ResourceDictionary { Source = new Uri("/RtlTerminal;component/Views/ProxyManagerStyles.xaml", UriKind.Relative) });
        Resources.MergedDictionaries.Add(new ResourceDictionary { Source = new Uri("/RtlTerminal;component/Controls/TerminalScrollBar.xaml", UriKind.Relative) });
        list.SetResourceReference(StyleProperty, "ProxyHistoryList");
        list.ItemTemplate = null;
        launch.SetResourceReference(Control.BackgroundProperty, "AccentBrush");
        launch.SetResourceReference(Control.ForegroundProperty, "TerminalBackgroundBrush");
        SetResourceReference(BackgroundProperty, "MenuBackgroundBrush"); SetResourceReference(ForegroundProperty, "ChromeForegroundBrush");
        WindowCaptionTheme.Attach(this);
        var root = new Grid { Margin = new Thickness(20) }; root.SetResourceReference(Panel.BackgroundProperty, "MenuBackgroundBrush"); Content = root;
        foreach (var height in new[] { GridLength.Auto, GridLength.Auto, GridLength.Auto, new GridLength(1, GridUnitType.Star), GridLength.Auto }) root.RowDefinitions.Add(new RowDefinition { Height = height });
        var heading = new StackPanel(); root.Children.Add(heading);
        heading.Children.Add(new TextBlock { Text = "CLI Test Lab", FontSize = 23, FontWeight = FontWeights.SemiBold });
        heading.Children.Add(new TextBlock { Text = "Real CLI interfaces · Local sample responses · No model subscription", Opacity = .7, Margin = new Thickness(0, 4, 0, 15) });
        var bar = new DockPanel { Margin = new Thickness(0, 0, 0, 8) }; Grid.SetRow(bar, 1); root.Children.Add(bar);
        var label = new TextBlock { Text = "Installed tools and versions", FontWeight = FontWeights.SemiBold, VerticalAlignment = VerticalAlignment.Center }; bar.Children.Add(label);
        var controls = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
        foreach (var control in new Control[] { kind, add, refresh }) { control.Margin = new Thickness(7, 0, 0, 0); controls.Children.Add(control); }
        bar.Children.Add(controls);
        var selection = new StackPanel(); Grid.SetRow(selection, 2); root.Children.Add(selection);
        list.ItemsSource = installations; selection.Children.Add(list); selection.Children.Add(detail);
        var editor = new Grid(); editor.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); editor.RowDefinitions.Add(new RowDefinition()); Grid.SetRow(editor, 3); root.Children.Add(editor);
        var presets = new DockPanel { Margin = new Thickness(0, 0, 0, 7) }; editor.Children.Add(presets);
        presets.Children.Add(new TextBlock { Text = "Sample response", FontWeight = FontWeights.SemiBold, VerticalAlignment = VerticalAlignment.Center });
        var buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right }; presets.Children.Add(buttons);
        foreach (var (name, text) in new[] { ("English", "Hello from the local test server\nCheck src/app.cs and HTTP 200\nLOCAL_UI_TEST_OK"), ("فارسی", "سلام دنیا\nاین یک پاسخ آزمایشی فارسی است\nLOCAL_UI_TEST_OK"), ("Mixed + Code", LocalCliServer.DefaultResponse) })
        { var button = new Button { Content = name, Padding = new Thickness(9, 4, 9, 4), Margin = new Thickness(5, 0, 0, 0) }; button.Click += (_, _) => response.Text = text; buttons.Children.Add(button); }
        Grid.SetRow(response, 1); editor.Children.Add(response);
        response.SetResourceReference(Control.BackgroundProperty, "ToolbarBrush"); response.SetResourceReference(Control.ForegroundProperty, "ChromeForegroundBrush");
        var footer = new StackPanel { Margin = new Thickness(0, 12, 0, 0) }; Grid.SetRow(footer, 4); root.Children.Add(footer);
        footer.Children.Add(new TextBlock { Text = "Each test uses a new isolated home and workspace. Existing accounts and config are not loaded.\nResponses are fixed fixtures, not AI. CLI test history stays in local test data; do not enter secrets.", TextWrapping = TextWrapping.Wrap, FontSize = 12, Opacity = .7 });
        footer.Children.Add(status);
        var actions = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 10, 0, 0) }; footer.Children.Add(actions);
        var close = new Button { Content = "Close", Padding = new Thickness(16, 8, 16, 8), Margin = new Thickness(0, 0, 8, 0), IsCancel = true }; actions.Children.Add(close); actions.Children.Add(launch);
        close.Click += (_, _) => Close();
        list.SelectionChanged += (_, _) => { var item = list.SelectedItem as CliLabInstallation; detail.Text = item == null ? "Select a tool, or add a native executable for another version." : item.Path + (item.UnavailableReason == null ? "" : "\n" + item.UnavailableReason); UpdateLaunch(); };
        response.TextChanged += (_, _) => UpdateLaunch();
        refresh.Click += async (_, _) => await DetectAsync();
        add.Click += async (_, _) => await AddAsync();
        launch.Click += async (_, _) => await LaunchAsync();
        Loaded += async (_, _) => await DetectAsync();
        Closing += (_, e) => { if (launching) { e.Cancel = true; return; } closed = true; detection?.Cancel(); };
        Closed += (_, _) => { detection?.Cancel(); detection?.Dispose(); };
        System.Windows.Automation.AutomationProperties.SetName(response, "Editable local response fixture");
        System.Windows.Automation.AutomationProperties.SetName(list, "Detected CLI installations and versions");
    }
    private void UpdateLaunch() => launch.IsEnabled = !launching && list.SelectedItem is CliLabInstallation { Supported: true } && !string.IsNullOrWhiteSpace(response.Text);
    private async Task DetectAsync()
    {
        detection?.Cancel(); detection?.Dispose(); var current = detection = new();
        refresh.IsEnabled = false; status.Text = "Detecting installed versions locally…";
        var selected = (list.SelectedItem as CliLabInstallation)?.Path; installations.Clear();
        try
        {
            var paths = await Task.Run(() => CliLabCatalog.Candidates().Concat(ReadRegistered()).DistinctBy(item => item.Path, StringComparer.OrdinalIgnoreCase).ToArray(), current.Token);
            foreach (var (tool, path) in paths)
            {
                var item = await CliLabCatalog.InspectAsync(tool, path, current.Token);
                if (closed || current != detection) return;
                installations.Add(item); if (list.SelectedItem == null || path == selected) list.SelectedItem = item;
            }
            status.Text = installations.Count == 0 ? "No installed CLI was found. Add an executable to test a specific version." : $"{installations.Count} installations found. Select a version to test.";
        }
        catch (OperationCanceledException) { }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or JsonException) { if (!closed) status.Text = "Detection could not finish: " + e.Message; }
        finally { if (!closed && current == detection) { refresh.IsEnabled = true; UpdateLaunch(); } }
    }
    private static IEnumerable<(CliLabTool Tool, string Path)> ReadRegistered()
    {
        try { return (JsonSerializer.Deserialize<CliLabInstallation[]>(File.ReadAllText(InventoryFile)) ?? []).Where(item => File.Exists(item.Path)).Select(item => (item.Tool, item.Path)).ToArray(); }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or JsonException) { return []; }
    }
    private async Task AddAsync()
    {
        var picker = new OpenFileDialog { Title = "Choose a native CLI executable", Filter = "CLI executable (*.exe)|*.exe", CheckFileExists = true };
        if (picker.ShowDialog(this) != true) return;
        add.IsEnabled = false;
        try
        {
            var item = await CliLabCatalog.InspectAsync((CliLabTool)kind.SelectedItem, picker.FileName, detection?.Token ?? default);
            if (closed) return;
            var existing = installations.FirstOrDefault(i => i.Path.Equals(item.Path, StringComparison.OrdinalIgnoreCase)); if (existing != null) installations.Remove(existing);
            installations.Add(item); list.SelectedItem = item;
            System.IO.Directory.CreateDirectory(CliLabSession.StorageRoot);
            var temp = InventoryFile + ".tmp"; await File.WriteAllTextAsync(temp, JsonSerializer.Serialize(installations)); File.Move(temp, InventoryFile, true);
        }
        catch (OperationCanceledException) { }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException) { if (!closed) status.Text = "Could not add this executable: " + e.Message; }
        finally { if (!closed) add.IsEnabled = true; }
    }
    private async Task LaunchAsync()
    {
        if (launching || list.SelectedItem is not CliLabInstallation { Supported: true } item) return;
        launching = true; UpdateLaunch(); refresh.IsEnabled = add.IsEnabled = false; status.Text = "Starting the local fixture server and isolated terminal…";
        try { await open(item, response.Text); launching = false; DialogResult = true; }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException or System.ComponentModel.Win32Exception or System.Net.Sockets.SocketException)
        { status.Text = "The local test could not start: " + e.Message; }
        finally { launching = false; if (!closed) { refresh.IsEnabled = add.IsEnabled = true; UpdateLaunch(); } }
    }
}
