using System.IO;
using System.IO.Compression;
using System.Net;
using System.Net.Http;
using System.Windows;
using System.Windows.Media;
using RtlTerminal;

internal static partial class Program
{
    private static void CheckOptionalFonts(bool live = false)
    {
        using (var manifest = typeof(OptionalFonts).Assembly.GetManifestResourceStream("RtlTerminal.FontManifest.json"))
            if (manifest == null || manifest.Length == 0) throw new Exception("Font manifest must be an assembly resource, independent of WPF content-file resolution");
        if (Application.Current == null) _ = new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
        var root = Path.GetFullPath(Path.Combine("artifacts", "font-download-tests-" + Guid.NewGuid().ToString("N")));
        Directory.CreateDirectory(root);
        foreach (var package in OptionalFonts.Packages)
        {
            if (OptionalFonts.IsInstalled(package, root)) throw new Exception("Missing optional font was shown as installed");
            if (live)
            {
                Console.WriteLine("Downloading official GitHub package: " + package.Name);
                var downloaded = OptionalFonts.InstallAsync(package, null, default, root).GetAwaiter().GetResult();
                if (!OptionalFonts.IsInstalled(package, root)) throw new Exception("Live font download did not commit");
                VerifyFamily(package, root);
                Console.WriteLine("PASS GitHub download / SHA256 / WPF private family " + package.Name + ": " + downloaded + " bytes fetched");
                continue;
            }
            var original = Path.Combine("artifacts", "optional-font-originals", package.Id);
            var officialZip = Path.Combine("artifacts", "official-font-archives", package.Id + ".zip");
            var hasOfficialZip = File.Exists(officialZip) && new FileInfo(officialZip).Length > 0;
            // A cancelled live download is not a completed offline fixture.
            if (hasOfficialZip)
            {
                try { using var check = ZipFile.OpenRead(officialZip); _ = check.Entries.Count; }
                catch (InvalidDataException) { hasOfficialZip = false; }
            }
            if (hasOfficialZip || Directory.Exists(original))
            {
                var zip = Path.Combine(root, package.Id + ".zip");
                if (hasOfficialZip) File.Copy(officialZip, zip); else ZipFile.CreateFromDirectory(original, zip);
                // An unrelated, uncompressed 8 MB file must never be downloaded
                // in full just because it shares the upstream ZIP.
                using (var archive = ZipFile.Open(zip, ZipArchiveMode.Update))
                using (var padding = archive.CreateEntry("unused-weight.bin", CompressionLevel.NoCompression).Open())
                { var block = new byte[8192]; for (var i = 0; i < 1024; i++) padding.Write(block); }
                var handler = new FontResponse(File.ReadAllBytes(zip));
                using var client = new HttpClient(handler);
                OptionalFonts.InstallAsync(package, null, default, root, client).GetAwaiter().GetResult();
                if (!OptionalFonts.IsInstalled(package, root)) throw new Exception("Verified package was not installed");
                if (handler.BytesReturned >= new FileInfo(zip).Length - 2_000_000) throw new Exception("Selective downloader fetched unrelated ZIP content");
                Console.WriteLine($"PASS {package.Id}: fetched {handler.BytesReturned} bytes from {new FileInfo(zip).Length}-byte test ZIP");
                VerifyFamily(package, root);
            }
        }
        if (!live)
        {
            var badRoot = Path.Combine(root, "invalid");
            var corrupt = Path.Combine(root, "corrupt.zip");
            using (var zip = ZipFile.Open(corrupt, ZipArchiveMode.Create))
            {
                using var writer = new StreamWriter(zip.CreateEntry("../../Iosevka-Regular.ttf").Open()); writer.Write("invalid font");
            }
            using var client = new HttpClient(new FontResponse(File.ReadAllBytes(corrupt)));
            try { OptionalFonts.InstallAsync(OptionalFonts.Packages[0], null, default, badRoot, client).GetAwaiter().GetResult(); throw new Exception("Corrupt package installed"); }
            catch (InvalidDataException) { }
            if (OptionalFonts.IsInstalled(OptionalFonts.Packages[0], badRoot) || Directory.GetDirectories(badRoot, ".download-*").Length != 0 || File.Exists(Path.Combine(root, "Iosevka-Regular.ttf")))
                throw new Exception("Failed download left an installation or extracted outside staging");
            using var cancelled = new CancellationTokenSource(); cancelled.Cancel();
            try { OptionalFonts.InstallAsync(OptionalFonts.Packages[0], null, cancelled.Token, badRoot, client).GetAwaiter().GetResult(); throw new Exception("Cancelled package installed"); }
            catch (OperationCanceledException) { }
            using var failed = new HttpClient(new FontResponse([], HttpStatusCode.NotFound));
            try { OptionalFonts.InstallAsync(OptionalFonts.Packages[0], null, default, badRoot, failed).GetAwaiter().GetResult(); throw new Exception("404 package installed"); }
            catch (HttpRequestException) { }
            using var ignoredRanges = new HttpClient(new FontResponse(new byte[1000], ignoreRanges: true));
            try { OptionalFonts.InstallAsync(OptionalFonts.Packages[0], null, default, badRoot, ignoredRanges).GetAwaiter().GetResult(); throw new Exception("Ignored HTTP ranges were accepted"); }
            catch (IOException ex) when (ex.Message.StartsWith("This server or proxy")) { }
            foreach (var wrongTag in new[] { false, true })
            {
                using var invalid = new HttpClient(new FontResponse(new byte[1000], wrongRange: !wrongTag, wrongTag: wrongTag));
                try { OptionalFonts.InstallAsync(OptionalFonts.Packages[0], null, default, badRoot, invalid).GetAwaiter().GetResult(); throw new Exception("Invalid range or changed ETag was accepted"); }
                catch (Exception ex) when (ex is IOException or InvalidDataException) { }
            }
            var settings = new FontSettingsWindow("Cascadia Mono", 14, FontWeights.Normal, FontStyles.Normal, 2000);
            settings.Show(); CaptureOperation(settings, "optional-font-rows"); settings.Close();
        }
        Console.WriteLine("PASS optional fonts: verified installation, private WPF families, invalid archive/hash, path isolation, cancellation and HTTP failure cleanup");
    }
    private static void VerifyFamily(OptionalFontPackage package, string root)
    {
        var family = new FontFamily(new Uri(OptionalFonts.DirectoryFor(package, root) + Path.DirectorySeparatorChar), "./#" + (package.Id == "JetBrainsMonoNF" ? "JetBrainsMono NFM" : package.Name));
        foreach (var (weight, style) in new[] { (FontWeights.Normal, FontStyles.Normal), (FontWeights.Bold, FontStyles.Normal), (FontWeights.Normal, FontStyles.Italic), (FontWeights.Bold, FontStyles.Italic) })
            if (!new Typeface(family, style, weight, FontStretches.Normal).TryGetGlyphTypeface(out var face) || !face.FontUri.IsFile || !face.FontUri.LocalPath.StartsWith(root, StringComparison.OrdinalIgnoreCase))
                throw new Exception("Downloaded font resolved to a fallback: " + package.Name);
    }
    private sealed class FontResponse(byte[] content, HttpStatusCode status = HttpStatusCode.OK, bool ignoreRanges = false, bool wrongRange = false, bool wrongTag = false) : HttpMessageHandler
    {
        public long BytesReturned;
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();
            if (status != HttpStatusCode.OK || ignoreRanges) return Task.FromResult(new HttpResponseMessage(status) { Content = new ByteArrayContent(content) });
            var range = request.Headers.Range?.Ranges.Single() ?? throw new Exception("Request omitted its range");
            var start = range.From!.Value; var end = range.To!.Value;
            var response = new HttpResponseMessage(HttpStatusCode.PartialContent) { Content = new ByteArrayContent(content[(int)start..((int)end + 1)]) };
            response.Content.Headers.ContentRange = new System.Net.Http.Headers.ContentRangeHeaderValue(start, wrongRange && end > 0 ? end - 1 : end, content.Length);
            response.Headers.ETag = new System.Net.Http.Headers.EntityTagHeaderValue(wrongTag && end > 0 ? "\"changed\"" : "\"test-font\"");
            BytesReturned += end - start + 1;
            return Task.FromResult(response);
        }
    }
}
