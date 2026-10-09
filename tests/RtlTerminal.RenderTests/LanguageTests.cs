using System.Collections;
using System.Net.Http;
using System.Reflection;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Threading;
using RtlTerminal;

internal static partial class Program
{
    private static void CheckLanguageChanges()
    {
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
        var previousLanguage = Ui.Language;
        var previousContext = SynchronizationContext.Current;
        SynchronizationContext.SetSynchronizationContext(new DispatcherSynchronizationContext(Dispatcher.CurrentDispatcher));
        Ui.Language = UiLanguage.English;
        var window = new MainWindow();
        try
        {
            var tabs = (IList)typeof(MainWindow).GetField("_tabs", flags)!.GetValue(window)!;
            var tabType = typeof(MainWindow).GetNestedType("TerminalTab", BindingFlags.NonPublic)!;
            var profileType = typeof(MainWindow).GetNestedType("TerminalProfile", BindingFlags.NonPublic)!;
            var profile = Enum.ToObject(profileType, 0);
            string ProfileTitle() => (string)typeof(MainWindow).GetMethod("GetProfileTitle", BindingFlags.Static | BindingFlags.NonPublic)!
                .Invoke(null, new[] { profile })!;
            tabs.Add(Activator.CreateInstance(tabType, 1, profile, ProfileTitle()));
            var updateItem = (MenuItem)window.FindName("CheckForUpdatesMenuItem");

            void Switch(UiLanguage language)
            {
                Ui.Language = language;
                typeof(MainWindow).GetMethod("ApplyLanguage", flags)!.Invoke(window, null);
                typeof(MainWindow).GetMethod("RebuildTabStrip", flags)!.Invoke(window, null);
                var menu = (Menu)window.FindName("MainMenu");
                var direction = language == UiLanguage.Hebrew ? FlowDirection.RightToLeft : FlowDirection.LeftToRight;
                if (menu.FlowDirection != direction || (string)((MenuItem)menu.Items[0]).Header != Ui.T("_File"))
                    throw new Exception("Language switching did not update menu text and direction");
                for (var i = 0; i < tabs.Count; i++)
                {
                    var expected = $"{Ui.T("Command Prompt")} {i + 1}";
                    if ((string)tabType.GetProperty("Title")!.GetValue(tabs[i])! != expected)
                        throw new Exception("Existing tab retained the old language");
                    var strip = (StackPanel)window.FindName("TabStrip");
                    var grid = (Grid)strip.Children[i];
                    var panel = (DockPanel)grid.Children.OfType<DockPanel>().Single();
                    var buttons = panel.Children.OfType<Button>().ToArray();
                    if (AutomationProperties.GetName(buttons[0]) != Ui.T("Close {0}", expected) ||
                        AutomationProperties.GetName(buttons[1]) != expected)
                        throw new Exception("Tab controls retained old-language labels after rebuilding");
                }
            }

            Switch(UiLanguage.Hebrew);
            tabs.Add(Activator.CreateInstance(tabType, 2, profile, ProfileTitle()));
            Switch(UiLanguage.English);

            foreach (var failure in new[] { false, true })
            {
                var completion = new TaskCompletionSource<UpdateCheckResult>(TaskCreationOptions.RunContinuationsAsynchronously);
                Func<Task<UpdateCheckResult>> check = () => completion.Task;
                var task = (Task)typeof(MainWindow).GetMethod("CheckForUpdatesAsync", flags)!
                    .Invoke(window, new object[] { false, check })!;
                Switch(UiLanguage.Hebrew);
                if (updateItem.IsEnabled || (string)updateItem.Header != Ui.T("Checking for updates..."))
                    throw new Exception("Language switching lost the pending update-check state");
                if (failure)
                    completion.SetException(new HttpRequestException("Simulated offline update check"));
                else
                    completion.SetResult(new UpdateCheckResult(new Version(1, 0, 7), new Version(1, 0, 7),
                        "v1.0.7", new Uri("https://github.com/mirbehnam/RtlTerminal/releases/tag/v1.0.7"), false));
                var frame = new DispatcherFrame();
                _ = task.ContinueWith(_ => window.Dispatcher.BeginInvoke(() => frame.Continue = false));
                Dispatcher.PushFrame(frame);
                task.GetAwaiter().GetResult();
                if (!updateItem.IsEnabled || (string)updateItem.Header != Ui.T("Check for _updates..."))
                    throw new Exception("Update completion restored an old-language header");
                Switch(UiLanguage.English);
            }
            Console.WriteLine("PASS language round trips for existing/new tabs, accessibility and pending/successful/failed update checks");
        }
        finally
        {
            window.Close();
            Ui.Language = previousLanguage;
            SynchronizationContext.SetSynchronizationContext(previousContext);
        }
    }
}
