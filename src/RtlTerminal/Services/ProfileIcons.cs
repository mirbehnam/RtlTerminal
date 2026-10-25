using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace RtlTerminal;

public enum ProfileIconKind { CommandPrompt, PowerShell, Linux, PowerShell7, GitBash }

public static class ProfileIcons
{
    private static readonly Dictionary<ProfileIconKind, ImageSource> Cache = [];
    public static ImageSource Get(ProfileIconKind profile)
    {
        lock (Cache)
        {
            if (Cache.TryGetValue(profile, out var cached)) return cached;
            // Shell file info may return a generic executable icon for MSIX
            // aliases or Git's bash.exe. These profiles have stable vector marks.
            if (profile is ProfileIconKind.PowerShell7 or ProfileIconKind.GitBash)
                return Cache[profile] = Fallback(profile);
            string? executable = profile switch
            {
                ProfileIconKind.PowerShell => Path.Combine(Environment.SystemDirectory, @"WindowsPowerShell\v1.0\powershell.exe"),
                ProfileIconKind.Linux => Path.Combine(Environment.SystemDirectory, "wsl.exe"),
                _ => Path.Combine(Environment.SystemDirectory, "cmd.exe")
            };
            var icon = (executable is not null ? ReadNativeIcon(executable) : null) ?? Fallback(profile);
            Cache[profile] = icon; return icon;
        }
    }
    private static ImageSource? ReadNativeIcon(string path)
    {
        if (!File.Exists(path)) return null;
        var info = new ShellFileInfo();
        if (SHGetFileInfo(path, 0, ref info, (uint)Marshal.SizeOf<ShellFileInfo>(), 0x100) == IntPtr.Zero || info.Icon == IntPtr.Zero)
            return null;
        try
        {
            var image = Imaging.CreateBitmapSourceFromHIcon(info.Icon, Int32Rect.Empty, BitmapSizeOptions.FromEmptyOptions());
            image.Freeze(); return image;
        }
        finally { DestroyIcon(info.Icon); }
    }
    public static ImageSource Fallback(ProfileIconKind kind)
    {
        var drawing = new DrawingGroup();
        void Shape(string geometry, Brush fill, Pen? pen = null) => drawing.Children.Add(new GeometryDrawing(fill, pen, Geometry.Parse(geometry)));
        if (kind == ProfileIconKind.Linux)
        {
            Shape("M8,0 C3,0 3,5 3,8 L1,13 L5,15 L11,15 L15,13 L13,8 C13,5 13,0 8,0 Z", Brushes.Black);
            Shape("M8,5 C4,5 4,9 4,12 C4,15 12,15 12,12 C12,9 12,5 8,5 Z", Brushes.White);
            Shape("M4,13 L0,15 L6,16 L8,14 L10,16 L16,15 L12,13 Z M5,5 L8,8 L11,5 L8,4 Z", Brushes.Goldenrod);
        }
        else if (kind == ProfileIconKind.PowerShell7)
        {
            Shape("M0,0 H16 V16 H0 Z", Brushes.Transparent);
            var blue = new LinearGradientBrush(Color.FromRgb(0, 120, 212), Color.FromRgb(1, 36, 86), 90);
            blue.Freeze();
            Shape("M3,1.5 H14 C15,1.5 15.5,2.2 15.2,3.3 L12.6,13.1 C12.4,14 11.9,14.5 11,14.5 H1.8 C.8,14.5 .4,13.8 .7,12.7 L3.2,3.1 C3.4,2.2 3.7,1.5 4.5,1.5 Z", blue);
            var white = new Pen(Brushes.White, 1.35) { StartLineCap = PenLineCap.Round, EndLineCap = PenLineCap.Round, LineJoin = PenLineJoin.Round };
            white.Freeze();
            Shape("M5,4.6 L8,7.5 L4.2,10.6 M8.1,11 H11.3", Brushes.Transparent, white);
        }
        else if (kind == ProfileIconKind.GitBash)
        {
            Shape("M0,0 H16 V16 H0 Z", Brushes.Transparent);
            var gitOrange = new SolidColorBrush(Color.FromRgb(240, 80, 50));
            gitOrange.Freeze();
            var branchPen = new Pen(Brushes.White, 1.2) { StartLineCap = PenLineCap.Round, EndLineCap = PenLineCap.Round, LineJoin = PenLineJoin.Round };
            branchPen.Freeze();
            Shape("M8,.7 Q8.4,.7 8.7,1 L15,7.3 Q15.7,8 15,8.7 L8.7,15 Q8,15.7 7.3,15 L1,8.7 Q.3,8 1,7.3 L7.3,1 Q7.6,.7 8,.7 Z", gitOrange);
            Shape("M4.8,4.8 L10.8,10.8 M7.8,7.8 L7.8,11.3", Brushes.Transparent, branchPen);
            drawing.Children.Add(new GeometryDrawing(Brushes.White, null, new EllipseGeometry(new Point(4.8, 4.8), 1.15, 1.15)));
            drawing.Children.Add(new GeometryDrawing(Brushes.White, null, new EllipseGeometry(new Point(10.8, 10.8), 1.15, 1.15)));
            drawing.Children.Add(new GeometryDrawing(Brushes.White, null, new EllipseGeometry(new Point(7.8, 11.3), 1.15, 1.15)));
        }
        else
        {
            Shape("M1,2 L15,2 L15,14 L1,14 Z", kind == ProfileIconKind.PowerShell ? Brushes.RoyalBlue : Brushes.DimGray);
            Shape("M4,5 L7,8 L4,11 M8,11 L12,11", Brushes.Transparent, new Pen(Brushes.White, 1.4));
        }
        drawing.Freeze(); var result = new DrawingImage(drawing); result.Freeze(); return result;
    }
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct ShellFileInfo
    {
        public IntPtr Icon;
        public int Index;
        public uint Attributes;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)] public string DisplayName;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 80)] public string TypeName;
    }
    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    private static extern IntPtr SHGetFileInfo(string path, uint attributes, ref ShellFileInfo info, uint size, uint flags);
    [DllImport("user32.dll")] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool DestroyIcon(IntPtr icon);
}
