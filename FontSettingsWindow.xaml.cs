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
        "Cascadia Mono",
        "Cascadia Code",
        "Consolas",
        "Lucida Console",
        "Courier New",
        "JetBrains Mono",
        "Fira Code",
        "Source Code Pro",
        "IBM Plex Mono",
        "DejaVu Sans Mono",
        "Ubuntu Mono",
        "Hack",
        "Iosevka",
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
        FontStyle style)
    {
        InitializeComponent();
        _fontFamilies = Fonts.SystemFontFamilies
            .Select(font => font.Source)
            .Distinct(StringComparer.CurrentCultureIgnoreCase)
            .OrderBy(name => name, StringComparer.CurrentCultureIgnoreCase)
            .ToArray();
        _initialFamily = ResolveInitialFamily(family);

        StandardFontListBox.ItemsSource = RecommendedFontNames
            .Where(IsFontInstalled)
            .ToArray();

        FontSizeComboBox.Text = size.ToString(
            CultureInfo.CurrentCulture);
        BoldCheckBox.IsChecked = weight >= FontWeights.Bold;
        ItalicCheckBox.IsChecked = style == FontStyles.Italic;
        RefreshFontList();
    }

    public TerminalFontSettings SelectedSettings { get; private set; }

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
        PreviewTextBlock.FontFamily = new FontFamily(family);
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
        var family = GetSelectedFamily();

        if (string.IsNullOrWhiteSpace(family))
        {
            MessageBox.Show(
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
            : 15;
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

    private bool IsFontInstalled(string fontName)
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

            if (IsFontInstalled(trimmed))
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
