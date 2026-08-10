using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace RtlTerminal;

public partial class GuideWindow : Window
{
    private static readonly SolidColorBrush ActiveLanguageBackground =
        new(Color.FromRgb(25, 112, 168));
    private static readonly SolidColorBrush InactiveLanguageBackground =
        new(Color.FromRgb(38, 54, 80));

    public GuideWindow()
    {
        InitializeComponent();
        ApplyLanguage(GuideLanguage.Persian);
    }

    private void LanguageButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: string languageName } &&
            Enum.TryParse(languageName, out GuideLanguage language))
        {
            ApplyLanguage(language);
        }
    }

    private void ApplyLanguage(GuideLanguage language)
    {
        var content = GetContent(language);
        var isEnglish = language == GuideLanguage.English;

        Title = content.WindowTitle;
        ContentRoot.FlowDirection = isEnglish
            ? FlowDirection.LeftToRight
            : FlowDirection.RightToLeft;
        GuideHeading.Text = content.Heading;
        GuideSubtitle.Text = content.Subtitle;
        DirectionTitle.Text = content.DirectionTitle;
        DirectionText.Text = content.DirectionText;
        ClipboardTitle.Text = content.ClipboardTitle;
        ClipboardText.Text = content.ClipboardText;
        ClipboardNote.Text = content.ClipboardNote;
        LinksTitle.Text = content.LinksTitle;
        LinksText.Text = content.LinksText;
        ContextMenuTitle.Text = content.ContextMenuTitle;
        ContextMenuText.Text = content.ContextMenuText;
        KeysTitle.Text = content.KeysTitle;
        KeysText.Text = content.KeysText;

        PersianButton.Background = language == GuideLanguage.Persian
            ? ActiveLanguageBackground
            : InactiveLanguageBackground;
        ArabicButton.Background = language == GuideLanguage.Arabic
            ? ActiveLanguageBackground
            : InactiveLanguageBackground;
        EnglishButton.Background = language == GuideLanguage.English
            ? ActiveLanguageBackground
            : InactiveLanguageBackground;
    }

    private static GuideContent GetContent(GuideLanguage language) =>
        language switch
        {
            GuideLanguage.Arabic => new GuideContent(
                "دليل Rtl Terminal",
                "دليل Rtl Terminal",
                "دليل سريع لميزات الطرفية واختصاراتها",
                "اتجاه العرض",
                "من قائمة View فعّل خيار Right-to-left لجعل الصفحة بأكملها من اليمين إلى اليسار. عند إلغاء تفعيله يعود العرض من اليسار إلى اليمين.",
                "النسخ واللصق",
                "للنسخ، حدّد النص واضغط Ctrl+C أو Ctrl+Shift+C. للصق استخدم Ctrl+V أو Ctrl+Shift+V.",
                "عندما لا يكون هناك نص محدد، يرسل Ctrl+C إشارة إيقاف إلى الأمر الجاري في الطرفية.",
                "فتح الروابط",
                "تظهر الروابط باللون الأزرق. لتجنب فتحها عن طريق الخطأ، اضغط باستمرار على Ctrl ثم انقر على الرابط.",
                "قائمة النقر بزر الفأرة الأيمن في Windows",
                "من قائمة Tools يمكنك إضافة خيار Open in RtlTerminal للمجلدات أو إزالته. ستُفتح الطرفية في المسار الذي اخترته.",
                "مفاتيح الطرفية",
                "تُرسل مفاتيح الأسهم وHome وEnd وInsert وDelete وPage Up وPage Down وTab وEscape إلى البرنامج الجاري داخل الطرفية."),
            GuideLanguage.English => new GuideContent(
                "Rtl Terminal Guide",
                "Rtl Terminal Guide",
                "A quick guide to terminal features and shortcuts",
                "Display direction",
                "Enable Right-to-left from the View menu to display the entire page from right to left. Disable it to return to left-to-right display.",
                "Copy and paste",
                "To copy, select text and press Ctrl+C or Ctrl+Shift+C. To paste, use Ctrl+V or Ctrl+Shift+V.",
                "When no text is selected, Ctrl+C sends an interrupt signal to the command currently running in the terminal.",
                "Opening links",
                "Links are displayed in blue. To prevent accidental opening, hold Ctrl and click the link.",
                "Windows context menu",
                "From the Tools menu, you can add or remove Open in RtlTerminal for folders. The terminal opens in the selected path.",
                "Terminal keys",
                "Arrow keys, Home, End, Insert, Delete, Page Up, Page Down, Tab and Escape are sent to the application running in the terminal."),
            _ => new GuideContent(
                "راهنمای Rtl Terminal",
                "راهنمای Rtl Terminal",
                "راهنمای سریع امکانات و میان‌برهای ترمینال",
                "جهت نمایش",
                "از منوی View گزینه Right-to-left را فعال کنید تا کل صفحه راست‌به‌چپ شود. با غیرفعال‌کردن آن نمایش دوباره چپ‌به‌راست خواهد شد.",
                "کپی و چسباندن",
                "برای کپی، متن را انتخاب کرده و Ctrl+C یا Ctrl+Shift+C را بزنید. برای چسباندن از Ctrl+V یا Ctrl+Shift+V استفاده کنید.",
                "وقتی متنی انتخاب نشده باشد، Ctrl+C برای توقف فرمان جاری به ترمینال ارسال می‌شود.",
                "بازکردن لینک‌ها",
                "لینک‌ها با رنگ آبی نمایش داده می‌شوند. برای جلوگیری از بازشدن تصادفی، کلید Ctrl را نگه دارید و روی لینک کلیک کنید.",
                "منوی راست‌کلیک ویندوز",
                "از منوی Tools می‌توانید گزینه Open in RtlTerminal را برای پوشه‌ها نصب یا حذف کنید. ترمینال در همان مسیر انتخاب‌شده باز می‌شود.",
                "کلیدهای ترمینال",
                "کلیدهای جهت، Home، End، Insert، Delete، Page Up، Page Down، Tab و Escape به برنامه در حال اجرای ترمینال ارسال می‌شوند.")
        };

    private enum GuideLanguage
    {
        Persian,
        Arabic,
        English
    }

    private sealed record GuideContent(
        string WindowTitle,
        string Heading,
        string Subtitle,
        string DirectionTitle,
        string DirectionText,
        string ClipboardTitle,
        string ClipboardText,
        string ClipboardNote,
        string LinksTitle,
        string LinksText,
        string ContextMenuTitle,
        string ContextMenuText,
        string KeysTitle,
        string KeysText);
}
