using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace RtlTerminal;

public sealed partial class QuickInputWindow
{
    private void InitializeFluentTheme()
    {
        // Explicit window-local dictionary: no global ThemeMode, backdrop or proxy styles.
        // Match the terminal's chosen light/dark mode, independently of Windows app mode.
        var mode = AppTheme.IsLight ? "Light" : "Dark";
        Resources.MergedDictionaries.Add(new ResourceDictionary
        {
            Source = new Uri($"pack://application:,,,/PresentationFramework.Fluent;component/Themes/Fluent.{mode}.xaml")
        });
        // Translucent brush layers keep text and controls fully opaque. No backdrop
        // effect or per-frame blur is needed for this lightweight layered window.
        var surface = ((SolidColorBrush)FindResource("MenuBackgroundBrush")).Color;
        var top = Color.FromArgb(AppTheme.IsLight ? (byte)238 : (byte)224, surface.R, surface.G, surface.B);
        var bottom = Color.FromArgb(AppTheme.IsLight ? (byte)226 : (byte)212, surface.R, surface.G, surface.B);
        var glass = new LinearGradientBrush(top, bottom, new Point(0, 0), new Point(0, 1));
        glass.Freeze(); Resources["QuickInputGlassBrush"] = glass;
        var editor = ((SolidColorBrush)FindResource("DirectoryBadgeBrush")).Color;
        var tint = new SolidColorBrush(Color.FromArgb(AppTheme.IsLight ? (byte)80 : (byte)96, editor.R, editor.G, editor.B));
        tint.Freeze(); Resources["QuickInputEditorGlassBrush"] = tint;
        draft.Editor.BorderThickness = new Thickness(1);
    }
}
