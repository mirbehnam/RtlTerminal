using System.IO;
using System.Text;
using System.Text.RegularExpressions;

namespace RtlTerminal.Proxy;

public sealed record DetectedProxyTool(string Name, string Executable, IProxyProvider? Provider, string Status)
{
    public ProxyToolCapabilities? Capabilities { get; init; }
}

public sealed class DeveloperToolDetectionService
{
    private static List<DetectedProxyTool>? cache;
    private static DateTime cachedAt;
    public async Task DetectAsync(Action<DetectedProxyTool> discovered, bool refresh, CancellationToken ct)
    {
        if (!refresh && cache is { } saved && DateTime.UtcNow - cachedAt < TimeSpan.FromMinutes(2))
        { foreach (var tool in saved) { ct.ThrowIfCancellationRequested(); discovered(tool); } return; }
        var result = new List<DetectedProxyTool>();
        await Task.Run(async () =>
        {
            var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            var app = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            var local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            var known = new[] { Path.Combine(home, ".cargo", "bin"), Path.Combine(home, ".local", "bin"), Path.Combine(app, "npm"),
                Path.Combine(local, "Programs", "Python", "Python313", "Scripts"), Path.Combine(home, "scoop", "shims"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Git", "cmd"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Docker", "Docker", "resources", "bin") };
            var dirs = (Environment.GetEnvironmentVariable("PATH") ?? "").Split(Path.PathSeparator).Concat(known).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
            foreach (var (name, command) in new[] { ("Git", "git"), ("npm", "npm"), ("pnpm", "pnpm"), ("Yarn", "yarn"),
                ("Python pip", "pip"), ("uv", "uv"), ("Gradle", "gradle"), ("Maven", "mvn"), ("NuGet / .NET", "dotnet"),
                ("Cargo", "cargo"), ("Conda", "conda"), ("Docker", "docker"), ("Composer", "composer"), ("curl", "curl"),
                ("wget", "wget"), ("GitHub CLI", "gh"), ("Codex CLI", "codex"), ("Claude Code", "claude"), ("OpenCode", "opencode"), ("Cypress", "cypress") })
            {
                ct.ThrowIfCancellationRequested();
                var executable = dirs.SelectMany(d => new[] { ".exe", ".cmd", ".bat" }.Select(ext => Path.Combine(d.Trim('"'), command + ext))).FirstOrDefault(File.Exists);
                if (executable is null) continue;
                IProxyProvider? provider = name switch
                {
                    "Git" => new GitProxyProvider(executable),
                    "npm" or "pnpm" when string.IsNullOrEmpty(Environment.GetEnvironmentVariable("NPM_CONFIG_USERCONFIG")) => new ProxyFileProvider("npm / pnpm user", Path.Combine(home, ".npmrc"), "", ["proxy", "https-proxy"], false),
                    "Python pip" when string.IsNullOrEmpty(Environment.GetEnvironmentVariable("PIP_CONFIG_FILE")) => new ProxyFileProvider("pip user", Path.Combine(app, "pip", "pip.ini"), "global", ["proxy"], false),
                    "Gradle" => new ProxyFileProvider("Gradle user", Path.Combine(Environment.GetEnvironmentVariable("GRADLE_USER_HOME") ?? Path.Combine(home, ".gradle"), "gradle.properties"), "", ["systemProp.http.proxyHost", "systemProp.http.proxyPort", "systemProp.https.proxyHost", "systemProp.https.proxyPort", "systemProp.http.nonProxyHosts"], true),
                    "Cargo" => new ProxyFileProvider("Cargo user", CargoPath(home), "http", ["proxy"], false, true),
                    "Maven" => new XmlProxyProvider("Maven user", Path.Combine(home, ".m2", "settings.xml"), true),
                    "NuGet / .NET" => new XmlProxyProvider("NuGet user", Path.Combine(app, "NuGet", "NuGet.Config"), false),
                    _ => null
                };
                discovered(new DetectedProxyTool(name, executable, null, "Checking version and adapter compatibility…"));
                ProxyToolCapabilities? capability;
                using (StartupPerformance.Measure("ProxyDetection.Version." + name)) capability = await ProxyToolVersion.DetectAsync(name, executable, ct);
                if (capability?.Method != "Verified adapter") provider = null;
                else if (provider != null) provider = new VersionedProxyProvider(provider, executable, capability);
                var tool = new DetectedProxyTool(name, executable, provider, provider is null
                    ? "Detected · inherited, unsupported or unverified adapter" : "Version-verified User adapter · project/environment overrides may remain") { Capabilities = capability };
                result.Add(tool); discovered(tool);
            }
        }, ct);
        cache = result; cachedAt = DateTime.UtcNow;
    }
    private static string CargoPath(string home)
    {
        var folder = Environment.GetEnvironmentVariable("CARGO_HOME") ?? Path.Combine(home, ".cargo");
        return File.Exists(Path.Combine(folder, "config")) ? Path.Combine(folder, "config") : Path.Combine(folder, "config.toml");
    }
}

