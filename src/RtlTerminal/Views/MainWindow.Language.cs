using System.Windows;
using System.Windows.Controls;

namespace RtlTerminal;

public partial class MainWindow
{
    private void InitializeUiLanguage()
    {
        UiText.Changed += ApplyUiLanguage;
        ApplyUiLanguage();
    }
    private void ApplyUiLanguage()
    {
        EnglishLanguageItem.IsChecked = UiText.Current == UiLanguage.English;
        PersianLanguageItem.IsChecked = UiText.Current == UiLanguage.Persian;
        ArabicLanguageItem.IsChecked = UiText.Current == UiLanguage.Arabic;
        RefreshContextMenuIntegrationState();
        if (_macroRecorder is not null) SetMacroStatus(UiText.Format("● Recording · pane {0}", _macroPane?.Number));
        if (_updateCheckInProgress) CheckForUpdatesMenuItem.SetCurrentValue(MenuItem.HeaderProperty, UiText.Get("Checking for updates..."));
        if (ActiveDirectoryButton is not null) RefreshActiveDirectory();
        if (SearchBar is not null && SearchBar.Visibility == Visibility.Visible) UpdateSearch(preserveCurrent: true);
    }
    private void Language_Click(object sender, RoutedEventArgs e)
    {
        if (sender is MenuItem { Tag: string value } && Enum.TryParse<UiLanguage>(value, out var language)) UiText.Select(language);
    }
}
