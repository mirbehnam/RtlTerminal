using System.Collections;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;
using RtlTerminal;

internal static partial class Program
{
    [DllImport("shell32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern IntPtr CommandLineToArgvW(string command, out int count);
    [DllImport("kernel32.dll")] private static extern IntPtr LocalFree(IntPtr memory);

    private static string[] ParseWindowsCommand(string command)
    {
        var memory = CommandLineToArgvW(command, out var count);
        if (memory == IntPtr.Zero) throw new Exception("Could not parse Windows command line");
        try { return Enumerable.Range(0, count).Select(i => Marshal.PtrToStringUni(Marshal.ReadIntPtr(memory, i * IntPtr.Size))!).ToArray(); }
        finally { LocalFree(memory); }
    }

    private static void CheckWslProfiles()
    {
        const BindingFlags instance = BindingFlags.Instance | BindingFlags.NonPublic;
        const BindingFlags methods = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
        var type = typeof(MainWindow).Assembly.GetType("RtlTerminal.WslProfiles")!;
        object? Profiles(string name, params object?[] args) => type.GetMethod(name, methods)!.Invoke(null, args);
        var registryPath = @"Software\RtlTerminal.Tests\Wsl-" + Guid.NewGuid().ToString("N");
        try
        {
            using var root = Registry.CurrentUser.CreateSubKey(registryPath);
            void Entry(string id, string? name, bool modern = false)
            {
                using var key = root.CreateSubKey(id);
                if (name is not null) key.SetValue("DistributionName", name);
                if (modern) key.SetValue("Modern", 1);
            }
            var ubuntuId = Guid.NewGuid().ToString("B");
            Entry(ubuntuId, "Ubuntu-24.04");
            Entry(Guid.NewGuid().ToString("B"), "My-Linux_فارسی", modern: true);
            Entry(Guid.NewGuid().ToString("B"), "docker-desktop");
            Entry(Guid.NewGuid().ToString("B"), "docker-desktop-data");
            Entry(Guid.NewGuid().ToString("B"), "rancher-desktop-data");
            Entry(Guid.NewGuid().ToString("B"), null);
            Entry(Guid.NewGuid().ToString("B"), "");
            Entry("NotADistributionGuid", "Invalid entry");
            Entry(Guid.NewGuid().ToString("B"), "Invalid distribution name");
            var names = (IReadOnlyList<string>)Profiles("ReadDistributions", root)!;
            if (names.Count != 2 || !names.Contains("Ubuntu-24.04") || !names.Contains("My-Linux_فارسی"))
                throw new Exception("WSL discovery lost a distro or exposed utility/invalid entries");
            root.SetValue("DefaultDistribution", ubuntuId);
            if ((string?)Profiles("ReadDefaultDistribution", root) != "Ubuntu-24.04") throw new Exception("WSL default lookup failed");
            root.SetValue("DefaultDistribution", Guid.NewGuid().ToString("B"));
            if (Profiles("ReadDefaultDistribution", root) is not null) throw new Exception("Stale WSL default was accepted");
            if (((IReadOnlyList<string>)Profiles("ReadDistributions", new object?[] { null })!).Count != 0)
                throw new Exception("Missing WSL registration must produce no profiles");
        }
        finally { Registry.CurrentUser.DeleteSubKeyTree(registryPath, false); }

        var profileType = typeof(MainWindow).GetNestedType("TerminalProfile", BindingFlags.NonPublic)!;
        var wsl = Enum.Parse(profileType, "Wsl");
        var commandMethod = typeof(MainWindow).GetMethod("GetProfileCommand", methods)!;
        var distro = "My-Linux_فارسی";
        var command = (string)commandMethod.Invoke(null, new object?[] { wsl, true, distro, null })!;
        var argv = ParseWindowsCommand(command);
        var expected = new List<string> { Path.Combine(Environment.SystemDirectory, "wsl.exe"), "-d", distro };
        if (OperatingSystem.IsWindowsVersionAtLeast(10, 0, 19041)) expected.AddRange(["--cd", "~"]);
        if (command.Contains("-d \"") || !argv.SequenceEqual(expected)) throw new Exception("WSL must start its own default shell in home, without injected commands");
        if (command != (string)commandMethod.Invoke(null, new object?[] { wsl, false, distro, null })!)
            throw new Exception("Split WSL startup differs from normal WSL startup");
        var directory = @"C:\work space\a'b\";
        argv = ParseWindowsCommand((string)Profiles("CommandLine", distro, directory)!);
        if (OperatingSystem.IsWindowsVersionAtLeast(10, 0, 19041) && argv.Last() != directory)
            throw new Exception("WSL starting-directory quoting changed the path");
        argv = ParseWindowsCommand((string)Profiles("CommandLine", null, null)!);
        if (argv.Contains("-d") || argv.Contains("--distribution")) throw new Exception("Default WSL launch must leave selection to WSL");

        var window = new MainWindow();
        try
        {
            object? Call(string name, params object?[] args) => typeof(MainWindow).GetMethod(name, instance)!.Invoke(window, args);
            var menu = (ContextMenu)Call("CreateProfileMenu")!;
            var headers = menu.Items.Cast<MenuItem>().Select(i => ((TextBlock)i.Header).Text).ToArray();
            var discovered = WslProfilesForTests(type);
            var nonWslCount = headers.Length - discovered.Count;
            if (File.Exists(Path.Combine(Environment.SystemDirectory, "wsl.exe")) && !headers.Skip(nonWslCount).SequenceEqual(discovered))
                throw new Exception("Terminal menu does not contain one profile per WSL distribution");
            var pane = Call("CreatePane", wsl)!;
            pane.GetType().GetProperty("WslDistribution")!.SetValue(pane, distro);
            var tabType = typeof(MainWindow).GetNestedType("TerminalTab", BindingFlags.NonPublic)!;
            var tab = Activator.CreateInstance(tabType, pane)!;
            ((IList)typeof(MainWindow).GetField("_tabs", instance)!.GetValue(window)!).Add(tab);
            Call("ShowTab", tab);
            var direction = Enum.Parse(typeof(MainWindow).GetNestedType("PaneSplitDirection", BindingFlags.NonPublic)!, "SideBySide");
            typeof(MainWindow).GetField("_defaultWslDistribution", instance)!.SetValue(window, "DifferentDefault");
            var split = Call("AddSplitPane", tab, pane, direction)!;
            if ((string?)split.GetType().GetProperty("WslDistribution")!.GetValue(split) != distro ||
                !((string)split.GetType().GetProperty("Title")!.GetValue(split)!).StartsWith(distro))
                throw new Exception("Splitting WSL switched to another/default distribution");
        }
        finally { window.Close(); }
        Console.WriteLine("PASS WSL discovery, modern/utility distros, stale defaults, exact launch arguments, profile menu and split distribution retention");
    }

    private static IReadOnlyList<string> WslProfilesForTests(Type type) =>
        (IReadOnlyList<string>)type.GetMethod("Discover")!.Invoke(null, null)!;
}
