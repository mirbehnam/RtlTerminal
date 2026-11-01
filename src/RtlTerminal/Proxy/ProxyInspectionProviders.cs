using System.Runtime.InteropServices;
using System.ComponentModel;

namespace RtlTerminal.Proxy;

// Inspection records make machine/process overrides explicit in comprehensive
// snapshots. Restoration never escalates or mutates these scopes implicitly.
public sealed class EnvironmentScopeInspectionProvider(EnvironmentVariableTarget scope) : IProxyProvider
{
    public bool CanRestore => false;
    public string Name => "Environment " + scope + " (inspection)";
    public ProxyDescriptor Descriptor => new(Name, "schema-1", "Windows environment", scope.ToString());
    public Task<ProxyState> ReadAsync(CancellationToken ct) => Task.FromResult(new ProxyState(EnvironmentProxyProvider.Variables.ToDictionary(n => n, n => Environment.GetEnvironmentVariable(n, scope))));
    public void Validate(ProxyEndpoint e) => throw new NotSupportedException("This scope is inspection-only.");
    public ProxyState Desired(ProxyEndpoint e, ProxyState previous, bool clear) => previous;
    public async Task WriteAsync(ProxyState state, CancellationToken ct)
    {
        if ((await ReadAsync(ct)).Signature != state.Signature) throw new UnauthorizedAccessException("Inspection scope changed; explicit administrative/manual recovery is required.");
    }
}

public sealed class WinHttpBasicInspectionProvider : IProxyProvider
{
    public bool CanRestore => false;
    public string Name => "WinHTTP basic Machine (inspection)";
    public ProxyDescriptor Descriptor => new(Name, "schema-1", "WinHttpGetDefaultProxyConfiguration", "Machine");
    [StructLayout(LayoutKind.Sequential)] private struct Info { public uint Access; public IntPtr Proxy; public IntPtr Bypass; }
    [DllImport("winhttp.dll", SetLastError = true)] private static extern bool WinHttpGetDefaultProxyConfiguration(out Info info);
    [DllImport("kernel32.dll")] private static extern IntPtr GlobalFree(IntPtr value);
    public Task<ProxyState> ReadAsync(CancellationToken ct)
    {
        if (!WinHttpGetDefaultProxyConfiguration(out var info)) throw new Win32Exception(Marshal.GetLastWin32Error());
        try { return Task.FromResult(new ProxyState(new() { ["Access"] = info.Access.ToString(), ["Proxy"] = Marshal.PtrToStringUni(info.Proxy), ["Bypass"] = Marshal.PtrToStringUni(info.Bypass) })); }
        finally { if (info.Proxy != IntPtr.Zero) GlobalFree(info.Proxy); if (info.Bypass != IntPtr.Zero) GlobalFree(info.Bypass); }
    }
    public void Validate(ProxyEndpoint e) => throw new NotSupportedException("Machine WinHTTP is inspection-only.");
    public ProxyState Desired(ProxyEndpoint e, ProxyState old, bool clear) => old;
    public async Task WriteAsync(ProxyState state, CancellationToken ct)
    {
        if ((await ReadAsync(ct)).Signature != state.Signature) throw new UnauthorizedAccessException("Machine WinHTTP differs from the snapshot; explicit administrative recovery is required.");
    }
}
