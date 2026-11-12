using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using RtlTerminal;

internal static partial class Program
{
    private static void ProbeClaudeUi()
    {
        var exe = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Microsoft", "WinGet", "Packages", "Anthropic.ClaudeCode_Microsoft.Winget.Source_8wekyb3d8bbwe", "claude.exe");
        var arguments = Environment.GetCommandLineArgs();
        var executableArgument = Array.IndexOf(arguments, "--claude-exe");
        if (executableArgument >= 0) exe = Path.GetFullPath(arguments[executableArgument + 1]);
        if (!File.Exists(exe)) throw new FileNotFoundException("Claude Code executable not found", exe);
        var root = Path.GetFullPath(Path.Combine("artifacts", "claude-ui-probe-" + Guid.NewGuid().ToString("N"))); Directory.CreateDirectory(root);
        var emptyMcp = Path.Combine(root, "mcp.json"); File.WriteAllText(emptyMcp, "{\"mcpServers\":{}}");
        using var mock = Environment.GetCommandLineArgs().Contains("--mock") ? new ClaudeMockServer() : null;
        var captures = new List<(string Name, TerminalSnapshot Snapshot)>();
        var failures = new List<string>();
        var snapshot = Task.Run(async () =>
        {
            using var session = new ConPtySession(100, 30);
            var buffer = new TerminalBuffer(100, 30);
            var variables = new Dictionary<string, string?> { ["CLAUDE_CONFIG_DIR"] = Path.Combine(root, "config"), ["CLAUDE_CODE_DISABLE_NONESSENTIAL_TRAFFIC"] = "1", ["DISABLE_AUTOUPDATER"] = "1" };
            if (mock != null)
            {
                Directory.CreateDirectory(variables["CLAUDE_CONFIG_DIR"]!);
                File.WriteAllText(Path.Combine(variables["CLAUDE_CONFIG_DIR"]!, ".claude.json"), "{\"hasCompletedOnboarding\":true,\"theme\":\"dark\"}");
                variables["ANTHROPIC_BASE_URL"] = mock.Address;
                variables["ANTHROPIC_API_KEY"] = "sk-ant-api03-local-ui-test-only";
                variables["ANTHROPIC_AUTH_TOKEN"] = null;
            }
            var previous = variables.Keys.ToDictionary(k => k, Environment.GetEnvironmentVariable);
            try
            {
                foreach (var v in variables) Environment.SetEnvironmentVariable(v.Key, v.Value);
                session.Start(ShellPath.WindowsArgument(exe) + " --tools \"\" --strict-mcp-config --mcp-config " + ShellPath.WindowsArgument(emptyMcp) + " --setting-sources \"\" --no-chrome", root);
            }
            finally { foreach (var v in previous) Environment.SetEnvironmentVariable(v.Key, v.Value); }
            var read = Task.Run(() =>
            {
                var bytes = new byte[8192]; var chars = new char[8192]; var decoder = Encoding.UTF8.GetDecoder();
                try { int n; while ((n = session.Read(bytes)) > 0) { buffer.ClaudeLayout = session.ReadTuiCompatibility() == TuiCompatibilityProfile.Claude; var frame = buffer.Process(new string(chars, 0, decoder.GetChars(bytes, 0, n, chars, 0))); foreach (var response in frame.Responses) session.Write(response); } }
                catch (IOException) { } catch (ObjectDisposedException) { }
            });
            try
            {
                await Task.Delay(5000);
                var frame = buffer.CaptureSnapshot();
                var text = string.Join("\n", frame.Lines.Select(l => l.Text));
                Console.WriteLine("Claude startup: alternate=" + frame.Modes.AlternateScreen + ", bracketedPaste=" + frame.Modes.BracketedPaste + ", themePicker=" + text.Contains("text style", StringComparison.OrdinalIgnoreCase) + ", login=" + text.Contains("log in", StringComparison.OrdinalIgnoreCase));
                // Choosing a color theme is local UI setup, not accepting terms or authentication.
                if (text.Contains("Choose the text style", StringComparison.OrdinalIgnoreCase))
                {
                    session.Write("\r"); await Task.Delay(2000); frame = buffer.CaptureSnapshot();
                }
                if (session.RequiresFixedTuiGrid()) throw new Exception("Claude must not inherit the OpenCode/MiMo compatibility profile.");
                if (!buffer.ClaudeLayout) throw new Exception("Claude-specific layout profile was not detected.");
                if (mock != null)
                {
                    var setup = string.Join("\n", frame.Lines.Select(l => l.Text));
                    if (setup.Contains("Yes, I trust this folder", StringComparison.Ordinal))
                    {
                        // Newer Claude defaults to No. Confirm only this generated test workspace.
                        if (setup.Contains("> No, exit", StringComparison.Ordinal) || setup.Contains("❯ No, exit", StringComparison.Ordinal))
                        { session.Write("\x1b[B"); await Task.Delay(250); }
                        session.Write("\r"); await Task.Delay(2000); frame = buffer.CaptureSnapshot();
                    }
                    setup = string.Join("\n", frame.Lines.Select(l => l.Text));
                    if (setup.Contains("Do you want to use this API key?", StringComparison.Ordinal))
                    { session.Write("\x1b[A"); await Task.Delay(250); session.Write("\r"); await Task.Delay(2000); frame = buffer.CaptureSnapshot(); }
                    captures.Add(("main", frame));
                    var cases = new[] { "hello English", "سلام دنیا", "سلام English دنیا src/test.cs" };
                    for (var i = 0; i < cases.Length; i++)
                    {
                        session.Write("\x15");
                        var written = await session.WriteInputAsync(cases[i], false, default);
                        if (!written.Success) throw new Exception("Local UI input transfer failed");
                        await Task.Delay(500); frame = buffer.CaptureSnapshot();
                        var expectedVisual = new[] { "hello English", "ایند مالس", "src/test.cs ایند English مالس" };
                        var modernVisual = TuiCompatibility.ReadClaudeVisualOrder(frame) == true;
                        var visible = frame.Lines.Any(l => l.Text.Contains(modernVisual ? expectedVisual[i] : cases[i], StringComparison.Ordinal));
                        Console.WriteLine("Claude input cursor=" + frame.CursorRow + ", visual input region=" + TuiCompatibility.IsClaudeInputRow(frame, frame.CursorRow));
                        Console.WriteLine("Claude input case " + i + ": visible=" + visible);
                        if (!visible) failures.Add("Input fixture " + i + " was not visible");
                        captures.Add(("input-" + i, frame));
                    }
                    var sent = await session.WriteInputAsync("", true, default);
                    if (!sent.Success) throw new Exception("Mock prompt submission failed");
                    for (var i = 0; i < 100 && !buffer.CaptureSnapshot().Lines.Any(l => l.Text.Contains("LOCAL_UI_TEST_OK")); i++) await Task.Delay(150);
                    frame = buffer.CaptureSnapshot();
                    captures.Add(("streamed-response", frame));
                    var received = frame.Lines.Any(l => l.Text.Contains("LOCAL_UI_TEST_OK"));
                    if (!received || mock.Requests == 0) failures.Add("Local streamed response was not verified");
                    var submitted = mock.LastUserText ?? "";
                    while (submitted.StartsWith("<system-reminder>", StringComparison.Ordinal) && submitted.IndexOf("</system-reminder>", StringComparison.Ordinal) is var end && end >= 0)
                        submitted = submitted[(end + "</system-reminder>".Length)..].TrimStart('\r', '\n');
                    if (submitted != cases[^1]) failures.Add("Submitted mixed prompt was not preserved in logical order");
                    Console.WriteLine("Claude local streamed response: marker=" + received + ", requests=" + mock.Requests);
                    session.Resize(70, 30); buffer.Resize(70, 30); await Task.Delay(600);
                    frame = buffer.CaptureSnapshot(); captures.Add(("resized", frame));
                    if (received && !frame.Lines.Any(l => l.Text.Contains("LOCAL_UI_TEST_OK"))) failures.Add("Resize lost the completed response");
                    // Inspect the next local setup screen before sending any prompt.
                    Console.WriteLine("Mock ready: requests=" + mock.Requests);
                }
                var finalText = string.Join("\n", frame.Lines.Select(l => l.Text));
                Console.WriteLine("Claude result: loginGate=" + finalText.Contains("Select login method", StringComparison.OrdinalIgnoreCase) + ", OpenCode/MiMo profile disabled; backend=" + (mock == null ? "none" : "local fixture"));
                return frame;
            }
            finally { session.Dispose(); await read.WaitAsync(TimeSpan.FromSeconds(5)); }
        }).GetAwaiter().GetResult();
        if (Application.Current == null) _ = new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
        AppTheme.Apply(AppThemeKind.Dark);
        var view = new TerminalView { Width = 1100, Height = 780, FontFamily = new FontFamily("Consolas"), FontSize = 18, Background = Brushes.Black };
        captures.Add(("startup", snapshot));
        foreach (var capture in captures)
        {
        File.WriteAllText(Path.Combine(root, "claude-" + capture.Name + ".txt"), string.Join("\n", capture.Snapshot.Lines.Select(line => line.Text)));
        var width = capture.Snapshot.Columns * 10.8 + 20;
        view.Width = width;
        view.Present(capture.Snapshot, true, 10.8, 25, true); view.Measure(new Size(width, 780)); view.Arrange(new Rect(0, 0, width, 780)); view.UpdateLayout();
        view.Dispatcher.Invoke(() => { }, System.Windows.Threading.DispatcherPriority.ContextIdle);
        var bitmap = new RenderTargetBitmap((int)Math.Ceiling(width), 780, 96, 96, PixelFormats.Pbgra32); bitmap.Render(view);
        var png = new PngBitmapEncoder(); png.Frames.Add(BitmapFrame.Create(bitmap));
        using var file = File.Create(Path.Combine(root, "claude-" + capture.Name + ".png")); png.Save(file);
        }
        Console.WriteLine("Claude UI artifact: " + root);
        if (failures.Count > 0) throw new Exception(string.Join("; ", failures));
    }
}
