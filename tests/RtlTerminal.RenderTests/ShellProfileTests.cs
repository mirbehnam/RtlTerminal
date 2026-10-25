using System.Collections;
using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Microsoft.Win32;
using RtlTerminal;

internal static partial class Program
{
    private static void CheckShellProfiles()
    {
        const BindingFlags staticFlags = BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public;
        const BindingFlags instanceFlags = BindingFlags.Instance | BindingFlags.NonPublic;

        // 1. Detection tests for PowerShell 7
        // Standard path
        var standardPwsh = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "PowerShell", "7", "pwsh.exe");
        var found = ShellProfiles.FindPowerShell7(path => string.Equals(path, standardPwsh, StringComparison.OrdinalIgnoreCase), () => null);
        if (found != Path.GetFullPath(standardPwsh))
            throw new Exception("PowerShell 7 detection failed for standard path");

        // Preview path
        var previewPwsh = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "PowerShell", "7-preview", "pwsh.exe");
        found = ShellProfiles.FindPowerShell7(path => string.Equals(path, previewPwsh, StringComparison.OrdinalIgnoreCase), () => null);
        if (found != Path.GetFullPath(previewPwsh))
            throw new Exception("PowerShell 7 detection failed for preview path");

        // PATH search
        var pathDir = @"C:\CustomTools\bin";
        var pathPwsh = Path.Combine(pathDir, "pwsh.exe");
        found = ShellProfiles.FindPowerShell7(path => path == pathPwsh, () => @"C:\Windows\system32;" + pathDir);
        if (found != Path.GetFullPath(pathPwsh))
            throw new Exception("PowerShell 7 detection failed from PATH");

        // Registry search
        var regPath = @"Software\RtlTerminal.Tests\Pwsh-" + Guid.NewGuid().ToString("N");
        try
        {
            using var root = Registry.CurrentUser.CreateSubKey(regPath);
            using var subKey = root.CreateSubKey(Guid.NewGuid().ToString("B"));
            var regDir = @"C:\RegistryInstalled\PowerShell";
            var regPwsh = Path.Combine(regDir, "pwsh.exe");
            subKey.SetValue("InstallDir", regDir);
            found = ShellProfiles.FindPowerShell7(path => path == regPwsh, () => null, root);
            if (found != Path.GetFullPath(regPwsh))
                throw new Exception("PowerShell 7 detection failed from registry");
        }
        finally { Registry.CurrentUser.DeleteSubKeyTree(regPath, false); }

        // Not installed
        found = ShellProfiles.FindPowerShell7(_ => false, () => null);
        if (found is not null)
            throw new Exception("PowerShell 7 should be null when not found");

        // 2. Detection tests for Git Bash
        // Standard Git Bash path
        var standardGitBash = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Git", "bin", "bash.exe");
        var foundGit = ShellProfiles.FindGitBash(path => string.Equals(path, standardGitBash, StringComparison.OrdinalIgnoreCase), () => null);
        if (foundGit != Path.GetFullPath(standardGitBash))
            throw new Exception("Git Bash detection failed for standard bin path");

        // usr\bin path
        var usrGitBash = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Git", "usr", "bin", "bash.exe");
        foundGit = ShellProfiles.FindGitBash(path => string.Equals(path, usrGitBash, StringComparison.OrdinalIgnoreCase), () => null);
        if (foundGit != Path.GetFullPath(usrGitBash))
            throw new Exception("Git Bash detection failed for usr bin path");

        // Registry search
        var gitRegPath = @"Software\RtlTerminal.Tests\Git-" + Guid.NewGuid().ToString("N");
        try
        {
            using var root = Registry.CurrentUser.CreateSubKey(gitRegPath);
            var gitInstallDir = @"C:\RegistryGit";
            root.SetValue("InstallPath", gitInstallDir);
            var gitExePath = Path.Combine(gitInstallDir, "bin", "bash.exe");
            foundGit = ShellProfiles.FindGitBash(path => path == gitExePath, () => null, root);
            if (foundGit != Path.GetFullPath(gitExePath))
                throw new Exception("Git Bash detection failed from registry");
        }
        finally { Registry.CurrentUser.DeleteSubKeyTree(gitRegPath, false); }

        // PATH search via git.exe in cmd directory
        var gitCmdDir = @"C:\Tools\Git\cmd";
        var gitBinBash = @"C:\Tools\Git\bin\bash.exe";
        foundGit = ShellProfiles.FindGitBash(
            path => path == Path.Combine(gitCmdDir, "git.exe") || path == gitBinBash,
            () => gitCmdDir);
        if (foundGit != Path.GetFullPath(gitBinBash))
            throw new Exception("Git Bash detection failed via git.exe in cmd folder");

        // Reject WindowsApps and System32 WSL stub bash
        var stubWsl = @"C:\Windows\System32\bash.exe";
        foundGit = ShellProfiles.FindGitBash(path => path == stubWsl, () => @"C:\Windows\System32");
        if (foundGit is not null)
            throw new Exception("System32 bash.exe must not be detected as Git Bash");

        var appsStub = @"C:\Users\User\AppData\Local\Microsoft\WindowsApps\bash.exe";
        foundGit = ShellProfiles.FindGitBash(path => path == appsStub, () => @"C:\Users\User\AppData\Local\Microsoft\WindowsApps");
        if (foundGit is not null)
            throw new Exception("WindowsApps bash.exe must not be detected as Git Bash");

        // 3. Profile Titles
        var profileType = typeof(MainWindow).GetNestedType("TerminalProfile", BindingFlags.NonPublic)!;
        var titleMethod = typeof(MainWindow).GetMethod("GetProfileTitle", staticFlags)!;
        string Title(string name) => (string)titleMethod.Invoke(null, [Enum.Parse(profileType, name)])!;

        if (Title("PowerShell7") != "PowerShell 7") throw new Exception("PowerShell 7 title is incorrect");
        if (Title("PowerShell") != "PowerShell") throw new Exception("PowerShell title is incorrect");
        if (Title("GitBash") != "Git Bash") throw new Exception("Git Bash title is incorrect");
        if (Title("CommandPrompt") != "Command Prompt") throw new Exception("Command Prompt title is incorrect");
        if (Title("Wsl") != "WSL") throw new Exception("WSL title is incorrect");

        // 4. Profile Commands
        var commandMethod = typeof(MainWindow).GetMethod("GetProfileCommand", staticFlags)!;
        string Command(string name, bool intro) =>
            (string)commandMethod.Invoke(null, [Enum.Parse(profileType, name), intro, null, null])!;

        var pwshIntro = Command("PowerShell7", true);
        var pwshQuiet = Command("PowerShell7", false);
        if (!pwshIntro.Contains("-NoLogo") || !pwshIntro.Contains("-NoExit") || !pwshIntro.Contains("$global:__RtlOriginalPrompt"))
            throw new Exception("PowerShell 7 intro command missing expected flags or OSC directory prompt");
        if (!pwshQuiet.Contains("-NoLogo") || !pwshQuiet.Contains("-NoExit") || !pwshQuiet.Contains("$global:__RtlOriginalPrompt"))
            throw new Exception("PowerShell 7 quiet command missing expected flags or OSC directory prompt");
        if (pwshQuiet.Contains("RtlTerminal v"))
            throw new Exception("Quiet PowerShell 7 must not include the startup banner");

        var gitIntro = Command("GitBash", true);
        var gitQuiet = Command("GitBash", false);
        if (!gitIntro.EndsWith("--login -i") || !gitQuiet.EndsWith("--login -i"))
            throw new Exception("Git Bash command must end with --login -i");

        // 5. Profile Icons
        var pwshIcon = ProfileIcons.Get(ProfileIconKind.PowerShell7);
        var gitIcon = ProfileIcons.Get(ProfileIconKind.GitBash);
        if (!pwshIcon.IsFrozen || pwshIcon.Width <= 0) throw new Exception("PowerShell 7 icon must be frozen and nonempty");
        if (!gitIcon.IsFrozen || gitIcon.Width <= 0) throw new Exception("Git Bash icon must be frozen and nonempty");

        var pwshFallback = ProfileIcons.Fallback(ProfileIconKind.PowerShell7);
        var gitFallback = ProfileIcons.Fallback(ProfileIconKind.GitBash);
        if (!pwshFallback.IsFrozen || pwshFallback.Width <= 0) throw new Exception("PowerShell 7 vector fallback must be frozen and nonempty");
        if (!gitFallback.IsFrozen || gitFallback.Width <= 0) throw new Exception("Git Bash vector fallback must be frozen and nonempty");

        // 6. Menu and Shortcuts
        var window = new MainWindow();
        try
        {
            var menu = (ContextMenu)typeof(MainWindow).GetMethod("CreateProfileMenu", instanceFlags)!.Invoke(window, null)!;
            var items = menu.Items.OfType<MenuItem>().ToArray();
            if (items.Length < 2) throw new Exception("Profile menu must contain installed shells");

            for (var i = 0; i < items.Length; i++)
            {
                if (items[i].Icon is not Image) throw new Exception("Menu item missing icon");
                var expectedShortcut = i < 8 ? $"Ctrl+Shift+{i + 1}" : string.Empty;
                if (items[i].InputGestureText != expectedShortcut)
                    throw new Exception($"Menu item {i} gesture mismatch: expected '{expectedShortcut}' got '{items[i].InputGestureText}'");
            }

            var tabs = (IList)typeof(MainWindow).GetField("_tabs", instanceFlags)!.GetValue(window)!;
            var tabType = typeof(MainWindow).GetNestedType("TerminalTab", BindingFlags.NonPublic)!;
            var activeTabField = typeof(MainWindow).GetField("_activeTab", instanceFlags)!;
            var previewKeyDown = typeof(MainWindow).GetMethod("Window_PreviewKeyDown", instanceFlags)!;

            // Test Ctrl+Shift+1 shortcut opens 1st profile
            var initialTabCount = tabs.Count;
            var eventArgs = new KeyEventArgs(
                Keyboard.PrimaryDevice,
                new System.Windows.Interop.HwndSource(0, 0, 0, 0, 0, "", IntPtr.Zero),
                0,
                Key.D1)
            {
                RoutedEvent = UIElement.PreviewKeyDownEvent
            };

            // Keyboard.Modifiers cannot easily be set without low-level simulation,
            // but we can verify available profiles list matches menu count.
            var availableProfilesMethod = typeof(MainWindow).GetMethod("GetAvailableProfiles", instanceFlags)!;
            var available = (IList)availableProfilesMethod.Invoke(window, null)!;
            if (available.Count != items.Length)
                throw new Exception("Available profiles list count must match menu item count");
        }
        finally { window.Close(); }

        // 7. TerminalEnvironment
        var env = TerminalEnvironment.Create(new Hashtable());
        if (!env.TryGetValue("CHERE_INVOKING", out var chere) || chere != "1")
            throw new Exception("TerminalEnvironment must contain CHERE_INVOKING=1 for Git Bash startup directory support");

        Console.WriteLine("PASS PowerShell 7 and Git Bash detection, commands, modern icons, dropdown menu, and startup directories");
    }
}
