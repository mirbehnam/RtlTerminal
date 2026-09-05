using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace RtlTerminal;

public readonly record struct TerminalColor(byte Red, byte Green, byte Blue);

public readonly record struct TerminalStyle(
    TerminalColor? Foreground,
    TerminalColor? Background,
    bool Bold,
    bool Italic,
    bool Dim,
    bool Underline,
    bool Strikethrough,
    bool Inverse,
    bool Hidden);

public readonly record struct TerminalModes(
    bool AlternateScreen,
    bool ApplicationCursorKeys,
    bool BracketedPaste,
    bool FocusReporting,
    int MouseTrackingMode,
    bool SgrMouse,
    bool SynchronizedOutput);

public sealed record TerminalRun(string Text, TerminalStyle Style);

public sealed record TerminalLine(
    IReadOnlyList<TerminalRun> Runs,
    int CellLength)
{
    internal bool ContainsRightToLeft { get; } =
        SmartRtl.ContainsRightToLeft(Runs);
}

public sealed record TerminalSnapshot(
    IReadOnlyList<TerminalLine> Lines,
    int CursorRow,
    int CursorColumn,
    bool CursorVisible,
    int ScrollbackCount,
    long ScrollbackStartIndex,
    TerminalModes Modes,
    IReadOnlyList<string> Responses,
    long Revision);

public sealed class TerminalBuffer
{
    private const int DefaultMaximumScrollbackRows = 2000;
    private static readonly TerminalColor[] AnsiColors =
    [
        new(12, 12, 12),
        new(197, 15, 31),
        new(19, 161, 14),
        new(193, 156, 0),
        new(0, 55, 218),
        new(136, 23, 152),
        new(58, 150, 221),
        new(204, 204, 204),
        new(118, 118, 118),
        new(231, 72, 86),
        new(22, 198, 12),
        new(249, 241, 165),
        new(59, 120, 255),
        new(180, 0, 158),
        new(97, 214, 214),
        new(242, 242, 242)
    ];

    private readonly object _syncRoot = new();
    private readonly StringBuilder _csi = new();
    private readonly StringBuilder _osc = new();
    private readonly StringBuilder _dcs = new();
    private readonly List<TerminalLine> _scrollback = [];
    private readonly List<string> _pendingResponses = [];
    private int _maximumScrollbackRows;
    private long _scrollbackStartIndex;
    private Cell[,] _cells;
    private bool[] _wrappedFromPrevious;
    private int _columns;
    private int _rows;
    private int _cursorColumn;
    private int _cursorRow;
    private int _savedColumn;
    private int _savedRow;
    private int _scrollTop;
    private int _scrollBottom;
    private long _revision;
    private bool _wrapPending;
    private bool _alternateScreenActive;
    private bool _cursorVisible = true;
    private bool _applicationCursorKeys;
    private bool _autoWrapMode = true;
    private bool _originMode;
    private bool _bracketedPaste;
    private bool _focusReporting;
    private int _mouseTrackingMode;
    private bool _sgrMouse;
    private bool _synchronizedOutput;
    private bool _joinNextCharacter;
    private bool _regionalIndicatorPending;
    private char? _pendingHighSurrogate;
    private ParserState _state;
    private TerminalStyle _currentStyle;
    private SavedScreen? _savedMainScreen;

    public TerminalBuffer(
        int columns,
        int rows,
        int maximumScrollbackRows = DefaultMaximumScrollbackRows)
    {
        _maximumScrollbackRows = NormalizeMaximumScrollbackRows(
            maximumScrollbackRows);
        _columns = Math.Max(10, columns);
        _rows = Math.Max(5, rows);
        _scrollBottom = _rows - 1;
        _cells = new Cell[_rows, _columns];
        _wrappedFromPrevious = new bool[_rows];
        ClearAll();
    }

    public TerminalSnapshot SetMaximumScrollbackRows(int maximumRows)
    {
        lock (_syncRoot)
        {
            _maximumScrollbackRows = NormalizeMaximumScrollbackRows(
                maximumRows);
            TrimScrollback();
            return CreateSnapshot();
        }
    }

    public TerminalSnapshot Process(string text)
    {
        lock (_syncRoot)
        {
            foreach (var character in text)
                ProcessCharacter(character);

            return CreateSnapshot();
        }
    }

    public TerminalSnapshot Resize(int columns, int rows)
    {
        lock (_syncRoot)
        {
            columns = Math.Max(10, columns);
            rows = Math.Max(5, rows);

            if (columns == _columns && rows == _rows)
                return CreateSnapshot();

            var resized = ResizeCells(_cells, columns, rows);
            var resizedWrapState = ResizeWrapState(
                _wrappedFromPrevious,
                rows);

            _cells = resized;
            _wrappedFromPrevious = resizedWrapState;

            if (_savedMainScreen is { } saved)
            {
                _savedMainScreen = saved with
                {
                    Cells = ResizeCells(saved.Cells, columns, rows),
                    WrappedFromPrevious = ResizeWrapState(
                        saved.WrappedFromPrevious,
                        rows),
                    CursorRow = Math.Clamp(saved.CursorRow, 0, rows - 1),
                    CursorColumn = Math.Clamp(saved.CursorColumn, 0, columns - 1),
                    ScrollTop = 0,
                    ScrollBottom = rows - 1
                };
            }

            _columns = columns;
            _rows = rows;
            _scrollTop = 0;
            _scrollBottom = rows - 1;
            _cursorRow = Math.Clamp(_cursorRow, 0, rows - 1);
            _cursorColumn = Math.Clamp(_cursorColumn, 0, columns - 1);
            _wrapPending = false;
            return CreateSnapshot();
        }
    }

