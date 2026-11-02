using RtlTerminal.Proxy;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Threading;

namespace RtlTerminal;

public sealed partial class ProxyManagerWindow : Window
{
    private readonly CancellationTokenSource lifetime = new();
    private readonly ProxyService service = new();
    private readonly IProxyProvider[] systems = [new EnvironmentProxyProvider(), new WindowsProxyProvider(), new WinHttpProxyProvider()];
    private readonly MachineEnvironmentProxyProvider machineEnvironment = new();
    private readonly DeveloperToolDetectionService detection = new();
    private readonly TextBox host = new(), port = new(), bypass = new();
    private readonly ComboBox protocol = new() { ItemsSource = new[] { "HTTP" }, SelectedIndex = 0, MinWidth = 100 };
    private readonly TextBlock protocolHint = new() { TextWrapping = TextWrapping.Wrap, FontSize = 11, Margin = new Thickness(0, 8, 0, 0) };
    private readonly TextBlock protocolNotice = new() { TextWrapping = TextWrapping.Wrap, FontSize = 11, Margin = new Thickness(0, 4, 0, 0), Visibility = Visibility.Collapsed };
    private readonly TextBlock connectionError = new() { TextWrapping = TextWrapping.Wrap, FontSize = 12, Margin = new Thickness(0, 6, 0, 0), Visibility = Visibility.Collapsed };
    private sealed record ConnectionDraft(string Host, string Port, string Bypass, string Protocol, bool Manual, bool AutoDetect, string Pac);
    private readonly Dictionary<int, ConnectionDraft> connectionDrafts = [];
    private int editorScope, readGeneration;
    private readonly ListBox section = new() { ItemsSource = new[] { "All", "Environment", "Windows System", "WinHTTP" }, SelectedIndex = 0 };
    private readonly Button setProxy;
    private readonly Button clearProxy;
    private readonly Button setToolProxy, clearToolProxy;
    private readonly Expander developerPanel = new() { IsExpanded = false };
    private readonly TextBox diagnostics = new() { IsReadOnly = true, TextWrapping = TextWrapping.Wrap, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, MinHeight = 95, MaxHeight = 155 };
    private readonly TextBlock result = new() { TextWrapping = TextWrapping.Wrap }, loading = new() { Text = "Discovering installed tools…" }, selected = new();
    private readonly StackPanel automatic = new();
    private readonly CheckBox manual = new() { Content = "Enable manual proxy", IsChecked = true }, autoDetect = new() { Content = "Automatically detect settings" };
    private readonly TextBox pac = new();
    private readonly CheckBox optIn = new() { Content = "Also apply to selected developer tools", IsChecked = false };
    private readonly WrapPanel cards = new();
    private readonly List<(DetectedProxyTool Tool, CheckBox Card)> tools = [];
    private readonly List<Button> mutations = [];
    private readonly Button refresh;
    private readonly Button restore;
    private readonly Button clearHistory;
    private readonly ListBox historyList = new() { MaxHeight = 135, MinHeight = 65 };
    private List<IProxyProvider> lastProviders = [];
    private bool busy, closed, detectionComplete;
    private string diagnosticSource = "";
    private CancellationTokenSource? discovery;

