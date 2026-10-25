using RtlTerminal;

var tests = new (string Name, Action Run)[]
{
    ("OSC 8 streams preserve hyperlink metadata independently of SGR", Osc8Links),
    ("alternate screen is isolated and restored", AlternateScreenIsIsolated),
    ("styled trailing spaces remain visible", StyledTrailingSpacesRemainVisible),
    ("modern SGR attributes reset independently", SgrAttributesResetIndependently),
    ("colon truecolor is parsed", ColonTrueColorIsParsed),
    ("indexed and explicit RGB colors stay distinct across resets", ColorOriginsArePreserved),
    ("terminal capability queries receive replies", CapabilityQueriesReceiveReplies),
    ("OpenTUI capability handshake does not leak", OpenTuiHandshakeDoesNotLeak),
    ("modern TUI modes are tracked", ModernTuiModesAreTracked),
    ("emoji grapheme clusters keep terminal width", EmojiClustersKeepWidth),
    ("Persian text remains detectable for Smart RTL", PersianTextRemainsSmartRtl),
    ("carriage return is immediate across reads", CarriageReturnIsImmediate),
    ("style changes preserve pending autowrap", StylePreservesWrap),
    ("status redraw clears old characters across reads", StatusRedraw),
    ("wide characters can be erased and overwritten", WideErase),
    ("snapshot rows are reused without hiding edits", SnapshotReuse),
    ("resize reflows shell history without losing text, styles or graphemes", ResizeReflow),
    ("dropped Windows paths become WSL paths", DroppedPathsBecomeWslPaths)
};

var failures = new List<string>();

foreach (var test in tests)
{
    try
    {
        test.Run();
        Console.WriteLine($"PASS  {test.Name}");
    }
    catch (Exception exception)
    {
        failures.Add($"FAIL  {test.Name}: {exception.Message}");
    }
}

foreach (var failure in failures)
    Console.Error.WriteLine(failure);

return failures.Count == 0 ? 0 : 1;

static void Osc8Links()
{
    const string target = "https://example.org/a;b?q=1&x=2";
    var buffer = new TerminalBuffer(12, 5);
    var stream = "\x1b]8;id=example;" + target + "\x1b\\" +
        "first\x1b[31mSECOND\x1b[0mthird\x1b]8;;\a plain";
    TerminalSnapshot snapshot = buffer.Process("");
    foreach (var c in stream) snapshot = buffer.Process(c.ToString());
    var linked = snapshot.Lines.SelectMany(l => l.Runs).Where(r => r.Style.Hyperlink is not null).ToArray();
    Assert(string.Concat(linked.Select(r => r.Text)) == "firstSECONDthird", "OSC 8 escaped text leaked or SGR reset closed the link");
    Assert(linked.All(r => r.Style.Hyperlink == target), "OSC 8 URI parameters or semicolons were lost");
    snapshot = buffer.Resize(30, 10);
    Assert(string.Concat(snapshot.Lines.SelectMany(l => l.Runs).Where(r => r.Style.Hyperlink == target).Select(r => r.Text)) == "firstSECONDthird", "Reflow lost hyperlink metadata");

    var erase = new TerminalBuffer(30, 5);
    snapshot = erase.Process("\x1b]8;;" + target + "\aold\r\x1b[2Knew\x1b]8;;\a");
    Assert(snapshot.Lines[0].Runs.Where(r => r.Style.Hyperlink is not null).All(r => r.Text == "new"), "Erased padding inherited hyperlink metadata");
    snapshot = erase.Process("\x1b]8;;" + new string('a', 4200) + "\aoverflow\x1b]8;;\a");
    Assert(snapshot.Lines.SelectMany(l => l.Runs).Where(r => r.Text.Contains("overflow")).All(r => r.Style.Hyperlink is null), "Truncated OSC URI was accepted");

    var history = new TerminalBuffer(30, 5);
    snapshot = history.Process("\x1b]8;;" + target + "\alink\x1b]8;;\a" + new string('\n', 8));
    Assert(snapshot.ScrollbackCount > 0 && snapshot.Lines[0].Runs.Any(r => r.Style.Hyperlink == target), "Scrollback lost the OSC 8 target");
    snapshot = history.Process("\x1b[?1049h\x1b]8;;https://other.org\aother\x1b]8;;\a\x1b[?1049l");
    Assert(snapshot.Lines[0].Runs.Any(r => r.Style.Hyperlink == target), "Alternate screen damaged saved links");
}