    public TerminalSnapshot CaptureSnapshot()
    {
        lock (_syncRoot)
            return CreateSnapshot();
    }

    private void ProcessCharacter(char character)
    {
        if (_state == ParserState.Normal && _pendingHighSurrogate is { } highSurrogate)
        {
            _pendingHighSurrogate = null;

            if (char.IsLowSurrogate(character))
            {
                WriteTextElement(string.Concat(highSurrogate, character));
                return;
            }

            WriteTextElement(highSurrogate.ToString());
        }

        switch (_state)
        {
            case ParserState.Escape:
                ProcessEscape(character);
                return;
            case ParserState.Csi:
                ProcessCsi(character);
                return;
            case ParserState.Osc:
                if (character == '\a')
                {
                    ProcessOsc();
                    _state = ParserState.Normal;
                }
                else if (character == '\x1b')
                    _state = ParserState.OscEscape;
                else if (_osc.Length < 4096)
                    _osc.Append(character);
                return;
            case ParserState.OscEscape:
                if (character == '\\')
                {
                    ProcessOsc();
                    _state = ParserState.Normal;
                }
                else
                {
                    if (_osc.Length < 4096)
                    {
                        _osc.Append('\x1b');
                        _osc.Append(character);
                    }

                    _state = ParserState.Osc;
                }
                return;
            case ParserState.Dcs:
                if (character == '\x1b')
                    _state = ParserState.DcsEscape;
                else if (_dcs.Length < 4096)
                    _dcs.Append(character);
                return;
            case ParserState.DcsEscape:
                if (character == '\\')
                {
                    ProcessDcs();
                    _state = ParserState.Normal;
                }
                else
                {
                    if (_dcs.Length < 4096)
                    {
                        _dcs.Append('\x1b');
                        _dcs.Append(character);
                    }

                    _state = ParserState.Dcs;
                }
                return;
            case ParserState.ControlString:
                if (character == '\x1b')
                    _state = ParserState.ControlStringEscape;
                return;
            case ParserState.ControlStringEscape:
                _state = character == '\\'
                    ? ParserState.Normal
                    : ParserState.ControlString;
                return;
            case ParserState.IgnoreNext:
                _state = ParserState.Normal;
                return;
        }

        switch (character)
        {
            case '\x1b':
                ResetClusterState();
                _state = ParserState.Escape;
                break;
            case '\r':
                ResetClusterState();
                _cursorColumn = 0;
                _wrapPending = false;
                break;
            case '\n':
                ResetClusterState();
                _wrapPending = false;
                LineFeed(wrapped: false);
                break;
            case '\b':
                ResetClusterState();
                _wrapPending = false;
                _cursorColumn = Math.Max(0, _cursorColumn - 1);
                break;
            case '\t':
                ResetClusterState();
                _wrapPending = false;
                _cursorColumn = Math.Min(_columns - 1, ((_cursorColumn / 8) + 1) * 8);
                break;
            case '\0':
            case '\a':
                break;
            default:
                if (char.IsHighSurrogate(character))
                    _pendingHighSurrogate = character;
                else if (!char.IsControl(character))
                    WriteTextElement(character.ToString());
                break;
        }
    }

    private void ProcessEscape(char character)
    {
        switch (character)
        {
            case '[':
                _csi.Clear();
                _state = ParserState.Csi;
                break;
            case ']':
                _osc.Clear();
                _state = ParserState.Osc;
                break;
            case 'P':
                _dcs.Clear();
                _state = ParserState.Dcs;
                break;
            case '_':
            case '^':
            case 'X':
                _state = ParserState.ControlString;
                break;
            case '7':
                SaveCursor();
                _state = ParserState.Normal;
                break;
            case '8':
                RestoreCursor();
                _state = ParserState.Normal;
                break;
            case 'D':
                _wrapPending = false;
                LineFeed(wrapped: false);
                _state = ParserState.Normal;
                break;
            case 'E':
                _wrapPending = false;
                _cursorColumn = 0;
                LineFeed(wrapped: false);
                _state = ParserState.Normal;
                break;
            case 'M':
                _wrapPending = false;
                ReverseIndex();
                _state = ParserState.Normal;
                break;
            case '(':
            case ')':
            case '#':
                _state = ParserState.IgnoreNext;
                break;
            default:
                _state = ParserState.Normal;
                break;
        }
    }

    private void ProcessCsi(char character)
    {
        if (character is >= '@' and <= '~')
        {
            ExecuteCsi(character, _csi.ToString());
            _csi.Clear();
            _state = ParserState.Normal;
            return;
        }

        if (_csi.Length < 128)
            _csi.Append(character);
    }

    private void ProcessOsc()
    {
        var payload = _osc.ToString();
        _osc.Clear();
        var separator = payload.IndexOf(';');

        if (separator <= 0)
            return;

        if (!int.TryParse(payload[..separator], out var command))
            return;

        var value = payload[(separator + 1)..];

        switch (command)
        {
            case 4:
                // OSC 4 normally contains a palette index before the query.
                // It is handled below when that extended form is present.
                break;
            case 10:
                if (value != "?")
                    return;
                QueueResponse($"\x1b]10;{FormatOscColor(AnsiColors[7])}\x1b\\");
                return;
            case 11:
                if (value != "?")
                    return;
                QueueResponse($"\x1b]11;{FormatOscColor(AnsiColors[0])}\x1b\\");
                return;
            case 12:
                if (value != "?")
                    return;
                QueueResponse($"\x1b]12;{FormatOscColor(AnsiColors[7])}\x1b\\");
                return;
        }

        if (!payload.StartsWith("4;", StringComparison.Ordinal))
            return;

        var parts = payload.Split(';');

        if (parts.Length >= 3 &&
            parts[2] == "?" &&
            int.TryParse(parts[1], out var paletteIndex))
        {
            QueueResponse(
                $"\x1b]4;{paletteIndex};" +
                $"{FormatOscColor(Get256Color(paletteIndex))}\x1b\\");
        }
    }

