using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;

namespace RtlTerminal;

internal static class WindowCaptionTheme
{
    // Native captions retain Windows sizing, accessibility and caption-button behavior.
    public static void Attach(Window window)
    {
        Color? previous = null;
        void Update()
        {
            if (window.TryFindResource("TitleBarBrush") is not SolidColorBrush brush || previous == brush.Color) return;
            var handle = new WindowInteropHelper(window).Handle; if (handle == IntPtr.Zero) return;
            previous = brush.Color;
            _ = Apply(window);
        }
        EventHandler layout = (_, _) => Update();
        window.SourceInitialized += (_, _) => Update(); window.LayoutUpdated += layout;
        window.Loaded += (_, _) => { previous = null; Update(); };
        window.ContentRendered += (_, _) => { previous = null; Update(); };
        window.Closed += (_, _) => window.LayoutUpdated -= layout;
    }
    internal static int Apply(Window window)
    {
        var handle = new WindowInteropHelper(window).Handle;
        var dark = AppTheme.IsLight ? 0 : 1;
        _ = DwmSetWindowAttribute(handle, 20, ref dark, sizeof(int));
        var color = ((SolidColorBrush)window.FindResource("TitleBarBrush")).Color;
        var caption = color.R | color.G << 8 | color.B << 16;
        var result = DwmSetWindowAttribute(handle, 35, ref caption, sizeof(int));
        if (window.TryFindResource("ChromeForegroundBrush") is SolidColorBrush foreground)
        {
            var text = foreground.Color.R | foreground.Color.G << 8 | foreground.Color.B << 16;
            _ = DwmSetWindowAttribute(handle, 36, ref text, sizeof(int));
        }
        return result;
    }
    [DllImport("dwmapi.dll")] private static extern int DwmSetWindowAttribute(IntPtr handle, int attribute, ref int value, int size);
}
