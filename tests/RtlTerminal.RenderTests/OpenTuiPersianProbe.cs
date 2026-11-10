using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Media;
using RtlTerminal;

internal static partial class Program
{
    private static void ProbeOpenTuiPersian()
    {
        if (Application.Current == null) _ = new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
        AppTheme.Apply(AppThemeKind.Dark);
        var root = Path.GetFullPath(Path.Combine("artifacts", "opentui-probe")); Directory.CreateDirectory(root);
        var tools = new[] {
            ("OpenCode", Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "npm", "node_modules", "@opencode", "cli", "bin", "opencode.exe"), " --standalone"),
            ("MiMo", Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".mimocode", "bin", "mimo.exe"), " --pure") };
        foreach (var (name, exe, args) in tools)
        {
            if (!File.Exists(exe)) { Console.WriteLine("SKIP " + name + " not installed"); continue; }
            var snapshots = Task.Run(async () =>
            {
                using var session = new ConPtySession(100, 30);
                var buffer = new TerminalBuffer(100, 30);
                session.Start(ShellPath.WindowsArgument(exe) + args, root);
                var read = Task.Run(() =>
                {
                    var bytes = new byte[8192]; var chars = new char[8192]; var decoder = Encoding.UTF8.GetDecoder();
                    try { int n; while ((n = session.Read(bytes)) > 0) { buffer.PreserveTuiGrid = session.RequiresFixedTuiGrid(); var snap = buffer.Process(new string(chars, 0, decoder.GetChars(bytes, 0, n, chars, 0))); foreach (var response in snap.Responses) session.Write(response); } }
                    catch (IOException) { } catch (ObjectDisposedException) { }
                });
                try
                {
                    await Task.Delay(6000);
                    await session.WriteInputAsync("hello English", false, default); await Task.Delay(600);
                    for (var retry = 0; retry < 5 && !buffer.CaptureSnapshot().Lines.Any(l => l.Text.Contains("hello English")); retry++)
                    {
                        await Task.Delay(500); session.Write("\x15");
                        await session.WriteInputAsync("hello English", false, default); await Task.Delay(500);
                    }
                    var english = buffer.CaptureSnapshot();
                    session.Write("\x15"); await Task.Delay(150);
                    await session.WriteInputAsync("سلام دنیا Persian test", false, default); await Task.Delay(600);
                    var persian = buffer.CaptureSnapshot();
                    if (!persian.PreserveTuiGrid) throw new Exception(name + " compatibility profile was not detected in its live process tree.");
                    Console.WriteLine(name + ": alternate=" + persian.Modes.AlternateScreen + ", English input visible=" + english.Lines.Any(l => l.Text.Contains("hello English")) + ", Persian input visible=" + persian.Lines.Any(l => l.Text.Contains("سلام")));
                    return (english, persian);
                }
                finally { session.Dispose(); await read.WaitAsync(TimeSpan.FromSeconds(5)); }
            }).GetAwaiter().GetResult();
            var view = new TerminalView { Width = 1000, Height = 640, FontFamily = new FontFamily("Consolas"), FontSize = 18, Background = Brushes.Black };
            Capture(snapshots.english, "english"); Capture(snapshots.persian, "persian");
            void Capture(TerminalSnapshot snapshot, string kind)
            {
                view.Present(snapshot, true, 10.8, 25, false);
                view.Width = 1100; view.Height = 780;
                view.Measure(new Size(1100, 780)); view.Arrange(new Rect(0, 0, 1100, 780)); view.UpdateLayout();
                view.Dispatcher.Invoke(() => { }, System.Windows.Threading.DispatcherPriority.ContextIdle);
                var bitmap = new System.Windows.Media.Imaging.RenderTargetBitmap(1100, 780, 96, 96, PixelFormats.Pbgra32); bitmap.Render(view);
                var encoder = new System.Windows.Media.Imaging.PngBitmapEncoder(); encoder.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(bitmap));
                Directory.CreateDirectory("artifacts/render-checks");
                using var file = File.Create("artifacts/render-checks/opentui-" + name + "-" + kind + ".png"); encoder.Save(file);
            }
        }
    }
}