    private void ResetClusterState()
    {
        _joinNextCharacter = false;
        _regionalIndicatorPending = false;
    }

    private void ProcessDcs()
    {
        var payload = _dcs.ToString();
        _dcs.Clear();

        if (!payload.StartsWith("+q", StringComparison.Ordinal))
            return;

        var capability = payload[2..];

        if (capability.Length > 0)
            QueueResponse($"\x1bP0+r{capability}\x1b\\");
    }

    private static string FormatOscColor(TerminalColor color) =>
        $"rgb:{color.Red:x2}{color.Red:x2}/" +
        $"{color.Green:x2}{color.Green:x2}/" +
        $"{color.Blue:x2}{color.Blue:x2}";

    private void QueueResponse(string response)
    {
        _pendingResponses.Add(response);
    }

    private void ExecuteCsi(char command, string parameterText)
    {
        var privateMode = parameterText.Length > 0 && parameterText[0] == '?';
        var parameters = ParseParameters(parameterText);
        // Styling and capability queries do not move the cursor. In particular,
        // SGR after the last column must not make the next glyph overwrite it.
        if (command is 'A' or 'B' or 'C' or 'D' or 'E' or 'F' or 'G' or '`' or
            'd' or 'H' or 'f' or 'J' or 'K' or 'P' or '@' or 'X' or 'L' or 'M' or 'S' or 'T' or 'r')
            _wrapPending = false;

        switch (command)
        {
            case 'A':
                _cursorRow -= GetParameter(parameters, 0, 1);
                break;
            case 'B':
                _cursorRow += GetParameter(parameters, 0, 1);
                break;
            case 'C':
                _cursorColumn += GetParameter(parameters, 0, 1);
                break;
            case 'D':
                _cursorColumn -= GetParameter(parameters, 0, 1);
                break;
            case 'E':
                _cursorRow += GetParameter(parameters, 0, 1);
                _cursorColumn = 0;
                break;
            case 'F':
                _cursorRow -= GetParameter(parameters, 0, 1);
                _cursorColumn = 0;
                break;
            case 'G':
            case '`':
                _cursorColumn = GetParameter(parameters, 0, 1) - 1;
                break;
            case 'd':
                _cursorRow = GetRowPosition(parameters, 0);
                break;
            case 'H':
            case 'f':
                _cursorRow = GetRowPosition(parameters, 0);
                _cursorColumn = GetParameter(parameters, 1, 1) - 1;
                break;
            case 'J':
                EraseDisplay(GetParameter(parameters, 0, 0));
                break;
            case 'K':
                EraseLine(GetParameter(parameters, 0, 0));
                break;
            case 'P':
                DeleteCharacters(GetParameter(parameters, 0, 1));
                break;
            case '@':
                InsertCharacters(GetParameter(parameters, 0, 1));
                break;
            case 'X':
                EraseCharacters(GetParameter(parameters, 0, 1));
                break;
            case 'L':
                InsertLines(GetParameter(parameters, 0, 1));
                break;
            case 'M':
                DeleteLines(GetParameter(parameters, 0, 1));
                break;
            case 'S':
                ScrollUp(GetParameter(parameters, 0, 1));
                break;
            case 'T':
                ScrollDown(GetParameter(parameters, 0, 1));
                break;
            case 'r':
                SetScrollRegion(parameters);
                break;
            case 'm':
                if (parameterText.StartsWith(">4;", StringComparison.Ordinal))
                {
                    // xterm modifyOtherKeys changes input encoding only; it
                    // is not an SGR sequence and must never alter text style.
                }
                else if (!parameterText.StartsWith('>'))
                    SetGraphicsRendition(parameterText);
                break;
            case 's':
                SaveCursor();
                break;
            case 'u':
                if (parameterText.Trim() == "?")
                    QueueResponse("\x1b[?0u");
                else if (parameterText.StartsWith('>') ||
                    parameterText.StartsWith('<'))
                {
                    // Kitty's keyboard protocol is deliberately not
                    // advertised. Ignore push/pop requests without moving
                    // the cursor if an application sends them anyway.
                }
                else
                    RestoreCursor();
                break;
            case 'n':
                HandleDeviceStatusReport(privateMode, parameters);
                break;
            case 'c':
                HandleDeviceAttributes(parameterText);
                break;
            case 'p':
                HandleModeQuery(parameterText);
                break;
            case 't':
                HandleWindowReport(parameters);
                break;
            case 'q':
                if (parameterText.StartsWith(">0", StringComparison.Ordinal))
                {
                    QueueResponse(
                        "\x1bP>|RtlTerminal(1.0.5)\x1b\\");
                }
                break;
            case 'h':
            case 'l':
                if (privateMode)
                    HandlePrivateMode(parameters, command == 'h');
                break;
        }

        ClampCursor();

    }

    private int GetRowPosition(IReadOnlyList<int> parameters, int index)
    {
        var row = GetParameter(parameters, index, 1) - 1;
        return _originMode ? _scrollTop + row : row;
    }

    private void HandleDeviceStatusReport(
        bool privateMode,
        IReadOnlyList<int> parameters)
    {
        var code = GetParameter(parameters, 0, 0);

        if (code == 5 && !privateMode)
        {
            QueueResponse("\x1b[0n");
            return;
        }

        if (code != 6)
            return;

        var row = _originMode
            ? _cursorRow - _scrollTop + 1
            : _cursorRow + 1;
        var prefix = privateMode ? "?" : string.Empty;
        QueueResponse($"\x1b[{prefix}{row};{_cursorColumn + 1}R");
    }

