using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace RtlTerminal;

public sealed class ProxyLogWindow : Window
{
    public ProxyLogWindow(string log)
    {
        Title = "Proxy operation log"; Width = 820; Height = 540; MinWidth = 560; MinHeight = 320;
        WindowStartupLocation = WindowStartupLocation.CenterOwner; ShowInTaskbar = false;
        SetResourceReference(BackgroundProperty, "MenuBackgroundBrush"); SetResourceReference(ForegroundProperty, "ChromeForegroundBrush");
        var root = new DockPanel { Margin = new Thickness(18) }; Content = root;
        var heading = new TextBlock { Text = "Full diagnostic log · sensitive values are redacted", Margin = new Thickness(0, 0, 0, 12), FontSize = 16 };
        DockPanel.SetDock(heading, Dock.Top); root.Children.Add(heading);
        var controls = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 12, 0, 0) };
        var copy = new Button { Content = "Copy All", Padding = new Thickness(16, 7, 16, 7), Margin = new Thickness(0, 0, 10, 0) };
        var close = new Button { Content = "Close", IsCancel = true, Padding = new Thickness(16, 7, 16, 7) };
        foreach (var button in new[] { copy, close }) { button.SetResourceReference(Control.BackgroundProperty, "ToolbarBrush"); button.SetResourceReference(Control.ForegroundProperty, "ChromeForegroundBrush"); controls.Children.Add(button); }
        DockPanel.SetDock(controls, Dock.Bottom); root.Children.Add(controls);
        var text = new TextBox { Text = Proxy.ProxyDiagnostics.Sanitize(log), IsReadOnly = true, AcceptsReturn = true, AcceptsTab = true, FontFamily = new FontFamily("Consolas"), FontSize = 12,
            TextWrapping = TextWrapping.NoWrap, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = ScrollBarVisibility.Auto, Padding = new Thickness(10), FlowDirection = FlowDirection.LeftToRight };
        text.SetResourceReference(Control.BackgroundProperty, "ToolbarBrush"); text.SetResourceReference(Control.ForegroundProperty, "ChromeForegroundBrush");
        System.Windows.Automation.AutomationProperties.SetName(text, "Copyable proxy diagnostic log"); root.Children.Add(text);
        copy.Click += (_, _) =>
        {
            try { Clipboard.SetText(text.Text); copy.Content = "Copied"; }
            catch (System.Runtime.InteropServices.ExternalException) { ProxyUiLanguage.Show(this, "The clipboard is busy. Select the log and use Ctrl+C or try again.", "Copy log", MessageBoxButton.OK, MessageBoxImage.Information); }
        };
        close.Click += (_, _) => Close();
        ProxyUiLanguage.Attach(this);
    }
}
