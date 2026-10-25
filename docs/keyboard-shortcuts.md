# Keyboard shortcut reference

Open **Help → Keyboard shortcuts**, or choose the **Keyboard shortcuts** tab in Guide. Search by a key combination, action or category. The reference is available in English, Persian and Arabic. Key combinations remain left-to-right in all three languages.

The catalogue in `src/RtlTerminal/Services/ShortcutCatalog.cs` documents tabs/profiles, configurable pane shortcuts, clipboard and mouse actions, search, font zoom, full screen, macro playback and keys forwarded to the running terminal application. Pane and macro entries explicitly identify settings that can disable them. Search Enter/Escape and tab-rename Enter/Escape are contextual; other applications running inside the terminal can define additional shortcuts of their own.

Font zoom remains available via Ctrl++ / Ctrl+=, Ctrl+-, Ctrl+Mouse wheel and Ctrl+0 (including the supported numpad equivalents). View no longer lists Zoom in/out. Reset zoom and its adjacent separator appear only when the font size differs from the saved preference; resetting or saving a new font size hides them again.

Focused regression and optional offscreen capture:

```powershell
$env:RTL_CAPTURE_SHORTCUTS = '1'
dotnet run --project tests/RtlTerminal.RenderTests -c Release -- --shortcut-guide-tests
```

The capture is written to the ignored `artifacts/render-checks/guide-shortcuts-persian.png`.
