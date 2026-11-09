# Moving a tab to another window

Press and drag a tab title, then release it outside the RTL Terminal window.
The whole tab moves to a new window, including splits, layout proportions, title,
tab color, buffers, running ConPTY sessions, scroll state and Quick Input drafts.
No command is replayed and no shell is restarted. Releasing within the original
window or pressing Escape cancels detachment. Close/rename controls retain their
existing behavior.
After passing the drag threshold, the tab lifts slightly and becomes translucent
with a move cursor. Release, Escape, lost capture or window deactivation restores
its original appearance immediately.

The empty source window closes after its last tab is moved. Its remaining tabs,
if any, keep running. Clipboard attachment files remain available until all
terminal windows close, so closing the source cannot invalidate a moved draft.
The window shares the existing directory-history store.

Finish macro recording or stop playback before moving its tab. A tab whose shell
is still starting cannot be detached until startup completes. This feature moves
tabs into new windows; dropping into another existing window is not implemented.

Automated coverage uses real CMD sessions to verify persistent shell variables,
output after source closure, split identity, draft transfer and source-view reuse:

```powershell
dotnet run --project tests/RtlTerminal.RenderTests -c Release -- --tab-history-tests
```