static void ResizeReflow()
{
    static string Logical(TerminalSnapshot snapshot)
    {
        var result = new System.Text.StringBuilder();
        for (var i = 0; i < snapshot.Lines.Count; i++)
        {
            var text = string.Concat(snapshot.Lines[i].Runs.Select(r => r.Text));
            var continues = i + 1 < snapshot.Lines.Count && snapshot.Lines[i + 1].WrappedFromPrevious;
            result.Append(continues ? text : text.TrimEnd());
            if (!continues && i + 1 < snapshot.Lines.Count) result.Append('\n');
        }
        return result.ToString();
    }
    var buffer = new TerminalBuffer(80, 10);
    var original = buffer.Process(string.Join("\r\n", Enumerable.Range(0, 25)
        .Select(i => $"ROW{i:D2}: سلام دنیا می‌خواهم آزمایش کنم 👨‍💻 中文 \x1b[31mcolored text\x1b[0m " + new string('x', 45))));
    var expected = Logical(original);
    foreach (var (columns, rows) in new[] { (20, 5), (120, 30), (11, 8), (80, 10), (13, 5), (100, 20) })
    {
        var resized = buffer.Resize(columns, rows);
        Assert(Logical(resized) == expected, $"Resize {columns}x{rows} changed logical shell text");
        Assert(resized.Lines.SelectMany(l => l.Runs).Any(r => r.Text.Contains("colored") && r.Style.Foreground is not null), "Resize lost ANSI style");
    }
    var final = buffer.Process("!");
    Assert(Logical(final) == expected + "!", "Resize misplaced the cursor for subsequent output");

    var edge = new TerminalBuffer(20, 8);
    edge.Process("12345678901234567890");
    edge.Resize(10, 5);
    Assert(Logical(edge.Process("Z")) == "12345678901234567890Z", "Resize lost pending auto-wrap at the right edge");

    var wide = new TerminalBuffer(11, 6);
    var wideText = Logical(wide.Process("1234567890中文🙂 end"));
    for (var i = 0; i < 5; i++)
    {
        Assert(Logical(wide.Resize(13, 5)) == wideText, "Widening added padding to wide glyphs");
        Assert(Logical(wide.Resize(10, 7)) == wideText, "Narrowing split a wide glyph or inserted spaces");
    }

    var alt = new TerminalBuffer(40, 8);
    var main = Logical(alt.Process("saved shell text with a long soft wrapped sentence سلام دنیا\r\nnext prompt"));
    alt.Process("\x1b[?1049hTUI");
    var alternate = alt.Resize(15, 5);
    Assert(alternate.Modes.AlternateScreen && alternate.Lines.Count == 5 && alternate.ScrollbackCount == 0, "Resize reflowed an application-owned alternate grid");
    Assert(Logical(alt.Process("\x1b[?1049l")) == main, "Resize lost the saved main screen behind a TUI");
}

static void AlternateScreenIsIsolated()
{
    var buffer = new TerminalBuffer(24, 6);
    buffer.Process("shell history\r\nsecond line");

    var alternate = buffer.Process("\x1b[?1049hOpenCode");
    Assert(alternate.Modes.AlternateScreen, "alternate mode was not enabled");
    Assert(alternate.ScrollbackCount == 0, "main scrollback leaked into the TUI");
    Assert(alternate.Lines.Count == 6, "alternate screen did not retain its fixed grid");
    Assert(Text(alternate).Contains("OpenCode"), "alternate content is missing");
    Assert(!Text(alternate).Contains("shell history"), "main screen leaked into alternate content");

    var restored = buffer.Process("\x1b[?1049l");
    Assert(!restored.Modes.AlternateScreen, "alternate mode was not disabled");
    Assert(Text(restored).Contains("shell history"), "main screen was not restored");
}

