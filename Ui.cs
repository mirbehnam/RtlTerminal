namespace RtlTerminal;

public enum UiLanguage
{
    English,
    Hebrew
}

// User interface text. English (the original text) is the lookup key,
// so a missing translation falls back to the original string.
public static class Ui
{
    private static readonly Dictionary<string, string> Hebrew = new()
    {
        ["_File"] = "_קובץ",
        ["_Last directories"] = "_תיקיות אחרונות",
        ["No recent directories"] = "אין תיקיות אחרונות",
        ["_Export session..."] = "_ייצוא התוכן לקובץ...",
        ["E_xit"] = "י_ציאה",
        ["_Edit"] = "_עריכה",
        ["_Font settings..."] = "הגדרות _גופן...",
        ["_View"] = "_תצוגה",
        ["_Smart RTL"] = "RTL _חכם",
        ["Use right-to-left layout only for lines containing RTL letters"] =
            "יישור מימין לשמאל רק בשורות שיש בהן אותיות בכתב מימין לשמאל",
        ["Row RTL"] = "RTL בכל השורות",
        ["Force right-to-left direction and right alignment for every row, including full-screen applications. This may move application borders."] =
            "כיוון מימין לשמאל ויישור לימין בכל שורה, גם ביישומים במסך מלא. עלול להזיז את המסגרות של היישום.",
        ["_Theme"] = "_ערכת צבעים",
        ["_Dark"] = "_כהה",
        ["_Light"] = "_בהירה",
        ["_Language"] = "_שפה",
        ["_Tools"] = "_כלים",
        ["Add _Open in RtlTerminal"] = "הוספת „Open in RtlTerminal” לקליק הימני",
        ["Remove _Open in RtlTerminal"] = "הסרת „Open in RtlTerminal” מהקליק הימני",
        ["_Help"] = "_עזרה",
        ["_Guide"] = "_מדריך",
        ["Check for _updates..."] = "_בדיקת עדכונים...",
        ["Checking for updates..."] = "בודק עדכונים...",
        ["_About Rtl Terminal"] = "_אודות Rtl Terminal",
        ["_Copy"] = "_העתקה",
        ["_Paste"] = "_הדבקה",
        ["Select _all"] = "בחירת ה_כול",
        ["New terminal (Ctrl+Shift+T)"] = "טרמינל חדש (Ctrl+Shift+T)",
        ["New terminal"] = "טרמינל חדש",
        ["Terminal profiles"] = "סוגי טרמינל",
        ["Minimize"] = "מזעור",
        ["Maximize / Restore"] = "הגדלה / שחזור",
        ["Maximize or restore"] = "הגדלה או שחזור",
        ["Close window"] = "סגירת החלון",
        ["Close {0}"] = "סגירת {0}",
        ["Smart RTL automatically handles Persian and mixed-direction text"] =
            "RTL חכם מטפל אוטומטית בטקסט בעברית ובטקסט דו־כיווני",
        ["Command Prompt"] = "שורת הפקודה",
        ["Open link"] = "פתיחת קישור",
        ["There is no active terminal session to export."] = "אין טרמינל פעיל לייצוא.",
        ["Export session"] = "ייצוא התוכן",
        ["Export terminal session"] = "ייצוא תוכן הטרמינל",
        ["Text files (*.txt)|*.txt|All files (*.*)|*.*"] = "קובצי טקסט (*.txt)|*.txt|כל הקבצים (*.*)|*.*",
        ["The session could not be exported."] = "לא ניתן היה לייצא את התוכן.",
        ["An update check is already in progress."] = "בדיקת עדכונים כבר מתבצעת.",
        ["Check for updates"] = "בדיקת עדכונים",
        ["Rtl Terminal {0} is up to date."] = "Rtl Terminal {0} מעודכן לגרסה האחרונה.",
        ["Rtl Terminal could not check GitHub for updates."] = "Rtl Terminal לא הצליח לבדוק עדכונים ב־GitHub.",
        ["The update page could not be opened."] = "לא ניתן היה לפתוח את דף העדכון.",
        ["Rtl Terminal update"] = "עדכון Rtl Terminal",
        ["About Rtl Terminal"] = "אודות Rtl Terminal",
        ["تغییر منوی راست‌کلیک انجام نشد."] = "השינוי בתפריט הקליק הימני נכשל.",
        ["آیا گزینه «Open in RtlTerminal» به منوی راست‌کلیک پوشه‌ها اضافه شود؟"] =
            "להוסיף את „Open in RtlTerminal” לתפריט הקליק הימני של תיקיות?",
        ["افزودن منوی راست‌کلیک انجام نشد."] = "ההוספה לתפריט הקליק הימני נכשלה.",
        ["The clipboard is busy. Please try copying again."] = "הלוח תפוס כרגע. נסו להעתיק שוב.",
        ["The clipboard is busy. Please try pasting again."] = "הלוח תפוס כרגע. נסו להדביק שוב.",
        ["Clipboard"] = "לוח"
    };

    public static UiLanguage Language { get; set; } = UiLanguage.English;

    public static bool IsRightToLeft => Language == UiLanguage.Hebrew;

    public static string T(string text) =>
        Language == UiLanguage.Hebrew && Hebrew.TryGetValue(text, out var translated)
            ? translated
            : text;

    public static string T(string format, params object[] arguments) =>
        string.Format(T(format), arguments);
}
