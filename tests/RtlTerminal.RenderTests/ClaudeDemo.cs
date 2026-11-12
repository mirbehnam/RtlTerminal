using System.Collections;
using System.IO;
using System.Reflection;
using System.Text.Json;
using System.Windows;
using RtlTerminal;

internal static partial class Program
{
    private static void RunClaudeDemo()
    {
        var exe = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Microsoft", "WinGet", "Packages", "Anthropic.ClaudeCode_Microsoft.Winget.Source_8wekyb3d8bbwe", "claude.exe");
        var arguments = Environment.GetCommandLineArgs();
        var executableArgument = Array.IndexOf(arguments, "--claude-exe");
        if (executableArgument >= 0) exe = Path.GetFullPath(arguments[executableArgument + 1]);
        if (!File.Exists(exe)) throw new FileNotFoundException("Claude Code is not installed at the expected location.", exe);
        var root = Path.GetFullPath(Path.Combine("artifacts", "claude-interactive-demo-mixed"));
        Directory.CreateDirectory(root);
        var config = Path.Combine(root, "config"); Directory.CreateDirectory(config);
        const string key = "sk-ant-api03-local-ui-test-only";
        File.WriteAllText(Path.Combine(config, ".claude.json"), JsonSerializer.Serialize(new
        {
            hasCompletedOnboarding = true, theme = "dark",
            customApiKeyResponses = new { approved = new[] { key[^20..] }, rejected = Array.Empty<string>() },
            projects = new Dictionary<string, object> { [root] = new { hasTrustDialogAccepted = true, projectOnboardingSeenCount = 1 } }
        }));
        var mcp = Path.Combine(root, "mcp.json"); File.WriteAllText(mcp, "{\"mcpServers\":{}}");
        File.WriteAllText(Path.Combine(root, "README.txt"), "LOCAL CLAUDE UI DEMO\nResponses are predefined fixtures from localhost, not a real model. No subscription or model download is required.\nType Persian, English, mixed text or code and press Enter. Every response is the same mixed-language test fixture.\nTools and MCP are disabled. Close all demo terminal windows to stop the local server.\n");
        var responseFile = Path.Combine(root, "response.txt");
        if (!File.Exists(responseFile)) File.WriteAllText(responseFile, ClaudeMockServer.DefaultText);
        using var mock = new ClaudeMockServer(responseFile);
        File.WriteAllText(Path.Combine(root, "demo.json"), JsonSerializer.Serialize(new { processId = Environment.ProcessId, endpoint = mock.Address, mode = "local-test-fixtures" }));
        var app = new Application { ShutdownMode = ShutdownMode.OnLastWindowClose };
        AppTheme.Apply(AppSettings.LoadTheme(), ColorScheme.Find(AppSettings.LoadColorScheme()));
        var window = new MainWindow(new DirectoryHistoryStore(@"Software\RtlTerminal.Tests\InteractiveClaudeHistory"));
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
        typeof(MainWindow).GetField("_receivingDetachedTab", flags)!.SetValue(window, true);
        var profileType = typeof(MainWindow).GetNestedType("TerminalProfile", BindingFlags.NonPublic)!;
        var pane = typeof(MainWindow).GetMethod("CreatePane", flags)!.Invoke(window, new[] { Enum.Parse(profileType, "CommandPrompt") })!;
        var tab = Activator.CreateInstance(typeof(MainWindow).GetNestedType("TerminalTab", BindingFlags.NonPublic)!, pane)!;
        tab.GetType().GetProperty("CustomTitle")!.SetValue(tab, executableArgument >= 0 ? "Claude Code — INPUT + RESPONSE FIX" : "Claude Code — MIXED RTL TEST");
        ((IList)typeof(MainWindow).GetField("_tabs", flags)!.GetValue(window)!).Add(tab);
        typeof(MainWindow).GetMethod("ShowTab", flags)!.Invoke(window, new[] { tab });
        using var session = new ConPtySession(100, 30);
        var variables = new Dictionary<string, string?> { ["CLAUDE_CONFIG_DIR"] = config, ["ANTHROPIC_BASE_URL"] = mock.Address, ["ANTHROPIC_API_KEY"] = key, ["ANTHROPIC_AUTH_TOKEN"] = null, ["CLAUDE_CODE_DISABLE_NONESSENTIAL_TRAFFIC"] = "1", ["DISABLE_AUTOUPDATER"] = "1" };
        var previous = variables.Keys.ToDictionary(k => k, Environment.GetEnvironmentVariable);
        try
        {
            foreach (var v in variables) Environment.SetEnvironmentVariable(v.Key, v.Value);
            var command = ShellPath.WindowsArgument(exe) + " --tools \"\" --strict-mcp-config --mcp-config " + ShellPath.WindowsArgument(mcp) + " --setting-sources \"\" --no-chrome";
            if (Environment.GetCommandLineArgs().Contains("--cmd"))
                command = ShellPath.WindowsArgument(Path.Combine(Environment.SystemDirectory, "cmd.exe")) + " /d /s /k \"" + command + "\"";
            session.Start(command, root);
        }
        finally { foreach (var v in previous) Environment.SetEnvironmentVariable(v.Key, v.Value); }
        void Set(string property, object value) => pane.GetType().GetProperty(property)!.SetValue(pane, value);
        Set("Session", session); Set("Buffer", new TerminalBuffer(100, 30) { ClaudeLayout = true }); Set("CancellationTokenSource", new CancellationTokenSource());
        var reader = Task.Run(() => typeof(MainWindow).GetMethod("ReadOutputLoop", flags)!.Invoke(window, new[] { pane }));
        window.Closed += (_, _) => File.WriteAllText(Path.Combine(root, "last-session.txt"), "Local mock requests: " + mock.Requests);
        app.Run(window);
        session.Dispose();
        if (!reader.Wait(TimeSpan.FromSeconds(5))) throw new TimeoutException("Demo reader did not finish.");
    }
}