static void StyledTrailingSpacesRemainVisible()
{
    var buffer = new TerminalBuffer(20, 5);
    var snapshot = buffer.Process("\x1b[48;2;33;35;55m    \x1b[0m");
    var line = snapshot.Lines[0];
    Assert(line.CellLength >= 4, "background cells were trimmed");
    Assert(line.Runs.Any(run =>
        run.Text.Length >= 4 &&
        run.Style.Background == new TerminalColor(33, 35, 55)),
        "background style on blank cells was lost");
}

static void SgrAttributesResetIndependently()
{
    var buffer = new TerminalBuffer(20, 5);
    var snapshot = buffer.Process(
        "\x1b[4;7;9mA\x1b[24;27;29mB");
    var runs = snapshot.Lines[0].Runs;
    var first = runs.Single(run => run.Text == "A").Style;
    var second = runs.Single(run => run.Text.StartsWith('B')).Style;
    Assert(first.Underline && first.Inverse && first.Strikethrough,
        "enabled SGR attributes were not retained");
    Assert(!second.Underline && !second.Inverse && !second.Strikethrough,
        "SGR reset codes leaked into following text");
}

static void ColonTrueColorIsParsed()
{
    var buffer = new TerminalBuffer(20, 5);
    var snapshot = buffer.Process("\x1b[38:2::91:145:255mX");
    var style = snapshot.Lines[0].Runs[0].Style;
    Assert(style.Foreground == new TerminalColor(91, 145, 255),
        "colon-form RGB foreground was parsed incorrectly");
}

static void ColorOriginsArePreserved()
{
    var snapshot = new TerminalBuffer(30, 5).Process(
        "\x1b[31;41mA" +
        "\x1b[38;2;197;15;31;48:2::197:15:31mB" +
        "\x1b[38;5;1;48;5;1mC" +
        "\x1b[90;100mD" +
        "\x1b[38;5;231;48;5;231mE" +
        "\x1b[39mF\x1b[49mG\x1b[31;41mH\x1b[0mI\x1b[38;5;243;48;5;243mJ");
    TerminalStyle Style(char letter) => snapshot.Lines[0].Runs.Single(run => run.Text.Contains(letter)).Style;
    var ansi = Style('A');
    var rgb = Style('B');
    Assert(ansi.Foreground == rgb.Foreground && ansi.Background == rgb.Background,
        "matching ANSI and truecolor RGB values differ");
    Assert(ansi.ForegroundPaletteIndex == 1 && ansi.BackgroundPaletteIndex == 1 &&
        rgb.ForegroundPaletteIndex is null && rgb.BackgroundPaletteIndex is null,
        "truecolor inherited ANSI palette metadata or adjacent runs lost their origin");
    Assert(Style('C').ForegroundPaletteIndex == 1 && Style('C').BackgroundPaletteIndex == 1,
        "indexed SGR lost its palette index");
    Assert(Style('D').ForegroundPaletteIndex == 8 && Style('D').BackgroundPaletteIndex == 8,
        "bright ANSI colors lost their palette indexes");
    Assert(Style('E').ForegroundPaletteIndex == 231 && Style('E').BackgroundPaletteIndex == 231,
        "extended palette indexes were truncated");
    Assert(Style('F').Foreground is null && Style('F').ForegroundPaletteIndex is null &&
        Style('F').BackgroundPaletteIndex == 231, "SGR 39 did not reset only the foreground");
    Assert(Style('G').Background is null && Style('G').BackgroundPaletteIndex is null,
        "SGR 49 retained its background palette index");
    Assert(Style('I') == default, "SGR 0 did not clear palette metadata");
    Assert(Style('J').ForegroundPaletteIndex == 243 && Style('J').BackgroundPaletteIndex == 243 &&
        Style('J').Foreground == Style('D').Foreground && Style('J').Background == Style('D').Background,
        "an extended palette color was confused with an identical standard ANSI color");
}

