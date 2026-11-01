using RtlTerminal.Proxy;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Effects;
using System.Windows.Threading;

namespace RtlTerminal;

public sealed class ProxyOperationWindow : Window
{
    public ProxyOperationState State { get; }
    public IReadOnlyList<ProxyResult> Results { get; private set; } = [];
    private readonly bool clear;
    private readonly Func<IProgress<ProxyOperationUpdate>, Task<IReadOnlyList<ProxyResult>>> execute;
    private readonly TextBlock title = new() { FontSize = 21, FontWeight = FontWeights.SemiBold, TextWrapping = TextWrapping.Wrap };
    private readonly TextBlock subtitle = new() { TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 7, 0, 16) };
    private readonly TextBlock counts = new() { Margin = new Thickness(0, 7, 0, 0), TextWrapping = TextWrapping.Wrap };
    private readonly TextBlock finishIcon = new() { FontSize = 36, Visibility = Visibility.Collapsed, Margin = new Thickness(0, 0, 0, 8) };
    private readonly ProgressBar progress = new() { Height = 5, Minimum = 0, Maximum = 100 };
    private readonly Button ok = new() { Content = "OK", MinWidth = 92, Padding = new Thickness(16, 7, 16, 7), HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 16, 0, 0), IsDefault = true, Visibility = Visibility.Collapsed };
    private readonly Expander details = new() { Header = "Operation details", Visibility = Visibility.Collapsed, Margin = new Thickness(0, 12, 0, 0) };
    private readonly TextBlock detailText = new() { TextWrapping = TextWrapping.Wrap, MaxWidth = 440 };
    private readonly TextBlock restartNotice = new()
    {
        Text = "You may need to close and reopen the affected application for proxy changes to take effect.",
        TextWrapping = TextWrapping.Wrap,
        FlowDirection = FlowDirection.LeftToRight,
        TextAlignment = TextAlignment.Left,
        FontSize = 12,
        Margin = new Thickness(0, 14, 0, 0),
        Visibility = Visibility.Collapsed
    };
    private readonly System.Text.StringBuilder diagnosticLog = new();
    private readonly Button viewLog = new() { Content = "View Full Log…", Padding = new Thickness(12, 7, 12, 7), Visibility = Visibility.Collapsed, Margin = new Thickness(0, 12, 0, 0), HorizontalAlignment = HorizontalAlignment.Left };
    public string FullLog => ProxyDiagnostics.Sanitize(diagnosticLog.ToString());
    private readonly Dictionary<ProxyOperationRow, (TextBlock Icon, TextBlock Label)> rowControls = [];
    private bool started;

    public ProxyOperationWindow(IEnumerable<string> providers, bool clear,
        Func<IProgress<ProxyOperationUpdate>, Task<IReadOnlyList<ProxyResult>>> execute)
    {
        this.clear = clear; this.execute = execute; State = new(providers);
        Log($"Operation: {(clear ? "Clear" : "Set")}; app {AppVersion.Display}; OS {Environment.OSVersion}; process {System.Runtime.InteropServices.RuntimeInformation.ProcessArchitecture}");
        Log("Selected providers: " + string.Join(", ", State.Rows.Select(r => r.Provider)));
        Title = clear ? "Clearing Proxy Settings" : "Applying Proxy Settings";
        Width = 530; SizeToContent = SizeToContent.Height;
        MaxHeight = Math.Max(360, SystemParameters.WorkArea.Height - 50);
        WindowStartupLocation = WindowStartupLocation.CenterOwner; ShowInTaskbar = false;
        WindowStyle = WindowStyle.None; ResizeMode = ResizeMode.NoResize; AllowsTransparency = true; Background = Brushes.Transparent;
        FontFamily = new FontFamily("Segoe UI"); FontSize = 13;
        SetResourceReference(ForegroundProperty, "ChromeForegroundBrush");
        var frame = new Border { CornerRadius = new CornerRadius(12), BorderThickness = new Thickness(1), Padding = new Thickness(23), Margin = new Thickness(10),
            Effect = new DropShadowEffect { BlurRadius = 16, ShadowDepth = 2, Opacity = .25 } };
        frame.SetResourceReference(Border.BackgroundProperty, "MenuBackgroundBrush"); frame.SetResourceReference(Border.BorderBrushProperty, "ToolbarBorderBrush");
        var body = new StackPanel(); frame.Child = new ScrollViewer { Content = body, VerticalScrollBarVisibility = ScrollBarVisibility.Auto }; Content = frame;
        title.Text = Title; subtitle.Text = "A recovery snapshot is verified before any configuration is changed.";
        body.Children.Add(finishIcon); body.Children.Add(title); body.Children.Add(subtitle);
        var rows = new StackPanel();
        foreach (var row in State.Rows)
        {
            var icon = new TextBlock { Text = "○", FontSize = 21, Width = 30, VerticalAlignment = VerticalAlignment.Center, TextAlignment = TextAlignment.Center, RenderTransformOrigin = new Point(.5, .5) };
            icon.SetResourceReference(TextBlock.ForegroundProperty, "MutedForegroundBrush");
            var label = new TextBlock { Text = "Pending", FontSize = 11, Margin = new Thickness(0, 3, 0, 0) }; label.SetResourceReference(TextBlock.ForegroundProperty, "MutedForegroundBrush");
            var text = new StackPanel(); text.Children.Add(new TextBlock { Text = row.DisplayName, FontWeight = FontWeights.SemiBold }); text.Children.Add(label);
            var grid = new Grid { Margin = new Thickness(0, 7, 0, 7) }; grid.ColumnDefinitions.Add(new() { Width = new GridLength(40) }); grid.ColumnDefinitions.Add(new());
            grid.Children.Add(icon); Grid.SetColumn(text, 1); grid.Children.Add(text); rows.Children.Add(grid);
            rowControls[row] = (icon, label); row.PropertyChanged += (_, _) => RenderRow(row);
        }
        body.Children.Add(new ScrollViewer { Content = rows, MaxHeight = Math.Min(290, MaxHeight * .4), VerticalScrollBarVisibility = ScrollBarVisibility.Auto });
        var progressGroup = new StackPanel { Margin = new Thickness(0, 14, 0, 0) }; progressGroup.Children.Add(progress); progressGroup.Children.Add(counts); body.Children.Add(progressGroup);
        progress.SetResourceReference(Control.ForegroundProperty, "AccentBrush");
        details.SetResourceReference(Control.ForegroundProperty, "ChromeForegroundBrush");
        var detailsHeader = new TextBlock { Text = "Operation details" }; detailsHeader.SetResourceReference(TextBlock.ForegroundProperty, "ChromeForegroundBrush"); details.Header = detailsHeader;
        detailText.SetResourceReference(TextBlock.ForegroundProperty, "ChromeForegroundBrush");
        details.Content = new ScrollViewer { MaxHeight = 140, Content = detailText, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
        body.Children.Add(details); body.Children.Add(viewLog); body.Children.Add(ok); body.Children.Add(restartNotice);
        restartNotice.Foreground = new SolidColorBrush(AppTheme.IsLight ? Color.FromRgb(196, 43, 28) : Color.FromRgb(255, 153, 164));
        viewLog.SetResourceReference(Control.BackgroundProperty, "ToolbarBrush"); viewLog.SetResourceReference(Control.ForegroundProperty, "ChromeForegroundBrush");
        viewLog.Click += (_, _) => new ProxyLogWindow(FullLog) { Owner = this }.ShowDialog();
        ok.SetResourceReference(Control.BackgroundProperty, "AccentBrush"); ok.SetResourceReference(Control.ForegroundProperty, "TerminalBackgroundBrush");
        ok.Click += (_, _) => Close();
        State.PropertyChanged += (_, _) => RenderOverall(); RenderOverall();
        Closing += (_, e) => { if (started && !State.IsComplete) e.Cancel = true; };
        PreviewKeyDown += (_, e) =>
        {
            if (e.Key == System.Windows.Input.Key.Escape) { if (State.IsComplete) Close(); e.Handled = true; }
        };
        Loaded += async (_, _) =>
        {
            if (started) return; started = true;
            FadeScale(frame, scale: false);
            await Dispatcher.Yield(DispatcherPriority.Render);
            await ExecuteAsync();
        };
        ProxyUiLanguage.Attach(this);
    }
    private async Task ExecuteAsync()
    {
        try
        {
            var updates = new DispatcherProgress(this);
            Results = await Task.Run(() => execute(updates));
            Log("Execution returned. Finalizing displayed results.");
            State.Complete(Results);
        }
        catch (Exception ex) { Log(ProxyDiagnostics.Describe(ex)); State.Complete(Results, ProxyService.SafeError(ex)); }
        ok.Focus();
    }
    private sealed class DispatcherProgress(ProxyOperationWindow window) : IProgress<ProxyOperationUpdate>
    {
        public void Report(ProxyOperationUpdate update) => window.Dispatcher.Invoke(() =>
        {
            window.Log(update.Diagnostic ?? update.Phase ?? (update.Provider + ": " + (update.Result == null ? "Starting mutation / verification" : update.Result.Outcome + " · " + update.Result.Message)));
            window.State.Report(update);
        });
    }
    private void Log(string message) => diagnosticLog.Append('[').Append(DateTimeOffset.Now.ToString("O")).Append("] ").AppendLine(ProxyDiagnostics.Sanitize(message));
    private void RenderRow(ProxyOperationRow row)
    {
        var (icon, label) = rowControls[row];
        if (icon.RenderTransform is RotateTransform previousRotation) previousRotation.BeginAnimation(RotateTransform.AngleProperty, null);
        icon.BeginAnimation(OpacityProperty, null); icon.RenderTransform = Transform.Identity;
        icon.Foreground = row.Status switch
        {
            ProxyRowStatus.Success => new SolidColorBrush(AppTheme.IsLight ? Color.FromRgb(16, 124, 16) : Color.FromRgb(108, 203, 95)),
            ProxyRowStatus.Failed => new SolidColorBrush(AppTheme.IsLight ? Color.FromRgb(196, 43, 28) : Color.FromRgb(255, 153, 164)),
            ProxyRowStatus.Skipped => new SolidColorBrush(AppTheme.IsLight ? Color.FromRgb(132, 92, 0) : Color.FromRgb(252, 225, 0)),
            _ => (Brush)FindResource("MutedForegroundBrush")
        };
        icon.Text = row.Status switch { ProxyRowStatus.Pending => "○", ProxyRowStatus.Running => "◌", ProxyRowStatus.Success => "✓", ProxyRowStatus.Failed => "!", _ => "−" };
        label.Text = row.Status switch { ProxyRowStatus.Pending => "Pending", ProxyRowStatus.Running => clear ? "Clearing and verifying…" : "Applying and verifying…", ProxyRowStatus.Success => "Verified · managed settings", ProxyRowStatus.Failed => "Failed · see details", _ => "Skipped · see details" };
        label.ToolTip = row.Detail;
        System.Windows.Automation.AutomationProperties.SetName(icon, row.DisplayName + ": " + label.Text);
        if (row.Status == ProxyRowStatus.Running)
        {
            var rotation = new RotateTransform(); icon.RenderTransform = rotation;
            rotation.BeginAnimation(RotateTransform.AngleProperty, new DoubleAnimation(0, 360, TimeSpan.FromMilliseconds(900)) { RepeatBehavior = RepeatBehavior.Forever });
        }
        else if (row.Status == ProxyRowStatus.Success) FadeScale(icon);
    }
    private void RenderOverall()
    {
        var from = progress.Value;
        progress.BeginAnimation(RangeBaseValueProperty, null);
        var value = State.Completed * 100d / State.Rows.Count;
        progress.Value = value;
        if (SystemParameters.ClientAreaAnimation && from != value)
            progress.BeginAnimation(RangeBaseValueProperty, new DoubleAnimation(from, value, TimeSpan.FromMilliseconds(140)));
        counts.Text = $"{State.Completed} / {State.Rows.Count} processed · {State.Succeeded} verified · {State.Failed} failed · {State.Skipped} skipped";
        if (!State.IsComplete) { subtitle.Text = State.Phase; return; }
        finishIcon.Visibility = Visibility.Visible;
        finishIcon.Text = State.AllSucceeded ? "✓" : "!";
        finishIcon.Foreground = new SolidColorBrush(State.AllSucceeded ? AppTheme.IsLight ? Color.FromRgb(16, 124, 16) : Color.FromRgb(108, 203, 95) : AppTheme.IsLight ? Color.FromRgb(196, 43, 28) : Color.FromRgb(255, 153, 164));
        title.Text = State.AllSucceeded ? clear ? "Selected Proxy Settings Cleared!" : "Selected Proxies Applied!" : clear ? "Some Proxy Settings Could Not Be Cleared" : "Some Proxy Settings Could Not Be Applied";
        subtitle.Text = State.AllSucceeded ? (clear ? "All selected managed configurations were cleared and verified. " : "All selected managed configurations were updated and verified. ") + "Existing processes and out-of-scope overrides may retain other settings."
            : "Review the results below. Configuration History is available for recovery; no automatic retry was attempted.";
        detailText.Text = string.Join("\n\n", State.Rows.Select(r => r.DisplayName + ": " + r.Detail)) + (State.Error == null ? "" : "\n\n" + State.Error);
        details.Visibility = Visibility.Visible; viewLog.Visibility = Visibility.Visible; ok.Visibility = Visibility.Visible;
        restartNotice.Visibility = State.Succeeded > 0 || State.Failed > 0 ? Visibility.Visible : Visibility.Collapsed;
        FadeScale(finishIcon);
    }
    private static readonly DependencyProperty RangeBaseValueProperty = System.Windows.Controls.Primitives.RangeBase.ValueProperty;
    private static void FadeScale(UIElement element, bool scale = true)
    {
        if (!SystemParameters.ClientAreaAnimation) return;
        var duration = TimeSpan.FromMilliseconds(150);
        element.BeginAnimation(OpacityProperty, new DoubleAnimation(0, 1, duration));
        if (!scale) return;
        var transform = new ScaleTransform(1, 1); element.RenderTransform = transform;
        transform.BeginAnimation(ScaleTransform.ScaleXProperty, new DoubleAnimation(.75, 1, duration));
        transform.BeginAnimation(ScaleTransform.ScaleYProperty, new DoubleAnimation(.75, 1, duration));
    }
    public bool? ShowModal(Window owner)
    {
        Owner = owner;
        var target = owner.Content as UIElement;
        var layer = target == null ? null : AdornerLayer.GetAdornerLayer(target);
        var dim = target == null ? null : new DimAdorner(target);
        if (layer != null && dim != null) layer.Add(dim);
        try { return ShowDialog(); }
        finally { if (layer != null && dim != null) layer.Remove(dim); }
    }
    private sealed class DimAdorner(UIElement target) : Adorner(target)
    {
        protected override void OnRender(DrawingContext dc) => dc.DrawRectangle(new SolidColorBrush(Color.FromArgb(65, 0, 0, 0)), null, new Rect(AdornedElement.RenderSize));
    }
}
