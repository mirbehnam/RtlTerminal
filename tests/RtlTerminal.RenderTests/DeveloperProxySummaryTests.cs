using System.Text;
using RtlTerminal.Proxy;

internal static partial class Program
{
    private static void CheckDeveloperProxySummaries()
    {
        static ProxyState State(string text) => new(new() { ["Content"] = Convert.ToBase64String(Encoding.UTF8.GetBytes(text)) });
        var fixtures = new (string Tool, string Content, string Expected)[]
        {
            ("Git", "[http]\nproxy = http://user:secret@127.0.0.1:10808\n[remote \"private\"]\nurl=https://secret.test", "http://127.0.0.1:10808"),
            ("npm", "# proxy=http://wrong:80\nproxy=http://proxy.test:3128\nhttps-proxy=http://proxy.test:3128", "http://proxy.test:3128"),
            ("pnpm", "https-proxy=http://proxy.test:3128", "http://proxy.test:3128"),
            ("Python pip", "[global]\nproxy=http://proxy.test:80\n[install]\nindex-url=https://private.test", "http://proxy.test:80"),
            ("Cargo", "[http]\nproxy=\"socks5h://[::1]:1080\"\n", "socks5h://[::1]:1080"),
            ("Gradle", "systemProp.http.proxyHost=proxy.test\nsystemProp.http.proxyPort=3128\n", "http://proxy.test:3128"),
            ("Maven", "<settings xmlns=\"urn:test\"><proxies><proxy><active>false</active><host>inactive.test</host><port>80</port></proxy><proxy><host>proxy.test</host><port>3128</port></proxy></proxies></settings>", "http://proxy.test:3128"),
            ("NuGet / .NET", "<configuration><config><add key=\"http_proxy\" value=\"http://user:secret@proxy.test:3128/private?token=secret\"/></config></configuration>", "http://proxy.test:3128")
        };
        foreach (var (tool, content, expected) in fixtures)
        {
            var addresses = DeveloperProxySummary.Parse(tool, State(content));
            if (addresses.Length != 1 || addresses[0] != expected) throw new Exception("Proxy card summary failed: " + tool);
        }
        if (DeveloperProxySummary.Parse("Git", State("[remote \"origin\"]\nurl=https://private.test")).Length != 0 ||
            DeveloperProxySummary.Parse("Cargo", new(new() { ["Content"] = null })).Length != 0)
            throw new Exception("Unconfigured tool falsely displayed a proxy");
        var malformed = new SummaryProvider(State("<configuration>"));
        var toolState = DeveloperProxySummary.ReadAsync(new("NuGet / .NET", "fixture", malformed, "fixture"), default).Result;
        if (!toolState.ProxyReadFailed || toolState.ProxyAddresses.Length != 0) throw new Exception("Malformed tool file was not reported as unavailable");
        var inherited = DeveloperProxySummary.ReadAsync(new("Codex CLI", "fixture", null, "Inherited"), default).Result;
        if (inherited.ProxyAddresses.Length != 0) throw new Exception("Inherited tool invented an independent proxy");
        Console.WriteLine("PASS developer proxy cards: eight adapters, duplicate endpoints, active Maven only, absent/malformed files, inherited tools, IPv6, and credential/path redaction");
    }
    private sealed class SummaryProvider(ProxyState state) : IProxyProvider
    {
        public string Name => "fixture";
        public Task<ProxyState> ReadAsync(CancellationToken ct) => Task.FromResult(state);
        public void Validate(ProxyEndpoint endpoint) => throw new NotSupportedException();
        public ProxyState Desired(ProxyEndpoint endpoint, ProxyState before, bool clear) => throw new NotSupportedException();
        public Task WriteAsync(ProxyState after, CancellationToken ct) => throw new NotSupportedException();
    }
}
