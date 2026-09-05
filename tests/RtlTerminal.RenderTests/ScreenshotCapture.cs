using System.Collections;
using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using RtlTerminal;

internal static class ScreenshotCapture
{
    // Capture the real WPF window with deterministic demo output, without starting
    // shells, exposing user paths, modifying settings, or contacting services.
    public static void Run()
    {
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
        var window = new MainWindow { Width = 1080, Height = 680 };
        var loaded = typeof(MainWindow).GetMethod("Window_Loaded", flags)!;
        window.Loaded -= (RoutedEventHandler)loaded.CreateDelegate(typeof(RoutedEventHandler), window);
        var tabs = (IList)typeof(MainWindow).GetField("_tabs", flags)!.GetValue(window)!;
        var tabType = typeof(MainWindow).GetNestedType("TerminalTab", BindingFlags.NonPublic)!;
        var profileType = typeof(MainWindow).GetNestedType("TerminalProfile", BindingFlags.NonPublic)!;
        tabs.Add(Activator.CreateInstance(tabType, 1, Enum.ToObject(profileType, 1), "PowerShell"));
        tabs.Add(Activator.CreateInstance(tabType, 2, Enum.ToObject(profileType, 0), "Command Prompt"));
        typeof(MainWindow).GetField("_activeTab", flags)!.SetValue(window, tabs[0]);
        typeof(MainWindow).GetMethod("RebuildTabStrip", flags)!.Invoke(window, null);
        try
        {
            window.Show(); window.Activate(); window.UpdateLayout();
            var view = (TerminalView)window.FindName("TerminalTextBox");
            view.FontFamily = new FontFamily("Consolas"); view.FontSize = 14;
            typeof(MainWindow).GetMethod("UpdateFontMetrics", flags)!.Invoke(window, null);
            var cell = (double)typeof(MainWindow).GetField("_cellWidth", flags)!.GetValue(window)!;
            var line = (double)typeof(MainWindow).GetField("_lineHeight", flags)!.GetValue(window)!;
            var columns = (int)(view.ViewportWidth / cell);
            var rows = (int)(view.ViewportHeight / line);
            Capture("rtl-terminal-main-window.png",
                "\x1b[?25l\r\n\x1b[38;2;114;214;197m" +
                "  RTL TERMINAL  /  v1.0.5\x1b[0m\r\n" +
                "  Windows terminal · ConPTY · Smart RTL\r\n\r\n" +
                "به ترمینال فارسی خوش آمدید\r\n" +
                "نمایش متن فارسی و English در کنار یکدیگر\r\n" +
                "مرحباً بكم في الطرفية — دعم العربية والفارسية\r\n\r\n" +
                "  \x1b[32m✓\x1b[0m  Independent terminal tabs\r\n" +
                "  \x1b[32m✓\x1b[0m  ANSI colors and cell-based graphics\r\n" +
                "  \x1b[32m✓\x1b[0m  Logical Unicode selection and copying\r\n\r\n" +
                "  \x1b[31m████\x1b[33m████\x1b[32m████\x1b[36m████\x1b[34m████\x1b[35m████\x1b[0m\r\n\r\n" +
                "  https://github.com/mirbehnam/RtlTerminal\r\n" +
                "  Ctrl + Left Click to open link\r\n\r\n" +
                "  Demo output · Font: Consolas 14 · Smart RTL: on");
            Capture("rtl-terminal-persian-rtl-cli.png",
                "\x1b[?25l\r\n\x1b[38;2;114;214;197m  SMART RTL  /  Mixed-language output\x1b[0m\r\n\r\n" +
                "این یک نمونهٔ نمایشی از خروجی فارسی ترمینال است\r\n\r\n" +
                "وضعیت پروژه: آماده برای اجرا\r\n" +
                "فایل README.md با موفقیت ذخیره شد\r\n" +
                "نتیجه build: موفق — نسخه 1.0.5\r\n\r\n" +
                "العربية والفارسية مع English والأرقام 123\r\n\r\n" +
                "  \x1b[32mPASS\x1b[0m  Persian and Arabic text shaping\r\n" +
                "  \x1b[32mPASS\x1b[0m  ANSI colors and mixed-direction text\r\n\r\n" +
                "  ┌──────────────────────────────────────────────┐\r\n" +
                "  │  Copy selection     Right-click              │\r\n" +
                "  │  Paste              Right-click (no selection)│\r\n" +
                "  │  Context menu       Apps key / Shift+F10     │\r\n" +
                "  └──────────────────────────────────────────────┘\r\n\r\n" +
                "  Demo output · https://github.com/mirbehnam/RtlTerminal");

            void Capture(string name, string output)
            {
                view.Clear();
                view.Present(new TerminalBuffer(columns, rows).Process(output), true, cell, line, false);
                window.UpdateLayout();
                window.Dispatcher.Invoke(() => { }, DispatcherPriority.ContextIdle);
                var bitmap = new RenderTargetBitmap(1080, 680, 96, 96, PixelFormats.Pbgra32);
                bitmap.Render(window);
                Directory.CreateDirectory("screenshots");
                var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap));
                using var stream = File.Create(Path.Combine("screenshots", name)); encoder.Save(stream);
                Console.WriteLine($"Captured screenshots/{name}");
            }
        }
        finally { window.Close(); }
    }
}
