using RtlTerminal.Proxy;
using System.IO;

internal static partial class Program
{
    private static void CheckMachineProxy()
    {
        static void Check(bool value, string message) { if (!value) throw new Exception(message); }
        var values = EnvironmentProxyProvider.Variables.ToDictionary(k => k, _ => (string?)null);
        ProxyState Read() => new(new(values));
        var writer = new FakeMachineWriter(Read, (key, value) => values[key] = value);
        var machine = new MachineEnvironmentProxyProvider(Read) { Writer = writer };
        var service = new ProxyService(Path.Combine(Environment.CurrentDirectory, "artifacts", "machine-proxy-tests-" + Guid.NewGuid().ToString("N")));
        var endpoint = new ProxyEndpoint("HTTP", "127.0.0.1", 10808, "");
        var set = service.ExecuteAsync([machine], endpoint, false, default).GetAwaiter().GetResult();
        Check(set.Single().Success && values["HTTP_PROXY"] == endpoint.Url && values["NO_PROXY"]!.Contains("localhost"), "Machine proxy variables did not set/verify");
        var setBackup = service.LastBackupPath!;
        var clear = service.ExecuteAsync([machine], endpoint, true, default).GetAwaiter().GetResult();
        Check(clear.Single().Success && values.Values.All(v => v == null), "Machine proxy clear did not remove/verify all managed values");
        var restore = service.RestoreHistoryAsync(setBackup, [machine], new() { [machine.Name] = Read().Signature }, default).GetAwaiter().GetResult();
        Check(restore.Single().Success && values.Values.All(v => v == null), "Machine snapshot restoration did not preserve original absence");
        machine.Writer = null;
        var denied = service.ExecuteAsync([machine], endpoint, false, default).GetAwaiter().GetResult();
        Check(denied.Single().Outcome == ProxyOutcome.RequiresPermission && values.Values.All(v => v == null), "Machine mutation proceeded without elevation writer");
        try { MachineProxyElevation.ValidateRequest(new(new() { ["PATH"] = "unsafe" })); throw new Exception("Helper accepted an arbitrary environment variable"); } catch (ArgumentException) { }
        var before = Read(); values["HTTP_PROXY"] = "external change";
        try { MachineProxyElevation.ApplyVerifiedEnvironment(before, machine.Desired(endpoint, before, false), Read, (key, value) => values[key] = value); throw new Exception("External Machine change was overwritten"); } catch (IOException) { }
        Check(values["HTTP_PROXY"] == "external change", "Helper did not preserve concurrent changes");
        Console.WriteLine("PASS Machine proxy set/clear/absence restore with fake privileged storage, permission refusal, allowlist and concurrent-change protection");
    }
    private sealed class FakeMachineWriter(Func<ProxyState> read, Action<string, string?> set) : IMachineProxyWriter
    {
        public Task WriteAsync(ProxyState before, ProxyState desired, CancellationToken ct)
        { MachineProxyElevation.ApplyVerifiedEnvironment(before, desired, read, set); return Task.CompletedTask; }
    }
}
