using System.Runtime.InteropServices;

namespace RtlTerminal;

internal enum TuiCompatibilityProfile { Default, OpenTui, Claude }

internal static class TuiCompatibility
{
    internal static bool? ReadClaudeVisualOrder(TerminalSnapshot snapshot)
    {
        foreach (var line in snapshot.Lines.Skip(snapshot.ScrollbackCount).Reverse())
        {
            if (!line.Text.Contains("Claude Code v", StringComparison.Ordinal)) continue;
            var match = System.Text.RegularExpressions.Regex.Match(line.Text, @"Claude Code v(\d+\.\d+\.\d+)",
                System.Text.RegularExpressions.RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(50));
            if (match.Success && Version.TryParse(match.Groups[1].Value, out var version))
                return version >= new Version(2, 1, 322);
        }
        return null;
    }

    internal static bool IsClaudeInputRow(TerminalSnapshot snapshot, int row)
    {
        var cursor = snapshot.CursorRow;
        if (cursor < 0 || cursor >= snapshot.Lines.Count) return false;
        static bool Border(TerminalLine line) => line.Text.Trim().Length >= 3 && line.Text.Trim().All(c => c is '─' or '━' or ' ');
        var top = cursor - 1;
        while (top >= 0 && !Border(snapshot.Lines[top])) top--;
        var bottom = cursor + 1;
        while (bottom < snapshot.Lines.Count && !Border(snapshot.Lines[bottom])) bottom++;
        if (top < 0 || bottom >= snapshot.Lines.Count || row <= top || row >= bottom) return false;
        var prompt = snapshot.Lines[top + 1].Text.TrimStart();
        return prompt.Length >= 2 && prompt[0] is '>' or '❯' && char.IsWhiteSpace(prompt[1]);
    }

    internal static TerminalSnapshot ApplyOverride(TerminalSnapshot snapshot, TuiCompatibilityProfile? profile) => profile is null
        ? snapshot
        : snapshot with { ClaudeLayout = profile == TuiCompatibilityProfile.Claude, PreserveTuiGrid = profile == TuiCompatibilityProfile.OpenTui };

    internal static bool IsFixedGridApp(string name) => name.Equals("opencode.exe", StringComparison.OrdinalIgnoreCase) ||
        name.Equals("opencode2.exe", StringComparison.OrdinalIgnoreCase) || name.Equals("mimo.exe", StringComparison.OrdinalIgnoreCase);

    internal static bool Read(int rootProcessId) => ReadProfile(rootProcessId) == TuiCompatibilityProfile.OpenTui;
    internal static TuiCompatibilityProfile ReadProfile(int rootProcessId)
    {
        if (rootProcessId <= 0) return TuiCompatibilityProfile.Default;
        var snapshot = CreateToolhelp32Snapshot(2, 0);
        if (snapshot == new IntPtr(-1)) return TuiCompatibilityProfile.Default;
        try
        {
            var entries = new List<Entry>(); var entry = new Entry { Size = (uint)Marshal.SizeOf<Entry>() };
            if (!Process32FirstW(snapshot, ref entry)) return TuiCompatibilityProfile.Default;
            do { entries.Add(entry); } while (Process32NextW(snapshot, ref entry));
            var descendants = new HashSet<uint> { (uint)rootProcessId };
            bool added;
            do
            {
                added = false;
                foreach (var p in entries)
                    if (descendants.Contains(p.Parent) && descendants.Add(p.Id)) added = true;
            } while (added);
            // Never opt Codex/Claude into this app-specific compatibility profile,
            // even if another CLI remains in the shell's background process tree.
            if (entries.Any(p => descendants.Contains(p.Id) && p.Exe.Equals("codex.exe", StringComparison.OrdinalIgnoreCase))) return TuiCompatibilityProfile.Default;
            if (entries.Any(p => descendants.Contains(p.Id) && p.Exe.Equals("claude.exe", StringComparison.OrdinalIgnoreCase))) return TuiCompatibilityProfile.Claude;
            return entries.Any(p => descendants.Contains(p.Id) && IsFixedGridApp(p.Exe)) ? TuiCompatibilityProfile.OpenTui : TuiCompatibilityProfile.Default;
        }
        finally { CloseHandle(snapshot); }
    }
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct Entry
    {
        public uint Size, Usage, Id;
        public UIntPtr Heap;
        public uint Module, Threads, Parent;
        public int Priority;
        public uint Flags;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)] public string Exe;
    }
    [DllImport("kernel32.dll", SetLastError = true)] private static extern IntPtr CreateToolhelp32Snapshot(uint flags, uint id);
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)] private static extern bool Process32FirstW(IntPtr snapshot, ref Entry entry);
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)] private static extern bool Process32NextW(IntPtr snapshot, ref Entry entry);
    [DllImport("kernel32.dll")] private static extern bool CloseHandle(IntPtr handle);
}
