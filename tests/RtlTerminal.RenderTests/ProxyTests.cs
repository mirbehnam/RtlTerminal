using RtlTerminal;
using RtlTerminal.Proxy;
using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;

internal static partial class Program
{
    private static void CheckProxyManager()
    {
        UiText.Select(UiLanguage.English, persist: false);
        CheckMachineProxy();
        static void Check(bool value, string reason) { if (!value) throw new Exception(reason); }
        var endpoint = new ProxyEndpoint("HTTP", "127.0.0.1", 10808, "internal.test"); endpoint.Validate();
        Check(endpoint.LocalBypass(",").Contains("localhost") && endpoint.LocalBypass(",").Contains("::1"), "Loopback bypass missing");
        Check(endpoint.WindowsBypass.Contains("[::1]") && !endpoint.LocalBypass(",").Contains("[::1]"), "Windows and environment IPv6 bypass syntax were mixed");
        foreach (var invalid in new[] { endpoint with { Host = "http://host" }, endpoint with { Port = 0 }, endpoint with { Host = "a@b" }, endpoint with { Pac = "file:///evil" } })
        { try { invalid.Validate(); throw new Exception("Invalid endpoint accepted"); } catch (ArgumentException) { } }
        try { new WindowsProxyProvider().Validate(endpoint with { Protocol = "SOCKS5" }); throw new Exception("SOCKS silently converted"); } catch (NotSupportedException) { }
        var old = new ProxyState(new() { ["Flags"] = "13", ["Proxy"] = "", ["Bypass"] = "", ["Pac"] = "https://example.test/pac" });
        var win = new WindowsProxyProvider(); var desired = win.Desired(endpoint, old, false);
        Check(desired["Pac"] == old["Pac"] && desired["Flags"] == "15", "Set discarded PAC/autodetect");
        var clear = win.Desired(endpoint, old, true);
        Check(clear["Flags"] == "1" && clear["Pac"] == "" && clear["Proxy"] == "", "Clear left automatic proxy active");
        var source = "; comment\r\n[global]\r\nindex-url=https://packages.test\r\nproxy=http://old:80\r\n[install]\r\ntimeout=10\r\n";
        var edited = ProxyFileProvider.Edit(source, "global", ["proxy"], new Dictionary<string,string> { ["proxy"] = endpoint.Url });
        Check(edited.Contains("; comment\r\n") && edited.Contains("index-url=https://packages.test\r\n") && edited.Contains("[install]\r\ntimeout=10"), "Editor damaged unrelated configuration");
        Check(!edited.Contains("old:80") && edited.Contains("proxy=http://127.0.0.1:10808"), "Editor failed proxy update");
        Check(!ProxyFileProvider.Edit(edited, "global", ["proxy"], new Dictionary<string,string>()).Contains("proxy="), "Clear left proxy entry");
        var temporary = Path.Combine(Environment.CurrentDirectory, "artifacts", "proxy-tests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(temporary);
        var first = new FakeProxy("first"); var fail = new FakeProxy("fail") { Fail = true }; var last = new FakeProxy("last");
        var service = new ProxyService(temporary);
        var results = service.ExecuteAsync([first, fail, last], endpoint, false, default).GetAwaiter().GetResult();
        Check(results.Count == 3 && results[0].Success && !results[1].Success && results[2].Success, "Partial failure stopped safe remaining operations");
        Check(File.Exists(service.History.JournalPath), "Partial operation did not leave durable recovery metadata");
        Check(service.LastBackupPath != null && !File.ReadAllText(service.LastBackupPath).Contains("127.0.0.1"), "Backup was not encrypted");
        var restorePreview = new[] { first, fail, last }.ToDictionary(p => p.Name, p => p.ReadAsync(default).Result.Signature);
        service.RestoreHistoryAsync(service.LastBackupPath!, [first, fail, last], restorePreview, default).GetAwaiter().GetResult();
        Check(first.Value == "old" && last.Value == "old", "Snapshot restoration failed");
        Check(!File.Exists(service.History.JournalPath), "Verified restoration left a stale pending marker");
        var absent = new FakeProxy("absent") { Value = null! }; var untouched = new FakeProxy("inventory-only");
        service.ExecuteAsync([absent], endpoint, false, default, [absent, untouched]).GetAwaiter().GetResult();
        var absencePath = service.LastBackupPath!;
        Check(service.History.ReadAsync(absencePath, default).Result.Snapshot.Entries.Count == 2, "Snapshot omitted an unselected managed provider");
        service.RestoreHistoryAsync(absencePath, [absent, untouched], new() { [absent.Name] = absent.ReadAsync(default).Result.Signature, [untouched.Name] = untouched.ReadAsync(default).Result.Signature }, default).GetAwaiter().GetResult();
        Check(absent.Value == null, "Restore did not recover absent value");
        var denied = new FakeProxy("denied") { Denied = true };
        Check(service.ExecuteAsync([denied], endpoint, false, default).Result.Single().Outcome == ProxyOutcome.RequiresPermission, "Insufficient permission was not reported accurately");
        for (var i = 0; i < 12; i++) service.History.SaveAsync("retention fixture", [new(first.Descriptor, first.ReadAsync(default).Result)], default).GetAwaiter().GetResult();
        var retained = service.History.ListAsync(default).Result;
        Check(retained.Count == 10, "History did not retain exactly ten valid snapshots");
        var damaged = Path.Combine(temporary, "damaged.proxybackup"); File.WriteAllBytes(damaged, [1, 2, 3, 4]);
        Check(service.History.ListAsync(default).Result.Count == 10, "Corrupt snapshot appeared in valid history");
        try { service.History.ReadAsync(damaged, default).GetAwaiter().GetResult(); throw new Exception("Corrupt snapshot restored"); } catch (System.Security.Cryptography.CryptographicException) { }
        using (service.History.AcquireLock())
        {
            try { service.ExecuteAsync([first], endpoint, false, default).GetAwaiter().GetResult(); throw new Exception("Concurrent operation was accepted"); }
            catch (IOException) { }
        }
        var versioned = new FakeProxy("versioned") { Version = "1" };
        var savedVersion = service.ExecuteAsync([versioned], endpoint, false, default).GetAwaiter().GetResult();
        var versionPath = service.LastBackupPath!; versioned.Version = "2";
        try { service.RestoreHistoryAsync(versionPath, [versioned], new() { [versioned.Name] = versioned.ReadAsync(default).Result.Signature }, default).GetAwaiter().GetResult(); throw new Exception("Changed provider version was restored"); }
        catch (NotSupportedException) { }
        versioned.Version = "1";
        try { service.RestoreHistoryAsync(versionPath, [versioned], new() { [versioned.Name] = "stale preview" }, default).GetAwaiter().GetResult(); throw new Exception("Stale restore preview was accepted"); }
        catch (IOException) { }
        var unknown = new VersionedProxyProvider(first, "fixture.exe", new("999.0", 999, ["HTTP"], "Unverified / inherited", "User"));
        try { unknown.WriteAsync(first.ReadAsync(default).Result, default).GetAwaiter().GetResult(); throw new Exception("Unknown adapter version was writable"); } catch (NotSupportedException) { }
        var notTouched = new FakeProxy("not touched");
        try { service.ExecuteAsync([notTouched, new FakeProxy("bad") { Reject = true }], endpoint, false, default).GetAwaiter().GetResult(); throw new Exception("Preflight accepted incompatible provider"); }
        catch (NotSupportedException) { }
        Check(notTouched.Writes == 0, "Preflight changed earlier provider");
        var path = Path.Combine(temporary, "sample.ini"); File.WriteAllText(path, source);
        var file = new ProxyFileProvider("fixture", path, "global", ["proxy"], false);
        var before = file.ReadAsync(default).Result; file.WriteAsync(file.Desired(endpoint, before, false), default).GetAwaiter().GetResult();
        Check(File.ReadAllText(path).Contains("index-url=https://packages.test"), "Integration write damaged unrelated field");
        file.WriteAsync(before, default).GetAwaiter().GetResult(); Check(File.ReadAllText(path) == source, "Integration restore changed original bytes");
        var xmlPath = Path.Combine(temporary, "nuget.config");
        File.WriteAllText(xmlPath, "<configuration><!--preserve--><packageSources><add key=\"test\" value=\"https://example.test\" /></packageSources></configuration>");
        var xml = new XmlProxyProvider("xml fixture", xmlPath, false);
        var xmlBefore = xml.ReadAsync(default).Result;
        xml.WriteAsync(xml.Desired(endpoint, xmlBefore, false), default).GetAwaiter().GetResult();
        Check(File.ReadAllText(xmlPath).Contains("<!--preserve-->") && File.ReadAllText(xmlPath).Contains("packageSources"), "XML edit lost comments or package sources");
        if (Environment.GetCommandLineArgs().Contains("--proxy-read"))
        {
            var optionType = typeof(WindowsProxyProvider).Assembly.GetType("RtlTerminal.Proxy.WindowsInternetOptions+Option")!;
            var listType = typeof(WindowsProxyProvider).Assembly.GetType("RtlTerminal.Proxy.WindowsInternetOptions+Options")!;
            Check(System.Runtime.InteropServices.Marshal.SizeOf(optionType) == (IntPtr.Size == 8 ? 16 : 12) && System.Runtime.InteropServices.Marshal.SizeOf(listType) == (IntPtr.Size == 8 ? 32 : 20), "WinINet interop layout differs from Windows SDK");
            var gitExe = (Environment.GetEnvironmentVariable("PATH") ?? "").Split(Path.PathSeparator).Select(d => Path.Combine(d.Trim('"'), "git.exe")).FirstOrDefault(File.Exists);
            if (gitExe != null)
            {
                var gitPath = Path.Combine(temporary, "fixture.gitconfig");
                const string gitOriginal = "# preserve comments\n[user]\n name = Fixture\n[http]\n proxy = http://old.test:80\n";
                File.WriteAllText(gitPath, gitOriginal);
                var git = new GitProxyProvider(gitExe, gitPath); var originalGit = git.ReadAsync(default).Result;
                git.WriteAsync(git.Desired(endpoint, originalGit, false), default).GetAwaiter().GetResult();
                Check(File.ReadAllText(gitPath).Contains("# preserve comments") && File.ReadAllText(gitPath).Contains(endpoint.Url), "Native Git planning damaged unrelated settings");
                git.WriteAsync(originalGit, default).GetAwaiter().GetResult(); Check(File.ReadAllText(gitPath) == gitOriginal, "Git restoration was not byte-exact");
            }
        }
        if (Environment.GetCommandLineArgs().Contains("--proxy-read"))
        {
            new WinHttpBasicInspectionProvider().ReadAsync(default).GetAwaiter().GetResult();
            var native = new WindowsProxyProvider().ReadAsync(default).GetAwaiter().GetResult();
            Check(int.TryParse(native["Flags"], out _), "Windows proxy read returned malformed flags");
            try { new WinHttpProxyProvider().ReadAsync(default).GetAwaiter().GetResult(); Console.WriteLine("PASS read-only WinHTTP advanced query"); }
            catch (NotSupportedException) { Console.WriteLine("SKIP WinHTTP writes: unsupported OS or effective Machine scope"); }
            Console.WriteLine("PASS read-only WinINet per-connection query");
        }
        var window = new ProxyManagerWindow();
        var opt = (CheckBox)typeof(ProxyManagerWindow).GetField("optIn", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(window)!;
        Check(opt.IsChecked == false, "Developer tools are opted in by default"); window.Close();
        CheckProxyScopes(temporary);
        if (Environment.GetCommandLineArgs().Contains("--proxy-preview"))
        {
            if (Application.Current == null) _ = new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
            foreach (var theme in new[] { AppThemeKind.Dark, AppThemeKind.Light })
            {
                AppTheme.Apply(theme);
                var preview = new ProxyManagerWindow(); preview.Show();
                for (var i = 0; i < 160; i++)
                {
                    var frame = new System.Windows.Threading.DispatcherFrame();
                    preview.Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.ContextIdle, new Action(() => frame.Continue = false));
                    System.Windows.Threading.Dispatcher.PushFrame(frame); Thread.Sleep(25);
                }
                preview.UpdateLayout();
                var detectedCount = ((System.Collections.IList)typeof(ProxyManagerWindow).GetField("tools", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(preview)!).Count;
                Console.WriteLine("Proxy preview " + theme + " cards: " + detectedCount);
                var bitmap = new System.Windows.Media.Imaging.RenderTargetBitmap((int)preview.ActualWidth, (int)preview.ActualHeight, 96, 96, System.Windows.Media.PixelFormats.Pbgra32);
                bitmap.Render(preview);
                var encoder = new System.Windows.Media.Imaging.PngBitmapEncoder(); encoder.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(bitmap));
                Directory.CreateDirectory("artifacts/render-checks");
                using (var image = File.Create("artifacts/render-checks/proxy-" + theme + ".png")) encoder.Save(image);
                var panel = (Expander)typeof(ProxyManagerWindow).GetField("developerPanel", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(preview)!;
                panel.IsExpanded = true; preview.UpdateLayout();
                var expanded = new System.Windows.Media.Imaging.RenderTargetBitmap((int)preview.ActualWidth, (int)preview.ActualHeight, 96, 96, System.Windows.Media.PixelFormats.Pbgra32);
                expanded.Render(preview);
                var expandedEncoder = new System.Windows.Media.Imaging.PngBitmapEncoder(); expandedEncoder.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(expanded));
                using (var image = File.Create("artifacts/render-checks/proxy-expanded-" + theme + ".png")) expandedEncoder.Save(image);
                panel.BringIntoView(); preview.UpdateLayout();
                CaptureOperation(preview, "tools-" + theme);
                preview.Width = 620; preview.UpdateLayout(); panel.BringIntoView();
                CaptureOperation(preview, "narrow-" + theme);
                preview.Close();
            }
        }
        Check(ProxyService.Redact("http://user:secret@host:80").Contains("secret") == false, "Credentials leaked into diagnostics");
        if (Environment.GetCommandLineArgs().Contains("--proxy-versions"))
            new DeveloperToolDetectionService().DetectAsync(tool => { if (tool.Capabilities != null) Console.WriteLine(tool.Name + " " + tool.Capabilities.Version + " " + (tool.Provider == null ? "inspection" : "configurable")); }, true, default).GetAwaiter().GetResult();
        Console.WriteLine("PASS proxy validation, loopback, protocol compatibility, PAC preservation/clear, opt-in default, encrypted snapshots, partial failure, preflight and lossless temporary-file restore");
        var clearStore = new ProxyHistoryStore(Path.Combine(temporary, "clear-history"));
        var clearSnapshot = clearStore.SaveAsync("clear-history fixture", [new(first.Descriptor, first.ReadAsync(default).Result)], default).Result;
        clearStore.MarkPendingAsync(clearSnapshot, default).GetAwaiter().GetResult();
        var unrelated = Path.Combine(clearStore.DirectoryPath, "unrelated.txt"); File.WriteAllText(unrelated, "preserve");
        using (clearStore.AcquireLock())
        {
            try { clearStore.ClearAsync(default).GetAwaiter().GetResult(); throw new Exception("History clear bypassed operation lock"); } catch (IOException) { }
        }
        clearStore.ClearAsync(default).GetAwaiter().GetResult();
        if (clearStore.ListAsync(default).Result.Count != 0 || File.Exists(clearStore.JournalPath) || File.ReadAllText(unrelated) != "preserve") throw new Exception("History clear left snapshots/reminder or removed unrelated data");
        Console.WriteLine("PASS confirmed-history-clear storage semantics, operation lock, recovery marker removal and unrelated-file preservation");
    }
    private static void CheckProxyScopes(string temporary)
    {
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
        var window = new ProxyManagerWindow();
        T Field<T>(string name) => (T)typeof(ProxyManagerWindow).GetField(name, flags)!.GetValue(window)!;
        var scope = Field<ListBox>("section"); var opt = Field<CheckBox>("optIn"); var panel = Field<Expander>("developerPanel");
        if (scope.SelectedIndex != 0 || !scope.Items.Cast<string>().SequenceEqual(new[] { "All", "Environment", "Windows System", "WinHTTP" }) || panel.IsExpanded || opt.IsChecked != false)
            throw new Exception("Proxy defaults are not All / collapsed / opted out");
        var systems = Field<IProxyProvider[]>("systems");
        var fakes = new[] { new FakeProxy("environment"), new FakeProxy("windows"), new FakeProxy("winhttp") };
        var machine = new FakeProxy("Environment (Machine · administrator)");
        for (var i = 0; i < 3; i++) systems[i] = fakes[i];
        var tool = new FakeProxy("developer");
        var card = new CheckBox { IsChecked = true };
        var tools = Field<List<(DetectedProxyTool Tool, CheckBox Card)>>("tools");
        tools.Add((new("Fixture", "fixture.exe", tool, "fixture"), card));
        tools.Add((new("Unsupported", "fixture.exe", null, "inspection"), new CheckBox { IsChecked = true }));
        var select = typeof(ProxyManagerWindow).GetMethod("SelectedProviders", flags)!;
        List<IProxyProvider> Selected() => (List<IProxyProvider>)select.Invoke(window, null)!;
        var service = new ProxyService(Path.Combine(temporary, "scope-history"));
        var endpoint = new ProxyEndpoint("HTTP", "127.0.0.1", 10808, "");
        for (var i = 0; i < 4; i++)
        {
            scope.SelectedIndex = i;
            var expected = i == 0 ? new[] { fakes[0].Name, machine.Name, fakes[1].Name, fakes[2].Name } : i == 1 ? new[] { fakes[0].Name, machine.Name } : new[] { fakes[i - 1].Name };
            if (!Selected().Select(p => p.Name).SequenceEqual(expected)) throw new Exception("Scope included another Windows provider or opted-out tool");
            if ((string)Field<Button>("setProxy").Content != (i == 0 ? "Set All Proxies" : "Set Proxy") || (string)Field<Button>("clearProxy").Content != (i == 0 ? "Clear All Proxies" : "Clear Proxy"))
                throw new Exception("Set/Clear labels do not match selected scope");
            foreach (var clear in new[] { false, true })
            {
                foreach (var fake in fakes.Append(machine).Append(tool)) fake.Value = "old";
                var selectedProviders = Selected().Select(p => p is MachineEnvironmentProxyProvider ? machine : p).ToArray();
                service.ExecuteAsync(selectedProviders, endpoint, clear, default, fakes.Cast<IProxyProvider>().Append(machine).Append(tool).ToArray()).GetAwaiter().GetResult();
                foreach (var fake in fakes.Append(machine).Append(tool))
                    if (fake.Value != (expected.Contains(fake.Name) ? clear ? "" : endpoint.Url : "old")) throw new Exception("Scoped Set/Clear modified an unrelated provider");
                if (service.History.ReadAsync(service.LastBackupPath!, default).Result.Snapshot.Entries.Count != 5) throw new Exception("Scoped operation lost comprehensive inventory backup");
            }
            opt.IsChecked = true;
            if (!panel.IsExpanded) throw new Exception("Developer opt-in did not reveal tool selections");
            if (!Selected().Select(p => p.Name).SequenceEqual(expected.Append(tool.Name))) throw new Exception("Opt-in did not add only the selected supported developer tool");
            panel.IsExpanded = true; panel.IsExpanded = false;
            if (opt.IsChecked != true) throw new Exception("Expanding/collapsing tools changed opt-in");
            opt.IsChecked = false;
            if (card.IsChecked != true) throw new Exception("Opt-out discarded tool selection");
        }
        var protocol = Field<ComboBox>("protocol"); var host = Field<TextBox>("host"); var port = Field<TextBox>("port");
        scope.SelectedIndex = 0; host.Text = "all.example.test";
        if (protocol.IsEnabled || !protocol.Items.Cast<string>().SequenceEqual(new[] { "HTTP" })) throw new Exception("All offered an unsupported proxy protocol");
        scope.SelectedIndex = 1; protocol.SelectedItem = "SOCKS5"; host.Text = "environment.example.test"; port.Text = "1080";
        if (!protocol.IsEnabled || protocol.Items.Count != 3) throw new Exception("Environment lost supported proxy protocols");
        foreach (var index in new[] { 2, 3, 0 })
        {
            scope.SelectedIndex = index;
            if (protocol.IsEnabled || protocol.SelectedItem?.ToString() != "HTTP" || protocol.Items.Count != 1) throw new Exception("HTTP-only provider inherited SOCKS5");
        }
        if (host.Text != "all.example.test") throw new Exception("All connection draft was overwritten by another scope");
        scope.SelectedIndex = 1;
        if (protocol.SelectedItem?.ToString() != "SOCKS5" || host.Text != "environment.example.test" || port.Text != "1080") throw new Exception("Environment connection draft was lost across scope changes");
        if (opt.IsChecked == true || Field<StackPanel>("automatic").Visibility != Visibility.Collapsed) throw new Exception("Scope changes enabled developer tools or exposed unrelated PAC controls");
        var pendingRead = new TaskCompletionSource<ProxyState>(TaskCreationOptions.RunContinuationsAsynchronously);
        fakes[0].ReadOverride = pendingRead.Task;
        Task? reading = null;
        window.Dispatcher.BeginInvoke(new Action(() => reading = (Task)typeof(ProxyManagerWindow).GetMethod("ReadSelected", flags)!.Invoke(window, new object[] { true })!));
        PumpUntil(() => reading != null);
        host.Text = "typed-during-read.example.test";
        pendingRead.SetResult(new ProxyState(new() { ["HTTP_PROXY"] = "http://older.example.test:8080" }));
        PumpUntil(() => reading!.IsCompleted); reading!.GetAwaiter().GetResult();
        if (host.Text != "typed-during-read.example.test") throw new Exception("A delayed configuration read overwrote a newer user edit");
        window.Close();
        Console.WriteLine("PASS All/default segmented scopes, HTTP compatibility, preserved per-scope connection drafts, dynamic labels, isolated Set/Clear, opt-in across scopes, collapsed panel independence and comprehensive scoped-operation backups");
    }
    private sealed class FakeProxy(string name) : IProxyProvider
    {
        public string Name => name;
        public string Version = "schema-1";
        public ProxyDescriptor Descriptor => new(Name, Version, "fixture", "User");
        public string Value = "old"; public int Writes; public bool Fail, Reject, Denied;
        public Task<ProxyState>? ReadOverride;
        public void Validate(ProxyEndpoint endpoint) { if (Reject) throw new NotSupportedException("fixture"); }
        public Task<ProxyState> ReadAsync(CancellationToken ct) => ReadOverride ?? Task.FromResult(new ProxyState(new() { ["Value"] = Value }));
        public ProxyState Desired(ProxyEndpoint e, ProxyState old, bool clear) => new(new() { ["Value"] = clear ? "" : e.Url });
        public Task WriteAsync(ProxyState state, CancellationToken ct) { if (Denied) throw new UnauthorizedAccessException(); if (Fail && state["Value"] != "old") throw new IOException("fixture"); Writes++; Value = state["Value"]!; return Task.CompletedTask; }
    }
}
