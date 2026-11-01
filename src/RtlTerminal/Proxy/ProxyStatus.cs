namespace RtlTerminal.Proxy;

public enum ProxyConfigurationStatus { Enabled, NotConfigured, Partial, Error }
public sealed record ProxyStatusGroup(string Name, IReadOnlyList<KeyValuePair<string, string>> Values);
public sealed record ProxyStatusSnapshot(int Index, ProxyConfigurationStatus Status, string Address, IReadOnlyList<ProxyStatusGroup> Groups);

/// <summary>Read-only dashboard projection. A configured endpoint is not a network connectivity test.</summary>
public sealed class ProxyStatusReader(IProxyProvider user, IProxyProvider machine, IProxyProvider process,
    IProxyProvider windows, Func<CancellationToken, Task<(ProxyState State, string Scope)>> winHttp)
{
    public async Task RefreshAsync(Action<ProxyStatusSnapshot> publish, CancellationToken ct)
    {
        // Provider APIs can include synchronous registry/native work: always enter on a worker.
        await Task.WhenAll(Task.Run(() => EnvironmentAsync(publish, ct), ct), Task.Run(() => WindowsAsync(publish, ct), ct), Task.Run(() => WinHttpAsync(publish, ct), ct));
    }
    private static string Safe(string? value) => string.IsNullOrWhiteSpace(value) ? "Not set" : ProxyDiagnostics.Sanitize(value);
    private static ProxyStatusGroup Group(string name, IEnumerable<KeyValuePair<string, string?>> values) => new(name, values.Select(p => KeyValuePair.Create(p.Key, Safe(p.Value))).ToArray());
    private static bool Endpoint(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return false;
        return Uri.TryCreate(value.Contains("://") ? value : "http://" + value, UriKind.Absolute, out var uri)
            && uri.Scheme is "http" or "https" or "socks5" or "socks5h" && !string.IsNullOrEmpty(uri.Host) && uri.Port is >= 1 and <= 65535;
    }
    private static bool Server(string? value) => !string.IsNullOrWhiteSpace(value) && value.Split(';', StringSplitOptions.RemoveEmptyEntries).Length > 0 && value.Split(';', StringSplitOptions.RemoveEmptyEntries)
        .All(v => Endpoint(v.Contains('=') ? v[(v.IndexOf('=') + 1)..].Trim() : v.Trim()));
    private static bool Pac(string? value) => Uri.TryCreate(value, UriKind.Absolute, out var uri) && uri.Scheme is "http" or "https" or "file";
    private static string Address(string? value) => string.IsNullOrWhiteSpace(value) ? "" : ProxyDiagnostics.Sanitize(value);
    private async Task EnvironmentAsync(Action<ProxyStatusSnapshot> publish, CancellationToken ct)
    {
        var groups = new List<ProxyStatusGroup>(); var readings = new List<(string Scope, ProxyState State)>(); var errors = false;
        foreach (var (scope, provider) in new[] { ("User", user), ("Machine", machine), ("Process", process) })
        {
            ct.ThrowIfCancellationRequested();
            try
            {
                var state = await provider.ReadAsync(ct); readings.Add((scope, state));
                groups.Add(Group(scope, EnvironmentProxyProvider.Variables.Select(n => KeyValuePair.Create(n, state[n]))));
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
            catch (Exception ex) { errors = true; groups.Add(Group(scope, [KeyValuePair.Create<string, string?>("Error", ProxyService.SafeError(ex))])); }
        }
        var routes = EnvironmentProxyProvider.Variables.Where(v => v != "NO_PROXY").ToArray();
        var configured = readings.SelectMany(r => routes.Select(key => (r.Scope, Key: key, Value: r.State[key]))).Where(v => !string.IsNullOrWhiteSpace(v.Value)).ToArray();
        var conflicting = routes.Any(key => readings.Select(r => r.State[key]).Where(v => !string.IsNullOrWhiteSpace(v)).Distinct(StringComparer.Ordinal).Count() > 1);
        var status = errors ? ProxyConfigurationStatus.Error : configured.Length == 0 ? ProxyConfigurationStatus.NotConfigured
            : configured.Any(v => !Endpoint(v.Value)) || conflicting || configured.All(v => v.Scope == "Process") ? ProxyConfigurationStatus.Partial : ProxyConfigurationStatus.Enabled;
        ct.ThrowIfCancellationRequested(); publish(new(0, status, Address(configured.FirstOrDefault().Value), groups));
    }
    private async Task WindowsAsync(Action<ProxyStatusSnapshot> publish, CancellationToken ct)
    {
        try
        {
            var state = await windows.ReadAsync(ct);
            ct.ThrowIfCancellationRequested();
            var validFlags = int.TryParse(state["Flags"], out var flags); var manual = (flags & 2) != 0; var pac = (flags & 4) != 0; var detect = (flags & 8) != 0;
            var partial = !validFlags || manual && !Server(state["Proxy"]) || pac && !Pac(state["Pac"]) || !manual && !string.IsNullOrWhiteSpace(state["Proxy"]) || !pac && !string.IsNullOrWhiteSpace(state["Pac"]);
            var status = partial ? ProxyConfigurationStatus.Partial : manual || pac || detect ? ProxyConfigurationStatus.Enabled : ProxyConfigurationStatus.NotConfigured;
            publish(new(1, status, Address(!string.IsNullOrWhiteSpace(state["Proxy"]) ? state["Proxy"] : state["Pac"]), [Group("Windows System", new Dictionary<string, string?> {
                ["Proxy Enable"] = manual ? "Yes" : "No", ["Proxy Server"] = state["Proxy"], ["PAC URL"] = state["Pac"], ["Bypass List"] = state["Bypass"], ["Automatic Detection"] = detect ? "Yes" : "No" })]));
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
        catch (Exception ex) { publish(Failure(1, ex)); }
    }
    private async Task WinHttpAsync(Action<ProxyStatusSnapshot> publish, CancellationToken ct)
    {
        try
        {
            var (state, scope) = await winHttp(ct); var server = state["Proxy"]; var url = state["AutoconfigUrl"]; var detect = state["AutoDetect"] == "true";
            ct.ThrowIfCancellationRequested();
            var hasServer = !string.IsNullOrWhiteSpace(server); var hasPac = !string.IsNullOrWhiteSpace(url);
            var partial = hasServer && !Server(server) || hasPac && !Pac(url) || state["AutoDetect"] is not ("true" or "false");
            publish(new(2, partial ? ProxyConfigurationStatus.Partial : hasServer || hasPac || detect ? ProxyConfigurationStatus.Enabled : ProxyConfigurationStatus.NotConfigured,
                Address(hasServer ? server : url), [Group("WinHTTP", new Dictionary<string, string?> { ["Proxy"] = server, ["Proxy Bypass"] = state["ProxyBypass"], ["AutoConfig URL"] = url, ["Auto Detect"] = detect ? "Yes" : "No", ["Configuration scope"] = scope })]));
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
        catch (Exception ex) { publish(Failure(2, ex)); }
    }
    private static ProxyStatusSnapshot Failure(int index, Exception ex) => new(index, ProxyConfigurationStatus.Error, "", [Group("Details", [KeyValuePair.Create<string, string?>("Error", ProxyService.SafeError(ex))])]);
}
