using System;
using System.Diagnostics;
using System.IO;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Controls;
using System.Windows.Threading;

namespace RtlTerminal;

public partial class MainWindow : Window
{
    private static readonly TerminalColor DefaultForeground = new(230, 230, 230);
    private static readonly TerminalColor DefaultBackground = new(12, 12, 12);
    private static readonly TerminalColor LinkForeground = new(86, 156, 214);
    private static readonly Regex LinkPattern = new(
        @"(?i)\b(?:https?://|www\.)[^\s<>{}\[\]""']+",
        RegexOptions.Compiled);
    private readonly object _renderLock = new();
    private readonly DispatcherTimer _renderTimer;
    private readonly List<RenderedLine> _renderedLines = [];
    private ConPtySession? _session;
    private TerminalBuffer? _terminalBuffer;
    private CancellationTokenSource? _cancellationTokenSource;
    private TerminalSnapshot? _pendingSnapshot;
    private bool _renderStartQueued;
    private bool _updatingContextMenuItem;
    private long _latestQueuedRevision;
    private TerminalSnapshot? _lastRenderedSnapshot;
    private FlowDocument? _terminalDocument;
    private double _cellWidth = 8.5;
    private double _lineHeight = 18;

    public MainWindow()
    {
        InitializeComponent();
        _renderTimer = new DispatcherTimer(DispatcherPriority.Render)
        {
            Interval = TimeSpan.FromMilliseconds(33)
        };
        _renderTimer.Tick += RenderTimer_Tick;
        ApplySavedFontSettings();
        UpdateFontMetrics();
    }

    private void Window_Loaded(object sender, RoutedEventArgs e)
    {
        RefreshContextMenuIntegrationState();
        PromptForContextMenuIntegration();
        TerminalTextBox.Focus();
        TerminalTextBox.UpdateLayout();
        Dispatcher.BeginInvoke(
            DispatcherPriority.Loaded,
            StartTerminalSession);
    }

    private void StartTerminalSession()
    {
        try
        {
            var columns = GetColumns();
            var rows = GetRows();
            _terminalBuffer = new TerminalBuffer(columns, rows);
            _cancellationTokenSource = new CancellationTokenSource();
            _session = new ConPtySession(columns, rows);
            _session.Start(
                @"C:\Windows\System32\cmd.exe /D /K ""chcp 65001>nul & echo RtlTerminal by behnam tajadini & echo GitHub: https://github.com/mirbehnam/RtlTerminal & echo YouTube: akatechno & echo تقدیم به همه فارسی زبانان & echo.""",
                GetStartupDirectory());
            _ = Task.Run(() => ReadOutputLoop(_cancellationTokenSource.Token));
        }
        catch (Exception exception)
        {
            TerminalTextBox.Document.Blocks.Clear();
            TerminalTextBox.Document.Blocks.Add(
                new Paragraph(new Run(
                    "خطا در اجرای ConPTY:" +
                    Environment.NewLine +
                    exception))
                {
                    FlowDirection = FlowDirection.RightToLeft
                });
        }
    }

    private void Window_Closed(object? sender, EventArgs e)
    {
        _cancellationTokenSource?.Cancel();
        _session?.Dispose();
        _cancellationTokenSource?.Dispose();
    }

    private void Window_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        if (_session is null || _terminalBuffer is null)
            return;

