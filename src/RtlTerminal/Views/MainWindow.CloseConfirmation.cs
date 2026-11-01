using System.ComponentModel;
using System.Windows.Threading;

namespace RtlTerminal;

public partial class MainWindow
{
    private bool _closeConfirmationOpen;
    private void Window_Closing(object? sender, CancelEventArgs e)
    {
        if (_closeConfirmationOpen) { e.Cancel = true; return; }
        if (!IsVisible || _tabs.Count <= 1 || App.SystemSessionEnding) return;
        e.Cancel = true; _closeConfirmationOpen = true;
        try { e.Cancel = new CloseTabsWindow(_tabs.Count) { Owner = this }.ShowDialog() != true; }
        finally { _closeConfirmationOpen = false; }
        if (e.Cancel) Dispatcher.BeginInvoke(DispatcherPriority.Input, new Action(() => { if (IsVisible) ActiveView.Focus(); }));
    }
}
