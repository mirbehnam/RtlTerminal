# Terminal macros

The Tools macro menu lists recordings in creation-date order, oldest first.
Renaming or editing a recording does not move it. Existing recordings without
creation metadata use their file creation date, preserved when next saved.

Open **Tools → Macros → Start recording** in the terminal pane you want to record. Type the command and respond to its prompts. Choose **Stop recording** (the same menu item), or click the recording indicator beside the preview controls. Enter a name, optionally assign a password and a shortcut, review the expected prompts, and save.

**Tools → Macros → Run** lists saved recordings. **Manage recorded macros** lets you search by name, run, step through, rename, assign shortcuts, change password protection, or delete a recording. The editor has a numbered step list, input/key descriptions, per-step conditions and sensitivity. Add, duplicate, reorder and delete steps; input stays hidden until you choose **Reveal / edit input**. **Macro settings** opens the Macros tab of the application Settings dialog; changes take effect with Apply.

- Interactive mode is the default. New submissions wait for the recorded prompt at the terminal cursor; the first command starts immediately. Editing keys remain in order without requiring a new prompt. Interrupts (`Ctrl+C`, `Ctrl+D`, `Ctrl+Z`) and Escape are independent steps using their recorded delay. Their next submission waits for the new prompt. Every step can instead use **Wait for prompt**, **After a delay**, or **Immediately**. Matching uses logical text after escape-sequence interpretation, including wrapped prompts. Review prompts before saving, especially if input was entered before the server was ready.
- Timed mode records each input write and its elapsed delay. Use it for workflows without stable prompts; it does not wait for server readiness.
- Interactive timeout is configurable from 5 to 600 seconds. Unrelated output does not immediately fail the macro. On timeout, playback waits for review: **Wait again** with the same prompt, edit the condition and wait again, or **End run**. Edits affect this run only. A prior command is never automatically resent and the failing step is never skipped. A closed target ends playback.
- The toolbar shows state and the target pane, with separate pause/resume, single-step and stop controls. **Ctrl+Shift+Alt+P** pauses/resumes; **Ctrl+Shift+Alt+End** stops. These shortcuts can be disabled separately in Settings. Escape is no longer the playback stop shortcut. Pausing freezes future sends and local delay/timeout accounting, not the server. A prompt is rechecked after a pause or runtime-input dialog. Stopping does not undo commands already sent.
- Recording and playback remain bound to the original pane when tabs or focus change. Input from other panes is not recorded. Manual text/paste in the playback pane is blocked while it runs. Terminal protocol replies and mouse/focus reports are not recorded.
- Optional shortcuts are **Ctrl+Shift+Alt+1** through **Ctrl+Shift+Alt+9**. Each number can belong to only one macro. Disable these shortcuts in Settings → Macros to pass them through to the terminal.

## Keys and a Ctrl+C example

Control combinations, navigation/editing keys and function keys are recorded by default. Settings provides three group switches and semicolon-separated exclusions, such as `Ctrl+L; F5; Shift+Tab`. Modifier order and case are normalized. Enter continues to delimit submissions when navigation recording is disabled; it can be explicitly excluded. Excluding a key changes the recorded workflow but never blocks that key from reaching the terminal. A recording-limit error stops recording while still delivering the user's input, including interrupts.

For a long-running command: start recording, run the command, wait, press Ctrl+C, wait for the shell prompt, then enter another command. The saved macro sends the first command, waits the recorded interval before Ctrl+C, and waits for the shell prompt before sending the next command. Ctrl+C used to copy selected text is an application action and is not recorded as an interrupt. Tab switching and other application shortcuts are not recorded. Key steps use the current terminal cursor-key mode when replayed.

## Sensitive input and runtime variables

Automatic detection is on by default. It recognizes common password/passphrase/PIN/OTP/token prompts (including Persian), masked input, and input whose echo could not be confirmed. Slow, unusual, wrapped, or full-screen applications can produce conservative false positives. The ConPTY connection does **not** expose a universal password-field type, particularly for SSH/WSL. Detection is not a guarantee; the editor shows its reason and supports **Auto**, **Sensitive**, and **Normal** overrides.

Encrypted storage remains the default. Enable **Do not store sensitive values; ask when running the macro** to remove sensitive values before saving. A sensitive submission split by editing keys becomes one runtime input with its final Enter preserved; the replacement is entered as the final value, not the original typo/backspace sequence. Expected prompts containing a removed value are cleared and must be replaced with a safe condition before saving. Runtime answers are never written back to the macro, registry, or logs.

