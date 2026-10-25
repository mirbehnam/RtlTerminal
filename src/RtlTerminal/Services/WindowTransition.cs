using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;

namespace RtlTerminal;

/// <summary>Keep intermediate Win32 frames out of a fullscreen transition.</summary>
internal sealed class WindowTransition : IDisposable
{
    private readonly Window _window;
    private readonly IntPtr _handle;
    private readonly int _previousTransitionsDisabled;
    private readonly bool _restoreTransitions;

    public WindowTransition(Window window)
    {
        _window = window;
        _handle = new WindowInteropHelper(window).Handle;
        // Do not create or show an HWND when called by an unshown window/test.
        if (_handle == IntPtr.Zero || !IsWindowVisible(_handle)) { _handle = IntPtr.Zero; return; }
        if (HwndSource.FromHwnd(_handle)?.CompositionTarget is { } target && window.Background is SolidColorBrush background)
            target.BackgroundColor = background.Color;
        DwmGetWindowAttribute(_handle, 3, out _previousTransitionsDisabled, sizeof(int));
        var disable = 1;
        _restoreTransitions = DwmSetWindowAttribute(_handle, 3, ref disable, sizeof(int)) >= 0; // DWMWA_TRANSITIONS_FORCEDISABLED
        SendMessage(_handle, 0x000B, IntPtr.Zero, IntPtr.Zero); // WM_SETREDRAW
    }

    public void Dispose()
    {
        if (_handle == IntPtr.Zero) return;
        try
        {
            // Finish WPF bindings/layout while the old native frame is frozen.
            _window.Dispatcher.Invoke(() => _window.UpdateLayout(), DispatcherPriority.Render);
        }
        finally
        {
            SendMessage(_handle, 0x000B, new IntPtr(1), IntPtr.Zero);
            // RDW_INVALIDATE | RDW_ALLCHILDREN | RDW_UPDATENOW | RDW_FRAME.
            RedrawWindow(_handle, IntPtr.Zero, IntPtr.Zero, 0x0581);
            _window.InvalidateVisual();
            _window.Dispatcher.Invoke(() => { }, DispatcherPriority.Render);
            DwmFlush();
            if (_restoreTransitions)
            {
                var previous = _previousTransitionsDisabled;
                DwmSetWindowAttribute(_handle, 3, ref previous, sizeof(int));
            }
        }
    }

    [DllImport("user32.dll")] private static extern IntPtr SendMessage(IntPtr hwnd, uint message, IntPtr wParam, IntPtr lParam);
    [DllImport("user32.dll")] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool IsWindowVisible(IntPtr hwnd);
    [DllImport("user32.dll")] [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool RedrawWindow(IntPtr hwnd, IntPtr rect, IntPtr region, uint flags);
    [DllImport("dwmapi.dll")] private static extern int DwmGetWindowAttribute(IntPtr hwnd, int attribute, out int value, int size);
    [DllImport("dwmapi.dll")] private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref int value, int size);
    [DllImport("dwmapi.dll")] private static extern int DwmFlush();
}
