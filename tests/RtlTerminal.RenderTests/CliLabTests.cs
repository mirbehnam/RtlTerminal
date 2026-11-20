using System.IO;
using System.Reflection;
using System.Windows;
using RtlTerminal;
#if CLI_TEST_LAB
using System.Net.Http;
using System.Text;
using System.Text.Json;
using RtlTerminal.CliLab;
#endif

internal static partial class Program
{
    private static void CheckCliLab()
    {
#if CLI_TEST_LAB
        CheckCliLabAsync().GetAwaiter().GetResult();
        if (Application.Current == null) _ = new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
        foreach (var theme in new[] { AppThemeKind.Light, AppThemeKind.Dark })
        {
            AppTheme.Apply(theme);
            var dialog = new CliTestLabWindow((_, _) => Task.CompletedTask);
            const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
            var items = (System.Collections.ObjectModel.ObservableCollection<CliLabInstallation>)typeof(CliTestLabWindow).GetField("installations", flags)!.GetValue(dialog)!;
            items.Add(new(CliLabTool.Claude, "C:/tests/claude/2.1.322/claude.exe", new Version(2, 1, 322), null));
            items.Add(new(CliLabTool.OpenCode, "C:/tests/opencode.exe", new Version(2, 2, 5), null));
            items.Add(new(CliLabTool.MiMo, "C:/tests/mimo.exe", new Version(0, 1, 14), null));
            ((System.Windows.Controls.ListBox)typeof(CliTestLabWindow).GetField("list", flags)!.GetValue(dialog)!).SelectedIndex = 0;
            var surface = (FrameworkElement)dialog.Content; surface.Measure(new Size(760, 590)); surface.Arrange(new Rect(0, 0, 760, 590)); surface.UpdateLayout();
            var bitmap = new System.Windows.Media.Imaging.RenderTargetBitmap(760, 590, 96, 96, System.Windows.Media.PixelFormats.Pbgra32); bitmap.Render(surface);
            var encoder = new System.Windows.Media.Imaging.PngBitmapEncoder(); encoder.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(bitmap));
            Directory.CreateDirectory("artifacts/render-checks"); using (var file = File.Create("artifacts/render-checks/cli-lab-" + theme + ".png")) encoder.Save(file);
            if (dialog.MinWidth > 600 || dialog.Title != "CLI Test Lab · Preview builds only") throw new Exception("Lab dialog construction failed");
            dialog.Close();
        }
        var main = new MainWindow(new DirectoryHistoryStore(@"Software\RtlTerminal.Tests\CliLabMenu"));
        var menu = (System.Windows.Controls.MenuItem)main.FindName("ToolsMenuItem");
        if (!menu.Items.OfType<System.Windows.Controls.MenuItem>().Any(item => Equals(item.Header, "CLI Test Lab…"))) throw new Exception("Lab menu is missing from preview");
        main.Close();
        Console.WriteLine("PASS CLI Test Lab: preview UI, version refusal, isolated child environment, local Anthropic/OpenAI streaming, error handling, session isolation and shutdown");
#else
        if (typeof(MainWindow).Assembly.GetType("RtlTerminal.CliLab.LocalCliServer") != null || typeof(MainWindow).Assembly.GetType("RtlTerminal.CliLab.CliTestLabWindow") != null)
            throw new Exception("Official Release contains the CLI Test Lab");
        Console.WriteLine("PASS official Release excludes CLI Test Lab code and mock server");
#endif
    }
