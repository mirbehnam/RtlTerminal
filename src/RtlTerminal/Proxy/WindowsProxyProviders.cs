using Microsoft.Win32;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Text.Json;

namespace RtlTerminal.Proxy;

public sealed class EnvironmentProxyProvider : IProxyProvider
{
    public static readonly string[] Variables = ["HTTP_PROXY", "HTTPS_PROXY", "ALL_PROXY", "NO_PROXY", "FTP_PROXY"];
    public string Name => "Environment (User)";
    public Task<ProxyState> ReadAsync(CancellationToken ct) => Task.FromResult(new ProxyState(Variables.ToDictionary(n => n, n => Environment.GetEnvironmentVariable(n, EnvironmentVariableTarget.User))));
    public void Validate(ProxyEndpoint endpoint) => endpoint.Validate();
    public Task WriteAsync(ProxyState state, CancellationToken ct)
    {
        foreach (var n in Variables) { ct.ThrowIfCancellationRequested(); Environment.SetEnvironmentVariable(n, state[n], EnvironmentVariableTarget.User); }
        return Task.CompletedTask;
    }
    public ProxyState Desired(ProxyEndpoint e, ProxyState previous, bool clear) => new(Variables.ToDictionary(n => n,
        n => clear ? null : n == "NO_PROXY" ? e.LocalBypass(",") : e.Url));
    public static string Diagnostics() => string.Join("\n", Enum.GetValues<EnvironmentVariableTarget>().SelectMany(scope => Variables.Select(n =>
        $"{scope}: {n} = {ProxyService.Redact(Environment.GetEnvironmentVariable(n, scope) ?? "(unset)")}"))) +
        "\nWindows names are case-insensitive. Process values are inherited snapshots, not live persistent configuration. Restart parent applications to inherit changes.";
}

public sealed class WindowsProxyProvider : IProxyProvider
{
    public string Name => "Windows system (User)";
    public void Validate(ProxyEndpoint e) { e.Validate(); if (e.Protocol != "HTTP") throw new NotSupportedException("Windows manual proxy supports the HTTP endpoint in this manager. Select HTTP; SOCKS5 and HTTPS endpoints are not silently converted."); }
    public Task<ProxyState> ReadAsync(CancellationToken ct) => Task.FromResult(WindowsInternetOptions.Read());
    public Task WriteAsync(ProxyState s, CancellationToken ct) { WindowsInternetOptions.Write(s); return Task.CompletedTask; }
    public ProxyState Desired(ProxyEndpoint e, ProxyState old, bool clear) => new(new(old.Values)
    {
        ["Flags"] = clear ? "1" : Flags(e, old).ToString(),
        ["Proxy"] = clear || e.ManualEnabled == false ? "" : e.Authority,
        ["Bypass"] = clear ? "" : e.WindowsBypass,
        ["Pac"] = clear ? "" : e.Pac ?? old["Pac"]
    });
    private static int Flags(ProxyEndpoint e, ProxyState old)
    {
        var flags = int.Parse(old["Flags"]!) | 1;
        flags = e.ManualEnabled == false ? flags & ~2 : flags | 2;
        if (e.AutoDetect is { } auto) flags = auto ? flags | 8 : flags & ~8;
        if (e.Pac is { } pac) flags = pac.Length > 0 ? flags | 4 : flags & ~4;
        return flags;
    }
}