    private void HandleDeviceAttributes(string parameterText)
    {
        if (parameterText.StartsWith('>'))
            QueueResponse("\x1b[>0;10;1c");
        else if (!parameterText.StartsWith('='))
            QueueResponse("\x1b[?1;2c");
    }

    private void HandleModeQuery(string parameterText)
    {
        if (!parameterText.StartsWith('?') || !parameterText.EndsWith('$'))
            return;

        if (!int.TryParse(parameterText[1..^1], out var mode))
            return;

        var supported = mode is 1 or 6 or 7 or 25 or 47 or 1000 or 1002 or
            1003 or 1004 or 1006 or 1047 or 1048 or 1049 or 2004 or 2026;
        var enabled = IsPrivateModeEnabled(mode);
        var status = supported ? enabled ? 1 : 2 : 0;
        QueueResponse($"\x1b[?{mode};{status}$y");
    }

    private void HandleWindowReport(IReadOnlyList<int> parameters)
    {
        switch (GetParameter(parameters, 0, 0))
        {
            case 14:
                QueueResponse($"\x1b[4;{_rows * 18};{_columns * 9}t");
                break;
            case 16:
                QueueResponse("\x1b[6;18;9t");
                break;
            case 18:
                QueueResponse($"\x1b[8;{_rows};{_columns}t");
                break;
        }
    }

    private static List<int> ParseParameters(string text)
    {
        var parameters = new List<int>();
        text = text.TrimStart('?', '>', '!', '=');

        if (string.IsNullOrEmpty(text))
            return parameters;

        foreach (var part in text.Split(';'))
            parameters.Add(int.TryParse(part, out var value) ? value : 0);

        return parameters;
    }

    private static int GetParameter(IReadOnlyList<int> parameters, int index, int defaultValue)
    {
        return index >= parameters.Count || parameters[index] == 0
            ? defaultValue
            : parameters[index];
    }

    private void WriteTextElement(string text)
    {
        var rune = Rune.GetRuneAt(text, 0);

        if (_joinNextCharacter)
        {
            AppendCombiningCharacter(text);
            _joinNextCharacter = false;
            _regionalIndicatorPending = false;
            return;
        }

        if (rune.Value == 0x200d)
        {
            AppendCombiningCharacter(text);
            _joinNextCharacter = true;
            return;
        }

        if (IsRegionalIndicator(rune.Value))
        {
            if (_regionalIndicatorPending)
            {
                AppendCombiningCharacter(text);
                _regionalIndicatorPending = false;
                return;
            }

            _regionalIndicatorPending = true;
        }
        else
        {
            _regionalIndicatorPending = false;
        }

        if (IsCombining(text))
        {
            AppendCombiningCharacter(text);
            return;
        }

        if (_wrapPending)
        {
            if (_autoWrapMode)
            {
                _cursorColumn = 0;
                LineFeed(wrapped: true);
            }

            _wrapPending = false;
        }

        var width = GetCellWidth(rune);

        if (width == 2 && _cursorColumn == _columns - 1)
        {
            _cursorColumn = 0;
            LineFeed(wrapped: true);
        }

        // A redraw may replace only one half of an existing wide glyph.
        // Clear any wide character crossing either edge of the cells that
        // are about to be overwritten, otherwise later erase commands can
        // leave stale cells behind or erase the newly written character.
        NormalizeWideCharacterBoundary(_cursorRow, _cursorColumn);
        NormalizeWideCharacterBoundary(_cursorRow, _cursorColumn + width);

        _cells[_cursorRow, _cursorColumn] = new Cell(text, _currentStyle, false);

        if (width == 2 && _cursorColumn + 1 < _columns)
            _cells[_cursorRow, _cursorColumn + 1] = new Cell(string.Empty, _currentStyle, true);

        if (_cursorColumn + width >= _columns)
        {
            _cursorColumn = _columns - 1;
            _wrapPending = _autoWrapMode;
        }
        else
        {
            _cursorColumn += width;
        }
    }

    private void AppendCombiningCharacter(string text)
    {
        var column = _wrapPending ? _cursorColumn : _cursorColumn - 1;

        while (column >= 0 && _cells[_cursorRow, column].Continuation)
            column--;

        if (column >= 0 && !string.IsNullOrEmpty(_cells[_cursorRow, column].Text))
        {
            var cell = _cells[_cursorRow, column];
            _cells[_cursorRow, column] = cell with { Text = cell.Text + text };
        }
    }

    private static bool IsCombining(string text)
    {
        return GetCellWidth(Rune.GetRuneAt(text, 0)) == 0;
    }

    internal static int GetCellWidth(Rune rune)
    {
        var category = Rune.GetUnicodeCategory(rune);

        if (category is UnicodeCategory.NonSpacingMark
            or UnicodeCategory.SpacingCombiningMark
            or UnicodeCategory.EnclosingMark)
        {
            return 0;
        }

        if (rune.Value is 0x200b or 0x200c or 0x200d or 0x2060 or 0xfeff ||
            rune.Value is >= 0x1f3fb and <= 0x1f3ff ||
            category == UnicodeCategory.Format)
        {
            return 0;
        }

        return IsWide(rune.Value) ? 2 : 1;
    }

    private static bool IsRegionalIndicator(int value) =>
        value is >= 0x1f1e6 and <= 0x1f1ff;

