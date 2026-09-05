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
                "من قائمة View فعّل Smart RTL لمعالجة العربية والفارسية والنص المختلط تلقائياً. تبقى الأسطر اللاتينية يسارية، وتحافظ التطبيقات بملء الشاشة على شبكة الطرفية.",
                "النسخ واللصق",
                "للنسخ، حدّد النص واضغط Ctrl+C أو Ctrl+Shift+C. للصق استخدم Ctrl+V أو Ctrl+Shift+V.",
                "بدون تحديد نص، يرسل Ctrl+C إشارة إيقاف. النقر الأيمن ينسخ النص المحدد، أو يلصق إن لم يوجد تحديد. مفتاح قائمة السياق أو Shift+F10 يفتح قائمة النسخ واللصق. في التطبيقات التي تلتقط الفأرة، اضغط Shift لتحديد النص أو تجاوز التقاط الفأرة.",
                "فتح الروابط",
                "تظهر روابط HTTP/HTTPS باللون الأزرق. مرّر المؤشر لرؤية التلميح الإنجليزي، ثم استخدم Ctrl مع النقر الأيسر لفتح الرابط.",
                "قائمة النقر بزر الفأرة الأيمن في Windows",
                "من قائمة Tools يمكنك إضافة خيار Open in RtlTerminal للمجلدات أو إزالته. ستُفتح الطرفية في المسار الذي اخترته.",
                "مفاتيح الطرفية",
                "تُرسل مفاتيح الأسهم وHome وEnd وInsert وDelete وPage Up وPage Down وTab وEscape إلى البرنامج الجاري داخل الطرفية."),
            GuideLanguage.English => new GuideContent(
                "Rtl Terminal Guide",
                "Rtl Terminal Guide",
                "A quick guide to terminal features and shortcuts",
                "Display direction",
                "Enable Smart RTL in View for automatic Persian, Arabic and mixed-text layout. Latin-only lines stay left-to-right. Full-screen applications retain their terminal grid.",
                "Copy and paste",
                "To copy, select text and press Ctrl+C or Ctrl+Shift+C. To paste, use Ctrl+V or Ctrl+Shift+V.",
                "With no selection, Ctrl+C interrupts the running command. Right-click copies selected text; without a selection, it pastes. Press the keyboard Context Menu key or Shift+F10 for Copy, Paste and Select all. When an application captures the mouse, hold Shift to select text or bypass mouse reporting.",
                "Opening links",
                "HTTP/HTTPS links are blue. Hover for the hint, then use Ctrl + Left Click to open link.",
                "Windows context menu",
                "From the Tools menu, you can add or remove Open in RtlTerminal for folders. The terminal opens in the selected path.",
                "Terminal keys",
                "Arrow keys, Home, End, Insert, Delete, Page Up, Page Down, Tab and Escape are sent to the application running in the terminal."),
            _ => new GuideContent(
                "راهنمای Rtl Terminal",
                "راهنمای Rtl Terminal",
                "راهنمای سریع امکانات و میان‌برهای ترمینال",
                "جهت نمایش",
                "از منوی View گزینه Smart RTL را فعال کنید تا متن فارسی، عربی و ترکیبی خودکار نمایش داده شود. سطرهای کاملاً لاتین چپ‌به‌راست می‌مانند و برنامه‌های تمام‌صفحه شبکهٔ ترمینال را حفظ می‌کنند.",
                "کپی و چسباندن",
                "برای کپی، متن را انتخاب کرده و Ctrl+C یا Ctrl+Shift+C را بزنید. برای چسباندن از Ctrl+V یا Ctrl+Shift+V استفاده کنید.",
                "بدون انتخاب متن، Ctrl+C فرمان جاری را متوقف می‌کند. راست‌کلیک متن انتخاب‌شده را کپی می‌کند؛ بدون انتخاب، Paste انجام می‌دهد. کلید Context Menu کیبورد یا Shift+F10 منوی Copy، Paste و Select all را باز می‌کند. در برنامه‌هایی که ماوس را دریافت می‌کنند، برای انتخاب متن یا نادیده‌گرفتن دریافت ماوس Shift را نگه دارید.",
                "بازکردن لینک‌ها",
                "لینک‌های HTTP/HTTPS آبی هستند. با قرارگرفتن ماوس روی لینک، راهنمای انگلیسی نمایش داده می‌شود. برای بازکردن لینک Ctrl را نگه دارید و کلیک چپ کنید.",
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
