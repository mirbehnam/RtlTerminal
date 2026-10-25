using System.IO;
using System.Security;
using Microsoft.Win32;

namespace RtlTerminal;

public static class ShellProfiles
{
    private const string GitForWindowsKey = @"Software\GitForWindows";
    private const string PowerShellCoreKey = @"Software\Microsoft\PowerShellCore\InstalledVersions";
    private const string PowerShell7Key = @"Software\Microsoft\PowerShell\7";

    private static string? _powerShell7Path;
    private static bool _powerShell7Searched;
    private static string? _gitBashPath;
    private static bool _gitBashSearched;
    private static readonly object SyncLock = new();

    public static string? PowerShell7Path
    {
        get
        {
            lock (SyncLock)
            {
                if (!_powerShell7Searched)
                {
                    _powerShell7Path = FindPowerShell7();
                    _powerShell7Searched = true;
                }
                return _powerShell7Path;
            }
        }
    }

    public static bool IsPowerShell7Available => PowerShell7Path is not null;

    public static string? GitBashPath
    {
        get
        {
            lock (SyncLock)
            {
                if (!_gitBashSearched)
                {
                    _gitBashPath = FindGitBash();
                    _gitBashSearched = true;
                }
                return _gitBashPath;
            }
        }
    }

    public static bool IsGitBashAvailable => GitBashPath is not null;

    internal static void ResetSearch()
    {
        lock (SyncLock)
        {
            _powerShell7Searched = false;
            _powerShell7Path = null;
            _gitBashSearched = false;
            _gitBashPath = null;
        }
    }

    public static string? FindPowerShell7(
        Func<string, bool>? fileExists = null,
        Func<string?>? getPathEnv = null,
        RegistryKey? mockRoot = null)
    {
        fileExists ??= File.Exists;
        getPathEnv ??= () => Environment.GetEnvironmentVariable("PATH");

        // 1. Registry installations
        try
        {
            if (mockRoot is not null)
            {
                var fromMock = ReadPowerShellFromRegistry(mockRoot, fileExists);
                if (fromMock is not null) return fromMock;
            }
            else
            {
                using var hklmCore = Registry.LocalMachine.OpenSubKey(PowerShellCoreKey);
                var path = ReadPowerShellFromRegistry(hklmCore, fileExists);
                if (path is not null) return path;

                using var hkcuCore = Registry.CurrentUser.OpenSubKey(PowerShellCoreKey);
                path = ReadPowerShellFromRegistry(hkcuCore, fileExists);
                if (path is not null) return path;

                using var hklm7 = Registry.LocalMachine.OpenSubKey(PowerShell7Key);
                if (hklm7?.GetValue("InstallFolder") is string folder && !string.IsNullOrWhiteSpace(folder))
                {
                    var exe = Path.Combine(folder, "pwsh.exe");
                    if (fileExists(exe)) return Path.GetFullPath(exe);
                }
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or SecurityException) { }

        // 2. Standard directory locations
        var programFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
        var programFilesX86 = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);
        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);

        var standardCandidates = new[]
        {
            Path.Combine(programFiles, "PowerShell", "7", "pwsh.exe"),
            Path.Combine(programFiles, "PowerShell", "7-preview", "pwsh.exe"),
            @"C:\Program Files\PowerShell\7\pwsh.exe",
            @"C:\Program Files\PowerShell\7-preview\pwsh.exe",
            Path.Combine(programFilesX86, "PowerShell", "7", "pwsh.exe"),
            Path.Combine(programFilesX86, "PowerShell", "7-preview", "pwsh.exe"),
            Path.Combine(localAppData, "Programs", "PowerShell", "7", "pwsh.exe"),
            Path.Combine(localAppData, "Programs", "PowerShell", "7-preview", "pwsh.exe"),
            Path.Combine(localAppData, "Microsoft", "WindowsApps", "pwsh.exe")
        };

        foreach (var candidate in standardCandidates)
        {
            try
            {
                if (fileExists(candidate)) return Path.GetFullPath(candidate);
            }
            catch { }
        }

        // 3. Search PATH
        var pathEnv = getPathEnv();
        if (!string.IsNullOrEmpty(pathEnv))
        {
            var segments = pathEnv.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            foreach (var segment in segments)
            {
                try
                {
                    var candidate = Path.Combine(segment, "pwsh.exe");
                    if (fileExists(candidate)) return Path.GetFullPath(candidate);
                }
                catch { }
            }
        }