internal static class WindowsInternetOptions
{
    [StructLayout(LayoutKind.Explicit, Size = 8)] private struct Value { [FieldOffset(0)] public int Number; [FieldOffset(0)] public IntPtr Text; }
    [StructLayout(LayoutKind.Sequential)] private struct Option { public int Id; public Value Value; }
    [StructLayout(LayoutKind.Sequential)] private struct Options { public int Size; public IntPtr Connection; public int Count; public int Error; public IntPtr Items; }
    [DllImport("wininet.dll", CharSet = CharSet.Unicode, SetLastError = true)] private static extern bool InternetQueryOptionW(IntPtr handle, int option, ref Options buffer, ref int length);
    [DllImport("wininet.dll", CharSet = CharSet.Unicode, SetLastError = true)] private static extern bool InternetSetOptionW(IntPtr handle, int option, IntPtr buffer, int length);
    [DllImport("kernel32.dll")] private static extern IntPtr GlobalFree(IntPtr memory);
    public static void CheckPolicy()
    {
        foreach (var hive in new[] { Registry.CurrentUser, Registry.LocalMachine })
        foreach (var path in new[] { @"Software\Policies\Microsoft\Internet Explorer\Control Panel", @"Software\Policies\Microsoft\Windows\CurrentVersion\Internet Settings" })
        {
            using var key = hive.OpenSubKey(path);
            if (key?.GetValueNames().Any(n => n.Contains("Proxy", StringComparison.OrdinalIgnoreCase) || n.Contains("AutoConfig", StringComparison.OrdinalIgnoreCase)) == true)
                throw new UnauthorizedAccessException("Proxy settings are managed by policy.");
        }
    }
    public static ProxyState Read()
    {
        ProxyState state;
        try { state = ReadOptions([10, 2, 4]); }
        catch (Win32Exception) { state = ReadOptions([1, 2, 4]); }
        try { state.Values["Bypass"] = ReadOptions([3])["Bypass"]; }
        catch (Win32Exception ex) when (ex.NativeErrorCode == 87)
        {
            // A persisted bypass list can be rejected by WinINet's parser even
            // when its server/flags/PAC queries work. Preserve the original value;
            // never silently substitute an empty bypass or rewrite user rules.
            using var policy = Registry.LocalMachine.OpenSubKey(@"Software\Policies\Microsoft\Windows\CurrentVersion\Internet Settings");
            using var machine = Registry.LocalMachine.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Internet Settings");
            var perMachine = Equals(policy?.GetValue("ProxySettingsPerUser"), 0) || Equals(machine?.GetValue("ProxySettingsPerUser"), 0);
            using var key = (perMachine ? Registry.LocalMachine : Registry.CurrentUser).OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Internet Settings");
            var server = key?.GetValue("ProxyServer", "", RegistryValueOptions.DoNotExpandEnvironmentNames) as string ?? "";
            var pac = key?.GetValue("AutoConfigURL", "", RegistryValueOptions.DoNotExpandEnvironmentNames) as string ?? "";
            var enabled = Equals(key?.GetValue("ProxyEnable", 0), 1);
            if (enabled != ((int.Parse(state["Flags"]!) & 2) != 0) || !string.Equals(server, state["Proxy"], StringComparison.Ordinal) || !string.Equals(pac, state["Pac"], StringComparison.Ordinal))
                throw new System.IO.IOException("WinINet rejected bypass (Windows error 87), and the persistent server/PAC does not match the effective connection. Snapshot blocked to avoid backing up the wrong scope.", ex);
            var raw = key?.GetValue("ProxyOverride", null, RegistryValueOptions.DoNotExpandEnvironmentNames);
            if (raw is not null && raw is not string) throw new System.IO.IOException("ProxyOverride has an unsupported registry type; snapshot blocked.", ex);
            state.Values["Bypass"] = raw as string ?? "";
        }
        return state;
    }
    private static ProxyState ReadOptions(int[] ids)
    {
        var size = Marshal.SizeOf<Option>(); var ptr = Marshal.AllocHGlobal(size * ids.Length);
        try
        {
            for (var i = 0; i < ids.Length; i++) Marshal.StructureToPtr(new Option { Id = ids[i] }, ptr + i * size, false);
            var list = new Options { Size = Marshal.SizeOf<Options>(), Count = ids.Length, Items = ptr }; var length = list.Size;
            if (!InternetQueryOptionW(IntPtr.Zero, 75, ref list, ref length))
                throw new Win32Exception(Marshal.GetLastWin32Error(), "InternetQueryOptionW failed for option IDs " + string.Join(",", ids) + "; option index " + list.Error + ".");
            var values = new Dictionary<string, string?>();
            for (var i = 0; i < ids.Length; i++)
            {
                var item = Marshal.PtrToStructure<Option>(ptr + i * size);
                var numeric = ids[i] is 1 or 10;
                var name = ids[i] switch { 1 or 10 => "Flags", 2 => "Proxy", 3 => "Bypass", 4 => "Pac", _ => throw new ArgumentException("Unknown option") };
                values[name] = numeric ? item.Value.Number.ToString() : Marshal.PtrToStringUni(item.Value.Text) ?? "";
            }
            return new(values);
        }
        finally
        {
            for (var i = 0; i < ids.Length; i++)
            {
                var item = Marshal.PtrToStructure<Option>(ptr + i * size);
                if (ids[i] is not (1 or 10) && item.Value.Text != IntPtr.Zero) GlobalFree(item.Value.Text);
            }
            Marshal.FreeHGlobal(ptr);
        }
    }
    public static void Write(ProxyState state)
    {
        CheckPolicy();
        // Do not submit an empty PAC URL when there is no PAC setting to clear.
        // Only include that option for an actual URL update/removal.
        var includePac = !string.IsNullOrEmpty(state["Pac"]) || !string.IsNullOrEmpty(Read()["Pac"]);
        var names = includePac ? new[] { "Flags", "Proxy", "Bypass", "Pac" } : new[] { "Flags", "Proxy", "Bypass" };
        var size = Marshal.SizeOf<Option>(); var ptr = Marshal.AllocHGlobal(size * names.Length); var strings = new List<IntPtr>();
        try
        {
            for (var i = 0; i < names.Length; i++)
            {
                var value = new Value();
                if (i == 0) value.Number = int.Parse(state[names[i]]!);
                else if (!string.IsNullOrEmpty(state[names[i]])) { value.Text = Marshal.StringToHGlobalUni(state[names[i]]!); strings.Add(value.Text); }
                Marshal.StructureToPtr(new Option { Id = i + 1, Value = value }, ptr + i * size, false);
            }
            var list = new Options { Size = Marshal.SizeOf<Options>(), Count = names.Length, Error = -1, Items = ptr };
            var data = Marshal.AllocHGlobal(list.Size);
            try
            {
                Marshal.StructureToPtr(list, data, false);
                if (!InternetSetOptionW(IntPtr.Zero, 75, data, list.Size))
                {
                    var code = Marshal.GetLastWin32Error();
                    var failed = Marshal.PtrToStructure<Options>(data).Error;
                    var option = failed >= 0 && failed < names.Length ? names[failed] : "unknown";
                    throw new Win32Exception(code, $"InternetSetOptionW(PER_CONNECTION_OPTION) rejected option index {failed} ({option}); flags={state["Flags"]}; option count={names.Length}; proxy length={state["Proxy"]?.Length ?? 0}; bypass length={state["Bypass"]?.Length ?? 0}; PAC length={state["Pac"]?.Length ?? 0}.");
                }
                if (!InternetSetOptionW(IntPtr.Zero, 39, IntPtr.Zero, 0) || !InternetSetOptionW(IntPtr.Zero, 37, IntPtr.Zero, 0))
                    throw new System.IO.IOException("Settings written, but Windows notification failed. Refresh and inspect the resulting state.");
            }
            finally { Marshal.FreeHGlobal(data); }
        }
        finally { foreach (var str in strings) Marshal.FreeHGlobal(str); Marshal.FreeHGlobal(ptr); }
    }
}

