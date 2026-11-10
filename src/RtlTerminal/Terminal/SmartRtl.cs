using System.Globalization;
using System.Text;

namespace RtlTerminal;

internal readonly record struct DirectionalSpan(
    int Start,
    int Length,
    bool IsRightToLeft);

internal static class SmartRtl
{
    internal static bool FirstStrongRtl(string text) => text.EnumerateRunes().Where(Rune.IsLetter).Select(GetStrongDirection).FirstOrDefault(d => d >= 0, 0) == 1;
    internal static bool IsClaudeCodeLine(string text)
    {
        var value = text.TrimStart(' ', '\t', '●', '>', '❯');
        return System.Text.RegularExpressions.Regex.IsMatch(value,
            @"^(?:[A-Za-z_$][A-Za-z0-9_.$]*\s*(?:\(|=|:)|(?:const|let|var|return|import|from|using|public|private|class|def|function|if|for|while)\b|[{}\[\]]|//|/\*)",
            System.Text.RegularExpressions.RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(50));
    }
    internal static IReadOnlyList<DirectionalSpan> GetCodeSpans(string text)
    {
        var result = new List<DirectionalSpan>();
        var start = 0;
        bool RtlAt(int offset) => Rune.TryGetRuneAt(text, offset, out var rune) && IsStrongRtlLetter(rune);
        for (var i = 0; i < text.Length;)
        {
            if (text[i] is '"' or '\'' or '`')
            {
                var quote = text[i];
                result.Add(new(start, i + 1 - start, false));
                var contentStart = ++i;
                while (i < text.Length && text[i] != quote)
                {
                    if (text[i] == '\\' && i + 1 < text.Length) i += 2; else i++;
                }
                var content = text[contentStart..i]; var rtl = FirstStrongRtl(content);
                var inner = GetDirectionalSpans(content, rtl);
                foreach (var span in rtl ? inner.Reverse() : inner) result.Add(span with { Start = span.Start + contentStart });
                if (i < text.Length) { result.Add(new(i, 1, false)); i++; }
                start = i; continue;
            }
            if (!RtlAt(i)) { i++; continue; }
            if (i > start) result.Add(new(start, i - start, false));
            start = i;
            do { i += char.IsSurrogatePair(text, i) ? 2 : 1; }
            while (i < text.Length && (RtlAt(i) || text[i] == '\u200c' || CharUnicodeInfo.GetUnicodeCategory(text, i) is UnicodeCategory.NonSpacingMark or UnicodeCategory.SpacingCombiningMark));
            result.Add(new(start, i - start, true)); start = i;
        }
        if (start < text.Length) result.Add(new(start, text.Length - start, false));
        return result;
    }
    // Reorder text inside a widget without moving the widget's grid position.
    // Borders and multi-cell padding separate independent TUI text regions.
    internal static IReadOnlyList<DirectionalSpan> GetFixedGridSpans(string text)
    {
        var result = new List<DirectionalSpan>();
        var start = 0;
        void Add(int end)
        {
            if (end <= start) return;
            var content = text[start..end];
            var first = content.EnumerateRunes().Select(GetStrongDirection).FirstOrDefault(d => d >= 0, 0);
            var spans = GetDirectionalSpans(content, first == 1);
            foreach (var span in first == 1 ? spans.Reverse() : spans)
                result.Add(span with { Start = span.Start + start });
        }
        for (var i = 0; i < text.Length;)
        {
            var end = i;
            if (text[i] is >= '\u2500' and <= '\u257f') end = i + 1;
            else if (char.IsWhiteSpace(text[i]))
            {
                while (end < text.Length && char.IsWhiteSpace(text[end])) end++;
                if (end - i < 2 && i != 0 && end != text.Length) { i = end; continue; }
            }
            else { i++; continue; }
            Add(i);
            result.Add(new DirectionalSpan(i, end - i, false));
            start = end; i = end;
        }
        Add(text.Length);
        return result;
    }
    public static bool IsRightToLeft(TerminalLine line)
    {
        return line.ContainsRightToLeft;
    }

    public static bool ShouldRightAlign(
        TerminalLine line,
        bool smartRtlEnabled,
        bool preserveTerminalGrid)
    {
        return smartRtlEnabled &&
            !preserveTerminalGrid &&
            IsRightToLeft(line);
    }

    internal static bool ContainsRightToLeft(
        IReadOnlyList<TerminalRun> runs)
    {
        foreach (var run in runs)
        {
            foreach (var rune in run.Text.EnumerateRunes())
            {
                if (IsStrongRtlLetter(rune))
                    return true;
            }
        }

        return false;
    }

