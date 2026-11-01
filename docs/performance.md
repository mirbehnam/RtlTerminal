# Startup and component timings

Normal launches write local JSON Lines timing reports to
`%LOCALAPPDATA%\RtlTerminal\Performance`. Only the latest ten automatic reports are
kept, with a maximum of 10,000 records per run. Serialization, disk writes and
retention cleanup run on a background worker with a bounded queue. A full queue
can drop diagnostic events; logging failures never prevent application startup.
Reports contain stage names, elapsed/duration milliseconds and managed thread IDs,
not terminal output, commands, directory names, proxy addresses or credentials.
Privileged proxy helpers do not start this logger.

The stages include theme/settings loading, main XAML and initialization, font
metrics, language/profile initialization, initial window rendering, pseudoconsole
creation, shell command preparation, environment-block construction, native
`CreateProcess`, and first received terminal output. Window Loaded/ContentRendered
events are recorded for other windows too. Settings construction and Proxy Manager
construction, configuration/history reads and each tool's version detection have
separate timings. Closing a dialog during detection can produce a shorter,
cancelled detection span: a duration alone is not a success indicator.

`elapsedMs` is relative to the logging entry in `App.OnStartup`; `processElapsedMs`
adds the measured process age at that point. `App.ManagedEntry.durationMs` is that
initial process age, including work before `OnStartup` such as runtime startup.
It does not separately measure single-file extraction, security scanning or JIT.
Other `durationMs` values measure the named span using a monotonic Stopwatch.
Loaded-to-ContentRendered durations measure that interval, not total opening time.
Nested/overlapping spans must not be added together. First shell output does not
mean a shell prompt is ready or user profiles have finished executing.

## Repeatable measurements

After publishing a standalone application:

```powershell
./scripts/measure-startup.ps1 -Executable ./artifacts/preview-performance/RtlTerminal.exe -Runs 3
```

This opens real terminal instances using `--performance-probe --performance-log
<unique-path>`. Probe mode skips the optional context-menu installation prompt and
update check, then closes its own window after both first output and the first
rendered frame, or after a 30-second UI timer deadline. It runs the configured shell
and its normal startup profile. Do not type work into these temporary test windows.
The script waits for graceful exit, never kills a process, and produces per-run
JSONL reports and `summary.csv`. The timeout cannot interrupt a blocked UI thread;
failure to close is reported for manual handling. Normal app launches do not
automatically close or suppress startup prompts/updates.

Run a normal launch with a custom, **new** log file when diagnosing ordinary startup:

```powershell
./artifacts/preview-performance/RtlTerminal.exe --performance-log C:\Temp\rtl-startup-new.jsonl
```

Reusing an existing log file intentionally does not overwrite it. Custom report
paths are not covered by automatic retention.

Safe component measurements and logger concurrency/failure checks:

```powershell
dotnet run --project tests/RtlTerminal.RenderTests -c Release -- --performance-tests
```

These measure actual settings and proxy dialogs and read-only tool detection.
They do not Set/Clear proxies or modify developer-tool configuration. Reports and
preview screenshots are written beneath `artifacts`.

Compare identical publish options, runtime version, shell, font and launch path.
The first trial and later trials have different cache conditions; these scripts
do not flush OS, font, runtime or antivirus caches. They are local observations,
not performance guarantees or a statistically controlled baseline comparison.

## Size investigation, 2026-11-02

Existing local standalone builds (bytes):

| Build | RtlTerminal.exe |
| --- | ---: |
| PR17 OSC8 hover | 239,176,762 |
| Proxy Manager localized | 239,488,058 |
| Proxy Manager modern UI | 239,496,250 |

The modern UI adds 8,192 bytes over the localized preview. All proxy changes up
to that preview add 319,488 bytes over the retained PR17 OSC8 preview. No new NuGet
dependency was added for the Proxy Manager UI. The bundled `.ttf` files total
76,778,336 bytes; older pre-font previews were approximately 162 MB. These are
uncompressed executable sizes, not installer/download sizes. A reported 20 MB
increase cannot be attributed precisely without the two exact compared artifacts.

Local measurements of the instrumented standalone build: the first observed
launch reached its first frame in 3,001 ms, then subsequent trials took 759 and
740 ms. In that first trial native `CreateProcess` took 2,172 ms; subsequent calls
took approximately 25 ms. Main XAML took 294–307 ms and saved font setup 29–30 ms.
This locates the first-trial delay but does not establish whether Windows caching,
security software or another native-system factor caused it. There is no measured
pre-feature startup baseline, so these results do not prove a regression source.

Separate component checks measured Settings construction at approximately 279 ms,
Proxy Manager construction at 15–16 ms, and complete asynchronous developer-tool
detection at approximately 2 seconds. Detection starts only when opening the Proxy
Manager and is not on the ordinary application startup path. These are individual
local samples, not isolated statistical benchmarks.
