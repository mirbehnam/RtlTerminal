using System.Reflection;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Threading;
using RtlTerminal;

internal static class WindowStateSmoke
{
    public static void Run()
    {
        if (Application.Current is null) _ = new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
        AppTheme.Apply(AppThemeKind.Dark);
        var window = new MainWindow();
        // Prevent shell startup, update checks and context-menu installation prompts.
        var loaded = typeof(MainWindow).GetMethod("Window_Loaded", BindingFlags.Instance | BindingFlags.NonPublic)!;
        window.Loaded -= (RoutedEventHandler)loaded.CreateDelegate(typeof(RoutedEventHandler), window);
        try
        {
            window.Show(); Pump();
            var menu = (System.Windows.Controls.ContextMenu)typeof(MainWindow)
                .GetMethod("CreateTerminalContextMenu", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(window, null)!;
            menu.IsOpen = true; Pump();
            if (!menu.IsOpen) throw new Exception("Terminal context menu did not open");
            menu.IsOpen = false;
            var view = (TerminalView)window.FindName("TerminalTextBox");
            view.Focus(); Pump();
            var key = new System.Windows.Input.KeyEventArgs(System.Windows.Input.Keyboard.PrimaryDevice,
                PresentationSource.FromVisual(window), Environment.TickCount, System.Windows.Input.Key.Apps)
                { RoutedEvent = System.Windows.Input.Keyboard.PreviewKeyDownEvent };
            view.RaiseEvent(key); Pump();
            if (!key.Handled) throw new Exception("Context Menu key was not handled");
            if (!view.IsKeyboardFocusWithin) throw new Exception("Menu opened before the Context Menu key was released");
            var release = new System.Windows.Input.KeyEventArgs(System.Windows.Input.Keyboard.PrimaryDevice,
                PresentationSource.FromVisual(window), Environment.TickCount, System.Windows.Input.Key.Apps)
                { RoutedEvent = System.Windows.Input.Keyboard.PreviewKeyUpEvent };
            view.RaiseEvent(release); Pump();
            if (!release.Handled) throw new Exception("Context Menu key release was not handled");
            Pump(); // The popup must remain open after the complete down/up cycle.
            if (System.Windows.Input.Keyboard.FocusedElement is not System.Windows.Controls.ContextMenu keyboardMenu)
            {
                if (System.Windows.Input.Keyboard.FocusedElement is System.Windows.Controls.MenuItem item &&
                    System.Windows.Controls.ItemsControl.ItemsControlFromItemContainer(item) is System.Windows.Controls.ContextMenu parent)
                    parent.IsOpen = false;
                else throw new Exception("Context Menu key did not focus its menu");
            }
            else keyboardMenu.IsOpen = false;
            Console.WriteLine("PASS context-menu separator and persistent menu after Context Menu key down/up");
            window.WindowState = WindowState.Maximized; Pump();
            var handle = new WindowInteropHelper(window).Handle;
            var monitor = new MonitorInfo { Size = Marshal.SizeOf<MonitorInfo>() };
            if (!GetMonitorInfo(MonitorFromWindow(handle, 2), ref monitor) || !GetWindowRect(handle, out var rect))
                throw new Exception("Cannot read native window bounds");
            if (rect.Left < monitor.Work.Left || rect.Top < monitor.Work.Top ||
                rect.Right > monitor.Work.Right || rect.Bottom > monitor.Work.Bottom)
                throw new Exception("Maximized window overlaps non-work area/taskbar");
            if (((System.Windows.Controls.Border)window.Content).Padding.Bottom < 12)
                throw new Exception("Maximized bottom padding missing");
            window.WindowState = WindowState.Minimized; Pump();
            window.WindowState = WindowState.Maximized; Pump();
            window.WindowState = WindowState.Normal; Pump();
            if (((TerminalView)window.FindName("TerminalTextBox")).ViewportHeight <= 0)
                throw new Exception("Viewport did not recover after minimize/restore");
            Console.WriteLine("PASS native maximize work-area bounds, bottom padding and minimize/restore cycle");
            var monitors = new List<RectI>();
            EnumDisplayMonitors(IntPtr.Zero, IntPtr.Zero,
                (IntPtr monitorHandle, IntPtr dc, ref RectI monitorBounds, IntPtr data) =>
                { monitors.Add(monitorBounds); return true; }, IntPtr.Zero);
            if (monitors.Count == 0) throw new Exception("No connected monitor found");
            foreach (var monitorBounds in monitors)
            {
                window.WindowState = WindowState.Normal;
                if (!SetWindowPos(handle, IntPtr.Zero, monitorBounds.Left + 30, monitorBounds.Top + 30,
                    Math.Min(850, monitorBounds.Right - monitorBounds.Left - 60),
                    Math.Min(560, monitorBounds.Bottom - monitorBounds.Top - 60), 0x0014))
                    throw new Exception("Unable to move test window to the target monitor");
                Pump();
                CheckFullScreen(window);
            }
            Console.WriteLine($"PASS fullscreen on all {monitors.Count} connected monitor(s)");
        }
        finally { window.Close(); }
    }
    private static void CheckFullScreen(MainWindow window)
    {
        var handle = new WindowInteropHelper(window).Handle;
        var originalChrome = System.Windows.Shell.WindowChrome.GetWindowChrome(window);
        // Exercise real F11 routing, including entry from an already maximized window.
        void F11()
        {
            var key = new System.Windows.Input.KeyEventArgs(System.Windows.Input.Keyboard.PrimaryDevice,
                PresentationSource.FromVisual(window), Environment.TickCount, System.Windows.Input.Key.F11)
                { RoutedEvent = System.Windows.Input.Keyboard.PreviewKeyDownEvent };
            window.RaiseEvent(key); Pump();
            if (!key.Handled) throw new Exception("F11 did not toggle fullscreen");
        }
        window.Left += 24; window.Top += 18; window.Width = 850; window.Height = 560; Pump();
        GetWindowRect(handle, out var originalRect);
        foreach (var state in new[] { WindowState.Normal, WindowState.Maximized })
        {
            window.WindowState = state; Pump();
            GetWindowRect(handle, out var before);
            for (var repeat = 0; repeat < 2; repeat++)
            {
                F11();
                var monitor = new MonitorInfo { Size = Marshal.SizeOf<MonitorInfo>() };
                if (!GetMonitorInfo(MonitorFromWindow(handle, 2), ref monitor) || !GetWindowRect(handle, out var full))
                    throw new Exception("Cannot read fullscreen bounds");
                if (!window.IsFullScreen || !full.Equals(monitor.Monitor))
                    throw new Exception($"Fullscreen does not cover the complete monitor: {full.Left},{full.Top},{full.Right},{full.Bottom}");
                var frame = (System.Windows.Controls.Border)window.FindName("WindowFrame");
                if (frame.Padding != new Thickness(0) || window.Topmost ||
                    ((FrameworkElement)window.FindName("TitleBarArea")).Visibility != Visibility.Collapsed ||
                    ((FrameworkElement)window.FindName("MenuBarArea")).Visibility != Visibility.Collapsed)
                    throw new Exception("Fullscreen retains outer spacing, window controls or always-on-top state");
                var view = (TerminalView)window.FindName("TerminalTextBox");
                if (Math.Abs(view.ActualHeight - window.ActualHeight) > 1)
                    throw new Exception("Terminal did not reclaim the hidden header space");
                // Another app can take focus; returning does not leave fullscreen.
                var other = new Window { Width = 120, Height = 80, ShowInTaskbar = false };
                try { other.Show(); other.Activate(); Pump(); window.Activate(); Pump(); }
                finally { other.Close(); }
                GetWindowRect(handle, out var afterActivation);
                if (!window.IsFullScreen || !afterActivation.Equals(monitor.Monitor))
                    throw new Exception("Fullscreen bounds changed after losing and regaining activation");
                F11();
                GetWindowRect(handle, out var after);
                if (window.IsFullScreen || window.WindowState != state || !before.Equals(after) ||
                    !ReferenceEquals(originalChrome, System.Windows.Shell.WindowChrome.GetWindowChrome(window)))
                    throw new Exception("Fullscreen did not restore the previous native placement or chrome");
                if (state == WindowState.Maximized && frame.Padding.Bottom < 12)
                    throw new Exception("Normal maximize padding was not restored");
            }
            window.WindowState = WindowState.Normal; Pump();
            GetWindowRect(handle, out var restored);
            if (!originalRect.Equals(restored)) throw new Exception("Fullscreen changed the normal restore bounds");
        }
        Console.WriteLine("PASS real F11 monitor coverage, hidden headers, zero outer padding, activation, repeated toggles and exact normal/maximized restore bounds");
    }
    private static void Pump()
    {
        var frame = new DispatcherFrame();
        var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(180) };
        timer.Tick += (_, _) => { timer.Stop(); frame.Continue = false; };
        timer.Start(); Dispatcher.PushFrame(frame);
    }
    [StructLayout(LayoutKind.Sequential)] private struct RectI { public int Left, Top, Right, Bottom; }
    [StructLayout(LayoutKind.Sequential)] private struct MonitorInfo { public int Size; public RectI Monitor, Work; public int Flags; }
    [DllImport("user32.dll")] private static extern IntPtr MonitorFromWindow(IntPtr hwnd, uint flags);
    private delegate bool MonitorEnumProc(IntPtr monitor, IntPtr dc, ref RectI rect, IntPtr data);
    [DllImport("user32.dll")] [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool EnumDisplayMonitors(IntPtr dc, IntPtr clip, MonitorEnumProc callback, IntPtr data);
    [DllImport("user32.dll")] [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetWindowPos(IntPtr hwnd, IntPtr after, int x, int y, int width, int height, uint flags);
    [DllImport("user32.dll")] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool GetWindowRect(IntPtr hwnd, out RectI rect);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetMonitorInfo(IntPtr monitor, ref MonitorInfo info);
}
