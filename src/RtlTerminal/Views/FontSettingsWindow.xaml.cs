using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace RtlTerminal;

public partial class FontSettingsWindow : Window
{
    private static readonly string[] RecommendedFontNames =
    [
        "JetBrains Mono",
        "Cascadia Mono",
        "Fira Code",
        "Iosevka",
        "Hack",
        "Geist Mono",
        "Vazir Code",
        "Cascadia Mono NF",
        "JetBrains Mono NF",
        "Cascadia Code",
        "Segoe UI Variable Text",
        "Cairo",
        "Almarai",
        "Consolas",
        "Lucida Console",
        "Courier New",
        "Source Code Pro",
        "IBM Plex Mono",
        "DejaVu Sans Mono",
        "Ubuntu Mono",
        "MesloLGS NF",
        "CaskaydiaCove Nerd Font"
    ];

    private readonly IReadOnlyList<string> _fontFamilies;
    private readonly string _initialFamily;
    private bool _synchronizingSelection;

    public FontSettingsWindow(
        string family,
        double size,
        FontWeight weight,
        FontStyle style,
        int historySize,
        HtmlPreviewScope previewScope = HtmlPreviewScope.CurrentPage,
        PaneShortcutSettings? shortcuts = null)
    {
        InitializeComponent();
        InitializeMacroSettings();
        InitializeDirectoryHistorySettings();
        _fontFamilies = BundledFonts.Names.Concat(Fonts.SystemFontFamilies
            .Select(font => font.Source)
            .Select(BundledFonts.CanonicalName))
            .Where(name => !BundledFonts.IsExcluded(name))
            .Distinct(StringComparer.CurrentCultureIgnoreCase)
            .OrderBy(name => name, StringComparer.CurrentCultureIgnoreCase)
            .ToArray();
        _initialFamily = ResolveInitialFamily(family);

        StandardFontListBox.ItemsSource = RecommendedFontNames
            .Where(IsFontAvailable)
            .ToArray();

        FontSizeComboBox.Text = size.ToString(
            CultureInfo.CurrentCulture);
        BoldCheckBox.IsChecked = weight >= FontWeights.Bold;
        ItalicCheckBox.IsChecked = style == FontStyles.Italic;
        HistorySizeComboBox.SelectedIndex = historySize switch
        {
            5000 => 1,
            10000 => 2,
            _ => 0
        };
        shortcuts ??= new();
        SplitShortcutCheckBox.IsChecked = shortcuts.Split;
        NavigateShortcutCheckBox.IsChecked = shortcuts.Navigate;
        CloseShortcutCheckBox.IsChecked = shortcuts.Close;
        PreviewScopeComboBox.SelectedIndex = (int)previewScope;
        RefreshFontList();
        ThemeListBox.ItemsSource = ThemeOption.All;
        ThemeListBox.SelectedItem = ThemeOption.All.First(t => t.Theme == AppTheme.Current && t.Scheme == AppTheme.Scheme);
    }

    public ThemeOption SelectedTheme { get; private set; } = ThemeOption.All[0];

    public void ShowThemeTab()
    {
        SettingsTabs.SelectedItem = ThemeSettingsTab;
        Loaded += (_, _) => { ThemeListBox.Focus(); ThemeListBox.ScrollIntoView(ThemeListBox.SelectedItem); };
    }

