using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;

namespace RtlTerminal;

public partial class MainWindow
{
    private bool _receivingDetachedTab;
    private TerminalTab? _draggingTab;
    private Point _tabDragStart;
    private FrameworkElement? _dragFeedbackVisual;
    private Button? _dragFeedbackButton;
    private double _dragOriginalOpacity;
    private Transform? _dragOriginalTransform;
    private object? _dragOriginalCursor;

    private void InitializeTabDetach()
    {
        PreviewKeyDown += (_, e) =>
        {
            if (e.Key != Key.Escape || _draggingTab == null) return;
            ClearTabDrag(); Mouse.Capture(null); e.Handled = true;
        };
        Deactivated += (_, _) => ClearTabDrag();
        Closed += (_, _) => ClearTabDrag();
    }

    private void WireTabDrag(Button button, TerminalTab tab)
    {
        button.PreviewMouseLeftButtonDown += (_, e) =>
        {
            if (e.ClickCount != 1 || _renamingTab != null) return;
            ClearTabDrag();
            _draggingTab = tab; _tabDragStart = e.GetPosition(this);
        };
        button.PreviewMouseMove += (_, e) =>
        {
            if (!ReferenceEquals(_draggingTab, tab)) return;
            if (e.LeftButton != MouseButtonState.Pressed) { ClearTabDrag(); return; }
            if (TabDragThresholdReached(_tabDragStart, e.GetPosition(this))) BeginTabDragFeedback(button, tab);
        };
        button.LostMouseCapture += (_, _) => ClearTabDrag();
        button.PreviewMouseLeftButtonUp += (_, e) =>
        {
            var source = _draggingTab;
            ClearTabDrag();
            if (!ReferenceEquals(source, tab)) return;
            var point = e.GetPosition(this);
            if (!ShouldDetachTab(_tabDragStart, point, new Size(ActualWidth, ActualHeight))) return;
            e.Handled = true;
            Mouse.Capture(null);
            var screenPoint = PointToScreen(point);
            Dispatcher.BeginInvoke(DispatcherPriority.Input, new Action(() => DetachTabToWindow(tab, screenPoint)));
        };
    }

    private void BeginTabDragFeedback(Button button, TerminalTab tab)
    {
        if (_dragFeedbackVisual != null) return;
        DependencyObject? parent = button;
        while (parent is not null && parent is not Grid { Tag: TerminalTab }) parent = VisualTreeHelper.GetParent(parent);
        if (parent is not FrameworkElement visual || !ReferenceEquals(visual.Tag, tab)) return;
        _dragFeedbackVisual = visual; _dragFeedbackButton = button;
        _dragOriginalOpacity = visual.Opacity; _dragOriginalTransform = visual.RenderTransform;
        _dragOriginalCursor = button.ReadLocalValue(CursorProperty);
        visual.Opacity = .7;
        visual.RenderTransform = new TranslateTransform(0, -2);
        button.Cursor = Cursors.SizeAll;
    }

    private void ClearTabDrag()
    {
        _draggingTab = null;
        if (_dragFeedbackVisual is { } visual)
        {
            visual.Opacity = _dragOriginalOpacity;
            visual.RenderTransform = _dragOriginalTransform ?? Transform.Identity;
        }
        if (_dragFeedbackButton is { } button)
        {
            if (_dragOriginalCursor == DependencyProperty.UnsetValue) button.ClearValue(CursorProperty);
            else button.SetValue(CursorProperty, _dragOriginalCursor);
        }
        _dragFeedbackVisual = null; _dragFeedbackButton = null; _dragOriginalTransform = null; _dragOriginalCursor = null;
    }

    internal static bool TabDragThresholdReached(Point start, Point end) =>
        Math.Abs(end.X - start.X) >= SystemParameters.MinimumHorizontalDragDistance ||
        Math.Abs(end.Y - start.Y) >= SystemParameters.MinimumVerticalDragDistance;

    internal static bool ShouldDetachTab(Point start, Point end, Size windowSize) =>
        TabDragThresholdReached(start, end) &&
        !new Rect(new Point(), windowSize).Contains(end);

