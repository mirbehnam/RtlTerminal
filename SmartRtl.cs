using System.Globalization;
using System.Text;

namespace RtlTerminal;

internal readonly record struct DirectionalSpan(
    int Start,
    int Length,
    bool IsRightToLeft);

internal static class SmartRtl
{
    public static bool IsRightToLeft(TerminalLine line)
    {
        foreach (var run in line.Runs)
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