    private static bool IsWide(int value)
    {
        return value is >= 0x1100 and <= 0x115f
            or >= 0x2329 and <= 0x232a
            or >= 0x2e80 and <= 0xa4cf
            or >= 0xac00 and <= 0xd7a3
            or >= 0xf900 and <= 0xfaff
            or >= 0xfe10 and <= 0xfe19
            or >= 0xfe30 and <= 0xfe6f
            or >= 0xff00 and <= 0xff60
            or >= 0xffe0 and <= 0xffe6
            or >= 0x1f1e6 and <= 0x1f1ff
            or >= 0x1f300 and <= 0x1faff
            or >= 0x20000 and <= 0x3fffd;
    }

    private void SetGraphicsRendition(string parameterText)
    {
        var parameters = ParseGraphicsParameters(parameterText);

        for (var index = 0; index < parameters.Count; index++)
        {
            var code = parameters[index] ?? 0;

            switch (code)
            {
                case 0:
                    _currentStyle = default;
                    break;
                case 1:
                    _currentStyle = _currentStyle with { Bold = true };
                    break;
                case 2:
                    _currentStyle = _currentStyle with { Dim = true };
                    break;
                case 3:
                    _currentStyle = _currentStyle with { Italic = true };
                    break;
                case 4:
                case 21:
                    _currentStyle = _currentStyle with { Underline = true };
                    break;
                case 7:
                    _currentStyle = _currentStyle with { Inverse = true };
                    break;
                case 8:
                    _currentStyle = _currentStyle with { Hidden = true };
                    break;
                case 9:
                    _currentStyle = _currentStyle with { Strikethrough = true };
                    break;
                case 22:
                    _currentStyle = _currentStyle with
                    {
                        Bold = false,
                        Dim = false
                    };
                    break;
                case 23:
                    _currentStyle = _currentStyle with { Italic = false };
                    break;
                case 24:
                    _currentStyle = _currentStyle with { Underline = false };
                    break;
                case 27:
                    _currentStyle = _currentStyle with { Inverse = false };
                    break;
                case 28:
                    _currentStyle = _currentStyle with { Hidden = false };
                    break;
                case 29:
                    _currentStyle = _currentStyle with { Strikethrough = false };
                    break;
                case 39:
                    _currentStyle = _currentStyle with { Foreground = null };
                    break;
                case 49:
                    _currentStyle = _currentStyle with { Background = null };
                    break;
                case >= 30 and <= 37:
                    _currentStyle = _currentStyle with { Foreground = AnsiColors[code - 30] };
                    break;
                case >= 40 and <= 47:
                    _currentStyle = _currentStyle with { Background = AnsiColors[code - 40] };
                    break;
                case >= 90 and <= 97:
                    _currentStyle = _currentStyle with { Foreground = AnsiColors[code - 90 + 8] };
                    break;
                case >= 100 and <= 107:
                    _currentStyle = _currentStyle with { Background = AnsiColors[code - 100 + 8] };
                    break;
                case 38:
                    if (TryReadExtendedColor(parameters, ref index, out var foreground))
                        _currentStyle = _currentStyle with { Foreground = foreground };
                    break;
                case 48:
                    if (TryReadExtendedColor(parameters, ref index, out var background))
                        _currentStyle = _currentStyle with { Background = background };
                    break;
            }
        }
    }

    private static List<int?> ParseGraphicsParameters(string text)
    {
        if (string.IsNullOrEmpty(text))
            return [0];

        var parameters = new List<int?>();

        foreach (var section in text.TrimStart('?', '>', '!', '=').Split(';'))
        {
            foreach (var part in section.Split(':'))
            {
                parameters.Add(int.TryParse(part, out var value)
                    ? value
                    : null);
            }
        }

        return parameters;
    }

    private static bool TryReadExtendedColor(
        IReadOnlyList<int?> parameters,
        ref int index,
        out TerminalColor color)
    {
        color = default;

        if (index + 2 < parameters.Count && parameters[index + 1] == 5)
        {
            color = Get256Color(parameters[index + 2] ?? 0);
            index += 2;
            return true;
        }

        if (index + 4 < parameters.Count && parameters[index + 1] == 2)
        {
            var componentStart = index + 2;

            // ISO-8613-6's colon form can include an omitted color-space id:
            // 38:2::R:G:B. The semicolon form starts directly at R.
            if (parameters[componentStart] is null)
                componentStart++;

            if (componentStart + 2 >= parameters.Count)
                return false;

            color = new(
                (byte)Math.Clamp(parameters[componentStart] ?? 0, 0, 255),
                (byte)Math.Clamp(parameters[componentStart + 1] ?? 0, 0, 255),
                (byte)Math.Clamp(parameters[componentStart + 2] ?? 0, 0, 255));
            index = componentStart + 2;
            return true;
        }

        return false;
    }

    private static TerminalColor Get256Color(int index)
    {
        index = Math.Clamp(index, 0, 255);

        if (index < 16)
            return AnsiColors[index];

        if (index >= 232)
        {
            var gray = (byte)(8 + ((index - 232) * 10));
            return new(gray, gray, gray);
        }

        var cubeIndex = index - 16;
        return new(
            ColorCubeValue(cubeIndex / 36),
            ColorCubeValue((cubeIndex / 6) % 6),
            ColorCubeValue(cubeIndex % 6));
    }

    private static byte ColorCubeValue(int value)
    {
        return value == 0 ? (byte)0 : (byte)(55 + (value * 40));
    }

