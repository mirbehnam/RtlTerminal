using System.IO;
using RtlTerminal;

internal static partial class Program
{
    private static void CheckStartupDirectories()
    {
        var root = Path.Combine(Path.GetTempPath(), "rtl-startup-" + Guid.NewGuid().ToString("N"));
        var app = Path.Combine(root, "Rtl Terminal");
        var child = Path.Combine(app, "ConPty");
        var sibling = Path.Combine(root, "Rtl Terminal Projects");
        var home = Path.Combine(root, "User");
        foreach (var directory in new[] { app, child, sibling, home }) Directory.CreateDirectory(directory);
        try
        {
            void Expect(string expected, string? requested, string? argument, string cwd)
            {
                var actual = StartupDirectories.Resolve(requested, argument, cwd, app, home);
                if (actual != expected) throw new Exception($"Startup directory: expected '{expected}', got '{actual}'.");
            }
            Expect(home, null, null, app);
            Expect(home, null, null, child);
            Expect(sibling, null, null, sibling);
            Expect(app, app, null, home); // Explicit folder choices must win, including the installation itself.
            Expect(sibling, null, sibling, app);
            Expect(home, Path.Combine(root, "missing"), null, app);
            Expect(home, null, null, Path.Combine(root, "missing"));
        }
        finally { Directory.Delete(root, recursive: true); }
    }
}
