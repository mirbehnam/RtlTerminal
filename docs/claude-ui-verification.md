# Claude Code UI verification

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