    private void HandlePrivateMode(IReadOnlyList<int> parameters, bool enabled)
    {
        foreach (var mode in parameters)
        {
            switch (mode)
            {
                case 1:
                    _applicationCursorKeys = enabled;
                    break;
                case 6:
                    _originMode = enabled;
                    _cursorRow = enabled ? _scrollTop : 0;
                    _cursorColumn = 0;
                    break;
                case 7:
                    _autoWrapMode = enabled;
                    _wrapPending = false;
                    break;
                case 25:
                    _cursorVisible = enabled;
                    break;
                case 47:
                case 1047:
                case 1049:
                    SetAlternateScreen(enabled);
                    break;
                case 1048:
                    if (enabled)
                        SaveCursor();
                    else
                        RestoreCursor();
                    break;
                case 1000:
                case 1002:
                case 1003:
                    if (enabled)
                        _mouseTrackingMode = mode;
                    else if (_mouseTrackingMode == mode)
                        _mouseTrackingMode = 0;
                    break;
                case 1004:
                    _focusReporting = enabled;
                    break;
                case 1006:
                    _sgrMouse = enabled;
                    break;
                case 2004:
                    _bracketedPaste = enabled;
                    break;
                case 2026:
                    _synchronizedOutput = enabled;
                    break;
            }
        }
    }

    private bool IsPrivateModeEnabled(int mode) =>
        mode switch
        {
            1 => _applicationCursorKeys,
            6 => _originMode,
            7 => _autoWrapMode,
            25 => _cursorVisible,
            47 or 1047 or 1049 => _alternateScreenActive,
            1000 or 1002 or 1003 => _mouseTrackingMode == mode,
            1004 => _focusReporting,
            1006 => _sgrMouse,
            2004 => _bracketedPaste,
            2026 => _synchronizedOutput,
            _ => false
        };

    private void SetAlternateScreen(bool enabled)
    {
        if (enabled && !_alternateScreenActive)
        {
            _savedMainScreen = new SavedScreen(
                _cells,
                _wrappedFromPrevious,
                _cursorRow,
                _cursorColumn,
                _savedRow,
                _savedColumn,
                _scrollTop,
                _scrollBottom,
                _currentStyle,
                _originMode,
                _autoWrapMode);
            _cells = new Cell[_rows, _columns];
            _wrappedFromPrevious = new bool[_rows];
            Fill(_cells);
            _alternateScreenActive = true;
            _currentStyle = default;
            _originMode = false;
            _autoWrapMode = true;
            ClearAll();
            _cursorRow = 0;
            _cursorColumn = 0;
            _scrollTop = 0;
            _scrollBottom = _rows - 1;
            return;
        }

        if (!enabled && _alternateScreenActive && _savedMainScreen is { } saved)
        {
            _cells = saved.Cells;
            _wrappedFromPrevious = saved.WrappedFromPrevious;
            _cursorRow = saved.CursorRow;
            _cursorColumn = saved.CursorColumn;
            _savedRow = saved.SavedRow;
            _savedColumn = saved.SavedColumn;
            _scrollTop = saved.ScrollTop;
            _scrollBottom = saved.ScrollBottom;
            _currentStyle = saved.Style;
            _originMode = saved.OriginMode;
            _autoWrapMode = saved.AutoWrapMode;
            _savedMainScreen = null;
            _alternateScreenActive = false;
            _wrapPending = false;
        }
    }

    private void SetScrollRegion(IReadOnlyList<int> parameters)
    {
        var top = GetParameter(parameters, 0, 1) - 1;
        var bottom = GetParameter(parameters, 1, _rows) - 1;

        if (top < bottom && top >= 0 && bottom < _rows)
        {
            _scrollTop = top;
            _scrollBottom = bottom;
            _cursorRow = _originMode ? _scrollTop : 0;
            _cursorColumn = 0;
        }
    }

    private void SaveCursor()
    {
        _savedColumn = _cursorColumn;
        _savedRow = _cursorRow;
    }

    private void RestoreCursor()
    {
        _wrapPending = false;
        _cursorColumn = _savedColumn;
        _cursorRow = _savedRow;
        ClampCursor();
    }

    private void LineFeed(bool wrapped)
    {
        if (_cursorRow == _scrollBottom)
        {
            ScrollRegionUp(_scrollTop, _scrollBottom, 1);
            _wrappedFromPrevious[_cursorRow] = wrapped;
            return;
        }

        _cursorRow = Math.Min(_rows - 1, _cursorRow + 1);
        _wrappedFromPrevious[_cursorRow] = wrapped;
    }

    private void ReverseIndex()
    {
        if (_cursorRow == _scrollTop)
        {
            ScrollRegionDown(_scrollTop, _scrollBottom, 1);
            return;
        }

        _cursorRow = Math.Max(0, _cursorRow - 1);
    }

    private void ScrollUp(int count) => ScrollRegionUp(_scrollTop, _scrollBottom, count);

    private void ScrollDown(int count) => ScrollRegionDown(_scrollTop, _scrollBottom, count);

    private void ScrollRegionUp(int top, int bottom, int count)
    {
        count = Math.Clamp(count, 1, bottom - top + 1);

        if (!_alternateScreenActive &&
            top == 0 &&
            bottom == _rows - 1)
        {
            for (var row = 0; row < count; row++)
                AddScrollbackRow(row);
        }

        for (var row = top; row <= bottom - count; row++)
        {
            CopyRow(row + count, row);
            _wrappedFromPrevious[row] =
                _wrappedFromPrevious[row + count];
        }

        for (var row = bottom - count + 1; row <= bottom; row++)
            ClearRow(row);
    }

    private void ScrollRegionDown(int top, int bottom, int count)
    {
        count = Math.Clamp(count, 1, bottom - top + 1);

        for (var row = bottom; row >= top + count; row--)
        {
            CopyRow(row - count, row);
            _wrappedFromPrevious[row] =
                _wrappedFromPrevious[row - count];
        }

        for (var row = top; row < top + count; row++)
            ClearRow(row);
    }

