# Fullscreen

Press **F11** (or choose **View → Full screen**) to fill the current monitor.
The tabs, menus, window buttons and outer padding are hidden; the terminal and
its split panes use the available space. The active window covers the taskbar
without changing Windows taskbar settings or becoming always on top.

Press **F11** again to restore the previous window size, position and normal or
maximized state. Fullscreen is not saved between launches.

Tab, pane and macro keyboard shortcuts remain available. **Ctrl+F** opens search;
**Escape** closes search, while **F11** exits fullscreen. Switching to another
application is allowed and does not end fullscreen.

The normal maximized window continues to respect the taskbar work area.

Native window checks (briefly opens shell-free test windows):

```powershell
dotnet run --project tests/RtlTerminal.RenderTests -c Release -- --window-smoke
```

The checks exercise all connected monitors, repeated F11 transitions from normal
and maximized states, header visibility, monitor bounds and placement restoration.