static void CapabilityQueriesReceiveReplies()
{
    var buffer = new TerminalBuffer(20, 5);
    var snapshot = buffer.Process("\x1b[6n\x1b[c\x1b]11;?\x07");
    Assert(snapshot.Responses.Any(response => response.EndsWith("R")),
        "cursor-position report is missing");
    Assert(snapshot.Responses.Any(response => response.EndsWith("c")),
        "device-attributes report is missing");
    Assert(snapshot.Responses.Any(response => response.StartsWith("\x1b]11;rgb:")),
        "terminal-background report is missing");
}

static void OpenTuiHandshakeDoesNotLeak()
{
    var buffer = new TerminalBuffer(30, 6);
    var snapshot = buffer.Process(
        "\x1b]10;?\x1b\\" +
        "\x1b]11;?\x1b\\" +
        "\x1b[>0q" +
        "\x1bP+q4d73\x1b\\" +
        "\x1b[?1016$p\x1b[?2027$p\x1b[?2031$p" +
        "\x1b[?1004$p\x1b[?2004$p\x1b[?2026$p" +
        "\x1b[?u" +
        "\x1b]99;i=opentui-notifications:p=?;\x1b\\" +
        "\x1b]1337;Capabilities\x1b\\" +
        "\x1b_Gi=31337,s=1,v=1,a=q,t=d,f=24;AAAA\x1b\\" +
        "\x1b[>4;1mX");
    var visibleText = Text(snapshot);
    var xStyle = snapshot.Lines[0].Runs
        .Single(run => run.Text.Contains('X')).Style;

    Assert(!visibleText.Contains("4d73") &&
        !visibleText.Contains("Gi=31337") &&
        !visibleText.Contains("Capabilities"),
        "a terminal control string leaked into visible output");
    Assert(!xStyle.Bold && !xStyle.Underline,
        "modifyOtherKeys was misread as a graphic rendition");
    Assert(snapshot.Responses.Any(response => response.Contains("RtlTerminal")),
        "XTVERSION response is missing");
    Assert(snapshot.Responses.Any(response => response.Contains("0+r4d73")),
        "XTGETTCAP response is missing");
    Assert(snapshot.Responses.Count(response => response.EndsWith("$y")) == 6,
        "not every OpenTUI mode query received a response");
}

static void ModernTuiModesAreTracked()
{
    var buffer = new TerminalBuffer(20, 5);
    var active = buffer.Process(
        "\x1b[?1h\x1b[?2004h\x1b[?1002h\x1b[?1006h\x1b[?2026h");
    Assert(active.Modes.ApplicationCursorKeys, "application cursor mode is missing");
    Assert(active.Modes.BracketedPaste, "bracketed paste mode is missing");
    Assert(active.Modes.MouseTrackingMode == 1002 && active.Modes.SgrMouse,
        "SGR mouse mode is missing");
    Assert(active.Modes.SynchronizedOutput, "synchronized output mode is missing");

    var inactive = buffer.Process(
        "\x1b[?1l\x1b[?2004l\x1b[?1002l\x1b[?1006l\x1b[?2026l");
    Assert(!inactive.Modes.ApplicationCursorKeys &&
        !inactive.Modes.BracketedPaste &&
        inactive.Modes.MouseTrackingMode == 0 &&
        !inactive.Modes.SgrMouse &&
        !inactive.Modes.SynchronizedOutput,
        "TUI modes did not reset cleanly");
}

static void EmojiClustersKeepWidth()
{
    var buffer = new TerminalBuffer(20, 5);
    var snapshot = buffer.Process("👨‍💻🇮🇷X");
    var line = snapshot.Lines[0];
    Assert(line.CellLength == 6,
        $"emoji clusters plus cursor occupied {line.CellLength} cells instead of 6");
    Assert(string.Concat(line.Runs.Select(run => run.Text)) == "👨‍💻🇮🇷X ",
        "emoji cluster text was split or lost");
}

