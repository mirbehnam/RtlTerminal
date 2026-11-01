using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;

namespace RtlTerminal;

// A local text adapter for code-built proxy windows. Original English strings are
// retained across language changes. Editable/raw text and protocol codes are never
// traversed; layout, FlowDirection and provider identifiers are never changed.
internal sealed class ProxyUiLanguage
{
    private readonly Window window;
    private readonly Action? refreshed;
    private readonly Dictionary<(DependencyObject, DependencyProperty), (string Source, string Rendered)> sources = [];
    private bool disposed, applying, queued;
    private ProxyUiLanguage(Window window, Action? refreshed)
    {
        this.window = window; this.refreshed = refreshed;
        UiText.Changed += Refresh;
        window.LayoutUpdated += QueueRefresh;
        window.Closed += (_, _) => { disposed = true; UiText.Changed -= Refresh; window.LayoutUpdated -= QueueRefresh; sources.Clear(); };
        Refresh();
    }
    public static void Attach(Window window, Action? refreshed = null) => _ = new ProxyUiLanguage(window, refreshed);
    private void QueueRefresh(object? sender, EventArgs e)
    {
        if (queued || applying || disposed) return;
        queued = true;
        window.Dispatcher.BeginInvoke(DispatcherPriority.Background, new Action(() => { queued = false; Refresh(); }));
    }
    private void Refresh()
    {
        if (disposed || applying) return;
        if (!window.Dispatcher.CheckAccess()) { window.Dispatcher.BeginInvoke(new Action(Refresh)); return; }
        applying = true;
        try
        {
            var seen = new HashSet<DependencyObject>();
            void Visit(DependencyObject element)
            {
                if (!seen.Add(element)) return;
                if (element is FrameworkElement { Tag: "ProxyLiteral" }) return;
                Translate(element, System.Windows.Automation.AutomationProperties.NameProperty);
                if (element is TextBox or ComboBox) return;
                if (element is Window) Translate(element, Window.TitleProperty);
                if (element is TextBlock) Translate(element, TextBlock.TextProperty);
                if (element is ContentControl) Translate(element, ContentControl.ContentProperty);
                if (element is HeaderedContentControl) Translate(element, HeaderedContentControl.HeaderProperty);
                if (element is FrameworkElement) Translate(element, FrameworkElement.ToolTipProperty);
                foreach (var child in LogicalTreeHelper.GetChildren(element).OfType<DependencyObject>()) Visit(child);
                if (element is Visual)
                    for (var i = 0; i < VisualTreeHelper.GetChildrenCount(element); i++) Visit(VisualTreeHelper.GetChild(element, i));
            }
            Visit(window); refreshed?.Invoke();
        }
        finally { applying = false; }
    }
    private void Translate(DependencyObject element, DependencyProperty property)
    {
        if (element.GetValue(property) is not string current || current.Length == 0) return;
        var key = (element, property);
        var source = sources.TryGetValue(key, out var old) && current == old.Rendered ? old.Source : current;
        var translated = Text(source);
        sources[key] = (source, translated);
        if (translated != current) element.SetCurrentValue(property, translated);
    }
    public static string Text(string source)
    {
        if (source.StartsWith("Not started: ", StringComparison.Ordinal))
            return UiText.Format("Not started: {0}", Text(source["Not started: ".Length..]));
        if (source.StartsWith("Snapshot: reading ", StringComparison.Ordinal))
            return UiText.Format("Snapshot: reading {0}", UiText.Get(source["Snapshot: reading ".Length..]));
        const string readFailure = "Snapshot/read failed for ";
        if (source.StartsWith(readFailure, StringComparison.Ordinal))
        {
            var split = source.IndexOf(": ", readFailure.Length, StringComparison.Ordinal);
            if (split > 0) return UiText.Format("Snapshot/read failed for {0}: {1}", UiText.Get(source[readFailure.Length..split]), Text(source[(split + 2)..]));
        }
        const string historyEnd = " · Complete (managed scope)";
        if (source.EndsWith(historyEnd, StringComparison.Ordinal))
        {
            var first = source.IndexOf(" · ", StringComparison.Ordinal);
            var second = first < 0 ? -1 : source.IndexOf(" · ", first + 3, StringComparison.Ordinal);
            if (second > 0)
                return UiText.Format("{0} · {1} · {2} · Complete (managed scope)", source[..first],
                    UiText.Get(source[(first + 3)..second]),
                    string.Join(", ", source[(second + 3)..^historyEnd.Length].Split(", ").Select(UiText.Get)));
        }
        var whole = UiText.Get(source);
        if (whole != source) return whole;
        return string.Join("\n", source.Split('\n').Select(line =>
        {
            var translated = UiText.Get(line);
            if (translated != line) return translated;
            foreach (var prefix in new[] { "✓ Restored ", "! Failed ", "! Skipped ", "! Conflict ", "! RequiresPermission " })
                if (line.StartsWith(prefix, StringComparison.Ordinal))
                    return prefix[..2] + UiText.Get(prefix[2..].Trim()) + " " + Text(line[prefix.Length..]);
            var colon = line.IndexOf(": ", StringComparison.Ordinal);
            return colon > 0 ? UiText.Get(line[..colon]) + ": " + UiText.Get(line[(colon + 2)..]) : line;
        }));
    }
    public static MessageBoxResult Show(Window owner, string text, string caption, MessageBoxButton buttons, MessageBoxImage image)
        => MessageBox.Show(owner, Text(text), UiText.Get(caption), buttons, image);
    public static string Diagnostics(string raw) => string.Join("\n", raw.Split('\n').Select(line =>
    {
        var colon = line.IndexOf(':');
        if (colon >= 0)
        {
            var label = line[..colon];
            var remainder = line[(colon + 1)..];
            if (remainder.EndsWith(" = (unset)", StringComparison.Ordinal)) remainder = remainder[..^7] + UiText.Get("(unset)");
            return UiText.Get(label) + ":" + remainder;
        }
        return UiText.Get(line);
    }));
}
