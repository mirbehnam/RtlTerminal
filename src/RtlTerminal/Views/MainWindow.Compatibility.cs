using System.Windows;
using System.Windows.Controls;

namespace RtlTerminal;

public partial class MainWindow
{
    private void CompatibilityMenu_Opened(object sender, RoutedEventArgs e)
    {
        if (!ReferenceEquals(e.OriginalSource, sender)) return;
        var selected = ActivePane?.CompatibilityOverride?.ToString() ?? "Auto";
        foreach (MenuItem item in CompatibilityMenuItem.Items)
        {
            item.IsChecked = Equals(item.Tag, selected);
            item.IsEnabled = ActivePane is not null;
        }
    }

    private void CompatibilityProfile_Click(object sender, RoutedEventArgs e)
    {
        if (ActivePane is not { } pane || sender is not MenuItem { Tag: string tag }) return;
        pane.CompatibilityOverride = tag == "Auto" ? null : Enum.Parse<TuiCompatibilityProfile>(tag);
        // Re-render the unmodified snapshot immediately, even while the CLI is idle.
        RenderVisiblePanes();
        pane.View.Focus();
    }
}