static void PersianTextRemainsSmartRtl()
{
    var buffer = new TerminalBuffer(40, 5);
    var snapshot = buffer.Process("status: سلام دنیا 123");
    var line = snapshot.Lines[0];
    Assert(SmartRtl.IsRightToLeft(line), "Persian content was not detected");
    Assert(SmartRtl.ShouldRightAlign(
            line,
            smartRtlEnabled: true,
            preserveTerminalGrid: false),
        "normal terminal output no longer uses full Smart RTL alignment");
    Assert(!SmartRtl.ShouldRightAlign(
            line,
            smartRtlEnabled: true,
            preserveTerminalGrid: true),
        "full-screen TUI grid would be moved by Smart RTL");
    var text = string.Concat(line.Runs.Select(run => run.Text));
    var spans = SmartRtl.GetDirectionalSpans(text, true);
    Assert(spans.Any(span => span.IsRightToLeft) &&
        spans.Any(span => !span.IsRightToLeft),
        "mixed Persian/Latin direction spans were not preserved");
}

static void CarriageReturnIsImmediate()
{
    var buffer = new TerminalBuffer(30, 6);
    var snapshot = buffer.Process("Working\r");
    Assert(snapshot.CursorColumn == 0, "CR left the snapshot cursor at the old column");
    Assert(Text(buffer.Process("\x1b[KDone")).TrimEnd() == "Done", "CR + erase left old text");
}

static void StylePreservesWrap()
{
    var buffer = new TerminalBuffer(10, 6);
    var snapshot = buffer.Process("1234567890\x1b[31mW");
    Assert(string.Concat(snapshot.Lines[0].Runs.Select(run => run.Text)) == "1234567890",
        "SGR cancelled pending wrap and overwrote the last column");
    Assert(snapshot.CursorRow == 1 && snapshot.CursorColumn == 1, "cursor did not wrap");
}

static void StatusRedraw()
{
    const string stream = "Working (0s - esc to interrupt)\r\nold footer" +
        "\x1b[1A\r\x1b[2KWorking 3\x1b[1B\r\x1b[2Knew footer";
    var expected = new TerminalBuffer(40, 6).Process(stream);
    for (var split = 0; split <= stream.Length; split++)
    {
        var buffer = new TerminalBuffer(40, 6);
        buffer.Process(stream[..split]);
        var actual = buffer.Process(stream[split..]);
        Assert(Text(actual) == Text(expected), $"read boundary {split} changed redraw");
    }
    Assert(Text(expected).TrimEnd() == "Working 3\nnew footer", "old status or footer survived redraw");
    var shortUpdate = new TerminalBuffer(30, 6);
    shortUpdate.Process("WWorking\r\x1b[P");
    Assert(Text(shortUpdate.CaptureSnapshot()).TrimEnd() == "Working", "DCH left duplicate W");
}

static void WideErase()
{
    var buffer = new TerminalBuffer(30, 6);
    buffer.Process("👨‍💻Working\r\x1b[2X");
    Assert(Text(buffer.CaptureSnapshot()).TrimEnd() == "  Working", "wide glyph was not erased");
    Assert(Text(buffer.Process("\rOK\x1b[K")).TrimEnd() == "OK", "wide redraw left stale text");
}

static void SnapshotReuse()
{
    var buffer = new TerminalBuffer(40, 8);
    var first = buffer.Process("\x1b[?1049hسلام English\r\nunchanged\x1b[1;1H");
    var second = buffer.Process("\x1b[0m");
    Assert(first.Lines.Zip(second.Lines).All(pair => ReferenceEquals(pair.First, pair.Second)), "unchanged rows were allocated again");
    var edited = buffer.Process("\x1b[1;1H\x1b[31mX");
    Assert(!ReferenceEquals(second.Lines[0], edited.Lines[0]), "cell edit did not invalidate snapshot");
    Assert(ReferenceEquals(second.Lines[1], edited.Lines[1]), "unrelated row was rebuilt");
    var erased = buffer.Process("\x1b[1;1H\x1b[2K");
    Assert(!Text(erased).Contains("سلام"), "erased text survived cache");
    buffer.Resize(60, 10);
    var resized = buffer.Process("\x1b[1;1Hnew");
    Assert(Text(resized).StartsWith("new"), "resize reused stale cells");
    var restored = buffer.Process("\x1b[?1049l");
    Assert(!Text(restored).Contains("unchanged"), "alternate screen cache leaked into main screen");
}