    private void InsertLines(int count)
    {
        if (_cursorRow < _scrollTop || _cursorRow > _scrollBottom)
            return;

        ScrollRegionDown(_cursorRow, _scrollBottom, count);
    }

    private void DeleteLines(int count)
    {
        if (_cursorRow < _scrollTop || _cursorRow > _scrollBottom)
            return;

        ScrollRegionUp(_cursorRow, _scrollBottom, count);
    }

    private void EraseDisplay(int mode)
    {
        if (mode is 2 or 3)
        {
            ClearAll();

            if (mode == 3 && !_alternateScreenActive)
            {
                _scrollbackStartIndex += _scrollback.Count;
                _scrollback.Clear();
            }

            return;
        }

        if (mode == 1)
        {
            for (var row = 0; row < _cursorRow; row++)
                ClearRow(row);

            ClearCells(_cursorRow, 0, _cursorColumn + 1);
            return;
        }

        ClearCells(_cursorRow, _cursorColumn, _columns - _cursorColumn);

        for (var row = _cursorRow + 1; row < _rows; row++)
            ClearRow(row);
    }

    private void EraseLine(int mode)
    {
        if (mode == 2)
            ClearRow(_cursorRow);
        else if (mode == 1)
            ClearCells(_cursorRow, 0, _cursorColumn + 1);
        else
            ClearCells(_cursorRow, _cursorColumn, _columns - _cursorColumn);
    }

    private void DeleteCharacters(int count)
    {
        count = Math.Clamp(count, 1, _columns - _cursorColumn);
        NormalizeWideCharacterBoundary(_cursorRow, _cursorColumn);
        NormalizeWideCharacterBoundary(_cursorRow, _cursorColumn + count);

        for (var column = _cursorColumn; column < _columns - count; column++)
            _cells[_cursorRow, column] = _cells[_cursorRow, column + count];

        ClearCells(_cursorRow, _columns - count, count);
        NormalizeRow(_cursorRow);
    }

    private void InsertCharacters(int count)
    {
        count = Math.Clamp(count, 1, _columns - _cursorColumn);
        NormalizeWideCharacterBoundary(_cursorRow, _cursorColumn);

        for (var column = _columns - 1; column >= _cursorColumn + count; column--)
            _cells[_cursorRow, column] = _cells[_cursorRow, column - count];

        ClearCells(_cursorRow, _cursorColumn, count);
        NormalizeRow(_cursorRow);
    }

    private void EraseCharacters(int count)
    {
        count = Math.Clamp(count, 1, _columns - _cursorColumn);
        ClearCells(_cursorRow, _cursorColumn, count);
    }

    private void ClearAll()
    {
        for (var row = 0; row < _rows; row++)
            ClearRow(row);
    }

    private void ClearRow(int row)
    {
        ClearCells(row, 0, _columns);
        _wrappedFromPrevious[row] = false;
    }

    private void ClearCells(int row, int start, int count)
    {
        var end = Math.Min(_columns, start + count);

        if (start > 0 && _cells[row, start].Continuation)
            start--;

        if (end < _columns && _cells[row, end].Continuation)
            end++;

        var emptyCell = new Cell(" ", _currentStyle, false);

        for (var column = Math.Max(0, start); column < end; column++)
            _cells[row, column] = emptyCell;
    }

    private void NormalizeWideCharacterBoundary(int row, int column)
    {
        if (column <= 0 || column >= _columns)
            return;

        if (_cells[row, column].Continuation)
        {
            _cells[row, column - 1] = new Cell(" ", _currentStyle, false);
            _cells[row, column] = new Cell(" ", _currentStyle, false);
        }
    }

    private void NormalizeRow(int row)
    {
        for (var column = 0; column < _columns; column++)
        {
            if (!_cells[row, column].Continuation)
                continue;

            if (column == 0 || string.IsNullOrWhiteSpace(_cells[row, column - 1].Text))
                _cells[row, column] = new Cell(" ", _currentStyle, false);
        }
    }

    private void CopyRow(int source, int destination)
    {
        for (var column = 0; column < _columns; column++)
            _cells[destination, column] = _cells[source, column];
    }

    private static void Fill(Cell[,] cells)
    {
        for (var row = 0; row < cells.GetLength(0); row++)
        {
            for (var column = 0; column < cells.GetLength(1); column++)
                cells[row, column] = new Cell(" ", default, false);
        }
    }

    private static Cell[,] ResizeCells(Cell[,] source, int columns, int rows)
    {
        var resized = new Cell[rows, columns];
        Fill(resized);
        var copiedRows = Math.Min(rows, source.GetLength(0));
        var copiedColumns = Math.Min(columns, source.GetLength(1));

        for (var row = 0; row < copiedRows; row++)
        {
            for (var column = 0; column < copiedColumns; column++)
                resized[row, column] = source[row, column];
        }

        return resized;
    }

    private static bool[] ResizeWrapState(bool[] source, int rows)
    {
        var resized = new bool[rows];
        Array.Copy(source, resized, Math.Min(source.Length, rows));
        return resized;
    }

    private void ClampCursor()
    {
        _cursorColumn = Math.Clamp(_cursorColumn, 0, _columns - 1);
        _cursorRow = _originMode
            ? Math.Clamp(_cursorRow, _scrollTop, _scrollBottom)
            : Math.Clamp(_cursorRow, 0, _rows - 1);
    }

