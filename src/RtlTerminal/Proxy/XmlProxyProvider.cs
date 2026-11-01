using System.IO;
using System.Text;
using System.Xml;
using System.Xml.Linq;

namespace RtlTerminal.Proxy;

public sealed class XmlProxyProvider(string name, string path, bool maven) : IProxyProvider
{
    public string Name => name;
    public ProxyDescriptor Descriptor => new(Name, "schema-1", Path.GetFullPath(path), "User");
    private readonly ProxyFileProvider file = new(name, path, "", [], false);
    public void Validate(ProxyEndpoint e) => file.Validate(e);
    public Task<ProxyState> ReadAsync(CancellationToken ct) => file.ReadAsync(ct);
    public Task WriteAsync(ProxyState state, CancellationToken ct) => file.WriteAsync(state, ct);
    public ProxyState Desired(ProxyEndpoint e, ProxyState previous, bool clear)
    {
        var content = previous["Content"];
        if (content == null && clear) return previous;
        var bytes = content == null ? Encoding.UTF8.GetBytes(maven ? "<settings />" : "<configuration />") : Convert.FromBase64String(content);
        using var input = new MemoryStream(bytes);
        using var reader = XmlReader.Create(input, new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null, MaxCharactersInDocument = 4_000_000 });
        var doc = XDocument.Load(reader, LoadOptions.PreserveWhitespace);
        var root = doc.Root ?? throw new IOException("Missing XML configuration root.");
        if (root.Name.LocalName != (maven ? "settings" : "configuration")) throw new IOException("Unexpected XML configuration root.");
        var ns = root.Name.Namespace;
        var container = root.Element(ns + (maven ? "proxies" : "config"));
        if (container == null && !clear) { container = new XElement(ns + (maven ? "proxies" : "config")); root.Add(container); }
        if (maven)
        {
            foreach (var proxy in container?.Elements(ns + "proxy").Where(p => (string?)p.Element(ns + "id") == "rtl-terminal").ToArray() ?? []) proxy.Remove();
            if (!clear)
            {
                if (container!.Elements(ns + "proxy").Any(p => !string.Equals((string?)p.Element(ns + "active"), "false", StringComparison.OrdinalIgnoreCase)))
                    throw new NotSupportedException("Maven has another active proxy. Resolve it manually; this manager will not disable unrelated proxy profiles.");
                container.Add(new XElement(ns + "proxy", new XElement(ns + "id", "rtl-terminal"), new XElement(ns + "active", "true"),
                    new XElement(ns + "protocol", "http"), new XElement(ns + "host", e.Host), new XElement(ns + "port", e.Port), new XElement(ns + "nonProxyHosts", e.LocalBypass("|"))));
            }
        }
        else
        {
            foreach (var entry in container?.Elements(ns + "add").Where(p => (string?)p.Attribute("key") is "http_proxy" or "no_proxy").ToArray() ?? []) entry.Remove();
            if (!clear)
            {
                container!.Add(new XElement(ns + "add", new XAttribute("key", "http_proxy"), new XAttribute("value", e.Url)));
                container.Add(new XElement(ns + "add", new XAttribute("key", "no_proxy"), new XAttribute("value", e.LocalBypass(","))));
            }
        }
        using var output = new MemoryStream();
        using (var writer = XmlWriter.Create(output, new XmlWriterSettings { Encoding = new UTF8Encoding(false), Indent = false, OmitXmlDeclaration = doc.Declaration == null })) doc.Save(writer);
        return new(new() { ["Content"] = Convert.ToBase64String(output.ToArray()) });
    }
}
