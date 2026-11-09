using System.Windows;

namespace RtlTerminal;

public partial class MainWindow
{
    private static readonly HashSet<string> DeferredClipboardFiles = new(StringComparer.OrdinalIgnoreCase);
    private static readonly List<QuickInputImages> RetiredQuickImages = [];
    private bool OtherTerminalWindowsOpen() => Application.Current?.Windows.OfType<MainWindow>().Any(w => !ReferenceEquals(w, this) && !w._windowClosed) == true;
    private void RetireQuickInputImages()
    {
        RetiredQuickImages.Add(_quickImages);
        if (OtherTerminalWindowsOpen()) return;
        foreach (var images in RetiredQuickImages) images.Dispose();
        RetiredQuickImages.Clear();
    }
}