    private TerminalSnapshot CreateSnapshot()
    {
        var lastVisibleRow = _alternateScreenActive
            ? _rows - 1
            : _cursorRow;

        for (var row = _rows - 1;
             !_alternateScreenActive && row >= 0;
             row--)
        {
            if (FindLastCharacter(row) >= 0)
            {
                lastVisibleRow = Math.Max(lastVisibleRow, row);
                break;
            }
        }

        var lines = new List<TerminalLine>(
            (_alternateScreenActive ? 0 : _scrollback.Count) +
            lastVisibleRow + 1);

        if (!_alternateScreenActive)
            lines.AddRange(_scrollback);

        for (var row = 0; row <= lastVisibleRow; row++)
        {
            var lastCharacter = FindLastCharacter(row);
            var cellLength = lastCharacter + 1;

            if (row == _cursorRow)
                cellLength = Math.Max(cellLength, _cursorColumn + 1);

            lines.Add(CreateLine(_cells, row, cellLength));
        }

        var scrollbackCount = _alternateScreenActive ? 0 : _scrollback.Count;
        var responses = _pendingResponses.ToArray();
        _pendingResponses.Clear();

        return new TerminalSnapshot(
            lines,
            scrollbackCount + _cursorRow,
            _cursorColumn,
            _cursorVisible,
            scrollbackCount,
            _scrollbackStartIndex,
            new TerminalModes(
                _alternateScreenActive,
                _applicationCursorKeys,
                _bracketedPaste,
                _focusReporting,
                _mouseTrackingMode,
                _sgrMouse,
                _synchronizedOutput),
            responses,
            ++_revision);
    }

    private TerminalLine CreateLine(Cell[] cells, int cellLength)
    {
        var lastCharacter = FindLastCharacter(cells);
        return new TerminalLine(
            CreateRuns(cells, Math.Min(cellLength, lastCharacter + 1)),
            cellLength);
    }

    private TerminalLine CreateLine(
        Cell[,] cells,
        int row,
        int cellLength)
    {
        return new TerminalLine(
            CreateRuns(cells, row, cellLength),
            cellLength);
    }

    private static IReadOnlyList<TerminalRun> CreateRuns(
        Cell[] cells,
        int cellLength)
    {
        var runs = new List<TerminalRun>();
        var text = new StringBuilder();
        TerminalStyle? style = null;

        for (var column = 0; column < cellLength; column++)
            AppendCellToRuns(cells[column], runs, text, ref style);

        FlushRun(runs, text, style);
        return runs;
    }

    private IReadOnlyList<TerminalRun> CreateRuns(
        Cell[,] cells,
        int row,
        int cellLength)
    {
        var runs = new List<TerminalRun>();
        var text = new StringBuilder();
        TerminalStyle? style = null;

        for (var column = 0; column < cellLength; column++)
            AppendCellToRuns(cells[row, column], runs, text, ref style);

        FlushRun(runs, text, style);
        return runs;
    }

    private static void AppendCellToRuns(
        Cell cell,
        ICollection<TerminalRun> runs,
        StringBuilder text,
        ref TerminalStyle? style)
    {
        if (cell.Continuation)
            return;

        if (style is not null && style.Value != cell.Style)
        {
            runs.Add(new TerminalRun(text.ToString(), style.Value));
            text.Clear();
        }

        style = cell.Style;
        text.Append(cell.Text);
    }

    private static void FlushRun(
        ICollection<TerminalRun> runs,
        StringBuilder text,
        TerminalStyle? style)
    {
        if (text.Length > 0)
            runs.Add(new TerminalRun(text.ToString(), style ?? default));
    }

    private int FindLastCharacter(int row)
    {
        for (var column = _columns - 1; column >= 0; column--)
        {
            var cell = _cells[row, column];

            if (cell.Continuation ||
                cell.Text != " " ||
                HasVisibleStyle(cell.Style))
                return column;
        }

        return -1;
    }

    private static int FindLastCharacter(Cell[] row)
    {
        for (var column = row.Length - 1; column >= 0; column--)
        {
            if (row[column].Continuation ||
                row[column].Text != " " ||
                HasVisibleStyle(row[column].Style))
                return column;
        }

        return -1;
    }

    private static bool HasVisibleStyle(TerminalStyle style) =>
        style.Background is not null ||
        style.Inverse ||
        style.Underline ||
        style.Strikethrough;

    private void AddScrollbackRow(int row)
    {
        var copy = new Cell[_columns];

        for (var column = 0; column < _columns; column++)
            copy[column] = _cells[row, column];

        _scrollback.Add(CreateLine(copy, copy.Length));

        if (_scrollback.Count > _maximumScrollbackRows + 128)
            TrimScrollback();
    }

    private void TrimScrollback()
    {
        if (_scrollback.Count <= _maximumScrollbackRows)
            return;

        var removeCount =
            _scrollback.Count - _maximumScrollbackRows;
        _scrollback.RemoveRange(0, removeCount);
        _scrollbackStartIndex += removeCount;
    }

    private static int NormalizeMaximumScrollbackRows(int maximumRows)
    {
        return maximumRows is 2000 or 5000 or 10000
            ? maximumRows
            : DefaultMaximumScrollbackRows;
    }

    private readonly record struct Cell(
        string Text,
        TerminalStyle Style,
        bool Continuation);

    private sealed record SavedScreen(
        Cell[,] Cells,
        bool[] WrappedFromPrevious,
        int CursorRow,
        int CursorColumn,
        int SavedRow,
        int SavedColumn,
        int ScrollTop,
        int ScrollBottom,
        TerminalStyle Style,
        bool OriginMode,
        bool AutoWrapMode);

    private enum ParserState
    {
        Normal,
        Escape,
        Csi,
        Osc,
        OscEscape,
        Dcs,
        DcsEscape,
        ControlString,
        ControlStringEscape,
        IgnoreNext
    }
}
