namespace RtlTerminal;

public sealed partial class UiText
{
    private const string ProxyTranslationData = """
Proxy Manager...|مدیریت پراکسی...|إدارة الوكيل...
Proxy Manager|مدیریت پراکسی|إدارة الوكيل
One place for Windows and developer-tool proxy settings.|مدیریت یکپارچهٔ پراکسی ویندوز و ابزارهای توسعه.|مكان واحد لإعدادات وكيل Windows وأدوات التطوير.
Proxy connection|اتصال پراکسی|اتصال الوكيل
Choose the endpoint used by the selected configurations.|نشانی پراکسی را برای تنظیمات انتخاب‌شده وارد کنید.|اختر نقطة الوكيل التي تستخدمها الإعدادات المحددة.
Bypass addresses|نشانی‌های مستثنا|عناوين الاستثناء
Localhost and loopback are always included when setting a proxy.|هنگام تنظیم پراکسی، localhost و loopback همیشه مستثنا می‌شوند.|يُستثنى localhost والاسترجاع المحلي دائماً عند ضبط الوكيل.
Environment includes User + Machine (UAC). Windows System and WinHTTP use the current user.|محیط شامل کاربر و سیستم با تأیید UAC است؛ Windows System و WinHTTP برای کاربر فعلی تنظیم می‌شوند.|تشمل البيئة المستخدم والجهاز مع UAC؛ ويستخدم Windows System وWinHTTP نطاق المستخدم الحالي.
Optional. Only selected, supported tools are modified.|اختیاری است؛ فقط ابزارهای پشتیبانی‌شده و انتخاب‌شده تغییر می‌کنند.|اختياري؛ لا تتغير إلا الأدوات المدعومة والمحددة.
Configuration History|تاریخچهٔ تنظیمات|سجل الإعدادات
Latest 10 verified snapshots. Restore the exact saved configuration.|۱۰ نسخهٔ پشتیبان معتبر اخیر؛ بازیابی دقیق تنظیمات ذخیره‌شده.|أحدث 10 نسخ احتياطية متحقق منها؛ استعادة الإعدادات المحفوظة بدقة.
Current configuration and diagnostics|تنظیمات فعلی و عیب‌یابی|الإعدادات الحالية والتشخيص
Environment changes include User + Machine (UAC approval). Windows System and WinHTTP remain User-scoped. Restart existing processes to inherit changes.|تغییرات محیطی شامل کاربر و سیستم است و تأیید UAC می‌خواهد. Windows System و WinHTTP در محدودهٔ کاربر می‌مانند. برنامه‌های باز را دوباره اجرا کنید.|تشمل تغييرات البيئة المستخدم والجهاز مع موافقة UAC. يبقى Windows System وWinHTTP ضمن نطاق المستخدم. أعد تشغيل البرامج المفتوحة.
Protocol|پروتکل|البروتوكول
Host / IP|میزبان / نشانی IP|المضيف / عنوان IP
Port|درگاه|المنفذ
Bypass addresses · localhost is always preserved|نشانی‌های مستثنا · localhost همیشه حفظ می‌شود|عناوين الاستثناء · يتم الاحتفاظ بـ localhost دائماً
Configuration scope|محدودهٔ تنظیمات|نطاق الإعدادات
Proxy Status|وضعیت پراکسی|حالة الوكيل
Help|راهنما|المساعدة
Proxy Manager Guide|راهنمای مدیریت پراکسی|دليل إدارة الوكيل
The stored protocol is unavailable in this scope. Choose Environment to review it.|پروتکل ذخیره‌شده در این محدوده در دسترس نیست. برای بررسی آن، متغیرهای محیطی را انتخاب کنید.|البروتوكول المحفوظ غير متاح في هذا النطاق. اختر متغيرات البيئة لمراجعته.
Refresh proxy status|بازخوانی وضعیت پراکسی|تحديث حالة الوكيل
Reading…|در حال خواندن…|جارٍ القراءة…
Enabled|فعال|مفعّل
Not configured|تنظیم نشده|غير مكوّن
Partially configured|تنظیم ناقص|مكوّن جزئياً
Not set|ثبت نشده|غير معيّن
Notifications|اطلاعیه‌ها|الإشعارات
ⓘ  Notifications|ⓘ  اطلاعیه‌ها|ⓘ  الإشعارات
Message|پیام|الرسالة
User|کاربر|المستخدم
Machine|سیستم|الجهاز
Process|فرایند|العملية
Proxy Enable|فعال بودن پراکسی|تمكين الوكيل
Proxy Server|سرور پراکسی|خادم الوكيل
PAC URL|نشانی PAC|عنوان PAC
Bypass List|فهرست استثناها|قائمة التجاوز
Automatic Detection|تشخیص خودکار|الاكتشاف التلقائي
Proxy Bypass|استثناهای پراکسی|تجاوز الوكيل
AutoConfig URL|نشانی تنظیم خودکار|عنوان التكوين التلقائي
Auto Detect|تشخیص خودکار|الاكتشاف التلقائي
Configured settings only; connectivity is not tested.|فقط وضعیت تنظیمات ثبت‌شده است؛ اتصال آزمایش نشده است.|تعرض الإعدادات المكوّنة فقط؛ لم يتم اختبار الاتصال.
Process values are inherited snapshots. Persistent User and Machine settings are read separately.|مقدارهای فرایند هنگام اجرا به ارث رسیده‌اند. تنظیمات دائمی کاربر و سیستم جداگانه خوانده می‌شوند.|قيم العملية موروثة عند التشغيل. تُقرأ إعدادات المستخدم والجهاز الدائمة بشكل منفصل.
HTTP, HTTPS or SOCKS5 · User + Machine (UAC)|HTTP، HTTPS یا SOCKS5 · کاربر و سیستم (UAC)|HTTP أو HTTPS أو SOCKS5 · المستخدم والجهاز (UAC)
HTTP only · Environment requires UAC|فقط HTTP · متغیرهای محیطی نیازمند UAC هستند|HTTP فقط · تتطلب متغيرات البيئة UAC
HTTP only · Current user|فقط HTTP · کاربر فعلی|HTTP فقط · المستخدم الحالي
Environment variables support HTTP, HTTPS and SOCKS5. Selected developer tools must support the chosen protocol too.|متغیرهای محیطی از HTTP، HTTPS و SOCKS5 پشتیبانی می‌کنند. ابزارهای توسعهٔ انتخاب‌شده نیز باید از پروتکل انتخابی پشتیبانی کنند.|تدعم متغيرات البيئة HTTP وHTTPS وSOCKS5. يجب أن تدعم أدوات التطوير المحددة البروتوكول المختار أيضاً.
All uses HTTP, the protocol supported by all three Windows proxy systems.|در حالت همه از HTTP استفاده می‌شود؛ این پروتکل در هر سه سیستم پراکسی ویندوز پشتیبانی می‌شود.|يستخدم خيار الكل HTTP، وهو البروتوكول المدعوم في أنظمة وكيل Windows الثلاثة.
This provider supports HTTP proxy endpoints. PAC and automatic detection are configured below.|این بخش از اتصال پراکسی HTTP پشتیبانی می‌کند. تنظیمات PAC و تشخیص خودکار در پایین قرار دارند.|يدعم هذا الموفّر اتصالات وكيل HTTP. يمكن ضبط PAC والاكتشاف التلقائي أدناه.
Your Environment protocol selection is preserved when you return to Environment.|هنگام بازگشت به متغیرهای محیطی، پروتکل انتخابی قبلی شما حفظ می‌شود.|يُحفظ البروتوكول الذي اخترته عند العودة إلى متغيرات البيئة.
All|همه|الكل
Environment|متغیرهای محیطی|متغيرات البيئة
Windows System|تنظیمات ویندوز|إعدادات Windows
Set and Clear affect the selected Windows scope. HTTP works across all three systems; other protocols require a compatible provider.|تنظیم و پاک‌کردن فقط محدودهٔ انتخاب‌شده را تغییر می‌دهند. HTTP در هر سه بخش کار می‌کند؛ سایر پروتکل‌ها به پشتیبانی نیاز دارند.|يؤثر الضبط والمسح على النطاق المحدد فقط. يعمل HTTP في الأنظمة الثلاثة؛ وتتطلب البروتوكولات الأخرى دعماً متوافقاً.
Enable manual proxy|فعال‌کردن پراکسی دستی|تمكين الوكيل اليدوي
Automatically detect settings|تشخیص خودکار تنظیمات|اكتشاف الإعدادات تلقائياً
Automatic configuration script (PAC URL)|نشانی اسکریپت تنظیم خودکار (PAC)|عنوان برنامج التكوين التلقائي (PAC)
Set All Proxies|تنظیم همهٔ پراکسی‌ها|ضبط جميع الوكلاء
Clear All Proxies|پاک‌کردن همهٔ پراکسی‌ها|مسح جميع الوكلاء
Set Proxy|تنظیم پراکسی|ضبط الوكيل
Clear Proxy|پاک‌کردن پراکسی|مسح الوكيل
Restore Selected Backup|بازیابی نسخهٔ پشتیبان انتخاب‌شده|استعادة النسخة الاحتياطية المحددة
Clear Snapshots…|پاک‌کردن نسخه‌های پشتیبان…|مسح النسخ الاحتياطية…
Developer Tools|ابزارهای توسعه|أدوات التطوير
Optional. Selected supported tools can accompany any Windows scope.|اختیاری است. ابزارهای پشتیبانی‌شدهٔ انتخابی می‌توانند همراه هر محدوده تغییر کنند.|اختياري. يمكن تعديل الأدوات المدعومة المحددة مع أي نطاق.
Also apply to selected developer tools|برای ابزارهای توسعهٔ انتخاب‌شده هم اعمال شود|التطبيق أيضاً على أدوات التطوير المحددة
Installed tools|ابزارهای نصب‌شده|الأدوات المثبتة
Expand installed developer tools|نمایش ابزارهای توسعهٔ نصب‌شده|عرض أدوات التطوير المثبتة
Select All|انتخاب همه|تحديد الكل
Deselect All|لغو انتخاب همه|إلغاء تحديد الكل
Refresh|بازخوانی|تحديث
{0} selected|{0} انتخاب‌شده|تم تحديد {0}
Discovering installed tools…|در حال شناسایی ابزارهای نصب‌شده…|جارٍ اكتشاف الأدوات المثبتة…
No supported installations found in PATH or known locations.|ابزاری در PATH یا مسیرهای شناخته‌شده پیدا نشد.|لم يُعثر على أدوات في PATH أو المواقع المعروفة.
{0} installations found|{0} ابزار پیدا شد|عُثر على {0} أداة
Inherited / inspection|ارث‌بری / فقط مشاهده|موروث / للعرض فقط
User settings|تنظیمات کاربر|إعدادات المستخدم
Checking version and adapter compatibility…|در حال بررسی نسخه و سازگاری…|جارٍ التحقق من الإصدار والتوافق…
Detected · inherited, unsupported or unverified adapter|شناسایی‌شده · ارث‌بری یا پشتیبانی تأییدنشده|مكتشف · موروث أو دعم غير متحقق منه
Version-verified User adapter · project/environment overrides may remain|نسخهٔ پشتیبانی‌شدهٔ کاربر · تنظیمات پروژه یا محیط ممکن است اولویت داشته باشند|إصدار مستخدم متحقق منه · قد تبقى تجاوزات المشروع أو البيئة
Configuration History · latest 10 verified snapshots|تاریخچهٔ تنظیمات · ۱۰ نسخهٔ پشتیبان معتبر اخیر|سجل الإعدادات · أحدث 10 نسخ احتياطية متحقق منها
{0} · {1} · {2} · Complete (managed scope)|{0} · {1} · {2} · کامل در محدودهٔ مدیریت‌شده|{0} · {1} · {2} · مكتمل ضمن النطاق المُدار
Environment (User)|متغیرهای محیطی (کاربر)|متغيرات البيئة (المستخدم)
Environment (Machine · administrator)|متغیرهای محیطی (سیستم · مدیر)|متغيرات البيئة (الجهاز · مسؤول)
Windows system (User)|تنظیمات ویندوز (کاربر)|إعدادات Windows (المستخدم)
WinHTTP (effective User)|WinHTTP (تنظیم مؤثر کاربر)|WinHTTP (إعداد المستخدم الفعلي)
Environment Process (inspection)|محیط پردازش (فقط مشاهده)|بيئة العملية (للعرض فقط)
Environment Machine (inspection)|محیط سیستم (فقط مشاهده)|بيئة الجهاز (للعرض فقط)
WinHTTP basic Machine (inspection)|WinHTTP پایهٔ سیستم (فقط مشاهده)|WinHTTP الأساسي للجهاز (للعرض فقط)
Git user|Git کاربر|Git للمستخدم
npm / pnpm user|npm / pnpm کاربر|npm / pnpm للمستخدم
pip user|pip کاربر|pip للمستخدم
NuGet user|NuGet کاربر|NuGet للمستخدم
Cargo user|Cargo کاربر|Cargo للمستخدم
Gradle user|Gradle کاربر|Gradle للمستخدم
Maven user|Maven کاربر|Maven للمستخدم
Process|پردازش|العملية
User|کاربر|المستخدم
Machine|سیستم|الجهاز
(unset)|(تنظیم نشده)|(غير مضبوط)
Flags|پرچم‌ها|الأعلام
Proxy|پراکسی|الوكيل
Proxy status unavailable|وضعیت پراکسی در دسترس نیست|حالة الوكيل غير متاحة
Quick Input|ورودی سریع|الإدخال السريع
characters|کاراکتر|محرف
words|کلمه|كلمة
Write your text here|متن خود را اینجا بنویسید|اكتب نصك هنا
Right-to-left writing direction|جهت نوشتن راست‌به‌چپ|اتجاه الكتابة من اليمين إلى اليسار
The writing direction could not be saved.|جهت نوشتن ذخیره نشد.|تعذر حفظ اتجاه الكتابة.
Multiline input|ورودی چندخطی|إدخال متعدد الأسطر
✕  Cancel|✕  لغو|✕  إلغاء
✓  Confirm|✓  تأیید|✓  تأكيد
No terminal was selected. Open a terminal, then recover this draft.|نشستی انتخاب نشده بود. یک ترمینال باز کنید و سپس این پیش‌نویس را بازیابی کنید.|لم تُحدد طرفية. افتح طرفية ثم استعد هذه المسودة.
The clipboard image is too large. Use an image file instead.|تصویر کلیپ‌بورد بیش از حد بزرگ است؛ فایل تصویر را پیوست کنید.|صورة الحافظة كبيرة جداً. أرفق ملف الصورة بدلاً منها.
Quick Input...|ورودی سریع...|الإدخال السريع...
Write, edit and insert text into the active terminal.|نوشتن، ویرایش و درج متن در ترمینال فعال.|اكتب النص وحرره وأدرجه في الطرفية النشطة.
Insert|درج|إدراج
Send|ارسال|إرسال
Recover draft|بازیابی پیش‌نویس|استعادة المسودة
Clear undo history|پاک‌کردن سابقهٔ ویرایش|مسح سجل التراجع
The clipboard is busy. Please try again.|کلیپ‌بورد در دسترس نیست. دوباره تلاش کنید.|الحافظة مشغولة. حاول مجدداً.
Invalid clipboard content.|محتوای کلیپ‌بورد معتبر نیست.|محتوى الحافظة غير صالح.
The destination terminal is no longer available. Your draft was preserved.|نشست مقصد دیگر در دسترس نیست. پیش‌نویس حفظ شد.|الطرفية المستهدفة لم تعد متاحة. تم الاحتفاظ بالمسودة.
Could not transfer text. Your draft was preserved.|انتقال متن انجام نشد. پیش‌نویس حفظ شد.|تعذر نقل النص. تم الاحتفاظ بالمسودة.
Write some text before inserting it.|ابتدا متنی بنویسید.|اكتب نصاً أولاً.
Nothing was inserted. Your draft was preserved.|متنی درج نشد. پیش‌نویس حفظ شد.|لم يُدرج نص. تم الاحتفاظ بالمسودة.
An attached file is no longer available.|یکی از فایل‌های پیوست دیگر در دسترس نیست.|أحد الملفات المرفقة لم يعد متاحاً.
The destination changed. Review it and try again.|وضعیت مقصد تغییر کرد. آن را بررسی و دوباره تلاش کنید.|تغيرت حالة الوجهة. راجعها وحاول مجدداً.
Stop macro playback before inserting text.|پیش از درج متن، اجرای ماکرو را متوقف کنید.|أوقف تشغيل الماكرو قبل إدراج النص.
Quick Input supports up to 1 MB of text. Shorten the text and try again.|ورودی سریع حداکثر یک مگابایت متن می‌پذیرد. متن را کوتاه‌تر کنید.|يدعم الإدخال السريع نصاً بحجم 1 ميغابايت كحد أقصى. اختصر النص.
Remove control characters from the text before inserting it.|پیش از درج، کاراکترهای کنترلی را از متن حذف کنید.|أزل محارف التحكم من النص قبل إدراجه.
This terminal has not enabled safe multiline paste.|این ترمینال درج امن متن چندخطی را فعال نکرده است.|لم تفعّل هذه الطرفية اللصق الآمن متعدد الأسطر.
This terminal has not enabled safe multiline paste. Insert the text as one line? Line breaks become spaces and tabs become four spaces. Your original draft is preserved.|این ورودی درج امن متن چندخطی را فعال نکرده است. آیا متن به صورت یک خط درج شود؟ شکست خطوط به فاصله و Tab به چهار فاصله تبدیل می‌شود. پیش‌نویس اصلی حفظ می‌شود.|لم تفعّل الطرفية اللصق الآمن متعدد الأسطر. هل تريد إدراج النص كسطر واحد؟ تتحول فواصل الأسطر إلى مسافات وTab إلى أربع مسافات. تبقى المسودة الأصلية محفوظة.
Terminal input timed out or was cancelled.|مهلت انتقال ورودی تمام شد یا انتقال لغو شد.|انتهت مهلة نقل الإدخال أو أُلغي.
The terminal input connection is closed.|اتصال ورودی ترمینال بسته است.|اتصال إدخال الطرفية مغلق.
All 20 temporary images are still in use. Remove an attachment and clear its undo history before adding another image.|هر ۲۰ تصویر موقت هنوز موردنیاز است. یک پیوست را حذف و سابقهٔ ویرایش را پاک کنید، سپس تصویر جدید اضافه کنید.|الصور المؤقتة العشرون لا تزال قيد الاستخدام. احذف مرفقاً وامسح سجل التراجع قبل إضافة صورة أخرى.
Some text may already have reached the terminal. No further Enter was sent. Your draft was preserved; check the terminal before retrying.|ممکن است بخشی از متن به ترمینال رسیده باشد. Enter دیگری ارسال نشد. پیش‌نویس حفظ شد؛ پیش از تلاش مجدد ترمینال را بررسی کنید.|ربما وصل جزء من النص إلى الطرفية. لم يُرسل Enter إضافي. احتُفظ بالمسودة؛ تحقق من الطرفية قبل المحاولة مجدداً.
The final Enter may already have reached the terminal. Your draft was preserved; check the terminal before retrying.|ممکن است Enter نهایی به ترمینال رسیده باشد. پیش‌نویس حفظ شد؛ پیش از تلاش مجدد ترمینال را بررسی کنید.|ربما وصل Enter النهائي إلى الطرفية. احتُفظ بالمسودة؛ تحقق من الطرفية قبل المحاولة مجدداً.
The destination terminal closed during transfer. Some input may have reached it. Your draft was preserved.|نشست مقصد هنگام انتقال بسته شد. ممکن است بخشی از ورودی به آن رسیده باشد. پیش‌نویس حفظ شد.|أُغلقت الطرفية أثناء النقل. ربما وصل جزء من الإدخال. تم الاحتفاظ بالمسودة.
Download|دانلود|تنزيل
Download from GitHub|دانلود از گیت‌هاب|تنزيل من GitHub
Only required font files are downloaded from GitHub.|فقط فایل‌های موردنیاز فونت از گیت‌هاب دانلود می‌شوند.|تُنزّل ملفات الخط المطلوبة فقط من GitHub.
This server or proxy does not support partial font downloads. No full archive was downloaded.|سرور یا پراکسی دانلود بخشی از فایل را پشتیبانی نمی‌کند. بستهٔ کامل دانلود نشد.|لا يدعم الخادم أو الوكيل التنزيل الجزئي. لم تُنزّل الحزمة الكاملة.
Font download|دانلود فونت|تنزيل الخط
The font download timed out. Check your connection and try again.|مهلت دانلود فونت تمام شد. اتصال را بررسی و دوباره تلاش کنید.|انتهت مهلة تنزيل الخط. تحقق من الاتصال وحاول مجدداً.
Font verification failed. Nothing was installed. Please try again.|فایل فونت تأیید نشد و نصب انجام نشد. دوباره تلاش کنید.|فشل التحقق من الخط ولم يتم تثبيته. حاول مجدداً.
The font folder is not writable. Check its permissions and try again.|امکان نوشتن در پوشهٔ فونت نیست. دسترسی پوشه را بررسی کنید.|تعذرت الكتابة في مجلد الخطوط. تحقق من الأذونات.
The font could not be downloaded or installed. Check your connection, available disk space, or another running download, and try again.|دانلود یا نصب فونت انجام نشد. اتصال، فضای خالی دیسک و دانلود هم‌زمان را بررسی و دوباره تلاش کنید.|تعذر تنزيل الخط أو تثبيته. تحقق من الاتصال ومساحة القرص أو تنزيل آخر قيد التشغيل وحاول مجدداً.
Check proxy connection|بررسی اطلاعات پراکسی|تحقق من بيانات الوكيل
Please enter a valid IP address or host and port first.|لطفاً ابتدا IP یا میزبان و پورت معتبر وارد کنید.|يرجى إدخال عنوان IP أو مضيف ومنفذ صالحين أولاً.
Set Proxy uses Host / IP, Port and Protocol from Configuration scope above.|دکمهٔ تنظیم پراکسی از میزبان، درگاه و پروتکل بخش بالای صفحه استفاده می‌کند.|يستخدم ضبط الوكيل المضيف والمنفذ والبروتوكول من قسم نطاق الإعداد أعلاه.
Only selected developer tools. Windows proxy settings remain unchanged.|فقط ابزارهای توسعهٔ انتخاب‌شده؛ تنظیمات پراکسی ویندوز تغییر نمی‌کنند.|أدوات التطوير المحددة فقط؛ لا تتغير إعدادات وكيل Windows.
Saved user configuration; environment or project overrides may still apply.|تنظیمات ذخیره‌شدهٔ کاربر؛ تنظیمات محیط یا پروژه ممکن است اولویت داشته باشند.|إعدادات المستخدم المحفوظة؛ قد تتجاوزها إعدادات البيئة أو المشروع.
Bypass|استثناها|الاستثناءات
ProxyBypass|استثناهای پراکسی|استثناءات الوكيل
Pac|اسکریپت PAC|برنامج PAC
AutoconfigUrl|نشانی تنظیم خودکار|عنوان التكوين التلقائي
AutoDetect|تشخیص خودکار|الاكتشاف التلقائي
Windows names are case-insensitive. Process values are inherited snapshots, not live persistent configuration. Restart parent applications to inherit changes.|نام متغیرها در ویندوز به بزرگی حروف حساس نیست. مقادیر پردازش ارث‌بری‌شده‌اند و با تنظیمات دائمی هم‌زمان نمی‌شوند. برای دریافت تغییرات برنامهٔ والد را دوباره اجرا کنید.|أسماء المتغيرات في Windows غير حساسة لحالة الأحرف. قيم العملية موروثة ولا تتزامن مع الإعدادات الدائمة. أعد تشغيل البرنامج الأصل لتلقّي التغييرات.
Confirm proxy changes|تأیید تغییرات پراکسی|تأكيد تغييرات الوكيل
Set persistent proxy configuration for:|تنظیم پراکسی دائمی برای:|ضبط إعدادات الوكيل الدائمة لـ:
Clear persistent proxy configuration for the listed scopes (including their NO_PROXY or bypass/PAC/auto-detection settings):|تنظیمات پراکسی محدوده‌های زیر، از جمله NO_PROXY و استثناها و PAC و تشخیص خودکار پاک شوند:|مسح إعدادات الوكيل للنطاقات التالية، بما فيها NO_PROXY والاستثناءات وPAC والاكتشاف التلقائي:
An encrypted recovery snapshot will be saved before changes.|پیش از تغییرات، نسخهٔ پشتیبان رمزگذاری‌شده ذخیره می‌شود.|ستُحفظ نسخة احتياطية مشفرة قبل التغييرات.
Administrator approval is required for Machine environment variables before any changes begin.|پیش از شروع تغییرات، تأیید مدیر برای متغیرهای محیطی سیستم لازم است.|تلزم موافقة المسؤول لمتغيرات بيئة الجهاز قبل بدء أي تغييرات.
Unselected scopes, project proxies and running processes may retain settings; removing bypass rules can affect those configurations.|محدوده‌های انتخاب‌نشده، پراکسی پروژه و پردازش‌های باز ممکن است تنظیمات قبلی را نگه دارند؛ حذف استثناها می‌تواند بر آن‌ها اثر بگذارد.|قد تحتفظ النطاقات غير المحددة ووكلاء المشاريع والعمليات المفتوحة بإعداداتها؛ وقد يؤثر حذف الاستثناءات عليها.
Confirm configuration restore|تأیید بازیابی تنظیمات|تأكيد استعادة الإعدادات
Restore exact saved configuration from {0}?|تنظیمات ذخیره‌شدهٔ {0} دقیقاً بازیابی شوند؟|هل تريد استعادة الإعدادات المحفوظة بتاريخ {0} بدقة؟
Changed providers:|بخش‌های تغییرکرده:|المكونات المتغيرة:
Newer values/files for these providers will be replaced. A complete backup of current settings will be created first.|مقادیر و فایل‌های جدیدتر این بخش‌ها جایگزین می‌شوند. ابتدا از تنظیمات فعلی نسخهٔ پشتیبان کامل گرفته می‌شود.|ستُستبدل القيم والملفات الأحدث لهذه المكونات. ستُنشأ أولاً نسخة احتياطية كاملة للإعدادات الحالية.
Clear proxy snapshots|پاک‌کردن نسخه‌های پشتیبان پراکسی|مسح النسخ الاحتياطية للوكيل
Delete all saved proxy snapshots and their recovery reminder?|تمام نسخه‌های پشتیبان پراکسی و یادآوری بازیابی حذف شوند؟|هل تريد حذف جميع نسخ الوكيل الاحتياطية وتذكير الاستعادة؟
Proxy settings will not change. Deleted snapshots cannot be restored.|تنظیمات پراکسی تغییر نمی‌کند. نسخه‌های حذف‌شده قابل بازیابی نیستند.|لن تتغير إعدادات الوكيل. لا يمكن استعادة النسخ المحذوفة.
Saved snapshots cleared. Proxy configuration was not changed.|نسخه‌های پشتیبان پاک شدند. تنظیمات پراکسی تغییر نکرد.|مُسحت النسخ الاحتياطية. لم تتغير إعدادات الوكيل.
An interrupted or partially failed operation was recorded. Review Configuration History before changing settings.|عملیاتی ناتمام یا ناموفق ثبت شده است. پیش از تغییر تنظیمات، تاریخچه را بررسی کنید.|سُجلت عملية متوقفة أو فاشلة جزئياً. راجع سجل الإعدادات قبل إجراء تغييرات.
Saving and verifying configuration…|در حال ذخیره و تأیید تنظیمات…|جارٍ حفظ الإعدادات والتحقق منها…
Please wait until configuration verification finishes.|لطفاً تا پایان تأیید تنظیمات صبر کنید.|يرجى الانتظار حتى انتهاء التحقق من الإعدادات.
Applying Proxy Settings|در حال اعمال تنظیمات پراکسی|جارٍ تطبيق إعدادات الوكيل
Clearing Proxy Settings|در حال پاک‌کردن تنظیمات پراکسی|جارٍ مسح إعدادات الوكيل
A recovery snapshot is verified before any configuration is changed.|پیش از تغییر هر تنظیمی، نسخهٔ پشتیبان تأیید می‌شود.|يجري التحقق من نسخة الاستعادة قبل تغيير أي إعداد.
Environment Proxy|پراکسی محیطی|وكيل البيئة
Windows System Proxy|پراکسی ویندوز|وكيل Windows
WinHTTP Proxy|پراکسی WinHTTP|وكيل WinHTTP
Pending|در انتظار|قيد الانتظار
Verified · managed settings|تأیید شد · تنظیمات مدیریت‌شده|تم التحقق · إعدادات مُدارة
Failed · see details|ناموفق · جزئیات را ببینید|فشل · راجع التفاصيل
Skipped · see details|انجام نشد · جزئیات را ببینید|تم التخطي · راجع التفاصيل
Clearing and verifying…|در حال پاک‌کردن و تأیید…|جارٍ المسح والتحقق…
Applying and verifying…|در حال اعمال و تأیید…|جارٍ التطبيق والتحقق…
Preparing operation…|در حال آماده‌سازی عملیات…|جارٍ تحضير العملية…
Reading configurations and preparing a verified recovery snapshot…|در حال خواندن تنظیمات و آماده‌سازی نسخهٔ پشتیبان معتبر…|جارٍ قراءة الإعدادات وتحضير نسخة استعادة متحقق منها…
Snapshot: reading {0}|نسخهٔ پشتیبان: خواندن {0}|نسخة احتياطية: قراءة {0}
Finalizing operation history…|در حال تکمیل تاریخچهٔ عملیات…|جارٍ إنهاء سجل العملية…
Requesting administrator approval for Machine environment. Cancelling stops this operation before any proxy changes.|در انتظار تأیید مدیر برای محیط سیستم. لغو، عملیات را پیش از هر تغییری متوقف می‌کند.|طلب موافقة المسؤول لبيئة الجهاز. يؤدي الإلغاء إلى إيقاف العملية قبل تغيير أي وكيل.
{0} / {1} processed · {2} verified · {3} failed · {4} skipped|{0} از {1} بررسی شد · {2} تأییدشده · {3} ناموفق · {4} انجام‌نشده|عولج {0} من {1} · تم التحقق من {2} · فشل {3} · تم تخطي {4}
Selected Proxy Settings Cleared!|تنظیمات پراکسی انتخاب‌شده پاک شدند!|مُسحت إعدادات الوكيل المحددة!
Selected Proxies Applied!|پراکسی‌های انتخاب‌شده اعمال شدند!|طُبقت إعدادات الوكلاء المحددة!
Some Proxy Settings Could Not Be Cleared|برخی تنظیمات پراکسی پاک نشدند|تعذر مسح بعض إعدادات الوكيل
Some Proxy Settings Could Not Be Applied|برخی تنظیمات پراکسی اعمال نشدند|تعذر تطبيق بعض إعدادات الوكيل
All selected managed configurations were cleared and verified. Existing processes and out-of-scope overrides may retain other settings.|تمام تنظیمات مدیریت‌شدهٔ انتخابی پاک و تأیید شدند. پردازش‌های باز و تنظیمات خارج از محدوده ممکن است مقادیر دیگری داشته باشند.|مُسحت جميع الإعدادات المُدارة المحددة وتم التحقق منها. قد تحتفظ العمليات المفتوحة والتجاوزات خارج النطاق بإعدادات أخرى.
All selected managed configurations were updated and verified. Existing processes and out-of-scope overrides may retain other settings.|تمام تنظیمات مدیریت‌شدهٔ انتخابی به‌روز و تأیید شدند. پردازش‌های باز و تنظیمات خارج از محدوده ممکن است مقادیر دیگری داشته باشند.|حُدثت جميع الإعدادات المُدارة المحددة وتم التحقق منها. قد تحتفظ العمليات المفتوحة والتجاوزات خارج النطاق بإعدادات أخرى.
Review the results below. Configuration History is available for recovery; no automatic retry was attempted.|نتایج را بررسی کنید. برای بازیابی از تاریخچه استفاده کنید؛ تلاش مجدد خودکار انجام نشده است.|راجع النتائج أدناه. سجل الإعدادات متاح للاستعادة؛ لم تُجرَ محاولة تلقائية أخرى.
Operation details|جزئیات عملیات|تفاصيل العملية
View Full Log…|نمایش گزارش کامل…|عرض السجل الكامل…
You may need to close and reopen the affected application for proxy changes to take effect.|ممکن است برای اعمال تغییرات پراکسی لازم باشد برنامهٔ موردنظر را یک‌بار ببندید و دوباره باز کنید.|قد تحتاج إلى إغلاق التطبيق المعني وإعادة فتحه لتصبح تغييرات الوكيل نافذة.
Proxy operation log|گزارش عملیات پراکسی|سجل عملية الوكيل
Full diagnostic log · sensitive values are redacted|گزارش کامل عیب‌یابی · مقادیر حساس پوشانده شده‌اند|سجل تشخيص كامل · القيم الحساسة محجوبة
Copy All|کپی همه|نسخ الكل
Copied|کپی شد|تم النسخ
Copy log|کپی گزارش|نسخ السجل
Copyable proxy diagnostic log|گزارش قابل کپی عیب‌یابی پراکسی|سجل تشخيص وكيل قابل للنسخ
The clipboard is busy. Select the log and use Ctrl+C or try again.|کلیپ‌بورد مشغول است. گزارش را انتخاب و از Ctrl+C استفاده کنید یا دوباره تلاش کنید.|الحافظة مشغولة. حدد السجل واستخدم Ctrl+C أو حاول مرة أخرى.
Saved and verified. Environment/project overrides and existing processes may retain other settings.|ذخیره و تأیید شد. تنظیمات محیط یا پروژه و پردازش‌های باز ممکن است مقادیر دیگری داشته باشند.|حُفظ وتم التحقق منه. قد تحتفظ تجاوزات البيئة أو المشروع والعمليات المفتوحة بإعدادات أخرى.
Already matches the requested managed configuration; re-read verified. No write was needed. Existing processes and out-of-scope overrides may retain other settings.|از قبل با تنظیمات درخواستی یکسان است و دوباره تأیید شد؛ نوشتن لازم نبود. پردازش‌های باز و تنظیمات خارج از محدوده ممکن است متفاوت باشند.|يطابق الإعداد المطلوب بالفعل وتم التحقق مجدداً؛ لم تلزم كتابة. قد تختلف إعدادات العمليات المفتوحة والتجاوزات خارج النطاق.
Operation interrupted; configuration state requires inspection in History.|عملیات متوقف شد؛ وضعیت تنظیمات را در تاریخچه بررسی کنید.|توقفت العملية؛ راجع حالة الإعدادات في السجل.
Not started: {0}|شروع نشد: {0}|لم يبدأ: {0}
UAC was cancelled. No proxy settings were changed.|درخواست UAC لغو شد. هیچ تنظیم پراکسی تغییر نکرد.|أُلغي طلب UAC. لم تتغير إعدادات الوكيل.
Requires permission: access denied or organization policy. No success was assumed.|مجوز لازم است: دسترسی یا سیاست سازمان مانع شد. موفقیتی اعلام نشده است.|يلزم إذن: رُفض الوصول أو منعت سياسة المؤسسة الإجراء. لم يُفترض النجاح.
Windows error {0} (0x{1}): {2}|خطای ویندوز {0} (0x{1}): {2}|خطأ Windows رقم {0} (0x{1}): {2}
Complete backup blocked: adapter version or configuration location could not be verified for {0}. No proxy configuration has been changed.|پشتیبان‌گیری کامل متوقف شد: نسخه یا مسیر تنظیمات {0} تأیید نشد. هیچ تنظیم پراکسی تغییر نکرد.|مُنعت النسخة الكاملة: تعذر التحقق من إصدار أو موقع إعدادات {0}. لم تتغير إعدادات الوكيل.
Snapshot/read failed for {0}: {1}|خواندن نسخهٔ پشتیبان برای {0} ناموفق بود: {1}|فشلت قراءة النسخة الاحتياطية لـ {0}: {1}
Another proxy operation is in progress. Wait for it to finish.|عملیات پراکسی دیگری در جریان است. تا پایان آن صبر کنید.|توجد عملية وكيل أخرى قيد التنفيذ. انتظر انتهاءها.
Conflict detected: configuration changed after the snapshot and does not match the requested state. Changed fields: {0}. Refresh and retry.|تعارض: تنظیمات پس از نسخهٔ پشتیبان تغییر کرده و با وضعیت درخواستی یکسان نیست. فیلدها: {0}. بازخوانی و دوباره تلاش کنید.|تعارض: تغيرت الإعدادات بعد النسخة ولا تطابق الحالة المطلوبة. الحقول: {0}. حدّث وحاول مجدداً.
Read-back differs from requested configuration. Recovery is available in Configuration History.|نتیجهٔ خواندن با تنظیمات درخواستی متفاوت است. بازیابی از تاریخچه در دسترس است.|تختلف القراءة اللاحقة عن الإعداد المطلوب. الاستعادة متاحة في سجل الإعدادات.
Machine environment requires UAC approval. Retry from Proxy Manager.|محیط سیستم به تأیید UAC نیاز دارد. از مدیریت پراکسی دوباره تلاش کنید.|تتطلب بيئة الجهاز موافقة UAC. أعد المحاولة من إدارة الوكيل.
Complete (managed scope)|کامل (محدودهٔ مدیریت‌شده)|مكتمل (النطاق المُدار)
Save proxies|ذخیرهٔ پراکسی‌ها|حفظ الوكلاء
Clear proxies|پاک‌کردن پراکسی‌ها|مسح الوكلاء
Before restore {0}|پیش از بازیابی {0}|قبل استعادة {0}
Restored|بازیابی شد|تمت الاستعادة
Skipped|انجام نشد|تم التخطي
Conflict|تعارض|تعارض
RequiresPermission|نیازمند مجوز|يلزم إذن
Enter a host or IP without a scheme, credentials or path, and a port between 1 and 65535.|میزبان یا IP را بدون پروتکل، اطلاعات ورود و مسیر وارد کنید؛ درگاه باید بین ۱ و ۶۵۵۳۵ باشد.|أدخل المضيف أو IP دون بروتوكول أو بيانات اعتماد أو مسار، ومنفذاً بين 1 و65535.
Bypass rules contain control characters.|فهرست استثناها دارای نویسهٔ کنترلی نامعتبر است.|تحتوي قواعد الاستثناء على محارف تحكم غير صالحة.
PAC must be an HTTP/HTTPS URL without credentials.|نشانی PAC باید HTTP یا HTTPS و بدون اطلاعات ورود باشد.|يجب أن يكون عنوان PAC من نوع HTTP أو HTTPS ودون بيانات اعتماد.
Windows manual proxy supports the HTTP endpoint in this manager. Select HTTP; SOCKS5 and HTTPS endpoints are not silently converted.|پراکسی دستی ویندوز در این بخش فقط HTTP را پشتیبانی می‌کند. HTTP را انتخاب کنید؛ SOCKS5 و HTTPS خودکار تبدیل نمی‌شوند.|يدعم الوكيل اليدوي في هذه الأداة نقطة HTTP فقط. اختر HTTP؛ لا يُحوّل SOCKS5 أو HTTPS تلقائياً.
WinHTTP requires an HTTP proxy endpoint. HTTPS and SOCKS5 endpoints are not supported by this provider.|WinHTTP به پراکسی HTTP نیاز دارد. این بخش از HTTPS و SOCKS5 پشتیبانی نمی‌کند.|يتطلب WinHTTP نقطة وكيل HTTP. لا يدعم هذا المكوّن HTTPS أو SOCKS5.
Advanced WinHTTP configuration is not available on this Windows version. No reset or write will be attempted.|تنظیمات پیشرفتهٔ WinHTTP در این نسخهٔ ویندوز در دسترس نیست. بازنشانی یا نوشتن انجام نمی‌شود.|إعدادات WinHTTP المتقدمة غير متاحة في إصدار Windows هذا. لن تُجرى إعادة تعيين أو كتابة.
WinHTTP is configured at Machine scope. Machine writes are not available in this User-only dialog; no setting has been changed.|WinHTTP در محدودهٔ سیستم تنظیم شده است. تغییر WinHTTP سیستم در این پنجره پشتیبانی نمی‌شود؛ هیچ تنظیمی تغییر نکرد.|ضُبط WinHTTP في نطاق الجهاز. لا يدعم هذا الحوار تعديل WinHTTP للجهاز؛ لم يتغير أي إعداد.
{0} does not support the selected proxy protocol.|بخش {0} از پروتکل انتخاب‌شده پشتیبانی نمی‌کند.|لا يدعم {0} بروتوكول الوكيل المحدد.
Operation failed ({0}). No success was assumed.|عملیات ناموفق بود ({0}). موفقیتی اعلام نشده است.|فشلت العملية ({0}). لم يُفترض النجاح.
Conflict detected: configuration changed while verifying the already-matching state. Refresh and retry.|تعارض: تنظیمات هنگام تأیید دوباره تغییر کرد. بازخوانی و دوباره تلاش کنید.|تعارض: تغيرت الإعدادات أثناء إعادة التحقق. حدّث وحاول مجدداً.
Skipped: inspection-only scope differs from the snapshot. Manual recovery is required.|انجام نشد: محدودهٔ فقط‌خواندنی با نسخهٔ پشتیبان متفاوت است. بازیابی دستی لازم است.|تم التخطي: يختلف نطاق العرض فقط عن النسخة الاحتياطية. تلزم استعادة يدوية.
Machine elevation requires the packaged RtlTerminal.exe. Run the preview executable rather than dotnet run.|ارتقای دسترسی به فایل RtlTerminal.exe نیاز دارد. نسخهٔ اجرایی را به‌جای dotnet run باز کنید.|تتطلب ترقية الصلاحيات ملف RtlTerminal.exe. شغّل الملف التنفيذي بدلاً من dotnet run.
Application executable could not be located.|فایل اجرایی برنامه پیدا نشد.|تعذر العثور على الملف التنفيذي للتطبيق.
Conflict detected: Machine proxy variables changed after snapshot.|تعارض: متغیرهای پراکسی سیستم پس از نسخهٔ پشتیبان تغییر کردند.|تعارض: تغيرت متغيرات وكيل الجهاز بعد النسخة الاحتياطية.
Conflict detected: a Machine proxy variable changed during mutation.|تعارض: یکی از متغیرهای پراکسی سیستم حین تغییر تنظیمات عوض شد.|تعارض: تغير أحد متغيرات وكيل الجهاز أثناء التعديل.
Could not start Machine proxy helper.|راه‌اندازی بخش کمکی پراکسی سیستم ممکن نشد.|تعذر بدء مساعد وكيل الجهاز.
Machine environment registry key could not be opened for writing.|کلید رجیستری محیط سیستم برای نوشتن باز نشد.|تعذر فتح مفتاح سجل بيئة الجهاز للكتابة.
Machine helper did not verify permission to write the Machine environment.|بخش کمکی، مجوز نوشتن محیط سیستم را تأیید نکرد.|لم يتحقق المساعد من إذن كتابة بيئة الجهاز.
Machine helper disconnected; inspect Machine settings before retrying.|ارتباط بخش کمکی قطع شد؛ پیش از تلاش مجدد تنظیمات سیستم را بررسی کنید.|انقطع اتصال المساعد؛ افحص إعدادات الجهاز قبل إعادة المحاولة.
Machine helper exited before authentication. Approve UAC using the same Windows account; alternate-account elevation is not supported. No proxy settings were changed.|بخش کمکی پیش از احراز هویت بسته شد. UAC را با همان حساب ویندوز تأیید کنید؛ حساب مدیر متفاوت پشتیبانی نمی‌شود. تنظیمات پراکسی تغییر نکرد.|أُغلق المساعد قبل المصادقة. وافق على UAC بالحساب نفسه؛ الترقية بحساب آخر غير مدعومة. لم تتغير إعدادات الوكيل.
Machine helper identity did not match the launched process.|هویت بخش کمکی با پردازش اجراشده یکسان نبود.|لم تتطابق هوية المساعد مع العملية المُشغلة.
Machine proxy read-back did not match the request.|نتیجهٔ خواندن پراکسی سیستم با درخواست یکسان نبود.|لم تتطابق القراءة اللاحقة لوكيل الجهاز مع الطلب.
Machine requests may contain only the allowlisted proxy variables.|درخواست سیستم فقط می‌تواند شامل متغیرهای پراکسی مجاز باشد.|لا يجوز أن تتضمن طلبات الجهاز إلا متغيرات الوكيل المسموح بها.
Duplicate provider identities in snapshot.|شناسهٔ بخش‌ها در نسخهٔ پشتیبان تکراری است.|هويات المكونات مكررة في النسخة الاحتياطية.
History directory must not be a symbolic link.|پوشهٔ تاریخچه نباید پیوند نمادین باشد.|يجب ألا يكون مجلد السجل رابطاً رمزياً.
Snapshot exceeds the safe 32 MB limit. No changes were made.|نسخهٔ پشتیبان از حد امن ۳۲ مگابایت بزرگ‌تر است. تغییری انجام نشد.|تتجاوز النسخة الاحتياطية الحد الآمن 32 ميغابايت. لم تُجرَ تغييرات.
Snapshot integrity validation failed.|اعتبارسنجی یکپارچگی نسخهٔ پشتیبان ناموفق بود.|فشل التحقق من سلامة النسخة الاحتياطية.
Snapshot is incomplete or uses an unsupported schema.|نسخهٔ پشتیبان ناقص است یا قالب پشتیبانی‌نشده‌ای دارد.|النسخة الاحتياطية غير مكتملة أو تستخدم تنسيقاً غير مدعوم.
Conflict detected after restore preview. Review the current configuration again.|پس از پیش‌نمایش بازیابی تعارض ایجاد شد. تنظیمات فعلی را دوباره بررسی کنید.|اكتُشف تعارض بعد معاينة الاستعادة. راجع الإعدادات الحالية مجدداً.
Inspection scope changed; explicit administrative/manual recovery is required.|محدودهٔ فقط‌خواندنی تغییر کرده است؛ بازیابی دستی با مجوز مناسب لازم است.|تغير نطاق العرض فقط؛ تلزم استعادة يدوية بالصلاحيات المناسبة.
Machine WinHTTP differs from the snapshot; explicit administrative recovery is required.|WinHTTP سیستم با نسخهٔ پشتیبان متفاوت است؛ بازیابی با مجوز مدیر لازم است.|يختلف WinHTTP للجهاز عن النسخة الاحتياطية؛ تلزم استعادة بصلاحيات المسؤول.
Configuration command failed, exit code {0}. Check permissions and tool configuration.|فرمان تنظیمات با کد {0} ناموفق بود. مجوزها و تنظیمات ابزار را بررسی کنید.|فشل أمر الإعداد برمز خروج {0}. تحقق من الأذونات وإعدادات الأداة.
Proxy settings are managed by policy.|تنظیمات پراکسی تحت سیاست مدیریتی هستند.|تخضع إعدادات الوكيل لسياسة إدارية.
ProxyOverride has an unsupported registry type; snapshot blocked.|نوع رجیستری ProxyOverride پشتیبانی نمی‌شود؛ پشتیبان‌گیری متوقف شد.|نوع سجل ProxyOverride غير مدعوم؛ مُنعت النسخة الاحتياطية.
Settings written, but Windows notification failed. Refresh and inspect the resulting state.|تنظیمات نوشته شد، اما اطلاع‌رسانی به ویندوز ناموفق بود. وضعیت نهایی را بازخوانی و بررسی کنید.|كُتبت الإعدادات لكن فشل إشعار Windows. حدّث وافحص الحالة الناتجة.
A proxy entry uses continuation or inline comments; edit it manually to avoid data loss.|یکی از تنظیمات پراکسی چندخطی یا دارای توضیح درون‌خطی است؛ برای حفظ داده آن را دستی ویرایش کنید.|أحد إعدادات الوكيل متعدد الأسطر أو يحوي تعليقاً مضمناً؛ حرره يدوياً لتجنب فقدان البيانات.
Refusing to replace a linked configuration file.|جایگزینی فایل تنظیمات پیوندشده مجاز نیست.|لا يُسمح باستبدال ملف إعدادات مرتبط.
Git configuration directory does not exist; create it explicitly before using this adapter.|پوشهٔ تنظیمات Git موجود نیست؛ پیش از استفاده آن را ایجاد کنید.|مجلد إعدادات Git غير موجود؛ أنشئه قبل استخدام هذا المكوّن.
Maven has another active proxy. Resolve it manually; this manager will not disable unrelated proxy profiles.|Maven پراکسی فعال دیگری دارد. آن را دستی بررسی کنید؛ این ابزار پروفایل‌های نامرتبط را غیرفعال نمی‌کند.|لدى Maven وكيل نشط آخر. عالجه يدوياً؛ لن تعطل هذه الأداة ملفات الوكلاء غير المرتبطة.
Tool inventory could not be refreshed. Restore blocked.|فهرست ابزارها بازخوانی نشد. بازیابی متوقف شد.|تعذر تحديث قائمة الأدوات. مُنعت الاستعادة.
{0}: requested state is already present; verifying again without rewriting.|{0}: وضعیت درخواستی موجود است؛ تأیید دوباره بدون نوشتن انجام می‌شود.|{0}: الحالة المطلوبة موجودة بالفعل؛ يجري التحقق مجدداً دون كتابة.
""";
}
