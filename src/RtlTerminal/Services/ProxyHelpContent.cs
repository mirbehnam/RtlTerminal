namespace RtlTerminal;

public sealed record ProxyHelpSection(string Title, string Body);

public static class ProxyHelpContent
{
    public static IReadOnlyList<ProxyHelpSection> Get(UiLanguage language)
    {
        string L(string en, string fa, string ar) => ShortcutCatalog.Text(language, en, fa, ar);
        return [
            new(L("Why use it?", "چه کمکی می‌کند؟", "كيف يساعدك؟"), L(
                "Manage Windows and developer-tool proxy settings in one place. Bring your own proxy service; this is not a VPN.",
                "تنظیمات پراکسی ویندوز و ابزارهای توسعه را یک‌جا مدیریت کنید. این ابزار از پراکسی موجود شما استفاده می‌کند؛ خودش VPN نیست.",
                "أدر إعدادات وكيل Windows وأدوات التطوير من مكان واحد. استخدم خدمة وكيل موجودة؛ هذه الأداة ليست VPN.")),
            new(L("Set or clear", "تنظیم یا پاک‌کردن", "التعيين أو المسح"), L(
                "Enter your proxy host and port. Choose All or one scope, then Set or Clear. Machine changes ask for administrator approval.",
                "میزبان و پورت پراکسی خود را وارد کنید. همه یا یک محدوده را انتخاب و سپس تنظیم یا پاک کنید. تغییرات سیستمی نیازمند تأیید مدیر است.",
                "أدخل مضيف الوكيل ومنفذه. اختر الكل أو نطاقاً واحداً ثم التعيين أو المسح. تتطلب تغييرات الجهاز موافقة المسؤول.")),
            new(L("Developer tools are optional", "ابزارهای توسعه اختیاری‌اند", "أدوات التطوير اختيارية"), L(
                "Off by default. Enable the checkbox and select supported tools to change their own proxy settings too.",
                "این گزینه ابتدا خاموش است. برای تغییر تنظیمات اختصاصی ابزارها، تیک آن را بزنید و ابزارهای پشتیبانی‌شده را انتخاب کنید.",
                "معطلة افتراضياً. فعّل الخيار وحدد الأدوات المدعومة لتغيير إعدادات الوكيل الخاصة بها أيضاً.")),
            new(L("Status, recovery and restart", "وضعیت، بازیابی و راه‌اندازی دوباره", "الحالة والاستعادة وإعادة التشغيل"), L(
                "Click cards for details; green does not test connectivity. Ten encrypted backups allow recovery. Restart affected apps after environment changes.",
                "برای جزئیات روی کارت‌ها کلیک کنید؛ سبز بودن به معنی آزمایش اتصال نیست. ده پشتیبان رمزگذاری‌شده برای بازیابی دارید. پس از تغییر متغیرهای محیطی، برنامهٔ مربوط را دوباره باز کنید.",
                "انقر البطاقات للتفاصيل؛ الأخضر لا يعني اختبار الاتصال. تتوفر عشر نسخ احتياطية مشفرة للاستعادة. أعد تشغيل التطبيقات المتأثرة بعد تغيير متغيرات البيئة."))
        ];
    }
}
