using System.Collections;
using System.Reflection;
using System.Windows;
using System.Windows.Media;
using RtlTerminal;

internal static partial class Program
{
    private static void CheckClaudeVisualInput()
    {
        var view = new TerminalView { Width = 900, Height = 420, FontFamily = new FontFamily("Consolas"), FontSize = 18, Background = Brushes.Black };
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
        foreach (var input in new[] { "ایند مالس", "src/test.cs ایند English مالس", "src/test.cs ایند English مالس\u200c", "123 (ایند) 😀 مالس" })
        {
            var buffer = new TerminalBuffer(70, 16) { ClaudeLayout = true };
            var snapshot = buffer.Process("Claude Code v2.1.322\r\n────────────────────\r\n>\u00a0 " + input + "\r\n────────────────────\x1b[3;4H");
            if (TuiCompatibility.ReadClaudeVisualOrder(snapshot) != true || !TuiCompatibility.IsClaudeInputRow(snapshot, 2) || TuiCompatibility.IsClaudeInputRow(snapshot, 0))
                throw new Exception("Claude visual prompt/version detection failed");
            Render(view, snapshot, "claude-visual-input-" + Array.IndexOf(new[] { "ایند مالس", "src/test.cs ایند English مالس", "src/test.cs ایند English مالس\u200c", "123 (ایند) 😀 مالس" }, input) + ".png");
            var layouts = (IDictionary)typeof(TerminalView).GetField("_layouts", flags)!.GetValue(view)!;
            var layout = layouts[2]!;
            var cells = ((IEnumerable)layout.GetType().GetProperty("Cells")!.GetValue(layout)!).Cast<object>().ToArray();
            double last = -1;
            foreach (var cell in cells.OrderBy(c => (int)c.GetType().GetProperty("Start")!.GetValue(c)!))
            {
                var x = (double)cell.GetType().GetProperty("X")!.GetValue(cell)!;
                if (x + .01 < last) throw new Exception("Claude visual cell order reversed twice");
                last = x;
                if ((bool)cell.GetType().GetProperty("Rtl")!.GetValue(cell)!) throw new Exception("Visual input hit testing used logical RTL order");
            }
            var glyphs = ((IEnumerable)layout.GetType().GetProperty("Glyphs")!.GetValue(layout)!).Cast<object>();
            if (!glyphs.Any(g => ((string)g.GetType().GetProperty("Text")!.GetValue(g)!).Replace("\u200c", "").Contains("سلام", StringComparison.Ordinal)))
                throw new Exception("Claude preordered Persian was not restored for contextual shaping: " + input + " glyphs=" + string.Join("|", glyphs.Select(g => (string)g.GetType().GetProperty("Text")!.GetValue(g)!)));
            if (snapshot.Lines[2].Text.TrimEnd() != ">\u00a0 " + input) throw new Exception("Raw terminal cells were modified");
            var old = new TerminalBuffer(70, 16).Process("Claude Code v2.1.50");
            if (TuiCompatibility.ReadClaudeVisualOrder(old) != false) throw new Exception("Legacy Claude was opted into visual-input mode");
        }
        Console.WriteLine("PASS Claude 2.1.322 visual input: grid order, contextual shaping, source offsets, hit testing, mixed paths, emoji and legacy-version isolation");
        CheckClaudeVisualResponses();
    }

    private static void CheckClaudeVisualResponses()
    {
        var view = new TerminalView { Width = 1100, Height = 420, FontFamily = new FontFamily("Consolas"), FontSize = 18, Background = Brushes.Black };
        var buffer = new TerminalBuffer(100, 16) { ClaudeLayout = true };
        var snapshot = buffer.Process("Claude Code v2.1.322\r\n" +
            "● .تسا RTL Terminal رد English و Persian یسررب یارب یشیامزآ خساپ کی نیا !مالس\r\n" +
            "  var message = \"ایند مالس - Hello world\";\r\n" +
            "  Console.WriteLine(\"هجیتن: Success\");\r\n" +
            "  English first: ریسم C:\\Projects\\هنومن\\app.json (HTTP 200) 😀\r\n" +
            "────────────────────\r\n>\u00a0 \r\n────────────────────\x1b[7;4H");
        Render(view, snapshot, "claude-visual-response.png");
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
        var layouts = (IDictionary)typeof(TerminalView).GetField("_layouts", flags)!.GetValue(view)!;
        for (var row = 1; row <= 4; row++)
        {
            var layout = layouts[row]!;
            var cells = ((IEnumerable)layout.GetType().GetProperty("Cells")!.GetValue(layout)!).Cast<object>().ToArray();
            var text = snapshot.Lines[row].Text;
            foreach (var cell in cells)
            {
                var start = (int)cell.GetType().GetProperty("Start")!.GetValue(cell)!;
                if (start >= text.Length || text[start] >= 128) continue;
                var x = (double)cell.GetType().GetProperty("X")!.GetValue(cell)!;
                if (Math.Abs(x - start * 10.8) > .01) throw new Exception("Claude response moved a code/English/punctuation cell: " + text[start]);
            }
            var glyphs = ((IEnumerable)layout.GetType().GetProperty("Glyphs")!.GetValue(layout)!).Cast<object>();
            var expected = new[] { "", "سلام", "سلام", "نتیجه", "نمونه" }[row];
            if (!glyphs.Any(g => (string)g.GetType().GetProperty("Text")!.GetValue(g)! == expected))
                throw new Exception("Claude response Persian shaping is reversed: " + expected);
        }
        // Wrapped visual rows must stay in the CLI's grid instead of logical reflow.
        var wrapped = new TerminalBuffer(30, 16) { ClaudeLayout = true }.Process("Claude Code v2.1.322\r\n" + string.Join(" ", Enumerable.Repeat("ایند مالس", 10)));
        Render(view, wrapped, "claude-visual-response-wrapped.png");
        var presented = (TerminalSnapshot)typeof(TerminalView).GetField("_snapshot", flags)!.GetValue(view)!;
        if (!ReferenceEquals(presented.Lines, wrapped.Lines)) throw new Exception("Visual Claude response was logically reflowed");
        Console.WriteLine("PASS Claude visual responses: Persian words, mixed English, code strings, punctuation, emoji and CLI-owned wrapping");
    }
}
