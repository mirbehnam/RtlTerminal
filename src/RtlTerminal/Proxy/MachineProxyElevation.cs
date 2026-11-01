using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.IO.Pipes;
using System.Runtime.InteropServices;
using System.Security.Principal;
using System.Security.AccessControl;
using System.Text.Json;
using Microsoft.Win32;

namespace RtlTerminal.Proxy;

public interface IMachineProxyWriter
{
    Task WriteAsync(ProxyState before, ProxyState desired, CancellationToken ct);
}
public sealed class MachineEnvironmentProxyProvider(Func<ProxyState>? read = null) : IProxyProvider
{
    public string Name => "Environment (Machine · administrator)";
    public ProxyDescriptor Descriptor => new(Name, "schema-1", "Windows environment", "Machine");
    public IMachineProxyWriter? Writer { get; set; }
    private ProxyState? lastRead;
    public Task<ProxyState> ReadAsync(CancellationToken ct)
    {
        lastRead = read == null ? ReadMachine() : read();
        return Task.FromResult(lastRead);
    }
    public static ProxyState ReadMachine() => new(EnvironmentProxyProvider.Variables.ToDictionary(n => n, n => Environment.GetEnvironmentVariable(n, EnvironmentVariableTarget.Machine)));
    public void Validate(ProxyEndpoint e) => e.Validate();
    public ProxyState Desired(ProxyEndpoint e, ProxyState previous, bool clear) => new EnvironmentProxyProvider().Desired(e, previous, clear);
    public async Task WriteAsync(ProxyState state, CancellationToken ct)
    {
        var writer = Writer ?? throw new UnauthorizedAccessException("Machine environment requires UAC approval. Retry from Proxy Manager.");
        await writer.WriteAsync(lastRead ?? await ReadAsync(ct), state, ct);
    }
}

internal sealed record MachineProxyRequest(string Action, ProxyState? Before = null, ProxyState? Desired = null);
internal sealed record MachineProxyReply(bool Success, bool Elevated, string? Error, ProxyOutcome? Outcome = null, bool MachineWriteAccess = false);

