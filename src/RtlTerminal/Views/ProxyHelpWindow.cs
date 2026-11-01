using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace RtlTerminal;

public sealed class ProxyHelpWindow : Window
{
    private readonly StackPanel sections = new();
    public ProxyHelpWindow()
    {
        Title = "Proxy Manager Guide"; Width = 560; Height = 545; MinWidth = 420; MinHeight = 320;
        WindowStartupLocation = WindowStartupLocation.CenterOwner; ShowInTaskbar = false; FontFamily = new FontFamily("Segoe UI"); FontSize = 13;
        Resources.MergedDictionaries.Add(new() { Source = new Uri("/RtlTerminal;component/Controls/TerminalScrollBar.xaml", UriKind.Relative) });
        Resources.MergedDictionaries.Add(new() { Source = new Uri("/RtlTerminal;component/Views/ProxyManagerStyles.xaml", UriKind.Relative) });
        SetResourceReference(BackgroundProperty, "TitleBarBrush"); SetResourceReference(ForegroundProperty, "ChromeForegroundBrush"); WindowCaptionTheme.Attach(this);
        var root = new DockPanel { Margin = new Thickness(16) }; Content = root;
        var close = new Button { Content = "Close", IsCancel = true, IsDefault = true, Padding = new Thickness(18, 6, 18, 6), HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 10, 0, 0) };
        close.Click += (_, _) => Close(); DockPanel.SetDock(close, Dock.Bottom); root.Children.Add(close);
        root.Children.Add(new ScrollViewer { Content = sections, VerticalScrollBarVisibility = ScrollBarVisibility.Auto });
        UpdateContent(); UiText.Changed += UpdateContent; Closed += (_, _) => UiText.Changed -= UpdateContent;
        ProxyUiLanguage.Attach(this);
    }
    private void UpdateContent()
    {
        sections.Children.Clear();
        foreach (var section in ProxyHelpContent.Get(UiText.Current))
        {
            var content = new StackPanel();
            content.Children.Add(new TextBlock { Text = section.Title, FontWeight = FontWeights.SemiBold, FontSize = 14, TextWrapping = TextWrapping.Wrap });
            var body = new TextBlock { Text = section.Body, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 6, 0, 0), LineHeight = 21 };
            body.SetResourceReference(TextBlock.ForegroundProperty, "MutedForegroundBrush"); content.Children.Add(body);
            var card = new Border { Child = content, CornerRadius = new CornerRadius(8), Padding = new Thickness(12), Margin = new Thickness(0, 0, 0, 8), BorderThickness = new Thickness(1) };
            card.SetResourceReference(Border.BackgroundProperty, "MenuBackgroundBrush"); card.SetResourceReference(Border.BorderBrushProperty, "ToolbarBorderBrush"); sections.Children.Add(card);
        }
    }
}