    public ProxyManagerWindow(bool openHistory = false)
    {
        using var timing = StartupPerformance.Measure("ProxyManager.Construct");
        Title = "Proxy Manager"; Width = 920; Height = 720; MinWidth = 600; MinHeight = 580;
        WindowStartupLocation = WindowStartupLocation.CenterOwner; FontFamily = new FontFamily("Segoe UI"); FontSize = 13;
        Resources.MergedDictionaries.Add(new ResourceDictionary { Source = new Uri("/RtlTerminal;component/Views/ProxyManagerStyles.xaml", UriKind.Relative) });
        SetResourceReference(BackgroundProperty, "MenuBackgroundBrush"); SetResourceReference(ForegroundProperty, "ChromeForegroundBrush");
        Resources.MergedDictionaries.Insert(0, new ResourceDictionary { Source = new Uri("/RtlTerminal;component/Controls/TerminalScrollBar.xaml", UriKind.Relative) });
        SetResourceReference(BackgroundProperty, "TitleBarBrush");
        UseLayoutRounding = true;
        WindowCaptionTheme.Attach(this);
        var root = new Grid(); Content = root;
        root.RowDefinitions.Add(new()); root.RowDefinitions.Add(new() { Height = GridLength.Auto });
        var scroll = new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto, Margin = new Thickness(16, 12, 8, 0) }; root.Children.Add(scroll);
        var body = new StackPanel { Margin = new Thickness(0, 0, 10, 0) }; scroll.Content = body;
        body.Children.Add(CreateStatusDashboard());
        var scope = new StackPanel(); scope.Children.Add(SectionHeading("Configuration scope", null));
        section.Style = (Style)FindResource("ProxyScopeSelector"); scope.Children.Add(section);
        var connection = new StackPanel { Margin = new Thickness(0, 8, 0, 0) };
        var endpoint = new Grid(); endpoint.ColumnDefinitions.Add(new() { Width = new GridLength(130) }); endpoint.ColumnDefinitions.Add(new()); endpoint.ColumnDefinitions.Add(new() { Width = new GridLength(110) });
        endpoint.Children.Add(Field("Protocol", protocol)); var h = Field("Host / IP", host); Grid.SetColumn(h, 1); endpoint.Children.Add(h); var p = Field("Port", port); Grid.SetColumn(p, 2); endpoint.Children.Add(p); connection.Children.Add(endpoint);
        connection.Children.Add(Field("Bypass addresses", bypass));
        bypass.ToolTip = "Localhost and loopback are always included when setting a proxy.";
        protocolHint.SetResourceReference(TextBlock.ForegroundProperty, "MutedForegroundBrush"); connection.Children.Add(protocolHint); scope.Children.Add(connection);
        protocolNotice.SetResourceReference(TextBlock.ForegroundProperty, "AccentBrush"); connection.Children.Add(protocolNotice);
        connectionError.Foreground = new SolidColorBrush(Color.FromRgb(225, 85, 100)); connection.Children.Add(connectionError);
        host.TextChanged += (_, _) => connectionError.Visibility = Visibility.Collapsed;
        port.TextChanged += (_, _) => connectionError.Visibility = Visibility.Collapsed;
        bypass.TextChanged += (_, _) => connectionError.Visibility = Visibility.Collapsed;
        pac.TextChanged += (_, _) => connectionError.Visibility = Visibility.Collapsed;
        section.ToolTip = "Environment includes User + Machine (UAC). Windows System and WinHTTP use the current user.";
        foreach (var check in new[] { optIn, manual, autoDetect })
        {
            var label = new TextBlock { Text = check.Content?.ToString() ?? "", TextWrapping = TextWrapping.Wrap };
            label.SetResourceReference(TextBlock.ForegroundProperty, "ChromeForegroundBrush"); check.Content = label;
        }
        automatic.Margin = new Thickness(0, 12, 0, 0); automatic.Children.Add(manual); automatic.Children.Add(autoDetect); automatic.Children.Add(Field("Automatic configuration script (PAC URL)", pac));
        automatic.Visibility = Visibility.Collapsed; scope.Children.Add(automatic); body.Children.Add(Card(scope));
        var developer = new StackPanel(); developer.Children.Add(SectionHeading("Developer Tools", "Optional. Only selected, supported tools are modified."));
        developer.Children.Add(optIn); optIn.Checked += (_, _) => { developerPanel.IsExpanded = true; UpdateCards(); }; optIn.Unchecked += (_, _) => UpdateCards();
        var developerHeader = new Grid(); developerHeader.ColumnDefinitions.Add(new()); developerHeader.ColumnDefinitions.Add(new() { Width = GridLength.Auto });
        var developerLabel = new TextBlock { Text = "Installed tools", Margin = new Thickness(0, 4, 0, 4) }; developerLabel.SetResourceReference(TextBlock.ForegroundProperty, "ChromeForegroundBrush"); developerHeader.Children.Add(developerLabel);
        selected.SetResourceReference(TextBlock.ForegroundProperty, "MutedForegroundBrush"); selected.VerticalAlignment = VerticalAlignment.Center; selected.Margin = new Thickness(12, 0, 4, 0); Grid.SetColumn(selected, 1); developerHeader.Children.Add(selected); developerPanel.Header = developerHeader;
        developerPanel.Style = (Style)FindResource("ProxyDeveloperPanel"); developerPanel.Margin = new Thickness(0, 7, 0, 0);
        System.Windows.Automation.AutomationProperties.SetName(developerPanel, "Expand installed developer tools");
        var developerContent = new StackPanel(); developerPanel.Content = developerContent; developer.Children.Add(developerPanel);
        loading.FontSize = 11; loading.TextWrapping = TextWrapping.Wrap; loading.SetResourceReference(TextBlock.ForegroundProperty, "MutedForegroundBrush"); developerContent.Children.Add(loading);
        var controls = new WrapPanel { Margin = new Thickness(0, 6, 0, 6) };
        controls.Children.Add(Action("Select All", () => { foreach (var (_, card) in tools.Where(t => t.Tool.Provider != null)) card.IsChecked = true; UpdateCards(); }, track: false));
        controls.Children.Add(Action("Deselect All", () => { foreach (var (_, card) in tools) card.IsChecked = false; UpdateCards(); }, track: false));
        refresh = Action("Refresh", () => _ = Detect(true), track: false); controls.Children.Add(refresh);
        setToolProxy = Action("Set Proxy", () => Operate(false, toolsOnly: true));
        clearToolProxy = Action("Clear Proxy", () => Operate(true, toolsOnly: true));
        setToolProxy.ToolTip = clearToolProxy.ToolTip = "Only selected developer tools. Windows proxy settings remain unchanged.";
        controls.Children.Add(setToolProxy); controls.Children.Add(clearToolProxy); developerContent.Children.Add(controls);
        var toolConnectionHint = new TextBlock { Text = "Set Proxy uses Host / IP, Port and Protocol from Configuration scope above.", FontSize = 11, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 0, 0, 6) };
        toolConnectionHint.SetResourceReference(TextBlock.ForegroundProperty, "MutedForegroundBrush"); developerContent.Children.Add(toolConnectionHint);
        setToolProxy.IsEnabled = clearToolProxy.IsEnabled = false;
        var toolScroll = new ScrollViewer { MaxHeight = 245, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled, Content = cards };
        developerContent.Children.Add(toolScroll); toolScroll.SizeChanged += (_, _) => ResizeCards(toolScroll.ActualWidth - 12); body.Children.Add(Card(developer));
        var historySection = new Expander { Header = new TextBlock { Text = "Configuration History", FontSize = 15, FontWeight = FontWeights.SemiBold }, IsExpanded = openHistory, Style = (Style)FindResource("ProxyDeveloperPanel") };
        historyList.Style = (Style)FindResource("ProxyHistoryList"); historyList.Margin = new Thickness(0, 8, 0, 10);
        clearHistory = Action("Clear Snapshots\u2026", ClearHistory, track: false); restore = Action("Restore Selected Backup", Restore); restore.IsEnabled = false;
        var historyActions = new WrapPanel(); historyActions.Children.Add(restore); historyActions.Children.Add(clearHistory);
        var historyContent = new StackPanel(); historyContent.Children.Add(new TextBlock { Text = "Latest 10 verified snapshots. Restore the exact saved configuration.", TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 8, 0, 0) }); historyContent.Children.Add(historyList); historyContent.Children.Add(historyActions); historySection.Content = historyContent; body.Children.Add(Card(historySection));
        diagnostics.SetResourceReference(Control.BackgroundProperty, "DirectoryBadgeBrush"); diagnostics.SetResourceReference(Control.ForegroundProperty, "ChromeForegroundBrush");
        diagnostics.FontFamily = new FontFamily("Consolas"); diagnostics.FontSize = 11; diagnostics.Padding = new Thickness(10); diagnostics.Margin = new Thickness(0, 10, 0, 0); diagnostics.MaxHeight = 190;
        var footer = new Border { Padding = new Thickness(16, 6, 16, 6), BorderThickness = new Thickness(0, 1, 0, 0) }; footer.SetResourceReference(Border.BackgroundProperty, "TitleBarBrush"); footer.SetResourceReference(Border.BorderBrushProperty, "ToolbarBorderBrush"); Grid.SetRow(footer, 1); root.Children.Add(footer);
        var footerGrid = new Grid(); footerGrid.ColumnDefinitions.Add(new()); footerGrid.ColumnDefinitions.Add(new() { Width = GridLength.Auto }); footer.Child = footerGrid;
        notificationButton = CreateNotificationButton(); footerGrid.Children.Add(notificationButton);
        var actions = new WrapPanel { HorizontalAlignment = HorizontalAlignment.Right }; Grid.SetColumn(actions, 1); footerGrid.Children.Add(actions);
        setProxy = Action("Set All Proxies", () => Operate(false), true); clearProxy = Action("Clear All Proxies", () => Operate(true)); actions.Children.Add(setProxy); actions.Children.Add(clearProxy);
        historyList.SelectionChanged += (_, _) => restore.IsEnabled = !busy && detectionComplete && historyList.SelectedItem is ProxyHistoryItem;
        section.SelectionChanged += async (_, _) => await ChangeScope();
        UpdateProtocolChoices();
        Loaded += async (_, _) => { _ = Detect(false); _ = ReadHistory(); _ = RefreshStatus(); await ReadSelected(true); };
        Closing += (_, e) => { if (busy) { e.Cancel = true; result.Text = "Please wait until configuration verification finishes."; } };
        Closed += (_, _) => { closed = true; lifetime.Cancel(); discovery?.Cancel(); };
        PreviewKeyDown += (_, e) => { if (e.Key == System.Windows.Input.Key.Escape && !busy) { Close(); e.Handled = true; } };
        ProxyUiLanguage.Attach(this, () => diagnostics.Text = ProxyUiLanguage.Diagnostics(diagnosticSource));
    }
    private static Border Card(UIElement child)
    {
        var card = new Border { Child = child, CornerRadius = new CornerRadius(10), Padding = new Thickness(12), Margin = new Thickness(0, 0, 0, 10), BorderThickness = new Thickness(1) };
        card.SetResourceReference(Border.BackgroundProperty, "MenuBackgroundBrush"); card.SetResourceReference(Border.BorderBrushProperty, "ToolbarBorderBrush"); return card;
    }
    private static StackPanel SectionHeading(string title, string? description)
    {
        var heading = new StackPanel { Margin = new Thickness(0, 0, 0, 6) };
        heading.Children.Add(new TextBlock { Text = title, FontSize = 15, FontWeight = FontWeights.SemiBold });
        if (description != null)
        {
            var text = new TextBlock { Text = description, TextWrapping = TextWrapping.Wrap, FontSize = 12, Margin = new Thickness(0, 4, 0, 0) };
            text.SetResourceReference(TextBlock.ForegroundProperty, "MutedForegroundBrush"); heading.Children.Add(text);
        }
        return heading;
    }
    private FrameworkElement Field(string label, Control control)
    {
        var stack = new StackPanel { Margin = new Thickness(0, 4, 10, 4) };
        stack.Children.Add(new TextBlock { Text = label, Margin = new Thickness(0, 0, 0, 5) }); control.Padding = new Thickness(8, 6, 8, 6);
        System.Windows.Automation.AutomationProperties.SetName(control, label);
        control.SetResourceReference(Control.BackgroundProperty, "DirectoryBadgeBrush"); control.SetResourceReference(Control.ForegroundProperty, "ChromeForegroundBrush");
        stack.Children.Add(control); return stack;
    }
    private Button Action(string label, Action click, bool primary = false, bool track = true)
    {
        var button = new Button { Content = label, Padding = new Thickness(14, 8, 14, 8), Margin = new Thickness(0, 0, 8, 4) };
        button.SetResourceReference(Control.BackgroundProperty, primary ? "AccentBrush" : "DirectoryBadgeBrush");
        button.SetResourceReference(Control.ForegroundProperty, primary ? "TerminalBackgroundBrush" : "ChromeForegroundBrush");
        button.Click += (_, _) => click(); if (track) mutations.Add(button); return button;
    }
    private void UpdateScopeActions()
    {
        var all = section.SelectedIndex == 0;
        setProxy.Content = all ? "Set All Proxies" : "Set Proxy";
        clearProxy.Content = all ? "Clear All Proxies" : "Clear Proxy";
        automatic.Visibility = section.SelectedIndex >= 2 ? Visibility.Visible : Visibility.Collapsed;
        protocolNotice.Visibility = Visibility.Collapsed;
    }
    private List<IProxyProvider> SelectedProviders()
    {
        var providers = section.SelectedIndex == 0 ? systems.ToList() : [systems[section.SelectedIndex - 1]];
        if (section.SelectedIndex is 0 or 1) providers.Insert(1, machineEnvironment);
        if (optIn.IsChecked == true)
            providers.AddRange(tools.Where(t => t.Card.IsChecked == true && t.Tool.Provider != null).Select(t => t.Tool.Provider!));
        return providers.DistinctBy(p => p.Name).ToList();
    }
    private List<IProxyProvider> SelectedToolProviders() => optIn.IsChecked == true
        ? tools.Where(t => t.Card.IsChecked == true && t.Tool.Provider != null).Select(t => t.Tool.Provider!).DistinctBy(p => p.Name).ToList() : [];
    private async Task ReadSelected(bool loadEndpoint = true)
    {
        using var timing = StartupPerformance.Measure("ProxyManager.ReadConfiguration");
        var scope = section.SelectedIndex;
        if (scope < 0) return;
        var generation = ++readGeneration;
        var draftBeforeRead = CaptureDraft();
        try
        {
            var indices = scope == 0 ? new[] { 0, 1, 2 } : new[] { scope - 1 };
            var readings = await Task.Run(async () =>
            {
                var entries = new List<(int Index, ProxyState? State, string Text)>();
                foreach (var index in indices)
                {
                    try
                    {
                        var state = await systems[index].ReadAsync(lifetime.Token);
                        if (index == 0)
                        {
                            var machine = await machineEnvironment.ReadAsync(lifetime.Token);
                            state = new ProxyState(EnvironmentProxyProvider.Variables.ToDictionary(n => n, n => state[n] ?? machine[n]));
                        }
                        var text = index == 0 ? EnvironmentProxyProvider.Diagnostics() : string.Join("\n", state.Values.Select(v => v.Key + ": " + ProxyService.Redact(v.Value ?? "(unset)")));
                        entries.Add((index, state, text));
                    }
                    catch (Exception ex) { entries.Add((index, null, ProxyService.SafeError(ex))); }
                }
                return entries;
            });
            if (closed || section.SelectedIndex != scope || generation != readGeneration) return;
            diagnosticSource = string.Join("\n\n", readings.Select(r => systems[r.Index].Name + "\n" + r.Text));
            diagnostics.Text = ProxyUiLanguage.Diagnostics(diagnosticSource);
            automatic.Visibility = scope >= 2 ? Visibility.Visible : Visibility.Collapsed;
            // A slow read must not replace edits typed meanwhile or a restored per-scope draft.
            if (!loadEndpoint || CaptureDraft() != draftBeforeRead) return;
            var chosen = readings.FirstOrDefault(r => r.State != null && !string.IsNullOrWhiteSpace(ConnectionAddress(r.State, r.Index)));
            if (chosen.State == null) chosen = readings.FirstOrDefault(r => r.State != null);
            if (chosen.State is not { } current) return;
            var providerIndex = chosen.Index;
            if (scope >= 2)
            {
                manual.IsChecked = providerIndex == 1 ? (int.Parse(current["Flags"]!) & 2) != 0 : !string.IsNullOrEmpty(current["Proxy"]);
                autoDetect.IsChecked = providerIndex == 1 ? (int.Parse(current["Flags"]!) & 8) != 0 : current["AutoDetect"] == "true";
                pac.Text = current[providerIndex == 1 ? "Pac" : "AutoconfigUrl"] ?? "";
            }
            if (loadEndpoint)
            {
                host.Text = ""; port.Text = ""; bypass.Text = ""; protocolNotice.Visibility = Visibility.Collapsed;
                var address = ConnectionAddress(current, providerIndex);
                if (address != null && Uri.TryCreate(address.Contains("://") ? address : "http://" + address, UriKind.Absolute, out var uri) && string.IsNullOrEmpty(uri.UserInfo))
                {
                    var scheme = uri.Scheme.ToUpperInvariant();
                    if (protocol.Items.Contains(scheme)) { host.Text = uri.Host; port.Text = uri.Port > 0 ? uri.Port.ToString() : ""; protocol.SelectedItem = scheme; }
                    else { protocolNotice.Text = "The stored protocol is unavailable in this scope. Choose Environment to review it."; protocolNotice.Visibility = Visibility.Visible; }
                }
                var rules = current[providerIndex == 0 ? "NO_PROXY" : providerIndex == 1 ? "Bypass" : "ProxyBypass"];
                if (!string.IsNullOrWhiteSpace(rules)) bypass.Text = rules;
            }
        }
        catch (Exception ex) { if (!closed && section.SelectedIndex == scope && generation == readGeneration) { diagnosticSource = ProxyService.SafeError(ex); diagnostics.Text = ProxyUiLanguage.Diagnostics(diagnosticSource); } }
    }
    private static string? ConnectionAddress(ProxyState state, int index)
    {
        if (index == 0) return new[] { "HTTP_PROXY", "HTTPS_PROXY", "ALL_PROXY", "FTP_PROXY" }.Select(n => state[n]).FirstOrDefault(v => !string.IsNullOrWhiteSpace(v));
        var values = (state["Proxy"] ?? "").Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var value = values.FirstOrDefault(v => v.StartsWith("http=", StringComparison.OrdinalIgnoreCase)) ?? values.FirstOrDefault();
        if (value?.Contains('=') != true) return value;
        var separator = value.IndexOf('='); var target = value[..separator].ToLowerInvariant();
        return target is "http" or "https" or "ftp" ? value[(separator + 1)..] : null;
    }
    private ConnectionDraft CaptureDraft() => new(host.Text, port.Text, bypass.Text, protocol.SelectedItem?.ToString() ?? "HTTP", manual.IsChecked == true, autoDetect.IsChecked == true, pac.Text);
    private void RestoreDraft(ConnectionDraft draft)
    {
        host.Text = draft.Host; port.Text = draft.Port; bypass.Text = draft.Bypass;
        protocol.SelectedItem = section.SelectedIndex == 1 ? draft.Protocol : "HTTP";
        manual.IsChecked = draft.Manual; autoDetect.IsChecked = draft.AutoDetect; pac.Text = draft.Pac;
    }
    private void UpdateProtocolChoices()
    {
        var environment = section.SelectedIndex == 1;
        protocol.ItemsSource = environment ? new[] { "HTTP", "HTTPS", "SOCKS5" } : new[] { "HTTP" };
        protocol.SelectedIndex = 0; protocol.IsEnabled = environment;
        protocolHint.Text = environment
            ? "HTTP, HTTPS or SOCKS5 · User + Machine (UAC)"
            : section.SelectedIndex == 0
                ? "HTTP only · Environment requires UAC"
                : "HTTP only · Current user";
        automatic.Visibility = section.SelectedIndex >= 2 ? Visibility.Visible : Visibility.Collapsed;
    }
    private async Task ChangeScope()
    {
        if (section.SelectedIndex < 0) return;
        var previous = CaptureDraft(); connectionDrafts[editorScope] = previous;
        editorScope = section.SelectedIndex; UpdateScopeActions(); UpdateProtocolChoices();
        var saved = connectionDrafts.TryGetValue(editorScope, out var draft);
        RestoreDraft(draft ?? previous with { Protocol = "HTTP", Manual = true, AutoDetect = false, Pac = "" });
        if (editorScope != 1 && previous.Protocol != "HTTP")
        {
            protocolNotice.Text = "Your Environment protocol selection is preserved when you return to Environment.";
            protocolNotice.Visibility = Visibility.Visible;
        }
        if (IsLoaded) await ReadSelected(loadEndpoint: !saved);
    }
    private async Task Detect(bool force)
    {
        using var timing = StartupPerformance.Measure("ProxyManager.DetectTools");
        discovery?.Cancel(); discovery = CancellationTokenSource.CreateLinkedTokenSource(lifetime.Token); var token = discovery.Token;
        detectionComplete = false; SetBusy(busy);
        refresh.IsEnabled = false; loading.Text = "Discovering installed tools…";
        var selections = tools.Where(t => t.Card.IsChecked == true).Select(t => t.Tool.Name).ToHashSet(); tools.Clear(); cards.Children.Clear();
        try
        {
            await detection.DetectAsync(tool => Dispatcher.Invoke(() =>
            {
                if (closed || token.IsCancellationRequested) return;
                var previous = tools.FirstOrDefault(t => t.Tool.Name == tool.Name);
                var wasSelected = previous.Card?.IsChecked == true || selections.Contains(tool.Name);
                if (previous.Card != null) { cards.Children.Remove(previous.Card); tools.Remove(previous); }
                var card = new CheckBox {
                    ToolTip = tool.Status + "\n" + tool.Executable, Margin = new Thickness(3), Padding = new Thickness(10), MinHeight = 57, IsChecked = wasSelected };
                card.Style = (Style)FindResource("ProxyToolCard");
                ToolTipService.SetShowOnDisabled(card, true);
                UpdateToolCardContent(tool, card);
                card.Checked += (_, _) => UpdateCards(); card.Unchecked += (_, _) => UpdateCards();
                tools.Add((tool, card)); cards.Children.Add(card); ResizeCards(Math.Max(500, cards.ActualWidth)); UpdateCards();
            }), force, token);
            await Dispatcher.InvokeAsync(() => { if (!closed && !token.IsCancellationRequested) loading.Text = tools.Count == 0 ? "No supported installations found in PATH or known locations." : tools.Count + " installations found"; });
            if (!token.IsCancellationRequested) detectionComplete = true;
        }
        catch (OperationCanceledException) { }
        catch (Exception ex) { if (!closed) loading.Text = ProxyService.SafeError(ex); }
        finally { if (!closed && !token.IsCancellationRequested) { refresh.IsEnabled = true; SetBusy(busy); } }
    }
    private void ResizeCards(double width) { var columns = width >= 760 ? 4 : width >= 570 ? 3 : 2; foreach (var (_, c) in tools) c.Width = Math.Max(150, width / columns - 8); }
    private void UpdateCards()
    {
        foreach (var (tool, card) in tools) { card.IsEnabled = !busy && optIn.IsChecked == true && tool.Provider != null; card.Opacity = card.IsEnabled ? 1 : .65; }
        selected.Text = tools.Count(t => t.Card.IsChecked == true && t.Tool.Provider != null) + " selected";
        var canModify = !busy && detectionComplete && SelectedToolProviders().Count > 0;
        setToolProxy.IsEnabled = clearToolProxy.IsEnabled = canModify;
    }
    private void UpdateToolCardContent(DetectedProxyTool tool, CheckBox card)
    {
        var stack = new StackPanel();
        var name = new TextBlock { Text = "›_  " + tool.Name + " " + (tool.Capabilities?.Version ?? "…"), TextWrapping = TextWrapping.Wrap, Tag = "ProxyLiteral" };
        name.SetResourceReference(TextBlock.ForegroundProperty, "ChromeForegroundBrush"); stack.Children.Add(name);
        var scope = new TextBlock { Text = tool.Provider == null ? "Inherited / inspection" : "User settings", FontSize = 10, Margin = new Thickness(0, 2, 0, 0) };
        scope.SetResourceReference(TextBlock.ForegroundProperty, "MutedForegroundBrush"); stack.Children.Add(scope);
        if (tool.ProxyAddresses.Length > 0)
        {
            var blue = new SolidColorBrush(AppTheme.IsLight ? Color.FromRgb(0, 91, 173) : Color.FromRgb(118, 188, 255));
            stack.Children.Add(new TextBlock { Text = "Proxy", FontSize = 10, Foreground = blue, Margin = new Thickness(0, 4, 0, 0) });
            var address = new TextBlock { Text = string.Join("\n", tool.ProxyAddresses), FontSize = 11, Foreground = blue,
                TextWrapping = TextWrapping.Wrap, Tag = "ProxyLiteral", FlowDirection = FlowDirection.LeftToRight };
            stack.ToolTip = "Saved user configuration; environment or project overrides may still apply.";
            stack.Children.Add(address);
        }
        if (tool.ProxyReadFailed) stack.Children.Add(new TextBlock { Text = "Proxy status unavailable", FontSize = 10, TextWrapping = TextWrapping.Wrap });
        card.Content = new Border { Background = Brushes.Transparent, Child = stack };
    }
    private async Task RefreshToolProxyStatuses()
    {
        foreach (var entry in tools.ToArray())
        {
            var updated = await Task.Run(() => DeveloperProxySummary.ReadAsync(entry.Tool, lifetime.Token), lifetime.Token);
            if (closed) return;
            var index = tools.FindIndex(t => ReferenceEquals(t.Card, entry.Card));
            if (index < 0) continue;
            tools[index] = (updated, entry.Card); UpdateToolCardContent(updated, entry.Card);
        }
    }
    private bool ValidateConnection(ProxyEndpoint endpoint, bool clear)
    {
        connectionError.Visibility = Visibility.Collapsed;
        if (clear) return true;
        try { endpoint.Validate(); return true; }
        catch (ArgumentException ex)
        {
            connectionError.Text = ex.Message.StartsWith("Enter a host or IP", StringComparison.Ordinal)
                ? "Please enter a valid IP address or host and port first." : ex.Message;
            connectionError.Visibility = Visibility.Visible;
            connectionError.BringIntoView();
            var field = ex.Message.StartsWith("PAC", StringComparison.Ordinal) ? pac : ex.Message.StartsWith("Bypass", StringComparison.Ordinal) ? bypass :
                string.IsNullOrWhiteSpace(host.Text) ? host : endpoint.Port is < 1 or > 65535 ? port : host;
            field.Focus(); field.SelectAll();
            return false;
        }
    }
    private async void Operate(bool clear, bool toolsOnly = false)
    {
        if (busy || !detectionComplete) return;
        var endpoint = new ProxyEndpoint(protocol.SelectedItem?.ToString() ?? "HTTP", host.Text.Trim(), int.TryParse(port.Text.Trim(), out var n) ? n : 0, bypass.Text);
        var all = section.SelectedIndex == 0;
        if (!toolsOnly && !all && section.SelectedIndex >= 2) endpoint = endpoint with { ManualEnabled = manual.IsChecked == true, AutoDetect = autoDetect.IsChecked == true, Pac = pac.Text.Trim() };
        if (!ValidateConnection(endpoint, clear))
        {
            var field = System.Windows.Input.Keyboard.FocusedElement as TextBox;
            new ProxyInfoWindow("Check proxy connection", [], connectionError.Text)
                { Owner = this, Width = 480, Height = 220, MinHeight = 180 }.ShowDialog();
            field?.Focus();
            return;
        }
        try
        {
            var providers = toolsOnly ? SelectedToolProviders() : SelectedProviders();
            if (providers.Count == 0) return;
            if ((providers.Count > 1 || clear) && ProxyUiLanguage.Show(this, (clear ? "Clear persistent proxy configuration for the listed scopes (including their NO_PROXY or bypass/PAC/auto-detection settings):" : "Set persistent proxy configuration for:") + "\n\n" + string.Join("\n", providers.Select(p => p.Name)) + "\n\nAn encrypted recovery snapshot will be saved before changes." + (providers.Any(p => p is MachineEnvironmentProxyProvider) ? "\nAdministrator approval is required for Machine environment variables before any changes begin." : "") + (clear ? "\nUnselected scopes, project proxies and running processes may retain settings; removing bypass rules can affect those configurations." : ""), "Confirm proxy changes", MessageBoxButton.OKCancel, MessageBoxImage.Warning) != MessageBoxResult.OK) return;
            SetBusy(true); lastProviders = providers;
            var dialog = new ProxyOperationWindow(providers.Select(p => p.Name), clear, async updates =>
            {
                if (!clear) { endpoint.Validate(); foreach (var provider in providers) provider.Validate(endpoint); }
                var inventory = Inventory();
                return await WithMachinePermission(providers, () => service.ExecuteAsync(providers, endpoint, clear, lifetime.Token, inventory, toolsOnly ? clear ? "Clear developer tool proxies" : "Set developer tool proxies" : all ? clear ? "Clear All Proxies" : "Set All Proxies" : clear ? "Clear Proxy" : "Set Proxy", updates), updates);
            });
            dialog.ShowModal(this);
            result.Text = string.Join("\n", dialog.State.Rows.Select(r => r.DisplayName + ": " + r.Detail));
            if (dialog.State.Error != null) result.Text += "\n" + dialog.State.Error;
            await ReadSelected(loadEndpoint: !toolsOnly);
        }
        catch (Exception ex) { result.Text = ProxyService.SafeError(ex); }
        finally { try { await ReadHistory(); await RefreshStatus(); await RefreshToolProxyStatuses(); } finally { SetBusy(false); } }
    }
    private async void Restore()
    {
        if (busy || !detectionComplete || historyList.SelectedItem is not ProxyHistoryItem item) return;
        SetBusy(true);
        try
        {
            await Detect(true);
            if (!detectionComplete) throw new System.IO.IOException("Tool inventory could not be refreshed. Restore blocked.");
            var inventory = Inventory();
            var current = await Task.Run(async () =>
            {
                var values = new Dictionary<string, string>();
                foreach (var provider in inventory) values[provider.Name] = (await provider.ReadAsync(lifetime.Token)).Signature;
                return values;
            });
            var changed = item.Snapshot.Entries.Where(e => !current.TryGetValue(e.Provider.Name, out var state) || state != e.State.Signature).Select(e => e.Provider.Name).ToArray();
            if (ProxyUiLanguage.Show(this, "Restore exact saved configuration from " + item.Snapshot.Created.LocalDateTime.ToString("g") + "?\n\nChanged providers:\n" + string.Join("\n", changed) + "\n\nNewer values/files for these providers will be replaced. A complete backup of current settings will be created first.", "Confirm configuration restore", MessageBoxButton.OKCancel, MessageBoxImage.Warning) != MessageBoxResult.OK) return;
            var targets = inventory.Where(p => item.Snapshot.Entries.Any(e => e.Provider.Name == p.Name)).ToArray();
            var results = await Task.Run(() => WithMachinePermission(targets, () => service.RestoreHistoryAsync(item.Path, inventory, current, lifetime.Token)));
            result.Text = string.Join("\n", results.Select(r => (r.Success ? "✓ Restored " : "! " + r.Outcome + " ") + r.Provider + ": " + r.Message)); await ReadSelected();
        }
        catch (Exception ex) { result.Text = ProxyService.SafeError(ex); }
        finally { try { await ReadHistory(); await RefreshStatus(); await RefreshToolProxyStatuses(); } finally { SetBusy(false); } }
    }
    private List<IProxyProvider> Inventory()
    {
        var incomplete = tools.Where(t => t.Tool.Name is "Git" or "npm" or "pnpm" or "Python pip" or "Gradle" or "Maven" or "NuGet / .NET" or "Cargo").Where(t => t.Tool.Provider == null && t.Tool.BackupProvider == null).Select(t => t.Tool.Name).ToArray();
        if (incomplete.Length > 0) throw new NotSupportedException("Complete backup blocked: adapter version or configuration location could not be verified for " + string.Join(", ", incomplete) + ". No proxy configuration has been changed.");
        return systems.Concat<IProxyProvider>([machineEnvironment, new EnvironmentScopeInspectionProvider(EnvironmentVariableTarget.Process), new EnvironmentScopeInspectionProvider(EnvironmentVariableTarget.Machine), new WinHttpBasicInspectionProvider()])
            .Concat(tools.Select(t => t.Tool.Provider ?? t.Tool.BackupProvider).OfType<IProxyProvider>()).DistinctBy(p => p.Name).ToList();
    }
    private async Task<IReadOnlyList<ProxyResult>> WithMachinePermission(IEnumerable<IProxyProvider> providers, Func<Task<IReadOnlyList<ProxyResult>>> operation, IProgress<ProxyOperationUpdate>? progress = null)
    {
        if (!providers.Any(p => ReferenceEquals(p, machineEnvironment))) return await operation();
        WindowsInternetOptions.CheckPolicy();
        progress?.Report(new(null, Phase: "Requesting administrator approval for Machine environment. Cancelling stops this operation before any proxy changes."));
        var executable = Environment.ProcessPath ?? throw new System.IO.IOException("Application executable could not be located.");
        if (!System.IO.Path.GetFileName(executable).Equals("RtlTerminal.exe", StringComparison.OrdinalIgnoreCase))
            throw new NotSupportedException("Machine elevation requires the packaged RtlTerminal.exe. Run the preview executable rather than dotnet run.");
        await using var elevated = await MachineProxyElevation.OpenAsync(executable, lifetime.Token);
        machineEnvironment.Writer = elevated;
        try { return await operation(); }
        finally { machineEnvironment.Writer = null; }
    }
    private async Task ReadHistory()
    {
        using var timing = StartupPerformance.Measure("ProxyManager.ReadHistory");
        try
        {
            var items = await Task.Run(() => service.History.ListAsync(lifetime.Token));
            if (closed) return; historyList.ItemsSource = items.Take(10).ToList();
            if (System.IO.File.Exists(service.History.JournalPath) && string.IsNullOrEmpty(result.Text)) result.Text = "An interrupted or partially failed operation was recorded. Review Configuration History before changing settings.";
        }
        catch (Exception ex) { if (!closed) result.Text = ProxyService.SafeError(ex); }
    }
    private async void ClearHistory()
    {
        if (busy || ProxyUiLanguage.Show(this, "Delete all saved proxy snapshots and their recovery reminder?\n\nProxy settings will not change. Deleted snapshots cannot be restored.", "Clear proxy snapshots", MessageBoxButton.OKCancel, MessageBoxImage.Warning) != MessageBoxResult.OK) return;
        SetBusy(true);
        try { await Task.Run(() => service.History.ClearAsync(lifetime.Token)); result.Text = "Saved snapshots cleared. Proxy configuration was not changed."; await ReadHistory(); }
        catch (Exception ex) { result.Text = ProxyService.SafeError(ex); }
        finally { SetBusy(false); }
    }
    private void SetBusy(bool value)
    {
        busy = value; foreach (var b in mutations) b.IsEnabled = !value && detectionComplete;
        refresh.IsEnabled = !value && detectionComplete;
        clearHistory.IsEnabled = !value;
        restore.IsEnabled = !value && detectionComplete && historyList.SelectedItem is ProxyHistoryItem; section.IsEnabled = !value; optIn.IsEnabled = !value; UpdateCards();
        if (value) result.Text = "Saving and verifying configuration…";
    }
}

public partial class MainWindow
{
    private void ProxyManager_Click(object sender, RoutedEventArgs e) => new ProxyManagerWindow { Owner = this }.ShowDialog();
}
