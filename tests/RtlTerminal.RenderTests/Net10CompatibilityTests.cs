using System.Collections;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using RtlTerminal;

internal static partial class Program
{
    private static void CheckNet10Compatibility()
    {
        static void Check(bool ok, string message) { if (!ok) throw new Exception(".NET 10: " + message); }
        Check(Environment.Version.Major == 10, "tests must execute on the migrated runtime");
        // In-memory IDataObject exercises the same API as OLE paste/drop without replacing the user's clipboard.
        IDataObject data = new DataObject();
        const string text = "سلام English\nline 2";
        data.SetData(DataFormats.UnicodeText, text);
        Check(data.TryGetData<string>(DataFormats.UnicodeText, out var actual) && actual == text, "typed Unicode paste");
        data.SetData(QuickInputWindow.ClipboardFormat, "{\"Text\":\"hello\",\"Attachments\":{}}");
        Check(data.TryGetData<string>(QuickInputWindow.ClipboardFormat, out var json) && json.Contains("Attachments"), "custom JSON string remains compatible");
        Check(!data.TryGetData<string>("Missing.Format", out _), "missing format fails safely");
        data.SetData(DataFormats.FileDrop, new[] { @"C:\test folder\نمونه.txt" });
        Check(data.TryGetData<string[]>(DataFormats.FileDrop, out var files) && files.Length == 1, "typed file drop");
        var image = BitmapSource.Create(1, 1, 96, 96, PixelFormats.Bgra32, null, new byte[4], 4); image.Freeze();
        data.SetData(DataFormats.Bitmap, image);
        Check(data.TryGetData<BitmapSource>(DataFormats.Bitmap, out var bitmap) && bitmap.PixelWidth == 1, "typed bitmap paste");

        var variable = "RTLTERMINAL_TEST_" + Guid.NewGuid().ToString("N");
        try
        {
            Check(EnvironmentPaths.ReadOrDefault(variable, "fallback") == "fallback", "absent path fallback");
            Environment.SetEnvironmentVariable(variable, "");
            Check(Environment.GetEnvironmentVariable(variable) == "", "empty value is distinct from absence");
            Check(EnvironmentPaths.ReadOrDefault(variable, "fallback") == "fallback", "empty path cannot redirect config into working directory");
            Environment.SetEnvironmentVariable(variable, @"C:\tool home");
            Check(EnvironmentPaths.ReadOrDefault(variable, "fallback") == @"C:\tool home", "explicit path preserved");
            Environment.SetEnvironmentVariable(variable, null);
            Check(Environment.GetEnvironmentVariable(variable) is null, "null still removes environment variable");
        }
        finally { Environment.SetEnvironmentVariable(variable, null); }
        var inherited = new Hashtable { ["EMPTY_VALUE"] = "", ["HTTP_PROXY"] = "http://localhost:8080" };
        var child = TerminalEnvironment.Create(inherited);
        Check(child["EMPTY_VALUE"] == "" && child["HTTP_PROXY"] == "http://localhost:8080", "child shell inherits exact empty and proxy values");
        Console.WriteLine("PASS .NET 10 runtime, typed Unicode/JSON/file/bitmap clipboard, empty environment paths and exact child environment inheritance");
    }
}
