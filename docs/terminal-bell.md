# Terminal bell

RTL Terminal plays a Windows sound when a CLI sends a standalone BEL (U+0007).
It does not infer when an AI agent has finished: enable the CLI's own terminal-bell
notification if it provides one. BEL used to terminate OSC commands never plays a sound.

In **Settings → General → Terminal bell**, enable/disable sound, restrict playback
to when the main window is inactive, choose Notification/Information/Warning/Error,
and use **Test sound**. Changes take effect after **Apply**, across all tabs and split
panes. Closing settings without applying preserves the previous preferences.
The defaults are enabled, Notification, and playback in foreground and background.
Test sound previews the selection even when playback is disabled or background-only.

All panes share a 500 ms rate limit; repeated bells are coalesced, not queued for later.
Sound playback runs off the UI and terminal parser threads. No WAV files are bundled.
Windows sound scheme and volume settings determine whether these system sounds are audible.
Playback errors never stop terminal output and do not open an error dialog.

PowerShell example: `[Console]::Write([char]7)`

Run automated parser, notification-service, isolated registry persistence, settings
and live ConPTY checks with:

```powershell
dotnet run --project tests/RtlTerminal.RenderTests -c Release -- --bell-tests
```

Automated tests replace the audio sink; they verify requests and do not certify
that a particular speaker or CLI completion hook produces an audible sound.
