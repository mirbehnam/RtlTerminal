using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using RtlTerminal;

internal static partial class Program
{
    private static void CheckThemes()
    {
        if (Application.Current is null)
            _ = new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
        AppTheme.Apply(AppThemeKind.Dark);
        var view = new TerminalView
        {
            Width = 800, Height = 420, FontFamily = new FontFamily("Consolas"),
            FontSize = 18, Background = new SolidColorBrush(Color.FromRgb(12, 12, 12))
        };
        var buffer = new TerminalBuffer(70, 16);
        var snapshot = buffer.Process("\x1b[?25l\x1b[31m█\x1b[38;2;197;15;31m█" +
            "\x1b[38;5;1m█\x1b[0m\r\n\x1b[41m \x1b[48;2;197;15;31m " +
            "\x1b[48;5;1m \x1b[0m\r\nسلام English");
        byte[] Pixels(RenderTargetBitmap bitmap)
        {
            var pixels = new byte[bitmap.PixelWidth * bitmap.PixelHeight * 4];
            bitmap.CopyPixels(pixels, bitmap.PixelWidth * 4, 0);
            return pixels;
        }
        void AssertPixel(byte[] pixels, int x, int y, TerminalColor expected)
        {
            var offset = (y * 800 + x) * 4;
            if (pixels[offset] != expected.Blue || pixels[offset + 1] != expected.Green || pixels[offset + 2] != expected.Red)
                throw new Exception($"Unexpected themed color at {x},{y}: expected {expected}");
        }
        try
        {
            var dark = Pixels(Render(view, snapshot, "theme-dark.png"));
            AppTheme.Apply(AppThemeKind.Light);
            view.Background = Brushes.White;
            view.RefreshTheme();
            var light = Pixels(Render(view, snapshot, "theme-light.png"));
            AssertPixel(light, 5, 12, new(207, 34, 46));
            AssertPixel(light, 16, 12, new(197, 15, 31));
            AssertPixel(light, 27, 12, new(207, 34, 46));
            AssertPixel(light, 5, 37, new(255, 235, 233));
            AssertPixel(light, 16, 37, new(197, 15, 31));
            AssertPixel(light, 27, 37, new(255, 235, 233));
            var replies = buffer.Process("\x1b]10;?\a\x1b]11;?\a\x1b]12;?\a").Responses;
            if (!replies.Contains("\x1b]10;rgb:2424/2929/2f2f\x1b\\") ||
                !replies.Contains("\x1b]11;rgb:ffff/ffff/ffff\x1b\\") ||
                !replies.Contains("\x1b]12;rgb:2424/2929/2f2f\x1b\\"))
                throw new Exception("OSC queries did not report the active light default colors");
            AppTheme.Apply(AppThemeKind.Dark);
            view.Background = new SolidColorBrush(Color.FromRgb(12, 12, 12));
            view.RefreshTheme();
            var restored = Pixels(Render(view, snapshot, "theme-dark-restored.png"));
            if (!dark.SequenceEqual(restored))
                throw new Exception("Switching back to dark retained light colors or stale glyphs");
            Console.WriteLine("PASS live theme switching, ANSI/indexed remapping, exact RGB preservation and OSC defaults");
        }
        finally { AppTheme.Apply(AppThemeKind.Dark); }
    }
}
