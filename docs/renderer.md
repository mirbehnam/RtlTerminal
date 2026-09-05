# Cell renderer

The WPF window now hosts `TerminalView`, a drawing surface inside a scroll viewer.
It consumes the existing immutable `TerminalSnapshot`; ConPTY and the VT parser
remain responsible for process input/output and terminal protocol semantics.

Only visible rows are laid out. Visible layouts and shaped text are cached;
scrollback does not create a document tree or a WPF element for every cell.
Common box-drawing characters are drawn geometrically to join across cells.
Block elements U+2580–U+259F use cell-relative rectangles (shades use blended
foreground/background colors). Graphics have hard edges and font metrics are
rounded to physical pixels. The dark scroll gutter remains allocated when empty,
so history growth does not change the PTY's column count.
Other characters use WPF text shaping and font fallback. Emoji use Segoe UI Emoji;
color emoji and flag appearance depend on the platform's text renderer.

Smart RTL preserves the existing directional-span policy. Normal RTL lines are
right aligned. Alternate-screen lines retain their terminal-grid origin while
RTL spans are shaped together. Selection and copying use logical Unicode offsets,
so copied Persian is not visually reversed. Cursor and hit testing use the same
cell placements. The renderer does not implement a new Unicode bidi algorithm.

Existing keyboard sequences, bracketed paste, clipboard files/images, mouse and
focus reporting, session export, tabs, history limits, font settings, and update
checks remain in the window/backend. Ctrl+click opens links; Ctrl+Shift+A selects
all retained text. Scroll offset and selection are saved separately for each tab.

Proxy UI, persistence, and process-environment overrides have been removed. Shells
inherit the normal parent process environment; this does not modify Windows proxy
settings or external applications' environment configuration.

Verification on Windows:

```powershell
dotnet build RtlTerminal.sln -c Release
dotnet run --project tests/RtlTerminal.BufferTests -c Release
dotnet run --project tests/RtlTerminal.RenderTests -c Release
```

Render tests create offscreen WPF images under `bin/render-checks` for mixed RTL,
block continuity at 96/120/144 DPI, incremental-versus-fresh redraw equality,
stable viewport width and row-cache invalidation, in addition to
ANSI styles, selection, alternate screen, and large scrollback. These smoke tests
do not replace interactive checks with actual OpenCode/Claude/Codex installations,
IME input, clipboard operations, multiple DPI settings, and different fonts.

Buffer regressions cover immediate CR, pending autowrap across SGR, status
redraw split at every input boundary, and wide-character erasure. These catch
specific cursor/redraw defects but do not prove that every observed live duplicate
line has the same cause; that requires reproducing the original output stream.
