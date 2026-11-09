using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using RtlTerminal;

internal static partial class Program
{
    private static void CheckQuickInputFluent()
    {
        if (Application.Current is null) _ = new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
        var initialTheme = AppTheme.Current; var initialScheme = AppTheme.Scheme;
        var language = UiText.Current;
        var application = Application.Current!;
        var dictionaries = application.Resources.MergedDictionaries.ToArray();
        var globalButton = application.TryFindResource(typeof(Button));
        const System.Reflection.BindingFlags flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
        try
        {
            UiText.Select(UiLanguage.English, false);
            foreach (var theme in new[] { AppThemeKind.Dark, AppThemeKind.Light })
            {
                AppTheme.Apply(theme);
                using var images = new QuickInputImages();
                var draft = new QuickInputDraft();
                draft.InsertText("سلام دنیا\nWrite and edit a prompt comfortably\nفارسی و English در کنار هم");
                var window = new QuickInputWindow(draft, _ => Task.FromResult(new TerminalInputResult(false, false, "test")), images, () => new HashSet<string>());
                try
                {
                    if (!window.Resources.MergedDictionaries.Any(d => d.Source?.OriginalString.Contains($"Fluent.{theme}.xaml") == true))
                        throw new Exception("Quick Input did not load the selected Fluent theme locally.");
                    window.Show(); window.UpdateLayout();
                    if (window.Opacity != 1 || window.Content is not Border { Background: LinearGradientBrush glass } ||
                        glass.GradientStops.Any(s => s.Color.A is 0 or 255) || draft.Editor.Background is not SolidColorBrush { Color.A: < 255 })
                        throw new Exception("Quick Input must use translucent surfaces without fading text or controls.");
                    var send = (Button)typeof(QuickInputWindow).GetField("sendAction", flags)!.GetValue(window)!;
                    var insert = (Button)typeof(QuickInputWindow).GetField("insertAction", flags)!.GetValue(window)!;
                    // WPF theme synchronization can reload dictionaries on Show, replacing resource identities.
                    if (send.ReadLocalValue(FrameworkElement.StyleProperty) is not Style || send.Template is null || send.Template.FindName("Frame", send) is Border)
                        throw new Exception("Send must use the native Fluent accent template, not the proxy button template.");
                    if (insert.Style is null || insert.Template is null || insert.Template.FindName("Frame", insert) is Border || draft.Editor.Template is null)
                        throw new Exception("Quick Input controls did not use Fluent templates.");
                    if (application.Resources.MergedDictionaries.Count != dictionaries.Length || !ReferenceEquals(globalButton, application.TryFindResource(typeof(Button))))
                        throw new Exception("Quick Input changed application-wide styling.");
                    CaptureOperation(window, "quick-input-fluent-" + theme);
                    var confirm = window.ConfirmSingleLineAsync();
                    CaptureOperation(window, "quick-input-fluent-confirm-" + theme);
                    typeof(QuickInputWindow).GetMethod("CompleteConfirmation", flags)!.Invoke(window, new object[] { false });
                    if (confirm.GetAwaiter().GetResult() || draft.Text(a => a.Path) != "سلام دنیا\nWrite and edit a prompt comfortably\nفارسی و English در کنار هم")
                        throw new Exception("Fluent confirmation changed draft text.");
                }
                finally { window.Close(); }
                // Reuse the same in-memory editor after the Fluent window has detached it.
                var reopened = new QuickInputWindow(draft, _ => Task.FromResult(new TerminalInputResult(false, false, "test")), images, () => new HashSet<string>());
                reopened.Show(); reopened.Close();
            }
        }
        finally { AppTheme.Apply(initialTheme, initialScheme); UiText.Select(language, false); }
        Console.WriteLine("PASS Quick Input native Fluent templates, light/dark, local scope, confirmation and reusable draft");
    }
}
