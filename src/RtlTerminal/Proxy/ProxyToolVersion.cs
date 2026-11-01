using System.IO;
using System.Text.RegularExpressions;
using System.Text.Json;

namespace RtlTerminal.Proxy;

public sealed record ProxyToolCapabilities(string Version, int Major, string[] Protocols, string Method, string Scope);
public static class ProxyToolVersion
{
    public static async Task<ProxyToolCapabilities?> DetectAsync(string name, string executable, CancellationToken ct)
    {
        try
        {
            var versionArg = name == "Maven" ? "-v" : "--version";
            string text;
            if (Path.GetExtension(executable).Equals(".exe", StringComparison.OrdinalIgnoreCase)) text = await ProxyProcess.Run(executable, [versionArg], ct);
            else
            {
                var package = name switch { "npm" => "npm", "pnpm" => "pnpm", "Yarn" => "yarn", "Cypress" => "cypress", _ => null };
                var manifest = package == null ? null : Path.Combine(Path.GetDirectoryName(executable)!, "node_modules", package, "package.json");
                if (manifest != null && File.Exists(manifest))
                {
                    using var json = JsonDocument.Parse(await File.ReadAllTextAsync(manifest, ct));
                    if (json.RootElement.GetProperty("name").GetString() != package) return null;
                    text = json.RootElement.GetProperty("version").GetString() ?? "";
                }
                else
                {
                // Fixed version argument only. Refuse shell metacharacters, expansions
                // and embedded quotes in shim paths instead of interpolating unsafe paths.
                if (executable.IndexOfAny(['"', '%', '!', '^', '&', '|', '<', '>', '\r', '\n']) >= 0) return null;
                text = await ProxyProcess.ShimVersion(executable, versionArg, ct);
                }
            }
            var match = Regex.Match(text, @"(?<!\d)(\d+)\.(\d+)(?:\.(\d+))?");
            if (!match.Success || !int.TryParse(match.Groups[1].Value, out var major)) return null;
            // Conservative adapter compatibility ranges. Future formats stay visible
            // but read-only until their adapter has been reviewed.
            var supported = name switch
            {
                "Git" => major == 2,
                "npm" => major is >= 6 and <= 11,
                "pnpm" => major is >= 6 and <= 10,
                "Python pip" => major is >= 20 and <= 26,
                "Gradle" => major is >= 6 and <= 9,
                "Maven" => major == 3,
                "NuGet / .NET" => major is >= 6 and <= 10,
                "Cargo" => major == 1,
                _ => false
            };
            return new(match.Value, major, name == "Git" ? ["HTTP", "HTTPS", "SOCKS5"] : ["HTTP"], supported ? "Verified adapter" : "Unverified / inherited", "User");
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested) { return null; }
        catch (IOException) { return null; }
        catch (System.ComponentModel.Win32Exception) { return null; }
        catch (JsonException) { return null; }
    }
}

public sealed class VersionedProxyProvider(IProxyProvider inner, string executable, ProxyToolCapabilities capabilities) : IProxyProvider
{
    public string Name => inner.Name;
    public ProxyDescriptor Descriptor => inner.Descriptor with { Version = capabilities.Version, Location = executable + " | " + inner.Descriptor.Location };
    public Task<ProxyState> ReadAsync(CancellationToken ct) => inner.ReadAsync(ct);
    public void Validate(ProxyEndpoint endpoint)
    {
        EnsureCompatible();
        if (!capabilities.Protocols.Contains(endpoint.Protocol)) throw new NotSupportedException(Name + " does not support the selected proxy protocol.");
        inner.Validate(endpoint);
    }
    private void EnsureCompatible() { if (capabilities.Method != "Verified adapter") throw new NotSupportedException(Name + ": adapter version has not been verified."); }
    public ProxyState Desired(ProxyEndpoint e, ProxyState previous, bool clear) { EnsureCompatible(); return inner.Desired(e, previous, clear); }
    public Task WriteAsync(ProxyState state, CancellationToken ct) { EnsureCompatible(); return inner.WriteAsync(state, ct); }
}
