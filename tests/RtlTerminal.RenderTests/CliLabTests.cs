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

    private static async Task ProbeCliLab()
    {
        var root = Path.GetFullPath(Path.Combine("artifacts", "cli-lab-live")); Directory.CreateDirectory(root);
        var candidates = CliLabCatalog.Candidates().GroupBy(c => c.Tool).Select(g => g.First()).ToList();
        var newer = Path.GetFullPath("artifacts/claude-2.1.322/claude.exe"); if (File.Exists(newer)) candidates.Add((CliLabTool.Claude, newer));
        var failures = new List<string>();
        foreach (var (tool, path) in candidates)
        {
            var installation = await CliLabCatalog.InspectAsync(tool, path, default);
            if (!installation.Supported) { failures.Add(installation.Label + ": " + installation.UnavailableReason); continue; }
            using var lab = new CliLabSession(installation, LocalCliServer.DefaultResponse, root);
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
                var input = "سلام English local test";
                if (buffer.CaptureSnapshot().Modes.BracketedPaste) input = "\x1b[200~" + input + "\x1b[201~";
                await session.WriteInputAsync(input, true, default);
                for (var i = 0; i < 100 && !Text().Contains("LOCAL_UI_TEST_OK"); i++) await Task.Delay(200);
                File.WriteAllText(Path.Combine(lab.Directory, "final.txt"), Text());
                var success = lab.Server.Requests > 0 && Text().Contains("LOCAL_UI_TEST_OK");
                Console.WriteLine(installation.Label + ": requests=" + lab.Server.Requests + ", response=" + success + ", artifacts=" + lab.Directory);
                if (!success) failures.Add(installation.Label + " did not return the local fixture");
            }
            finally { session.Dispose(); await reader.WaitAsync(TimeSpan.FromSeconds(5)); }
        }
        if (failures.Count > 0) throw new Exception(string.Join("; ", failures));
    }
#endif
}
