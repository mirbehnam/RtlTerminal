using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace RtlTerminal;

public sealed record QuickInputAttachment(string Path, bool Image, int Number);
public sealed class QuickInputDraft
{
    public RichTextBox Editor { get; } = new() { AcceptsTab = true, IsDocumentEnabled = true, AllowDrop = false, VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
        HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled, FontFamily = new FontFamily("Segoe UI"), FontSize = 17,
        Padding = new Thickness(10), BorderThickness = new Thickness(0), IsUndoEnabled = true, UndoLimit = 100 };
    private int imageNumber, fileNumber;
    public HashSet<string> RetainedImages { get; } = new(StringComparer.OrdinalIgnoreCase);
    public string Title { get; set; } = "Quick Input";
    public void Recover(QuickInputDraft source)
    {
        var attachments = new Dictionary<string, QuickInputAttachment>();
        var text = source.Text(a => { var key = "[rtl-" + Guid.NewGuid().ToString("N") + "]"; attachments[key] = a; return key; });
        foreach (var piece in System.Text.RegularExpressions.Regex.Split(text, @"(\[rtl-[a-f0-9]{32}\])"))
            if (attachments.TryGetValue(piece, out var attachment)) InsertAttachment(attachment.Path, attachment.Image); else InsertText(piece);
    }
    public QuickInputDraft()
    {
        Editor.Document = new FlowDocument(new Paragraph()) { PagePadding = new Thickness(0) };
        Editor.Document.Resources.Add(typeof(Paragraph), new Style(typeof(Paragraph)) { Setters = { new Setter(Block.MarginProperty, new Thickness(0)) } });
        Editor.SetResourceReference(Control.BackgroundProperty, "TerminalBackgroundBrush");
        Editor.SetResourceReference(Control.ForegroundProperty, "ChromeForegroundBrush");
        var menu = new ContextMenu();
        foreach (var command in new[] { System.Windows.Input.ApplicationCommands.Cut, System.Windows.Input.ApplicationCommands.Copy, System.Windows.Input.ApplicationCommands.Paste, System.Windows.Input.ApplicationCommands.SelectAll })
            menu.Items.Add(new MenuItem { Header = command.Text, Command = command, CommandTarget = Editor });
        menu.Items.Add(new Separator());
        var clearUndo = new MenuItem { Header = UiText.Get("Clear undo history") };
        clearUndo.Click += (_, _) => { Editor.IsUndoEnabled = false; Editor.IsUndoEnabled = true; RetainedImages.IntersectWith(Attachments().Where(a => a.Image).Select(a => a.Path)); };
        menu.Items.Add(clearUndo); Editor.ContextMenu = menu;
    }
    public void InsertText(string text)
    {
        if (System.Text.Encoding.UTF8.GetByteCount(text) > QuickInputText.MaximumBytes) throw new ArgumentException("Quick Input supports up to 1 MB of text. Shorten the text and try again.");
        Editor.BeginChange();
        try
        {
            Editor.Selection.Text = "";
            var pointer = Editor.CaretPosition.GetInsertionPosition(LogicalDirection.Forward);
            var pieces = System.Text.RegularExpressions.Regex.Split(text, "(\r\n|\r|\n)");
            foreach (var piece in pieces)
            {
                if (piece is "\r" or "\n" or "\r\n")
                { var line = new LineBreak(pointer) { Tag = piece }; pointer = line.ElementEnd; }
                else if (piece.Length > 0) { var run = new Run(piece, pointer); pointer = run.ContentEnd; }
            }
            Editor.CaretPosition = pointer;
        }
        finally { Editor.EndChange(); }
    }
    public void InsertAttachment(string path, bool image)
    {
        Editor.BeginChange();
        try
        {
        if (image) RetainedImages.Add(path);
        var value = new QuickInputAttachment(path, image, image ? ++imageNumber : ++fileNumber);
        var content = new TextBlock { Text = $"[{(image ? "Image" : "File")} {value.Number}]", Padding = new Thickness(5, 1, 5, 1), Foreground = Brushes.DodgerBlue, FlowDirection = FlowDirection.LeftToRight };
        var tooltip = new StackPanel(); tooltip.Children.Add(new TextBlock { Text = path, TextWrapping = TextWrapping.Wrap, MaxWidth = 400 });
        if (image || new[] { ".png", ".jpg", ".jpeg", ".bmp", ".gif", ".webp" }.Contains(System.IO.Path.GetExtension(path), StringComparer.OrdinalIgnoreCase))
        {
            try
            {
                var bitmap = new BitmapImage(); bitmap.BeginInit(); bitmap.CacheOption = BitmapCacheOption.OnLoad;
                bitmap.DecodePixelWidth = 240; bitmap.UriSource = new Uri(path); bitmap.EndInit(); bitmap.Freeze();
                tooltip.Children.Add(new Image { Source = bitmap, MaxWidth = 240, MaxHeight = 160, Margin = new Thickness(0, 6, 0, 0) });
            }
            catch (Exception ex) when (ex is IOException or NotSupportedException or ArgumentException) { }
        }
        var preview = new ToolTip { Content = tooltip, Padding = new Thickness(9), BorderThickness = new Thickness(1) };
        preview.SetResourceReference(Control.BackgroundProperty, "MenuBackgroundBrush"); preview.SetResourceReference(Control.ForegroundProperty, "ChromeForegroundBrush"); preview.SetResourceReference(Control.BorderBrushProperty, "ToolbarBorderBrush"); content.ToolTip = preview;
        Editor.Selection.Text = "";
        var inline = new InlineUIContainer(content, Editor.CaretPosition.GetInsertionPosition(LogicalDirection.Forward)) { Tag = value, BaselineAlignment = BaselineAlignment.Center };
        Editor.CaretPosition = inline.ElementEnd.GetInsertionPosition(LogicalDirection.Forward);
        }
        finally { Editor.EndChange(); }
    }
    public IEnumerable<QuickInputAttachment> Attachments()
    {
        for (var p = Editor.Document.ContentStart; p != null && p.CompareTo(Editor.Document.ContentEnd) < 0; p = p.GetNextContextPosition(LogicalDirection.Forward))
            if (p.GetPointerContext(LogicalDirection.Forward) == TextPointerContext.ElementStart && p.GetAdjacentElement(LogicalDirection.Forward) is InlineUIContainer { Tag: QuickInputAttachment attachment }) yield return attachment;
    }
    public string Text(Func<QuickInputAttachment, string> attachment, TextPointer? start = null, TextPointer? end = null)
    {
        start ??= Editor.Document.ContentStart; end ??= Editor.Document.ContentEnd;
        var text = new System.Text.StringBuilder(); var p = start;
        while (p != null && p.CompareTo(end) < 0)
        {
            var context = p.GetPointerContext(LogicalDirection.Forward);
            if (context == TextPointerContext.Text)
            {
                var run = p.GetTextInRun(LogicalDirection.Forward);
                var length = run.Length;
                while (length > 0 && p.GetPositionAtOffset(length)!.CompareTo(end) > 0) length--;
                text.Append(run.AsSpan(0, length)); p = p.GetPositionAtOffset(length); if (length == 0) break; continue;
            }
            var element = p.GetAdjacentElement(LogicalDirection.Forward);
            if (context == TextPointerContext.ElementStart && element is LineBreak line) text.Append(line.Tag as string ?? "\n");
            if (context == TextPointerContext.ElementStart && element is InlineUIContainer { Tag: QuickInputAttachment value } token)
            { if (token.ElementEnd.CompareTo(end) <= 0) text.Append(attachment(value)); p = token.ElementEnd; continue; }
            if (context == TextPointerContext.ElementEnd && element is Paragraph paragraph && paragraph.NextBlock != null) text.Append('\n');
            p = p.GetNextContextPosition(LogicalDirection.Forward);
        }
        return text.ToString();
    }
}

public sealed class QuickInputImages : IDisposable
{
    private readonly object gate = new();
    private readonly List<string> images = [];
    public string Save(BitmapSource bitmap, IReadOnlySet<string> pinned)
    {
        lock (gate) return SaveCore(bitmap, pinned);
    }
    private string SaveCore(BitmapSource bitmap, IReadOnlySet<string> pinned)
    {
        if (images.Count >= 20)
        {
            var obsolete = images.FirstOrDefault(p => !pinned.Contains(p));
            if (obsolete == null) throw new IOException("All 20 temporary images are still in use. Remove an attachment and clear its undo history before adding another image.");
            File.Delete(obsolete); images.Remove(obsolete);
        }
        var directory = Path.Combine(Path.GetTempPath(), "RtlTerminal", "QuickInput", Environment.ProcessId.ToString()); Directory.CreateDirectory(directory);
        var path = Path.Combine(directory, Guid.NewGuid().ToString("N") + ".png");
        var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using (var stream = new FileStream(path, FileMode.CreateNew)) encoder.Save(stream);
        images.Add(path); return path;
    }
    public void Dispose() { lock (gate) foreach (var path in images) try { File.Delete(path); } catch (IOException) { } catch (UnauthorizedAccessException) { } }
}
