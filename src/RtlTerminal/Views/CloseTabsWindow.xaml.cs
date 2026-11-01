using System.Windows;

namespace RtlTerminal;

public partial class CloseTabsWindow : Window
{
    private readonly int tabCount;
    public CloseTabsWindow(int tabCount)
    {
        this.tabCount = tabCount; InitializeComponent(); UpdateCount();
        UiText.Changed += UpdateCount; Closed += (_, _) => UiText.Changed -= UpdateCount;
        Loaded += (_, _) => CancelButton.Focus();
    }
    private void UpdateCount() => TabCountText.Text = UiText.Format("{0} tabs will be closed.", tabCount);
    private void Confirm_Click(object sender, RoutedEventArgs e) => DialogResult = true;
    private void Cancel_Click(object sender, RoutedEventArgs e) => DialogResult = false;
}
