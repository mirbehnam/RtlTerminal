using System.Text;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;
using RtlTerminal;

internal static partial class Program
{
    private static void CheckTerminalBell()
    {
        static void Check(bool ok, string message) { if (!ok) throw new Exception("Bell: " + message); }
        var buffer = new TerminalBuffer(80, 24);
        buffer.Process("hello\a\a");
        Check(buffer.ConsumePendingBells() == 2 && buffer.ConsumePendingBells() == 0, "consume exactly once");
        buffer.CaptureSnapshot(); buffer.Resize(90, 25);
        Check(buffer.ConsumePendingBells() == 0, "render and resize do not replay");
        foreach (var text in new[] { "\x1b]9;9;C:\\work\a", "\x1b]133;B\a", "\x1b]8;;https://example.com\aLINK\x1b]8;;\a", "\x1b]0;title\x1b\\" })
            foreach (var c in text) buffer.Process(c.ToString());
        Check(buffer.ConsumePendingBells() == 0, "fragmented OSC terminators stay silent");
        buffer.Process("\a");
        Check(buffer.ConsumePendingBells() == 1, "normal bell after OSC");
        var otherPane = new TerminalBuffer(80, 24); otherPane.Process("\a");
        Check(otherPane.ConsumePendingBells() == 1 && buffer.ConsumePendingBells() == 0, "independent panes");

        long now = 0; var sounds = new List<TerminalBellSound>(); var queue = new Queue<Action>();
        var service = new TerminalBellService(new(), sounds.Add, () => now, queue.Enqueue);
        Check(service.Notify(true) && sounds.Count == 0, "playback scheduled off caller");
        Check(!service.Notify(false), "coalesce outstanding playback");
        queue.Dequeue()();
        now = 499; Check(!service.Notify(false), "rate limit shared by panes");
        now = 500; Check(service.Notify(false), "500 ms boundary"); queue.Dequeue()();
        service.UpdateSettings(new(false)); now = 1000;
        Check(!service.Notify(false), "disabled");
        Check(service.Notify(true, true, TerminalBellSound.Warning), "preview bypasses disabled"); queue.Dequeue()();
        Check(sounds.Last() == TerminalBellSound.Warning, "preview sound selection");
        service.UpdateSettings(new(true, true, TerminalBellSound.Error)); now = 1500;
        Check(!service.Notify(true) && service.Notify(false), "background only"); queue.Dequeue()();
        Check(sounds.Last() == TerminalBellSound.Error, "updated sound applied immediately");
        var failing = new TerminalBellService(new(), _ => throw new InvalidOperationException(), () => now, a => a());
        Check(failing.Notify(true), "audio failure contained"); now += 500;
        Check(failing.Notify(true), "audio failure releases pending flag");
        var concurrentSounds = 0;
        var concurrent = new TerminalBellService(new(), _ => Interlocked.Increment(ref concurrentSounds), () => 0, a => a());
        Parallel.For(0, 40, _ => concurrent.Notify(false));
        Check(concurrentSounds == 1, "concurrent panes share limiter");

        var path = @"Software\RtlTerminal.Tests\Bell-" + Guid.NewGuid().ToString("N");
        try
        {
            using var key = Registry.CurrentUser.CreateSubKey(path);
            Check(TerminalBellSettings.Read(key) == new TerminalBellSettings(), "default settings");
            var saved = new TerminalBellSettings(false, true, TerminalBellSound.Information);
            saved.Write(key); Check(TerminalBellSettings.Read(key) == saved, "registry round trip");
            key.SetValue("BellSound", "999");
            Check(TerminalBellSettings.Read(key).Sound == TerminalBellSound.Notification, "invalid sound fallback");
        }
        finally { Registry.CurrentUser.DeleteSubKeyTree(path, false); }

        if (Application.Current is null) _ = new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
        var language = UiText.Current;
        try
        {
            foreach (var locale in Enum.GetValues<UiLanguage>())
            {
                UiText.Select(locale, false);
                if (locale != UiLanguage.English) Check(UiText.Get("Terminal bell") != "Terminal bell", "localized settings");
            }
            UiText.Select(UiLanguage.English, false);
            foreach (var theme in new[] { AppThemeKind.Dark, AppThemeKind.Light })
            {
                AppTheme.Apply(theme);
                var window = new FontSettingsWindow("Cascadia Mono", 14, FontWeights.Normal, FontStyles.Normal, 2000);
                window.Measure(new Size(900, 740)); window.Arrange(new Rect(0, 0, 900, 740)); window.UpdateLayout();
                Check(window.FindName("BellSoundComboBox") is ComboBox { Items.Count: 4 }, "sound selector present in both themes");
                Check(window.SelectedBellSettings == AppSettings.LoadBellSettings(), "settings load existing preferences");
                window.Close();
            }
        }
        finally { UiText.Select(language, false); }

        ProbeTerminalBell().GetAwaiter().GetResult();
        Console.WriteLine("Terminal bell: parser, settings, service, concurrency and live ConPTY tests passed.");
    }

    private static async Task ProbeTerminalBell()
    {
        using var session = new ConPtySession(80, 24);
        var ps = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "WindowsPowerShell", "v1.0", "powershell.exe");
        // Encoded source avoids mistaking echoed input for a real BEL; no user profile or network.
        var script = "[Console]::Write(([char]27).ToString()+']133;B'+[char]7); [Console]::Write([char]7); [Console]::Write('BELL_PROBE_DONE'); Start-Sleep -Milliseconds 500";
        var encoded = Convert.ToBase64String(Encoding.Unicode.GetBytes(script));
        session.Start(ShellPath.WindowsArgument(ps) + " -NoProfile -NonInteractive -EncodedCommand " + encoded);
        var buffer = new TerminalBuffer(80, 24);
        var read = Task.Run(() =>
        {
            var bytes = new byte[4096]; var decoder = Encoding.UTF8.GetDecoder(); var chars = new char[4096];
            var bells = 0; var text = new StringBuilder();
            int count;
            while ((count = session.Read(bytes)) > 0)
            {
                var output = new string(chars, 0, decoder.GetChars(bytes, 0, count, chars, 0));
                buffer.Process(output); text.Append(output); bells += buffer.ConsumePendingBells();
                if (text.ToString().Contains("BELL_PROBE_DONE", StringComparison.Ordinal)) return bells;
            }
            throw new Exception("Bell probe ended before completion marker.");
        });
        try
        {
            if (await read.WaitAsync(TimeSpan.FromSeconds(15)) != 1)
                throw new Exception("Expected one standalone BEL and no OSC bell through ConPTY.");
        }
        finally { session.Dispose(); await read.WaitAsync(TimeSpan.FromSeconds(5)); }
    }
}
