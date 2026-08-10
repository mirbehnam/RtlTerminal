using System.Windows;

namespace RtlTerminal;

public partial class UpdateAvailableWindow : Window
{
    public UpdateAvailableWindow(
        Version currentVersion,
        Version latestVersion)
    {
        InitializeComponent();
        VersionTextBlock.Text =
            $"Installed: {FormatVersion(currentVersion)}    " +
            $"Available: {FormatVersion(latestVersion)}";
    }

    public bool OpenUpdateRequested { get; private set; }
    public bool DontShowAgain => DontShowAgainCheckBox.IsChecked == true;

    private void OpenUpdateButton_Click(object sender, RoutedEventArgs e)
    {
        OpenUpdateRequested = true;
        DialogResult = true;
    }

    private void NotNowButton_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
    }

    private static string FormatVersion(Version version)
    {
        return version.Revision > 0
            ? version.ToString(4)
            : version.Build >= 0
                ? version.ToString(3)
                : version.ToString();
    }
}