static string Text(TerminalSnapshot snapshot) => string.Join(
    "\n",
    snapshot.Lines.Select(line => string.Concat(line.Runs.Select(run => run.Text))));

static void Assert(bool condition, string message)
{
    if (!condition)
        throw new InvalidOperationException(message);
}

static void DroppedPathsBecomeWslPaths()
{
    Assert(WslPath.SharePath(@"C:\Users\me\a.png", "Ubuntu") is null, "drive paths must use wslpath, not an assumed mount root");
    Assert(WslPath.SharePath(@"\\wsl.localhost\Ubuntu\home\me\x", "Ubuntu") == "/home/me/x", "WSL share path was not converted");
    Assert(WslPath.SharePath(@"\\wsl$\Ubuntu", "Ubuntu") == "/", "WSL share root was not converted");
    try { WslPath.SharePath(@"\\wsl$\Debian\home\me", "Ubuntu"); throw new Exception("cross-distribution path was accepted"); }
    catch (System.IO.IOException) { }
    Assert(WslPath.Quote("/mnt/c/My Files/a (1).png") == "'/mnt/c/My Files/a (1).png'", "shell path was not quoted");
    Assert(WslPath.Quote("/home/me/a'b;$x!.png") == "'/home/me/a'\\''b;$x!.png'", "POSIX apostrophe escaping failed");
    Assert(ShellPath.Windows(@"C:\a&b $x!'c.png", true) == @"'C:\a&b $x!''c.png'", "PowerShell path was not literal");
    Assert(ShellPath.Windows(@"C:\%PATH% & a!.png", false) == "\"C:\\%" + ShellPath.PercentVariable + "%PATH%" + ShellPath.PercentVariable + "% & a!.png\"", "CMD percent/path escaping failed");
    try { WslPath.Quote("/tmp/a\ncommand"); throw new Exception("control characters accepted"); } catch (ArgumentException) { }
    var original = new System.Collections.Hashtable { ["COLORTERM"]="old", ["WSLENV"]="KEEP/p:COLORTERM/w:TERM_PROGRAM/l", ["KEEP"]="value" };
    var env = TerminalEnvironment.Create(original);
    Assert(env["COLORTERM"]=="truecolor" && env["TERM_PROGRAM"]=="RtlTerminal" && env["WSLENV"]=="KEEP/p:COLORTERM:TERM_PROGRAM", "terminal capability forwarding is incorrect");
    Assert((string)original["COLORTERM"]! == "old", "parent environment was mutated");
    if (OperatingSystem.IsWindows())
    {
        const string path = @"C:\a&b $x!'c %RTL_TEST_TOKEN% (1)^.png";
        var cmd = new System.Diagnostics.ProcessStartInfo("cmd.exe", "/D /Q /V:OFF /C echo " + ShellPath.Windows(path, false))
        { UseShellExecute=false, CreateNoWindow=true, RedirectStandardOutput=true, RedirectStandardError=true };
        cmd.Environment[ShellPath.PercentVariable]="%";cmd.Environment["RTL_TEST_TOKEN"]="UNEXPECTED_EXPANSION";
        using(var process=System.Diagnostics.Process.Start(cmd)!)
        {
            var value=process.StandardOutput.ReadToEnd().TrimEnd('\r','\n');process.WaitForExit();
            Assert(process.ExitCode==0 && value=="\""+path+"\"", "CMD did not round-trip special-character path literally");
        }
        var ps = new System.Diagnostics.ProcessStartInfo("powershell.exe") { UseShellExecute=false,CreateNoWindow=true,RedirectStandardOutput=true,RedirectStandardError=true };
        foreach(var arg in new[]{"-NoProfile","-NonInteractive","-Command","& { param($p) [Console]::Write($p) } " + ShellPath.Windows(path,true)})ps.ArgumentList.Add(arg);
        using(var process=System.Diagnostics.Process.Start(ps)!)
        {
            var value=process.StandardOutput.ReadToEnd();process.WaitForExit();
            Assert(process.ExitCode==0 && value==path,"PowerShell did not round-trip special-character path literally");
        }
    }
}
