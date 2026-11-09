using System.Windows;

namespace RtlTerminal;

public partial class FontSettingsWindow
{
    private readonly TerminalBellService _bellPreview = new(new());
    public TerminalBellSettings SelectedBellSettings => new(BellEnabledCheckBox.IsChecked == true,
        BellBackgroundCheckBox.IsChecked == true,
        (TerminalBellSound)Math.Clamp(BellSoundComboBox.SelectedIndex, 0, 3));

    private void InitializeBellSettings()
    {
        var settings = AppSettings.LoadBellSettings();
        BellEnabledCheckBox.IsChecked = settings.Enabled;
        BellBackgroundCheckBox.IsChecked = settings.BackgroundOnly;
        BellSoundComboBox.SelectedIndex = (int)settings.Sound;
    }

    private void TestBell_Click(object sender, RoutedEventArgs e) =>
        _bellPreview.Notify(windowActive: true, preview: true, previewSound: SelectedBellSettings.Sound);
}
