using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
namespace RtlTerminal;

/// <summary>A cell-based terminal surface. Only visible rows are shaped and drawn.</summary>
public sealed class TerminalView : ContentControl
{
    private readonly ScrollViewer _scroll;
    private readonly Surface _surface;
    private TerminalSnapshot? _snapshot;
    private bool _smartRtl;
    private double _cellWidth = 8.5, _lineHeight = 18;
    private (int Row, int Offset)? _anchor, _end;
    private bool _dragging;
    private readonly Dictionary<int, RowLayout> _layouts = [];
    private readonly Dictionary<TerminalColor, Brush> _brushes = [];
    private string _fontKey = string.Empty;
    private static readonly Regex Links = new(@"(?i)\b(?:https?://|www\.)[^\s<>{}\[\]""']+", RegexOptions.Compiled);
    public event Action<Uri>? LinkRequested;
    public bool HasSelection => _anchor is not null && _end is not null && _anchor != _end;
    public double VerticalOffset => _scroll.VerticalOffset;
    public double ViewportWidth => _scroll.ViewportWidth;
    public double ViewportHeight => _scroll.ViewportHeight;
    public bool IsUpdatingScroll { get; private set; }
    public ((int Row, int Offset)? Anchor, (int Row, int Offset)? End) SelectionState
    {
        get => (_anchor, _end);
        set { (_anchor, _end) = value; _surface.InvalidateVisual(); }
    }

    public TerminalView()
    {
        Resources.MergedDictionaries.Add(new ResourceDictionary
        {
            Source = new Uri("/RtlTerminal;component/TerminalScrollBar.xaml", UriKind.Relative)
        });
        Focusable = true;
        HorizontalContentAlignment = HorizontalAlignment.Stretch;
        VerticalContentAlignment = VerticalAlignment.Stretch;
        FocusVisualStyle = null;
        _surface = new Surface(this) { VerticalAlignment = VerticalAlignment.Top };
        _scroll = new ScrollViewer
        {
            Content = _surface,
            // Reserve a stable gutter: history appearing must not resize the PTY.
            VerticalScrollBarVisibility = ScrollBarVisibility.Visible,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            CanContentScroll = false,
            VerticalContentAlignment = VerticalAlignment.Top,
            HorizontalContentAlignment = HorizontalAlignment.Stretch,
            Focusable = false
        };
        Content = _scroll;
        _scroll.SetBinding(MarginProperty, new System.Windows.Data.Binding(nameof(Padding)) { Source = this });
        _scroll.ScrollChanged += (_, e) =>
        {
            // Scrolling and history growth do not change existing row geometry.
            if (e.ViewportWidthChange != 0) _layouts.Clear();
            _surface.InvalidateVisual();
        };
        SizeChanged += (_, _) => { _layouts.Clear(); _surface.InvalidateVisual(); };
        _surface.MouseLeftButtonDown += BeginSelection;
        _surface.MouseMove += ExtendSelection;
        _surface.AddHandler(Mouse.MouseMoveEvent, new MouseEventHandler((_, e) =>
        {
            var link = LinkAt(e.GetPosition(_surface));
            _surface.ToolTip = link is null ? null : "Ctrl + Left Click to open link";
            if (link is null) _surface.ClearValue(CursorProperty);
            else _surface.Cursor = Cursors.Hand;
        }), true);
        _surface.MouseLeave += (_, _) => { _surface.ToolTip = null; _surface.ClearValue(CursorProperty); };
        _surface.MouseLeftButtonUp += (_, e) =>
        {
            if (!_dragging) return;
            _dragging = false;
            _surface.ReleaseMouseCapture();
            e.Handled = true;
        };
    }

