using System.Text;
using System.Text.RegularExpressions;

namespace RtlTerminal;

public static class QuickInputText
{
    public const int MaximumBytes = 1_048_576;
    public static bool NeedsSingleLine(string text) => text.IndexOfAny(['\r', '\n', '\t']) >= 0;
    public static string SingleLine(string text) => Regex.Replace(text, "\r\n|\r|\n", " ").Replace("\t", "    ");
    public static string Prepare(string text, bool bracketed)
    {
        if (Encoding.UTF8.GetByteCount(text) > MaximumBytes) throw new ArgumentException("Quick Input supports up to 1 MB of text. Shorten the text and try again.");
        if (text.Any(c => char.IsControl(c) && c is not ('\r' or '\n' or '\t'))) throw new ArgumentException("Remove control characters from the text before inserting it.");
        if (!bracketed && NeedsSingleLine(text)) throw new ArgumentException("This terminal has not enabled safe multiline paste.");
        return bracketed ? "\x1b[200~" + text + "\x1b[201~" : text;
    }
}
