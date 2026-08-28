using RtlTerminal;

var tests = new (string Name, Action Run)[]
{
    ("alternate screen is isolated and restored", AlternateScreenIsIsolated),
    ("styled trailing spaces remain visible", StyledTrailingSpacesRemainVisible),
    ("modern SGR attributes reset independently", SgrAttributesResetIndependently),
    ("colon truecolor is parsed", ColonTrueColorIsParsed),
    ("terminal capability queries receive replies", CapabilityQueriesReceiveReplies),
    ("OpenTUI capability handshake does not leak", OpenTuiHandshakeDoesNotLeak),
    ("modern TUI modes are tracked", ModernTuiModesAreTracked),
    ("emoji grapheme clusters keep terminal width", EmojiClustersKeepWidth),
    ("Persian text remains detectable for Smart RTL", PersianTextRemainsSmartRtl)
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

static string Text(TerminalSnapshot snapshot) => string.Join(
    "\n",
    snapshot.Lines.Select(line => string.Concat(line.Runs.Select(run => run.Text))));

static void Assert(bool condition, string message)
{
    if (!condition)
        throw new InvalidOperationException(message);
}
