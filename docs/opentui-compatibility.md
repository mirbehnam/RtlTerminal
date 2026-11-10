# OpenCode and MiMo Persian compatibility

OpenCode and MiMo use OpenTUI-style fixed terminal grids. MiMo's maintained
[TUI source](https://github.com/XiaomiMiMo/MiMo-Code/blob/main/packages/cli/src/cli/cmd/tui/app.tsx)
uses SolidJS with `@opentui/core` and `@opentui/solid`. The CLI controls layout,
padding, colored widget backgrounds and cursor positions through VT sequences.

RTL Terminal automatically enables a local fixed-grid RTL profile when the session
process tree contains `opencode.exe`, `opencode2.exe` or `mimo.exe`. Detection uses
the Windows Toolhelp process snapshot on the output worker, at most once a second.
The profile applies only to alternate-screen rendering. Persian spans retain
shaping and local RTL ordering, while the whole UI row keeps its original column
order. Rendering caches are invalidated when the profile changes.
Mixed text is reordered locally inside widget regions, bounded by box-drawing
borders and multi-cell padding. Persian-first input keeps Latin fragments in LTR
order within that RTL region. The insertion cursor at the end of such text follows
its visual boundary rather than the original trailing-padding column.

Codex and other terminal sessions retain their existing default behavior. Claude
has a separate profile described in [Claude verification](claude-ui-verification.md).
Codex/Claude processes veto the profile if present in the same process tree.
When detected tools exit, the profile clears on subsequent output. Explicit
user-selected Row RTL continues to override paragraph direction.
No global environment variables, CLI configuration files or application themes
are changed. This does not patch upstream text editing or add BiDi support to
OpenTUI itself. Remote tools inside WSL/SSH are not identifiable by this Windows
executable-based detection and are outside this automatic profile.

## Verification

```powershell
dotnet run --project tests/RtlTerminal.RenderTests -c Release -- --opentui-probe
```

The probe launches installed OpenCode and MiMo in `artifacts/opentui-probe`, types
English and mixed Persian/English without pressing Enter, checks live process
detection, and generates full-size PNGs in `artifacts/render-checks`. No model
request is submitted. MiMo is launched with `--pure`. Verified installations:
OpenCode 2.2.5 and MiMo 0.1.14. Model response streaming and all interactive
editing combinations are not certified by this input-only probe.

The regular regression suite checks fixed border columns and unchanged default
alternate-screen ordering, including existing Codex Persian selection tests.
