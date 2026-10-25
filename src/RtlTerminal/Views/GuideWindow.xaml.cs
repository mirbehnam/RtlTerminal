using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Data;

namespace RtlTerminal;

public partial class GuideWindow : Window
{
    private UiLanguage _shortcutLanguage;

    public void ShowShortcuts() => GuideTabs.SelectedItem = ShortcutsTab;

    private void ShortcutSearch_Changed(object sender, TextChangedEventArgs e) => RefreshShortcuts();

    private void RefreshShortcuts()
    {
        if (ShortcutRows is null || ShortcutSearchBox is null) return;
        var query = ShortcutSearchBox.Text.Trim();
        var rows = ShortcutCatalog.For(_shortcutLanguage).Where(row => query.Length == 0 ||
            (row.Keys + " " + row.Description + " " + row.Category).Contains(query, StringComparison.CurrentCultureIgnoreCase)).ToList();
        var view = new ListCollectionView(rows);
        view.GroupDescriptions.Add(new PropertyGroupDescription(nameof(ShortcutEntry.Category)));
        ShortcutRows.ItemsSource = view;
        ShortcutEmptyText.Visibility = rows.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
    }
    private static readonly SolidColorBrush ActiveLanguageBackground =
        new(Color.FromRgb(25, 112, 168));
    private static readonly SolidColorBrush InactiveLanguageBackground =
        new(Color.FromRgb(38, 54, 80));

