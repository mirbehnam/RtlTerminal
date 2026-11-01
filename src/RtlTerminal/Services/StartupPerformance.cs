using System.Diagnostics;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Threading.Channels;
using System.Windows;

namespace RtlTerminal;

/// <summary>Bounded, best-effort local timings. Never records terminal text or configuration values.</summary>
public static class StartupPerformance
{
    private static readonly object Gate = new();
    private sealed record Entry(string stage, double elapsedMs, double processElapsedMs, double? durationMs, int thread);
    private static Channel<Entry>? queue;
    private static Task writer = Task.CompletedTask;
    private static long origin;
    private static double processAge;
    private static int entries;
    private static int firstOutput, firstFrame;
    internal static bool StartupReady => Volatile.Read(ref firstOutput) != 0 && Volatile.Read(ref firstFrame) != 0;
    private static bool windowsRegistered;
    private static readonly ConditionalWeakTable<Window, object> Windows = new();
    public static string? LogPath { get; private set; }

    public static void Start(string? path = null)
    {
        lock (Gate)
        {
            if (queue != null) return;
            origin = Stopwatch.GetTimestamp();
            entries = 0;
            firstOutput = firstFrame = 0;
            try { using var process = Process.GetCurrentProcess(); processAge = (DateTime.UtcNow - process.StartTime.ToUniversalTime()).TotalMilliseconds; } catch { processAge = 0; }
            var automatic = path == null;
            LogPath = path ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "RtlTerminal", "Performance", $"startup-{DateTime.UtcNow:yyyyMMdd-HHmmssfff}-{Environment.ProcessId}.jsonl");
            var channel = Channel.CreateBounded<Entry>(new BoundedChannelOptions(2048) { SingleReader = true, FullMode = BoundedChannelFullMode.DropWrite });
            queue = channel;
            var destination = LogPath;
            writer = Task.Run(async () =>
            {
                try
                {
                    var directory = Path.GetDirectoryName(Path.GetFullPath(destination))!;
                    Directory.CreateDirectory(directory);
                    using var stream = new FileStream(destination, FileMode.CreateNew, FileAccess.Write, FileShare.ReadWrite, 4096, true);
                    using var output = new StreamWriter(stream);
                    if (automatic)
                        foreach (var old in new DirectoryInfo(directory).GetFiles("startup-*.jsonl").OrderByDescending(f => f.CreationTimeUtc).Skip(10))
                            try { old.Delete(); } catch (IOException) { } catch (UnauthorizedAccessException) { }
                    await foreach (var entry in channel.Reader.ReadAllAsync())
                    { await output.WriteLineAsync(JsonSerializer.Serialize(entry)); await output.FlushAsync(); }
                }
                catch (Exception) { /* Diagnostics must not prevent startup, including read-only/full disks. */ }
            });
            Mark("App.ManagedEntry", processAge);
            if (!windowsRegistered)
            {
                EventManager.RegisterClassHandler(typeof(Window), FrameworkElement.LoadedEvent, new RoutedEventHandler((sender, e) =>
                {
                    if (sender is not Window window || !ReferenceEquals(e.OriginalSource, window) || Windows.TryGetValue(window, out _)) return;
                    Windows.Add(window, new object());
                    var name = window.GetType().Name;
                    Mark(name + ".Loaded");
                    var started = Stopwatch.GetTimestamp();
                    EventHandler? rendered = null;
                    rendered = (_, _) => { window.ContentRendered -= rendered; Mark(name + ".ContentRendered", Stopwatch.GetElapsedTime(started).TotalMilliseconds); };
                    window.ContentRendered += rendered;
                }));
                windowsRegistered = true;
            }
        }
    }

    public static void Mark(string stage, double? durationMs = null)
    {
        if (stage == "Terminal.FirstOutput") Interlocked.Exchange(ref firstOutput, 1);
        if (stage == "MainWindow.ContentRendered") Interlocked.Exchange(ref firstFrame, 1);
        var target = queue;
        if (target == null || Interlocked.Increment(ref entries) > 10000) return;
        var elapsed = Stopwatch.GetElapsedTime(origin).TotalMilliseconds;
        target.Writer.TryWrite(new Entry(stage, Math.Round(elapsed, 3), Math.Round(processAge + elapsed, 3), durationMs, Environment.CurrentManagedThreadId));
    }

    public static IDisposable Measure(string stage) => new Timing(stage);
    private sealed class Timing(string stage) : IDisposable
    {
        private readonly long started = Stopwatch.GetTimestamp();
        private int disposed;
        public void Dispose() { if (Interlocked.Exchange(ref disposed, 1) == 0) Mark(stage, Stopwatch.GetElapsedTime(started).TotalMilliseconds); }
    }

    public static Task StopAsync()
    {
        lock (Gate) { queue?.Writer.TryComplete(); queue = null; return writer; }
    }
}
