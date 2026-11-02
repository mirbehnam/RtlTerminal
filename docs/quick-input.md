# Quick Input

Available starting with RTL Terminal 1.1.6.

Open **Tools → Quick Input…** or press **Ctrl+Shift+Space** anywhere in the main
terminal window, including menus, search and fullscreen. Settings and other modal
dialogs do not activate this shortcut. The icon beside browser preview is always
visible; opening the editor never depends on CLI detection or input-ready markers.

Write new text in the native editor. It never reads or edits the CLI's existing
input. Enter and Shift+Enter create new lines. **Insert / Ctrl+Shift+Enter** writes
the content without a final Enter; **Send / Ctrl+Enter** writes the content and
then, after a cancellable 200 ms paste-processing pause, one final CR.
**Close / Escape** preserves the draft without sending input.

| Action | Shortcut | Result |
| --- | --- | --- |
| Send | Ctrl+Enter | Same action as clicking Send: paste, wait 200 ms, then send one Enter. |
| Insert | Ctrl+Shift+Enter | Paste only; the CLI receives no final Enter. |
| New line | Enter or Shift+Enter | Edit the Quick Input draft only; send nothing to the CLI. |

Ctrl+Shift+Enter deliberately does not submit a Codex prompt. Use Ctrl+Enter or
the Send button to submit. Mouse and keyboard invoke the same button action;
unrelated modifier combinations do not trigger a transfer.
Standard selection, copy, cut, paste, undo and redo operate inside the editor.
Hover Close, Insert or Send to see Escape, Ctrl+Shift+Enter or Ctrl+Enter.
Shortcuts are also shown beneath the larger footer buttons. An empty-editor hint
explains Insert/Send, the writing area uses a distinct theme background, and the
lower-left counter shows Unicode characters and whitespace-separated words.
Attachment tokens count as one character and one word. Counts are debounced so
normal typing does not rescan the entire document on every keystroke.
The hint uses short separate lines without punctuation or ellipses in English,
Persian and Arabic to keep RTL display predictable.
The compact RTL button in the header is enabled on first use. It switches the
editor's writing direction without moving the surrounding controls or changing
the text, and remembers the last choice in the current user's settings.
Native Ctrl+Shift+Left/Right can change paragraph direction. Visual wrapping does
not add line breaks. Clipboard rich formatting is discarded.

The exact pane/session is captured when opening, including split panes. Drafts
are separate per session, stay only in memory, and are cleared after a successful
transfer. Reopening reuses the editor and its undo history. Closed-session drafts
can be explicitly recovered from the small recovery menu into another draft;
they are never automatically redirected. Dialog size, but not text, is saved.

Bitmap paste saves an application-owned PNG and inserts an `[Image n]` token.
File-drop clipboard content becomes `[File n]`. Hover shows the path and an image
thumbnail where supported. Token copying within Quick Input preserves its identity;
copying to other applications produces path text. Sending expands tokens to paths
with the existing target shell's quoting and WSL conversion rules. It does not
replace the clipboard with a path as a side effect of pasting.

Up to 20 temporary images are retained across drafts. Images required by undo
history are pinned. When all are pinned, remove an attachment and use **Clear undo
history** in the editor context menu before adding another image. Successful
transfer does not immediately delete its images, allowing the CLI to read them;
unreferenced images can be evicted by later image pastes. Application shutdown
cleans up images it owns and never deletes original attached files. Clipboard
bitmaps larger than 50 million pixels are refused; use an image file instead.

Bracketed paste is used only while the destination has enabled it. Without it,
multiline/tabbed text requires confirmation to make a single line: each CRLF, CR
or LF becomes one space, and Tab becomes four spaces. Original draft text remains
unchanged. Other control characters, including ESC/NUL/C1 controls, are rejected.
Confirmation covers the Quick Input content itself instead of opening a Windows
message box. Confirm continues the pending operation; Cancel or Escape returns
to the original editor and sends nothing. Other send/insert shortcuts are blocked
while confirmation is visible.
The transfer limit is 1 MiB of UTF-8 text, including expanded attachment paths.

Input writes run off the dispatcher and share the existing session input lock.
They have a 15-second deadline and cancel pending synchronous I/O on a dedicated
writer thread. Errors are returned instead of silently swallowed. A failed content
write never proceeds to the final Enter. Partial transfer or uncertainty about a
final Enter is reported honestly; drafts are retained and never retried
automatically. Existing input in the CLI is neither inspected nor cleared, so
insertion appends at its current input cursor. Bracketed paste and successful OS
pipe writes do not guarantee application-level acceptance or interpretation.

Tests:

```powershell
dotnet run --project tests/RtlTerminal.RenderTests -c Release -- --quick-input-tests
dotnet run --project tests/RtlTerminal.RenderTests -c Release -- --quick-input-cli-probe
```

Verified: native CMD, PowerShell and Git Bash Insert/Send, text/attachments,
per-session draft isolation, target closure, limits, undo/redo and both themes.
Codex, Claude Code and OpenCode were launched in temporary test directories and
their bracketed-paste state inspected; all enabled it, but none emitted an explicit
input-ready signal during the probe. Model prompts were not submitted. End-to-end
model response/submission, each tool's setup/authentication workflow and all
multiline editing semantics remain application-specific and are not certified by
these probes. No real project files or CLI configuration were modified.