    public void Present(TerminalSnapshot snapshot, bool smartRtl, double cellWidth,
        double lineHeight, bool followOutput)
    {
        IsUpdatingScroll = true;
        Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.ContextIdle,
            () => IsUpdatingScroll = false);
        var trimmed = _snapshot is null ? 0 : snapshot.ScrollbackStartIndex - _snapshot.ScrollbackStartIndex;
        if (_snapshot?.Modes.AlternateScreen != snapshot.Modes.AlternateScreen)
            ClearSelection();
        else if (trimmed > 0)
        {
            if (_anchor is { } a && _end is { } b && a.Row >= trimmed && b.Row >= trimmed)
            { _anchor = (a.Row - (int)trimmed, a.Offset); _end = (b.Row - (int)trimmed, b.Offset); }
            else ClearSelection();
        }
        var fontKey = $"{FontFamily.Source}|{FontSize}|{FontWeight}|{FontStyle}|{VisualTreeHelper.GetDpi(this).PixelsPerDip}";
        if (trimmed != 0 || _smartRtl != smartRtl || _cellWidth != cellWidth ||
            _lineHeight != lineHeight || _fontKey != fontKey ||
            _snapshot?.Modes.AlternateScreen != snapshot.Modes.AlternateScreen)
            _layouts.Clear();
        _fontKey = fontKey;
        _snapshot = snapshot;
        _smartRtl = smartRtl;
        _cellWidth = cellWidth;
        _lineHeight = lineHeight;
        _surface.Height = Math.Max(lineHeight, snapshot.Lines.Count * lineHeight);
        _surface.InvalidateVisual();
        if (followOutput) _scroll.ScrollToEnd();
        else if (trimmed > 0) _scroll.ScrollToVerticalOffset(Math.Max(0, VerticalOffset - trimmed * lineHeight));
    }

    public void Clear()
    {
        _snapshot = null;
        _layouts.Clear();
        _surface.Height = _lineHeight;
        ClearSelection();
        _surface.InvalidateVisual();
    }

    public void ScrollToVerticalOffset(double offset) => _scroll.ScrollToVerticalOffset(offset);
    public bool TryGetGridCell(MouseEventArgs e, out int x, out int y)
    {
        var point = e.GetPosition(_surface);
        x = (int)Math.Floor(point.X / _cellWidth) + 1;
        y = (int)Math.Floor(point.Y / _lineHeight) - (_snapshot?.ScrollbackCount ?? 0) + 1;
        return point.X >= 0 && point.X < ViewportWidth && point.Y >= VerticalOffset &&
            point.Y < VerticalOffset + ViewportHeight && y > 0;
    }
    public void ClearSelection() { _anchor = _end = null; _surface.InvalidateVisual(); }
    public void SelectAll()
    {
        if (_snapshot is not { Lines.Count: > 0 } snapshot) return;
        _anchor = (0, 0);
        _end = (snapshot.Lines.Count - 1, Text(snapshot.Lines[^1]).Length);
        _surface.InvalidateVisual();
    }

    public void CopySelection()
    {
        if (!HasSelection || _snapshot is null) return;
        Clipboard.SetText(GetSelectedText());
        ClearSelection();
    }

    public string GetSelectedText()
    {
        if (!HasSelection || _snapshot is null) return string.Empty;
        var (start, end) = SelectionRange();
        var result = new StringBuilder();
        for (var row = start.Row; row <= end.Row && row < _snapshot.Lines.Count; row++)
        {
            var text = Text(_snapshot.Lines[row]);
            var from = row == start.Row ? Math.Min(start.Offset, text.Length) : 0;
            var to = row == end.Row ? Math.Min(end.Offset, text.Length) : text.Length;
            if (row > start.Row) result.AppendLine();
            result.Append(text[from..Math.Max(from, to)]);
        }
        return result.ToString();
    }

    private ((int Row, int Offset) Start, (int Row, int Offset) End) SelectionRange()
    {
        var a = _anchor!.Value; var b = _end!.Value;
        return a.Row < b.Row || a.Row == b.Row && a.Offset <= b.Offset ? (a, b) : (b, a);
    }

    public bool IsLinkAt(MouseEventArgs e) => LinkAt(e.GetPosition(_surface)) is not null;

    private Uri? LinkAt(Point point)
    {
        if (_snapshot is null || point.Y < VerticalOffset || point.Y >= VerticalOffset + ViewportHeight ||
            point.X < 0 || point.X >= ViewportWidth) return null;
        var row = (int)(point.Y / _lineHeight);
        if (row < 0 || row >= _snapshot.Lines.Count) return null;
        var layout = Layout(row);
        var cell = layout.Cells.FirstOrDefault(cell => point.X >= cell.X && point.X < cell.X + cell.Width);
        if (cell is null || cell.Style.Hidden) return null;
        foreach (Match match in Links.Matches(layout.Text))
        {
            var address = match.Value.TrimEnd('.', ',', ';', ':', '!', '?', ')');
            if (cell.Start < match.Index || cell.Start >= match.Index + address.Length) continue;
            if (address.StartsWith("www.", StringComparison.OrdinalIgnoreCase)) address = "https://" + address;
            if (Uri.TryCreate(address, UriKind.Absolute, out var uri)) return uri;
        }
        return null;
    }

    private void BeginSelection(object sender, MouseButtonEventArgs e)
    {
        Focus();
        var hit = Hit(e.GetPosition(_surface));
        if (hit is null) return;
        if ((Keyboard.Modifiers & ModifierKeys.Control) != 0 && LinkAt(e.GetPosition(_surface)) is { } uri)
        {
            LinkRequested?.Invoke(uri);
            e.Handled = true;
            return;
        }
        _anchor = _end = hit;
        if (e.ClickCount == 2 && _snapshot is not null)
        {
            var text = Text(_snapshot.Lines[hit.Value.Row]);
            var start = Math.Min(hit.Value.Offset, text.Length); var end = start;
            while (start > 0 && !char.IsWhiteSpace(text[start - 1])) start--;
            while (end < text.Length && !char.IsWhiteSpace(text[end])) end++;
            _anchor = (hit.Value.Row, start); _end = (hit.Value.Row, end);
        }
        _dragging = true;
        _surface.CaptureMouse();
        _surface.InvalidateVisual();
        e.Handled = true;
    }

    private void ExtendSelection(object sender, MouseEventArgs e)
    {
        if (!_dragging || e.LeftButton != MouseButtonState.Pressed) return;
        var point = e.GetPosition(_surface);
        if (point.Y < VerticalOffset) _scroll.ScrollToVerticalOffset(VerticalOffset - _lineHeight);
        if (point.Y > VerticalOffset + _scroll.ViewportHeight) _scroll.ScrollToVerticalOffset(VerticalOffset + _lineHeight);
        _end = Hit(point);
        _surface.InvalidateVisual();
        e.Handled = true;
    }

    private (int Row, int Offset)? Hit(Point point)
    {
        if (_snapshot is not { Lines.Count: > 0 }) return null;
        var row = Math.Clamp((int)(point.Y / _lineHeight), 0, _snapshot.Lines.Count - 1);
        var layout = Layout(row);
        var closest = layout.Cells.FirstOrDefault(cell => point.X >= cell.X && point.X < cell.X + cell.Width)
            ?? layout.Cells.MinBy(cell => Math.Abs(cell.X + cell.Width / 2 - point.X));
        if (closest is null) return (row, 0);
        var after = point.X >= closest.X + closest.Width / 2;
        return (row, (after != closest.Rtl) ? closest.Start + closest.Length : closest.Start);
    }

    private RowLayout Layout(int row)
    {
        var line = _snapshot!.Lines[row];
        // Screen snapshots recreate line objects even when their contents are unchanged.
        if (_layouts.TryGetValue(row, out var cached) &&
            (ReferenceEquals(cached.Source, line) ||
             cached.Source.CellLength == line.CellLength && cached.Source.Runs.SequenceEqual(line.Runs)))
            return cached;
        var text = Text(line);
        var rightAlign = SmartRtl.ShouldRightAlign(line, _smartRtl, _snapshot.Modes.AlternateScreen);
        var spans = _smartRtl && line.ContainsRightToLeft
            ? SmartRtl.GetDirectionalSpans(text, rightAlign)
            : text.Length == 0 ? [] : new[] { new DirectionalSpan(0, text.Length, false) };
        var cells = new List<DrawCell>();
        var glyphs = new List<DrawGlyph>();
        var widths = new List<(int Start, int Length, int Width)>();
        var enumerator = StringInfo.GetTextElementEnumerator(text);
        while (enumerator.MoveNext())
        {
            var element = enumerator.GetTextElement();
            widths.Add((enumerator.ElementIndex, element.Length,
                element.EnumerateRunes().Max(TerminalBuffer.GetCellWidth)));
        }
        var totalWidth = widths.Sum(item => item.Width) * _cellWidth;
        var x = rightAlign ? Math.Max(0, _scroll.ViewportWidth - totalWidth) : 0;
        var orderedSpans = rightAlign ? spans.Reverse() : spans;
        foreach (var span in orderedSpans)
        {
            var members = widths.Where(item => item.Start >= span.Start && item.Start < span.Start + span.Length).ToArray();
            var spanWidth = members.Sum(item => item.Width) * _cellWidth;
            var advance = 0.0;
            foreach (var member in members)
            {
                var width = member.Width * _cellWidth;
                var cellX = x + (span.IsRightToLeft ? spanWidth - advance - width : advance);
                var style = StyleAt(line, member.Start);
                cells.Add(new DrawCell(member.Start, member.Length, cellX, width, span.IsRightToLeft, style));
                advance += width;
                // Latin and box-drawing glyphs are positioned at exact cell boundaries.
                if (!span.IsRightToLeft && width > 0)
                    glyphs.Add(new DrawGlyph(text.Substring(member.Start, member.Length), cellX, width, false, member.Start, style));
            }
            // Shape a complete RTL span together so Arabic joining survives ANSI style boundaries.
            if (span.IsRightToLeft && members.Length > 0)
                glyphs.Add(new DrawGlyph(text.Substring(span.Start, span.Length), x, spanWidth, true, span.Start, StyleAt(line, span.Start)));
            x += spanWidth;
        }
        var result = new RowLayout(line, text, cells, glyphs);
        _layouts[row] = result;
        return result;
    }

    private static TerminalStyle StyleAt(TerminalLine line, int offset)
    {
        foreach (var run in line.Runs) { if (offset < run.Text.Length) return run.Style; offset -= run.Text.Length; }
        return default;
    }
    private static string Text(TerminalLine line) => string.Concat(line.Runs.Select(run => run.Text));
    private Brush BrushFor(TerminalColor color)
    {
        if (_brushes.TryGetValue(color, out var cached)) return cached;
        if (_brushes.Count >= 1024) _brushes.Clear();
        var brush = new SolidColorBrush(Color.FromRgb(color.Red, color.Green, color.Blue));
        brush.Freeze();
        _brushes[color] = brush;
        return brush;
    }
    private static (TerminalColor Foreground, TerminalColor Background) Colors(TerminalStyle style)
    {
        var fg = style.Foreground ?? new TerminalColor(230, 230, 230);
        var bg = style.Background ?? new TerminalColor(12, 12, 12);
        if (style.Inverse) (fg, bg) = (bg, fg);
        if (style.Dim) fg = new((byte)(fg.Red * .55 + bg.Red * .45), (byte)(fg.Green * .55 + bg.Green * .45), (byte)(fg.Blue * .55 + bg.Blue * .45));
        if (style.Hidden) fg = bg;
        return (fg, bg);
    }

    private void Draw(DrawingContext dc)
    {
        if (_snapshot is null) return;
        var top = VerticalOffset;
        dc.PushClip(new RectangleGeometry(new Rect(0, top, Math.Max(0, _scroll.ViewportWidth), Math.Max(0, _scroll.ViewportHeight))));
        dc.DrawRectangle(Background, null, new Rect(0, top, Math.Max(0, _scroll.ViewportWidth), Math.Max(0, _scroll.ViewportHeight)));
        var first = Math.Max(0, (int)(top / _lineHeight));
        var last = Math.Min(_snapshot.Lines.Count, (int)Math.Ceiling((top + _scroll.ViewportHeight) / _lineHeight) + 1);
        foreach (var key in _layouts.Keys.Where(key => key < first || key >= last).ToArray()) _layouts.Remove(key);
        for (var row = first; row < last; row++)
        {
            var layout = Layout(row); var y = row * _lineHeight;
            if (layout.Drawing is null)
            {
                var drawing = new DrawingGroup();
                // Graphics use hard, adjoining cell edges; text keeps its own antialiasing.
                RenderOptions.SetEdgeMode(drawing, EdgeMode.Aliased);
                using (var rowContext = drawing.Open()) DrawRow(rowContext, layout);
                if (drawing.CanFreeze) drawing.Freeze();
                layout.Drawing = drawing;
            }
            dc.PushTransform(new TranslateTransform(0, y));
            dc.DrawDrawing(layout.Drawing);
            dc.Pop();
            if (HasSelection)
            {
                var (start, end) = SelectionRange();
                if (row >= start.Row && row <= end.Row)
                    foreach (var cell in layout.Cells.Where(cell => (row != start.Row || cell.Start + cell.Length > start.Offset) && (row != end.Row || cell.Start < end.Offset)))
                        dc.DrawRectangle(new SolidColorBrush(Color.FromArgb(100, 70, 125, 200)), null, new Rect(cell.X, y, cell.Width, _lineHeight));
            }
            if (_snapshot.CursorVisible && row == _snapshot.CursorRow)
            {
                var column = 0; DrawCell? cursor = null;
                foreach (var cell in layout.Cells.OrderBy(cell => cell.Start))
                { if (_snapshot.CursorColumn >= column && _snapshot.CursorColumn < column + cell.Width / _cellWidth) { cursor = cell; break; } column += (int)Math.Round(cell.Width / _cellWidth); }
                var rect = new Rect(cursor?.X ?? _snapshot.CursorColumn * _cellWidth, y, Math.Max(_cellWidth, cursor?.Width ?? _cellWidth), _lineHeight);
                dc.DrawRectangle(new SolidColorBrush(Color.FromArgb(90, 230, 230, 230)), new Pen(Brushes.LightGray, 1), rect);
            }
        }
        dc.Pop();
    }
    private void DrawRow(DrawingContext dc, RowLayout layout)
    {
        const double y = 0;
        // Merge adjacent backgrounds: fewer draw calls and no fractional-cell seams.
        var ordered = layout.Cells.OrderBy(cell => cell.X).ToArray();
        for (var index = 0; index < ordered.Length;)
        {
            var cell = ordered[index++];
            var color = Colors(cell.Style).Background;
            var right = cell.X + cell.Width;
            while (index < ordered.Length && Math.Abs(ordered[index].X - right) < .01 &&
                Colors(ordered[index].Style).Background == color)
            {
                right = ordered[index].X + ordered[index].Width;
                index++;
            }
            dc.DrawRectangle(BrushFor(color), null, new Rect(cell.X, y, right - cell.X, _lineHeight));
        }
        var links = Links.Matches(layout.Text);
        foreach (var glyph in layout.Glyphs)
        {
            if (DrawBlock(dc, glyph, y) || DrawBox(dc, glyph, y)) continue;
            var formatted = glyph.Formatted;
            if (formatted is null)
            {
            formatted = new FormattedText(glyph.Text, CultureInfo.CurrentCulture,
                glyph.Rtl ? FlowDirection.RightToLeft : FlowDirection.LeftToRight,
                new Typeface(glyph.Text.EnumerateRunes().Any(rune => rune.Value >= 0x1f000)
                    ? new FontFamily("Segoe UI Emoji") : FontFamily, glyph.Style.Italic ? FontStyles.Italic : FontStyle,
                    glyph.Style.Bold ? FontWeights.Bold : FontWeight, FontStretch), FontSize,
                BrushFor(Colors(glyph.Style).Foreground), VisualTreeHelper.GetDpi(this).PixelsPerDip);
            for (var index = 0; index < glyph.Text.Length;)
            {
                var style = StyleAt(layout.Source, glyph.Start + index);
                var end = index + 1;
                while (end < glyph.Text.Length && StyleAt(layout.Source, glyph.Start + end) == style) end++;
                formatted.SetForegroundBrush(BrushFor(Colors(style).Foreground), index, end - index);
                formatted.SetFontWeight(style.Bold ? FontWeights.Bold : FontWeight, index, end - index);
                formatted.SetFontStyle(style.Italic ? FontStyles.Italic : FontStyle, index, end - index);
                var decorations = new TextDecorationCollection();
                if (style.Underline) decorations.Add(TextDecorations.Underline[0]);
                if (style.Strikethrough) decorations.Add(TextDecorations.Strikethrough[0]);
                formatted.SetTextDecorations(decorations, index, end - index);
                index = end;
            }
            foreach (Match link in links)
            {
                var start = Math.Max(glyph.Start, link.Index); var end = Math.Min(glyph.Start + glyph.Text.Length, link.Index + link.Length);
                if (end <= start) continue;
                if (glyph.Style.Hidden) continue;
                formatted.SetForegroundBrush(Brushes.CornflowerBlue, start - glyph.Start, end - start);
                formatted.SetTextDecorations(TextDecorations.Underline, start - glyph.Start, end - start);
            }
            glyph.Formatted = formatted;
            }
            dc.PushClip(new RectangleGeometry(new Rect(glyph.X, y, glyph.Width, _lineHeight)));
            var scale = glyph.Rtl && formatted.WidthIncludingTrailingWhitespace > 0 ? glyph.Width / formatted.WidthIncludingTrailingWhitespace : 1;
            var origin = glyph.Rtl ? glyph.X + glyph.Width : glyph.X;
            dc.PushTransform(new ScaleTransform(scale, 1, origin, y));
            dc.DrawText(formatted, new Point(origin, y + Math.Max(0, (_lineHeight - formatted.Height) / 2)));
            dc.Pop(); dc.Pop();
        }
        foreach (var hidden in layout.Cells.Where(cell => cell.Style.Hidden))
            dc.DrawRectangle(BrushFor(Colors(hidden.Style).Background), null,
                new Rect(hidden.X, y, hidden.Width, _lineHeight));
    }

    private sealed class Surface(TerminalView owner) : FrameworkElement
    {
        protected override void OnRender(DrawingContext drawingContext) => owner.Draw(drawingContext);
    }
    private sealed record DrawCell(int Start, int Length, double X, double Width, bool Rtl, TerminalStyle Style);
    private sealed record DrawGlyph(string Text, double X, double Width, bool Rtl, int Start, TerminalStyle Style)
    {
        public FormattedText? Formatted { get; set; }
    }
    private sealed record RowLayout(TerminalLine Source, string Text, List<DrawCell> Cells, List<DrawGlyph> Glyphs)
    {
        public DrawingGroup? Drawing { get; set; }
    }

    private bool DrawBlock(DrawingContext dc, DrawGlyph glyph, double y)
    {
        if (glyph.Text.Length != 1 || glyph.Text[0] is < '\u2580' or > '\u259f') return false;
        var code = glyph.Text[0];
        var (foreground, background) = Colors(glyph.Style);
        var brush = BrushFor(foreground);
        void Fill(double left, double top, double width, double height) =>
            dc.DrawRectangle(brush, null, new Rect(glyph.X + left * glyph.Width,
                y + top * _lineHeight, width * glyph.Width, height * _lineHeight));

        if (code == '\u2580') Fill(0, 0, 1, .5);
        else if (code <= '\u2588')
        {
            var height = (code - 0x2580) / 8.0;
            Fill(0, 1 - height, 1, height);
        }
        else if (code <= '\u258f') Fill(0, 0, (0x2590 - code) / 8.0, 1);
        else if (code == '\u2590') Fill(.5, 0, .5, 1);
        else if (code <= '\u2593')
        {
            var amount = (code - 0x2590) / 4.0;
            byte Blend(byte fg, byte bg) => (byte)Math.Round(fg * amount + bg * (1 - amount));
            brush = BrushFor(new TerminalColor(Blend(foreground.Red, background.Red),
                Blend(foreground.Green, background.Green), Blend(foreground.Blue, background.Blue)));
            Fill(0, 0, 1, 1);
        }
        else if (code == '\u2594') Fill(0, 0, 1, .125);
        else if (code == '\u2595') Fill(.875, 0, .125, 1);
        else
        {
            // Quadrants: upper-left, upper-right, lower-left, lower-right.
            var mask = code switch
            {
                '\u2596' => 4, '\u2597' => 8, '\u2598' => 1, '\u2599' => 13,
                '\u259a' => 9, '\u259b' => 7, '\u259c' => 11, '\u259d' => 2,
                '\u259e' => 6, '\u259f' => 14, _ => 0
            };
            for (var quadrant = 0; quadrant < 4; quadrant++)
                if ((mask & (1 << quadrant)) != 0)
                    Fill((quadrant % 2) * .5, (quadrant / 2) * .5, .5, .5);
        }
        return true;
    }

    private bool DrawBox(DrawingContext dc, DrawGlyph glyph, double y)
    {
        // Edges meet at cell boundaries regardless of font metrics or DPI.
        var edges = glyph.Text switch
        {
            "─" or "━" or "═" => 3,
            "│" or "┃" or "║" => 12,
            "┌" or "┏" or "╔" or "╭" => 10,
            "┐" or "┓" or "╗" or "╮" => 9,
            "└" or "┗" or "╚" or "╰" => 6,
            "┘" or "┛" or "╝" or "╯" => 5,
            "├" or "┣" or "╠" => 14,
            "┤" or "┫" or "╣" => 13,
            "┬" or "┳" or "╦" => 11,
            "┴" or "┻" or "╩" => 7,
            "┼" or "╋" or "╬" => 15,
            _ => 0
        };
        if (edges == 0) return false;
        var foreground = BrushFor(Colors(glyph.Style).Foreground);
        var pen = new Pen(foreground, glyph.Text is "━" or "┃" or "┏" or "┓" or "┗" or "┛" or "┣" or "┫" or "┳" or "┻" or "╋" ? 2 : 1);
        var middle = new Point(glyph.X + glyph.Width / 2, y + _lineHeight / 2);
        double[] offsets = "═║╔╗╚╝╠╣╦╩╬".Contains(glyph.Text, StringComparison.Ordinal) ? [-1.5, 1.5] : [0];
        foreach (var offset in offsets)
        {
            if ((edges & 1) != 0) dc.DrawLine(pen, new Point(glyph.X, middle.Y + offset), new Point(middle.X + offset, middle.Y + offset));
            if ((edges & 2) != 0) dc.DrawLine(pen, new Point(middle.X + offset, middle.Y + offset), new Point(glyph.X + glyph.Width, middle.Y + offset));
            if ((edges & 4) != 0) dc.DrawLine(pen, new Point(middle.X + offset, y), new Point(middle.X + offset, middle.Y + offset));
            if ((edges & 8) != 0) dc.DrawLine(pen, new Point(middle.X + offset, middle.Y + offset), new Point(middle.X + offset, y + _lineHeight));
        }
        return true;
    }
}
