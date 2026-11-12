using System.IO;
using System.Windows;
using System.Windows.Controls;
using RtlTerminal.CliLab;

namespace RtlTerminal;

public partial class MainWindow
{
    private void InitializeCliTestLab()
    {
        var item = new MenuItem { Header = "CLI Test Lab…", ToolTip = "Preview builds only · local mock responses" };
        item.Click += (_, _) => new CliTestLabWindow(OpenCliLabAsync) { Owner = this }.ShowDialog();
        ToolsMenuItem.Items.Add(new Separator()); ToolsMenuItem.Items.Add(item);
    }
    private async Task OpenCliLabAsync(CliLabInstallation installation, string response)
    {
        var current = await CliLabCatalog.InspectAsync(installation.Tool, installation.Path, default);
        if (!current.Supported || current.Version != installation.Version)
            throw new NotSupportedException("The executable or version changed. Refresh detection before starting a test.");
        var columns = GetColumns(ActiveView); var rows = GetRows(ActiveView);
        var result = await Task.Run(() =>
        {
            var lease = new CliLabSession(installation, response);
            ConPtySession? session = null;
            try { session = new ConPtySession(columns, rows); session.Start(lease.Command, lease.Workspace, lease.Environment); return (lease, session); }
            catch { session?.Dispose(); lease.Dispose(); throw; }
        });
        if (_windowClosed)
        {
            var drain = Task.Run(() => { try { var bytes = new byte[8192]; while (result.session.Read(bytes) > 0) { } } catch (IOException) { } catch (ObjectDisposedException) { } });
            await Task.Run(result.session.Dispose); await drain; result.lease.Dispose(); return;
        }
        var pane = CreatePane(TerminalProfile.CommandPrompt);
        pane.Session = result.session; pane.CliLabLifetime = result.lease;
        pane.Buffer = new TerminalBuffer(columns, rows, _historySize) { ClaudeLayout = installation.Tool == CliLabTool.Claude, PreserveTuiGrid = installation.Tool != CliLabTool.Claude };
        pane.CompatibilityOverride = installation.Tool == CliLabTool.Claude ? TuiCompatibilityProfile.Claude : TuiCompatibilityProfile.OpenTui;
        pane.GridSize = (columns, rows); pane.CancellationTokenSource = new CancellationTokenSource();
        var tab = new TerminalTab(pane) { CustomTitle = $"LOCAL TEST · {installation.Tool} {installation.Version}" };
        _tabs.Add(tab); ShowTab(tab); _ = Task.Run(() => ReadOutputLoop(pane)); QueueViewportResize();
        _ = Dispatcher.BeginInvoke(() => pane.View.Focus(), System.Windows.Threading.DispatcherPriority.Input);
    }
}
