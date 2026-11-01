using System.IO;
using System.Security.AccessControl;
using System.Security.Cryptography;
using System.Security.Principal;
using System.Text.Json;

namespace RtlTerminal.Proxy;

public sealed record ProxyDescriptor(string Name, string Version, string Location, string Scope);
public sealed record ProxyHistoryEntry(ProxyDescriptor Provider, ProxyState State);
public sealed record ProxySnapshot(Guid Id, DateTimeOffset Created, string Operation, bool Complete, List<ProxyHistoryEntry> Entries)
{
    public int Schema { get; init; } = 1;
    public List<string> AffectedProviders { get; init; } = [];
}
internal sealed record ProtectedSnapshot(byte[] Payload, string Hash);
public sealed record ProxyHistoryItem(string Path, ProxySnapshot Snapshot)
{
    public override string ToString() => $"{Snapshot.Created.LocalDateTime:g} · {Snapshot.Operation} · {string.Join(", ", Snapshot.AffectedProviders)} · Complete (managed scope)";
}

public sealed class ProxyHistoryStore
{
    public string DirectoryPath { get; }
    public ProxyHistoryStore(string? directory = null) => DirectoryPath = directory ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "RtlTerminal", "ProxyHistory");
    private void Prepare()
    {
        Directory.CreateDirectory(DirectoryPath);
        if ((File.GetAttributes(DirectoryPath) & FileAttributes.ReparsePoint) != 0) throw new IOException("History directory must not be a symbolic link.");
        var sid = WindowsIdentity.GetCurrent().User ?? throw new UnauthorizedAccessException();
        var security = new DirectorySecurity(); security.SetOwner(sid); security.SetAccessRuleProtection(true, false);
        security.AddAccessRule(new FileSystemAccessRule(sid, FileSystemRights.FullControl, InheritanceFlags.ContainerInherit | InheritanceFlags.ObjectInherit, PropagationFlags.None, AccessControlType.Allow));
        security.AddAccessRule(new FileSystemAccessRule(new SecurityIdentifier(WellKnownSidType.LocalSystemSid, null), FileSystemRights.FullControl, InheritanceFlags.ContainerInherit | InheritanceFlags.ObjectInherit, PropagationFlags.None, AccessControlType.Allow));
        new DirectoryInfo(DirectoryPath).SetAccessControl(security);
    }
    public FileStream AcquireLock()
    {
        Prepare();
        try { return new FileStream(Path.Combine(DirectoryPath, "operation.lock"), FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None); }
        catch (IOException) { throw new IOException("Another proxy operation is in progress. Wait for it to finish."); }
    }
    public async Task<ProxyHistoryItem> SaveAsync(string operation, IEnumerable<ProxyHistoryEntry> entries, CancellationToken ct, IEnumerable<string>? affected = null)
    {
        Prepare();
        var snapshot = new ProxySnapshot(Guid.NewGuid(), DateTimeOffset.UtcNow, operation, true, entries.ToList()) { AffectedProviders = affected?.ToList() ?? [] };
        if (snapshot.Entries.Select(e => e.Provider.Name).Distinct().Count() != snapshot.Entries.Count) throw new IOException("Duplicate provider identities in snapshot.");
        var payload = JsonSerializer.SerializeToUtf8Bytes(snapshot);
        if (payload.Length > 32 * 1024 * 1024) throw new IOException("Snapshot exceeds the safe 32 MB limit. No changes were made.");
        var path = Path.Combine(DirectoryPath, snapshot.Created.UtcDateTime.ToString("yyyyMMddHHmmssffff") + "-" + snapshot.Id.ToString("N") + ".proxybackup");
        try
        {
            var envelope = JsonSerializer.SerializeToUtf8Bytes(new ProtectedSnapshot(payload, Convert.ToHexString(SHA256.HashData(payload))));
            try { await AtomicWrite(path, ProtectedData.Protect(envelope, null, DataProtectionScope.CurrentUser), ct); }
            finally { CryptographicOperations.ZeroMemory(envelope); }
            var verified = await ReadAsync(path, ct);
            // Commit and validate replacement before deleting an older valid snapshot.
            var history = await ListAsync(ct);
            foreach (var old in history.Skip(10)) File.Delete(old.Path);
            var validPaths = history.Select(h => h.Path).ToHashSet(StringComparer.OrdinalIgnoreCase);
            var invalid = Directory.EnumerateFiles(DirectoryPath, "*.proxybackup")
                .Where(p => !validPaths.Contains(p) && System.Text.RegularExpressions.Regex.IsMatch(Path.GetFileName(p), @"^\d{18}-[0-9a-f]{32}\.proxybackup$", System.Text.RegularExpressions.RegexOptions.IgnoreCase))
                .OrderByDescending(File.GetLastWriteTimeUtc).ToArray();
            // Keep at most two damaged application-owned records for diagnosis.
            foreach (var old in invalid.Skip(2)) File.Delete(old);
            return verified;
        }
        finally { CryptographicOperations.ZeroMemory(payload); }
    }
    public async Task<ProxyHistoryItem> ReadAsync(string path, CancellationToken ct)
    {
        if (new FileInfo(path).Length > 64 * 1024 * 1024) throw new IOException("Snapshot is too large.");
        var raw = ProtectedData.Unprotect(await File.ReadAllBytesAsync(path, ct), null, DataProtectionScope.CurrentUser);
        try
        {
            var envelope = JsonSerializer.Deserialize<ProtectedSnapshot>(raw) ?? throw new IOException("Malformed snapshot.");
            try
            {
                if (Convert.ToHexString(SHA256.HashData(envelope.Payload)) != envelope.Hash) throw new IOException("Snapshot integrity validation failed.");
                var snapshot = JsonSerializer.Deserialize<ProxySnapshot>(envelope.Payload) ?? throw new IOException("Missing snapshot metadata.");
                if (snapshot.Schema != 1 || !snapshot.Complete || snapshot.Entries.Count == 0 || snapshot.Entries.Select(e => e.Provider.Name).Distinct().Count() != snapshot.Entries.Count) throw new IOException("Snapshot is incomplete or uses an unsupported schema.");
                return new(path, snapshot);
            }
            finally { CryptographicOperations.ZeroMemory(envelope.Payload); }
        }
        finally { CryptographicOperations.ZeroMemory(raw); }
    }
    public async Task<List<ProxyHistoryItem>> ListAsync(CancellationToken ct)
    {
        if (!Directory.Exists(DirectoryPath)) return [];
        var result = new List<ProxyHistoryItem>();
        foreach (var path in Directory.EnumerateFiles(DirectoryPath, "*.proxybackup"))
        {
            ct.ThrowIfCancellationRequested();
            try { result.Add(await ReadAsync(path, ct)); }
            catch (Exception ex) when (ex is CryptographicException or IOException or JsonException or FormatException) { /* Corrupt snapshots are never presented as restorable. */ }
        }
        return result.OrderByDescending(e => e.Snapshot.Created).ToList();
    }
    public string JournalPath => Path.Combine(DirectoryPath, "operation.pending");
    public async Task MarkPendingAsync(ProxyHistoryItem snapshot, CancellationToken ct) => await AtomicWrite(JournalPath,
        ProtectedData.Protect(JsonSerializer.SerializeToUtf8Bytes(new { Snapshot = snapshot.Path, Started = DateTimeOffset.UtcNow }), null, DataProtectionScope.CurrentUser), ct);
    public void MarkComplete() { if (File.Exists(JournalPath)) File.Delete(JournalPath); }
    public Task ClearAsync(CancellationToken ct)
    {
        using var operationLock = AcquireLock();
        var root = Path.GetFullPath(DirectoryPath);
        foreach (var path in Directory.EnumerateFiles(root, "*.proxybackup", SearchOption.TopDirectoryOnly))
        {
            ct.ThrowIfCancellationRequested();
            var fullPath = Path.GetFullPath(path);
            if (!string.Equals(Path.GetDirectoryName(fullPath), root, StringComparison.OrdinalIgnoreCase) ||
                !System.Text.RegularExpressions.Regex.IsMatch(Path.GetFileName(fullPath), @"^\d{18}-[0-9a-f]{32}\.proxybackup$", System.Text.RegularExpressions.RegexOptions.IgnoreCase)) continue;
            File.Delete(fullPath);
        }
        MarkComplete();
        return Task.CompletedTask;
    }
    private static async Task AtomicWrite(string path, byte[] bytes, CancellationToken ct)
    {
        var temporary = path + ".tmp-" + Guid.NewGuid().ToString("N");
        try
        {
            await using (var stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None, 4096, FileOptions.WriteThrough))
            { await stream.WriteAsync(bytes, ct); stream.Flush(true); }
            File.Move(temporary, path, true);
        }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
    }
}
