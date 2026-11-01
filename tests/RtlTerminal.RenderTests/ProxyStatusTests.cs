using RtlTerminal;
using RtlTerminal.Proxy;
using System.Collections.Concurrent;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;

internal static partial class Program
{
    private static void CheckProxyStatus()
    {
        static void Check(bool condition, string message) { if (!condition) throw new Exception(message); }
        static ProxyState State(params (string Key, string? Value)[] fields) => new(fields.ToDictionary(p => p.Key, p => p.Value));
        var uiThread = Environment.CurrentManagedThreadId;
        var user = new StatusFixtureProxy(State()); var machine = new StatusFixtureProxy(State()); var process = new StatusFixtureProxy(State());
        var windows = new StatusFixtureProxy(State(("Flags", "1")));
        var http = State(("AutoDetect", "false")); var scope = "User";
        var reader = new ProxyStatusReader(user, machine, process, windows, _ => Task.FromResult((http, scope)));
        ProxyStatusSnapshot[] Read()
        {
            var results = new ConcurrentDictionary<int, ProxyStatusSnapshot>();
            reader.RefreshAsync(s => results[s.Index] = s, default).GetAwaiter().GetResult();
            Check(results.Count == 3, "Status refresh omitted a provider"); return results.OrderBy(p => p.Key).Select(p => p.Value).ToArray();
        }
        Check(Read().All(s => s.Status == ProxyConfigurationStatus.NotConfigured), "Absent proxies appeared active");
        Check(user.ReadThread != uiThread && windows.ReadThread != uiThread, "Synchronous provider read ran on the UI thread");
        machine.State = State(("HTTP_PROXY", "http://127.0.0.1:10808"));
        var current = Read(); Check(current[0].Status == ProxyConfigurationStatus.Enabled, "Persistent Machine proxy was ignored");
        Check(current[0].Groups.Count == 3 && current[0].Groups.All(g => g.Values.Count == 5), "Environment details omitted a scope or variable");
        process.State = State(("HTTP_PROXY", "http://127.0.0.1:9999"));
        Check(Read()[0].Status == ProxyConfigurationStatus.Partial, "Stale Process override was hidden");
        machine.State = State(); Check(Read()[0].Status == ProxyConfigurationStatus.Partial, "Process-only proxy claimed persistent enabled configuration");
        process.State = State(("NO_PROXY", "localhost")); Check(Read()[0].Status == ProxyConfigurationStatus.NotConfigured, "Bypass alone claimed active proxy routing");
        user.State = State(("HTTP_PROXY", "http://user:super-secret@127.0.0.1:10808"));
        current = Read(); Check(!current[0].Address.Contains("super-secret") && current[0].Groups.SelectMany(g => g.Values).All(v => !v.Value.Contains("super-secret")), "Dashboard leaked credentials");
        windows.State = State(("Flags", "3"), ("Proxy", "http=127.0.0.1:10808;https=127.0.0.1:10808"));
        Check(Read()[1].Status == ProxyConfigurationStatus.Enabled, "Valid protocol-specific Windows proxy was rejected");
        windows.State = State(("Flags", "3")); Check(Read()[1].Status == ProxyConfigurationStatus.Partial, "Missing enabled manual endpoint appeared green");
        windows.State = State(("Flags", "5"), ("Pac", "https://example.test/proxy.pac"));
        Check(Read()[1].Status == ProxyConfigurationStatus.Enabled, "PAC configuration was ignored");
        windows.State = State(("Flags", "9")); Check(Read()[1].Status == ProxyConfigurationStatus.Enabled, "Automatic detection state was ignored");
        http = State(("Proxy", "127.0.0.1:10808"), ("AutoDetect", "false")); scope = "Machine";
        current = Read(); Check(current[2].Status == ProxyConfigurationStatus.Enabled && current[2].Groups[0].Values.Any(v => v.Key == "Configuration scope" && v.Value == "Machine"), "Machine WinHTTP status lost its actual scope");
        http = State(("AutoconfigUrl", "invalid"), ("AutoDetect", "false")); Check(Read()[2].Status == ProxyConfigurationStatus.Partial, "Invalid WinHTTP PAC appeared green");
        user.Error = new UnauthorizedAccessException("fixture denied"); http = State(("AutoDetect", "false"));
        current = Read(); Check(current[0].Status == ProxyConfigurationStatus.Error && current[1].Status == ProxyConfigurationStatus.Enabled && current[2].Status == ProxyConfigurationStatus.NotConfigured, "One provider error contaminated other cards");
        using var cancelled = new CancellationTokenSource(); cancelled.Cancel();
        try { reader.RefreshAsync(_ => throw new Exception("Cancelled refresh published a result"), cancelled.Token).GetAwaiter().GetResult(); throw new Exception("Refresh ignored cancellation"); }
        catch (OperationCanceledException) { }

        var window = new ProxyManagerWindow();
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
        var root = (Grid)window.Content; var body = (StackPanel)((ScrollViewer)root.Children[0]).Content;
        var dashboard = (StackPanel)body.Children[0]; dashboard.Measure(new Size(560, double.PositiveInfinity));
        Check(dashboard.DesiredSize.Height <= 110, "Status dashboard is too tall");
        var tiles = dashboard.Children.OfType<UniformGrid>().Single(); Check(tiles.Columns == 3 && tiles.Children.Count == 3, "Status cards do not occupy one row");
        var notification = (Button)typeof(ProxyManagerWindow).GetField("notificationButton", flags)!.GetValue(window)!;
        Check(notification.Visibility == Visibility.Collapsed, "Empty notifications occupied footer space");
        var result = (TextBlock)typeof(ProxyManagerWindow).GetField("result", flags)!.GetValue(window)!; result.Text = "fixture notice";
        Check(notification.Visibility == Visibility.Visible && result.Parent == null, "Notification was lost or raw footer logs are still displayed");
        window.Close();
        if (Environment.GetCommandLineArgs().Contains("--visuals"))
        {
            if (Application.Current == null) _ = new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
            foreach (var theme in new[] { AppThemeKind.Dark, AppThemeKind.Light })
            {
                AppTheme.Apply(theme);
                var preview = new ProxyManagerWindow(); preview.Show();
                PumpUntil(() => !(bool)typeof(ProxyManagerWindow).GetField("statusRefreshing", flags)!.GetValue(preview)!);
                ((TextBlock)typeof(ProxyManagerWindow).GetField("result", flags)!.GetValue(preview)!).Text = "Saved snapshots cleared. Proxy configuration was not changed.";
                CaptureOperation(preview, "status-" + theme);
                if (OperatingSystem.IsWindowsVersionAtLeast(10, 0, 22000))
                {
                    // Caption color is a Set-only DWM attribute: test the actual native result,
                    // rather than calling an unsupported DwmGetWindowAttribute read-back.
                    var captionTheme = typeof(ProxyManagerWindow).Assembly.GetType("RtlTerminal.WindowCaptionTheme")!;
                    var captionResult = (int)captionTheme.GetMethod("Apply", BindingFlags.Static | BindingFlags.NonPublic)!.Invoke(null, new object[] { preview })!;
                    Check(captionResult >= 0, $"Windows rejected the themed caption: HRESULT {captionResult:X8}");
                }
                var details = new ProxyInfoWindow("Environment", current[0].Groups, "Process values are inherited snapshots. Persistent User and Machine settings are read separately.") { Owner = preview };
                details.Show(); CaptureOperation(details, "status-details-" + theme); details.Close(); preview.Close();
            }
        }
        Console.WriteLine("PASS compact three-card status, independent errors, persistent Machine/User/Process scopes, stale process, PAC/autodetect, actual WinHTTP scope, redaction, async reads, cancellation and notification-only footer");
    }
    private sealed class StatusFixtureProxy(ProxyState state) : IProxyProvider
    {
        public ProxyState State = state; public Exception? Error; public int ReadThread;
        public string Name => "Status fixture";
        public Task<ProxyState> ReadAsync(CancellationToken ct) { ReadThread = Environment.CurrentManagedThreadId; ct.ThrowIfCancellationRequested(); if (Error != null) throw Error; return Task.FromResult(State); }
        public void Validate(ProxyEndpoint endpoint) => throw new NotSupportedException();
        public ProxyState Desired(ProxyEndpoint endpoint, ProxyState previous, bool clear) => throw new NotSupportedException();
        public Task WriteAsync(ProxyState state, CancellationToken ct) => throw new Exception("Status must never write configuration");
    }
}
