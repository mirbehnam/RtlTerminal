using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Linq;

namespace RtlTerminal.Proxy;

// Summarizes the managed user file, not environment/project overrides or connectivity.
public static class DeveloperProxySummary
{
    public static async Task<DetectedProxyTool> ReadAsync(DetectedProxyTool tool, CancellationToken ct)
    {
        if (tool.Provider == null) return tool with { ProxyAddresses = [], ProxyReadFailed = false };
        try
        {
            var state = await tool.Provider.ReadAsync(ct);
            return tool with { ProxyAddresses = Parse(tool.Name, state), ProxyReadFailed = false };
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or NotSupportedException or FormatException or XmlException or ArgumentException)
        { return tool with { ProxyAddresses = [], ProxyReadFailed = true }; }
    }

    public static string[] Parse(string tool, ProxyState state)
    {
        if (state["Content"] is not { } content) return [];
        var bytes = Convert.FromBase64String(content);
        var addresses = new List<string>();
        void Add(string? value)
        {
            if (string.IsNullOrWhiteSpace(value)) return;
            value = value.Trim().Trim('"', '\'');
            if (!value.Contains("://")) value = "http://" + value;
            if (!Uri.TryCreate(value, UriKind.Absolute, out var uri) || uri.Scheme is not ("http" or "https" or "socks" or "socks5" or "socks5h") || string.IsNullOrEmpty(uri.Host)) return;
            // Rebuild only scheme/host/port: credentials, paths, queries and fragments
            // never enter the UI, tooltips or accessibility text.
            var host = uri.Host.Trim('[', ']');
            if (host.Contains(':')) host = "[" + host + "]";
            var port = uri.Port > 0 ? ":" + uri.Port : "";
            addresses.Add(uri.Scheme + "://" + host + port);
        }
        if (tool is "Maven" or "NuGet / .NET")
        {
            using var input = new MemoryStream(bytes);
            using var reader = XmlReader.Create(input, new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null, MaxCharactersInDocument = 4_000_000 });
            var doc = XDocument.Load(reader);
            var root = doc.Root ?? throw new XmlException("Missing root."); var ns = root.Name.Namespace;
            if (tool == "Maven")
            {
                foreach (var p in root.Element(ns + "proxies")?.Elements(ns + "proxy") ?? [])
                    if (!string.Equals((string?)p.Element(ns + "active"), "false", StringComparison.OrdinalIgnoreCase))
                        Add(((string?)p.Element(ns + "protocol") ?? "http") + "://" + (string?)p.Element(ns + "host") + ":" + (string?)p.Element(ns + "port"));
            }
            else foreach (var p in root.Element(ns + "config")?.Elements(ns + "add") ?? [])
                if (string.Equals((string?)p.Attribute("key"), "http_proxy", StringComparison.OrdinalIgnoreCase)) Add((string?)p.Attribute("value"));
        }
        else
        {
            var text = new UTF8Encoding(false, true).GetString(bytes).TrimStart('\uFEFF');
            var section = ""; var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var line in text.Split('\n'))
            {
                var trimmed = line.Trim();
                if (trimmed.StartsWith('#') || trimmed.StartsWith(';')) continue;
                if (trimmed.StartsWith('[')) { section = trimmed.Trim('[', ']').Trim(); continue; }
                var match = Regex.Match(trimmed, @"^([^\s=:]+)\s*[=:]\s*(.*)$");
                if (!match.Success) continue;
                var key = match.Groups[1].Value; var value = match.Groups[2].Value.Trim();
                if (tool == "Gradle") values[key] = value;
                else if ((tool == "Git" && section.Equals("http", StringComparison.OrdinalIgnoreCase) && key.Equals("proxy", StringComparison.OrdinalIgnoreCase)) ||
                    (tool is "npm" or "pnpm" && section == "" && key is "proxy" or "https-proxy") ||
                    (tool == "Python pip" && section.Equals("global", StringComparison.OrdinalIgnoreCase) && key.Equals("proxy", StringComparison.OrdinalIgnoreCase)) ||
                    (tool == "Cargo" && section == "http" && key == "proxy")) values[key] = value;
            }
            if (tool == "Gradle")
            {
                foreach (var scheme in new[] { "http", "https" })
                    if (values.TryGetValue("systemProp." + scheme + ".proxyHost", out var host))
                        Add("http://" + host + (values.TryGetValue("systemProp." + scheme + ".proxyPort", out var port) ? ":" + port : ":80"));
            }
            else foreach (var value in values.Values) Add(value);
        }
        return addresses.Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
    }
}