    private void ThemeListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (ThemeListBox.SelectedItem is not ThemeOption option || ThemePreviewText is null) return;
        ThemeListBox.ScrollIntoView(option);
        // Preview uses local brushes only; it never changes AppTheme or saved settings.
        ThemePreviewBorder.Background = ThemeOption.Brush(option.Background);
        ThemePreviewHeading.Text = option.Name;
        ThemePreviewHeading.Foreground = ThemeOption.Brush(option.Foreground);
        ThemePreviewTitleBar.Background = new SolidColorBrush(option.Theme == AppThemeKind.Dark ? Color.FromRgb(48,49,52) : Color.FromRgb(230,233,238));
        ThemePreviewTitle.Foreground = new SolidColorBrush(option.Theme == AppThemeKind.Dark ? Colors.White : Colors.Black);
        ThemePreviewText.Inlines.Clear();
        void Line(string text, TerminalColor color)
        {
            ThemePreviewText.Inlines.Add(new System.Windows.Documents.Run(text) { Foreground = ThemeOption.Brush(color) });
            ThemePreviewText.Inlines.Add(new System.Windows.Documents.LineBreak());
        }
        Line("PS C:\\Projects> terminal", option.Foreground);
        Line("سلام! این یک پیش‌نمایش فارسی است.", option.Foreground);
        Line("English text and numbers 123", option.Foreground);
        Line("✓ Build completed successfully", option.Ansi(2));
        Line("! Warning: check configuration", option.Ansi(3));
        Line("× Error: example message", option.Ansi(1));
        Line("https://github.com/mirbehnam/RtlTerminal", option.Ansi(4));
        ThemePreviewPalette.Children.Clear();
        for (var i = 0; i < 16; i++)
            ThemePreviewPalette.Children.Add(new Border { Width = 22, Height = 22, Margin = new Thickness(0,0,4,4), CornerRadius = new CornerRadius(3), Background = ThemeOption.Brush(option.Ansi(i)), ToolTip = $"ANSI {i}" });
    }

    public TerminalFontSettings SelectedSettings { get; private set; }
    public PaneShortcutSettings SelectedPaneShortcuts { get; private set; } = new();
    public HtmlPreviewScope SelectedPreviewScope { get; private set; }
    public int SelectedHistorySize { get; private set; } = 2000;

    private void FontSearchTextBox_TextChanged(
        object sender,
        TextChangedEventArgs e)
    {
        RefreshFontList();
    }

    private void RefreshFontList()
    {
        var search = FontSearchTextBox.Text.Trim();
        var selectedFamily =
            FontListBox.SelectedItem as string ?? _initialFamily;

        FontListBox.ItemsSource = string.IsNullOrEmpty(search)
            ? _fontFamilies
            : _fontFamilies.Where(name =>
                name.Contains(
                    search,
                    StringComparison.CurrentCultureIgnoreCase));

        FontListBox.SelectedItem = selectedFamily;

        if (FontListBox.SelectedItem is null &&
            string.IsNullOrEmpty(search))
        {
            FontListBox.SelectedItem = _fontFamilies.FirstOrDefault();
        }

        FontListBox.ScrollIntoView(FontListBox.SelectedItem);
        SynchronizeStandardFontSelection(selectedFamily);
        UpdatePreview();
    }

    private void StandardFontListBox_SelectionChanged(
        object sender,
        SelectionChangedEventArgs e)
    {
        if (_synchronizingSelection ||
            StandardFontListBox.SelectedItem is not string family)
        {
            return;
        }

        _synchronizingSelection = true;
        FontListBox.SelectedItem = family;
        _synchronizingSelection = false;
        UpdatePreview();
    }

    private void FontListBox_SelectionChanged(
        object sender,
        SelectionChangedEventArgs e)
    {
        if (_synchronizingSelection)
            return;

        SynchronizeStandardFontSelection(
            FontListBox.SelectedItem as string);
        UpdatePreview();
    }

    private void FontOption_Changed(object sender, RoutedEventArgs e)
    {
        UpdatePreview();
    }

    private void UpdatePreview()
    {
        if (PreviewTextBlock is null)
            return;

        var family = GetSelectedFamily();
        PreviewTextBlock.FontFamily = BundledFonts.Resolve(family);
        PreviewTextBlock.FontSize = GetFontSize();
        PreviewTextBlock.FontWeight =
            BoldCheckBox.IsChecked == true
                ? FontWeights.Bold
                : FontWeights.Normal;
        PreviewTextBlock.FontStyle =
            ItalicCheckBox.IsChecked == true
                ? FontStyles.Italic
                : FontStyles.Normal;
    }

    private void ApplyButton_Click(object sender, RoutedEventArgs e)
    {
        if (!ValidateMacroSettings()) return;
        var family = GetSelectedFamily();

        if (string.IsNullOrWhiteSpace(family))
        {
            UiText.Show(
                this,
                "Please select a font.",
                "Rtl Terminal",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
            return;
        }

        SelectedSettings = new TerminalFontSettings(
            family,
            GetFontSize(),
            BoldCheckBox.IsChecked == true,
            ItalicCheckBox.IsChecked == true);
        SelectedTheme = (ThemeOption)ThemeListBox.SelectedItem;
        SelectedHistorySize = GetHistorySize();
        SelectedPreviewScope = (HtmlPreviewScope)Math.Max(0, PreviewScopeComboBox.SelectedIndex);
        SelectedPaneShortcuts = new(SplitShortcutCheckBox.IsChecked == true, NavigateShortcutCheckBox.IsChecked == true, CloseShortcutCheckBox.IsChecked == true);
        DialogResult = true;
    }

    private double GetFontSize()
    {
        return double.TryParse(
            FontSizeComboBox.Text,
            NumberStyles.Float,
            CultureInfo.CurrentCulture,
            out var size)
            ? Math.Clamp(size, 8, 72)
            : 14;
    }

    private int GetHistorySize()
    {
        return HistorySizeComboBox.SelectedItem is ComboBoxItem item &&
            int.TryParse(item.Tag?.ToString(), out var historySize)
                ? historySize
                : 2000;
    }

    private string GetSelectedFamily()
    {
        return StandardFontListBox.SelectedItem as string ??
            FontListBox.SelectedItem as string ??
            _initialFamily;
    }

    private void SynchronizeStandardFontSelection(string? family)
    {
        if (_synchronizingSelection)
            return;

        _synchronizingSelection = true;
        StandardFontListBox.SelectedItem =
            StandardFontListBox.Items
                .Cast<string>()
                .FirstOrDefault(name =>
                    string.Equals(
                        name,
                        family,
                        StringComparison.CurrentCultureIgnoreCase));
        _synchronizingSelection = false;
    }

    private bool IsFontAvailable(string fontName)
    {
        return _fontFamilies.Contains(
            fontName,
            StringComparer.CurrentCultureIgnoreCase);
    }

    private string ResolveInitialFamily(string family)
    {
        foreach (var candidate in family.Split(','))
        {
            var trimmed = candidate.Trim();

            if (IsFontAvailable(trimmed))
                return _fontFamilies.First(name =>
                    string.Equals(
                        name,
                        trimmed,
                        StringComparison.CurrentCultureIgnoreCase));
        }

        return _fontFamilies.FirstOrDefault(name =>
            string.Equals(
                name,
                "Cascadia Mono",
                StringComparison.CurrentCultureIgnoreCase)) ??
            _fontFamilies.FirstOrDefault(name =>
                string.Equals(
                    name,
                    "Consolas",
                    StringComparison.CurrentCultureIgnoreCase)) ??
            _fontFamilies.First();
    }
}
