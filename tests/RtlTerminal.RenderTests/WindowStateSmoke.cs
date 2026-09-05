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
        }
        finally { window.Close(); }
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
    [DllImport("user32.dll")] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool GetWindowRect(IntPtr hwnd, out RectI rect);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetMonitorInfo(IntPtr monitor, ref MonitorInfo info);
}
