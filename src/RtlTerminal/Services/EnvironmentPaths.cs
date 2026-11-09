namespace RtlTerminal;

internal static class EnvironmentPaths
{
    // Since .NET 9, an explicitly empty process variable is distinct from absence.
    // Optional tool home directories must never turn into relative config paths.
    internal static string ReadOrDefault(string name, string fallback) =>
        Environment.GetEnvironmentVariable(name) is { Length: > 0 } value ? value : fallback;
}
