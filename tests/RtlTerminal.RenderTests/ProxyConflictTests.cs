using RtlTerminal.Proxy;
using System.IO;

internal static partial class Program
{
    private static void CheckProxyConflictSemantics()
    {
        static void Check(bool condition, string message) { if (!condition) throw new Exception(message); }
        foreach (var clear in new[] { false, true })
        {
            var shared = new SharedProxyState();
            var windows = new CoupledProxy("Windows", shared); var winhttp = new CoupledProxy("WinHTTP", shared);
            var endpoint = new ProxyEndpoint("HTTP", "127.0.0.1", 10808, "");
            var service = new ProxyService(Path.Combine(Environment.CurrentDirectory, "artifacts", "proxy-coupled-tests-" + Guid.NewGuid().ToString("N")));
            var results = service.ExecuteAsync([windows, winhttp], endpoint, clear, default).GetAwaiter().GetResult();
            Check(results.All(r => r.Success) && windows.Writes == 1 && winhttp.Writes == 0, "Coupled Windows/WinHTTP desired state was rejected or rewritten");
            var originalPath = service.LastBackupPath!;
            var preview = new IProxyProvider[] { windows, winhttp }.ToDictionary(p => p.Name, p => p.ReadAsync(default).Result.Signature);
            var restored = service.RestoreHistoryAsync(originalPath, [windows, winhttp], preview, default).GetAwaiter().GetResult();
            Check(restored.All(r => r.Success) && shared.Value == "old" && winhttp.Writes == 0, "Coupled restore could not verify an already-restored desired state");

            shared.Value = "old"; windows.Writes = 0; winhttp.Writes = 0;
            var external = new OperationProgress(update =>
            {
                if (update.Provider == windows.Name && update.Result?.Success == true) shared.Value = "external unexpected change";
            });
            var conflict = service.ExecuteAsync([windows, winhttp], endpoint, clear, default, progress: external).GetAwaiter().GetResult();
            Check(conflict[0].Success && conflict[1].Outcome == ProxyOutcome.Conflict && winhttp.Writes == 0 && shared.Value == "external unexpected change", "Real concurrent change was overwritten or reported successful");

            shared.Value = "old"; winhttp.Reads = 0; winhttp.ChangeOnThirdRead = true;
            var unstable = service.ExecuteAsync([windows, winhttp], endpoint, clear, default).GetAwaiter().GetResult();
            Check(!unstable[1].Success && unstable[1].Outcome == ProxyOutcome.Conflict && winhttp.Writes == 0, "An unstable already-matching state received a success result");
        }
        Console.WriteLine("PASS coupled Windows/WinHTTP Set/Clear/restore, no-op read-back verification, genuine external conflicts and changes during verification");
    }
    private sealed class SharedProxyState { public string Value = "old"; }
    private sealed class CoupledProxy(string name, SharedProxyState shared) : IProxyProvider
    {
        public string Name => name;
        public int Writes, Reads; public bool ChangeOnThirdRead;
        public void Validate(ProxyEndpoint e) => e.Validate();
        public Task<ProxyState> ReadAsync(CancellationToken ct)
        {
            Reads++;
            if (ChangeOnThirdRead && Reads == 3) shared.Value = "external change during verification";
            return Task.FromResult(new ProxyState(new() { ["Proxy"] = shared.Value }));
        }
        public Task WriteAsync(ProxyState state, CancellationToken ct) { Writes++; shared.Value = state["Proxy"]!; return Task.CompletedTask; }
        public ProxyState Desired(ProxyEndpoint e, ProxyState old, bool clear) => new(new() { ["Proxy"] = clear ? "" : e.Url });
    }
    private static void CheckWinHttpConflictProbe()
    {
        var provider = new WinHttpProxyProvider();
        var latest = new ProxyHistoryStore().ListAsync(default).GetAwaiter().GetResult().FirstOrDefault();
        if (latest == null) throw new Exception("No history snapshot found.");
        var saved = latest.Snapshot.Entries.Single(e => e.Provider.Name == provider.Name).State;
        var current = provider.ReadAsync(default).GetAwaiter().GetResult();
        var clear = provider.Desired(new("HTTP", "127.0.0.1", 10808, ""), saved, true);
        Console.WriteLine("WinHTTP changed field names since latest snapshot: " + string.Join(", ", saved.Values.Keys.Where(k => saved[k] != current[k])));
        Console.WriteLine("Current WinHTTP matches requested clear: " + (current.Signature == clear.Signature));
        var verifiedAgain = provider.ReadAsync(default).GetAwaiter().GetResult();
        Console.WriteLine("Already-cleared WinHTTP verified by second read: " + (current.Signature == clear.Signature && verifiedAgain.Signature == clear.Signature));
        var windows = new WindowsProxyProvider().ReadAsync(default).GetAwaiter().GetResult();
        Console.WriteLine("Current WinHTTP mirrors current Windows settings: " +
            (current["Proxy"] == windows["Proxy"] && current["ProxyBypass"] == windows["Bypass"] && current["AutoconfigUrl"] == windows["Pac"] &&
             (current["AutoDetect"] == "true") == ((int.Parse(windows["Flags"]!) & 8) != 0)));
    }
}
