using System.Diagnostics;
using System.IO;
using System.Text;
using RtlTerminal;

internal static partial class Program
{
    private static async Task ProbeQuickInputClis()
    {
        var folder = Path.GetFullPath(Path.Combine("artifacts", "quick-input-cli-" + Guid.NewGuid().ToString("N"))); Directory.CreateDirectory(folder);
        var npm = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "npm", "node_modules");
        var node = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "nodejs", "node.exe");
        var claude = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Microsoft", "WinGet", "Packages", "Anthropic.ClaudeCode_Microsoft.Winget.Source_8wekyb3d8bbwe", "claude.exe");
        var candidates = new List<(string Name, string Exe, string Command, bool Shell)>();
        var ps = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "WindowsPowerShell", "v1.0", "powershell.exe");
        var profileType = typeof(MainWindow).GetNestedType("TerminalProfile", System.Reflection.BindingFlags.NonPublic)!;
        var commandFactory = typeof(MainWindow).GetMethod("GetProfileCommand", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic)!;
        var psCommand = (string)commandFactory.Invoke(null, new object?[] { Enum.Parse(profileType, "PowerShell"), false, null, null })!;
        candidates.Add(("PowerShell", ps, psCommand, true));
        var cmd = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "cmd.exe");
        candidates.Add(("CMD", cmd, ShellPath.WindowsArgument(cmd) + " /d /q", true));
        if (ShellProfiles.GitBashPath is { } bash) candidates.Add(("Git Bash", bash, ShellPath.WindowsArgument(bash) + " --noprofile --norc -i", true));
        var codex = Path.Combine(npm, "@openai", "codex", "bin", "codex.js");
        if (File.Exists(codex)) candidates.Add(("Codex", node, ShellPath.WindowsArgument(node) + " " + ShellPath.WindowsArgument(codex) + " --no-daemon --no-alt-screen --sandbox read-only --ask-for-approval never", false));
        candidates.Add(("Claude Code", claude, ShellPath.WindowsArgument(claude) + " --tools \"\" --strict-mcp-config --mcp-config " + ShellPath.WindowsArgument("{\"mcpServers\":{}}"), false));
        var opencode = Path.Combine(npm, "@opencode", "cli", "bin", "opencode.exe");
        candidates.Add(("OpenCode", opencode, ShellPath.WindowsArgument(opencode) + " --standalone", false));
        foreach (var target in candidates)
        {
            if (!File.Exists(target.Exe)) { Console.WriteLine("SKIP " + target.Name + ": executable unavailable"); continue; }
            using var session = new ConPtySession(100, 28); var buffer = new TerminalBuffer(100, 28);
            session.Start(target.Command, folder); var any = false;
            var reader = Task.Run(() =>
            {
                var bytes = new byte[8192]; var chars = new char[8192]; var decoder = Encoding.UTF8.GetDecoder();
                try
                {
                    int count;
                    while ((count = session.Read(bytes)) > 0)
                    {
                        any = true; var snapshot = buffer.Process(new string(chars, 0, decoder.GetChars(bytes, 0, count, chars, 0)));
                        foreach (var response in snapshot.Responses) session.Write(response);
                    }
                }
                catch (IOException) { } catch (ObjectDisposedException) { }
            });
            try
            {
                var watch = Stopwatch.StartNew();
                while (watch.Elapsed < TimeSpan.FromSeconds(target.Shell ? 2 : 6)) await Task.Delay(100);
                if (target.Shell)
                {
                    if (target.Name is "PowerShell" or "CMD" && !buffer.QuickInputMode.Ready) throw new Exception(target.Name + " did not emit its explicit ready marker");
                    buffer.InvalidateQuickInput();
                    var command = target.Name == "Git Bash" ? "printf '\\nQI_SHELL_OK\\n'" : target.Name == "CMD" ? "echo QI_SHELL_OK" : "Write-Output 'QI_SHELL_OK'";
                    var inserted = await session.WriteInputAsync(command, false, default);
                    await Task.Delay(100);
                    if (!inserted.Success || buffer.CaptureSnapshot().Lines.Any(l => l.Text.Trim() == "QI_SHELL_OK")) throw new Exception(target.Name + " Insert executed unexpectedly or failed");
                    var sent = await session.WriteInputAsync("", true, default);
                    for (var i = 0; i < 100 && !buffer.CaptureSnapshot().Lines.Any(l => l.Text.Trim() == "QI_SHELL_OK"); i++) await Task.Delay(30);
                    if (!sent.Success || !buffer.CaptureSnapshot().Lines.Any(l => l.Text.Trim() == "QI_SHELL_OK")) throw new Exception(target.Name + " final Enter was not verified");
                    Console.WriteLine("PASS " + target.Name + ": native Insert does not submit; explicit Send submits once");
                }
                else Console.WriteLine("PROBE " + target.Name + ": output=" + any + ", bracketedPaste=" + buffer.QuickInputMode.BracketedPaste + ", inputSignal=" + buffer.QuickInputMode.Ready + ". Model submission not attempted; authentication/setup/network may require user interaction.");
            }
            finally { session.Dispose(); await reader.WaitAsync(TimeSpan.FromSeconds(5)); }
        }
    }
}
