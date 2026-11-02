# Proxy Manager

Open **Tools → Proxy Manager…**. The dialog reads configuration in the background;
opening it never writes proxy settings. Developer-tool changes are **off by default**.
Inside Developer Tools, **Set Proxy** and **Clear Proxy** target only the selected
supported tools, independently of the Windows scope selector. Enable the opt-in
checkbox and select at least one tool to use them. They use the shared connection
fields and the same complete backup, operation dialog and read-back verification
as the main actions; they never change Windows proxy settings.

Tools with a saved user-level proxy show a small blue **Proxy** label and endpoint
below their name. Addresses refresh asynchronously during discovery, manual Refresh,
and after Set/Clear/Restore. Credentials and URL paths are excluded. This reflects
the managed user file, not a connectivity test or all environment/project overrides.
No session-routing, VPN, driver or process injection is involved.

The connection fields start empty. No example host, port or bypass list is supplied.
When supported settings already exist, the dialog loads those values; Environment
uses persistent User values with Machine fallback. Loopback bypass is added when
setting a proxy even when the optional bypass field is empty. Clear does not require
an endpoint. PAC/detection-only settings can be saved with manual proxy disabled.
Use **Help** beside Proxy Status for a short categorized guide; the same content is
included in **Help → Guide**. Both guides support English, Persian and Arabic.

The dialog follows the terminal's active palette in light and dark themes. Compact
cards group connection settings, scope and optional tools; Set/Clear remain visible
in a compact fixed footer while the content scrolls. The large introductory banner
is omitted. History can be expanded when needed, and technical details open from
the status cards. Installed-tool cards adapt to two, three or four columns
as the window is resized.

## Compact proxy status

The **Proxy Status** dashboard keeps Environment, Windows System and WinHTTP in
one horizontal row, including at the minimum supported dialog width. Each card
has a small indicator, short status and a trimmed endpoint/PAC address when present.
Click a card for structured, selectable details; credentials are redacted. No raw
JSON or command output is shown in these dialogs.

- Green **Enabled** means a valid supported configuration is present. No network
  connection or proxy-server reachability test is performed.
- Gray **Not configured** means no routing configuration was found. Bypass rules
  alone do not count as an enabled proxy.
- Amber **Partially configured** covers incomplete/invalid entries, disabled saved
  manual settings and conflicting environment values. A proxy remaining only in
  Process scope is also amber, rather than claiming persistent proxy configuration.
- Red **Error** means an actual provider read failed; other cards still update.

Environment details include all five managed variables separately for User,
Machine and Process. The reader reuses the existing provider implementations and
reads persistent Windows values, rather than substituting current process values.
Windows details show manual enable/server, PAC, bypass and detection. WinHTTP
details show the advanced proxy/bypass/PAC/detection state and actual User/Machine
scope. Reading Machine WinHTTP for the dashboard does not authorize Machine writes.

The small Refresh control reads all three independently on worker tasks, shows an
indeterminate indicator, coalesces overlapping requests and guards results after
window closure. Set/Clear and Restore automatically request a refresh. A request
made during an active refresh triggers another read afterward to avoid retaining
a pre-operation result.

