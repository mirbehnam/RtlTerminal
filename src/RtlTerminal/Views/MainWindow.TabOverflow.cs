using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;

namespace RtlTerminal;

public partial class MainWindow
{
    private sealed record TabLayoutEntry(TerminalTab Tab, Grid Frame, System.Windows.Shapes.Path Shape, double Minimum, bool Active);
    private readonly List<TabLayoutEntry> _tabLayoutEntries = [];
    private TerminalTab? _lastTabForReveal, _tabScrollAnchor;
    private double _tabAnchorOffset;
    private bool _tabLayoutQueued, _revealActiveTab;
    private const double TabOverlap = 8;
    private const double TabEdgeTolerance = .5;

    private void CaptureTabScrollAnchor()
    {
        if (_tabScrollAnchor != null || TabScroller == null) return;
        double start = 0;
        foreach (var item in _tabLayoutEntries)
        {
            var width = Math.Max(0, item.Frame.Width - TabOverlap);
            if (start + width > TabScroller.HorizontalOffset + TabEdgeTolerance)
            { _tabScrollAnchor = item.Tab; _tabAnchorOffset = TabScroller.HorizontalOffset - start; return; }
            start += width;
        }
    }
    private void QueueTabLayout()
    {
        if (_tabLayoutQueued || TabScroller == null) return;
        _tabLayoutQueued = true;
        Dispatcher.BeginInvoke(DispatcherPriority.Loaded, new Action(() => { _tabLayoutQueued = false; UpdateTabLayout(); }));
    }
    private void UpdateTabLayout()
    {
        if (TitleTabsHost.ActualWidth <= 0) return;
        CaptureTabScrollAnchor();
        var scale = VisualTreeHelper.GetDpi(this).DpiScaleX;
        double Minimum(TabLayoutEntry item) => Math.Ceiling(item.Minimum * scale) / scale;
        var actionSpace = NewTerminalControls.Width + NewTerminalControls.Margin.Left + NewTerminalControls.Margin.Right;
        var available = Math.Max(0, TitleTabsHost.ActualWidth - actionSpace - (AdministratorBadge.Visibility == Visibility.Visible ? 30 : 0));
        var overflow = _tabLayoutEntries.Sum(t => Minimum(t) - TabOverlap) > available + TabEdgeTolerance;
        var arrowSpace = ScrollTabsLeftButton.Width + ScrollTabsLeftButton.Margin.Left + ScrollTabsLeftButton.Margin.Right
            + ScrollTabsRightButton.Width + ScrollTabsRightButton.Margin.Left + ScrollTabsRightButton.Margin.Right;
        var viewport = Math.Max(0, available - (overflow ? arrowSpace : 0));
        ScrollTabsLeftButton.Visibility = ScrollTabsRightButton.Visibility = overflow ? Visibility.Visible : Visibility.Collapsed;
        TabScroller.MaxWidth = viewport;
        // Water filling gives short titles room to shrink, without truncating normal titles.
        double low = 80, high = 248;
        for (var i = 0; i < 32; i++)
        {
            var cap = (low + high) / 2;
            if (_tabLayoutEntries.Sum(t => Math.Max(Minimum(t), cap) - TabOverlap) <= viewport) low = cap; else high = cap;
        }
        var maximum = overflow ? 0 : low;
        foreach (var item in _tabLayoutEntries)
        {
            // Round down the shared cap: per-tab layout rounding must not create hidden overflow.
            var width = overflow ? Minimum(item) : Math.Max(Minimum(item), Math.Floor(maximum * scale + .000001) / scale);
            if (Math.Abs(item.Frame.Width - width) < .01) continue;
            item.Frame.Width = width;
            var right = width - 4; var arc = width - 12;
            var path = item.Active
                ? FormattableString.Invariant($"M0,32 Q4,32 4,28 L4,8 A8,8 0 0 1 12,0 L{arc},0 A8,8 0 0 1 {right},8 L{right},28 Q{right},32 {width},32 Z")
                : FormattableString.Invariant($"M4,32 L4,8 A8,8 0 0 1 12,0 L{arc},0 A8,8 0 0 1 {right},8 L{right},32 Z");
            var geometry = Geometry.Parse(path); geometry.Freeze(); item.Shape.Data = geometry;
        }
        // Complete this measure before reading offsets or revealing a newly selected tab.
        TabScroller.UpdateLayout();
        var offset = overflow ? TabScroller.HorizontalOffset : 0;
        double start = 0;
        foreach (var item in _tabLayoutEntries)
        {
            var width = item.Frame.Width - TabOverlap;
            if (ReferenceEquals(item.Tab, _tabScrollAnchor)) offset = start + Math.Min(_tabAnchorOffset, Math.Max(0, width - 1));
            start += width;
        }
        if (_revealActiveTab)
        {
            start = 0;
            foreach (var item in _tabLayoutEntries)
            {
                var width = item.Frame.Width - TabOverlap;
                if (ReferenceEquals(item.Tab, _activeTab))
                {
                    if (start < offset) offset = start;
                    else if (start + width > offset + TabScroller.ViewportWidth) offset = Math.Min(start, start + width - TabScroller.ViewportWidth);
                    break;
                }
                start += width;
            }
        }
        _tabScrollAnchor = null; _revealActiveTab = false;
        TabScroller.ScrollToHorizontalOffset(overflow ? Math.Clamp(offset, 0, TabScroller.ScrollableWidth) : 0);
        UpdateTabScrollButtons();
    }
    private void UpdateTabScrollButtons()
    {
        if (ScrollTabsLeftButton == null || TabScroller == null) return;
        ScrollTabsLeftButton.IsEnabled = TabScroller.HorizontalOffset > TabEdgeTolerance;
        ScrollTabsRightButton.IsEnabled = TabScroller.HorizontalOffset < TabScroller.ScrollableWidth - TabEdgeTolerance;
    }
    private void TabScroller_ScrollChanged(object sender, ScrollChangedEventArgs e) => UpdateTabScrollButtons();
    private void ScrollTabsLeft_Click(object sender, RoutedEventArgs e) => ScrollTabs(false);
    private void ScrollTabsRight_Click(object sender, RoutedEventArgs e) => ScrollTabs(true);
    private void ScrollTabs(bool right)
    {
        var offset = TabScroller.HorizontalOffset; var target = right ? TabScroller.ScrollableWidth : 0;
        double start = 0;
        foreach (var item in _tabLayoutEntries)
        {
            var end = start + item.Frame.Width - TabOverlap;
            if (right && end > offset + TabScroller.ViewportWidth + TabEdgeTolerance)
            { target = end - TabScroller.ViewportWidth; break; }
            if (!right && start < offset - TabEdgeTolerance) target = start;
            start = end;
        }
        TabScroller.ScrollToHorizontalOffset(Math.Clamp(target, 0, TabScroller.ScrollableWidth));
    }
}
