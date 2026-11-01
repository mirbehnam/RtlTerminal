using System.Windows;



namespace RtlTerminal;



public partial class App : Application
{
    internal static bool PerformanceProbe { get; private set; }
    internal static bool SystemSessionEnding { get; private set; }
    private const int MinimumWindowsBuild = 17763;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        if (e.Args.Length == 3 && e.Args[0] == "--proxy-machine-helper" && int.TryParse(e.Args[2], out var parentPid))
        {
            ShutdownMode = ShutdownMode.OnExplicitShutdown;
            _ = RunMachineHelperAsync(e.Args[1], parentPid);
            return;
        }
        if (e.Args.Length == 2 && e.Args[0] == "--proxy-machine-probe")
        {
            ShutdownMode = ShutdownMode.OnExplicitShutdown;
            _ = ProbeMachineHelperAsync(e.Args[1]);
            return;
        }

        var timingArgument = Array.IndexOf(e.Args, "--performance-log");
        PerformanceProbe = e.Args.Contains("--performance-probe");
        StartupPerformance.Start(timingArgument >= 0 && timingArgument + 1 < e.Args.Length ? e.Args[timingArgument + 1] : null);
        if (!OperatingSystem.IsWindowsVersionAtLeast(
                major: 10,
                minor: 0,
                build: MinimumWindowsBuild))
        {
            var version = Environment.OSVersion.Version;
            MessageBox.Show(
                "RtlTerminal requires Windows 10 version 1809 " +
                "(build 17763) or Windows Server 2019 or later." +
                Environment.NewLine +
                Environment.NewLine +
                $"Detected Windows version: {version}",
                "Unsupported Windows version",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
            Shutdown();
            return;
        }

        using (StartupPerformance.Measure("Startup.ThemeAndSettings"))
            AppTheme.Apply(AppSettings.LoadTheme(), ColorScheme.Find(AppSettings.LoadColorScheme()));
        using (StartupPerformance.Measure("Startup.MainWindow.Construct")) MainWindow = new MainWindow();
        using (StartupPerformance.Measure("Startup.MainWindow.Show")) MainWindow.Show();
        if (PerformanceProbe)
        {
            var started = System.Diagnostics.Stopwatch.StartNew();
            var timer = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromMilliseconds(100) };
            timer.Tick += (_, _) =>
            {
                if (!StartupPerformance.StartupReady && started.Elapsed < TimeSpan.FromSeconds(30)) return;
                timer.Stop(); StartupPerformance.Mark(StartupPerformance.StartupReady ? "Probe.Ready" : "Probe.Timeout");
                MainWindow.Close();
            };
            timer.Start();
        }
    }
    protected override void OnExit(ExitEventArgs e)
    {
        StartupPerformance.Mark("App.Exit");
        StartupPerformance.StopAsync().Wait(TimeSpan.FromMilliseconds(500));
        base.OnExit(e);
    }
    protected override void OnSessionEnding(SessionEndingCancelEventArgs e)
    {
        base.OnSessionEnding(e);
        SystemSessionEnding = !e.Cancel;
    }
    private async Task RunMachineHelperAsync(string pipe, int parentPid)
    {
        try { Shutdown(await Proxy.MachineProxyElevation.RunHelperAsync(pipe, parentPid)); }
        catch { Shutdown(5); }
    }
    private async Task ProbeMachineHelperAsync(string path)
    {
        try { await Proxy.MachineProxyElevation.ProbeToFileAsync(path); Shutdown(0); }
        catch { Shutdown(6); }
    }
}
