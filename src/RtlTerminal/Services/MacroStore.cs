using System.IO;
using System.Security.AccessControl;
using System.Security.Cryptography;
using System.Security.Principal;
using System.Text.Json;

namespace RtlTerminal;

public sealed record MacroEntry(Guid Id, string Name, MacroMode Mode, int Shortcut, bool PasswordProtected)
{
    public DateTimeOffset? CreatedAt { get; init; }
    public override string ToString() => Name + " · " + UiText.Get(Mode.ToString()) + " · " + UiText.Get(PasswordProtected ? "Password protected" : "Windows protected") +
        (Shortcut > 0 ? $"  (Ctrl+Shift+Alt+{Shortcut})" : "");
}

public sealed class MacroStore
{
    public string DirectoryPath { get; }
    public MacroStore(string? directory = null) => DirectoryPath = directory ?? Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "RtlTerminal", "Macros");

    private sealed record Envelope(int Version, MacroEntry Entry, byte[] Payload, byte[] Salt, byte[] Nonce, byte[] Tag);
    private string FilePath(Guid id) => Path.Combine(DirectoryPath, id.ToString("N") + ".rtmacro");

    private void EnsureDirectory()
    {
        var directory = Directory.CreateDirectory(DirectoryPath);
        if ((directory.Attributes & FileAttributes.ReparsePoint) != 0)
            throw new IOException("The macro directory cannot be a symbolic link.");
        var security = new DirectorySecurity();
        security.SetAccessRuleProtection(true, false);
        security.AddAccessRule(new FileSystemAccessRule(WindowsIdentity.GetCurrent().User!, FileSystemRights.FullControl,
            InheritanceFlags.ContainerInherit | InheritanceFlags.ObjectInherit, PropagationFlags.None, AccessControlType.Allow));
        security.AddAccessRule(new FileSystemAccessRule(new SecurityIdentifier(WellKnownSidType.LocalSystemSid, null),
            FileSystemRights.FullControl, InheritanceFlags.ContainerInherit | InheritanceFlags.ObjectInherit,
            PropagationFlags.None, AccessControlType.Allow));
        directory.SetAccessControl(security);
    }

    private Envelope Read(Guid id)
    {
        var path = FilePath(id);
        var info = new FileInfo(path);
        if (info.Length > 16_000_000 || (info.Attributes & FileAttributes.ReparsePoint) != 0)
            throw new InvalidDataException("Invalid macro file.");
        var clear = ProtectedData.Unprotect(File.ReadAllBytes(path), null, DataProtectionScope.CurrentUser);
        try
        {
            var envelope = JsonSerializer.Deserialize<Envelope>(clear) ?? throw new InvalidDataException("Invalid macro file.");
            if (envelope.Version is < 1 or > 2 || envelope.Entry.Id != id) throw new InvalidDataException("Unsupported macro file.");
            return envelope;
        }
        finally { CryptographicOperations.ZeroMemory(clear); }
    }

    public IReadOnlyList<MacroEntry> List()
    {
        if (!Directory.Exists(DirectoryPath)) return [];
        // Report damaged files instead of silently hiding recordings.
        return Directory.EnumerateFiles(DirectoryPath, "*.rtmacro")
            .Select(path => Guid.TryParseExact(Path.GetFileNameWithoutExtension(path), "N", out var id)
                ? Read(id).Entry is { } entry ? entry with { CreatedAt = entry.CreatedAt ?? new DateTimeOffset(File.GetCreationTimeUtc(path)) } : throw new InvalidDataException("Invalid macro entry.")
                : throw new InvalidDataException("Invalid macro filename."))
            .OrderBy(e => e.Name, StringComparer.CurrentCultureIgnoreCase).ToArray();
    }

    public void Save(TerminalMacro macro, string? password, bool askSensitive = false)
    {
        macro = MacroPrivacy.Prepare(macro, askSensitive);
        macro.FormatVersion = 2;
        macro.Validate();
        EnsureDirectory();
        var entries = List();
        var createdAt = entries.FirstOrDefault(e => e.Id == macro.Id)?.CreatedAt ?? DateTimeOffset.UtcNow;
        if (entries.Any(e => e.Id != macro.Id && macro.Shortcut != 0 && e.Shortcut == macro.Shortcut))
            throw new InvalidOperationException("This macro shortcut is already assigned.");
        var payload = JsonSerializer.SerializeToUtf8Bytes(macro);
        byte[] salt = [], nonce = [], tag = [];
        var locked = !string.IsNullOrEmpty(password);
        try
        {
            if (locked)
            {
                salt = RandomNumberGenerator.GetBytes(16);
                nonce = RandomNumberGenerator.GetBytes(12);
                tag = new byte[16];
                var key = Rfc2898DeriveBytes.Pbkdf2(password!, salt, 600000, HashAlgorithmName.SHA256, 32);
                try
                {
                    var encrypted = new byte[payload.Length];
                    using var aes = new AesGcm(key, 16);
                    aes.Encrypt(nonce, payload, encrypted, tag, macro.Id.ToByteArray());
                    CryptographicOperations.ZeroMemory(payload);
                    payload = encrypted;
                }
                finally { CryptographicOperations.ZeroMemory(key); }
            }
            var envelope = new Envelope(2, new MacroEntry(macro.Id, macro.Name, macro.Mode, macro.Shortcut, locked) { CreatedAt = createdAt }, payload, salt, nonce, tag);
            var clear = JsonSerializer.SerializeToUtf8Bytes(envelope);
            byte[] protectedBytes;
            try { protectedBytes = ProtectedData.Protect(clear, null, DataProtectionScope.CurrentUser); }
            finally { CryptographicOperations.ZeroMemory(clear); }
            var temporary = Path.Combine(DirectoryPath, Guid.NewGuid().ToString("N") + ".tmp");
            try
            {
                using (var stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                { stream.Write(protectedBytes); stream.Flush(true); }
                File.Move(temporary, FilePath(macro.Id), true);
            }
            finally { if (File.Exists(temporary)) File.Delete(temporary); }
        }
        finally { CryptographicOperations.ZeroMemory(payload); }
    }

    public TerminalMacro Load(Guid id, string? password)
    {
        var envelope = Read(id);
        var payload = envelope.Payload;
        try
        {
            if (envelope.Entry.PasswordProtected)
            {
                if (string.IsNullOrEmpty(password)) throw new CryptographicException("A password is required.");
                var key = Rfc2898DeriveBytes.Pbkdf2(password, envelope.Salt, 600000, HashAlgorithmName.SHA256, 32);
                try
                {
                    var clear = new byte[payload.Length];
                    using var aes = new AesGcm(key, 16);
                    aes.Decrypt(envelope.Nonce, payload, envelope.Tag, clear, id.ToByteArray());
                    payload = clear;
                }
                finally { CryptographicOperations.ZeroMemory(key); }
            }
            var macro = JsonSerializer.Deserialize<TerminalMacro>(payload) ?? throw new InvalidDataException("Invalid macro.");
            macro.Validate();
            if (macro.Id != id || macro.Name != envelope.Entry.Name || macro.Shortcut != envelope.Entry.Shortcut || macro.Mode != envelope.Entry.Mode)
                throw new InvalidDataException("Macro metadata does not match.");
            return macro;
        }
        finally { CryptographicOperations.ZeroMemory(payload); }
    }

    public void Delete(Guid id) => File.Delete(FilePath(id));
}
