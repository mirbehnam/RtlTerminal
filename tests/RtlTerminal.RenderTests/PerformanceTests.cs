using System.IO;
using System.Reflection;
using System.Text.Json;
using System.Windows;
using RtlTerminal;

internal static partial class Program
{
    private static void CheckPerformanceLogging()
    {
        var directory = Path.GetFullPath(Path.Combine("artifacts", "performance-tests-" + Guid.NewGuid().ToString("N")));
        Directory.CreateDirectory(directory);
        var path = Path.Combine(directory, "concurrent.jsonl");
        StartupPerformance.Start(path);
        Parallel.For(0, 100, _ => { using var timing = StartupPerformance.Measure("Test.Concurrent"); });
        StartupPerformance.StopAsync().GetAwaiter().GetResult();
        var records = File.ReadAllLines(path).Select(line => JsonDocument.Parse(line)).ToArray();
        try
        {
            if (records.Count(r => r.RootElement.GetProperty("stage").GetString() == "Test.Concurrent") != 100 ||
                records.Any(r => r.RootElement.GetProperty("elapsedMs").GetDouble() < 0))
                throw new Exception("Performance records lost or invalid under concurrent writes");
        }
        finally { foreach (var record in records) record.Dispose(); }
        // A failed log destination must not throw or prevent UI startup.
        StartupPerformance.Start(path);
        StartupPerformance.Mark("Test.UnwritableDestination");
        StartupPerformance.StopAsync().GetAwaiter().GetResult();
        var uiLog = Path.Combine(directory, "components.jsonl");
        StartupPerformance.Start(uiLog);
        if (Application.Current == null) _ = new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
        using (StartupPerformance.Measure("Test.Theme")) AppTheme.Apply(AppThemeKind.Dark);
        FontSettingsWindow settings;
        using (StartupPerformance.Measure("Settings.Construct")) settings = new FontSettingsWindow("Consolas", 16, FontWeights.Normal, FontStyles.Normal, 2000);
        settings.Show();
        CaptureOperation(settings, "performance-settings"); settings.Close();
        var manager = new ProxyManagerWindow(); manager.Show();
        var field = typeof(ProxyManagerWindow).GetField("detectionComplete", BindingFlags.NonPublic | BindingFlags.Instance)!;
        var watch = System.Diagnostics.Stopwatch.StartNew();
        while (!(bool)field.GetValue(manager)! && watch.Elapsed < TimeSpan.FromSeconds(45))
        {
            var frame = new System.Windows.Threading.DispatcherFrame();
            manager.Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.ContextIdle, new Action(() => frame.Continue = false));
            System.Windows.Threading.Dispatcher.PushFrame(frame); Thread.Sleep(20);
        }
        var complete = (bool)field.GetValue(manager)!;
        manager.Close(); StartupPerformance.StopAsync().GetAwaiter().GetResult();
        if (!complete) throw new Exception("Developer-tool detection did not finish within the performance test deadline");
        Console.WriteLine("PASS concurrent performance logs, flush, failed destination isolation and read-only component timing: " + uiLog);
        foreach (var line in File.ReadAllLines(uiLog))
        {
            using var record = JsonDocument.Parse(line);
            var root = record.RootElement;
            if (root.GetProperty("durationMs").ValueKind == JsonValueKind.Number)
                Console.WriteLine(root.GetProperty("stage").GetString() + ": " + root.GetProperty("durationMs").GetDouble().ToString("F1") + " ms");
        }
    }
}
