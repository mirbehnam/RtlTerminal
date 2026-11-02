using System.IO;
using System.Runtime.InteropServices;
using System.Text;

namespace RtlTerminal;

public sealed record TerminalInputResult(bool Success, bool MayHaveWritten, string? Error, bool EnterAttempted = false);
public sealed partial class ConPtySession
{
    [DllImport("kernel32.dll")] private static extern uint GetCurrentThreadId();
    [DllImport("kernel32.dll", SetLastError = true)] private static extern IntPtr OpenThread(uint access, bool inherit, uint id);
    [DllImport("kernel32.dll", SetLastError = true)] private static extern bool CancelSynchronousIo(IntPtr thread);

    public Task<TerminalInputResult> WriteInputAsync(string payload, bool enter, CancellationToken cancellation)
    {
        var result = new TaskCompletionSource<TerminalInputResult>(TaskCreationOptions.RunContinuationsAsynchronously);
        // A dedicated thread makes cancellation specific to this write; no thread-pool
        // thread can be reused by unrelated I/O while cancellation is in progress.
        var worker = new Thread(() =>
        {
            var attempted = false; var held = false; var entered = false;
            var thread = OpenThread(1, false, GetCurrentThreadId());
            var cancelGate = new object(); var finished = false;
            using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellation);
            deadline.CancelAfter(TimeSpan.FromSeconds(15));
            using var timer = new Timer(_ =>
            {
                lock (cancelGate) if (!finished && deadline.IsCancellationRequested && thread != IntPtr.Zero) CancelSynchronousIo(thread);
            }, null, 50, 50);
            try
            {
                while (!(held = Monitor.TryEnter(_inputLock, 50))) deadline.Token.ThrowIfCancellationRequested();
                deadline.Token.ThrowIfCancellationRequested();
                var writer = _inputWriter ?? throw new IOException("The terminal input connection is closed.");
                if (thread == IntPtr.Zero) throw new IOException("Could not establish cancellable terminal input.");
                var bytes = Encoding.UTF8.GetBytes(payload);
                for (var offset = 0; offset < bytes.Length; offset += 16384)
                {
                    deadline.Token.ThrowIfCancellationRequested(); attempted = true;
                    writer.Write(bytes, offset, Math.Min(16384, bytes.Length - offset));
                }
                writer.Flush(); deadline.Token.ThrowIfCancellationRequested();
                if (enter)
                {
                    // Let bracketed-paste processing finish before the separate
                    // submit key, retaining the same input lock and cancellation.
                    if (deadline.Token.WaitHandle.WaitOne(TimeSpan.FromMilliseconds(200))) deadline.Token.ThrowIfCancellationRequested();
                    deadline.Token.ThrowIfCancellationRequested(); attempted = true; entered = true; writer.WriteByte(13); writer.Flush();
                }
                result.TrySetResult(new(true, attempted, null, entered));
            }
            catch (Exception ex) when (ex is IOException or ObjectDisposedException or OperationCanceledException)
            { result.TrySetResult(new(false, attempted, deadline.IsCancellationRequested ? "Terminal input timed out or was cancelled." : "The terminal input connection is closed.", entered)); }
            finally
            {
                lock (cancelGate) { finished = true; if (thread != IntPtr.Zero) CloseHandle(thread); }
                if (held) Monitor.Exit(_inputLock);
            }
        }) { IsBackground = true, Name = "Quick Input writer" };
        worker.Start(); return result.Task;
    }
}