public sealed class GitProxyProvider(string executable, string? configurationPath = null) : IProxyProvider
{
    public string Name => "Git user";
    private string ConfigPath()
    {
        if (configurationPath != null) return Path.GetFullPath(configurationPath);
        var configured = Environment.GetEnvironmentVariable("GIT_CONFIG_GLOBAL");
        if (!string.IsNullOrEmpty(configured)) return Path.GetFullPath(configured);
        var home = Environment.GetEnvironmentVariable("HOME") ?? Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        var conventional = Path.Combine(home, ".gitconfig");
        var xdg = Path.Combine(Environment.GetEnvironmentVariable("XDG_CONFIG_HOME") ?? Path.Combine(home, ".config"), "git", "config");
        return File.Exists(conventional) || !File.Exists(xdg) ? conventional : xdg;
    }
    public ProxyDescriptor Descriptor => new(Name, "schema-1", ConfigPath(), "User");
    private ProxyFileProvider FileProvider => new(Name, ConfigPath(), "http", ["proxy"], false);
    public void Validate(ProxyEndpoint endpoint) => endpoint.Validate();
    public Task<ProxyState> ReadAsync(CancellationToken ct) => FileProvider.ReadAsync(ct);
    public ProxyState Desired(ProxyEndpoint endpoint, ProxyState previous, bool clear)
    {
        if (clear && previous["Content"] == null) return previous;
        var folder = Path.GetDirectoryName(ConfigPath())!;
        if (!Directory.Exists(folder)) throw new NotSupportedException("Git configuration directory does not exist; create it explicitly before using this adapter.");
        var temporary = Path.Combine(folder, ".rtl-proxy-plan-" + Guid.NewGuid().ToString("N"));
        try
        {
            using (var stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                if (previous["Content"] is { } content) stream.Write(Convert.FromBase64String(content));
            }
            var args = clear ? new[] { "config", "--file", temporary, "--unset-all", "http.proxy" }
                : new[] { "config", "--file", temporary, "--replace-all", "http.proxy", endpoint.Url };
            ProxyProcess.Run(executable, args, default, clear ? 5 : 0).GetAwaiter().GetResult();
            return new(new() { ["Content"] = Convert.ToBase64String(File.ReadAllBytes(temporary)) });
        }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
    }
    public Task WriteAsync(ProxyState state, CancellationToken ct) => FileProvider.WriteAsync(state, ct);
}

