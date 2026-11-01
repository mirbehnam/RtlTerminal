using RtlTerminal;
using RtlTerminal.Proxy;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;

internal static partial class Program
{
    private static void CheckProxyLanguages(bool visuals = false)
    {
        static void Check(bool value, string message) { if (!value) throw new Exception(message); }
        const BindingFlags flags = BindingFlags.NonPublic | BindingFlags.Instance;
        var original = UiText.Current;
        if (Application.Current == null) _ = new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
        AppTheme.Apply(AppThemeKind.Dark);
        UiText.Select(UiLanguage.English, persist: false);
        var manager = new ProxyManagerWindow();
        var host = (TextBox)typeof(ProxyManagerWindow).GetField("host", flags)!.GetValue(manager)!;
        var scopes = (ListBox)typeof(ProxyManagerWindow).GetField("section", flags)!.GetValue(manager)!;
        var opt = (CheckBox)typeof(ProxyManagerWindow).GetField("optIn", flags)!.GetValue(manager)!;
        var set = (Button)typeof(ProxyManagerWindow).GetField("setProxy", flags)!.GetValue(manager)!;
        var protocol = (ComboBox)typeof(ProxyManagerWindow).GetField("protocol", flags)!.GetValue(manager)!;
        host.Text = "Proxy Manager"; // Literal user data that happens to match a UI key.
        const string rawLog = "User settings\nHTTP_PROXY=http://example.test:8080\nNativeErrorCode=87";
        var log = new ProxyLogWindow(rawLog);
        var logText = ((DockPanel)log.Content).Children.OfType<TextBox>().Single();
        var operation = new ProxyOperationWindow(["Environment (User)"], true, _ => throw new Exception("Test must not execute configuration"));
        operation.State.Complete([new("Environment (User)", true, "Saved and verified. Environment/project overrides and existing processes may retain other settings.", ProxyOutcome.Success)]);
        var reminder = (TextBlock)typeof(ProxyOperationWindow).GetField("restartNotice", flags)!.GetValue(operation)!;
        const string reminderSource = "You may need to close and reopen the affected application for proxy changes to take effect.";
        try
        {
            foreach (var language in new[] { UiLanguage.English, UiLanguage.Persian, UiLanguage.Arabic, UiLanguage.English })
            {
                UiText.Select(language, persist: false);
                Check(manager.Title == UiText.Get("Proxy Manager") && (string)set.Content == UiText.Get("Set All Proxies"), "Proxy window title/button did not follow live language changes");
                Check(reminder.Text == UiText.Get(reminderSource) && log.Title == UiText.Get("Proxy operation log"), "Operation/log/reminder text stayed in another language");
                Check(manager.FlowDirection == FlowDirection.LeftToRight && operation.FlowDirection == FlowDirection.LeftToRight && scopes.SelectedIndex == 0 && opt.IsChecked == false, "Language changed layout direction, scope or opt-in");
                Check(host.Text == "Proxy Manager" && protocol.SelectedItem?.ToString() == "HTTP" && logText.Text == rawLog, "Translation modified literal input, protocol or diagnostic data");
                if (language != UiLanguage.English) Check(manager.Title != "Proxy Manager" && reminder.Text != reminderSource, "Proxy catalog has no translation");
            }
            if (visuals)
            {
                manager.Show();
                foreach (var language in new[] { UiLanguage.Persian, UiLanguage.Arabic, UiLanguage.English })
                {
                    UiText.Select(language, persist: false);
                    CaptureOperation(manager, "language-" + language);
                }
            }
            Console.WriteLine("PASS proxy English/Persian/Arabic live titles, buttons, operation/reminder/log, return to English, fixed direction/scope/opt-in and preserved literal values/protocol/raw log");
        }
        finally { manager.Close(); log.Close(); operation.Close(); UiText.Select(original, persist: false); }
    }
}
