using RtlTerminal.Proxy;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Shapes;

namespace RtlTerminal;

public sealed partial class ProxyManagerWindow
{
    private sealed record StatusTile(Button Button, Ellipse Light, TextBlock Status, TextBlock Address);
    private readonly List<StatusTile> statusTiles = [];
    private readonly ProxyStatusSnapshot?[] statusSnapshots = new ProxyStatusSnapshot?[3];
    private readonly Button refreshStatus = new();
    private readonly ProgressBar statusLoading = new() { IsIndeterminate = true, Width = 30, Height = 3, Visibility = Visibility.Collapsed, Margin = new Thickness(8, 0, 8, 0) };
    private bool statusRefreshing;
    private bool statusRefreshAgain;
    private readonly List<(DateTime Time, string Message)> notifications = [];
    private Button? notificationButton;

    private FrameworkElement CreateStatusDashboard()
    {
        var panel = new StackPanel { Margin = new Thickness(0, 0, 0, 10) };
        var heading = new DockPanel { Margin = new Thickness(0, 0, 0, 6) };
        var help = new Button { Content = "Help", ToolTip = "Proxy Manager Guide", Padding = new Thickness(8, 2, 8, 2), Margin = new Thickness(6, 0, 0, 0) };
        System.Windows.Automation.AutomationProperties.SetName(help, "Proxy Manager Guide");
        help.Click += (_, _) => new ProxyHelpWindow { Owner = this }.ShowDialog(); DockPanel.SetDock(help, Dock.Right); heading.Children.Add(help);
        refreshStatus.Content = "↻"; refreshStatus.Padding = new Thickness(7, 1, 7, 1); refreshStatus.ToolTip = "Refresh proxy status";
        System.Windows.Automation.AutomationProperties.SetName(refreshStatus, "Refresh proxy status");
        DockPanel.SetDock(refreshStatus, Dock.Right); heading.Children.Add(refreshStatus); refreshStatus.Click += async (_, _) => await RefreshStatus();
        DockPanel.SetDock(statusLoading, Dock.Right); heading.Children.Add(statusLoading);
        heading.Children.Add(new TextBlock { Text = "Proxy Status", FontWeight = FontWeights.SemiBold, VerticalAlignment = VerticalAlignment.Center }); panel.Children.Add(heading);
        var grid = new System.Windows.Controls.Primitives.UniformGrid { Columns = 3 }; panel.Children.Add(grid);
        foreach (var name in new[] { "Environment", "Windows System", "WinHTTP" })
        {
            var stack = new StackPanel(); var row = new DockPanel();
            var light = new Ellipse { Width = 7, Height = 7, Fill = Brushes.Gray, Margin = new Thickness(0, 0, 6, 0), VerticalAlignment = VerticalAlignment.Center }; row.Children.Add(light);
            row.Children.Add(new TextBlock { Text = name, FontSize = 12, FontWeight = FontWeights.SemiBold, TextTrimming = TextTrimming.CharacterEllipsis }); stack.Children.Add(row);
            var status = new TextBlock { Text = "Reading…", FontSize = 11, Margin = new Thickness(13, 3, 0, 0), TextTrimming = TextTrimming.CharacterEllipsis }; stack.Children.Add(status);
            var address = new TextBlock { FontSize = 11, Opacity = .75, Margin = new Thickness(13, 2, 0, 0), TextTrimming = TextTrimming.CharacterEllipsis, Tag = "ProxyLiteral" }; stack.Children.Add(address);
            var button = new Button { Content = stack, Padding = new Thickness(10, 7, 10, 7), Margin = new Thickness(0, 0, name == "WinHTTP" ? 0 : 6, 0), HorizontalContentAlignment = HorizontalAlignment.Stretch, MinHeight = 67 };
            var index = statusTiles.Count; button.Click += (_, _) => ShowStatusDetails(index);
            System.Windows.Automation.AutomationProperties.SetName(button, name); statusTiles.Add(new(button, light, status, address)); grid.Children.Add(button);
        }
        return panel;
    }
    private async Task RefreshStatus()
    {
        if (closed) return;
        if (statusRefreshing) { statusRefreshAgain = true; return; }
        using var timing = StartupPerformance.Measure("ProxyManager.RefreshStatus");
        statusRefreshing = true; refreshStatus.IsEnabled = false; statusLoading.Visibility = Visibility.Visible;
        var reader = new ProxyStatusReader(systems[0], machineEnvironment, new EnvironmentScopeInspectionProvider(EnvironmentVariableTarget.Process), systems[1],
            systems[2] is WinHttpProxyProvider native ? native.ReadStatusAsync : async ct => (await systems[2].ReadAsync(ct), "User"));
        try
        {
            await reader.RefreshAsync(snapshot => Dispatcher.Invoke(() =>
            {
                if (closed || lifetime.IsCancellationRequested) return;
                statusSnapshots[snapshot.Index] = snapshot; var tile = statusTiles[snapshot.Index];
                tile.Status.Text = snapshot.Status switch { ProxyConfigurationStatus.Enabled => "Enabled", ProxyConfigurationStatus.NotConfigured => "Not configured", ProxyConfigurationStatus.Partial => "Partially configured", _ => "Error" };
                tile.Light.Fill = new SolidColorBrush(snapshot.Status switch { ProxyConfigurationStatus.Enabled => Color.FromRgb(45, 173, 108), ProxyConfigurationStatus.Partial => Color.FromRgb(216, 144, 40), ProxyConfigurationStatus.Error => Color.FromRgb(225, 77, 91), _ => Color.FromRgb(135, 141, 149) });
                tile.Address.Text = snapshot.Address; tile.Address.ToolTip = snapshot.Address;
                tile.Button.ToolTip = UiText.Get("Configured settings only; connectivity is not tested.");
                tile.Button.BeginAnimation(OpacityProperty, new DoubleAnimation(.65, 1, TimeSpan.FromMilliseconds(120)));
            }), lifetime.Token);
        }
        catch (OperationCanceledException) when (lifetime.IsCancellationRequested) { }
        catch (Exception ex) { if (!closed) result.Text = ProxyService.SafeError(ex); }
        finally { if (!closed) { statusRefreshing = false; refreshStatus.IsEnabled = true; statusLoading.Visibility = Visibility.Collapsed; } }
        if (statusRefreshAgain && !closed) { statusRefreshAgain = false; await RefreshStatus(); }
    }
    private void ShowStatusDetails(int index)
    {
        if (statusSnapshots[index] is not { } snapshot) return;
        var names = new[] { "Environment", "Windows System", "WinHTTP" };
        new ProxyInfoWindow(names[index], snapshot.Groups, index == 0
            ? "Process values are inherited snapshots. Persistent User and Machine settings are read separately."
            : "Configured settings only; connectivity is not tested.") { Owner = this }.ShowDialog();
    }
    private Button CreateNotificationButton()
    {
        var button = new Button { Content = "ⓘ  Notifications", Padding = new Thickness(9, 5, 9, 5), VerticalAlignment = VerticalAlignment.Center, HorizontalAlignment = HorizontalAlignment.Left, Visibility = Visibility.Collapsed };
        button.Click += (_, _) => new ProxyInfoWindow("Notifications", notifications.AsEnumerable().Reverse().Select(n => new ProxyStatusGroup(n.Time.ToString("g"), [KeyValuePair.Create(UiText.Get("Message"), ProxyUiLanguage.Text(n.Message))]))) { Owner = this }.ShowDialog();
        var property = DependencyPropertyDescriptor.FromProperty(TextBlock.TextProperty, typeof(TextBlock));
        EventHandler changed = (_, _) =>
        {
            if (string.IsNullOrWhiteSpace(result.Text) || notifications.LastOrDefault().Message == result.Text) return;
            notifications.Add((DateTime.Now, result.Text)); if (notifications.Count > 50) notifications.RemoveAt(0);
            button.Visibility = Visibility.Visible;
        };
        property.AddValueChanged(result, changed); Closed += (_, _) => property.RemoveValueChanged(result, changed);
        return button;
    }
}
