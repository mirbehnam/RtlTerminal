using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media.Animation;
using System.Windows.Shell;
using System.Windows.Threading;

namespace RtlTerminal;

public partial class MainWindow
{
    private const double CompactCaptionHeight = 12;
    private const int MenuDismissDelayMs = 900;
    private readonly DispatcherTimer _autoHideMenuTimer = new(DispatcherPriority.Input)
    {
        Interval = TimeSpan.FromMilliseconds(100)
    };
    private bool _autoHideMenu;
    private bool _hideTabsWithMenu;
    private double _normalCaptionHeight;
    private bool _menuAltPressed;
    private long? _menuPointerSince;
    private long _menuLastInteraction;

    private void InitializeAutoHideMenu()
    {
        _normalCaptionHeight = WindowChrome.GetWindowChrome(this)?.CaptionHeight ?? 40;
        ApplyAutoHideMenu(AppSettings.LoadAutoHideMenu(), AppSettings.LoadHideTabsWithMenu());
        _autoHideMenuTimer.Tick += AutoHideMenuTimer_Tick;
        PreviewMouseMove += (_, _) =>
        {
            if (_autoHideMenu && !IsFullScreen && IsActive && !_autoHideMenuTimer.IsEnabled && IsPointerAtMenuEdge())
            {
                _menuPointerSince = Environment.TickCount64;
                _autoHideMenuTimer.Start();
            }
        };
        PreviewKeyDown += AutoHideMenu_PreviewKeyDown;
        PreviewKeyUp += AutoHideMenu_PreviewKeyUp;
        Deactivated += (_, _) =>
        {
            if (!_closeConfirmationOpen) DismissAutoHideMenu(restoreFocus: false);
        };
        Closed += (_, _) => _autoHideMenuTimer.Stop();
    }

    private void ApplyAutoHideMenu(bool enabled, bool hideTabs)
    {
        if (_autoHideMenu == enabled && _hideTabsWithMenu == hideTabs) return;
        DismissAutoHideMenu();
        MoveChrome(MenuBarArea, enabled ? MenuBarOverlay : MenuBarDock);
        MoveChrome(TitleBarArea, enabled && hideTabs ? MenuBarOverlay : TitleBarDock, index: 0);
        _autoHideMenu = enabled;
        _hideTabsWithMenu = hideTabs;
        UpdateMenuRevealButton();
        if (_tabs.Count != 0) RebuildTabStrip();
    }

    private static void MoveChrome(FrameworkElement element, Panel destination, int index = 0)
    {
        if (ReferenceEquals(element.Parent, destination)) return;
        ((Panel)element.Parent).Children.Remove(element);
        destination.Children.Insert(index, element);
    }

    private void UpdateMenuRevealButton()
    {
        var hideTitleBar = _autoHideMenu && _hideTabsWithMenu && MenuBarOverlay.Visibility != Visibility.Visible;
        RevealMenuButton.Visibility = _autoHideMenu && !IsFullScreen && MenuBarOverlay.Visibility != Visibility.Visible
            ? Visibility.Visible : Visibility.Collapsed;
        CompactCaptionButtons.Visibility = hideTitleBar && !IsFullScreen ? Visibility.Visible : Visibility.Collapsed;
        // A slim native caption retains Windows dragging and snapping while the full header is hidden.
        var chrome = WindowChrome.GetWindowChrome(this);
        var captionHeight = hideTitleBar ? CompactCaptionHeight : _normalCaptionHeight;
        if (chrome is null || chrome.CaptionHeight == captionHeight) return;
        if (chrome.IsFrozen)
        {
            chrome = (WindowChrome)chrome.Clone();
            chrome.CaptionHeight = captionHeight;
            WindowChrome.SetWindowChrome(this, chrome);
        }
        else
            chrome.CaptionHeight = captionHeight;
    }

    private void RevealMenuButton_Click(object sender, RoutedEventArgs e)
    {
        var focusMenu = !RevealMenuButton.IsMouseOver;
        if (!focusMenu) ActiveView.Focus();
        RevealAutoHideMenu(focusMenu);
    }

    private bool IsPointerAtMenuEdge()
    {
        if (!TerminalArea.IsMouseOver || CompactWindowControls.IsMouseOver || Mouse.LeftButton != MouseButtonState.Released ||
            Mouse.RightButton != MouseButtonState.Released || Mouse.MiddleButton != MouseButtonState.Released)
            return false;
        var point = Mouse.GetPosition(TerminalArea);
        return point.X >= 0 && point.X < TerminalArea.ActualWidth && point.Y >= 0 &&
            point.Y < (_hideTabsWithMenu ? CompactCaptionHeight + 14 : 6);
    }

