using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace RtlTerminal;

public partial class MainWindow
{
    private readonly List<SearchMatch> _searchMatches = [];
    private int _currentSearchIndex = -1;

    private readonly record struct SearchMatch(int Row, int Start, int Length);

    private void FindMenuItem_Click(object sender, RoutedEventArgs e) => OpenSearchBar();

    private void OpenSearchBar()
    {
        SearchBar.Visibility = Visibility.Visible;
        var view = ActiveView;
        if (view.HasSelection)
        {
            var selected = view.GetSelectedText().Trim();
            if (!string.IsNullOrEmpty(selected))
            {
                var firstLine = selected.Split(["\r\n", "\r", "\n"], StringSplitOptions.None)[0].Trim();
                if (!string.IsNullOrEmpty(firstLine))
                {
                    SearchTextBox.Text = firstLine;
                }
            }
        }
        SearchTextBox.Focus();
        SearchTextBox.SelectAll();
        SearchPlaceholderText.Visibility = string.IsNullOrEmpty(SearchTextBox.Text) ? Visibility.Visible : Visibility.Collapsed;
        UpdateSearch(preserveCurrent: false);
    }

    private void CloseSearchBar()
    {
        if (SearchBar.Visibility != Visibility.Visible) return;
        SearchBar.Visibility = Visibility.Collapsed;
        ActiveView.Focus();
    }

