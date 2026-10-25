namespace RtlTerminal;

public sealed partial class UiText
{
    private const string TranslationData = """
_File|_فایل|_ملف
_Edit|_ویرایش|_تحرير
_View|_نمایش|_عرض
_Tools|_ابزارها|_أدوات
_Help|_راهنما|_مساعدة
LANGUAGE|زبان|اللغة
_Guide|_راهنما|_الدليل
Keyboard shortcuts|کلیدهای میانبر|اختصارات لوحة المفاتيح
_About Rtl Terminal|_دربارهٔ Rtl Terminal|_حول Rtl Terminal
Check for _updates...|بررسی _به‌روزرسانی…|التحقق من _التحديثات…
Checking for updates...|در حال بررسی به‌روزرسانی…|جارٍ التحقق من التحديثات…
Check for updates|بررسی به‌روزرسانی|التحقق من التحديثات
_Open current directory in Explorer|_بازکردن پوشهٔ فعلی در اکسپلورر|_فتح المجلد الحالي في المستكشف
_Last directories|_پوشه‌های اخیر|_المجلدات الأخيرة
_Export session...|_خروجی‌گرفتن از جلسه…|_تصدير الجلسة…
E_xit|_خروج|_خروج
_Settings...|_تنظیمات…|_الإعدادات…
_Split pane|_تقسیم پنل|_تقسيم الجزء
Show _tab side by side|نمایش _تب در کنار تب فعلی|عرض _علامة تبويب بجوار الحالية
_Move pane to a new tab|_انتقال پنل به تب جدید|_نقل الجزء إلى علامة جديدة
Close _pane|_بستن پنل|_إغلاق الجزء
_Smart RTL|_راست‌به‌چپ هوشمند|_اتجاه RTL الذكي
Row RTL|سطر راست‌به‌چپ|اتجاه السطر RTL
_Theme...|_تم…|_المظهر…
_Macros|_ماکروها|_وحدات الماكرو
_Start recording|_شروع ضبط|_بدء التسجيل
_Stop recording...|_پایان ضبط…|_إيقاف التسجيل…
Stop playback|پایان اجرا|إنهاء التشغيل
_Run|_اجرا|_تشغيل
_Manage recorded macros...|_مدیریت ماکروهای ضبط‌شده…|_إدارة الماكرو المسجّل…
Macro _settings...|_تنظیمات ماکرو…|_إعدادات الماكرو…
Add _Open in RtlTerminal|افزودن «_بازکردن در RtlTerminal»|إضافة «_فتح في RtlTerminal»
Remove _Open in RtlTerminal|حذف «_بازکردن در RtlTerminal»|إزالة «_فتح في RtlTerminal»
_Copy|_کپی|_نسخ
_Paste|_چسباندن|_لصق
Select _all|انتخاب _همه|تحديد _الكل
Split pane|تقسیم پنل|تقسيم الجزء
Split tab|تقسیم تب|تقسيم علامة التبويب
Close pane|بستن پنل|إغلاق الجزء
Close tab|بستن تب|إغلاق علامة التبويب
Move pane to a new tab|انتقال پنل به تب جدید|نقل الجزء إلى علامة جديدة
Show side by side with the current tab|نمایش در کنار تب فعلی|العرض بجوار علامة التبويب الحالية
Color...|رنگ…|اللون…
Custom color...|رنگ دلخواه…|لون مخصص…
Reset color|بازنشانی رنگ|إعادة تعيين اللون
No other tabs|تب دیگری وجود ندارد|لا توجد علامات أخرى
No recent directories|پوشهٔ اخیری وجود ندارد|لا توجد مجلدات حديثة
Manage history…|مدیریت تاریخچه…|إدارة السجل…
More… ({0} directories)|بیشتر… ({0} پوشه)|المزيد… ({0} مجلد)
Settings|تنظیمات|الإعدادات
General|عمومی|عام
Theme|تم|المظهر
History|تاریخچه|السجل
Macros|ماکروها|الماكرو
Standard fonts|فونت‌های پیشنهادی|الخطوط المقترحة
All installed fonts|همهٔ فونت‌های نصب‌شده|كل الخطوط المثبتة
Recommended fonts for CMD, PowerShell, terminals and coding that are installed on this Windows.|فونت‌های پیشنهادی نصب‌شده برای ترمینال و برنامه‌نویسی.|خطوط الطرفية والبرمجة المقترحة المثبتة على Windows.
Size|اندازه|الحجم
Bold|ضخیم|عريض
Italic|ایتالیک|مائل
History lines|تعداد سطرهای سابقه|عدد أسطر السجل
Maximum number of previous terminal lines kept in memory|حداکثر سطرهای قبلی نگه‌داری‌شده در حافظه|الحد الأقصى لأسطر الطرفية السابقة في الذاكرة
Larger history uses more memory and may make scrolling slower.|سابقهٔ بزرگ‌تر حافظهٔ بیشتری مصرف می‌کند و ممکن است اسکرول را کند کند.|السجل الأكبر يستهلك ذاكرة أكثر وقد يبطئ التمرير.
HTML preview range|محدودهٔ پیش‌نمایش HTML|نطاق معاينة HTML
Current page only|فقط صفحهٔ فعلی|الصفحة الحالية فقط
Two pages before the current view through the end|از دو صفحه قبل از نمای فعلی تا انتها|من صفحتين قبل العرض الحالي حتى النهاية
Entire available history|تمام سابقهٔ موجود|كل السجل المتاح
Pane keyboard shortcuts|میان‌برهای پنل|اختصارات الأجزاء
Ctrl+Shift+D — Open a new pane|Ctrl+Shift+D — بازکردن پنل جدید|Ctrl+Shift+D — فتح جزء جديد
Ctrl+Shift+Left / Right — Switch panes|Ctrl+Shift+Left / Right — جابه‌جایی بین پنل‌ها|Ctrl+Shift+Left / Right — التنقل بين الأجزاء
Ctrl+Shift+W — Close the active pane (or its last tab)|Ctrl+Shift+W — بستن پنل فعال یا آخرین تب آن|Ctrl+Shift+W — إغلاق الجزء النشط أو علامته الأخيرة
Disabled shortcuts are passed to the terminal. Ctrl+W is always passed through.|میان‌برهای غیرفعال به ترمینال ارسال می‌شوند. Ctrl+W همیشه ارسال می‌شود.|تُرسل الاختصارات المعطّلة إلى الطرفية. يُرسل Ctrl+W دائماً.
Apply|اعمال|تطبيق
Cancel|لغو|إلغاء
Save|ذخیره|حفظ
Close|بستن|إغلاق
Continue|ادامه|متابعة
Name|نام|الاسم
Preview|پیش‌نمایش|معاينة
Choose your theme|تم را انتخاب کنید|اختر المظهر
Use ↑ and ↓ to preview. Changes apply to all terminals only when you choose Apply.|برای پیش‌نمایش از ↑ و ↓ استفاده کنید. با «اعمال»، تم در همهٔ ترمینال‌ها اعمال می‌شود.|استخدم ↑ و↓ للمعاينة. تُطبّق التغييرات على كل الطرفيات عند اختيار «تطبيق».
Your tabs, panes and terminal output stay unchanged while you preview.|هنگام پیش‌نمایش، تب‌ها، پنل‌ها و خروجی ترمینال تغییر نمی‌کنند.|تبقى علامات التبويب والأجزاء ومخرجات الطرفية كما هي أثناء المعاينة.
Color themes|تم‌های رنگی|المظاهر اللونية
Directory history|تاریخچهٔ پوشه‌ها|سجل المجلدات
Search directories or shells|جست‌وجوی پوشه یا شل|البحث عن مجلد أو صدفة
Search directories or shell names|جست‌وجوی مسیر یا نام شل|البحث في المسارات وأسماء الصدفات
Search directory history|جست‌وجوی تاریخچهٔ پوشه‌ها|البحث في سجل المجلدات
Last visited|آخرین ورود|آخر زيارة
First visit|اولین ورود|أول زيارة
Last visit|آخرین ورود|آخر زيارة
History is on|ثبت تاریخچه فعال است|تسجيل السجل مفعّل
History is off|ثبت تاریخچه غیرفعال است|تسجيل السجل معطّل
No directories recorded yet.|هنوز پوشه‌ای ثبت نشده است.|لم تُسجّل مجلدات بعد.
No matching directories.|پوشه‌ای مطابق جست‌وجو پیدا نشد.|لا توجد مجلدات مطابقة.
Clear history|پاک‌سازی تاریخچه|مسح السجل
Open terminal|بازکردن ترمینال|فتح الطرفية
Remember visited directories|ثبت پوشه‌های بازدیدشده|تذكّر المجلدات التي تمت زيارتها
Time unavailable (imported)|زمان ثبت نشده (منتقل‌شده)|الوقت غير متاح (مستورد)
Unknown|نامشخص|غير معروف
Clear all recorded directories? New visits will still be recorded while history is enabled.|همهٔ مسیرهای ثبت‌شده پاک شوند؟ تا زمانی که ثبت فعال است، ورودهای جدید ثبت می‌شوند.|هل تريد مسح كل المجلدات المسجّلة؟ ستُسجّل الزيارات الجديدة ما دام التسجيل مفعّلاً.
Clear directory history|پاک‌سازی تاریخچهٔ پوشه‌ها|مسح سجل المجلدات
Unable to clear directory history. Please try again.|پاک‌سازی تاریخچه ممکن نشد. دوباره تلاش کنید.|تعذّر مسح السجل. حاول مرة أخرى.
This directory is currently unavailable.|این پوشه اکنون در دسترس نیست.|هذا المجلد غير متاح حالياً.
Default recording mode|حالت پیش‌فرض ضبط|وضع التسجيل الافتراضي
Interactive — wait for terminal prompts|تعاملی — انتظار برای اعلان ترمینال|تفاعلي — انتظار مطالبات الطرفية
Timed — reproduce recorded delays|زمانی — اجرای فاصله‌های زمانی ضبط‌شده|زمني — إعادة التأخيرات المسجّلة
Prompt timeout in seconds (5–600)|مهلت انتظار اعلان به ثانیه (۵ تا ۶۰۰)|مهلة انتظار المطالبة بالثواني (5–600)
Enable macro shortcuts (Ctrl+Shift+Alt+1–9)|فعال‌کردن میان‌برهای ماکرو (Ctrl+Shift+Alt+1–9)|تفعيل اختصارات الماكرو (Ctrl+Shift+Alt+1–9)
Playback shortcuts: Ctrl+Shift+Alt+P (pause), Ctrl+Shift+Alt+End (stop)|میان‌بر اجرا: Ctrl+Shift+Alt+P برای مکث، Ctrl+Shift+Alt+End برای پایان|اختصارات التشغيل: Ctrl+Shift+Alt+P للإيقاف المؤقت، Ctrl+Shift+Alt+End للإنهاء
Record terminal keys|ضبط کلیدهای ترمینال|تسجيل مفاتيح الطرفية
Control combinations, including Ctrl+C / Ctrl+D / Ctrl+Z|ترکیب‌های کنترلی، شامل Ctrl+C / Ctrl+D / Ctrl+Z|تركيبات التحكم، ومنها Ctrl+C / Ctrl+D / Ctrl+Z
Navigation and editing keys (arrows, Tab, Escape, Backspace…)|کلیدهای جابه‌جایی و ویرایش (جهت‌ها، Tab، Escape، Backspace…)|مفاتيح التنقل والتحرير (الأسهم، Tab، Escape، Backspace…)
Function keys F1–F12|کلیدهای تابع F1–F12|مفاتيح الوظائف F1–F12
Exclude combinations (separate with ;), for example: Ctrl+L; F5|استثناها (جداسازی با ;)؛ مانند Ctrl+L; F5|الاستثناءات (افصل بـ ;)؛ مثال: Ctrl+L; F5
Sensitive inputs|ورودی‌های حساس|المدخلات الحساسة
Detect password prompts, masked input and unconfirmed input echo|تشخیص اعلان رمز، ورودی پوشیده و بازتاب تأییدنشده|اكتشاف مطالبات كلمات المرور والإدخال المخفي والصدى غير المؤكد
Do not store sensitive values; ask when running the macro|مقادیر حساس ذخیره نشوند؛ هنگام اجرا پرسیده شوند|عدم تخزين القيم الحساسة؛ طلبها عند تشغيل الماكرو
Prompt timeout must be between 5 and 600 seconds.|مهلت انتظار باید بین ۵ و ۶۰۰ ثانیه باشد.|يجب أن تكون المهلة بين 5 و600 ثانية.
Use key names such as Ctrl+L; Shift+Tab; F5. Separate combinations with semicolons.|از نام‌هایی مانند Ctrl+L; Shift+Tab; F5 استفاده کنید و با ; جدا کنید.|استخدم أسماء مثل Ctrl+L; Shift+Tab; F5 وافصل بينها بفاصلة منقوطة.
Macro|ماکرو|الماكرو
Edit macro|ویرایش ماکرو|تحرير الماكرو
Recorded macros|ماکروهای ضبط‌شده|وحدات الماكرو المسجّلة
Search by name|جست‌وجو با نام|البحث بالاسم
Run|اجرا|تشغيل
Step through|اجرای مرحله‌ای|التنفيذ خطوة بخطوة
Edit / password…|ویرایش / رمز…|تحرير / كلمة المرور…
Delete|حذف|حذف
No recorded macros|ماکرویی ضبط نشده است|لا توجد وحدات ماكرو مسجّلة
Unable to read macros — open Manage|خواندن ماکروها ممکن نیست — مدیریت را باز کنید|تعذّرت قراءة الماكرو — افتح الإدارة
Macro protection and shortcut|حفاظت و میان‌بر ماکرو|حماية الماكرو واختصاره
Independent file password (optional)|رمز مستقل فایل (اختیاری)|كلمة مرور مستقلة للملف (اختيارية)
Confirm password|تکرار رمز|تأكيد كلمة المرور
Ctrl+Shift+Alt + number|Ctrl+Shift+Alt + عدد|Ctrl+Shift+Alt + رقم
Input (hidden until you choose to edit)|ورودی (تا زمان انتخاب ویرایش پوشیده است)|الإدخال (مخفي حتى تختار تحريره)
Reveal / edit input|نمایش / ویرایش ورودی|إظهار / تحرير الإدخال
Run this step|شرط اجرای این مرحله|شرط تشغيل هذه الخطوة
Expected prompt (logical terminal text)|اعلان مورد انتظار (متن منطقی ترمینال)|المطالبة المتوقعة (النص المنطقي للطرفية)
Delay in milliseconds|تأخیر به میلی‌ثانیه|التأخير بالمللي ثانية
Sensitivity|حساس‌بودن ورودی|حساسية الإدخال
Auto|خودکار|تلقائي
Sensitive|حساس|حساس
Normal|معمولی|عادي
Advanced: runtime input / variable|پیشرفته: ورودی زمان اجرا / متغیر|متقدم: إدخال وقت التشغيل / متغير
Ask for a value when this step runs|هنگام اجرای این مرحله، مقدار پرسیده شود|طلب قيمة عند تشغيل هذه الخطوة
Optional variable / input label|نام متغیر / ورودی (اختیاری)|اسم المتغير / الإدخال (اختياري)
Send Enter after the runtime answer|پس از پاسخ زمان اجرا، Enter ارسال شود|إرسال Enter بعد إجابة وقت التشغيل
The whole input of this step becomes the variable. Other text is never interpreted as a template.|کل ورودی این مرحله متغیر می‌شود. متن‌های دیگر به‌عنوان الگو تفسیر نمی‌شوند.|يصبح إدخال هذه الخطوة بالكامل متغيراً. لا تُفسّر النصوص الأخرى كقالب.
Sensitive values will be removed on save and requested when running.|مقادیر حساس هنگام ذخیره حذف و هنگام اجرا پرسیده می‌شوند.|تُحذف القيم الحساسة عند الحفظ وتُطلب أثناء التشغيل.
Sensitive values are stored encrypted. Enable asking at runtime to avoid storing this value.|مقادیر حساس رمزگذاری‌شده ذخیره می‌شوند. برای ذخیره‌نشدن، پرسش هنگام اجرا را فعال کنید.|تُخزّن القيم الحساسة مشفّرة. فعّل طلبها وقت التشغيل لمنع تخزينها.
No automatic sensitivity signal. You can mark this input manually.|نشانهٔ خودکاری یافت نشد. می‌توانید ورودی را دستی حساس علامت بزنید.|لم تُكتشف إشارة تلقائية. يمكنك تحديد حساسية الإدخال يدوياً.
Original recording behavior|رفتار ضبط اصلی|سلوك التسجيل الأصلي
Wait for prompt|انتظار برای اعلان|انتظار المطالبة
After a delay|پس از تأخیر|بعد تأخير
Immediately|بلافاصله|فوراً
+ Text|+ متن|+ نص
+ Key|+ کلید|+ مفتاح
Duplicate|تکثیر|تكرار
None|هیچ‌کدام|بلا
Input|ورودی|إدخال
Key|کلید|مفتاح
Ask when running|پرسیدن هنگام اجرا|الطلب عند التشغيل
{0} characters (hidden)|{0} نویسه (پوشیده)|{0} حرف (مخفي)
Step {0} · {1}|مرحلهٔ {0} · {1}|الخطوة {0} · {1}
Step {0} needs review|مرحلهٔ {0} نیاز به بررسی دارد|الخطوة {0} تحتاج إلى مراجعة
Delay must be a number between 0 and 86400000.|تأخیر باید عددی بین ۰ و ۸۶۴۰۰۰۰۰ باشد.|يجب أن يكون التأخير رقماً بين 0 و86400000.
Choose a valid terminal key combination.|ترکیب کلید معتبر انتخاب کنید.|اختر تركيبة مفاتيح صالحة للطرفية.
Enter a name.|نام را وارد کنید.|أدخل اسماً.
Passwords do not match.|رمزها یکسان نیستند.|كلمتا المرور غير متطابقتين.
This shortcut belongs to another macro.|این میان‌بر متعلق به ماکروی دیگری است.|هذا الاختصار مخصص لماكرو آخر.
Input for this step|ورودی این مرحله|إدخال هذه الخطوة
Sensitive input|ورودی حساس|إدخال حساس
Macro input|ورودی ماکرو|إدخال الماكرو
Used only for this run. Cancel stops the macro.|فقط در همین اجرا استفاده می‌شود. لغو، اجرا را پایان می‌دهد.|يُستخدم لهذا التشغيل فقط. الإلغاء ينهي الماكرو.
The expected prompt did not arrive. No further input was sent. The server may still be running.|اعلان مورد انتظار نرسید. ورودی دیگری ارسال نشد. ممکن است سرور همچنان در حال اجرا باشد.|لم تصل المطالبة المتوقعة. لم يُرسل إدخال آخر. قد يظل الخادم يعمل.
Expected prompt (edit for this run only)|اعلان مورد انتظار (ویرایش فقط برای همین اجرا)|المطالبة المتوقعة (تحرير لهذا التشغيل فقط)
Wait again|انتظار مجدد|الانتظار مجدداً
End run|پایان اجرا|إنهاء التشغيل
Enter a prompt to wait for.|اعلان مورد انتظار را وارد کنید.|أدخل مطالبة لانتظارها.
Macro recording|ضبط ماکرو|تسجيل الماكرو
Save macro|ذخیرهٔ ماکرو|حفظ الماكرو
Could not encrypt the macro.|رمزگذاری ماکرو ممکن نشد.|تعذّر تشفير الماكرو.
Incorrect password or damaged macro file.|رمز نادرست است یا فایل ماکرو آسیب دیده است.|كلمة المرور غير صحيحة أو ملف الماكرو تالف.
Macro stopped|اجرای ماکرو متوقف شد|توقف الماكرو
Unlocking macro…|در حال بازکردن ماکرو…|جارٍ فتح الماكرو…
▶ Running|▶ در حال اجرا|▶ قيد التشغيل
Ⅱ Paused|Ⅱ مکث‌شده|Ⅱ متوقف مؤقتاً
▶ One step|▶ اجرای یک مرحله|▶ تشغيل خطوة واحدة
● Recording · pane {0}|● در حال ضبط · پنل {0}|● جارٍ التسجيل · الجزء {0}
▶ Running · pane {0}|▶ در حال اجرا · پنل {0}|▶ قيد التشغيل · الجزء {0}
! Step {0} needs review|! مرحلهٔ {0} نیاز به بررسی دارد|! الخطوة {0} تحتاج إلى مراجعة
Ⅱ Paused · {0}|Ⅱ مکث‌شده · {0}|Ⅱ متوقف مؤقتاً · {0}
Delete '{0}'?|«{0}» حذف شود؟|حذف «{0}»؟
Delete macro|حذف ماکرو|حذف الماكرو
Recording stopped at its limit. Terminal input was still sent. You can save the captured steps.|ضبط به محدودیت رسید و متوقف شد. ورودی به ترمینال ارسال شد. می‌توانید مراحل ثبت‌شده را ذخیره کنید.|توقف التسجيل لبلوغ الحد. أُرسل الإدخال إلى الطرفية. يمكنك حفظ الخطوات المسجّلة.
Unable to read macro storage. A file may be damaged or belong to another Windows account.|خواندن ماکروها ممکن نیست. ممکن است فایل آسیب دیده یا متعلق به حساب دیگری باشد.|تعذّرت قراءة ملفات الماكرو. قد يكون ملف تالفاً أو تابعاً لحساب آخر.
A new update is available|به‌روزرسانی جدیدی موجود است|يتوفر تحديث جديد
Rtl Terminal update|به‌روزرسانی Rtl Terminal|تحديث Rtl Terminal
Open the official GitHub release page to download and install the update.|برای دریافت و نصب به‌روزرسانی، صفحهٔ رسمی انتشار گیت‌هاب را باز کنید.|افتح صفحة الإصدار الرسمية على GitHub لتنزيل التحديث وتثبيته.
Don't show this update again|این به‌روزرسانی دوباره نشان داده نشود|عدم إظهار هذا التحديث مجدداً
Not now|فعلاً نه|ليس الآن
Open update page|بازکردن صفحهٔ به‌روزرسانی|فتح صفحة التحديث
Installed: {0}    Available: {1}|نصب‌شده: {0}    موجود: {1}|المثبّت: {0}    المتاح: {1}
An update check is already in progress.|بررسی به‌روزرسانی در حال انجام است.|التحقق من التحديث جارٍ بالفعل.
Rtl Terminal {0} is up to date.|Rtl Terminal {0} به‌روز است.|Rtl Terminal {0} محدّث.
The clipboard is busy. Please try copying again.|کلیپ‌بورد مشغول است. دوباره کپی کنید.|الحافظة مشغولة. حاول النسخ مجدداً.
The clipboard is busy. Please try pasting again.|کلیپ‌بورد مشغول است. دوباره بچسبانید.|الحافظة مشغولة. حاول اللصق مجدداً.
Clipboard|کلیپ‌بورد|الحافظة
Paste|چسباندن|لصق
Drop|رهاکردن فایل|إفلات الملفات
Open link|بازکردن لینک|فتح الرابط
Open directory|بازکردن پوشه|فتح المجلد
HTML preview|پیش‌نمایش HTML|معاينة HTML
Preview this page in your browser|پیش‌نمایش این صفحه در مرورگر|معاينة هذه الصفحة في المتصفح
Dark mode|حالت تیره|الوضع الداكن
Switch to dark mode|تغییر به حالت تیره|التبديل إلى الوضع الداكن
Switch to Gruvbox Light|تغییر به Gruvbox Light|التبديل إلى Gruvbox Light
New terminal|ترمینال جدید|طرفية جديدة
New terminal (Ctrl+Shift+T)|ترمینال جدید (Ctrl+Shift+T)|طرفية جديدة (Ctrl+Shift+T)
Terminal profiles|پروفایل‌های ترمینال|ملفات تعريف الطرفية
Minimize|کوچک‌کردن|تصغير
Maximize / Restore|بزرگ‌کردن / بازگردانی|تكبير / استعادة
Maximize or restore|بزرگ‌کردن یا بازگردانی|تكبير أو استعادة
Close window|بستن پنجره|إغلاق النافذة
Macro status|وضعیت ماکرو|حالة الماكرو
Stop macro|پایان ماکرو|إنهاء الماكرو
Pause or resume macro|مکث یا ادامهٔ ماکرو|إيقاف الماكرو مؤقتاً أو متابعته
Run one macro step|اجرای یک مرحلهٔ ماکرو|تشغيل خطوة ماكرو واحدة
Run one step, then pause|اجرای یک مرحله و سپس مکث|تشغيل خطوة واحدة ثم الإيقاف المؤقت
Stop recording / pause playback|پایان ضبط / مکث اجرا|إنهاء التسجيل / إيقاف التشغيل مؤقتاً
Stop playback (Ctrl+Shift+Alt+End)|پایان اجرا (Ctrl+Shift+Alt+End)|إنهاء التشغيل (Ctrl+Shift+Alt+End)
Pause / resume (Ctrl+Shift+Alt+P)|مکث / ادامه (Ctrl+Shift+Alt+P)|إيقاف مؤقت / متابعة (Ctrl+Shift+Alt+P)
Smart RTL automatically handles Persian and mixed-direction text|راست‌به‌چپ هوشمند متن فارسی و ترکیبی را خودکار پردازش می‌کند|يعالج RTL الذكي النصوص الفارسية والمختلطة تلقائياً
Use right-to-left layout only for lines containing RTL letters|چیدمان راست‌به‌چپ فقط برای سطرهای دارای حروف راست‌به‌چپ|تطبيق اتجاه RTL فقط على الأسطر التي تحتوي حروفه
Force right-to-left direction and right alignment for every row, including full-screen applications. This may move application borders.|اعمال جهت و تراز راست‌به‌چپ به همهٔ سطرها، حتی برنامه‌های تمام‌صفحه؛ ممکن است کادرها جابه‌جا شوند.|فرض اتجاه ومحاذاة RTL على كل الأسطر، بما فيها تطبيقات ملء الشاشة؛ قد تتغير مواضع الحدود.
Export terminal session|خروجی‌گرفتن از جلسهٔ ترمینال|تصدير جلسة الطرفية
Please select a font.|یک فونت انتخاب کنید.|اختر خطاً.
There is not enough room for all panes. Enlarge the window and try again.|فضای کافی برای پنل‌ها وجود ندارد. پنجره را بزرگ‌تر کنید.|لا توجد مساحة كافية لكل الأجزاء. كبّر النافذة وحاول مجدداً.
{0} of {1} recent directories · {2}. Hover a row to see its first visit.|{0} از {1} پوشهٔ اخیر · {2}. زمان اولین ورود در راهنمای ردیف است.|{0} من {1} مجلد حديث · {2}. تظهر أول زيارة في تلميح الصف.
Keep up to 200 recently visited directories across tabs and split panes. File → Last directories shows the most recent 20. More opens the searchable history with visit dates.|تا ۲۰۰ پوشهٔ اخیر تب‌ها و اسپلیت‌ها نگه داشته می‌شود. File → Last directories بیست مسیر اخیر را نشان می‌دهد. «بیشتر» تاریخچهٔ قابل جست‌وجو همراه با زمان ورود را باز می‌کند.|يُحفظ حتى 200 مجلد حديث عبر علامات التبويب والأجزاء. تعرض File → Last directories آخر 20. يفتح «المزيد» سجلاً قابلاً للبحث مع أوقات الزيارة.
Turning history off stops recording new visits and keeps existing entries. Clear history from the directory history window to remove saved entries. Apply saves this preference.|غیرفعال‌کردن ثبت، ورودهای جدید را ثبت نمی‌کند و سابقهٔ موجود را نگه می‌دارد. برای حذف سابقه از پنجرهٔ تاریخچه استفاده کنید. «اعمال» این انتخاب را ذخیره می‌کند.|تعطيل التسجيل يوقف تسجيل الزيارات الجديدة ويحفظ السجل الحالي. امسح السجل من نافذة سجل المجلدات. يحفظ «تطبيق» هذا الخيار.
Applies when saving. Existing stored values are removed when you open and save that macro again. Detection is conservative, not guaranteed; review each step's sensitivity.|هنگام ذخیره اعمال می‌شود. مقادیر قدیمی با بازکردن و ذخیرهٔ مجدد همان ماکرو حذف می‌شوند. تشخیص محافظه‌کارانه و غیرقطعی است؛ حساس‌بودن مراحل را بررسی کنید.|يُطبّق عند الحفظ. تُحذف القيم القديمة عند فتح الماكرو وحفظه مجدداً. الاكتشاف احترازي وغير مضمون؛ راجع حساسية كل خطوة.
Files are encrypted for your Windows account. An independent macro password adds protection from other apps using that account. Runtime answers are never saved.|فایل‌ها برای حساب ویندوز شما رمزگذاری می‌شوند. رمز مستقل ماکرو حفاظت بیشتری در برابر برنامه‌های همان حساب ایجاد می‌کند. پاسخ‌های زمان اجرا ذخیره نمی‌شوند.|تُشفّر الملفات لحساب Windows. تضيف كلمة مرور مستقلة حماية من برامج الحساب نفسه. لا تُحفظ إجابات وقت التشغيل.
Interactive text waits for prompts; independent keys use their recorded delay. The editor lets you change either condition. A missing prompt pauses for review without repeating the previous command.|متن تعاملی منتظر اعلان می‌ماند؛ کلید مستقل از تأخیر ضبط‌شده استفاده می‌کند. شرط‌ها در ویرایشگر قابل تغییرند. نرسیدن اعلان باعث مکث برای بررسی می‌شود و فرمان قبلی تکرار نمی‌شود.|ينتظر النص التفاعلي المطالبة؛ تستخدم المفاتيح المستقلة التأخير المسجّل. يمكن تعديل الشرطين بالمحرر. غياب المطالبة يوقف التنفيذ للمراجعة دون تكرار الأمر السابق.
{0} · Select a numbered step to review its input and condition.|{0} · یک مرحله را برای بررسی ورودی و شرط اجرا انتخاب کنید.|{0} · اختر خطوة مرقّمة لمراجعة إدخالها وشرطها.
Interactive|تعاملی|تفاعلي
Timed|زمانی|زمني
Automatic detection: {0}|تشخیص خودکار: {0}|الاكتشاف التلقائي: {0}
Password or secret prompt|اعلان رمز یا اطلاعات محرمانه|مطالبة كلمة مرور أو بيانات سرية
Masked input detected|ورودی پوشیده تشخیص داده شد|اكتُشف إدخال مخفي
Input echo not confirmed — treated as sensitive|بازتاب ورودی تأیید نشد — حساس در نظر گرفته شد|لم يُؤكد صدى الإدخال — عومل كحساس
Windows protected|حفاظت حساب ویندوز|محمي بحساب Windows
Password protected|محافظت‌شده با رمز|محمي بكلمة مرور
file password|رمز فایل|كلمة مرور الملف
About Rtl Terminal|دربارهٔ Rtl Terminal|حول Rtl Terminal
Developer|توسعه‌دهنده|المطوّر
Dedicated to all Persian speakers|تقدیم به همهٔ فارسی‌زبانان|إهداء إلى جميع المتحدثين بالفارسية
Add 'Open in RtlTerminal' to the folder context menu?|گزینهٔ «Open in RtlTerminal» به منوی راست‌کلیک پوشه‌ها اضافه شود؟|إضافة «Open in RtlTerminal» إلى قائمة سياق المجلدات؟
Could not change the folder context menu.|تغییر منوی راست‌کلیک انجام نشد.|تعذّر تغيير قائمة سياق المجلدات.
Could not add the folder context menu.|افزودن منوی راست‌کلیک انجام نشد.|تعذّرت إضافة قائمة سياق المجلدات.
Rtl Terminal could not check GitHub for updates.|بررسی به‌روزرسانی گیت‌هاب ممکن نشد.|تعذّر التحقق من تحديثات GitHub.
The update page could not be opened.|بازکردن صفحهٔ به‌روزرسانی ممکن نشد.|تعذّر فتح صفحة التحديث.
Export session|خروجی‌گرفتن از جلسه|تصدير الجلسة
There is no active terminal session to export.|جلسهٔ فعالی برای خروجی‌گرفتن وجود ندارد.|لا توجد جلسة نشطة لتصديرها.
Split terminals|تقسیم ترمینال‌ها|تقسيم الطرفيات
Invalid macro or recording is too large.|ماکرو نامعتبر است یا حجم ضبط بیش از حد است.|الماكرو غير صالح أو التسجيل كبير جداً.
Invalid macro step.|مرحلهٔ ماکرو نامعتبر است.|خطوة الماكرو غير صالحة.
Invalid macro.|ماکرو نامعتبر است.|الماكرو غير صالح.
Invalid macro file.|فایل ماکرو نامعتبر است.|ملف الماكرو غير صالح.
Unsupported macro file.|قالب فایل ماکرو پشتیبانی نمی‌شود.|ملف الماكرو غير مدعوم.
Invalid macro filename.|نام فایل ماکرو نامعتبر است.|اسم ملف الماكرو غير صالح.
The macro directory cannot be a symbolic link.|پوشهٔ ماکرو نمی‌تواند لینک نمادین باشد.|لا يمكن أن يكون مجلد الماكرو رابطاً رمزياً.
This macro shortcut is already assigned.|این میان‌بر قبلاً اختصاص داده شده است.|اختصار الماكرو مخصص بالفعل.
A password is required.|رمز لازم است.|كلمة المرور مطلوبة.
Macro metadata does not match.|مشخصات فایل ماکرو همخوانی ندارد.|بيانات الماكرو غير متطابقة.
The target terminal has closed.|ترمینال مقصد بسته شده است.|أُغلقت الطرفية المستهدفة.
A prompt is required.|اعلان مورد انتظار لازم است.|المطالبة مطلوبة.
Recording limit reached. Stop and save this recording.|ضبط به محدودیت رسید. ضبط را متوقف و ذخیره کنید.|بلغ التسجيل الحد. أوقف هذا التسجيل واحفظه.
Step {0} needs a prompt to wait for. Edit the prompt before saving.|مرحلهٔ {0} به اعلان مورد انتظار نیاز دارد. پیش از ذخیره، اعلان را ویرایش کنید.|تحتاج الخطوة {0} إلى مطالبة لانتظارها. حرّر المطالبة قبل الحفظ.
Step {0}: prompt was not detected. Nothing further was sent.|مرحلهٔ {0}: اعلان تشخیص داده نشد. ورودی دیگری ارسال نشد.|الخطوة {0}: لم تُكتشف المطالبة. لم يُرسل إدخال آخر.
Running as administrator|در حال اجرا با دسترسی مدیر|يعمل بصلاحيات المسؤول
Open current directory in Explorer|بازکردن پوشهٔ فعلی در اکسپلورر|فتح المجلد الحالي في المستكشف
Current directory is not available yet|پوشهٔ فعلی هنوز مشخص نیست|المجلد الحالي غير متاح بعد
The current directory is unavailable. Return to the shell prompt and try again.|پوشهٔ فعلی در دسترس نیست. به اعلان شل برگردید و دوباره تلاش کنید.|المجلد الحالي غير متاح. ارجع إلى مطالبة الصدفة وحاول مجدداً.
Tab name|نام تب|اسم علامة التبويب
Cancel rename|لغو تغییر نام|إلغاء إعادة التسمية
Find...|جست‌وجو…|بحث…
Previous match|تطابق قبلی|المطابقة السابقة
Next match|تطابق بعدی|المطابقة التالية
Match case|حساس به حروف بزرگ و کوچک|مطابقة حالة الأحرف
Close search|بستن جست‌وجو|إغلاق البحث
{0} of {1}|{0} از {1}|{0} من {1}
No results|نتیجه‌ای یافت نشد|لا توجد نتائج
Full screen|تمام‌صفحه|ملء الشاشة
Zoom in|بزرگ‌نمایی|تكبير
Zoom out|کوچک‌نمایی|تصغير
Reset zoom|بازنشانی بزرگ‌نمایی|إعادة تعيين الحجم
PowerShell 7|پاورشل ۷|PowerShell 7
Git Bash|گیت بش|Git Bash
""";
}
