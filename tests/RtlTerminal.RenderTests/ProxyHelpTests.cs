using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using RtlTerminal;
using RtlTerminal.Proxy;

internal static partial class Program
{
    private static void CheckProxyDefaultsAndHelp()
    {
        static void Check(bool condition, string message) { if (!condition) throw new Exception(message); }
        const BindingFlags flags = BindingFlags.NonPublic | BindingFlags.Instance;
        if (Application.Current == null) _ = new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
        var original = UiText.Current; UiText.Select(UiLanguage.English, persist: false); AppTheme.Apply(AppThemeKind.Dark);
        var manager = new ProxyManagerWindow();
        T Field<T>(string name) => (T)typeof(ProxyManagerWindow).GetField(name, flags)!.GetValue(manager)!;
        var host = Field<TextBox>("host"); var port = Field<TextBox>("port"); var bypass = Field<TextBox>("bypass"); var section = Field<ListBox>("section");
        Check(host.Text == "" && port.Text == "" && bypass.Text == "", "Proxy fields contained a sample/default value");
        var user = new StatusFixtureProxy(new(new())); var windows = new StatusFixtureProxy(new(new() { ["Flags"] = "1" })); var http = new StatusFixtureProxy(new(new()));
        var machineState = new ProxyState(new()); var systems = Field<IProxyProvider[]>("systems"); systems[0] = user; systems[1] = windows; systems[2] = http;
        typeof(ProxyManagerWindow).GetField("machineEnvironment", flags)!.SetValue(manager, new MachineEnvironmentProxyProvider(() => machineState));
        void Read()
        {
            Task? read = null;
            manager.Dispatcher.BeginInvoke(new Action(() => read = (Task)typeof(ProxyManagerWindow).GetMethod("ReadSelected", flags)!.Invoke(manager, new object[] { true })!));
            PumpUntil(() => read != null); PumpUntil(() => read!.IsCompleted); read!.GetAwaiter().GetResult();
        }
        try
        {
            Read(); Check(host.Text == "" && port.Text == "", "Empty persistent settings invented a proxy endpoint");
            machineState = new(new() { ["HTTP_PROXY"] = "http://machine.example.test:3128" }); Read();
            Check(host.Text == "machine.example.test" && port.Text == "3128", "Existing Machine environment settings were not loaded");
            user.State = new(new() { ["HTTP_PROXY"] = "http://user.example.test:8080" }); Read();
            Check(host.Text == "user.example.test" && port.Text == "8080", "User environment did not take precedence over Machine");
            machineState = new(new()); user.State = new(new()); Read();
            Check(host.Text == "" && port.Text == "" && bypass.Text == "", "Cleared settings left an old proxy address in the editor");
            section.SelectedIndex = 1; user.State = new(new() { ["ALL_PROXY"] = "socks5://127.0.0.1:1080" }); Read();
            Check(host.Text == "127.0.0.1" && port.Text == "1080" && Field<ComboBox>("protocol").SelectedItem?.ToString() == "SOCKS5", "Existing ALL_PROXY was not loaded accurately");
            section.SelectedIndex = 2; windows.State = new(new() { ["Flags"] = "3", ["Proxy"] = "http=windows.example.test:8080;https=windows.example.test:8080" }); Read();
            Check(host.Text == "windows.example.test" && port.Text == "8080", "Existing Windows protocol mapping was not loaded");
            var automatic = new ProxyEndpoint("HTTP", "", 0, "") { ManualEnabled = false, Pac = "https://example.test/proxy.pac" };
            automatic.Validate(); var desired = new WindowsProxyProvider().Desired(automatic, windows.State, false);
            Check(desired["Proxy"] == "" && desired["Pac"] == automatic.Pac, "PAC-only settings required or invented a manual endpoint");
        }
        finally { manager.Close(); }
        var help = new ProxyHelpWindow();
        try
        {
            foreach (var language in new[] { UiLanguage.English, UiLanguage.Persian, UiLanguage.Arabic, UiLanguage.English })
            {
                UiText.Select(language, persist: false);
                var content = ProxyHelpContent.Get(language); Check(content.Count == 4 && content.All(s => s.Title.Length > 0 && s.Body.Length > 0), "Quick help is incomplete");
                Check(help.Title == UiText.Get("Proxy Manager Guide"), "Quick-help title did not follow live language changes");
                var guide = new GuideWindow();
                var sections = (ItemsControl)guide.FindName("ProxyGuideSections");
                Check(sections.ItemsSource.Cast<ProxyHelpSection>().SequenceEqual(content), "Help menu Guide and quick help have different content/language"); guide.Close();
            }
            if (Environment.GetCommandLineArgs().Contains("--visuals"))
            {
                help.Show();
                foreach (var language in new[] { UiLanguage.English, UiLanguage.Persian, UiLanguage.Arabic })
                { UiText.Select(language, persist: false); CaptureOperation(help, "proxy-help-" + language); }
            }
        }
        finally { help.Close(); UiText.Select(original, persist: false); }
        Console.WriteLine("PASS empty proxy fields, configured User/Machine precedence, clearing stale values, SOCKS/Windows mapping loading, endpoint-free PAC and shared English/Persian/Arabic quick-help/Guide content");
    }
}
