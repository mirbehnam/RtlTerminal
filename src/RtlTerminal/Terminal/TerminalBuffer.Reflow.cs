using System.Globalization;
using System.Text;

namespace RtlTerminal;

public sealed partial class TerminalBuffer
{
    private sealed record ReflowedScreen(Cell[,] Cells, bool[] Wraps, int CursorRow,
        int CursorColumn, bool WrapPending, int SavedRow, int SavedColumn);

    // Rebuild logical paragraphs, joining only automatic wraps. A smaller viewport
    // moves overflow into retained history rather than cropping columns or rows.
    private ReflowedScreen ReflowMainScreen(Cell[,] screen, bool[] wraps, int cursorRow,
        int cursorColumn, bool wrapPending, int savedRow, int savedColumn, int columns, int rows)
    {
        var output = new List<Cell[]>();
        var outputWraps = new List<bool>();
        var column = 0;
        var mappedCursor = (Row: 0, Column: 0, Pending: false);
        var mappedSaved = (Row: 0, Column: 0, Pending: false);
        Cell[] NewRow(bool wrapped)
        {
            var row = new Cell[columns];
            Array.Fill(row, new Cell(" ", default, false));
            output.Add(row); outputWraps.Add(wrapped); column = 0;
            return row;
        }
        void StartRow(bool wrapped)
        {
            if (output.Count == 0 || !wrapped) NewRow(wrapped);
        }
        (int Row, int Column, bool Pending) Position() =>
            (output.Count - 1, Math.Min(column, columns - 1), column == columns);
        void Put(Cell cell, int width, Action? map = null)
        {
            if (column + width > columns)
            {
                // A wide glyph's unused edge cell is layout padding, not a
                // literal space to accumulate on subsequent narrow/wide resizes.
                if (column < columns) output[^1][column] = new Cell(string.Empty, default, false);
                NewRow(true);
            }
            map?.Invoke();
            output[^1][column] = cell;
            if (width == 2) output[^1][column + 1] = new Cell(string.Empty, cell.Style, true);
            column += width;
        }

        foreach (var line in _scrollback)
        {
            StartRow(line.WrappedFromPrevious);
            foreach (var run in line.Runs)
            {
                var elements = StringInfo.GetTextElementEnumerator(run.Text);
                while (elements.MoveNext())
                {
                    var text = elements.GetTextElement();
                    Put(new Cell(text, run.Style, false), Math.Clamp(GetCellWidth(Rune.GetRuneAt(text, 0)), 1, 2));
                }
            }
        }

        var lastRow = cursorRow;
        for (var r = screen.GetLength(0) - 1; r >= 0; r--)
        {
            var found = false;
            for (var c = 0; c < screen.GetLength(1); c++)
                if (screen[r, c].Continuation || screen[r, c].Text != " " || HasVisibleStyle(screen[r, c].Style))
                { found = true; break; }
            if (found) { lastRow = Math.Max(lastRow, r); break; }
        }
        for (var r = 0; r <= lastRow; r++)
        {
            StartRow(wraps[r]);
            var limit = screen.GetLength(1);
            if (r + 1 >= wraps.Length || !wraps[r + 1])
            {
                while (limit > 0 && !screen[r, limit - 1].Continuation && screen[r, limit - 1].Text == " " &&
                    !HasVisibleStyle(screen[r, limit - 1].Style)) limit--;
            }
            var cursorOffset = cursorColumn + (wrapPending ? 1 : 0);
            if (r == cursorRow) limit = Math.Max(limit, cursorOffset);
            for (var c = 0; c < limit; c++)
            {
                var cell = screen[r, c];
                if (cell.Continuation || cell.Text.Length == 0) continue;
                var width = c + 1 < screen.GetLength(1) && screen[r, c + 1].Continuation ? 2 : 1;
                var mapsCursor = r == cursorRow && c == cursorOffset;
                var mapsSaved = r == savedRow && c == savedColumn;
                Put(cell, width, () =>
                {
                    if (mapsCursor) mappedCursor = Position();
                    if (mapsSaved) mappedSaved = Position();
                });
            }
            if (r == cursorRow && cursorOffset >= limit) mappedCursor = Position();
            if (r == savedRow && savedColumn >= limit) mappedSaved = Position();
        }

        var screenStart = Math.Max(0, output.Count - rows);
        _scrollback.Clear();
        for (var r = 0; r < screenStart; r++)
            _scrollback.Add(CreateLine(output[r], columns, r + 1 < outputWraps.Count && outputWraps[r + 1])
                with { WrappedFromPrevious = outputWraps[r] });
        TrimScrollback();
        var resized = new Cell[rows, columns];
        Fill(resized);
        var resizedWraps = new bool[rows];
        for (var r = screenStart; r < output.Count; r++)
        {
            resizedWraps[r - screenStart] = outputWraps[r];
            for (var c = 0; c < columns; c++) resized[r - screenStart, c] = output[r][c];
        }
        return new ReflowedScreen(resized, resizedWraps,
            Math.Clamp(mappedCursor.Row - screenStart, 0, rows - 1), mappedCursor.Column, mappedCursor.Pending,
            Math.Clamp(mappedSaved.Row - screenStart, 0, rows - 1), mappedSaved.Column);
    }
}
