namespace RtlTerminal;

public sealed record ShortcutEntry(string Category, string Keys, string Description);

public static class ShortcutCatalog
{
    public static string Text(UiLanguage language, string en, string fa, string ar) => language switch
    { UiLanguage.Persian => fa, UiLanguage.Arabic => ar, _ => en };

    public static IReadOnlyList<ShortcutEntry> For(UiLanguage language)
    {
        string L(string en, string fa, string ar) => Text(language, en, fa, ar);
        var entries = new List<ShortcutEntry>();
        void Add(string group, string keys, string en, string fa, string ar) => entries.Add(new(group, keys, L(en, fa, ar)));
        var tabs = L("Tabs and panes", "تب‌ها و پنل‌ها", "علامات التبويب والأجزاء");
        Add(tabs, "Ctrl+Shift+T", "New tab with the default profile", "تب جدید با پروفایل پیش‌فرض", "علامة تبويب جديدة بملف التعريف الافتراضي");
        Add(tabs, "Ctrl+Shift+1 … 8", "Open an available profile by its position in the profile menu", "بازکردن پروفایل بر اساس ترتیب آن در منوی پروفایل‌ها (در صورت وجود)", "فتح ملف تعريف متاح حسب ترتيبه في القائمة");
        Add(tabs, "Ctrl+Tab", "Next tab", "تب بعدی", "علامة التبويب التالية");
        Add(tabs, "Ctrl+Shift+Tab", "Previous tab", "تب قبلی", "علامة التبويب السابقة");
        Add(tabs, "Ctrl+Shift+D", "Split the active pane (if enabled in Settings)", "تقسیم پنل فعال (اگر در تنظیمات فعال باشد)", "تقسيم الجزء النشط عند تمكينه في الإعدادات");
        Add(tabs, "Ctrl+Shift+← / →", "Previous / next pane (if enabled in Settings)", "پنل قبلی / بعدی (اگر در تنظیمات فعال باشد)", "الجزء السابق / التالي عند تمكين الاختصار");
        Add(tabs, "Ctrl+Shift+W", "Close the active pane (if enabled in Settings)", "بستن پنل فعال (اگر در تنظیمات فعال باشد)", "إغلاق الجزء النشط عند تمكين الاختصار");
        Add(tabs, "Enter / Escape", "While renaming a tab: save / cancel", "هنگام ویرایش نام تب: ذخیره / لغو", "أثناء تعديل اسم التبويب: حفظ / إلغاء");
        var clipboard = L("Clipboard and mouse", "کلیپ‌بورد و ماوس", "الحافظة والفأرة");
        Add(clipboard, "Ctrl+C", "Copy selected text; without a selection, interrupt the running command", "کپی متن انتخاب‌شده؛ بدون انتخاب، توقف فرمان جاری", "نسخ النص المحدد؛ دون تحديد، مقاطعة الأمر الجاري");
        Add(clipboard, "Ctrl+Shift+C", "Copy selected text", "کپی متن انتخاب‌شده", "نسخ النص المحدد");
        Add(clipboard, "Ctrl+V / Ctrl+Shift+V", "Paste clipboard content", "چسباندن محتوای کلیپ‌بورد", "لصق محتوى الحافظة");
        Add(clipboard, "Ctrl+Shift+A", "Select all terminal text", "انتخاب همهٔ متن ترمینال", "تحديد كل نص الطرفية");
        Add(clipboard, "Shift+F10 / Menu", "Open the terminal context menu", "بازکردن منوی کپی، چسباندن و انتخاب همه", "فتح قائمة سياق الطرفية");
        Add(clipboard, "Ctrl+Left click", "Open an HTTP/HTTPS link", "بازکردن لینک وب", "فتح رابط ويب");
        Add(clipboard, "Right click", "Copy the selection, or paste if nothing is selected", "کپی متن انتخاب‌شده؛ بدون انتخاب، چسباندن", "نسخ التحديد، أو اللصق دون تحديد");
        Add(clipboard, "Shift+Mouse", "Select text / scroll locally when the terminal application captures the mouse", "انتخاب متن یا اسکرول محلی وقتی برنامهٔ داخل ترمینال ماوس را دریافت می‌کند", "تحديد النص أو التمرير محلياً عند التقاط التطبيق للفأرة");
        Add(clipboard, "Double-click tab title", "Edit the tab name", "ویرایش نام تب", "تعديل اسم علامة التبويب");
        var search = L("Search and display", "جست‌وجو و نمایش", "البحث والعرض");
        Add(search, "Ctrl+F", "Search terminal output", "جست‌وجو در خروجی ترمینال", "البحث في مخرجات الطرفية");
        Add(search, "Enter / Shift+Enter", "In the search box: next / previous result", "در کادر جست‌وجو: نتیجهٔ بعدی / قبلی", "داخل مربع البحث: النتيجة التالية / السابقة");
        Add(search, "Escape", "Close search when it is open; otherwise send Escape to the terminal", "بستن جست‌وجوی باز؛ در غیر این صورت ارسال Escape به ترمینال", "إغلاق البحث المفتوح؛ وإلا إرسال Escape إلى الطرفية");
        Add(search, "Ctrl++ / Ctrl+=", "Zoom in (also Ctrl + numpad +)", "بزرگ‌نمایی (با + بخش عددی کیبورد نیز کار می‌کند)", "تكبير؛ يعمل أيضاً مع + في لوحة الأرقام");
        Add(search, "Ctrl+-", "Zoom out (also Ctrl + numpad -)", "کوچک‌نمایی (با - بخش عددی کیبورد نیز کار می‌کند)", "تصغير؛ يعمل أيضاً مع - في لوحة الأرقام");
        Add(search, "Ctrl+Mouse wheel", "Zoom in / out", "بزرگ‌نمایی / کوچک‌نمایی", "تكبير / تصغير");
        Add(search, "Ctrl+0", "Reset to the saved font size (also numpad 0)", "بازگشت به اندازهٔ فونت ذخیره‌شده (با صفر بخش عددی نیز کار می‌کند)", "إعادة حجم الخط المحفوظ؛ يعمل أيضاً مع صفر لوحة الأرقام");
        Add(search, "F11", "Enter / leave full screen", "ورود به تمام‌صفحه / خروج از آن", "الدخول إلى ملء الشاشة / الخروج منه");
        var macros = L("Macros", "ماکروها", "وحدات الماكرو");
        Add(macros, "Ctrl+Shift+Alt+1 … 9", "Run the macro assigned to that top-row number, when macro shortcuts are enabled", "اجرای ماکروی اختصاص‌یافته به عدد ردیف بالای کیبورد، اگر میانبرهای ماکرو فعال باشند", "تشغيل الماكرو المعيّن لرقم الصف العلوي عند تمكين الاختصارات");
        Add(macros, "Ctrl+Shift+Alt+P", "Pause / resume macro playback, when playback shortcuts are enabled", "مکث / ادامهٔ اجرای ماکرو، اگر میانبرهای پخش فعال باشند", "إيقاف تشغيل الماكرو مؤقتاً / متابعته عند تمكين الاختصار");
        Add(macros, "Ctrl+Shift+Alt+End", "Stop macro playback, when playback shortcuts are enabled", "پایان اجرای ماکرو، اگر میانبرهای پخش فعال باشند", "إنهاء تشغيل الماكرو عند تمكين الاختصار");
        var terminal = L("Keys forwarded to the terminal", "کلیدهای ارسالی به ترمینال", "المفاتيح المرسلة إلى الطرفية");
        Add(terminal, "Ctrl+Space", "Send NUL to the running terminal application", "ارسال NUL به برنامهٔ در حال اجرا", "إرسال NUL إلى التطبيق الجاري");
        Add(terminal, "Ctrl+A … Z", "Other Ctrl-letter combinations go to the running application; its shortcuts depend on that program", "سایر ترکیب‌های Ctrl و حروف به برنامهٔ جاری ارسال می‌شوند؛ کاربردشان به همان برنامه بستگی دارد", "تُرسل بقية تركيبات Ctrl والحروف إلى التطبيق الجاري حسب اختصاراته");
        Add(terminal, "↑ ↓ ← → / Home / End", "Navigation keys, including supported modifier combinations, go to the running application", "کلیدهای حرکت، با ترکیب‌های پشتیبانی‌شده، به برنامهٔ جاری ارسال می‌شوند", "تُرسل مفاتيح التنقل وتركيباتها المدعومة إلى التطبيق الجاري");
        Add(terminal, "Insert / Delete / PgUp / PgDn", "Send editing and paging keys to the running application", "ارسال کلیدهای ویرایش و جابه‌جایی صفحه به برنامهٔ جاری", "إرسال مفاتيح التحرير والصفحات إلى التطبيق الجاري");
        Add(terminal, "Tab / Shift+Tab / Escape", "Send Tab, reverse Tab or Escape when a local editor/search is not handling them", "ارسال Tab، Tab معکوس یا Escape وقتی ویرایشگر نام یا جست‌وجو آن‌ها را دریافت نمی‌کند", "إرسال Tab أو Tab العكسي أو Escape عندما لا يعالجها محرر محلي");
        Add(terminal, "F1 … F10 / F12", "Send function keys to the running program; F11 belongs to full screen", "ارسال کلیدهای تابع به برنامهٔ جاری؛ F11 مخصوص تمام‌صفحه است", "إرسال مفاتيح الوظائف إلى التطبيق؛ F11 لملء الشاشة");
        Add(terminal, "Shift+/ (Persian layout)", "Type the Persian question mark", "نوشتن علامت سؤال فارسی در چیدمان فارسی کیبورد", "كتابة علامة الاستفهام الفارسية مع تخطيط لوحة المفاتيح الفارسي");
        return entries;
    }
}