    public static IReadOnlyList<DirectionalSpan> GetDirectionalSpans(
        string text,
        bool baseRightToLeft)
    {
        if (string.IsNullOrEmpty(text))
            return [];

        var baseDirection = baseRightToLeft ? 1 : 0;
        var directions = new int[text.Length];
        var whitespace = new bool[text.Length];
        Array.Fill(directions, -1);

        var index = 0;

        foreach (var rune in text.EnumerateRunes())
        {
            var direction = GetStrongDirection(rune);
            var isWhitespace = Rune.IsWhiteSpace(rune);

            for (var offset = 0; offset < rune.Utf16SequenceLength; offset++)
            {
                directions[index + offset] = direction;
                whitespace[index + offset] = isWhitespace;
            }

            index += rune.Utf16SequenceLength;
        }

        ResolveBracketDirections(text, directions);
        ResolveNeutralDirections(directions, whitespace, baseDirection);

        var spans = new List<DirectionalSpan>();
        var spanStart = 0;
        var spanDirection = directions[0];

        for (index = 1; index < text.Length; index++)
        {
            if (directions[index] == spanDirection)
                continue;

            spans.Add(new DirectionalSpan(
                spanStart,
                index - spanStart,
                spanDirection == 1));
            spanStart = index;
            spanDirection = directions[index];
        }

        spans.Add(new DirectionalSpan(
            spanStart,
            text.Length - spanStart,
            spanDirection == 1));
        return spans;
    }

    private static int GetStrongDirection(Rune rune)
    {
        if (IsStrongRtlLetter(rune))
            return 1;

        var category = Rune.GetUnicodeCategory(rune);

        if (category is UnicodeCategory.UppercaseLetter or
            UnicodeCategory.LowercaseLetter or
            UnicodeCategory.TitlecaseLetter or
            UnicodeCategory.ModifierLetter or
            UnicodeCategory.OtherLetter or
            UnicodeCategory.DecimalDigitNumber or
            UnicodeCategory.LetterNumber or
            UnicodeCategory.OtherNumber)
        {
            return 0;
        }

        return -1;
    }

    private static void ResolveBracketDirections(
        string text,
        int[] directions)
    {
        var brackets = new Stack<(char Character, int Index)>();

        for (var index = 0; index < text.Length; index++)
        {
            var character = text[index];

            if (character is '(' or '[' or '{' or '<')
            {
                brackets.Push((character, index));
                continue;
            }

            if (character is not (')' or ']' or '}' or '>'))
                continue;

            var expectedOpening = character switch
            {
                ')' => '(',
                ']' => '[',
                '}' => '{',
                _ => '<'
            };
            var matchingBracket = brackets
                .FirstOrDefault(item => item.Character == expectedOpening);

            if (matchingBracket.Character == '\0')
                continue;

            while (brackets.Count > 0)
            {
                var popped = brackets.Pop();

                if (popped.Index == matchingBracket.Index)
                    break;
            }

            var contentDirection = GetEnclosedDirection(
                directions,
                matchingBracket.Index + 1,
                index);

            if (contentDirection < 0)
                continue;

            directions[matchingBracket.Index] = contentDirection;
            directions[index] = contentDirection;
        }
    }

    private static int GetEnclosedDirection(
        int[] directions,
        int start,
        int end)
    {
        var foundDirection = -1;

        for (var index = start; index < end; index++)
        {
            if (directions[index] < 0)
                continue;

            if (foundDirection >= 0 && foundDirection != directions[index])
                return -1;

            foundDirection = directions[index];
        }

        return foundDirection;
    }

    private static void ResolveNeutralDirections(
        int[] directions,
        bool[] whitespace,
        int baseDirection)
    {
        var index = 0;

        while (index < directions.Length)
        {
            if (directions[index] >= 0)
            {
                index++;
                continue;
            }

            var start = index;
            var containsWhitespace = false;

            while (index < directions.Length && directions[index] < 0)
            {
                containsWhitespace |= whitespace[index];
                index++;
            }

            var previousDirection = start > 0
                ? directions[start - 1]
                : baseDirection;
            var nextDirection = index < directions.Length
                ? directions[index]
                : baseDirection;
            var resolvedDirection = previousDirection == nextDirection
                ? previousDirection
                : !containsWhitespace &&
                    (previousDirection == 0 || nextDirection == 0)
                    ? 0
                    : baseDirection;

            for (var neutralIndex = start;
                 neutralIndex < index;
                 neutralIndex++)
            {
                directions[neutralIndex] = resolvedDirection;
            }
        }
    }

    private static bool IsStrongRtlLetter(Rune rune)
    {
        var category = Rune.GetUnicodeCategory(rune);

        if (category is not (
                UnicodeCategory.UppercaseLetter or
                UnicodeCategory.LowercaseLetter or
                UnicodeCategory.TitlecaseLetter or
                UnicodeCategory.ModifierLetter or
                UnicodeCategory.OtherLetter))
        {
            return false;
        }

        var value = rune.Value;
        return value is >= 0x0590 and <= 0x08ff
            or >= 0xfb1d and <= 0xfdff
            or >= 0xfe70 and <= 0xfeff
            or >= 0x10800 and <= 0x10fff
            or >= 0x1e800 and <= 0x1eeff;
    }
}
