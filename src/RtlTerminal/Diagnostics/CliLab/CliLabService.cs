using System.Diagnostics;
using System.IO;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace RtlTerminal.CliLab;

internal enum CliLabTool { Claude, OpenCode, MiMo }
internal sealed record CliLabInstallation(CliLabTool Tool, string Path, Version? Version, string? UnavailableReason)
{
    public bool Supported => Version != null && UnavailableReason == null;
    public string Label => $"{Tool}  ·  {Version?.ToString() ?? "unknown version"}" + (Supported ? "" : "  ·  unavailable");
}

internal static class CliLabCatalog
{
    public static IEnumerable<(CliLabTool Tool, string Path)> Candidates()
    {
        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        var local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var roaming = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        var known = new List<(CliLabTool, string)> {
            (CliLabTool.Claude, System.IO.Path.Combine(local, "Microsoft/WinGet/Packages/Anthropic.ClaudeCode_Microsoft.Winget.Source_8wekyb3d8bbwe/claude.exe")),
            (CliLabTool.Claude, System.IO.Path.Combine(home, ".local/bin/claude.exe")),
            (CliLabTool.OpenCode, System.IO.Path.Combine(roaming, "npm/node_modules/@opencode/cli/bin/opencode.exe")),
            (CliLabTool.OpenCode, System.IO.Path.Combine(home, ".opencode/bin/opencode.exe")),
            (CliLabTool.MiMo, System.IO.Path.Combine(home, ".mimocode/bin/mimo.exe")) };
        foreach (var entry in (Environment.GetEnvironmentVariable("PATH") ?? "").Split(';', StringSplitOptions.RemoveEmptyEntries))
            foreach (var (tool, name) in new[] { (CliLabTool.Claude, "claude.exe"), (CliLabTool.OpenCode, "opencode.exe"), (CliLabTool.OpenCode, "opencode2.exe"), (CliLabTool.MiMo, "mimo.exe") })
                try { known.Add((tool, System.IO.Path.Combine(entry.Trim('"'), name))); } catch (ArgumentException) { }
        foreach (var root in new[] { System.IO.Path.Combine(CliLabSession.StorageRoot, "versions"), System.IO.Path.Combine(Environment.CurrentDirectory, "artifacts") })
            if (System.IO.Directory.Exists(root))
                foreach (var folder in System.IO.Directory.EnumerateDirectories(root).Take(100))
                    foreach (var (tool, name) in new[] { (CliLabTool.Claude, "claude.exe"), (CliLabTool.OpenCode, "opencode.exe"), (CliLabTool.MiMo, "mimo.exe") })
                        known.Add((tool, System.IO.Path.Combine(folder, name)));
        return known.Where(item => File.Exists(item.Item2)).DistinctBy(item => item.Item2, StringComparer.OrdinalIgnoreCase);
    }
    public static Task<CliLabInstallation> InspectAsync(CliLabTool tool, string path, CancellationToken ct) => Task.Run(() => InspectCoreAsync(tool, path, ct), ct);
    private static async Task<CliLabInstallation> InspectCoreAsync(CliLabTool tool, string path, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        path = System.IO.Path.GetFullPath(path);
        if (!File.Exists(path) || !path.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)) return new(tool, path, null, "Choose an installed native .exe file.");
        try
        {
            var versionText = await ReadAsync(path, "--version", ct);
            var match = Regex.Match(versionText, @"(?<!\d)\d+\.\d+\.\d+(?!\d)", RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(100));
            if (!match.Success || !Version.TryParse(match.Value, out var version)) return new(tool, path, null, "Version could not be verified. This executable will not be launched.");
            var help = await ReadAsync(path, "--help", ct);
            var supported = tool switch {
                CliLabTool.Claude => version.Major == 2 && help.Contains("--strict-mcp-config", StringComparison.Ordinal) && help.Contains("--setting-sources", StringComparison.Ordinal) && help.Contains("--tools", StringComparison.Ordinal),
                CliLabTool.OpenCode => (version.Major == 1 || version.Major == 2) && (version.Major == 1 || help.Contains("--standalone", StringComparison.Ordinal)),
                CliLabTool.MiMo => version.Major == 0 && version.Minor == 1 && help.Contains("--pure", StringComparison.Ordinal),
                _ => false };
            return new(tool, path, version, supported ? null : "No verified local-test adapter for this version or command-line interface.");
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
        catch (Exception e) when (e is IOException or System.ComponentModel.Win32Exception or OperationCanceledException or InvalidOperationException)
        { return new(tool, path, null, "The executable could not be inspected or did not respond within 8 seconds."); }
    }
    private static async Task<string> ReadAsync(string path, string argument, CancellationToken ct)
    {
        var info = new ProcessStartInfo(path) { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true };
        var scratch = Path.Combine(Path.GetTempPath(), "RtlTerminal", "CliLabInspection", Guid.NewGuid().ToString("N"));
        info.Environment.Clear();
        foreach (var pair in CliLabSession.CreateEnvironment(scratch)) info.Environment[pair.Key] = pair.Value;
        foreach (var prefix in new[] { "OPENCODE", "MIMOCODE" })
            foreach (var suffix in new[] { "DISABLE_AUTOUPDATE", "DISABLE_MODELS_FETCH", "DISABLE_PROJECT_CONFIG", "DISABLE_DEFAULT_PLUGINS" }) info.Environment[prefix + "_" + suffix] = "1";
        info.Environment["CLAUDE_CODE_DISABLE_NONESSENTIAL_TRAFFIC"] = "1"; info.Environment["DISABLE_AUTOUPDATER"] = "1";
        info.WorkingDirectory = scratch;
        info.ArgumentList.Add(argument);
        using var process = Process.Start(info) ?? throw new IOException("Cannot inspect executable.");
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(ct); deadline.CancelAfter(TimeSpan.FromSeconds(8));
        var output = process.StandardOutput.ReadToEndAsync(deadline.Token);
        var errors = process.StandardError.ReadToEndAsync(deadline.Token);
        try { await process.WaitForExitAsync(deadline.Token); return await output + await errors; }
        finally { if (!process.HasExited) { try { process.Kill(entireProcessTree: true); } catch (InvalidOperationException) { } } }
    }
}