        var columns = GetColumns();
        var rows = GetRows();
        _session.Resize(columns, rows);
        QueueRender(_terminalBuffer.Resize(columns, rows));
    }

    private void TerminalTextBox_PreviewTextInput(
        object sender,
        TextCompositionEventArgs e)
    {
        if (_session is null || string.IsNullOrEmpty(e.Text))
            return;

        _session.Write(e.Text);
        e.Handled = true;
    }

    private void TerminalTextBox_PreviewMouseRightButtonDown(
        object sender,
        MouseButtonEventArgs e)
    {
        if (!TerminalTextBox.Selection.IsEmpty)
            return;

        PasteClipboard();
        e.Handled = true;
        TerminalTextBox.Focus();
    }

    private void RtlMenuItem_Changed(object sender, RoutedEventArgs e)
    {
        if (_lastRenderedSnapshot is not null)
            Render(_lastRenderedSnapshot);

        TerminalTextBox.Focus();
    }

    private void FontSettingsMenuItem_Click(object sender, RoutedEventArgs e)
    {
        var settingsWindow = new FontSettingsWindow(
            TerminalTextBox.FontFamily.Source,
            TerminalTextBox.FontSize,
            TerminalTextBox.FontWeight,
            TerminalTextBox.FontStyle)
        {
            Owner = this
        };

        if (settingsWindow.ShowDialog() != true)
            return;

        ApplyFontSettings(settingsWindow.SelectedSettings);
        AppSettings.SaveFont(settingsWindow.SelectedSettings);
        TerminalTextBox.Focus();
    }

    private void GuideMenuItem_Click(object sender, RoutedEventArgs e)
    {
        var guideWindow = new GuideWindow
        {
            Owner = this
        };
        guideWindow.Show();
    }

    private void AboutMenuItem_Click(object sender, RoutedEventArgs e)
    {
        MessageBox.Show(
            this,
            """
            Rtl Terminal
            by behnamapps

            Developer: behnam tajadini
            YouTube: akatechno

            تقدیم به همه فارسی زبانان
            """,
            "About Rtl Terminal",
            MessageBoxButton.OK,
            MessageBoxImage.Information);
    }

    private void ContextMenuIntegrationMenuItem_Click(
        object sender,
        RoutedEventArgs e)
    {
        if (_updatingContextMenuItem)
            return;

        try
        {
            if (ContextMenuIntegrationMenuItem.IsChecked)
                ContextMenuIntegration.Install();
            else
                ContextMenuIntegration.Uninstall();
        }
        catch (Exception exception)
        {
            MessageBox.Show(
                this,
                "تغییر منوی راست‌کلیک انجام نشد." +
                Environment.NewLine +
                exception.Message,
                "RtlTerminal",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
        finally
        {
            RefreshContextMenuIntegrationState();
            TerminalTextBox.Focus();
        }
    }

    private void PromptForContextMenuIntegration()
    {
        if (ContextMenuIntegration.HasAnsweredInitialPrompt())
            return;

        var result = MessageBox.Show(
            this,
            "آیا گزینه «Open in RtlTerminal» به منوی راست‌کلیک پوشه‌ها اضافه شود؟",
            "RtlTerminal",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);

        try
        {
            if (result == MessageBoxResult.Yes)
                ContextMenuIntegration.Install();
        }
        catch (Exception exception)
        {
            MessageBox.Show(
                this,
                "افزودن منوی راست‌کلیک انجام نشد." +
                Environment.NewLine +
                exception.Message,
                "RtlTerminal",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
        finally
        {
            ContextMenuIntegration.MarkInitialPromptAnswered();
            RefreshContextMenuIntegrationState();
        }
    }

    private void RefreshContextMenuIntegrationState()
    {
        _updatingContextMenuItem = true;
        ContextMenuIntegrationMenuItem.IsChecked =
            ContextMenuIntegration.IsInstalled();
        ContextMenuIntegrationMenuItem.Header =
            ContextMenuIntegrationMenuItem.IsChecked
                ? "Remove _Open in RtlTerminal"
                : "Add _Open in RtlTerminal";
        _updatingContextMenuItem = false;
    }

    private static string? GetStartupDirectory()
    {
        var arguments = Environment.GetCommandLineArgs();

        if (arguments.Length < 2)
            return null;

        var path = arguments[1];
        return Directory.Exists(path) ? Path.GetFullPath(path) : null;
    }

    private void TerminalTextBox_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (_session is null)
            return;

        var controlPressed = (Keyboard.Modifiers & ModifierKeys.Control) != 0;
        var shiftPressed = (Keyboard.Modifiers & ModifierKeys.Shift) != 0;

        if (controlPressed && shiftPressed && e.Key == Key.C)
        {
            CopySelection();
            e.Handled = true;
            return;
        }

        if (controlPressed && shiftPressed && e.Key == Key.V)
        {
            PasteClipboard();
            e.Handled = true;
            return;
        }

        if (controlPressed && e.Key == Key.C)
        {
            if (!TerminalTextBox.Selection.IsEmpty)
                CopySelection();
            else
                _session.Write("\x03");

            e.Handled = true;
            return;
        }

        if (controlPressed && e.Key == Key.V)
        {
            PasteClipboard();
            e.Handled = true;
            return;
        }

        if (controlPressed && e.Key >= Key.A && e.Key <= Key.Z)
        {
            var controlCharacter = (char)(e.Key - Key.A + 1);
            _session.Write(controlCharacter.ToString());
            e.Handled = true;
            return;
        }

        string? sequence = e.Key switch
        {
            Key.Enter => "\r",
            Key.Space => " ",
            Key.Back => "\x7f",
            Key.Tab => "\t",
            Key.Up => "\x1b[A",
            Key.Down => "\x1b[B",
            Key.Right => "\x1b[C",
            Key.Left => "\x1b[D",
            Key.Home => "\x1b[H",
            Key.End => "\x1b[F",
            Key.Delete => "\x1b[3~",
            Key.PageUp => "\x1b[5~",
            Key.PageDown => "\x1b[6~",
            Key.Insert => "\x1b[2~",
            Key.Escape => "\x1b",
            _ => null
        };

        if (sequence is null)
            return;

        _session.Write(sequence);
        e.Handled = true;
    }

    private void CopySelection()
    {
        if (TerminalTextBox.Selection.IsEmpty)
            return;

        var selectionEnd = TerminalTextBox.Selection.End;
        TerminalTextBox.Copy();
        TerminalTextBox.Selection.Select(
            selectionEnd,
            selectionEnd);
        TerminalTextBox.CaretPosition = selectionEnd;
    }

    private void PasteClipboard()
    {
        if (_session is null || !Clipboard.ContainsText())
            return;

        var text = Clipboard.GetText()
            .Replace("\r\n", "\r")
            .Replace("\n", "\r");

        _session.Write(text);
    }

    private void ReadOutputLoop(CancellationToken cancellationToken)
    {
        if (_session is null || _terminalBuffer is null)
            return;

        var bytes = new byte[8192];
        var characters = new char[Encoding.UTF8.GetMaxCharCount(bytes.Length)];
        var decoder = Encoding.UTF8.GetDecoder();

        while (!cancellationToken.IsCancellationRequested)
        {
            int byteCount;

            try
            {
                byteCount = _session.Read(bytes);
            }
            catch (ObjectDisposedException)
            {
                break;
            }
            catch
            {
                break;
            }

            if (byteCount <= 0)
                break;

            var characterCount = decoder.GetChars(
                bytes,
                0,
                byteCount,
                characters,
                0,
                flush: false);

            var output = new string(characters, 0, characterCount);
            QueueRender(_terminalBuffer.Process(output));
        }
    }

    private void QueueRender(TerminalSnapshot snapshot)
    {
        lock (_renderLock)
        {
            if (snapshot.Revision < _latestQueuedRevision)
                return;

            _latestQueuedRevision = snapshot.Revision;
            _pendingSnapshot = snapshot;

            if (_renderTimer.IsEnabled || _renderStartQueued)
                return;

            _renderStartQueued = true;
        }

        Dispatcher.BeginInvoke(
            DispatcherPriority.Render,
            StartRenderTimer);
    }

    private void StartRenderTimer()
    {
        lock (_renderLock)
            _renderStartQueued = false;

        if (!_renderTimer.IsEnabled)
            _renderTimer.Start();
    }

    private void RenderTimer_Tick(object? sender, EventArgs e)
    {
        TerminalSnapshot? snapshot;

        lock (_renderLock)
        {
            snapshot = _pendingSnapshot;
            _pendingSnapshot = null;
        }

        if (snapshot is not null)
            Render(snapshot);

        lock (_renderLock)
        {
            if (_pendingSnapshot is null)
                _renderTimer.Stop();
        }
    }

    private void Render(TerminalSnapshot snapshot)
    {
        _lastRenderedSnapshot = snapshot;
        var isRightToLeft = RtlMenuItem.IsChecked;
        var scrollViewer = FindVisualChild<ScrollViewer>(TerminalTextBox);
        var shouldFollowOutput =
            scrollViewer is null ||
            scrollViewer.ScrollableHeight <= 0 ||
            scrollViewer.VerticalOffset >=
            scrollViewer.ScrollableHeight - 2;

        if (_terminalDocument is null)
        {
            _terminalDocument = new FlowDocument
            {
                PagePadding = new Thickness(0),
                ColumnWidth = double.PositiveInfinity,
                LineHeight = _lineHeight,
                FontFamily = TerminalTextBox.FontFamily,
                FontSize = TerminalTextBox.FontSize,
                Foreground = ToBrush(DefaultForeground),
                Background = ToBrush(DefaultBackground)
            };
            TerminalTextBox.Document = _terminalDocument;
        }

        while (_renderedLines.Count > snapshot.Lines.Count)
        {
            var lastLine = _renderedLines[^1];
            _terminalDocument.Blocks.Remove(lastLine.Paragraph);
            _renderedLines.RemoveAt(_renderedLines.Count - 1);
        }

        for (var row = 0; row < snapshot.Lines.Count; row++)
        {
            var line = snapshot.Lines[row];
            var key = CreateLineKey(line, isRightToLeft);

            if (row < _renderedLines.Count &&
                _renderedLines[row].Key == key)
            {
                continue;
            }

            var paragraph = new Paragraph
            {
                Margin = new Thickness(0),
                Padding = new Thickness(0),
                LineHeight = _lineHeight,
                FlowDirection = isRightToLeft
                    ? FlowDirection.RightToLeft
                    : FlowDirection.LeftToRight,
                TextAlignment = isRightToLeft
                    ? TextAlignment.Right
                    : TextAlignment.Left
            };

            var cellOffset = 0;
            var runPositions = new List<RunPosition>();

            foreach (var terminalRun in line.Runs)
            {
                foreach (var segment in SplitLinks(terminalRun.Text))
                {
                    var run = CreateRun(segment.Text, terminalRun.Style);

                    if (segment.Uri is not null)
                    {
                        var hyperlink = new Hyperlink(run)
                        {
                            Foreground = ToBrush(LinkForeground),
                            TextDecorations = TextDecorations.Underline,
                            Cursor = Cursors.Hand,
                            ToolTip = "Ctrl را نگه دارید و کلیک کنید"
                        };
                        hyperlink.Click += Link_Click;
                        hyperlink.Tag = segment.Uri;
                        paragraph.Inlines.Add(hyperlink);
                    }
                    else
                    {
                        paragraph.Inlines.Add(run);
                    }

                    runPositions.Add(
                        new RunPosition(run, cellOffset, segment.Text.Length));
                    cellOffset += segment.Text.Length;
                }
            }

            var renderedLine = new RenderedLine(
                paragraph,
                key,
                runPositions);

            if (row < _renderedLines.Count)
            {
                var oldParagraph = _renderedLines[row].Paragraph;
                _terminalDocument.Blocks.InsertBefore(oldParagraph, paragraph);
                _terminalDocument.Blocks.Remove(oldParagraph);
                _renderedLines[row] = renderedLine;
            }
            else
            {
                _terminalDocument.Blocks.Add(paragraph);
                _renderedLines.Add(renderedLine);
            }
        }

        if (_renderedLines.Count == 0)
        {
            var paragraph = new Paragraph();
            _terminalDocument.Blocks.Add(paragraph);
            _renderedLines.Add(new RenderedLine(paragraph, string.Empty, []));
        }

        var cursorRow = Math.Clamp(
            snapshot.CursorRow,
            0,
            _renderedLines.Count - 1);
        var cursorLine = _renderedLines[cursorRow];
        TerminalTextBox.CaretPosition =
            FindCaretPosition(cursorLine, snapshot.CursorColumn);

        if (shouldFollowOutput)
        {
            Dispatcher.BeginInvoke(
                DispatcherPriority.Background,
                TerminalTextBox.ScrollToEnd);
        }
    }

    private void ApplySavedFontSettings()
    {
        var settings = AppSettings.LoadFont();

        if (settings is { } savedSettings)
            ApplyFontSettings(savedSettings);
    }

    private void ApplyFontSettings(TerminalFontSettings settings)
    {
        TerminalTextBox.FontFamily = new FontFamily(settings.Family);
        TerminalTextBox.FontSize = settings.Size;
        TerminalTextBox.FontWeight = settings.Bold
            ? FontWeights.Bold
            : FontWeights.Normal;
        TerminalTextBox.FontStyle = settings.Italic
            ? FontStyles.Italic
            : FontStyles.Normal;
        UpdateFontMetrics();

        _terminalDocument = null;
        _renderedLines.Clear();

        if (_session is not null && _terminalBuffer is not null)
        {
            var columns = GetColumns();
            var rows = GetRows();
            _session.Resize(columns, rows);
            QueueRender(_terminalBuffer.Resize(columns, rows));
            return;
        }

        if (_lastRenderedSnapshot is not null)
            Render(_lastRenderedSnapshot);
    }

    private void UpdateFontMetrics()
    {
        var typeface = new Typeface(
            TerminalTextBox.FontFamily,
            TerminalTextBox.FontStyle,
            TerminalTextBox.FontWeight,
            TerminalTextBox.FontStretch);
        var pixelsPerDip = VisualTreeHelper.GetDpi(this).PixelsPerDip;
        var measurement = new FormattedText(
            "M",
            System.Globalization.CultureInfo.InvariantCulture,
            FlowDirection.LeftToRight,
            typeface,
            TerminalTextBox.FontSize,
            Brushes.White,
            pixelsPerDip);

        _cellWidth = Math.Max(4, measurement.WidthIncludingTrailingWhitespace);
        _lineHeight = Math.Max(
            TerminalTextBox.FontSize + 3,
            measurement.Height + 2);
    }

    private static string CreateLineKey(
        TerminalLine line,
        bool isRightToLeft)
    {
        var key = new StringBuilder(isRightToLeft ? "R|" : "L|");

        foreach (var run in line.Runs)
        {
            key.Append(run.Text)
                .Append('\u001f')
                .Append(run.Style.Foreground)
                .Append(run.Style.Background)
                .Append(run.Style.Bold)
                .Append(run.Style.Dim)
                .Append('\u001e');
        }

        return key.ToString();
    }

    private static TextPointer FindCaretPosition(
        RenderedLine line,
        int cursorColumn)
    {
        foreach (var position in line.Runs)
        {
            if (cursorColumn > position.Start + position.Length)
                continue;

            var offset = Math.Clamp(
                cursorColumn - position.Start,
                0,
                position.Length);
            return position.Run.ContentStart.GetPositionAtOffset(
                offset,
                LogicalDirection.Forward) ??
                position.Run.ContentEnd;
        }

        return line.Paragraph.ContentEnd;
    }

    private static Run CreateRun(string text, TerminalStyle style)
    {
        return new Run(text)
        {
            Foreground = ToBrush(GetForeground(style)),
            Background = ToBrush(style.Background ?? DefaultBackground),
            FontWeight = style.Bold ? FontWeights.Bold : FontWeights.Normal
        };
    }

    private static IEnumerable<LinkSegment> SplitLinks(string text)
    {
        var start = 0;

        foreach (Match match in LinkPattern.Matches(text))
        {
            if (match.Index > start)
                yield return new LinkSegment(text[start..match.Index], null);

            var linkText = match.Value.TrimEnd('.', ',', ';', ':', '!', '?', ')');
            var trailingText = match.Value[linkText.Length..];
            var uriText = linkText.StartsWith(
                "www.",
                StringComparison.OrdinalIgnoreCase)
                ? $"https://{linkText}"
                : linkText;

            if (Uri.TryCreate(uriText, UriKind.Absolute, out var uri) &&
                uri.Scheme is "http" or "https")
            {
                yield return new LinkSegment(linkText, uri);
            }
            else
            {
                yield return new LinkSegment(linkText, null);
            }

            if (trailingText.Length > 0)
                yield return new LinkSegment(trailingText, null);

            start = match.Index + match.Length;
        }

        if (start < text.Length)
            yield return new LinkSegment(text[start..], null);
    }

    private void Link_Click(object sender, RoutedEventArgs e)
    {
        e.Handled = true;

        if ((Keyboard.Modifiers & ModifierKeys.Control) == 0 ||
            sender is not Hyperlink { Tag: Uri uri })
        {
            return;
        }

        try
        {
            Process.Start(new ProcessStartInfo(uri.AbsoluteUri)
            {
                UseShellExecute = true
            });
        }
        catch (Exception exception)
        {
            MessageBox.Show(
                this,
                "بازکردن لینک انجام نشد." +
                Environment.NewLine +
                exception.Message,
                "Rtl Terminal",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
    }

    private static SolidColorBrush ToBrush(TerminalColor color)
    {
        var brush = new SolidColorBrush(
            Color.FromRgb(color.Red, color.Green, color.Blue));
        brush.Freeze();
        return brush;
    }

    private static TerminalColor GetForeground(TerminalStyle style)
    {
        var foreground = style.Foreground ?? DefaultForeground;

        if (!style.Dim)
            return foreground;

        var background = style.Background ?? DefaultBackground;
        return new TerminalColor(
            Blend(foreground.Red, background.Red),
            Blend(foreground.Green, background.Green),
            Blend(foreground.Blue, background.Blue));
    }

    private static byte Blend(byte foreground, byte background)
    {
        return (byte)((foreground * 0.55) + (background * 0.45));
    }

    private short GetColumns()
    {
        var scrollViewer = FindVisualChild<ScrollViewer>(TerminalTextBox);
        var viewportWidth = scrollViewer?.ViewportWidth ?? 0;
        var width = viewportWidth > 0
            ? viewportWidth
            : TerminalTextBox.ActualWidth;
        var horizontalPadding =
            TerminalTextBox.Padding.Left +
            TerminalTextBox.Padding.Right +
            6;
        var usableWidth = Math.Max(80, width - horizontalPadding);
        return (short)Math.Clamp(
            (int)Math.Floor(usableWidth / _cellWidth),
            10,
            300);
    }

    private short GetRows()
    {
        var height = Math.Max(120, TerminalTextBox.ActualHeight - 25);
        return (short)Math.Clamp((int)(height / _lineHeight), 10, 100);
    }

    private static T? FindVisualChild<T>(DependencyObject parent)
        where T : DependencyObject
    {
        for (var index = 0;
             index < VisualTreeHelper.GetChildrenCount(parent);
             index++)
        {
            var child = VisualTreeHelper.GetChild(parent, index);

            if (child is T match)
                return match;

            var descendant = FindVisualChild<T>(child);

            if (descendant is not null)
                return descendant;
        }

        return null;
    }

    private readonly record struct LinkSegment(string Text, Uri? Uri);

    private sealed record RenderedLine(
        Paragraph Paragraph,
        string Key,
        IReadOnlyList<RunPosition> Runs);

    private readonly record struct RunPosition(
        Run Run,
        int Start,
        int Length);
}
