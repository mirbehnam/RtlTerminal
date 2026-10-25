using System.Collections;

namespace RtlTerminal;

public static class TerminalEnvironment
{
    public const string SessionVariable = "RTLTERMINAL_SESSION_ID";
    public static Dictionary<string, string> Create(IDictionary inherited, string? sessionId = null)
    {
        var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (DictionaryEntry entry in inherited) values[(string)entry.Key] = (string)entry.Value!;
        values["COLORTERM"] = "truecolor";
        values["TERM_PROGRAM"] = "RtlTerminal";
        values["CHERE_INVOKING"] = "1";
        values[ShellPath.PercentVariable] = "%";
        // CMD emits its current directory on each prompt, without changing the
        // visible prompt or the user's persistent environment.
        var prompt = values.GetValueOrDefault("PROMPT", "$P$G");
        if (!prompt.Contains("$e]9;9;", StringComparison.OrdinalIgnoreCase))
            values["PROMPT"] = "$e]9;9;$P$e\\" + prompt;
        if (sessionId is not null) values[SessionVariable] = sessionId;
        var entries = values.GetValueOrDefault("WSLENV", "").Split(':', StringSplitOptions.RemoveEmptyEntries)
            .Where(e => !e.Split('/')[0].Equals("COLORTERM", StringComparison.OrdinalIgnoreCase) &&
                        !e.Split('/')[0].Equals("TERM_PROGRAM", StringComparison.OrdinalIgnoreCase) &&
                        (sessionId is null || !e.Split('/')[0].Equals(SessionVariable, StringComparison.OrdinalIgnoreCase)));
        values["WSLENV"] = string.Join(':', entries.Concat(sessionId is null
            ? new[] { "COLORTERM", "TERM_PROGRAM" } : new[] { "COLORTERM", "TERM_PROGRAM", SessionVariable }));
        return values;
    }
    public static string Block(string? sessionId = null) => string.Join('\0', Create(Environment.GetEnvironmentVariables(), sessionId)
        .OrderBy(p => p.Key, StringComparer.OrdinalIgnoreCase).Select(p => p.Key + "=" + p.Value)) + "\0\0";
}
