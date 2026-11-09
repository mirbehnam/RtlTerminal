using System.Diagnostics;
using System.Media;
using Microsoft.Win32;

namespace RtlTerminal;

public enum TerminalBellSound { Notification, Information, Warning, Error }

public sealed record TerminalBellSettings(bool Enabled = true, bool BackgroundOnly = false,
    TerminalBellSound Sound = TerminalBellSound.Notification)
{
    internal static TerminalBellSettings Read(RegistryKey? key) => new(
        key?.GetValue("BellEnabled") is not int enabled || enabled != 0,
        key?.GetValue("BellBackgroundOnly") is int background && background != 0,
        Enum.TryParse<TerminalBellSound>(key?.GetValue("BellSound") as string, out var sound) && Enum.IsDefined(sound)
            ? sound : TerminalBellSound.Notification);

    internal void Write(RegistryKey key)
    {
        key.SetValue("BellEnabled", Enabled ? 1 : 0, RegistryValueKind.DWord);
        key.SetValue("BellBackgroundOnly", BackgroundOnly ? 1 : 0, RegistryValueKind.DWord);
        key.SetValue("BellSound", (Enum.IsDefined(Sound) ? Sound : TerminalBellSound.Notification).ToString());
    }
}

// One instance per main window: all tabs share a rate limit, independent of rendering.
public sealed class TerminalBellService
{
    private readonly object _gate = new();
    private readonly Action<TerminalBellSound> _play;
    private readonly Func<long> _milliseconds;
    private readonly Action<Action> _schedule;
    private long? _lastBell;
    private bool _pending;
    private TerminalBellSettings _settings;

    public TerminalBellService(TerminalBellSettings settings)
        : this(settings, PlayWindowsSound, () => Environment.TickCount64, action => _ = Task.Run(action)) { }

    internal TerminalBellService(TerminalBellSettings settings, Action<TerminalBellSound> play,
        Func<long> milliseconds, Action<Action> schedule)
    {
        _settings = settings; _play = play; _milliseconds = milliseconds; _schedule = schedule;
    }

    public void UpdateSettings(TerminalBellSettings settings) { lock (_gate) _settings = settings; }

    public bool Notify(bool windowActive, bool preview = false, TerminalBellSound? previewSound = null)
    {
        TerminalBellSound sound;
        lock (_gate)
        {
            if (!preview && (!_settings.Enabled || (_settings.BackgroundOnly && windowActive))) return false;
            var now = _milliseconds();
            if (_pending || (_lastBell.HasValue && now - _lastBell.Value < 500)) return false;
            _lastBell = now; _pending = true;
            sound = previewSound ?? _settings.Sound;
        }
        try
        {
            _schedule(() =>
            {
                try { _play(sound); }
                catch (Exception ex) { Trace.TraceWarning("Terminal bell playback failed ({0}).", ex.GetType().Name); }
                finally { lock (_gate) _pending = false; }
            });
        }
        catch (Exception ex)
        {
            lock (_gate) _pending = false;
            Trace.TraceWarning("Terminal bell scheduling failed ({0}).", ex.GetType().Name);
            return false;
        }
        return true;
    }

    private static void PlayWindowsSound(TerminalBellSound sound) => (sound switch
    {
        TerminalBellSound.Information => SystemSounds.Asterisk,
        TerminalBellSound.Warning => SystemSounds.Exclamation,
        TerminalBellSound.Error => SystemSounds.Hand,
        _ => SystemSounds.Beep
    }).Play();
}
