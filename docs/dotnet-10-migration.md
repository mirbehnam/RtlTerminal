# .NET 10 migration

The WPF app and render regressions target `net10.0-windows`; buffer regressions
target `net10.0`. Build with a stable .NET 10 SDK. `global.json` selects the latest
installed stable 10.0 feature band, starting at 10.0.100. Both Windows workflows
use .NET 10; validation also runs on pushes to `upgrade/dotnet-10`.

## Compatibility review from .NET 8

Both intervening releases were reviewed against Microsoft's
[.NET 9 breaking changes](https://learn.microsoft.com/en-us/dotnet/core/compatibility/9.0)
and [.NET 10 breaking changes](https://learn.microsoft.com/en-us/dotnet/core/compatibility/10).

| Area | Application decision and verification |
| --- | --- |
| WPF clipboard | Paste in Quick Input and terminal file/image/text drops use .NET 10's typed `IDataObject.TryGetData<T>` API. Attachment metadata remains a JSON **string** under its existing format; no arbitrary object deserialization or BinaryFormatter compatibility package is enabled. Tests cover Unicode, custom JSON strings, images, files, attachment identity and missing formats. |
| Empty environment values | .NET 9+ distinguishes an empty process variable from an absent variable. Optional Git HOME/XDG, Cargo and Gradle home paths now explicitly fall back for empty values, preventing relative configuration paths. Shell environment blocks still preserve actual inherited empty values. Proxy Clear already uses `null` to delete values; this remains unchanged. |
| Native single-file loading | ConPTY already resolves `ConPty/<architecture>/conpty.dll` using an absolute path under `AppContext.BaseDirectory`. Keep this resolver and the companion OpenConsole executable. Verify the actual self-contained single-file application starts a shell; do not add broad DLL probing or disable runtime safeguards. |
| WPF resources | Existing dynamic brushes and Grid definitions require no compatibility switch. Full light/dark theme, DPI and window-construction tests exercise the resources under .NET 10's stricter DynamicResource behavior. |
| Serialization and encryption | JSON persistence, encrypted proxy snapshots and macros continue using their existing formats. Regression tests verify backup integrity, restoration, partial failures and tampering. No data migration or weakening of DPAPI is needed. |
| SDK and NuGet | CI and release SDK versions are upgraded together. Normal restore/build retains NuGet auditing. The application keeps its existing ConPTY package; no new dependency is needed for migration. |

Relevant Microsoft references:

- [WPF .NET 10 clipboard and rendering changes](https://learn.microsoft.com/en-us/dotnet/desktop/wpf/whats-new/net100)
- [Empty environment variable semantics](https://learn.microsoft.com/en-us/dotnet/core/compatibility/core-libraries/9.0/empty-env-variable)
- [Single-file native library search](https://learn.microsoft.com/en-us/dotnet/core/compatibility/interop/10.0/native-library-search)
- [DynamicResource type validation](https://learn.microsoft.com/en-us/dotnet/core/compatibility/wpf/10.0/dynamicresource-crash)

## Verification

```powershell
dotnet build RtlTerminal.sln -c Release
dotnet run --project tests/RtlTerminal.BufferTests -c Release --no-build
dotnet run --project tests/RtlTerminal.RenderTests -c Release --no-build
# Focused compatibility checks, without replacing the Windows clipboard:
dotnet run --project tests/RtlTerminal.RenderTests -c Release --no-build -- --net10-tests

dotnet publish src/RtlTerminal/RtlTerminal.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:PublishTrimmed=false -o artifacts/preview-net10
powershell -NoProfile -ExecutionPolicy Bypass -File scripts/measure-startup.ps1 -Executable artifacts/preview-net10/RtlTerminal.exe -Runs 1 -VerifyUiFirst -OutputDirectory artifacts/startup-net10
```

The startup probe opens and closes its own test window automatically; no screen
automation is required. Timing logs are local build artifacts. A successful first
output check verifies shell output, not readiness of every interactive CLI or a
performance improvement over .NET 8. Self-contained distributions include .NET 10;
local framework-dependent builds require the .NET 10 Desktop Runtime.

Keep the `ConPty` directory beside the executable. This migration does not opt
into global Fluent styles, change the application's version, or publish a release.

## Quick Input Fluent trial

Quick Input alone now loads WPF's built-in `Fluent.Dark.xaml` or `Fluent.Light.xaml`
resource dictionary to match the terminal theme. Native Fluent templates style
buttons, editor, scrollbars and menus; Send and the confirmation action use the
Fluent accent-button style. The existing frameless window, shortcuts, draft and
session targeting remain in place. No new UI package or application-wide theme
switch is used. Other windows keep their existing styles.
The window uses subtly translucent tinted surfaces with fully opaque text and
controls, without a blur effect. The editor hint contains only “Write your text
here”, localized into Persian and Arabic.

Run `dotnet run --project tests/RtlTerminal.RenderTests -c Release -- --quick-input-fluent-tests`
for both theme previews and checks of resource isolation, confirmation and draft
reuse. Generated PNGs are in `artifacts/render-checks/proxy-operation-quick-input-fluent-*.png`.