    private MainWindow? DetachTabToWindow(TerminalTab tab, Point screenPoint)
    {
        if (!_tabs.Contains(tab) || _windowClosed || _quickWindow != null) return null;
        if (tab.Panes.Any(p => p == _macroPane) && (_macroRecorder != null || _macroCancellation != null || _macroPreparing))
        { UiText.Show(this, "Stop macro recording or playback before moving this tab.", "Move tab"); return null; }
        if (tab.Panes.Any(p => p.Session is null && !p.SessionEnded)) return null;
        if (_renamingTab != null) FinishTabRename(true, false, false);
        StorePaneSizes();
        var destination = new MainWindow(_directoryHistory) { _receivingDetachedTab = true, Width = Width, Height = Height, WindowStartupLocation = WindowStartupLocation.Manual };
        destination._historySize = _historySize;
        destination.SmartRtlMenuItem.IsChecked = SmartRtlMenuItem.IsChecked;
        destination.RowRtlMenuItem.IsChecked = RowRtlMenuItem.IsChecked;
        destination.ApplyFontSettings(new TerminalFontSettings(BundledFonts.DisplayName(tab.ActivePane.View.FontFamily),
            tab.ActivePane.View.FontSize, tab.ActivePane.View.FontWeight >= FontWeights.Bold, tab.ActivePane.View.FontStyle == FontStyles.Italic));
        var dpi = VisualTreeHelper.GetDpi(this);
        destination.Left = screenPoint.X / dpi.DpiScaleX - 120;
        destination.Top = screenPoint.Y / dpi.DpiScaleY - 24;
        var index = _tabs.IndexOf(tab);
        var wasActive = ReferenceEquals(tab, _activeTab);
        var oldOffsets = tab.Panes.ToDictionary(p => p, p => p.IsShown ? p.View.VerticalOffset : p.VerticalOffset);
        // Replace the source template reference so a future tab cannot steal this view back.
        if (tab.Panes.Any(p => ReferenceEquals(p.View, TerminalTextBox)))
        {
            var template = CreateTerminalView();
            UnregisterName("TerminalTextBox"); TerminalTextBox = template;
            template.Name = "TerminalTextBox"; RegisterName("TerminalTextBox", template);
        }
        try
        {
            foreach (var pane in tab.Panes)
            {
                UnwireTerminalView(pane.View);
                destination.WireTerminalView(pane.View);
                lock (_renderLock)
                {
                    pane.IsShown = false;
                    pane.VerticalOffset = oldOffsets[pane];
                    pane.Window = destination;
                }
                if (pane.Session is { } session && _quickDrafts.Remove(session.SessionId, out var draft)) destination._quickDrafts[session.SessionId] = draft;
            }
            _tabs.Remove(tab);
            if (wasActive)
            {
                _activeTab = null;
                if (_tabs.Count > 0) ShowTab(_tabs[Math.Min(index, _tabs.Count - 1)]);
                else { PaneHost.Children.Clear(); _paneLayoutGrids.Clear(); RebuildTabStrip(); }
            }
            else RebuildTabStrip();
            destination._tabs.Add(tab);
            destination._nextTabNumber = Math.Max(destination._nextTabNumber, tab.Panes.Max(p => p.Number) + 1);
            destination.ShowTab(tab);
            destination.Show(); destination.Activate(); destination.ActiveView.Focus();
        }
        catch (Exception ex)
        {
            // The live sessions still belong to the source if window creation fails.
            destination._tabs.Clear(); destination._activeTab = null; destination.PaneHost.Children.Clear();
            foreach (var pane in tab.Panes)
            {
                destination.UnwireTerminalView(pane.View); WireTerminalView(pane.View); pane.Window = this;
                if (pane.Session is { } session && destination._quickDrafts.Remove(session.SessionId, out var draft)) _quickDrafts[session.SessionId] = draft;
            }
            if (!_tabs.Contains(tab)) _tabs.Insert(Math.Min(index, _tabs.Count), tab);
            ShowTab(tab); destination.Close();
            UiText.Show(this, ex.Message, "Move tab"); return null;
        }
        if (_tabs.Count == 0)
        {
            if (ReferenceEquals(Application.Current?.MainWindow, this)) Application.Current.MainWindow = destination;
            Close();
        }
        return destination;
    }

    private void TerminalView_LinkRequested(Uri uri)
    {
        try { Process.Start(new ProcessStartInfo(uri.AbsoluteUri) { UseShellExecute = true }); }
        catch (Exception ex) { UiText.Show(this, ex.Message, "Open link", MessageBoxButton.OK, MessageBoxImage.Error); }
    }

    private void UnwireTerminalView(TerminalView view)
    {
        view.LinkRequested -= TerminalView_LinkRequested;
        view.RemoveHandler(ScrollViewer.ScrollChangedEvent, new ScrollChangedEventHandler(TerminalTextBox_ScrollChanged));
        view.PreviewKeyDown -= TerminalTextBox_PreviewKeyDown; view.PreviewKeyUp -= TerminalTextBox_PreviewKeyUp;
        view.PreviewMouseDown -= TerminalTextBox_PreviewMouseDown; view.PreviewMouseUp -= TerminalTextBox_PreviewMouseUp;
        view.PreviewMouseMove -= TerminalTextBox_PreviewMouseMove; view.PreviewMouseWheel -= TerminalTextBox_PreviewMouseWheel;
        view.PreviewMouseRightButtonDown -= TerminalTextBox_PreviewMouseRightButtonDown;
        view.GotKeyboardFocus -= TerminalTextBox_GotKeyboardFocus; view.LostKeyboardFocus -= TerminalTextBox_LostKeyboardFocus;
        view.PreviewTextInput -= TerminalTextBox_PreviewTextInput;
    }
}