public sealed class WinHttpProxyProvider : IProxyProvider
{
    public string Name => "WinHTTP (effective User)";
    private static string Netsh => System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "netsh.exe");
    public void Validate(ProxyEndpoint e) { e.Validate(); if (e.Protocol != "HTTP") throw new NotSupportedException("WinHTTP requires an HTTP proxy endpoint. HTTPS and SOCKS5 endpoints are not supported by this provider."); }
    public async Task<ProxyState> ReadAsync(CancellationToken ct)
    {
        var reading = await ReadStatusAsync(ct);
        if (reading.Scope == "Machine") throw new NotSupportedException("WinHTTP is configured at Machine scope. Machine writes are not available in this User-only dialog; no setting has been changed.");
        return reading.State;
    }
    public async Task<(ProxyState State, string Scope)> ReadStatusAsync(CancellationToken ct)
    {
        var text = await ProxyProcess.Run(Netsh, ["winhttp", "show", "advproxy"], ct);
        var start = text.IndexOf('{'); var end = text.LastIndexOf('}');
        if (start < 0 || end <= start) throw new NotSupportedException("Advanced WinHTTP configuration is not available on this Windows version. No reset or write will be attempted.");
        using var json = JsonDocument.Parse(text[start..(end + 1)]); var root = json.RootElement;
        string S(string key) => root.TryGetProperty(key, out var v) ? v.ToString() : "";
        var scope = S("PerUserProxySettings").Equals("False", StringComparison.OrdinalIgnoreCase) ? "Machine" : "User";
        return (new(new() { ["Proxy"] = S("Proxy"), ["ProxyBypass"] = S("ProxyBypass"), ["AutoconfigUrl"] = S("AutoconfigUrl"), ["AutoDetect"] = S("AutoDetect").Equals("True", StringComparison.OrdinalIgnoreCase) ? "true" : "false" }), scope);
    }
    public ProxyState Desired(ProxyEndpoint e, ProxyState old, bool clear) => new(new(old.Values)
    { ["Proxy"] = clear || e.ManualEnabled == false ? "" : e.Authority, ["ProxyBypass"] = clear ? "" : e.WindowsBypass, ["AutoconfigUrl"] = clear ? "" : e.Pac ?? old["AutoconfigUrl"], ["AutoDetect"] = clear ? "false" : e.AutoDetect?.ToString().ToLowerInvariant() ?? old["AutoDetect"] });
    public async Task WriteAsync(ProxyState s, CancellationToken ct)
    {
        WindowsInternetOptions.CheckPolicy();
        var settings = JsonSerializer.Serialize(new { Proxy = s["Proxy"], ProxyBypass = s["ProxyBypass"], AutoconfigUrl = s["AutoconfigUrl"], AutoDetect = s["AutoDetect"] == "true" });
        await ProxyProcess.Run(Netsh, ["winhttp", "set", "advproxy", "setting-scope=user", "settings=" + settings], ct);
    }
}
