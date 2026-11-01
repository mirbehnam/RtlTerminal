using RtlTerminal;
using RtlTerminal.Proxy;
using System.IO;
using System.Windows;
using System.Windows.Threading;
using System.Windows.Media;
using System.Windows.Media.Imaging;

internal static partial class Program
{
    private static void CheckProxyOperations(bool visuals = false)
    {
        CheckProxyConflictSemantics();
        static void Check(bool condition, string message) { if (!condition) throw new Exception(message); }
        var endpoint = new ProxyEndpoint("HTTP", "127.0.0.1", 10808, "");
        var good = new OperationFixture("verified"); var bad = new OperationFixture("bad read-back") { BadReadback = true };
        var skipped = new OperationFixture("inspection") { InspectionOnly = true };
        var state = new ProxyOperationState([good.Name, bad.Name, skipped.Name]);
        var events = new List<string>();
        var progress = new OperationProgress(update =>
        {
            if (update.Provider != null && update.Diagnostic == null)
            {
                if (update.Result?.Success == true && (good.Writes != 1 || good.PostWriteReads == 0)) throw new Exception("Green checkmark preceded verification");
                events.Add(update.Provider + (update.Result == null ? ":running" : ":" + update.Result.Outcome));
            }
            state.Report(update);
        });
        var service = new ProxyService(Path.Combine(Environment.CurrentDirectory, "artifacts", "proxy-operation-tests-" + Guid.NewGuid().ToString("N")));
        var results = service.ExecuteAsync([good, bad, skipped], endpoint, false, default, progress: progress).GetAwaiter().GetResult();
        state.Complete(results);
        Check(events.SequenceEqual(new[] { "verified:running", "verified:Success", "bad read-back:running", "bad read-back:Failed", "inspection:running", "inspection:Skipped" }), "Operation progress did not follow actual sequential verification");
        Check(state.Completed == 3 && state.Succeeded == 1 && state.Failed == 1 && state.Skipped == 1 && !state.AllSucceeded, "Failed/skipped rows counted as success");
        var blocked = new ProxyOperationState(["a", "b"]); blocked.Complete([], "Snapshot failed");
        var nativeLog = ProxyDiagnostics.Describe(new IOException("Snapshot/read failed", new System.ComponentModel.Win32Exception(87, "http://user:password@example.test api_key=hidden-value")));
        Check(nativeLog.Contains("NativeErrorCode=87") && nativeLog.Contains("0x00000057") && !nativeLog.Contains("hidden-value") && !nativeLog.Contains("user:password"), "Full diagnostics lost native codes or leaked credentials");
        var readFailure = new OperationFixture("snapshot read failure") { ThrowRead = true };
        try { service.ExecuteAsync([readFailure], endpoint, false, default).GetAwaiter().GetResult(); throw new Exception("Snapshot failure was ignored"); }
        catch (IOException ex) { Check(ex.Message.Contains(readFailure.Name) && ProxyDiagnostics.Describe(ex).Contains("NativeErrorCode=87") && readFailure.Writes == 0, "Snapshot failure lost provider/native context or mutated settings"); }
        Check(blocked.Skipped == 2 && blocked.Succeeded == 0 && !blocked.AllSucceeded, "Preflight failure painted pending rows green");
        var interrupted = new ProxyOperationState(["a", "b"]); interrupted.Report(new("a")); interrupted.Complete([], "Interrupted");
        Check(interrupted.Failed == 1 && interrupted.Skipped == 1, "Interrupted mutation was presented as skipped/successful");
        var completed = new ProxyOperationState(["a"]); completed.Complete([new("a", true, "Verified", ProxyOutcome.Success)]);
        completed.Report(new("a")); Check(completed.AllSucceeded, "Late progress changed a completed operation");
        Console.WriteLine("PASS sequential real proxy progress, verification-gated success, bad read-back, skipped rows, partial failure, preflight interruption and accurate counts");
        if (!visuals) return;
        if (Application.Current == null) _ = new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
        foreach (var theme in new[] { AppThemeKind.Dark, AppThemeKind.Light })
        {
            AppTheme.Apply(theme);
            var gate = new TaskCompletionSource<IReadOnlyList<ProxyResult>>(TaskCreationOptions.RunContinuationsAsynchronously);
            var calls = 0;
            var window = new ProxyOperationWindow(["Environment (User)", "Git user"], false, updates =>
            {
                Interlocked.Increment(ref calls); updates.Report(new("Environment (User)")); return gate.Task;
            });
            window.Show(); PumpUntil(() => window.State.Rows[0].Status == ProxyRowStatus.Running);
            window.RaiseEvent(new RoutedEventArgs(FrameworkElement.LoadedEvent));
            window.Close(); Check(window.IsVisible && calls == 1, "Running dialog closed or started execution twice");
            CaptureOperation(window, "running-" + theme);
            gate.SetResult([new("Environment (User)", true, "Managed settings verified.", ProxyOutcome.Success), new("Git user", false, "Access denied. Review Configuration History.", ProxyOutcome.RequiresPermission)]);
            PumpUntil(() => window.State.IsComplete);
            Check(window.IsVisible && !window.State.AllSucceeded && window.State.Succeeded == 1, "Partial operation auto-closed or showed overall success");
            CaptureOperation(window, "partial-" + theme); window.Close();
            var success = new ProxyOperationWindow(["Environment (User)"], true, _ => Task.FromResult<IReadOnlyList<ProxyResult>>([new("Environment (User)", true, "Managed User settings cleared and verified.", ProxyOutcome.Success)]));
            success.Show(); PumpUntil(() => success.State.IsComplete);
            Check(success.State.AllSucceeded && success.IsVisible, "Success did not wait for user acknowledgement");
            CaptureOperation(success, "clear-success-" + theme); success.Close();
            var failedRead = new ProxyOperationWindow(["Windows system (User)"], true, _ => Task.FromException<IReadOnlyList<ProxyResult>>(new IOException("Snapshot/read failed for Windows system", new System.ComponentModel.Win32Exception(87))));
            failedRead.Show(); PumpUntil(() => failedRead.State.IsComplete);
            Check(failedRead.FullLog.Contains("NativeErrorCode=87") && failedRead.FullLog.Contains("Snapshot/read failed"), "Dialog did not retain full nested failure log");
            var logWindow = new ProxyLogWindow(failedRead.FullLog) { Owner = failedRead }; logWindow.Show();
            var logBox = ((System.Windows.Controls.DockPanel)logWindow.Content).Children.OfType<System.Windows.Controls.TextBox>().Single();
            Check(logBox.IsReadOnly && logBox.Text.Contains("NativeErrorCode=87"), "Full log is not exposed as selectable text");
            CaptureOperation(logWindow, "full-log-" + theme); logWindow.Close(); failedRead.Close();
        }
        var owner = new Window { Width = 600, Height = 300, Content = new System.Windows.Controls.Grid(), ShowInTaskbar = false };
        owner.Show();
        var modal = new ProxyOperationWindow(["Environment (User)"], false, _ => Task.FromResult<IReadOnlyList<ProxyResult>>([new("Environment (User)", true, "Verified", ProxyOutcome.Success)]));
        Dispatcher.CurrentDispatcher.BeginInvoke(DispatcherPriority.Background, new Action(() =>
        {
            PumpUntil(() => modal.State.IsComplete);
            Check(!ProxyOwnerEnabled(new System.Windows.Interop.WindowInteropHelper(owner).Handle), "Operation did not modal-disable the owner");
            modal.Close();
        }));
        modal.ShowModal(owner); Check(ProxyOwnerEnabled(new System.Windows.Interop.WindowInteropHelper(owner).Handle), "Owner remained disabled after acknowledgement"); owner.Close();
        Console.WriteLine("PASS animated dialog light/dark visuals, pending/running/partial/success, duplicate-load prevention, running close guard and explicit completion acknowledgement");
    }
    private static void PumpUntil(Func<bool> condition)
    {
        var until = DateTime.UtcNow.AddSeconds(10);
        while (!condition())
        {
            if (DateTime.UtcNow > until) throw new Exception("Operation dialog test timed out");
            var frame = new DispatcherFrame(); Dispatcher.CurrentDispatcher.BeginInvoke(DispatcherPriority.ContextIdle, new Action(() => frame.Continue = false)); Dispatcher.PushFrame(frame);
            Thread.Sleep(1);
        }
    }
    private static void CaptureOperation(Window window, string name)
    {
        var animationEnd = DateTime.UtcNow.AddMilliseconds(180);
        // Test-only rendering pump; production never waits for an animation.
        PumpUntil(() => DateTime.UtcNow >= animationEnd);
        window.UpdateLayout(); var bitmap = new RenderTargetBitmap((int)window.ActualWidth, (int)window.ActualHeight, 96, 96, PixelFormats.Pbgra32); bitmap.Render(window);
        var png = new PngBitmapEncoder(); png.Frames.Add(BitmapFrame.Create(bitmap)); Directory.CreateDirectory("artifacts/render-checks");
        using var output = File.Create("artifacts/render-checks/proxy-operation-" + name + ".png"); png.Save(output);
    }
    private sealed class OperationProgress(Action<ProxyOperationUpdate> report) : IProgress<ProxyOperationUpdate>
    { public void Report(ProxyOperationUpdate value) => report(value); }
    [System.Runtime.InteropServices.DllImport("user32.dll", EntryPoint = "IsWindowEnabled")]
    private static extern bool ProxyOwnerEnabled(IntPtr handle);
    private sealed class OperationFixture(string name) : IProxyProvider
    {
        public string Name => name; public bool BadReadback, InspectionOnly, ThrowRead; public int Writes, PostWriteReads; private string value = "old";
        public bool CanRestore => !InspectionOnly;
        public void Validate(ProxyEndpoint e) => e.Validate();
        public Task<ProxyState> ReadAsync(CancellationToken ct)
        {
            if (ThrowRead) throw new System.ComponentModel.Win32Exception(87);
            if (Writes > 0) PostWriteReads++;
            return Task.FromResult(new ProxyState(new() { ["Value"] = Writes > 0 && BadReadback ? "wrong" : value }));
        }
        public Task WriteAsync(ProxyState state, CancellationToken ct) { Writes++; value = state["Value"]!; return Task.CompletedTask; }
        public ProxyState Desired(ProxyEndpoint endpoint, ProxyState old, bool clear) => new(new() { ["Value"] = clear ? "" : endpoint.Url });
    }
}
