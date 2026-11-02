using System.Diagnostics;
using System.IO;
using System.Net;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace RtlTerminal.Proxy;

public sealed record ProxyEndpoint(string Protocol, string Host, int Port, string Bypass)
{
    public bool? ManualEnabled { get; init; }
    public bool? AutoDetect { get; init; }
    public string? Pac { get; init; }
    public string Authority => new UriBuilder("http", Host.Trim('[', ']'), Port).Uri.Authority;
    public string Url => Protocol.ToLowerInvariant() + "://" + Authority;
    public string LocalBypass(string separator) => string.Join(separator,
        Bypass.Split([',', ';', '\r', '\n'], StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
            .Concat(["localhost", "127.0.0.1", "::1"]).Distinct(StringComparer.OrdinalIgnoreCase));
    public string WindowsBypass => string.Join(";", LocalBypass(";").Split(';').Select(rule =>
        IPAddress.TryParse(rule.Trim('[', ']'), out var ip) && ip.AddressFamily == System.Net.Sockets.AddressFamily.InterNetworkV6
            ? "[" + ip + "]" : rule).Distinct(StringComparer.OrdinalIgnoreCase));
    public void Validate()
    {
        if (Protocol is not ("HTTP" or "HTTPS" or "SOCKS5") || ManualEnabled != false && (Port is < 1 or > 65535 ||
            string.IsNullOrWhiteSpace(Host) || Host != Host.Trim() ||
            Uri.CheckHostName(Host.Trim('[', ']')) == UriHostNameType.Unknown ||
            Host.IndexOfAny(['/', '\\', '@', ';', '=', ' ', '\r', '\n']) >= 0))
            throw new ArgumentException("Enter a host or IP without a scheme, credentials or path, and a port between 1 and 65535.");
        if (Bypass.Any(c => char.IsControl(c) && c is not ('\r' or '\n')))
            throw new ArgumentException("Bypass rules contain control characters.");
        if (!string.IsNullOrEmpty(Pac) && (!Uri.TryCreate(Pac, UriKind.Absolute, out var pac) || pac.Scheme is not ("http" or "https") || !string.IsNullOrEmpty(pac.UserInfo)))
            throw new ArgumentException("PAC must be an HTTP/HTTPS URL without credentials.");
    }
}

public sealed record ProxyState(Dictionary<string, string?> Values)
{
    public string? this[string key] => Values.GetValueOrDefault(key);
    public string Signature => JsonSerializer.Serialize(Values.OrderBy(p => p.Key));
}
public interface IProxyProvider
{
    string Name { get; }
    ProxyDescriptor Descriptor => new(Name, "schema-1", "Windows APIs", "User");
    bool CanRestore => true;
    Task<ProxyState> ReadAsync(CancellationToken cancellation);
    void Validate(ProxyEndpoint endpoint);
    Task WriteAsync(ProxyState state, CancellationToken cancellation);
    ProxyState Desired(ProxyEndpoint endpoint, ProxyState previous, bool clear);
}
public enum ProxyOutcome { Success, Skipped, Failed, RequiresPermission, Conflict }
public sealed record ProxyResult(string Provider, bool Success, string Message, ProxyOutcome Outcome = ProxyOutcome.Failed);
public sealed record ProxyOperationUpdate(string? Provider, ProxyResult? Result = null, string? Phase = null, string? Diagnostic = null);
public sealed record ProxyBackup(string Provider, ProxyState Before, ProxyState After);

public sealed class ProxyService(string? backupDirectory = null)
{
    public ProxyHistoryStore History { get; } = new(backupDirectory);
    public string? LastBackupPath { get; private set; }
    public async Task<IReadOnlyList<ProxyResult>> ExecuteAsync(IReadOnlyList<IProxyProvider> providers,
        ProxyEndpoint endpoint, bool clear, CancellationToken cancellation, IReadOnlyList<IProxyProvider>? inventory = null, string? operation = null, IProgress<ProxyOperationUpdate>? progress = null)
    {
        using var operationLock = History.AcquireLock();
        progress?.Report(new(null, Phase: "Reading configurations and preparing a verified recovery snapshot…"));
        if (!clear) { endpoint.Validate(); foreach (var p in providers) p.Validate(endpoint); }
        var all = (inventory ?? providers).DistinctBy(p => p.Name).ToArray();
        if (providers.Any(p => !all.Any(a => a.Name == p.Name))) throw new IOException("Operation provider is missing from the full inventory.");
        var states = await Capture(all, cancellation, progress);
        var desired = providers.ToDictionary(p => p.Name, p => p.Desired(endpoint, states.Single(s => s.Provider.Name == p.Name).State, clear));
        var saved = await History.SaveAsync(operation ?? (clear ? "Clear proxies" : "Save proxies"), states, cancellation, providers.Select(p => p.Name));
        LastBackupPath = saved.Path;
        await History.MarkPendingAsync(saved, cancellation);
        var results = await Mutate(providers, states.ToDictionary(s => s.Provider.Name, s => s.State), desired, cancellation, progress);
        progress?.Report(new(null, Phase: "Finalizing operation history…"));
        if (results.All(r => r.Success)) History.MarkComplete();
        return results;
    }
    private static async Task<List<ProxyHistoryEntry>> Capture(IEnumerable<IProxyProvider> providers, CancellationToken ct, IProgress<ProxyOperationUpdate>? progress = null)
    {
        var entries = new List<ProxyHistoryEntry>();
        foreach (var provider in providers)
        {
            progress?.Report(new(null, Phase: "Snapshot: reading " + provider.Name));
            try { entries.Add(new(provider.Descriptor, await provider.ReadAsync(ct))); }
            catch (OperationCanceledException) { throw; }
            catch (Exception ex) { throw new IOException("Snapshot/read failed for " + provider.Name + ": " + SafeError(ex), ex); }
        }
        return entries;
    }
    private static async Task<List<ProxyResult>> Mutate(IEnumerable<IProxyProvider> providers, Dictionary<string, ProxyState> before, Dictionary<string, ProxyState> desired, CancellationToken ct, IProgress<ProxyOperationUpdate>? progress = null)
    {
        var results = new List<ProxyResult>();
        foreach (var p in providers)
        {
            ct.ThrowIfCancellationRequested();
            var previousCount = results.Count;
            progress?.Report(new(p.Name));
            try
            {
                var current = await p.ReadAsync(ct);
                if (current.Signature == desired[p.Name].Signature)
                {
                    progress?.Report(new(null, Phase: p.Name + ": requested state is already present; verifying again without rewriting."));
                    if ((await p.ReadAsync(ct)).Signature != desired[p.Name].Signature)
                        throw new IOException("Conflict detected: configuration changed while verifying the already-matching state. Refresh and retry.");
                    results.Add(new(p.Name, true, "Already matches the requested managed configuration; re-read verified. No write was needed. Existing processes and out-of-scope overrides may retain other settings.", ProxyOutcome.Success));
                    continue;
                }
                if (current.Signature != before[p.Name].Signature)
                {
                    var changed = before[p.Name].Values.Keys.Union(current.Values.Keys).Where(k => before[p.Name][k] != current[k]);
                    throw new IOException("Conflict detected: configuration changed after the snapshot and does not match the requested state. Changed fields: " + string.Join(", ", changed) + ". Refresh and retry.");
                }
                if (!p.CanRestore && before[p.Name].Signature != desired[p.Name].Signature)
                {
                    results.Add(new(p.Name, false, "Skipped: inspection-only scope differs from the snapshot. Manual recovery is required.", ProxyOutcome.Skipped));
                    continue;
                }
                await p.WriteAsync(desired[p.Name], ct);
                if ((await p.ReadAsync(ct)).Signature != desired[p.Name].Signature) throw new IOException("Read-back differs from requested configuration. Recovery is available in Configuration History.");
                results.Add(new(p.Name, true, "Saved and verified. Environment/project overrides and existing processes may retain other settings.", ProxyOutcome.Success));
            }
            catch (OperationCanceledException) { throw; }
            catch (Exception ex)
            {
                progress?.Report(new(p.Name, Diagnostic: ProxyDiagnostics.Describe(ex)));
                results.Add(new(p.Name, false, SafeError(ex), ex is UnauthorizedAccessException ? ProxyOutcome.RequiresPermission : ex.Message.StartsWith("Conflict", StringComparison.Ordinal) ? ProxyOutcome.Conflict : ProxyOutcome.Failed));
            }
            finally { if (results.Count > previousCount) progress?.Report(new(p.Name, results[^1])); }
        }
        return results;
    }
    public async Task<IReadOnlyList<ProxyResult>> RestoreHistoryAsync(string path, IReadOnlyList<IProxyProvider> inventory, Dictionary<string, string> expectedCurrent, CancellationToken ct)
    {
        using var operationLock = History.AcquireLock();
        var selected = await History.ReadAsync(path, ct);
        var targets = new List<IProxyProvider>();
        foreach (var entry in selected.Snapshot.Entries)
        {
            var current = inventory.SingleOrDefault(p => p.Name == entry.Provider.Name) ?? throw new NotSupportedException(entry.Provider.Name + " is no longer installed or its version is unverified.");
            if (current.Descriptor != entry.Provider) throw new NotSupportedException(entry.Provider.Name + ": version, configuration location or scope changed. Unsafe restoration was refused.");
            targets.Add(current);
        }
        var states = await Capture(inventory, ct);
        foreach (var entry in states)
            if (!expectedCurrent.TryGetValue(entry.Provider.Name, out var expected) || expected != entry.State.Signature) throw new IOException("Conflict detected after restore preview. Review the current configuration again.");
        var backup = await History.SaveAsync("Before restore " + selected.Snapshot.Id.ToString("N"), states, ct, targets.Select(p => p.Name));
        LastBackupPath = backup.Path;
        await History.MarkPendingAsync(backup, ct);
        var results = await Mutate(targets, states.ToDictionary(s => s.Provider.Name, s => s.State), selected.Snapshot.Entries.ToDictionary(s => s.Provider.Name, s => s.State), ct);
        if (results.All(r => r.Success)) History.MarkComplete();
        return results;
    }
    public static string SafeError(Exception ex) => ex is System.ComponentModel.Win32Exception native
        ? $"Windows error {native.NativeErrorCode} (0x{native.NativeErrorCode:X8}): " + ProxyDiagnostics.Sanitize(native.Message)
        : ex is UnauthorizedAccessException
        ? "Requires permission: access denied or organization policy. No success was assumed."
        : ex is ArgumentException or NotSupportedException or IOException ? ProxyDiagnostics.Sanitize(ex.Message) : "Operation failed (" + ex.GetType().Name + "). No success was assumed.";
    public static string Redact(string value) => Regex.Replace(value, @"(?i)([a-z][a-z0-9+.-]*://)[^\s/@]+@", "$1[redacted]@");
}

internal static class ProxyProcess
{
    public static Task<string> ToolVersion(string executable, string argument, CancellationToken ct)
    {
        // Rustup shims otherwise resolve the toolchain relative to the terminal's
        // launch directory, which may contain a project override.
        var start = new ProcessStartInfo(executable) { UseShellExecute = false, CreateNoWindow = true,
            RedirectStandardOutput = true, RedirectStandardError = true,
            WorkingDirectory = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile) };
        start.ArgumentList.Add(argument);
        return RunStart(start, ct, []);
    }
    public static async Task<string> Run(string executable, IEnumerable<string> arguments, CancellationToken ct, params int[] allowedExitCodes)
    {
        var start = new ProcessStartInfo(executable) { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true };
        foreach (var arg in arguments) start.ArgumentList.Add(arg);
        return await RunStart(start, ct, allowedExitCodes);
    }
    public static Task<string> ShimVersion(string executable, string versionArg, CancellationToken ct)
    {
        if (executable.IndexOfAny(['"', '%', '!', '^', '&', '|', '<', '>', '\r', '\n']) >= 0 || versionArg is not ("--version" or "-v")) throw new ArgumentException("Unsafe shim version probe.");
        var start = new ProcessStartInfo(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "cmd.exe"))
        {
            UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true,
            Arguments = "/d /s /c \"\"" + executable + "\" " + versionArg + "\""
        };
        return RunStart(start, ct, []);
    }
    private static async Task<string> RunStart(ProcessStartInfo start, CancellationToken ct, int[] allowedExitCodes)
    {
        start.Environment["COREPACK_ENABLE_NETWORK"] = "0";
        start.Environment["npm_config_offline"] = "true";
        start.Environment["UV_OFFLINE"] = "1";
        start.Environment["PIP_NO_INDEX"] = "1";
        using var process = Process.Start(start) ?? throw new IOException("Could not start configuration tool.");
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct); timeout.CancelAfter(TimeSpan.FromSeconds(12));
        var output = process.StandardOutput.ReadToEndAsync(timeout.Token);
        var error = process.StandardError.ReadToEndAsync(timeout.Token);
        try { await process.WaitForExitAsync(timeout.Token); }
        catch { try { process.Kill(true); } catch { } throw; }
        var text = await output; await error;
        if (process.ExitCode != 0 && !allowedExitCodes.Contains(process.ExitCode)) throw new IOException("Configuration command failed, exit code " + process.ExitCode + ". Check permissions and tool configuration.");
        return text;
    }
}