The footer does not display diagnostic text. New operation/recovery notices reveal
a compact **Notifications** button, opening a themed dialog with the latest 50
in-memory notices. Full technical operation logs remain available in the existing
operation dialog. Native caption colors follow the application's title-bar palette
using the documented [Windows DWM caption attributes](https://learn.microsoft.com/en-us/windows/win32/api/dwmapi/ne-dwmapi-dwmwindowattribute),
with dark/light appearance support and native Windows caption buttons.

Run `dotnet run --project tests/RtlTerminal.RenderTests -c Release -- --proxy-status-tests --visuals`
for mocked classification, scopes, cancellation, read-only safety, compact layout,
notifications and themed dialog checks. Visual capture also reads the real status
without changing proxy settings.

Proxy Manager, confirmation prompts, operation status, recovery/history labels and
the diagnostic-window controls follow Help → LANGUAGE (English/Persian/Arabic),
including changes while a dialog is open. Direction, control order and selections
remain fixed. Literal host values, protocol identifiers, provider identities in
backups and raw technical logs are not translated. The restart reminder follows
the selected interface language too. Windows' own UAC dialog uses the OS language.

The segmented scope selector always opens at **All**, followed by Environment,
Windows System and WinHTTP. All shows **Set All Proxies / Clear All Proxies**;
a specific selection shows **Set Proxy / Clear Proxy** and changes only that
Windows provider. Environment includes both User and Machine variables, so All
contains four rows (User environment, Machine environment, Windows System User and
WinHTTP User) before optional developer tools. The other providers are still included in the complete
pre-operation snapshot, but are not mutated.

Proxy connection fields live beneath the scope selector inside the same card.
All, Windows System and WinHTTP show a fixed HTTP protocol; Environment additionally
offers HTTPS and SOCKS5. Compatibility hints explain the restriction, and selected
developer tools still undergo their own protocol validation before any changes.
Each scope retains its unsaved connection fields while switching within the dialog;
returning to Environment restores a previously chosen HTTPS/SOCKS5 protocol. A slow
background configuration read does not overwrite edits entered while it was running.

Developer Tools is a compact panel with a chevron, collapsed by default. Its
**Also apply to selected developer tools** checkbox remains prominent outside the
collapsed content and starts unchecked on every new dialog. Opening or closing
the panel never changes opt-in. Enabling opt-in automatically expands the tool
panel so the selections are visible; disabling opt-in preserves those selections.
With opt-in enabled, selected supported tools
accompany Set/Clear in **any** Windows scope. Turning opt-in off preserves tool
selections while preventing tool-specific writes. Detection runs in the background
even while the panel is collapsed; expanding it does not trigger configuration writes.

## Set/Clear operation dialog

Set/Clear opens a compact modal dialog listing only providers selected for mutation,
not the additional providers captured for comprehensive backup. Pending rows appear
before work starts. The coordinator reports each provider's start and its result
after read-back; UI animations never determine or delay configuration results.
Preflight/snapshot errors leave untouched providers skipped, while an interrupted
running provider is failed with an instruction to inspect History.

Rows distinguish pending, running, verified, failed and skipped. Numerical progress
counts processed configurations separately from verified successes, failures and
skips. Spinner/checkmark/progress animations run on the WPF animation system;
checkmarks fade/scale over 150 ms without artificial execution delays. Failure
details contain sanitized messages, not stack traces or raw command output.
Running dialogs cannot be accidentally closed and do not offer unsafe cancellation.
After completion the dialog stays open until OK; partial failure never shows an
overall-success screen. The owner is modal-disabled and subtly dimmed.

Success wording refers to **selected managed settings**, preserving the provider's
existing verification boundary. This UI does not add new effective-routing checks:
existing process environment or unhandled project/machine overrides can remain.
The dialog explicitly states that limitation rather than claiming system-wide
network routing is now clear. Existing backend backup, locking, mutation and
verification semantics are unchanged.

**View Full Log…** opens a selectable, read-only diagnostic window with Copy All
and normal Ctrl+A/Ctrl+C support. The in-memory log includes timestamped stages,
selected providers, native Windows error codes/HRESULTs, inner exceptions and stack
traces. Proxy credentials and common secret assignments are redacted. It does not
include configuration-file contents or raw command output, is not automatically
saved/uploaded, and does not write to the clipboard until requested by the user.

WinINet reads query flags/server/PAC separately from bypass. FLAGS_UI has the
documented FLAGS compatibility fallback. If bypass alone returns Windows error 87,
the reader preserves the persistent ProxyOverride value from the effective User or
Machine registry scope after matching enable state, server and PAC against the
native query. Scope mismatches and unsupported registry types still block snapshot
creation. This fallback is read-only and does not normalize/delete existing rules.

Windows writes use bracketed IPv6 literals in bypass lists (for example `[::1]`),
while environment NO_PROXY uses `::1`. An empty PAC option is omitted when no PAC
exists to clear; removing a previously stored PAC still submits its removal. Native
write failures log the returned option index, option count and value lengths without
logging proxy values/credentials. Global Windows writes have not been exercised by
destructive integration tests; read-back verification still determines success.

Clear intentionally removes User and Machine NO_PROXY when Environment/All is
selected, and the targeted Windows/WinHTTP bypass
lists, along with proxy/PAC/automatic-detection state. It does not preserve bypass
exceptions for independent Machine/project proxy configurations. Those overrides
can therefore matter after a clear; the confirmation dialog warns about this.

## Persistent configuration

- Environment: User- and Machine-scope HTTP_PROXY, HTTPS_PROXY, ALL_PROXY, FTP_PROXY and NO_PROXY.
  Windows variable names are case-insensitive, so uppercase and lowercase aliases
  are not written as separate variables. Diagnostics show Process, User and Machine
  values separately. Existing processes and their children can retain stale values;
  restart the parent application to inherit persistent changes.
- Windows: WinINet per-connection options through InternetQueryOptionW and
  InternetSetOptionW. Manual proxy, bypass, PAC URL and automatic detection are
  read independently. Save preserves PAC/automatic settings unless edited.
  INTERNET_OPTION_SETTINGS_CHANGED and INTERNET_OPTION_REFRESH notify compatible
  applications. Policy-controlled proxy configuration is refused.
- WinHTTP: structured, bounded `netsh winhttp show/set advproxy` commands. User
  settings are supported when the effective configuration is per-user. The JSON
  response is parsed and reread after writes. Unsupported Windows versions and
  effective Machine scope are refused; **Machine WinHTTP changes are not implemented**.
  The separate elevated environment helper does not change this limitation.
  Basic WinHTTP machine configuration is not cleared by this
  User-scope implementation. There is no claim that reset proxy clears advanced
  state; that command is not used.

HTTP endpoints work across all three sections. HTTPS and SOCKS5 endpoints are
accepted for environment variables and Git, but rejected by providers that cannot
safely support them. Bulk compatibility validation finishes before any write.
Host fields reject schemes, credentials, paths and injected delimiters. Loopback
bypass entries are always included when setting proxies. Different clients have
different wildcard/CIDR bypass semantics; rules are not universally interchangeable.

Set/Clear affect only the selected Windows scope. Developer tools are included
only when the main checkbox and individual tool selections are enabled.

### Machine environment and UAC

All and Environment operations request UAC authorization before any proxy mutation
begins. Cancelling UAC leaves all proxy scopes untouched. The ordinary application
stays unelevated; a short-lived instance with `--proxy-machine-helper` handles only
the five allowlisted Machine environment variables. User settings and developer
configuration files are never written under a different administrator account.

IPC uses a randomly named local pipe restricted by ACL to the initiating user's
SID. Both ends verify process IDs; the helper additionally verifies the parent
executable path. Requests carry no arbitrary commands, registry paths or file
paths. Complete snapshots are created by the original user and DPAPI-protected
before mutation. The helper handshake verifies administrator status and opens the
HKLM Environment key with write access without changing any values. Machine writes compare pre-write state, detect concurrent edits
and reread actual Machine values after writing. Restoring Machine snapshots uses
the same elevated channel. Group-policy proxy restrictions are honored.

UAC must elevate the **same Windows account**. Supplying a different administrator
account is deliberately unsupported and fails before mutations. Already-elevated
parents may not trigger another consent prompt. Elevation addresses permission,
not invalid proxy parameters, and does not update existing processes' environments.
Clear removes the targeted User and Machine proxy environment variables when
Environment/All is selected, plus selected User Windows/WinHTTP settings including
PAC and automatic detection. It does not modify other accounts' User settings or
Machine WinHTTP configuration.

## Recovery and failures

All affected providers are read before a bulk write. Snapshots are protected with
Windows DPAPI CurrentUser under `%LOCALAPPDATA%\RtlTerminal\ProxyHistory`.
Configuration file content and credentials are never printed in results. Writes
are reread; a difference is reported as failure, not success. Independent write
failures do not prevent safe subsequent providers from running. A preflight read
failure aborts the batch before changes begin.

**Configuration History → Restore Selected Backup** lists the latest ten valid
snapshots, newest first. Snapshots include IDs, UTC timestamps, operation labels,
provider inventory/version/location/scope and exact provider state. JSON payloads
and integrity hashes are inside DPAPI encryption. Directory ACLs restrict access
to the current Windows user and SYSTEM. Atomic, write-through backup creation is
verified before old valid snapshots are pruned. Corrupt snapshots are excluded.

Every operation snapshots the complete current managed inventory, including User
and Machine proxy environment variables and unselected configurable tools. Process/Machine environment and basic Machine
WinHTTP records are included for inspection. These inspection scopes cannot be
implicitly changed by restore; differences are reported as skipped. **Completeness
means the implemented managed User + Machine environment scope, not all Windows machine configurations.**
Non-effective advanced WinHTTP configurations cannot currently be exported by
this adapter, and full machine/other-account recovery is not implemented.

If an installed tool's version probe fails but its managed file location is
known, the complete original file bytes (or file absence) are still captured in
the encrypted snapshot. The tool remains unselectable and inspection-only, so a
Cargo version-detection failure does not block an unrelated Windows proxy clear.
Unknown-version configurations cannot be mutated or overwritten during restore;
newer file changes are reported as skipped. Unverifiable configuration locations
and actual file-read failures still block the backup and operation. Version
probes for native executables use the Windows user profile as their working
directory rather than inheriting a project's toolchain-selection directory.

Restoration refreshes detected tools and versions, compares metadata, previews
affected providers and requires confirmation before replacing newer values. A
new comprehensive current-state snapshot makes restore itself reversible.
Configuration changing after that preview causes refusal. Versions or locations
that differ from the backup are conservatively rejected. Exact file bytes and
absence are restored for file providers; no previous proxy address is broadcast
to unrelated providers. Independent failures are reported individually.

The mutation coordinator checks whether the current state already equals the
requested target before treating a snapshot difference as a conflict. This handles
coupled effective settings (for example, Windows System changes reflected by
per-user WinHTTP) and already-cleared configuration. A second read must confirm
the exact target; success then records that no write was needed. Unexpected states
and changes during that verification remain conflicts and are never overwritten.
This applies equally to Set, Clear and restoration, without changing the original
pre-operation backup or rebasing it onto intermediate state.

A shared file lock prevents overlapping mutations across application instances.
An encrypted durable pending-operation marker remains after interruption or partial
failure. A non-modal reminder appears inside Proxy Manager; terminal startup does
not display a recurring recovery popup. Successful verification clears the marker.
**Clear Snapshots…** deletes application-owned history snapshots and the recovery
marker after confirmation. It is protected by the operation lock, preserves other
files and does not change proxy configuration. Deletion is irreversible.
Changes across providers are not inherently atomic; recovery
uses explicit, verified compensating restoration rather than silent rollback.
DPAPI backups normally require the original Windows account/profile and machine;
they are not portable exports. No backup is uploaded or synchronized by the app.
Individual configuration files are limited to 4 MB, snapshots to 32 MB plaintext.

## Developer tools

Detection checks PATH and known installation directories asynchronously, publishes
results progressively, caches results for two minutes, supports Refresh and
cancels on closure. Version probes are local, time-bounded and run off the UI thread.
Corepack/network installation is disabled during probes. Node shim package metadata
is used where available. No full-drive scan occurs.
Only executable/shim files found in these locations appear. Portable installations
outside these locations, project-local binaries and some version-manager layouts
may not be detected. The first detected installation per tool is inspected; multiple
installation selection is not implemented. Each configurable tool requires a parsed
version in its reviewed compatibility range and a safe configuration location.
Unknown/future versions remain visible but read-only. A required adapter whose
version/location is unverified blocks comprehensive backup and mutation.

Independent User providers:

| Tool | Managed configuration |
| --- | --- |
| Git | Global http.proxy planned using Git against a temporary copy; entire target file bytes/absence are backed up and restored |
| npm / pnpm | User `.npmrc`: proxy and https-proxy; shared file is modified once |
| pip | `%APPDATA%\pip\pip.ini`, global.proxy |
| Gradle | GRADLE_USER_HOME/gradle.properties, HTTP/HTTPS host and port plus nonProxyHosts |
| Cargo | CARGO_HOME/config or config.toml, http.proxy |
| Maven | ~/.m2/settings.xml, only proxy id `rtl-terminal`; other active proxies cause refusal |
| NuGet / .NET | User NuGet.Config, http_proxy and no_proxy; package sources and credentials retained |

Yarn, uv, Conda, Docker, Composer, curl, wget, GitHub CLI, Codex CLI, Claude Code,
OpenCode and Cypress are detected when present but **not selectable for independent
configuration**. Their version-specific configuration has not been implemented;
the UI does not invent proxy settings for them. npm/pip with a configuration-path
override are also inspection-only. Project files are never recursively modified.
Git remote/URL-specific overrides and environment/project overrides can remain.
Read-back verifies the targeted storage, **not every tool's effective network
routing**. Full precedence/conflict resolution remains work before describing this
feature as production-ready across all requested tools. Adapter compatibility
ranges are deliberately conservative (Git 2, npm 6–11, pnpm 6–10, pip 20–26,
Gradle 6–9, Maven 3, .NET 6–10, Cargo 1); supporting a future major requires review.

Text editors preserve unrelated lines and comments and refuse ambiguous proxy
continuations/inline comments. XML is loaded with DTDs prohibited and whitespace
preserved; serialization can normalize XML quoting/declarations. Existing file
replacement uses File.Replace to retain destination security metadata.

## Validation

```
dotnet build RtlTerminal.sln -c Release --no-restore -p:NuGetAudit=false
dotnet run --project tests/RtlTerminal.RenderTests -c Release --no-build -- --proxy-tests
dotnet run --project tests/RtlTerminal.RenderTests -c Release --no-build -- --proxy-tests --proxy-read
```

The last command adds read-only native Windows queries. Tests cover encrypted
backup creation, ten-backup retention, corrupt snapshots, exact absence restoration,
unselected-provider inventory, version mismatches, restore conflicts, permission
failures, overlapping operations and durable recovery markers. Tests mutate fake providers
and files under artifacts only, not real proxy settings. `--proxy-preview` additionally
captures light/dark dialog images. A packaged executable also supports
`--proxy-machine-probe <report-path>` for a read-only UAC/IPC self-test. This verifies
that an unelevated parent can authenticate an elevated helper and obtain write
access to the Machine Environment key without changing Machine values. Real Machine Set/Clear writes are tested with injected storage;
real proxy writes, enterprise policy
deployments and end-to-end network routing have not been integration-tested.

References: [WinHTTP commands](https://learn.microsoft.com/en-us/windows-server/administration/windows-commands/netsh-winhttp),
[WinINet per-connection options](https://learn.microsoft.com/en-us/windows/win32/api/wininet/ns-wininet-internet_per_conn_optiona),
[Git](https://git-scm.com/docs/git-config), [npm](https://docs.npmjs.com/cli/using-npm/config/),
[pip](https://pip.pypa.io/en/stable/topics/configuration/),
[Gradle](https://docs.gradle.org/current/userguide/networking.html).
