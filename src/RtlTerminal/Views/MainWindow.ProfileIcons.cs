using System.Windows;
using System.Windows.Controls;

namespace RtlTerminal;

public partial class MainWindow
{
    private static Image CreateProfileIcon(TerminalProfile profile) => new()
    {
        Source = ProfileIcons.Get(profile switch
        {
            TerminalProfile.PowerShell7 => ProfileIconKind.PowerShell7,
            TerminalProfile.PowerShell => ProfileIconKind.PowerShell,
            TerminalProfile.Wsl => ProfileIconKind.Linux,
            TerminalProfile.GitBash => ProfileIconKind.GitBash,
            _ => ProfileIconKind.CommandPrompt
        }),
        Width = 16, Height = 16, VerticalAlignment = VerticalAlignment.Center,
        SnapsToDevicePixels = true, IsHitTestVisible = false
    };
}