    public GuideWindow()
    {
        InitializeComponent();
        ApplyLanguage(UiText.Current switch { UiLanguage.Arabic => GuideLanguage.Arabic, UiLanguage.Persian => GuideLanguage.Persian, _ => GuideLanguage.English });
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
        _shortcutLanguage = language switch { GuideLanguage.Persian => UiLanguage.Persian, GuideLanguage.Arabic => UiLanguage.Arabic, _ => UiLanguage.English };
        string L(string en, string fa, string ar) => ShortcutCatalog.Text(_shortcutLanguage, en, fa, ar);
        FeaturesTab.Header = L("Guide", "راهنما", "الدليل");
        ShortcutsTab.Header = L("Keyboard shortcuts", "کلیدهای میانبر", "اختصارات لوحة المفاتيح");
        ShortcutSearchLabel.Text = L("Search by key or action", "جست‌وجو با نام کلید یا عملکرد", "ابحث بالمفتاح أو الوظيفة");
        ShortcutNote.Text = L("Pane and macro shortcuts can be disabled in Settings. Other terminal keys belong to the running program.",
            "میانبرهای پنل و ماکرو در تنظیمات قابل غیرفعال‌کردن هستند. سایر کلیدهای ترمینال به برنامهٔ در حال اجرا مربوط‌اند.",
            "يمكن تعطيل اختصارات الأجزاء والماكرو في الإعدادات. مفاتيح الطرفية الأخرى تخص التطبيق الجاري.");
        ShortcutEmptyText.Text = L("No matching shortcuts.", "میانبری با این عبارت پیدا نشد.", "لم يُعثر على اختصار مطابق.");
        RefreshShortcuts();
        var content = GetContent(language);

        Title = content.WindowTitle;
        // ContentRoot keeps its existing XAML direction for every UI language.
        GuideHeading.Text = content.Heading;
        GuideSubtitle.Text = content.Subtitle;
        MacrosGuideTitle.Text = language switch { GuideLanguage.English => "Terminal macros", GuideLanguage.Arabic => "وحدات الماكرو", _ => "ماکروهای ترمینال" };
        MacrosGuideText.Text = language switch
        {
            GuideLanguage.English => "Tools → Macros records input in the selected pane. Stop recording to name and review its steps. Ctrl+C without selected text is recorded as an independent action; normal input waits for a prompt and independent keys use their recorded delay. Settings → Macros controls recorded key groups and exclusions. Manage lets you search, edit or step through a macro. Ctrl+Shift+Alt+P pauses/resumes; Ctrl+Shift+Alt+End stops playback. Pausing does not pause the server. An unexpected response waits until timeout, then asks you to review the condition; previous commands are not resent. Sensitive input detection uses password prompts, masking and missing echo, so review its result. Enable asking at runtime to omit sensitive values on save. Existing values are removed only after resaving that macro. In the editor, Advanced can request a named runtime value without saving it. Escape remains a terminal key. Macros stay in their original pane.",
            GuideLanguage.Arabic => "من Tools → Macros سجّل إدخال اللوحة المحددة ثم أوقف التسجيل لتسمية الخطوات ومراجعتها. يُسجَّل Ctrl+C دون تحديد نص كخطوة مستقلة. ينتظر النص المطالبة، وتستخدم المفاتيح المستقلة التأخير المسجّل. اضبط مجموعات المفاتيح والاستثناءات من Settings → Macros. يتيح Manage البحث والتحرير والتنفيذ خطوة بخطوة. Ctrl+Shift+Alt+P للإيقاف المؤقت والمتابعة، وCtrl+Shift+Alt+End لإنهاء التشغيل. لا يتوقف الخادم عند إيقاف الماكرو مؤقتاً. عند انتهاء مهلة المطالبة تُراجع الحالة دون إعادة إرسال الأوامر السابقة. اكتشاف البيانات الحساسة يعتمد على المطالبة والإخفاء وغياب صدى الإدخال؛ يمكن تصحيحه يدوياً. فعّل طلب القيم أثناء التشغيل لمنع تخزينها عند الحفظ. تبقى القيم القديمة حتى إعادة حفظ الماكرو. يتيح قسم Advanced طلب قيمة مسماة أثناء التشغيل. يبقى Escape مفتاحاً للطرفية وتظل الماكرو مرتبطة بلوحتها الأصلية.",
            _ => "از Tools → Macros ضبط ورودی پنل انتخاب‌شده را شروع کنید و با توقف ضبط، نام و مراحل را مرور کنید. Ctrl+C بدون انتخاب متن، یک عمل مستقل ضبط می‌شود؛ متن معمول منتظر اعلان می‌ماند و کلید مستقل با تأخیر ثبت‌شده اجرا می‌شود. در Settings → Macros گروه‌های کلید و استثناها را تنظیم کنید. Manage امکان جست‌وجو، ویرایش و اجرای مرحله‌ای دارد. Ctrl+Shift+Alt+P برای مکث و ادامه و Ctrl+Shift+Alt+End برای پایان اجراست. مکث ماکرو، سرور را متوقف نمی‌کند. پاسخ متفاوت تا پایان مهلت منتظر می‌ماند و سپس برای بررسی شرط مکث می‌کند؛ فرمان قبلی دوباره ارسال نمی‌شود. تشخیص ورودی حساس از اعلان رمز، پوشاندن و تأییدنشدن بازتاب ورودی استفاده می‌کند و قابل اصلاح دستی است. با فعال‌کردن پرسش هنگام اجرا، مقدار حساس هنگام ذخیره حذف می‌شود. مقادیر قدیمی فقط پس از ذخیرهٔ مجدد آن ماکرو حذف می‌شوند. بخش Advanced برای دریافت مقدار نام‌گذاری‌شده هنگام اجراست. Escape کلید ترمینال باقی می‌ماند و ماکرو به پنل اولیه متصل است."
        };
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
                "من قائمة View فعّل Smart RTL لمعالجة العربية والفارسية والنص المختلط تلقائياً. تبقى الحروف متصلة حتى عند تعطيله. خيار Row RTL معطّل افتراضياً ويفرض اتجاه ومحاذاة السطر إلى اليمين، حتى في التطبيقات بملء الشاشة؛ قد يغيّر مواضع حدود واجهتها.",
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
                "Enable Smart RTL in View for automatic Persian, Arabic and mixed-text layout. Persian and Arabic letters remain joined when it is off. Row RTL is off by default; it forces RTL paragraph direction and right alignment, including in full-screen applications, and may move their UI borders. English words keep their internal letter order.",
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
                "از منوی View گزینه Smart RTL را برای نمایش خودکار متن فارسی، عربی و ترکیبی فعال کنید. با خاموش‌کردن آن اتصال حروف حفظ می‌شود. گزینه Row RTL پیش‌فرض خاموش است و جهت و تراز کل سطر را حتی در برنامه‌های تمام‌صفحه راست‌به‌چپ می‌کند؛ ممکن است جای کادرهای رابط آن‌ها تغییر کند. ترتیب حروف داخل کلمات انگلیسی حفظ می‌شود.",
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
