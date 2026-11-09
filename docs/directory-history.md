# Directory history

## Opening a saved directory

**Settings → History → Open recent directories** offers three modes, saved per user:

- **Smart mode** (default): reuse an untouched tab; open a new tab after any terminal
  input, including pasted text, Quick Input, macros and interactive keys. Deleting
  previously typed text does not make a tab untouched again. Terminal output alone
  does not count as user input.
- **Always open a new tab**: leave existing sessions intact.
- **Always use the current tab**: an untouched pane is replaced directly. If the
  tab has received input, a themed **Yes / No / New tab** confirmation appears.
  No and Escape preserve the session. Yes closes and replaces the active session.

In split tabs, only the active pane is replaced; neighboring panes keep running.
The saved shell and WSL distribution are used in all modes. The same preference
applies to menu entries and selections from the full directory-history window.
Macro recording/playback must be stopped before replacing its target terminal.

The toolbar folder button after Help shows the last component of the selected pane's live directory. Its tooltip shows the full path; click to use the same Explorer action as the File menu. It follows pane/tab selection and directory changes even when history recording is disabled. Long names are truncated to keep the toolbar compact. A drive root is shown as its drive path; an unknown directory remains disabled until detected.

**File → Last directories** shows the 20 most recently entered directories across tabs and split panes. Visits are detected from each pane, including inactive panes, rather than only recording CMD's startup folder. Command Prompt, PowerShell and WSL retain their shell/distribution information when a history entry is reopened in a new tab.

When more than 20 entries exist, **More…** opens a searchable window containing up to 200 unique directories, newest first. With fewer entries, **Manage history…** opens the same window, so clearing is always accessible. Search matches paths and shell names. Select a row and choose **Open terminal**, or double-click it. Unavailable directories remain in history; opening one reports that it is unavailable.

Each row shows the local date and time of the last observed entry into the directory. Its tooltip includes the full path, shell, first observed visit and last visit. Repeated output or polling while a pane stays in the same directory does not move the entry or change its timestamp. Returning to a directory, or opening it in a new pane, records a new visit while retaining the first known visit. Windows paths are deduplicated without case sensitivity; WSL paths retain case distinctions.

**Clear history** requires confirmation and removes all saved entries, including the old CMD-only list. It does not delete directories. Existing panes remaining in their current directories do not immediately refill the cleared list; later visits are recorded while recording remains enabled.

In **Edit → Settings → History**, turn off **Remember visited directories** and choose Apply to stop recording. Existing entries remain available until cleared. Cancel does not change the preference.

Shell directory reports are observed directly. A background check every three seconds reads each running pane's current directory as a fallback, including WSL shells without prompt integration. It does not paste commands into the terminal input. Very brief visits between polls may not be detected when a shell does not report directory changes; times reflect observation by this terminal, not filesystem creation times.

The previous ten-entry CMD history is imported. Its dates were never recorded and are shown as unavailable until a visit is observed; timestamps are not fabricated. Settings and history are stored per Windows user in `HKCU\Software\RtlTerminal` (`DirectoryHistoryEnabled`, `DirectoryHistoryV2`). The bounded history contains paths, shell/distribution names and visit times.

The startup introduction randomly displays either `YouTube: @aka_techno` or `Instagram: @BehnamTjd`. Split panes still omit the introduction.

## فارسی

منوی **File → Last directories** تغییر مسیر در تب‌ها و اسپلیت‌های CMD، پاورشل و WSL را ثبت می‌کند. ۲۰ مسیر اخیر در منو و حداکثر ۲۰۰ مسیر در **More…** نمایش داده می‌شوند. برای کمتر از ۲۰ مسیر، **Manage history…** همان پنجره را باز می‌کند.

کنار هر مسیر، تاریخ و ساعت آخرین ورودِ مشاهده‌شده توسط همین ترمینال نمایش داده می‌شود؛ زمان اولین ورود نیز در راهنمای ردیف قرار دارد. خروجی‌های تکراری و بررسی دوره‌ای، زمان مسیر فعلی را تغییر نمی‌دهند. زمان مسیرهای قدیمی که قبلاً ثبت نشده، ساخته نمی‌شود. در شل‌های بدون گزارش مسیر، تشخیص دوره‌ای ممکن است تا سه ثانیه تأخیر داشته باشد.

با **Clear history** تاریخچه پاک می‌شود و با **Settings → History → Remember visited directories** می‌توان ثبت را غیرفعال کرد. غیرفعال‌کردن، سابقهٔ موجود را پاک نمی‌کند؛ پاک‌کردن نیز پوشه‌های واقعی را حذف نمی‌کند. در معرفی آغاز برنامه به‌طور تصادفی یوتیوب یا اینستاگرام **BehnamTjd** نمایش داده می‌شود.
