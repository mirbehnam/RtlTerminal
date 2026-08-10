using System.Net.Http;
using System.Net.Http.Headers;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.IO;

namespace RtlTerminal;

public sealed record UpdateCheckResult(
    Version CurrentVersion,
    Version LatestVersion,
    string LatestTag,
    Uri ReleasePage,
    bool IsUpdateAvailable);

public static class UpdateService
{
    private const string LatestReleaseApi =
        "https://api.github.com/repos/mirbehnam/RtlTerminal/releases/latest";
    private static readonly HttpClient Client = CreateClient();

    public static async Task<UpdateCheckResult> CheckAsync(
        CancellationToken cancellationToken = default)
    {
        using var response = await Client.GetAsync(
            LatestReleaseApi,
            HttpCompletionOption.ResponseHeadersRead,
            cancellationToken);
        response.EnsureSuccessStatusCode();

        await using var stream = await response.Content.ReadAsStreamAsync(
            cancellationToken);
        var release = await JsonSerializer.DeserializeAsync<GitHubRelease>(
            stream,
            cancellationToken: cancellationToken) ??
            throw new InvalidDataException(
                "GitHub returned an empty release response.");

        if (!TryParseVersion(release.TagName, out var latestVersion))
        {
            throw new InvalidDataException(
                $"The release tag '{release.TagName}' is not a valid version.");
        }

        if (!Uri.TryCreate(
                release.HtmlUrl,
                UriKind.Absolute,
                out var releasePage) ||
            releasePage.Scheme != Uri.UriSchemeHttps ||
            !string.Equals(
                releasePage.Host,
                "github.com",
                StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException(
                "GitHub returned an invalid release URL.");
        }

        var currentVersion =
            Assembly.GetExecutingAssembly().GetName().Version ??
            new Version(0, 0, 0, 0);

        return new UpdateCheckResult(
            currentVersion,
            latestVersion,
            release.TagName,
            releasePage,
            latestVersion > currentVersion);
    }

    private static bool TryParseVersion(string tag, out Version version)
    {
        var normalized = tag.Trim();

        if (normalized.StartsWith('v') || normalized.StartsWith('V'))
            normalized = normalized[1..];

        var suffixIndex = normalized.IndexOfAny(['-', '+']);
        if (suffixIndex >= 0)
            normalized = normalized[..suffixIndex];

        return Version.TryParse(normalized, out version!);
    }

    private static HttpClient CreateClient()
    {
        var client = new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(10)
        };
        client.DefaultRequestHeaders.UserAgent.Add(
            new ProductInfoHeaderValue("RtlTerminal", "1.0"));
        client.DefaultRequestHeaders.Accept.Add(
            new MediaTypeWithQualityHeaderValue(
                "application/vnd.github+json"));
        client.DefaultRequestHeaders.Add(
            "X-GitHub-Api-Version",
            "2026-03-10");
        return client;
    }

    private sealed record GitHubRelease(
        [property: JsonPropertyName("tag_name")] string TagName,
        [property: JsonPropertyName("html_url")] string HtmlUrl);
}
