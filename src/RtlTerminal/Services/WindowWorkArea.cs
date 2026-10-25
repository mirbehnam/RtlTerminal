using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace RtlTerminal;

internal static class WindowWorkArea
{
    public static void Attach(Window window, Func<bool>? isFullScreen = null)
    {
        var source = HwndSource.FromHwnd(new WindowInteropHelper(window).Handle);
        HwndSourceHook hook = (IntPtr hwnd, int message, IntPtr wParam, IntPtr lParam, ref bool handled) =>
            HandleMessage(hwnd, message, wParam, lParam, ref handled, isFullScreen?.Invoke() == true);
        source?.AddHook(hook);
        window.Closed += (_, _) => source?.RemoveHook(hook);
    }

    public static double GetAvailableWidth(Window window)
    {
        var handle = new WindowInteropHelper(window).Handle;
        var monitor = handle == IntPtr.Zero ? IntPtr.Zero : MonitorFromWindow(handle, 2);
        var info = new MonitorInfo { Size = Marshal.SizeOf<MonitorInfo>() };
        if (monitor == IntPtr.Zero || !GetMonitorInfo(monitor, ref info)) return SystemParameters.WorkArea.Width;
        var area = window is MainWindow { IsFullScreen: true } ? info.Monitor : info.Work;
        return (area.Right - area.Left) / System.Windows.Media.VisualTreeHelper.GetDpi(window).DpiScaleX;
    }

    private static IntPtr HandleMessage(IntPtr hwnd, int message, IntPtr wParam, IntPtr lParam, ref bool handled, bool fullScreen)
    {
        if (message != 0x0024) return IntPtr.Zero; // WM_GETMINMAXINFO
        var monitor = MonitorFromWindow(hwnd, 2); // nearest monitor, including secondary displays
        var info = new MonitorInfo { Size = Marshal.SizeOf<MonitorInfo>() };
        if (monitor == IntPtr.Zero || !GetMonitorInfo(monitor, ref info)) return IntPtr.Zero;
        var bounds = Marshal.PtrToStructure<MinMaxInfo>(lParam);
        var area = fullScreen ? info.Monitor : info.Work;
        bounds.MaxPosition = new PointI { X = area.Left - info.Monitor.Left, Y = area.Top - info.Monitor.Top };
        bounds.MaxSize = new PointI { X = area.Right - area.Left, Y = area.Bottom - area.Top };
        Marshal.StructureToPtr(bounds, lParam, false);
        handled = true;
        return IntPtr.Zero;
    }

    [StructLayout(LayoutKind.Sequential)] private struct PointI { public int X, Y; }
    [StructLayout(LayoutKind.Sequential)] private struct RectI { public int Left, Top, Right, Bottom; }
    [StructLayout(LayoutKind.Sequential)] private struct MonitorInfo { public int Size; public RectI Monitor, Work; public int Flags; }
    [StructLayout(LayoutKind.Sequential)] private struct MinMaxInfo
    { public PointI Reserved, MaxSize, MaxPosition, MinTrackSize, MaxTrackSize; }
    [DllImport("user32.dll")] private static extern IntPtr MonitorFromWindow(IntPtr hwnd, uint flags);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetMonitorInfo(IntPtr monitor, ref MonitorInfo info);
}
