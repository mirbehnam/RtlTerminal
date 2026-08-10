using System.Globalization;
using System.Text;

namespace RtlTerminal;

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