// The elevated process accepts only the five allowlisted Machine environment
// variables. No commands, executable paths, registry paths or file paths cross IPC.
public sealed class MachineProxyElevation : IMachineProxyWriter, IAsyncDisposable
{
    private readonly NamedPipeServerStream pipe;
    private readonly StreamReader reader;
    private readonly StreamWriter writer;
    private readonly Process child;
    private readonly SemaphoreSlim exchanges = new(1);
    private MachineProxyElevation(NamedPipeServerStream pipe, Process child)
    {
        this.pipe = pipe; this.child = child;
        reader = new(pipe, leaveOpen: true); writer = new(pipe, leaveOpen: true) { AutoFlush = true };
    }
    public static bool IsAdministrator => new WindowsPrincipal(WindowsIdentity.GetCurrent()).IsInRole(WindowsBuiltInRole.Administrator);
    public static async Task ProbeToFileAsync(string outputPath)
    {
        var before = MachineEnvironmentProxyProvider.ReadMachine().Signature;
        string? error = null; var verified = false;
        try { await using var session = await OpenAsync(Environment.ProcessPath!, default); verified = true; }
        catch (Exception ex) { error = ProxyDiagnostics.Describe(ex); }
        await File.WriteAllTextAsync(outputPath, JsonSerializer.Serialize(new { ParentElevated = IsAdministrator, HelperVerifiedElevated = verified, MachineWriteAccessVerified = verified,
            MachineUnchanged = before == MachineEnvironmentProxyProvider.ReadMachine().Signature, Error = error }));
    }
    public static async Task<MachineProxyElevation> OpenAsync(string executable, CancellationToken ct)
    {
        var pipeName = "RtlTerminal.Proxy." + Guid.NewGuid().ToString("N");
        var sid = WindowsIdentity.GetCurrent().User ?? throw new UnauthorizedAccessException();
        var security = new PipeSecurity(); security.SetAccessRuleProtection(true, false); security.SetOwner(sid);
        security.AddAccessRule(new PipeAccessRule(sid, PipeAccessRights.FullControl, AccessControlType.Allow));
        var pipe = NamedPipeServerStreamAcl.Create(pipeName, PipeDirection.InOut, 1, PipeTransmissionMode.Byte, PipeOptions.Asynchronous, 0, 0, security);
        Process? child = null;
        try
        {
            var start = new ProcessStartInfo(executable) { UseShellExecute = true, Verb = "runas", WindowStyle = ProcessWindowStyle.Hidden };
            start.ArgumentList.Add("--proxy-machine-helper"); start.ArgumentList.Add(pipeName); start.ArgumentList.Add(Environment.ProcessId.ToString());
            child = Process.Start(start) ?? throw new IOException("Could not start Machine proxy helper.");
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct); timeout.CancelAfter(TimeSpan.FromSeconds(90));
            var connection = pipe.WaitForConnectionAsync(timeout.Token);
            var exit = child.WaitForExitAsync(timeout.Token);
            if (await Task.WhenAny(connection, exit) == exit && !pipe.IsConnected)
                throw new IOException("Machine helper exited before authentication. Approve UAC using the same Windows account; alternate-account elevation is not supported. No proxy settings were changed.");
            await connection;
            if (!GetNamedPipeClientProcessId(pipe.SafePipeHandle.DangerousGetHandle(), out var pid) || pid != child.Id)
                throw new UnauthorizedAccessException("Machine helper identity did not match the launched process.");
            var session = new MachineProxyElevation(pipe, child);
            var reply = await session.Exchange(new("Probe"), ct);
            if (!reply.Success || !reply.Elevated || !reply.MachineWriteAccess) throw new UnauthorizedAccessException(reply.Error ?? "Machine helper did not verify permission to write the Machine environment.");
            return session;
        }
        catch (Win32Exception ex) when (ex.NativeErrorCode == 1223)
        {
            pipe.Dispose(); child?.Dispose();
            throw new IOException("UAC was cancelled. No proxy settings were changed.", ex);
        }
        catch { pipe.Dispose(); child?.Dispose(); throw; }
    }
    private async Task<MachineProxyReply> Exchange(MachineProxyRequest request, CancellationToken ct)
    {
        await exchanges.WaitAsync(ct);
        try
        {
            await writer.WriteLineAsync(JsonSerializer.Serialize(request).AsMemory(), ct);
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct); timeout.CancelAfter(TimeSpan.FromSeconds(30));
            return JsonSerializer.Deserialize<MachineProxyReply>(await reader.ReadLineAsync(timeout.Token) ?? throw new IOException("Machine helper disconnected; inspect Machine settings before retrying.")) ?? throw new IOException("Invalid Machine helper response.");
        }
        finally { exchanges.Release(); }
    }
    public async Task WriteAsync(ProxyState before, ProxyState desired, CancellationToken ct)
    {
        var reply = await Exchange(new("WriteEnvironment", before, desired), ct);
        if (!reply.Success && reply.Outcome == ProxyOutcome.RequiresPermission) throw new UnauthorizedAccessException(reply.Error);
        if (!reply.Success) throw new IOException(reply.Error ?? "Machine proxy operation failed.");
    }
    public async ValueTask DisposeAsync()
    {
        writer.Dispose(); reader.Dispose(); await pipe.DisposeAsync(); child.Dispose(); exchanges.Dispose();
    }
    public static async Task<int> RunHelperAsync(string pipeName, int parentPid)
    {
        if (!IsAdministrator || !pipeName.StartsWith("RtlTerminal.Proxy.", StringComparison.Ordinal) ||
            !Guid.TryParseExact(pipeName["RtlTerminal.Proxy.".Length..], "N", out _)) return 2;
        using var lifetime = new CancellationTokenSource(TimeSpan.FromMinutes(5));
        using var pipe = new NamedPipeClientStream(".", pipeName, PipeDirection.InOut, PipeOptions.Asynchronous);
        await pipe.ConnectAsync(15000, lifetime.Token);
        if (!GetNamedPipeServerProcessId(pipe.SafePipeHandle.DangerousGetHandle(), out var actualPid) || actualPid != parentPid) return 3;
        using var parent = Process.GetProcessById(parentPid);
        if (!string.Equals(parent.MainModule?.FileName, Environment.ProcessPath, StringComparison.OrdinalIgnoreCase)) return 4;
        using var reader = new StreamReader(pipe, leaveOpen: true);
        using var writer = new StreamWriter(pipe, leaveOpen: true) { AutoFlush = true };
        while (await reader.ReadLineAsync(lifetime.Token) is { } line)
        {
            MachineProxyReply reply;
            try
            {
                if (line.Length > 65536) throw new IOException("Machine request is too large.");
                var request = JsonSerializer.Deserialize<MachineProxyRequest>(line) ?? throw new IOException("Invalid Machine request.");
                if (request.Action == "Probe")
                {
                    WindowsInternetOptions.CheckPolicy();
                    using var key = Registry.LocalMachine.OpenSubKey(@"SYSTEM\CurrentControlSet\Control\Session Manager\Environment", writable: true)
                        ?? throw new UnauthorizedAccessException("Machine environment registry key could not be opened for writing.");
                    reply = new(true, true, null, MachineWriteAccess: true);
                }
                else if (request.Action == "WriteEnvironment")
                {
                    WindowsInternetOptions.CheckPolicy();
                    ValidateRequest(request.Before!); ValidateRequest(request.Desired!);
                    ApplyVerifiedEnvironment(request.Before!, request.Desired!, MachineEnvironmentProxyProvider.ReadMachine,
                        (key, value) => Environment.SetEnvironmentVariable(key, value, EnvironmentVariableTarget.Machine));
                    reply = new(true, true, null);
                }
                else throw new NotSupportedException("Unknown Machine operation.");
            }
            catch (Exception ex) { reply = new(false, true, ProxyService.SafeError(ex), ex is UnauthorizedAccessException ? ProxyOutcome.RequiresPermission : ProxyOutcome.Failed); }
            await writer.WriteLineAsync(JsonSerializer.Serialize(reply).AsMemory(), lifetime.Token);
        }
        return 0;
    }
    public static void ValidateRequest(ProxyState state)
    {
        if (state == null || state.Values.Count != EnvironmentProxyProvider.Variables.Length ||
            state.Values.Keys.Any(k => !EnvironmentProxyProvider.Variables.Contains(k, StringComparer.Ordinal)))
            throw new ArgumentException("Machine requests may contain only the allowlisted proxy variables.");
        if (state.Values.Values.Any(v => v != null && (v.Length > 8192 || v.Any(char.IsControl)))) throw new ArgumentException("Invalid Machine proxy value.");
    }
    public static void ApplyVerifiedEnvironment(ProxyState before, ProxyState desired, Func<ProxyState> read, Action<string, string?> set)
    {
        ValidateRequest(before); ValidateRequest(desired);
        var current = read();
        if (current.Signature != before.Signature && current.Signature != desired.Signature) throw new IOException("Conflict detected: Machine proxy variables changed after snapshot.");
        foreach (var key in EnvironmentProxyProvider.Variables)
        {
            if (current[key] == desired[key]) continue;
            if (read()[key] != current[key]) throw new IOException("Conflict detected: a Machine proxy variable changed during mutation.");
            set(key, desired[key]);
        }
        if (read().Signature != desired.Signature) throw new IOException("Machine proxy read-back did not match the request.");
    }
    [DllImport("kernel32.dll", SetLastError = true)] private static extern bool GetNamedPipeClientProcessId(IntPtr pipe, out int pid);
    [DllImport("kernel32.dll", SetLastError = true)] private static extern bool GetNamedPipeServerProcessId(IntPtr pipe, out int pid);
}
