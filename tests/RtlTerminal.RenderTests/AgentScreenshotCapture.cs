#if CLI_TEST_LAB
using System.Collections;
using System.IO;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using RtlTerminal;

internal static partial class Program
{
    private const string ScreenshotResponse = """
        بررسی انجام شد و پروژه آماده است.

        **Build passed**

        فایل `src/app.cs` بدون خطا بررسی شد. پاسخ سرویس `HTTP 200` است.

        **What changed**
        - Improved the startup check and simplified the error message.
        - Verified Persian and English output in the same terminal session.

        برای بررسی نهایی، دستور `dotnet test` را اجرا کنید.

        العربية: تم التحقق من حالة الإصدار بنجاح.
        """;

    private static void CaptureAgentScreenshots()
    {
        var captures = new Dictionary<string, TerminalSnapshot>();
        ProbeCliLab(screenshots: captures).GetAwaiter().GetResult();
        var codex = ProbeCodexShiftEnter(captures).GetAwaiter().GetResult();
        if (codex != "PASS" || captures.Count != 3)
            throw new InvalidOperationException("Expected three live agent captures. Codex: " + codex);

        if (Application.Current is null) _ = new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
        UiText.Select(UiLanguage.English, persist: false);
        AppTheme.Apply(AppThemeKind.Dark, ColorScheme.Find("Campbell"));
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
        var window = new MainWindow { Width = 1100, Height = 690 };
        var type = typeof(MainWindow);
        object? Call(string name, params object?[] args) => type.GetMethod(name, flags)!.Invoke(window, args);
        var loaded = type.GetMethod("Window_Loaded", flags)!;
        var closing = type.GetMethod("Window_Closing", flags)!;
        window.Loaded -= (RoutedEventHandler)loaded.CreateDelegate(typeof(RoutedEventHandler), window);
        window.Closing -= (System.ComponentModel.CancelEventHandler)closing.CreateDelegate(typeof(System.ComponentModel.CancelEventHandler), window);
        Call("ApplyAutoHideMenu", false, false);
        Call("ApplyFontSettings", new TerminalFontSettings("Cascadia Mono", 15, false, false));
        var tabs = (IList)type.GetField("_tabs", flags)!.GetValue(window)!;
        var tabType = type.GetNestedType("TerminalTab", BindingFlags.NonPublic)!;
        var profileType = type.GetNestedType("TerminalProfile", BindingFlags.NonPublic)!;
        var names = new[] { "Claude Code", "Codex", "OpenCode" };
        foreach (var name in names)
        {
            var pane = Call("CreatePane", Enum.Parse(profileType, "CommandPrompt"));
            var tab = Activator.CreateInstance(tabType, pane)!;
            tabType.GetProperty("CustomTitle")!.SetValue(tab, name);
            tabs.Add(tab);
        }
        try
        {
            Directory.CreateDirectory("artifacts/agent-screenshots");
            for (var i = 0; i < names.Length; i++)
            {
                Call("ApplyAutoHideMenu", i == 0, false);
                Call("ShowTab", tabs[i]);
                var root = (FrameworkElement)window.Content;
                var activePane = tabType.GetProperty("ActivePane")!.GetValue(tabs[i])!;
                var view = (TerminalView)activePane.GetType().GetProperty("View")!.GetValue(activePane)!;
                root.Measure(new Size(window.Width, window.Height));
                root.Arrange(new Rect(0, 0, window.Width, window.Height));
                root.UpdateLayout();
                var cellWidth = Math.Floor(view.ViewportWidth / 110 * 10) / 10;
                var lineHeight = Math.Floor(view.ViewportHeight / 35 * 10) / 10;
                var snapshot = RemoveLocalPaths(captures[names[i]]);
                view.Present(snapshot with { CursorVisible = false }, true, cellWidth, lineHeight, true);
                ((TextBlock)window.FindName("ActiveDirectoryText")).Text = "sample-project";
                ((Button)window.FindName("ActiveDirectoryButton")).IsEnabled = true;
                window.Dispatcher.Invoke(() => { }, DispatcherPriority.ContextIdle);
                root.UpdateLayout();
                const double scale = 1.5;
                var bitmap = new RenderTargetBitmap((int)(window.Width * scale), (int)(window.Height * scale), 96 * scale, 96 * scale, PixelFormats.Pbgra32);
                bitmap.Render(root);
                var png = new PngBitmapEncoder();
                BitmapSource image = names[i] == "Codex"
                    ? new CroppedBitmap(bitmap, new Int32Rect(0, 0, bitmap.PixelWidth, (int)(bitmap.PixelHeight * .56)))
                    : bitmap;
                png.Frames.Add(BitmapFrame.Create(image));
                using var output = File.Create("artifacts/agent-screenshots/" + names[i].ToLowerInvariant().Replace(' ', '-') + ".png");
                png.Save(output);
            }
        }
        finally { window.Close(); }
    }

    private static TerminalSnapshot RemoveLocalPaths(TerminalSnapshot snapshot)
    {
        var lines = snapshot.Lines.Select(line =>
        {
            var runs = line.Runs.Select(run =>
            {
                var text = Regex.Replace(run.Text, @"(?:[A-Z]:\\(?:Users\\|…\\)|~\\AppData\\Local\\Temp\\)[^│\r\n]*", match =>
                {
                    var firstSpace = match.Value.IndexOf(' ');
                    var pathLength = firstSpace >= 0 ? firstSpace : match.Value.Length;
                    return @"C:\sample-project".PadRight(pathLength) + match.Value[pathLength..];
                }, RegexOptions.IgnoreCase);
                return run with { Text = text };
            }).ToArray();
            return new TerminalLine(runs, line.CellLength) { WrappedFromPrevious = line.WrappedFromPrevious };
        }).ToArray();
        return snapshot with { Lines = lines };
    }
}
#endif