    private bool IsTopLevelMenuOpen() => MainMenu.Items.OfType<MenuItem>().Any(item => item.IsSubmenuOpen);

    private void AutoHideMenuTimer_Tick(object? sender, EventArgs e)
    {
        if (_closeConfirmationOpen) return;
        if (!_autoHideMenu || IsFullScreen || !IsActive)
        {
            DismissAutoHideMenu(restoreFocus: false);
            return;
        }

        var now = Environment.TickCount64;
        if (MenuBarOverlay.Visibility != Visibility.Visible)
        {
            if (!IsPointerAtMenuEdge())
                DismissAutoHideMenu();
            else if (_menuPointerSince is { } since && now - since >= 160)
                RevealAutoHideMenu(focusMenu: false);
            return;
        }

        if (MenuBarArea.IsMouseOver || MenuBarOverlay.IsKeyboardFocusWithin || IsTopLevelMenuOpen() ||
            Mouse.Captured is UIElement captured && MenuBarOverlay.IsAncestorOf(captured) ||
            (_hideTabsWithMenu && IsUsingRevealedTitleBar()))
            _menuLastInteraction = now;
        else if (now - _menuLastInteraction >= MenuDismissDelayMs)
            DismissAutoHideMenu();
    }

    private bool IsUsingRevealedTitleBar()
    {
        var pointer = Mouse.GetPosition(TitleBarArea);
        return new Rect(TitleBarArea.RenderSize).Contains(pointer) || _draggingTab is not null ||
            Mouse.Captured is ContextMenu;
    }

    private void RevealAutoHideMenu(bool focusMenu)
    {
        if (!_autoHideMenu || IsFullScreen) return;
        if (MenuBarOverlay.Visibility != Visibility.Visible)
        {
            MenuBarOverlay.Visibility = Visibility.Visible;
            UpdateMenuRevealButton();
            if (SystemParameters.ClientAreaAnimation && !SystemParameters.HighContrast)
                MenuBarOverlay.BeginAnimation(OpacityProperty,
                    new DoubleAnimation(0.78, 1, TimeSpan.FromMilliseconds(130)) { FillBehavior = FillBehavior.Stop });
        }
        _menuLastInteraction = Environment.TickCount64;
        _autoHideMenuTimer.Start();
        if (focusMenu && MainMenu.Items.OfType<MenuItem>().FirstOrDefault(item => item.IsEnabled) is { } first)
        {
            MenuBarOverlay.UpdateLayout();
            first.Focus();
        }
    }

    private void DismissAutoHideMenu(bool restoreFocus = true)
    {
        _menuAltPressed = false;
        _menuPointerSince = null;
        _autoHideMenuTimer.Stop();
        if (MenuBarOverlay.Visibility != Visibility.Visible) return;
        var hadFocus = MenuBarOverlay.IsKeyboardFocusWithin || IsTopLevelMenuOpen();
        foreach (var item in MainMenu.Items.OfType<MenuItem>())
            item.IsSubmenuOpen = false;
        MenuBarOverlay.BeginAnimation(OpacityProperty, null);
        MenuBarOverlay.Visibility = Visibility.Collapsed;
        UpdateMenuRevealButton();
        if (restoreFocus && hadFocus) ActiveView.Focus();
    }

    private void AutoHideMenu_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (!_autoHideMenu || IsFullScreen) return;
        var key = GetEffectiveKey(e);
        if (key == Key.LeftAlt)
        {
            if (!e.IsRepeat) _menuAltPressed = Keyboard.Modifiers == ModifierKeys.Alt;
            return;
        }

        _menuAltPressed = false;
        if (key == Key.Escape && MenuBarOverlay.Visibility == Visibility.Visible &&
            MenuBarArea.IsKeyboardFocusWithin && !IsTopLevelMenuOpen())
        {
            DismissAutoHideMenu();
            e.Handled = true;
        }
    }

    private void AutoHideMenu_PreviewKeyUp(object sender, KeyEventArgs e)
    {
        if (!_menuAltPressed || GetEffectiveKey(e) != Key.LeftAlt) return;
        _menuAltPressed = false;
        if (!_autoHideMenu || IsFullScreen) return;
        if (MenuBarOverlay.Visibility == Visibility.Visible && MenuBarArea.IsKeyboardFocusWithin)
            DismissAutoHideMenu();
        else
            RevealAutoHideMenu(focusMenu: true);
        e.Handled = true;
    }
}