        return null;
    }

    private static string? ReadPowerShellFromRegistry(RegistryKey? root, Func<string, bool> fileExists)
    {
        if (root is null) return null;
        foreach (var subKeyName in root.GetSubKeyNames())
        {
            try
            {
                using var entry = root.OpenSubKey(subKeyName);
                if (entry?.GetValue("InstallDir") is string installDir && !string.IsNullOrWhiteSpace(installDir))
                {
                    var candidate = Path.Combine(installDir, "pwsh.exe");
                    if (fileExists(candidate)) return Path.GetFullPath(candidate);
                }
            }
            catch { }
        }
        return null;
    }

    public static string? FindGitBash(
        Func<string, bool>? fileExists = null,
        Func<string?>? getPathEnv = null,
        RegistryKey? mockRoot = null)
    {
        fileExists ??= File.Exists;
        getPathEnv ??= () => Environment.GetEnvironmentVariable("PATH");

        // 1. Registry installations
        try
        {
            if (mockRoot is not null)
            {
                var fromMock = ReadGitBashFromRegistry(mockRoot, fileExists);
                if (fromMock is not null) return fromMock;
            }
            else
            {
                using var hklm = Registry.LocalMachine.OpenSubKey(GitForWindowsKey);
                var path = ReadGitBashFromRegistry(hklm, fileExists);
                if (path is not null) return path;

                using var hkcu = Registry.CurrentUser.OpenSubKey(GitForWindowsKey);
                path = ReadGitBashFromRegistry(hkcu, fileExists);
                if (path is not null) return path;
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or SecurityException) { }

        // 2. Standard directory locations
        var programFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
        var programFilesX86 = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);
        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);

        var standardCandidates = new[]
        {
            Path.Combine(programFiles, "Git", "bin", "bash.exe"),
            Path.Combine(programFiles, "Git", "usr", "bin", "bash.exe"),
            @"C:\Program Files\Git\bin\bash.exe",
            @"C:\Program Files\Git\usr\bin\bash.exe",
            Path.Combine(programFilesX86, "Git", "bin", "bash.exe"),
            Path.Combine(programFilesX86, "Git", "usr", "bin", "bash.exe"),
            Path.Combine(localAppData, "Programs", "Git", "bin", "bash.exe"),
            Path.Combine(localAppData, "Programs", "Git", "usr", "bin", "bash.exe")
        };

        foreach (var candidate in standardCandidates)
        {
            try
            {
                if (fileExists(candidate)) return Path.GetFullPath(candidate);
            }
            catch { }
        }

        // 3. Search PATH for git.exe or valid git bash.exe
        var pathEnv = getPathEnv();
        if (!string.IsNullOrEmpty(pathEnv))
        {
            var segments = pathEnv.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            foreach (var segment in segments)
            {
                try
                {
                    // If git.exe is in this segment (e.g. C:\Program Files\Git\cmd\git.exe):
                    var gitExe = Path.Combine(segment, "git.exe");
                    if (fileExists(gitExe))
                    {
                        var parent = Path.GetDirectoryName(segment);
                        if (!string.IsNullOrEmpty(parent))
                        {
                            var binBash = Path.Combine(parent, "bin", "bash.exe");
                            if (fileExists(binBash)) return Path.GetFullPath(binBash);

                            var usrBinBash = Path.Combine(parent, "usr", "bin", "bash.exe");
                            if (fileExists(usrBinBash)) return Path.GetFullPath(usrBinBash);
                        }
                    }

                    // If bash.exe is directly in PATH (e.g. user added Git\bin to PATH):
                    // Be strictly cautious not to pick up System32/WindowsApps stub bash.exe
                    var bashExe = Path.Combine(segment, "bash.exe");
                    if (fileExists(bashExe) && IsValidGitBash(bashExe, fileExists))
                    {
                        return Path.GetFullPath(bashExe);
                    }
                }
                catch { }
            }
        }

        return null;
    }

    private static string? ReadGitBashFromRegistry(RegistryKey? root, Func<string, bool> fileExists)
    {
        if (root is null) return null;
        if (root.GetValue("InstallPath") is string installPath && !string.IsNullOrWhiteSpace(installPath))
        {
            var binBash = Path.Combine(installPath, "bin", "bash.exe");
            if (fileExists(binBash)) return Path.GetFullPath(binBash);

            var usrBinBash = Path.Combine(installPath, "usr", "bin", "bash.exe");
            if (fileExists(usrBinBash)) return Path.GetFullPath(usrBinBash);
        }
        return null;
    }

    private static bool IsValidGitBash(string path, Func<string, bool> fileExists)
    {
        var lower = path.ToLowerInvariant();
        if (lower.Contains(@"\windowsapps\") || lower.Contains(@"\system32\") || lower.Contains(@"\syswow64\"))
            return false;

        if (lower.Contains("git")) return true;

        var dir = Path.GetDirectoryName(path);
        if (dir is not null && (fileExists(Path.Combine(dir, "sh.exe")) || fileExists(Path.Combine(dir, "..", "cmd", "git.exe"))))
            return true;

        return false;
    }
}
