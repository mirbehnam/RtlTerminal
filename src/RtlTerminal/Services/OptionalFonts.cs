using System.IO;
using System.IO.Compression;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text.Json;
using System.Windows;

namespace RtlTerminal;

public sealed record OptionalFontPackage(string Id, string Name, string Version, string Url);
public static class OptionalFonts
{
    public static IReadOnlyList<OptionalFontPackage> Packages { get; } = new OptionalFontPackage[]
    {
        new("Iosevka", "Iosevka", "v34.9.1", "https://github.com/be5invis/Iosevka/releases/download/v34.9.1/PkgTTF-Iosevka-34.9.1.zip"),
        new("CascadiaMonoNF", "Cascadia Mono NF", "v2407.24", "https://github.com/microsoft/cascadia-code/releases/download/v2407.24/CascadiaCode-2407.24.zip"),
        new("JetBrainsMonoNF", "JetBrains Mono NF", "v3.5.1", "https://github.com/ryanoasis/nerd-fonts/releases/download/v3.5.1/JetBrainsMono.zip")
    };
    public static string Root => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "RtlTerminal", "Fonts");
    public static OptionalFontPackage? Find(string name) => Packages.FirstOrDefault(p => p.Name == BundledFonts.CanonicalName(name));
    public static string DirectoryFor(OptionalFontPackage p, string? root = null) => Path.Combine(root ?? Root, p.Id, p.Version);
    private static readonly Lazy<Dictionary<string, Dictionary<string, string>>> Manifest = new(() =>
    {
        using var stream = typeof(OptionalFonts).Assembly.GetManifestResourceStream("RtlTerminal.FontManifest.json")
            ?? throw new InvalidDataException("The font download manifest is missing. Reinstall RTL Terminal.");
        using var doc = JsonDocument.Parse(stream);
        return doc.RootElement.EnumerateArray().ToDictionary(e => e.GetProperty("id").GetString()!, e => e.GetProperty("files").EnumerateArray()
            .ToDictionary(f => f.GetProperty("file").GetString()!, f => f.GetProperty("sha256").GetString()!, StringComparer.OrdinalIgnoreCase));
    });
    public static bool IsInstalled(OptionalFontPackage p, string? root = null)
    {
        var folder = DirectoryFor(p, root);
        if (!Directory.Exists(folder)) return false;
        try { return Manifest.Value[p.Id].Keys.All(f => File.Exists(Path.Combine(folder, f))); }
        catch (InvalidDataException) { return false; }
        catch (JsonException) { return false; }
        catch (IOException) { return false; }
        catch (UnauthorizedAccessException) { return false; }
    }
    public static async Task<long> InstallAsync(OptionalFontPackage p, IProgress<int>? progress, CancellationToken ct, string? root = null, HttpClient? client = null)
    {
        if (!Packages.Contains(p)) throw new ArgumentException("Unknown font package.");
        root ??= Root;
        Directory.CreateDirectory(root);
        // Cross-process lock; a second window cannot replace a package mid-install.
        await using var operationLock = new FileStream(Path.Combine(root, p.Id + ".lock"), FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
        if (IsInstalled(p, root)) return 0;
        var stage = Path.Combine(root, ".download-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(stage);
        var ownsClient = client == null; client ??= new HttpClient(new SocketsHttpHandler { ConnectTimeout = TimeSpan.FromSeconds(20) }) { Timeout = TimeSpan.FromMinutes(15) };
        try
        {
            using var remote = await HttpRangeStream.OpenAsync(client, p.Url, ct);
            using var archive = new ZipArchive(remote, ZipArchiveMode.Read, leaveOpen: true);
            var fonts = Path.Combine(stage, "fonts"); Directory.CreateDirectory(fonts);
            await ExtractVerifiedAsync(p, archive, fonts, ct, progress);
            ct.ThrowIfCancellationRequested();
            var destination = DirectoryFor(p, root); Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
            // No existing valid installation is removed. Incomplete directories are
            // retained for diagnosis rather than deleting unknown user files.
            if (Directory.Exists(destination)) throw new IOException("An incomplete font installation already exists. Close the app and review its Fonts directory before retrying.");
            Directory.Move(fonts, destination);
            progress?.Report(100);
            return remote.BytesTransferred;
        }
        finally
        {
            if (ownsClient) client.Dispose();
            // Only this operation's randomly named staging directory is removed.
            try { if (Directory.Exists(stage)) Directory.Delete(stage, true); }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
        }
    }
    public static async Task ExtractVerifiedAsync(OptionalFontPackage p, string archivePath, string target, CancellationToken ct)
    {
        if (!Packages.Contains(p)) throw new ArgumentException("Unknown font package.");
        using var archive = ZipFile.OpenRead(archivePath);
        await ExtractVerifiedAsync(p, archive, target, ct);
    }
    private static async Task ExtractVerifiedAsync(OptionalFontPackage p, ZipArchive archive, string target, CancellationToken ct, IProgress<int>? progress = null)
    {
        var completed = 0;
        foreach (var file in Manifest.Value[p.Id])
        {
            ct.ThrowIfCancellationRequested();
            var matches = archive.Entries.Where(e => string.Equals(e.Name, file.Key, StringComparison.OrdinalIgnoreCase)).ToArray();
            if (matches.Length != 1 || matches[0].Length is <= 0 or > 20_000_000) throw new InvalidDataException("The font archive is missing an expected file or contains ambiguous files.");
            var path = Path.Combine(target, file.Key);
            await using (var input = matches[0].Open())
            await using (var output = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None, 81920, true))
            {
                var buffer = new byte[81920]; int count; long total = 0;
                while ((count = await input.ReadAsync(buffer, ct)) > 0)
                {
                    total += count; if (total > 20_000_000) throw new InvalidDataException("Font file exceeds the allowed size.");
                    await output.WriteAsync(buffer.AsMemory(0, count), ct);
                }
            }
            await using var verify = File.OpenRead(path);
            if (!Convert.ToHexString(await SHA256.HashDataAsync(verify, ct)).Equals(file.Value, StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("Font integrity verification failed. No font was installed.");
            progress?.Report(++completed * 95 / Manifest.Value[p.Id].Count);
        }
    }
}