Existing files are not silently rewritten. With this setting enabled, sensitive steps are also requested on playback, but old disk values are only removed when that macro is opened and saved again. Independent file passwords must still be supplied to open protected macros. Detection metadata is absent from original v1 files; review and mark their sensitive steps when editing them.

The editor's collapsed **Advanced** section can request an entire step's value under a label such as `Server` or `Username`. A label is an explicit variable, not implicit string-template substitution; other text is not interpreted. Values are requested per step and per run, and can be marked sensitive. **Send Enter after the runtime answer** controls submission. Input dialogs appear after the step's condition is satisfied; the condition is rechecked before sending. Time spent answering does not consume the server prompt timeout. Cancel ends the run. The file-unlock password is separate from any server credential.

## Storage and passwords

Recordings are stored in `%LOCALAPPDATA%\RtlTerminal\Macros` as encrypted `.rtmacro` files. The folder has a restricted Windows ACL. All file contents, including names and prompt text, are protected with Windows DPAPI for the current user. Files are written atomically; there are no plaintext macro files or input logs.

An optional independent password adds AES-256-GCM authenticated encryption with a random salt and nonce, and PBKDF2-SHA256 (600,000 iterations). The password is requested for each run or edit and is not saved. Names and shortcut metadata remain accessible to the signed-in Windows account. Forgotten passwords cannot be recovered.

DPAPI alone is not a security boundary against software already running as the same Windows user. Use an independent strong password for sensitive macros. Neither encryption option protects against malware inspecting the process while a macro is unlocked, or a compromised server. The terminal and target program can still echo or log input according to their own behavior. Input values are masked in the macro editor.

## Scope and testing

Interactive mode targets command/prompt workflows; it is not a universal automation engine for changing full-screen TUIs. Prompt text must be stable. Multiline paste is recorded as one input write, so use individual submissions when subsequent lines must wait for different prompts. Timed mode reproduces input timing without interpreting application state.

New saves use storage envelope v2. Envelope v1 remains readable and original steps retain their original behavior; old control characters are not guessed into new action boundaries. Conversion occurs on explicit save, with atomic replacement and the same encryption protections.

The render test suite includes real ConPTY login with a split ANSI prompt and hidden password, Ctrl+C interruption of continuous output followed by a new command, partial-input interruption, editing keys, key exclusions, sensitivity overrides, delayed echo, runtime redaction inspected inside the DPAPI payload, cancelled answers, pause/step/recovery, v1 compatibility, encryption/password/tamper checks, and live pane isolation. UI previews are generated under `artifacts/render-checks`.

## راهنمای کوتاه فارسی

از **Tools → Macros** ضبط را شروع کنید. کلیدهای تعاملی به‌صورت پیش‌فرض ضبط می‌شوند؛ Ctrl+C بدون انتخاب متن یک مرحلهٔ مستقل است. گروه‌ها و استثناهای ضبط در **Settings → Macros** قابل تنظیم‌اند. با توقف ضبط، مراحل شماره‌دار، شرط اجرا و وضعیت حساس‌بودن هر ورودی را مرور کنید.

ذخیرهٔ رمزگذاری‌شده پیش‌فرض است. برای ذخیره‌نشدن اطلاعات حساس، گزینهٔ پرسیدن هنگام اجرا را فعال کنید. تشخیص خودکار از اعلان رمز، پوشاندن ورودی و تأییدنشدن بازتاب متن استفاده می‌کند؛ نتیجه را می‌توان دستی اصلاح کرد. فایل‌های قدیمی تنها با ذخیرهٔ مجدد پاک‌سازی می‌شوند. در **Advanced** می‌توانید نام سرور، نام کاربری یا ورودی دیگر را برای پرسیدن در زمان اجرا تعریف کنید.

پاسخ متفاوت سرور تا پایان مهلت منتظر می‌ماند؛ سپس امکان انتظار مجدد، اصلاح شرط برای همان اجرا یا پایان اجرا نمایش داده می‌شود. فرمان قبلی تکرار نمی‌شود. میانبر مکث/ادامه **Ctrl+Shift+Alt+P** و پایان اجرا **Ctrl+Shift+Alt+End** است. مکث ماکرو، سرور را متوقف نمی‌کند. مدیریت ماکروها جست‌وجو و اجرای مرحله‌ای دارد.

Terminal macros are available on the main branch. See the README for build and release instructions.
