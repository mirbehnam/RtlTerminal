using System.Text;
using System.Text.RegularExpressions;

namespace RtlTerminal;

internal sealed record TerminalLinkSpan(int Start, int Length, Uri Uri);

/// <summary>Detect addresses in logical paragraphs, then map them to physical rows.</summary>
internal static class TerminalLinks
{
    private static readonly Regex Pattern = new(@"(?i)\b(?:https?://|www\.)[^\s<>{}\[\]""']+", RegexOptions.Compiled);

    public static Dictionary<int, IReadOnlyList<TerminalLinkSpan>> ForParagraph(IReadOnlyList<TerminalLine> lines, int first, int end)
    {
        var text = new StringBuilder();
        var offsets = new int[end - first + 1];
        for (var row = first; row < end; row++)
        {
            offsets[row - first] = text.Length;
            text.Append(lines[row].Text);
        }
        offsets[^1] = text.Length;
        var result = new Dictionary<int, IReadOnlyList<TerminalLinkSpan>>();
        foreach (Match match in Pattern.Matches(text.ToString()))
        {
            var address = match.Value.TrimEnd('.', ',', ';', ':', '!', '?', ')');
            var stop = match.Index + address.Length;
            var hidden = false;
            for (var row = first; row < end && !hidden; row++)
            {
                var offset = offsets[row - first];
                if (offset >= stop || offsets[row - first + 1] <= match.Index) continue;
                foreach (var run in lines[row].Runs)
                {
                    if (run.Style.Hidden && offset < stop && offset + run.Text.Length > match.Index) { hidden = true; break; }
                    offset += run.Text.Length;
                }
            }
            if (hidden) continue; // Do not expose concealed URL tails through a visible prefix.
            if (address.StartsWith("www.", StringComparison.OrdinalIgnoreCase)) address = "https://" + address;
            if (!Uri.TryCreate(address, UriKind.Absolute, out var uri) || uri.Scheme is not ("http" or "https")) continue;
            for (var row = first; row < end; row++)
            {
                var offset = offsets[row - first];
                var start = Math.Max(match.Index, offset);
                var finish = Math.Min(stop, offsets[row - first + 1]);
                if (finish <= start) continue;
                if (!result.TryGetValue(row, out var spans)) result[row] = spans = new List<TerminalLinkSpan>();
                ((List<TerminalLinkSpan>)spans).Add(new(start - offset, finish - start, uri));
            }
        }
        return result;
    }

    public static Dictionary<int, IReadOnlyList<TerminalLinkSpan>> ForLines(IReadOnlyList<TerminalLine> lines)
    {
        var result = new Dictionary<int, IReadOnlyList<TerminalLinkSpan>>();
        for (var first = 0; first < lines.Count;)
        {
            var end = first + 1;
            while (end < lines.Count && lines[end].WrappedFromPrevious) end++;
            foreach (var pair in ForParagraph(lines, first, end)) result.Add(pair.Key, pair.Value);
            first = end;
        }
        return result;
    }
}