#if CLI_TEST_LAB
    private static async Task CheckCliLabAsync()
    {
        var root = Path.GetFullPath(Path.Combine("artifacts", "cli-lab-tests", Guid.NewGuid().ToString("N")));
        Directory.CreateDirectory(root);
        var before = Environment.GetEnvironmentVariable("ANTHROPIC_API_KEY");
        var environment = CliLabSession.CreateEnvironment(root);
        if (environment.ContainsKey("ANTHROPIC_API_KEY") || environment.ContainsKey("HTTP_PROXY") || environment.ContainsKey("GH_TOKEN") || environment["USERPROFILE"] == Environment.GetFolderPath(Environment.SpecialFolder.UserProfile)) throw new Exception("Inherited secret/settings leak");
        var block = TerminalEnvironment.Block("test-lab-session", environment);
        if (!block.Contains("USERPROFILE=" + environment["USERPROFILE"]) || !block.Contains("RTLTERMINAL_SESSION_ID=test-lab-session") || !block.EndsWith("\0\0")) throw new Exception("Explicit ConPTY environment was not used");
        foreach (var (tool, version) in new[] { (CliLabTool.Claude, new Version(2, 1, 322)), (CliLabTool.OpenCode, new Version(2, 2, 5)), (CliLabTool.OpenCode, new Version(1, 2, 0)), (CliLabTool.MiMo, new Version(0, 1, 14)) })
        {
            using var lease = new CliLabSession(new(tool, "C:/test/cli.exe", version, null), "سلام English 😀\nLOCAL_UI_TEST_OK", root);
            if (!lease.Workspace.StartsWith(root, StringComparison.OrdinalIgnoreCase) || !new Uri(lease.Server.Address).IsLoopback) throw new Exception("Lab isolation root invalid");
            var json = File.ReadAllText(Path.Combine(lease.Directory, "session.json"));
            if (json.Contains("apiKey") || json.Contains("sk-ant")) throw new Exception("Session metadata contains a credential");
            if (tool != CliLabTool.Claude)
            {
                var config = File.ReadAllText(lease.Environment[(tool == CliLabTool.MiMo ? "MIMOCODE" : "OPENCODE") + "_CONFIG"]);
                if (!config.Contains("enabled_providers") || !config.Contains("rtl-local") || !config.Contains("127.0.0.1") || config.Contains("https://")) throw new Exception("External provider leaked into configuration");
            }
        }
        if (Environment.GetEnvironmentVariable("ANTHROPIC_API_KEY") != before) throw new Exception("Process environment changed");
        try { using var invalid = new CliLabSession(new(CliLabTool.Claude, "bad.exe", null, "Unknown version"), "test", root); throw new Exception("Unknown version accepted"); } catch (NotSupportedException) { }
        using var server = new LocalCliServer("سلام English 😀\nLOCAL_UI_TEST_OK");
        using var http = new HttpClient { BaseAddress = new Uri(server.Address), Timeout = TimeSpan.FromSeconds(10) };
        var modelList = await http.GetStringAsync("/v1/models"); if (!modelList.Contains("rtl-local")) throw new Exception("Local model missing");
        foreach (var path in new[] { "/v1/messages", "/v1/chat/completions" })
            foreach (var streaming in new[] { false, true })
            {
                using var body = new StringContent(JsonSerializer.Serialize(new { model = "rtl-local", stream = streaming, messages = new[] { new { role = "user", content = "سلام English" } } }), Encoding.UTF8, "application/json");
                using var response = await http.PostAsync(path, body); response.EnsureSuccessStatusCode();
                var content = await response.Content.ReadAsStringAsync();
                // Serialized JSON may escape Persian; parse/stream completion markers are stable.
                if (!content.Contains("LOCAL_UI_TEST_OK") && !content.Contains("UI_TEST_OK")) throw new Exception("Local response did not complete");
                if (streaming && !content.Contains(path == "/v1/messages" ? "message_stop" : "[DONE]")) throw new Exception("Stream completion missing");
            }
        if (server.LastUserText != "سلام English" || server.Requests != 4) throw new Exception("Input was not preserved");
        using var bad = await http.GetAsync("/https://real-provider.invalid"); if ((int)bad.StatusCode != 404) throw new Exception("Unknown endpoint was not rejected");
        server.Dispose();
        try { using var stopped = await http.GetAsync("/v1/models"); throw new Exception("Server remained available after close"); } catch (HttpRequestException) { }
    }

    private static async Task ProbeCliLab(bool shiftEnter = false, Dictionary<string, TerminalSnapshot>? screenshots = null)
    {
        var root = Path.GetFullPath(Path.Combine("artifacts", "cli-lab-live")); Directory.CreateDirectory(root);
        var candidates = CliLabCatalog.Candidates().GroupBy(c => c.Tool).Select(g => g.First()).ToList();
        var newer = Path.GetFullPath("artifacts/claude-2.1.322/claude.exe"); if (File.Exists(newer)) candidates.Add((CliLabTool.Claude, newer));
        if (screenshots is not null)
            candidates = candidates.Where(c => c.Tool == CliLabTool.OpenCode || c.Tool == CliLabTool.Claude && (!File.Exists(newer) || string.Equals(c.Path, newer, StringComparison.OrdinalIgnoreCase))).ToList();
        var failures = new List<string>();
        foreach (var (tool, path) in candidates)
        {
            var installation = await CliLabCatalog.InspectAsync(tool, path, default);
            if (!installation.Supported) { failures.Add(installation.Label + ": " + installation.UnavailableReason); continue; }
            using var lab = new CliLabSession(installation, screenshots is null ? LocalCliServer.DefaultResponse : ScreenshotResponse, root);
            using var session = new ConPtySession(110, 35);
            var buffer = new TerminalBuffer(110, 35);
            session.Start(lab.Command, lab.Workspace, lab.Environment);
            var reader = Task.Run(() => {
                var bytes = new byte[8192]; var chars = new char[8192]; var decoder = Encoding.UTF8.GetDecoder();
                try { int n; while ((n = session.Read(bytes)) > 0) { var frame = buffer.Process(new string(chars, 0, decoder.GetChars(bytes, 0, n, chars, 0))); foreach (var answer in frame.Responses) session.Write(answer); } }
                catch (IOException) { } catch (ObjectDisposedException) { }
            });
            try
            {
                await Task.Delay(6000);
                string Text() => string.Join("\n", buffer.CaptureSnapshot().Lines.Select(l => l.Text));
                if (Text().Contains("Yes, I trust this folder"))
                {
                    var choices = Text().Split('\n'); var no = Array.FindIndex(choices, l => l.Contains("No, exit")); var yes = Array.FindIndex(choices, l => l.Contains("Yes, I trust this folder"));
                    if (no >= 0 && choices[no].TrimStart().StartsWith('>')) { session.Write(yes < no ? "\x1b[A" : "\x1b[B"); await Task.Delay(250); }
                    session.Write("\r"); await Task.Delay(1500);
                }
                if (Text().Contains("Do you want to use this API key?")) { session.Write("\x1b[A"); await Task.Delay(250); session.Write("\r"); await Task.Delay(1500); }
                File.WriteAllText(Path.Combine(lab.Directory, "startup.txt"), Text());
                if (shiftEnter)
                {
                    var result = await VerifyShiftEnter(session, buffer, () => lab.Server.Requests);
                    Console.WriteLine(installation.Label + ": " + result);
                    File.WriteAllText(Path.Combine(lab.Directory, "shift-enter.txt"), Text());
                    if (result != "PASS") failures.Add(installation.Label + ": " + result);
                    continue;
                }
                var input = screenshots is null ? "سلام English local test" : "سلام، وضعیت build و فایل src/app.cs را بررسی کن";
                if (buffer.CaptureSnapshot().Modes.BracketedPaste) input = "\x1b[200~" + input + "\x1b[201~";
                await session.WriteInputAsync(input, true, default);
                var marker = screenshots is null ? "LOCAL_UI_TEST_OK" : "Build passed";
                for (var i = 0; i < 100 && !Text().Contains(marker); i++) await Task.Delay(200);
                File.WriteAllText(Path.Combine(lab.Directory, "final.txt"), Text());
                var success = lab.Server.Requests > 0 && Text().Contains(marker);
                if (success && screenshots is not null)
                {
                    await Task.Delay(1400);
                    screenshots[tool == CliLabTool.Claude ? "Claude Code" : "OpenCode"] = buffer.CaptureSnapshot() with
                    { ClaudeLayout = tool == CliLabTool.Claude, PreserveTuiGrid = tool == CliLabTool.OpenCode };
                }
                Console.WriteLine(installation.Label + ": requests=" + lab.Server.Requests + ", response=" + success + ", artifacts=" + lab.Directory);
                if (!success) failures.Add(installation.Label + " did not return the local fixture");
            }
            finally { session.Dispose(); await reader.WaitAsync(TimeSpan.FromSeconds(5)); }
        }
        if (shiftEnter)
        {
            var comparison = await ProbeCodexShiftEnter();
            Console.WriteLine("Codex: " + comparison);
            if (comparison != "PASS") failures.Add("Codex: " + comparison);
        }
        if (failures.Count > 0) throw new Exception(string.Join("; ", failures));
    }

    private static async Task<string> VerifyShiftEnter(ConPtySession session, TerminalBuffer buffer, Func<int> requests)
    {
        string[] Lines() => buffer.CaptureSnapshot().Lines.Select(l => l.Text).ToArray();
        async Task<bool> InsertMarker(string marker)
        {
            var input = buffer.CaptureSnapshot().Modes.BracketedPaste ? "\x1b[200~" + marker + "\x1b[201~" : marker;
            if (!(await session.WriteInputAsync(input, false, default)).Success) return false;
            for (var attempt = 0; attempt < 40; attempt++)
            {
                if (Lines().Any(line => line.Contains(marker, StringComparison.Ordinal))) return true;
                await Task.Delay(100);
            }
            return false;
        }
        if (!await InsertMarker("shift_probe_first")) return "Test input did not reach the composer";
        const System.Windows.Input.Key enter = System.Windows.Input.Key.Enter;
        var sequence = MainWindow.GetTerminalKeySequence(enter, true, false, false, false,
            MainWindow.UseAgentShiftEnter(session, enter, true, false, false));
        session.Write(sequence!);
        await Task.Delay(800);
        await InsertMarker("shift_probe_second");
        var lines = Lines();
        var first = Array.FindLastIndex(lines, l => l.Contains("shift_probe_first"));
        var second = Array.FindLastIndex(lines, l => l.Contains("shift_probe_second"));
        return first >= 0 && second == first + 1 && requests() == 0
            ? "PASS" : $"No unsent multiline draft (first row={first}, second row={second}, requests={requests()})";
    }

    private static async Task CheckCodexShiftEnter()
    {
        var result = await ProbeCodexShiftEnter();
        Console.WriteLine("Codex: " + result);
        if (result != "PASS") throw new Exception(result);
    }

    private static async Task<string> ProbeCodexShiftEnter(Dictionary<string, TerminalSnapshot>? screenshots = null)
    {
        var npm = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        var vendor = Path.Combine(npm, "npm/node_modules/@openai/codex/node_modules/@openai/codex-win32-x64/vendor");
        var exe = Directory.Exists(vendor) ? Directory.EnumerateDirectories(vendor).Select(path => Path.Combine(path, "bin", "codex.exe")).FirstOrDefault(File.Exists) : null;
        if (exe is null) return "Native CLI unavailable";
        var workspace = Path.Combine(Path.GetTempPath(), "RtlTerminal", "codex-shift-enter-" + Guid.NewGuid().ToString("N"));
        using var mock = new LocalCliServer("Local keyboard test");
        var environment = CliLabSession.CreateEnvironment(workspace);
        environment["CODEX_HOME"] = Path.Combine(workspace, "home");
        File.WriteAllText(Path.Combine(environment["CODEX_HOME"], "config.toml"), $"""
            model_provider = "rtl_local"
            model = "rtl-local"
            check_for_update_on_startup = false
            [model_providers.rtl_local]
            name = "RTL Local Test"
            base_url = "{mock.Address}/v1"
            wire_api = "responses"
            requires_openai_auth = false
            [projects.{JsonSerializer.Serialize(workspace)}]
            trust_level = "trusted"
            """);
        using var session = new ConPtySession(110, 35);
        var buffer = new TerminalBuffer(110, 35);
        session.Start(ShellPath.WindowsArgument(exe) + " --no-daemon --no-alt-screen --sandbox read-only --ask-for-approval never -c check_for_update_on_startup=false", workspace, environment);
        var reader = Task.Run(() =>
        {
            var bytes = new byte[8192]; var chars = new char[8192]; var decoder = Encoding.UTF8.GetDecoder();
            try { int n; while ((n = session.Read(bytes)) > 0) { var frame = buffer.Process(new string(chars, 0, decoder.GetChars(bytes, 0, n, chars, 0))); foreach (var answer in frame.Responses) session.Write(answer); } }
            catch (IOException) { } catch (ObjectDisposedException) { }
        });
        try
        {
            await Task.Delay(6000);
            if (screenshots is not null)
            {
                var draft = "سلام، گزارش build را به فارسی بنویس\r\n" +
                    "Review src/app.cs and summarize the changes in English.\r\n" +
                    "نتیجهٔ تست‌ها و وضعیت HTTP 200 را هم اضافه کن\r\n" +
                    "العربية: أضف ملخصًا قصيرًا عن نتائج الاختبارات";
                var insert = await session.WriteInputAsync("\x1b[200~" + draft + "\x1b[201~", false, default);
                if (!insert.Success) return "Could not insert mixed-direction screenshot draft";
                for (var attempt = 0; attempt < 40; attempt++)
                {
                    var frame = buffer.CaptureSnapshot();
                    if (frame.Lines.Any(l => l.Text.Contains("summarize the changes")) &&
                        frame.Lines.Any(l => l.Text.Contains("HTTP 200")) &&
                        frame.Lines.Any(l => l.Text.Contains("الاختبارات")))
                    {
                        screenshots["Codex"] = frame;
                        return "PASS";
                    }
                    await Task.Delay(150);
                }
                return "The screenshot draft did not reach Codex";
            }
            var result = await VerifyShiftEnter(session, buffer, () => mock.Requests);
            Directory.CreateDirectory("artifacts/cli-lab-live");
            File.WriteAllLines("artifacts/cli-lab-live/codex-shift-enter.txt", buffer.CaptureSnapshot().Lines.Select(l => l.Text));
            return result;
        }
        finally { session.Dispose(); await reader.WaitAsync(TimeSpan.FromSeconds(5)); }
    }
#endif
}
