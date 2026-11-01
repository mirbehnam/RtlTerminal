using RtlTerminal.Proxy;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace RtlTerminal;

public sealed class ProxyInfoWindow : Window
{
    public ProxyInfoWindow(string title, IEnumerable<ProxyStatusGroup> groups, string? note = null)
    {
        Title = title; Width = 570; Height = 490; MinWidth = 420; MinHeight = 280; ShowInTaskbar = false;
        WindowStartupLocation = WindowStartupLocation.CenterOwner; FontFamily = new FontFamily("Segoe UI"); FontSize = 12;
        Resources.MergedDictionaries.Add(new() { Source = new Uri("/RtlTerminal;component/Controls/TerminalScrollBar.xaml", UriKind.Relative) });
        Resources.MergedDictionaries.Add(new() { Source = new Uri("/RtlTerminal;component/Views/ProxyManagerStyles.xaml", UriKind.Relative) });
        SetResourceReference(BackgroundProperty, "TitleBarBrush"); SetResourceReference(ForegroundProperty, "ChromeForegroundBrush"); WindowCaptionTheme.Attach(this);
        var root = new DockPanel { Margin = new Thickness(16) }; Content = root;
        var close = new Button { Content = "Close", IsCancel = true, IsDefault = true, Padding = new Thickness(18, 6, 18, 6), HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 10, 0, 0) };
        close.Click += (_, _) => Close(); DockPanel.SetDock(close, Dock.Bottom); root.Children.Add(close);
        var body = new StackPanel(); root.Children.Add(new ScrollViewer { Content = body, VerticalScrollBarVisibility = ScrollBarVisibility.Auto });
        if (note != null) body.Children.Add(new TextBlock { Text = note, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 0, 0, 12) });
        foreach (var group in groups)
        {
            var content = new StackPanel(); content.Children.Add(new TextBlock { Text = group.Name, FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 0, 0, 7) });
            foreach (var item in group.Values)
            {
                content.Children.Add(new TextBlock { Text = item.Key, FontSize = 11, Opacity = .7, Margin = new Thickness(0, 4, 0, 1) });
                var value = new TextBox { Text = item.Value, IsReadOnly = true, TextWrapping = TextWrapping.Wrap, BorderThickness = new Thickness(0), Padding = new Thickness(5), Background = Brushes.Transparent };
                value.SetResourceReference(ForegroundProperty, "ChromeForegroundBrush");
                System.Windows.Automation.AutomationProperties.SetName(value, item.Key); content.Children.Add(value);
            }
            var card = new Border { Child = content, CornerRadius = new CornerRadius(8), Padding = new Thickness(12), Margin = new Thickness(0, 0, 0, 8), BorderThickness = new Thickness(1) };
            card.SetResourceReference(Border.BackgroundProperty, "MenuBackgroundBrush"); card.SetResourceReference(Border.BorderBrushProperty, "ToolbarBorderBrush"); body.Children.Add(card);
        }
        ProxyUiLanguage.Attach(this);
    }
}