    private void SearchTextBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        SearchPlaceholderText.Visibility = string.IsNullOrEmpty(SearchTextBox.Text) ? Visibility.Visible : Visibility.Collapsed;
        UpdateSearch(preserveCurrent: false);
    }

    private void SearchTextBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            var shiftPressed = (Keyboard.Modifiers & ModifierKeys.Shift) != 0;
            if (shiftPressed)
                FindPrevious();
            else
                FindNext();
            e.Handled = true;
            return;
        }

        if (e.Key == Key.Escape)
        {
            CloseSearchBar();
            e.Handled = true;
            return;
        }
    }

    private void SearchCaseSensitiveButton_Click(object sender, RoutedEventArgs e) => UpdateSearch(preserveCurrent: false);

    private void SearchPreviousButton_Click(object sender, RoutedEventArgs e) => FindPrevious();

    private void SearchNextButton_Click(object sender, RoutedEventArgs e) => FindNext();

    private void SearchCloseButton_Click(object sender, RoutedEventArgs e) => CloseSearchBar();

    private void UpdateSearch(bool preserveCurrent = false)
    {
        var query = SearchTextBox.Text;
        if (string.IsNullOrEmpty(query))
        {
            _searchMatches.Clear();
            _currentSearchIndex = -1;
            SearchCounterText.Text = string.Empty;
            SearchPreviousButton.IsEnabled = false;
            SearchNextButton.IsEnabled = false;
            ActiveView.ClearSelection();
            return;
        }

        var snapshot = ActiveSnapshot ?? ActiveBuffer?.CaptureSnapshot();
        if (snapshot is null || snapshot.Lines.Count == 0)
        {
            _searchMatches.Clear();
            _currentSearchIndex = -1;
            SearchCounterText.Text = UiText.Get("No results");
            SearchPreviousButton.IsEnabled = false;
            SearchNextButton.IsEnabled = false;
            ActiveView.ClearSelection();
            return;
        }

        var matchCase = SearchCaseSensitiveButton.IsChecked == true;
        var comparison = matchCase ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase;

        var previousMatch = (_currentSearchIndex >= 0 && _currentSearchIndex < _searchMatches.Count)
            ? _searchMatches[_currentSearchIndex]
            : default(SearchMatch?);

        _searchMatches.Clear();

        for (var row = 0; row < snapshot.Lines.Count; row++)
        {
            var lineText = snapshot.Lines[row].Text;
            if (lineText.Length < query.Length) continue;

            var start = 0;
            while (start <= lineText.Length - query.Length)
            {
                var found = lineText.IndexOf(query, start, comparison);
                if (found < 0) break;
                _searchMatches.Add(new SearchMatch(row, found, query.Length));
                start = found + Math.Max(1, query.Length);
            }
        }

        if (_searchMatches.Count == 0)
        {
            _currentSearchIndex = -1;
            SearchCounterText.Text = UiText.Get("No results");
            SearchPreviousButton.IsEnabled = false;
            SearchNextButton.IsEnabled = false;
            ActiveView.ClearSelection();
            return;
        }

        SearchPreviousButton.IsEnabled = true;
        SearchNextButton.IsEnabled = true;

        if (preserveCurrent && previousMatch.HasValue)
        {
            var targetRow = previousMatch.Value.Row;
            var targetStart = previousMatch.Value.Start;
            var idx = _searchMatches.FindIndex(m => m.Row == targetRow && m.Start >= targetStart);
            if (idx < 0)
                idx = _searchMatches.FindIndex(m => m.Row > targetRow);
            _currentSearchIndex = idx >= 0 ? idx : 0;
        }
        else
        {
            var currentScrollRow = (int)Math.Floor(ActiveView.VerticalOffset / _lineHeight);
            var idx = _searchMatches.FindIndex(m => m.Row >= currentScrollRow);
            _currentSearchIndex = idx >= 0 ? idx : 0;
        }

        SelectAndScrollToCurrentMatch();
    }

    private void SelectAndScrollToCurrentMatch()
    {
        if (_currentSearchIndex < 0 || _currentSearchIndex >= _searchMatches.Count) return;

        var match = _searchMatches[_currentSearchIndex];
        var view = ActiveView;
        view.SelectionState = ((match.Row, match.Start), (match.Row, match.Start + match.Length));
        view.ScrollToVerticalOffset(match.Row * _lineHeight);
        SearchCounterText.Text = UiText.Format("{0} of {1}", _currentSearchIndex + 1, _searchMatches.Count);
    }

    private void FindNext()
    {
        if (_searchMatches.Count == 0)
        {
            UpdateSearch(false);
            return;
        }
        _currentSearchIndex = (_currentSearchIndex + 1) % _searchMatches.Count;
        SelectAndScrollToCurrentMatch();
    }

    private void FindPrevious()
    {
        if (_searchMatches.Count == 0)
        {
            UpdateSearch(false);
            return;
        }
        _currentSearchIndex = (_currentSearchIndex - 1 + _searchMatches.Count) % _searchMatches.Count;
        SelectAndScrollToCurrentMatch();
    }

    // Font Zoom and Fullscreen
    private static readonly DependencyPropertyKey IsFullScreenPropertyKey = DependencyProperty.RegisterReadOnly(
        nameof(IsFullScreen), typeof(bool), typeof(MainWindow), new PropertyMetadata(false));
    public static readonly DependencyProperty IsFullScreenProperty = IsFullScreenPropertyKey.DependencyProperty;
    public bool IsFullScreen => (bool)GetValue(IsFullScreenProperty);
    private WindowState _previousWindowState = WindowState.Normal;
    private WindowStyle _previousWindowStyle = WindowStyle.None;
    private ResizeMode _previousResizeMode = ResizeMode.CanResize;
    private Rect _previousWindowBounds;
    private System.Windows.Shell.WindowChrome? _previousWindowChrome;
    private bool _fullScreenTransitionPending;

    private void ViewMenu_Opened(object sender, RoutedEventArgs e) => UpdateZoomMenuVisibility();

    private void UpdateZoomMenuVisibility()
    {
        var baseline = Math.Clamp(AppSettings.LoadFont()?.Size ?? 14.0, 8, 48);
        var visibility = Math.Abs(TerminalTextBox.FontSize - baseline) > .01 ? Visibility.Visible : Visibility.Collapsed;
        ResetZoomMenuItem.Visibility = visibility;
        ResetZoomSeparator.Visibility = visibility;
    }

    private void KeyboardShortcutsMenuItem_Click(object sender, RoutedEventArgs e)
    {
        var guide = new GuideWindow { Owner = this };
        guide.ShowShortcuts();
        guide.ShowDialog();
    }
    private void ResetZoomMenuItem_Click(object sender, RoutedEventArgs e) => ResetFontSize();
    private void FullScreenMenuItem_Click(object sender, RoutedEventArgs e) => ToggleFullScreen();

    private void Window_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
    {
        if ((Keyboard.Modifiers & ModifierKeys.Control) != 0)
        {
            if (e.Delta > 0)
                ZoomFontSize(1);
            else if (e.Delta < 0)
                ZoomFontSize(-1);
            e.Handled = true;
        }
    }

    private void ZoomFontSize(int delta)
    {
        var currentSize = TerminalTextBox.FontSize;
        var newSize = Math.Clamp(currentSize + delta, 8, 48);
        if (Math.Abs(newSize - currentSize) < 0.01) return;
        _zoomReflowDue = Environment.TickCount64 + 180;
        ApplyFontSettings(new TerminalFontSettings(
            TerminalTextBox.FontFamily.Source,
            newSize,
            TerminalTextBox.FontWeight >= FontWeights.Bold,
            TerminalTextBox.FontStyle == FontStyles.Italic));
    }

    private void ResetFontSize()
    {
        var saved = AppSettings.LoadFont();
        var defaultSize = Math.Clamp(saved?.Size ?? 14.0, 8, 48);
        _zoomReflowDue = Environment.TickCount64 + 180;
        ApplyFontSettings(new TerminalFontSettings(
            TerminalTextBox.FontFamily.Source,
            defaultSize,
            TerminalTextBox.FontWeight >= FontWeights.Bold,
            TerminalTextBox.FontStyle == FontStyles.Italic));
    }

    private void ToggleFullScreen()
    {
        if (_fullScreenTransitionPending) return;
        _fullScreenTransitionPending = true;
        try
        {
            using var transition = new WindowTransition(this);
            ApplyFullScreen();
        }
        finally { _fullScreenTransitionPending = false; }
    }

    private void ApplyFullScreen()
    {
        if (!IsFullScreen)
        {
            _previousWindowState = WindowState;
            _previousWindowStyle = WindowStyle;
            _previousResizeMode = ResizeMode;
            _previousWindowBounds = WindowState == WindowState.Normal
                ? new Rect(Left, Top, Width, Height) : RestoreBounds;
            _previousWindowChrome = System.Windows.Shell.WindowChrome.GetWindowChrome(this);

            // Re-maximize even when invoked from a maximized window: its native
            // bounds must change from the work area to the complete monitor.
            WindowState = WindowState.Normal;
            SetValue(IsFullScreenPropertyKey, true);
            System.Windows.Shell.WindowChrome.SetWindowChrome(this, null);
            WindowStyle = WindowStyle.None;
            ResizeMode = ResizeMode.NoResize;
            WindowState = WindowState.Maximized;
        }
        else
        {
            WindowState = WindowState.Normal;
            SetValue(IsFullScreenPropertyKey, false);
            WindowStyle = _previousWindowStyle;
            ResizeMode = _previousResizeMode;
            System.Windows.Shell.WindowChrome.SetWindowChrome(this, _previousWindowChrome);
            if (!_previousWindowBounds.IsEmpty &&
                double.IsFinite(_previousWindowBounds.X) && double.IsFinite(_previousWindowBounds.Y))
            {
                Left = _previousWindowBounds.Left;
                Top = _previousWindowBounds.Top;
                Width = _previousWindowBounds.Width;
                Height = _previousWindowBounds.Height;
            }
            WindowState = _previousWindowState;
        }
        ActiveView.Focus();
        QueueViewportResize();
    }
}
