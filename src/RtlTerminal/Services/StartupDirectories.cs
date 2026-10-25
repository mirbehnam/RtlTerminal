using System.IO;

namespace RtlTerminal;

public static class StartupDirectories
{
    public static string? Resolve(string? requested, string? argument, string? currentDirectory, string applicationDirectory, string userDirectory)
    {
        if (Existing(requested) is { } explicitDirectory) return explicitDirectory;
        if (Existing(argument) is { } argumentDirectory) return argumentDirectory;
        if (Existing(currentDirectory) is { } inherited && !InsideApplication(inherited, applicationDirectory)) return inherited;
        return Existing(userDirectory);
    }

    private static bool InsideApplication(string directory, string applicationDirectory)
    {
        var app = Path.TrimEndingDirectorySeparator(Path.GetFullPath(applicationDirectory));
        var current = Path.TrimEndingDirectorySeparator(directory);
        return string.Equals(current, app, StringComparison.OrdinalIgnoreCase) ||
            current.StartsWith(Path.EndsInDirectorySeparator(app) ? app : app + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);
    }

    private static string? Existing(string? directory)
    {
        if (string.IsNullOrWhiteSpace(directory) || !Directory.Exists(directory)) return null;
        try { return Path.GetFullPath(directory); }
        catch (Exception e) when (e is ArgumentException or NotSupportedException or PathTooLongException) { return null; }
    }
}