internal sealed class CliLabSession : IDisposable
{
    public string Directory { get; }
    public string Command { get; }
    public Dictionary<string, string> Environment { get; }
    public LocalCliServer Server { get; }
    private int disposed;
    public static string StorageRoot => Path.Combine(System.Environment.GetFolderPath(System.Environment.SpecialFolder.LocalApplicationData), "RtlTerminal", "CliTestLab");

    public CliLabSession(CliLabInstallation installation, string response, string? storageRoot = null)
    {
        if (!installation.Supported) throw new NotSupportedException(installation.UnavailableReason ?? "This version is not supported.");
        Directory = Path.Combine(storageRoot ?? StorageRoot, "sessions", DateTime.UtcNow.ToString("yyyyMMdd-HHmmss") + "-" + Guid.NewGuid().ToString("N"));
        System.IO.Directory.CreateDirectory(Directory);
        Environment = CreateEnvironment(Directory);
        Server = new LocalCliServer(response);
        try
        {
            var workspace = Path.Combine(Directory, "workspace"); System.IO.Directory.CreateDirectory(workspace);
            File.WriteAllText(Path.Combine(workspace, "README.txt"), "RTL TERMINAL LOCAL CLI TEST\nResponses are fixed fixtures from localhost, not a real model. No account or subscription is used.\nThis workspace and CLI history are disposable test data. Do not enter secrets.\n");
            File.WriteAllText(Path.Combine(Directory, "session.json"), JsonSerializer.Serialize(new { tool = installation.Tool.ToString(), version = installation.Version?.ToString(), executable = installation.Path, endpoint = Server.Address, created = DateTimeOffset.UtcNow, mode = "local-fixture" }));
            const string key = "sk-ant-api03-local-ui-test-only";
            var config = Path.Combine(Directory, "config"); System.IO.Directory.CreateDirectory(config);
            if (installation.Tool == CliLabTool.Claude)
            {
                File.WriteAllText(Path.Combine(config, ".claude.json"), JsonSerializer.Serialize(new {
                    hasCompletedOnboarding = true, theme = AppTheme.IsLight ? "light" : "dark",
                    customApiKeyResponses = new { approved = new[] { key[^20..] }, rejected = Array.Empty<string>() },
                    projects = new Dictionary<string, object> { [workspace] = new { hasTrustDialogAccepted = true, projectOnboardingSeenCount = 1 } } }));
                var mcp = Path.Combine(config, "mcp.json"); File.WriteAllText(mcp, "{\"mcpServers\":{}}");
                Environment["CLAUDE_CONFIG_DIR"] = config; Environment["ANTHROPIC_BASE_URL"] = Server.Address; Environment["ANTHROPIC_API_KEY"] = key;
                Environment["CLAUDE_CODE_DISABLE_NONESSENTIAL_TRAFFIC"] = "1"; Environment["DISABLE_AUTOUPDATER"] = "1";
                Command = ShellPath.WindowsArgument(installation.Path) + " --tools \"\" --strict-mcp-config --mcp-config " + ShellPath.WindowsArgument(mcp) + " --setting-sources \"\" --no-chrome";
            }
            else
            {
                var isMimo = installation.Tool == CliLabTool.MiMo;
                var prefix = isMimo ? "MIMOCODE" : "OPENCODE";
                var legacy = isMimo || installation.Version!.Major == 1;
                var model = new { name = "RTL local test", limit = new { context = 128000, output = 8192 }, cost = new { input = 0, output = 0 } };
                object provider = legacy
                    ? new { name = "Local fixture only", npm = "@ai-sdk/openai-compatible", options = new { baseURL = Server.Address + "/v1", apiKey = "rtl-local-test-only" }, models = new Dictionary<string, object> { ["rtl-local"] = model } }
                    : new { name = "Local fixture only", package = "@opencode/ai/providers/openai-compatible", settings = new { baseURL = Server.Address + "/v1", apiKey = "rtl-local-test-only" }, models = new Dictionary<string, object> { ["rtl-local"] = model } };
                var settings = new Dictionary<string, object> {
                    [legacy ? "provider" : "providers"] = new Dictionary<string, object> { ["rtl-local"] = provider },
                    ["model"] = "rtl-local/rtl-local", ["small_model"] = "rtl-local/rtl-local",
                    ["enabled_providers"] = new[] { "rtl-local" }, ["permission"] = new Dictionary<string, string> { ["*"] = "deny" },
                    ["mcp"] = new Dictionary<string, object>(), ["plugin"] = Array.Empty<string>() };
                if (!legacy) { settings.Remove("plugin"); settings.Remove("small_model"); settings["plugins"] = new[] { "-opencode.provider.openai-context-caps" }; }
                var path = Path.Combine(config, isMimo ? "mimocode.json" : "opencode.json");
                File.WriteAllText(path, JsonSerializer.Serialize(settings));
                Environment[prefix + "_CONFIG"] = path; Environment[prefix + "_CONFIG_DIR"] = config;
                foreach (var suffix in new[] { "DISABLE_AUTOUPDATE", "DISABLE_MODELS_FETCH", "DISABLE_PROJECT_CONFIG", "DISABLE_DEFAULT_PLUGINS", "DISABLE_LSP_DOWNLOAD", "DISABLE_CLAUDE_CODE", "DISABLE_PROVIDER_ENV", "DISABLE_AGENTS_SKILLS", "DISABLE_SLASH_SKILLS", "DISABLE_BUILTIN_SKILLS", "DISABLE_OFFICIAL_SKILLS", "DISABLE_GIT" }) Environment[prefix + "_" + suffix] = "1";
                if (isMimo) Environment["MIMOCODE_HOME"] = Path.Combine(Directory, "mimocode");
                Command = ShellPath.WindowsArgument(installation.Path) + (isMimo ? " --pure --trust" : installation.Version!.Major >= 2 ? " --standalone" : "");
            }
        }
        catch { Server.Dispose(); throw; }
    }
    public string Workspace => Path.Combine(Directory, "workspace");
    internal static Dictionary<string, string> CreateEnvironment(string root)
    {
        var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        // A whitelist prevents accidental inheritance of provider keys, proxies,
        // CLI feature flags, credential-file locations and real account settings.
        foreach (var name in new[] { "PATH", "SystemRoot", "WINDIR", "ComSpec", "PATHEXT", "PROCESSOR_ARCHITECTURE", "NUMBER_OF_PROCESSORS" })
            if (System.Environment.GetEnvironmentVariable(name) is { } value) values[name] = value;
        foreach (var (name, folder) in new[] { ("HOME", "home"), ("USERPROFILE", "home"), ("APPDATA", "roaming"), ("LOCALAPPDATA", "local"), ("TEMP", "temp"), ("TMP", "temp"), ("XDG_CONFIG_HOME", "xdg/config"), ("XDG_DATA_HOME", "xdg/data"), ("XDG_CACHE_HOME", "xdg/cache"), ("XDG_STATE_HOME", "xdg/state") })
        { values[name] = Path.Combine(root, folder); System.IO.Directory.CreateDirectory(values[name]); }
        values["NO_PROXY"] = "localhost,127.0.0.1,::1";
        values["DO_NOT_TRACK"] = "1"; values["OTEL_SDK_DISABLED"] = "true";
        values["GIT_CONFIG_NOSYSTEM"] = "1"; values["GIT_CONFIG_GLOBAL"] = Path.Combine(root, "empty.gitconfig"); File.WriteAllText(values["GIT_CONFIG_GLOBAL"], "");
        return values;
    }
    public void Dispose() { if (Interlocked.Exchange(ref disposed, 1) == 0) Server.Dispose(); }
}
