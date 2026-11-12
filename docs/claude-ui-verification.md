# Claude Code UI verification

## Claude 2.1.322 reproduction (2026-11-12)

Downloaded the isolated Windows x64 binary from the official release endpoint;
SHA-256 verified against its official manifest:
`345996b4bdbae21d817f750fae2f34cbf8d81224a5444c78136091d121beb298`.
The existing installed version was not replaced. The probe accepts `--claude-exe`
to test another binary without installation:

```powershell
artifacts/claude-profile-validation/RtlTerminal.RenderTests.exe --claude-ui-probe --mock --claude-exe artifacts/claude-2.1.322/claude.exe
```

The new workspace trust screen defaults to No, unlike the earlier fixture.
The probe now explicitly selects Yes only for its generated temporary workspace.
English input passed and the local streamed response completed (one mock request).
Persian and mixed input assertions failed: the captured buffer contains
`ایند مالس` for input `سلام دنیا`, and
`src/test.cs ایند English مالس` for `سلام English دنیا src/test.cs`.
These reordered characters are present before WPF layout; this is not merely a
failure to recognize `claude.exe`. The generated PNG also shows incorrect Persian
ordering. This was the **pre-fix reproduction**; the older 2.1.50 result did not cover it.
Captures and raw text: `artifacts/claude-ui-probe-621abcff9b134c33b960408588379948`.

### Visual-order correction and verification

The Claude profile recognizes the version banner and, for 2.1.322 and newer,
preserves visual cell order for input, prompt history, responses and code alike.
Persian words are converted back to logical order solely for contextual glyph
shaping. A source-offset map preserves styles, links, selection hit testing and
cursor positions; the VT buffer and transmitted input remain untouched.
Spaces and ASCII punctuation stay in their original cells; only Arabic-script
letter spans receive contextual shaping. The CLI owns wrapping of visual-order
output, so logical Persian paragraph reflow is bypassed in this mode.
Older Claude versions and other CLI profiles retain their existing layout.
Explicit Row RTL remains an override.

The updated 2.1.322 local integration probe passed English, Persian and mixed
input, fixture response streaming, resize and exact logical prompt submission
(excluding Claude's separately added system reminder). Post-fix captures:
`artifacts/claude-ui-probe-005d623b86584e04bb63c58ce6644d8f`.
The same probe also passed on 2.1.50. Renderer regressions cover the visual cell
ordering, shaping-source mapping, ZWNJ, emoji and older-version isolation, plus
response words, code strings, ASCII punctuation positions and visual wrapping.
The complete existing render regression suite passed.

This is a version-gated compatibility rule tested on 2.1.322, not a terminal
protocol guarantee for future Claude releases. Detecting the version banner is
required; sessions attached after the banner has disappeared may need a fresh
Claude launch. Fullscreen mode has not been separately verified.
The mock tests rendering and transport, not real model behavior.

## Manual rendering profile

View → Compatibility profile offers Auto (initial default), Default, Claude and
OpenCode–MiMo. The choice affects only the active pane, survives moving that pane
to another tab/window, and lasts for that pane's lifetime. It is not a global
preference. Switching re-renders immediately, without requiring new CLI output;
Auto returns to the latest automatically detected profile. Smart RTL and Row RTL
remain separate controls. Claude enables the visual-order correction above when
the version banner is available; Default disables it.

## Interactive local demo

Run `powershell -NoProfile -ExecutionPolicy Bypass -File scripts/start-claude-demo.ps1`.
This opens an RTL Terminal window titled **Claude Code — MIXED RTL TEST**
using the original installed Claude executable. Type and press Enter to receive
the fixed Persian/English/code/long-line fixture. The fixture is identical for
each prompt and is not a live model. Built-in tools and MCP are disabled.

All configuration is isolated under `artifacts/claude-interactive-demo-mixed`; the API
server binds only to loopback and stops when all demo terminal windows close.
No model download or paid API access is used. The generated `demo.json` records
the local endpoint and helper PID, without credentials.

Observed local installation: Claude Code **2.1.50**, Windows native WinGet package.

The isolated ConPTY probe successfully launched the application, handled terminal
queries, observed bracketed-paste mode, selected the local color theme and reached
**Select login method**. This startup used the main screen, not alternate screen.
The OpenCode/MiMo fixed-grid profile correctly remained disabled for Claude.

The probe uses a new temporary configuration directory under `artifacts`, disables
built-in tools and Chrome integration, supplies an empty strict MCP configuration
and disables nonessential traffic/updates in its child environment. Existing Claude
configuration and credentials are not loaded or changed. No authentication method
is selected, no terms are accepted and no model prompt is submitted.

## Limitations

## Local mock backend test

The `--mock` option starts a loopback-only TCP server on an ephemeral port,
implementing token counting and JSON/SSE Messages responses. A test-only API key
and `ANTHROPIC_BASE_URL` point the child to this server. A temporary local config
fixture marks initial UI setup complete, then the probe confirms trust only for
its generated workspace and approves only its dummy key. It never uses a real
account. No model is downloaded and no Anthropic model request is made.

Verified with Claude Code 2.1.50:

- Actual main UI starts without a subscription using the local endpoint.
- English, Persian and mixed Persian/English/path input remain present in the buffer.
- A fixture response streams through the real Claude CLI and reaches the terminal,
  including Persian, English-first text, a code block and long lines.
- Narrowing the terminal from 100 to 70 columns preserves the completion marker.
- Claude stays excluded from the OpenCode/MiMo compatibility profile.

Generated images exposed a visual issue: code containing Persian string literals
inherited RTL paragraph ordering. The dedicated Claude profile now addresses this:
code-like lines preserve LTR syntax, with local RTL ordering inside quoted strings;
other paragraphs use their first strong letter to choose direction. Borders and
alternate-screen UI regions retain their grid layout. These rules apply only to
detected `claude.exe` sessions and are removed when the process exits. Codex keeps
its existing layout; OpenCode/MiMo retain their separate profile.

Code detection is deliberately limited to recognizable prefixes/operators and is
not a complete programming-language or Markdown parser. Model quality, all editing
keys and modern fullscreen mode are not tested by the mock backend. Explicit Row
RTL remains a user override. `--claude-layout-tests` checks syntax positions,
quoted strings, emoji, mixed paragraphs and default-profile isolation.

```powershell
dotnet run --project tests/RtlTerminal.RenderTests -c Release -- --claude-ui-probe --mock
```

### Earlier startup-only result

Without the mock backend fixture, this installed version stops before its main prompt.
Persian/English editing, caret behavior, response streaming and fullscreen chat
were **not verified**. Startup rendering is not a substitute for those tests.
Do not apply the OpenCode/MiMo profile to Claude on the basis of this probe.

The official [Claude Code changelog](https://github.com/anthropics/claude-code/blob/main/CHANGELOG.md)
records a Windows-terminal RTL fix in **2.1.74**, later than this installation.
Current [fullscreen documentation](https://code.claude.com/docs/en/fullscreen)
describes newer behavior that cannot be assumed for 2.1.50.

```powershell
dotnet run --project tests/RtlTerminal.RenderTests -c Release -- --claude-ui-probe
```

The generated PNG stays under that run's `artifacts/claude-ui-probe-*` directory.
The probe runs directly through ConPTY and the WPF renderer without screen control.