// Conservative line editor: refuses ambiguous/continued entries rather than guessing.
// File content is carried only in DPAPI-encrypted backups, never in status messages.
public sealed class ProxyFileProvider(string name, string path, string section, string[] keys, bool gradle, bool quoteValues = false) : IProxyProvider
{
    public string Name => name;
    public ProxyDescriptor Descriptor => new(Name, "schema-1", Path.GetFullPath(path), "User");
    public string ConfigurationPath => path;
    public void Validate(ProxyEndpoint e)
    {
        e.Validate();
        if (e.Protocol != "HTTP") throw new NotSupportedException(Name + " accepts HTTP endpoints in this manager.");
    }
    public async Task<ProxyState> ReadAsync(CancellationToken ct)
    {
        if (File.Exists(path) && new FileInfo(path).Length > 4 * 1024 * 1024) throw new IOException(Name + ": configuration exceeds the safe 4 MB limit.");
        return new(new() { ["Content"] = File.Exists(path) ? Convert.ToBase64String(await File.ReadAllBytesAsync(path, ct)) : null });
    }
    public ProxyState Desired(ProxyEndpoint e, ProxyState previous, bool clear)
    {
        var bytes = previous["Content"] is { } content ? Convert.FromBase64String(content) : [];
        var bom = bytes.Take(3).SequenceEqual(new byte[] { 239, 187, 191 });
        var encoding = new UTF8Encoding(false, true);
        string text;
        try { text = encoding.GetString(bytes.AsSpan(bom ? 3 : 0)); }
        catch (DecoderFallbackException) { throw new NotSupportedException(Name + ": only UTF-8 configuration files can be edited safely."); }
        var values = new Dictionary<string, string>();
        if (!clear)
        {
            if (gradle)
            {
                values[keys[0]] = e.Host; values[keys[1]] = e.Port.ToString(); values[keys[2]] = e.Host; values[keys[3]] = e.Port.ToString();
                values[keys[4]] = e.LocalBypass("|");
            }
            else foreach (var key in keys) values[key] = quoteValues ? "\"" + e.Url + "\"" : e.Url;
        }
        var updated = Edit(text, section, keys, values);
        if (clear && previous["Content"] is null) return previous;
        var output = encoding.GetBytes(updated);
        if (bom) output = new byte[] { 239, 187, 191 }.Concat(output).ToArray();
        return new(new() { ["Content"] = Convert.ToBase64String(output) });
    }
    public static string Edit(string text, string section, IReadOnlyList<string> keys, IReadOnlyDictionary<string, string> values)
    {
        var newline = text.Contains("\r\n") ? "\r\n" : "\n";
        var lines = Regex.Split(text, "(?<=\n)"); var output = new StringBuilder();
        var current = ""; var foundSection = section.Length == 0; var inserted = false;
        void Insert()
        {
            if (inserted) return;
            if (output.Length > 0 && output[^1] != '\n') output.Append(newline);
            foreach (var pair in values) output.Append(pair.Key).Append('=').Append(pair.Value).Append(newline);
            inserted = true;
        }
        foreach (var line in lines)
        {
            var trimmed = line.Trim();
            if (trimmed.StartsWith('['))
            {
                if (current.Equals(section, StringComparison.OrdinalIgnoreCase)) Insert();
                current = trimmed.Trim('[', ']'); if (current.Equals(section, StringComparison.OrdinalIgnoreCase)) foundSection = true;
            }
            var match = Regex.Match(line, @"^\s*([^#;\s=:]+)\s*[=:]");
            if (current.Equals(section, StringComparison.OrdinalIgnoreCase) && match.Success && keys.Contains(match.Groups[1].Value, StringComparer.OrdinalIgnoreCase))
            {
                if (trimmed.EndsWith('\\') || trimmed.Contains(" #") || trimmed.Contains(" ;"))
                    throw new NotSupportedException("A proxy entry uses continuation or inline comments; edit it manually to avoid data loss.");
                continue;
            }
            output.Append(line);
        }
        if (!inserted && values.Count > 0)
        {
            if (!foundSection) { if (output.Length > 0 && output[^1] != '\n') output.Append(newline); output.Append('[').Append(section).Append(']').Append(newline); }
            Insert();
        }
        return output.ToString();
    }
    public async Task WriteAsync(ProxyState state, CancellationToken ct)
    {
        if (File.Exists(path) && (File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0) throw new IOException("Refusing to replace a linked configuration file.");
        if (state["Content"] is not { } content) { if (File.Exists(path)) File.Delete(path); return; }
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var temporary = path + ".rtl-" + Guid.NewGuid().ToString("N");
        try
        {
            await File.WriteAllBytesAsync(temporary, Convert.FromBase64String(content), ct);
            if (File.Exists(path)) File.Replace(temporary, path, null);
            else File.Move(temporary, path);
        }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
    }
}
