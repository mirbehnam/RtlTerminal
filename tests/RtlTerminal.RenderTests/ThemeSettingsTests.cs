using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using RtlTerminal;

internal static partial class Program
{
    private static void CheckThemeSettingsPreview()
    {
        var originalTheme = AppTheme.Current; var originalScheme = AppTheme.Scheme;
        var savedTheme = AppSettings.LoadTheme(); var savedScheme = AppSettings.LoadColorScheme();
        var settings=new FontSettingsWindow("Consolas",14,FontWeights.Normal,FontStyles.Normal,2000);
        settings.ShowThemeTab();
        var list=(ListBox)settings.FindName("ThemeListBox");
        var border=(Border)settings.FindName("ThemePreviewBorder");
        var root=(Panel)settings.Content;root.Background=settings.Background;
        if(((TabControl)settings.FindName("SettingsTabs")).SelectedItem!=settings.FindName("ThemeSettingsTab") || list.Items.Count!=ColorScheme.All.Count+2)
            throw new Exception("Theme settings did not open the unified theme list");
        foreach(var option in ThemeOption.All)
        {
            list.SelectedItem=option;
            if(((SolidColorBrush)border.Background).Color != Color.FromRgb(option.Background.Red,option.Background.Green,option.Background.Blue))
                throw new Exception("Theme preview does not follow list selection");
            if(AppTheme.Current!=originalTheme || AppTheme.Scheme!=originalScheme || AppSettings.LoadTheme()!=savedTheme || AppSettings.LoadColorScheme()!=savedScheme)
                throw new Exception("Browsing themes changed the live application or saved settings before Apply");
        }
        foreach(var name in new[]{"Dracula","Gruvbox Light"})
        {
            list.SelectedItem=ThemeOption.All.Single(t=>t.Name==name);
            root.Measure(new Size(860,660));root.Arrange(new Rect(0,0,860,660));root.UpdateLayout();
            var bitmap=new RenderTargetBitmap(860,660,96,96,PixelFormats.Pbgra32);bitmap.Render(root);
            var png=new PngBitmapEncoder();png.Frames.Add(BitmapFrame.Create(bitmap));
            using var stream=File.Create("artifacts/render-checks/theme-settings-"+name.Replace(' ','-')+".png");png.Save(stream);
        }
        settings.Close();
        if(AppTheme.Current!=originalTheme || AppTheme.Scheme!=originalScheme)throw new Exception("Canceling theme settings applied the preview");
        var main=new MainWindow();
        if(main.FindName("DarkModeButton") is not null || main.FindName("DarkSchemesMenuItem") is not null || main.FindName("LightSchemesMenuItem") is not null || main.FindName("DarkThemeMenuItem") is not null)
            throw new Exception("Old duplicate theme submenus remain");
        main.Close();
        Console.WriteLine("PASS unified theme settings, isolated selection preview/cancel, theme menu access and removed redundant controls");
    }
}
