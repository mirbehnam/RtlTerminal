namespace RtlTerminal;

public partial class MainWindow
{
    private readonly TerminalBellService _terminalBell = new(AppSettings.LoadBellSettings());
    private volatile bool _bellWindowActive;

    private void InitializeTerminalBell()
    {
        _bellWindowActive = IsActive;
        Activated += (_, _) => _bellWindowActive = true;
        Deactivated += (_, _) => _bellWindowActive = false;
    }
}
