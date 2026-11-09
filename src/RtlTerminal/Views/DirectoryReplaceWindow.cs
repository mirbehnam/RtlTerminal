using System.Windows;
using System.Windows.Controls;

namespace RtlTerminal;

internal sealed class DirectoryReplaceWindow : Window
{
    internal DirectoryOpenAction Action { get; private set; } = DirectoryOpenAction.Cancel;
    internal DirectoryReplaceWindow()
    {
        Title = UiText.Get("Directory history"); Width = 440; SizeToContent = SizeToContent.Height;
        ResizeMode = ResizeMode.NoResize; WindowStartupLocation = WindowStartupLocation.CenterOwner;
        ShowInTaskbar = false; WindowStyle = WindowStyle.None; AllowsTransparency = true;
        Background = System.Windows.Media.Brushes.Transparent;
        Resources.MergedDictionaries.Add(new() { Source = new Uri("/RtlTerminal;component/Views/ProxyManagerStyles.xaml", UriKind.Relative) });
        var panel = new StackPanel { Margin = new Thickness(22) };
        var title = new TextBlock { Text = UiText.Get("Replace the current terminal?"), FontSize = 18, FontWeight = FontWeights.SemiBold, TextWrapping = TextWrapping.Wrap };
        title.SetResourceReference(TextBlock.ForegroundProperty, "ChromeForegroundBrush"); panel.Children.Add(title);
        var message = new TextBlock { Text = UiText.Get("This tab has received input. Replacing the active pane closes its running session."), TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 10, 0, 20) };
        message.SetResourceReference(TextBlock.ForegroundProperty, "MutedForegroundBrush"); panel.Children.Add(message);
        var buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
        foreach (var (label, result) in new[] { ("Yes", DirectoryOpenAction.Replace), ("No", DirectoryOpenAction.Cancel), ("New tab", DirectoryOpenAction.NewTab) })
        {
            var button = new Button { Content = UiText.Get(label), MinWidth = 82, Padding = new Thickness(12, 7, 12, 7), Margin = new Thickness(6, 0, 0, 0), IsCancel = result == DirectoryOpenAction.Cancel, IsDefault = result == DirectoryOpenAction.Cancel };
            button.Click += (_, _) => { Action = result; DialogResult = result != DirectoryOpenAction.Cancel; };
            buttons.Children.Add(button);
        }
        panel.Children.Add(buttons);
        var frame = new Border { CornerRadius = new CornerRadius(10), BorderThickness = new Thickness(1), Child = panel };
        frame.SetResourceReference(Border.BackgroundProperty, "MenuBackgroundBrush"); frame.SetResourceReference(Border.BorderBrushProperty, "ToolbarBorderBrush"); Content = frame;
        SourceInitialized += (_, _) => WindowWorkArea.Attach(this);
    }
}
