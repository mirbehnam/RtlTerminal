using System.Reflection;
using System.Windows;
using System.Windows.Media;
using RtlTerminal;

internal static partial class Program
{
    private static void CheckClaudeLayout()
    {
        if (Application.Current == null) _ = new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
        var view = new TerminalView { Width = 800, Height = 420, FontFamily = new FontFamily("Consolas"), FontSize = 18, Background = Brushes.Black };
        var flags = BindingFlags.Instance | BindingFlags.NonPublic;
        object[] Cells()
        {
            var layouts = (System.Collections.IDictionary)typeof(TerminalView).GetField("_layouts", flags)!.GetValue(view)!;
            return ((System.Collections.IEnumerable)layouts[0]!.GetType().GetProperty("Cells")!.GetValue(layouts[0])!).Cast<object>().ToArray();
        }
        double X(int offset) { var cell = Cells().Single(c => (int)c.GetType().GetProperty("Start")!.GetValue(c)! == offset); return (double)cell.GetType().GetProperty("X")!.GetValue(cell)!; }
        foreach (var code in new[] { "Console.WriteLine(\"سلام دنیا\");", "var text = \"سلام English دنیا\";", "// سلام دنیا 😀", "{\"text\":\"سلام\"}" })
        {
            var buffer = new TerminalBuffer(70, 16) { ClaudeLayout = true };
            var frame = buffer.Process(code);
            Render(view, frame, "claude-code-" + Array.IndexOf(new[] { "Console.WriteLine(\"سلام دنیا\");", "var text = \"سلام English دنیا\";", "// سلام دنیا 😀", "{\"text\":\"سلام\"}" }, code) + ".png");
            foreach (var cell in Cells())
            {
                var start = (int)cell.GetType().GetProperty("Start")!.GetValue(cell)!;
                if (start >= code.Length) continue;
                if (code[start] != '"' && code[..start].Count(c => c == '"') % 2 == 1) continue;
                if (code[start] < 128 && !char.IsWhiteSpace(code[start]) && Math.Abs(X(start) - start * 10.8) > .01)
                    throw new Exception("Claude code punctuation/Latin token moved: " + code);
            }
            if (frame.Lines[0].Text.TrimEnd() != code) throw new Exception("Claude layout changed underlying text");
        }
        var english = new TerminalBuffer(70, 16) { ClaudeLayout = true }.Process("English first سپس فارسی");
        Render(view, english, "claude-english-first.png");
        if (X(0) > 1) throw new Exception("Claude English-first paragraph must stay LTR");
        Render(view, english with { ClaudeLayout = false }, "claude-default-unchanged.png");
        if (X(0) < 100) throw new Exception("Default/Codex paragraph behavior changed");
        var persian = new TerminalBuffer(70, 16) { ClaudeLayout = true }.Process("سلام English دنیا");
        Render(view, persian, "claude-persian-first.png");
        if (X(0) < 100) throw new Exception("Claude Persian-first paragraph lost RTL alignment");
        Console.WriteLine("PASS Claude-only code punctuation, string shaping, mixed text, emoji, raw-text preservation and default-profile isolation");
    }
}
