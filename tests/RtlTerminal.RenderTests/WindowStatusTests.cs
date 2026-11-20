using Microsoft.Win32;
using System.Collections;
using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using RtlTerminal;

internal static partial class Program
{
    private static void CheckWindowStatus()
    {
        if (Application.Current is null) _ = new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
        AppTheme.Apply(AppThemeKind.Dark);
        var registryPath = @"Software\RtlTerminal.Tests\Chrome-" + Guid.NewGuid().ToString("N");
        var history = new DirectoryHistoryStore(registryPath); history.SetEnabled(false);
        var main = new MainWindow(history);
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
        object? Call(string name, params object?[] args) => typeof(MainWindow).GetMethod(name, flags | BindingFlags.Static)!.Invoke(main, args);
        Call("ApplyAutoHideMenu", false, false);
        var root = (FrameworkElement)main.Content;
        void Layout(double width = 1400)
        {
            root.Measure(new Size(width, 700)); root.Arrange(new Rect(0,0,width,700)); root.UpdateLayout();
            main.Dispatcher.Invoke(() => { }, DispatcherPriority.ContextIdle);
        }
        try
        {
            var badge = (Border)main.FindName("AdministratorBadge");
            Call("SetElevationBadge", false); Layout();
            if (badge.Visibility != Visibility.Collapsed) throw new Exception("Normal process shows administrator badge.");
            Call("SetElevationBadge", true); Layout();
            var scroller = (ScrollViewer)main.FindName("TabScroller");
            if (badge.Visibility != Visibility.Visible || badge.TranslatePoint(new Point(0,0), root).X >= scroller.TranslatePoint(new Point(0,0), root).X)
                throw new Exception("Administrator badge is not before the tab strip.");
            var tabs = (IList)typeof(MainWindow).GetField("_tabs", flags)!.GetValue(main)!;
            var profile = typeof(MainWindow).GetNestedType("TerminalProfile", BindingFlags.NonPublic)!;
            var tabType = typeof(MainWindow).GetNestedType("TerminalTab", BindingFlags.NonPublic)!;
            var a = Call("CreatePane", Enum.ToObject(profile, 1))!;
            var tabA = Activator.CreateInstance(tabType, a)!; tabs.Add(tabA);
            var b = Call("CreatePane", Enum.ToObject(profile, 0))!;
            var tabB = Activator.CreateInstance(tabType, b)!; tabs.Add(tabB);
            Call("ObserveDirectory", a, @"C:\Projects\پروژه فارسی");
            Call("ObserveDirectory", b, @"D:\Work\Second");
            Call("ShowTab", tabA); Layout();
            var label = (TextBlock)main.FindName("ActiveDirectoryText");
            var button = (Button)main.FindName("ActiveDirectoryButton");
            if (label.Text != "پروژه فارسی" || !button.IsEnabled) throw new Exception("Folder pill does not show the active pane's last path component.");
            if (!(bool)Call("TryJoinTab", tabB, false)!) throw new Exception("Status test could not join panes.");
            Call("ActivatePane", b); Layout();
            if (label.Text != "Second" || !button.ToolTip.ToString()!.Contains(@"D:\Work\Second")) throw new Exception("Folder pill did not follow selected split.");
            Call("ObserveDirectory", a, @"C:\Other\Inactive"); Layout();
            if (label.Text != "Second") throw new Exception("Inactive pane's output overwrote selected directory.");
            Call("ActivatePane", a); Layout();
            if (label.Text != "Inactive") throw new Exception("Selecting split did not use its current directory.");
            Call("ObserveDirectory", a, @"C:\"); Layout();
            if (label.Text != @"C:\" || history.Snapshot().Count != 0) throw new Exception("Root label or disabled-history independence failed.");
            var labelMethod = typeof(MainWindow).GetMethod("DirectoryLabel", BindingFlags.NonPublic | BindingFlags.Static)!;
            if ((string)labelMethod.Invoke(null, [@"\\wsl.localhost\Ubuntu\home\user\Repo"])! != "Repo") throw new Exception("WSL folder label failed.");
            Call("ObserveDirectory", a, @"C:\Work\Folder with a long descriptive Persian فارسی name"); Layout();
            SaveChrome(root, "window-status.png", 1400, 700);
            Layout(640);
            if (button.ActualWidth > 180 || button.TranslatePoint(new Point(button.ActualWidth,0), root).X > 640)
                throw new Exception("Folder pill overflowed the compact toolbar.");
            SaveChrome(root, "window-status-small.png", 640, 700);
            AppTheme.Apply(AppThemeKind.Light); Layout();
            SaveChrome(root, "window-status-light.png", 1400, 700);
            AppTheme.Apply(AppThemeKind.Dark);
            Console.WriteLine("PASS administrator shield placement, active split folder, inactive updates, roots/WSL, disabled history and compact toolbar");
        }
        finally { main.Close(); Registry.CurrentUser.DeleteSubKeyTree(registryPath, false); }
    }
    private static void SaveChrome(FrameworkElement root, string filename, int width, int height)
    {
        var image = new RenderTargetBitmap(width,height,96,96,PixelFormats.Pbgra32); image.Render(root);
        var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(image));
        Directory.CreateDirectory("artifacts/render-checks");
        using var output = File.Create(Path.Combine("artifacts/render-checks", filename)); encoder.Save(output);
    }
}
