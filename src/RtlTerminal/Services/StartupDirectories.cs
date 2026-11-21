using System.IO;

namespace RtlTerminal;

public enum StartupDirectoryMode { Default, LastDirectory, ChooseDirectory }

public static class StartupDirectories
{
    public static string? Resolve(string? requested, string? argument, string? currentDirectory, string applicationDirectory, string userDirectory)
    {
        if (Existing(requested) is { } explicitDirectory) return explicitDirectory;
        return ResolveLaunchDirectory(argument, currentDirectory, applicationDirectory) ?? Existing(userDirectory);
    }

    // The installer and Start menu can launch the app with the user's home as the working directory.
    public static string? ResolveLaunchDirectory(string? argument, string? currentDirectory, string applicationDirectory, string? userDirectory = null)
    {
        if (Existing(argument) is { } argumentDirectory) return argumentDirectory;
        if (Existing(currentDirectory) is not { } inherited || InsideApplication(inherited, applicationDirectory)) return null;
        return string.Equals(Path.TrimEndingDirectorySeparator(inherited),
            userDirectory is null ? null : Path.TrimEndingDirectorySeparator(userDirectory), StringComparison.OrdinalIgnoreCase) ? null : inherited;
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
