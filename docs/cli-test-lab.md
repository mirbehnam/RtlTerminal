# CLI Test Lab (preview builds)

Open **Tools → CLI Test Lab…** in a development build. Choose an installed
executable/version, edit the sample response or select English, Persian or
**Mixed + Code**, then choose **Open Local Test Tab**. Type a prompt in the resulting
`LOCAL TEST` tab and submit it normally. The original CLI renders a fixed local
response, without a model account, subscription or download.

Use **Add executable…** with the appropriate tool type to register another native
`.exe`, including a separately downloaded Claude version. Versions are inspected
asynchronously with a timeout. Registered paths are remembered; executables are
never installed, upgraded or replaced by this dialog. Unsupported/unidentified
versions remain visible with a reason and cannot launch. Native executables are
supported; npm `.cmd` shims and WSL binaries must be resolved to a native executable
first. Do not select an executable you do not trust.

Native versions placed one directory below the lab's `versions` folder are also
discovered. Source checkouts additionally inspect immediate subdirectories of
their `artifacts` folder, without recursively scanning the filesystem.

## Build availability

- Debug: enabled automatically.
- Optimized preview: `powershell -File scripts/build-preview.ps1` or use
  `-p:EnableCliTestLab=true` with `dotnet build/publish`.
- Official Release: disabled by default; the release workflow explicitly passes
  `-p:EnableCliTestLab=false`. Lab UI, detection and mock-server classes are removed
  from compilation rather than merely hidden.

This feature adds no NuGet dependency and performs no detection or server startup
until the lab is opened and a test is launched.

## Isolation and protocols

Each tab uses a unique local directory under
`%LOCALAPPDATA%/RtlTerminal/CliTestLab/sessions`. HOME, USERPROFILE, APPDATA,
LOCALAPPDATA, XDG paths, temporary files and CLI configuration point into it.
The child receives an explicit allowlisted environment, not the application's
provider credentials or proxy variables. This does not change the RTL Terminal
process, user/machine environment or normal terminal sessions.

Only the local provider is configured, with dummy credentials. Claude uses its
Anthropic base URL override, empty MCP config and disabled built-in tools; OpenCode
and MiMo use an allowlisted OpenAI-compatible provider, isolated configuration and
denied tool permissions. Updates, model-catalog fetches and external plugins are
disabled where supported. The fixture server binds only to IPv4 loopback, supports
Anthropic Messages and OpenAI Chat Completions (JSON and SSE), and never forwards
requests. Unsupported HTTP routes fail locally. Input and streaming are real CLI
operations; the fixed response does not simulate model intelligence or tool use.

The test tab owns the server lifetime, including when detached to another window.
Closing the tab/application stops its server. Test histories remain in the local
session directory; they are not synced or exported by this feature. Use test text
rather than secrets. `executables.json` stores executable paths/version metadata,
not account credentials.

This is configuration isolation, **not an OS network sandbox**. The selected CLI
can still make ancillary requests if it ignores its own documented controls; RTL
Terminal does not add firewall rules or alter its executable. A fully disconnected
machine/network is needed to guarantee zero external network activity. Only the
fixture backend's loopback requests are counted as successful local tests.

## Verified versions and tests

Real ConPTY UI tests received `LOCAL_UI_TEST_OK` from the loopback fixture with:

- Claude Code 2.1.50 and 2.1.322.
- OpenCode 2.2.5 (`--standalone`, v2 `providers` schema).
- MiMo Code 0.1.14 (`--pure`, `MIMOCODE_HOME`, legacy `provider` schema).

OpenCode 1.x has a separate legacy schema adapter; it has configuration tests but
was not exercised against an installed 1.x CLI. Future versions are not guaranteed
compatible merely because the dialog can inspect their version/help output.

```powershell
dotnet build tests/RtlTerminal.RenderTests -c Release -p:EnableCliTestLab=true -o artifacts/lab-check
artifacts/lab-check/RtlTerminal.RenderTests.exe --cli-lab-tests
artifacts/lab-check/RtlTerminal.RenderTests.exe --cli-lab-live
```

The live test uses temporary workspaces and fixed fixture requests, not real
provider accounts. Unit/integration coverage includes both local protocols,
stream completion, exact prompt receipt, explicit child environments, unknown
versions, unsupported routes, disposal, separate sessions, and Light/Dark UI
renders. Build without the flag and run `--cli-lab-tests` to assert lab classes
are absent from the official assembly. Existing application regressions also pass.

Configuration references: [OpenCode providers](https://opencode.ai/docs/providers/),
[MiMo flags](https://github.com/XiaomiMiMo/MiMo-Code/blob/main/packages/cli/src/flag/flag.ts),
[MiMo isolated home](https://github.com/XiaomiMiMo/MiMo-Code/blob/main/packages/shared/src/global.ts).
