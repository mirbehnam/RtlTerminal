using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;

namespace RtlTerminal.CliLab;

// This fixture server has no outbound client and never forwards requests.
internal sealed class LocalCliServer : IDisposable
{
    internal const string DefaultResponse = """
سلام این یک پاسخ آزمایشی محلی در RTL Terminal است

English and فارسی together — check src/app.cs and مقدار timeout = 30
این جمله با فارسی شروع می‌شود و includes English in the middle و دوباره فارسی

```csharp
var message = "سلام دنیا — Hello world";
Console.WriteLine(message);
```

مسیر C:\Projects\نمونه\app.json و نتیجه HTTP 200 را بررسی کنید
LOCAL_UI_TEST_OK
""";
    private readonly TcpListener listener = new(IPAddress.Loopback, 0);
    private readonly CancellationTokenSource stop = new();
    private readonly Task accept;
    private readonly string response;
    private readonly SemaphoreSlim capacity = new(8);
    private int disposed;
    private int requests;
    public string Address { get; }
    public int Requests => Volatile.Read(ref requests);
    internal string? LastUserText { get; private set; }
    public LocalCliServer(string response)
    {
        if (string.IsNullOrWhiteSpace(response) || response.Length > 65536) throw new ArgumentException("Enter a sample response of 1–65536 characters.");
        this.response = response;
        listener.Start();
        Address = "http://127.0.0.1:" + ((IPEndPoint)listener.LocalEndpoint).Port;
        accept = AcceptAsync();
    }
    private async Task AcceptAsync()
    {
        try
        {
            while (!stop.IsCancellationRequested)
            {
                var client = await listener.AcceptTcpClientAsync(stop.Token);
                if (!capacity.Wait(0)) { client.Dispose(); continue; }
                _ = HandleAsync(client);
            }
        }
        catch (OperationCanceledException) { }
        catch (SocketException) when (stop.IsCancellationRequested) { }
    }
    private async Task HandleAsync(TcpClient client)
    {
        using (client)
        using (var deadline = CancellationTokenSource.CreateLinkedTokenSource(stop.Token))
        try
        {
            deadline.CancelAfter(TimeSpan.FromSeconds(30)); var ct = deadline.Token;
            var stream = client.GetStream(); var header = new List<byte>(); var one = new byte[1];
            while (header.Count < 32768 && await stream.ReadAsync(one, ct) != 0)
            {
                header.Add(one[0]);
                if (header.Count >= 4 && header.TakeLast(4).SequenceEqual(new byte[] { 13, 10, 13, 10 })) break;
            }
            if (header.Count >= 32768 || header.Count < 4) return;
            var lines = Encoding.ASCII.GetString(header.ToArray()).Split("\r\n");
            var first = lines[0].Split(' '); if (first.Length != 3 || !first[1].StartsWith('/')) return;
            var path = first[1].Split('?')[0];
            if (lines.Any(l => l.StartsWith("Transfer-Encoding:", StringComparison.OrdinalIgnoreCase))) { await Json(new { error = "Chunked request bodies are not supported by this fixture." }, 400); return; }
            var size = 0;
            var length = lines.FirstOrDefault(l => l.StartsWith("Content-Length:", StringComparison.OrdinalIgnoreCase));
            if (length != null && (!int.TryParse(length[(length.IndexOf(':') + 1)..], out size) || size < 0 || size > 2_000_000)) { await Json(new { error = "Request too large" }, 413); return; }
            var body = new byte[size]; await stream.ReadExactlyAsync(body, ct);
            if (path is "/v1/models" or "/models")
            { await Json(new { @object = "list", data = new[] { new { id = "rtl-local", @object = "model", owned_by = "local-fixture" } } }); return; }
            if (path == "/v1/messages/count_tokens") { await Json(new { input_tokens = 20 }); return; }
            if (path is not ("/v1/messages" or "/v1/chat/completions" or "/chat/completions"))
            { await Json(new { error = "Unsupported local test endpoint. Nothing was forwarded." }, 404); return; }
            using var json = JsonDocument.Parse(body);
            Interlocked.Increment(ref requests);
            if (json.RootElement.TryGetProperty("messages", out var messages))
                foreach (var item in messages.EnumerateArray())
                    if (item.TryGetProperty("role", out var role) && role.GetString() == "user" && item.TryGetProperty("content", out var content))
                        LastUserText = content.ValueKind == JsonValueKind.String ? content.GetString() : string.Concat(content.EnumerateArray().Where(b => b.TryGetProperty("text", out _)).Select(b => b.GetProperty("text").GetString()));
            var streaming = json.RootElement.TryGetProperty("stream", out var s) && s.ValueKind == JsonValueKind.True;
            var anthropic = path == "/v1/messages";
            if (!streaming)
            {
                if (anthropic) await Json(new { id = "msg_local", type = "message", role = "assistant", model = "rtl-local", content = new[] { new { type = "text", text = response } }, stop_reason = "end_turn", usage = new { input_tokens = 20, output_tokens = 100 } });
                else await Json(new { id = "chatcmpl_local", @object = "chat.completion", created = 1, model = "rtl-local", choices = new[] { new { index = 0, message = new { role = "assistant", content = response }, finish_reason = "stop" } }, usage = new { prompt_tokens = 20, completion_tokens = 100, total_tokens = 120 } });
                return;
            }
            await Write("HTTP/1.1 200 OK\r\nContent-Type: text/event-stream\r\nCache-Control: no-cache\r\nConnection: close\r\n\r\n");
            if (anthropic)
            {
                await Event("message_start", new { type = "message_start", message = new { id = "msg_local", type = "message", role = "assistant", model = "rtl-local", content = Array.Empty<object>(), stop_reason = (string?)null, usage = new { input_tokens = 20, output_tokens = 0 } } });
                await Event("content_block_start", new { type = "content_block_start", index = 0, content_block = new { type = "text", text = "" } });
            }
            foreach (var chunk in response.EnumerateRunes().Chunk(20))
            {
                var text = string.Concat(chunk.Select(r => r.ToString()));
                if (anthropic) await Event("content_block_delta", new { type = "content_block_delta", index = 0, delta = new { type = "text_delta", text } });
                else await Chat(new { content = text }, (string?)null);
                await Task.Delay(15, ct);
            }
            if (anthropic)
            {
                await Event("content_block_stop", new { type = "content_block_stop", index = 0 });
                await Event("message_delta", new { type = "message_delta", delta = new { stop_reason = "end_turn", stop_sequence = (string?)null }, usage = new { output_tokens = 100 } });
                await Event("message_stop", new { type = "message_stop" });
            }
            else { await Chat(new { }, "stop"); await Write("data: [DONE]\n\n"); }
            Task Chat(object delta, string? finish) => Write("data: " + JsonSerializer.Serialize(new { id = "chatcmpl_local", @object = "chat.completion.chunk", created = 1, model = "rtl-local", choices = new[] { new { index = 0, delta, finish_reason = finish } } }) + "\n\n");
            Task Event(string name, object value) => Write("event: " + name + "\ndata: " + JsonSerializer.Serialize(value) + "\n\n");
            Task Write(string text) => stream.WriteAsync(Encoding.UTF8.GetBytes(text), ct).AsTask();
            async Task Json(object value, int code = 200)
            {
                var data = JsonSerializer.SerializeToUtf8Bytes(value);
                await Write($"HTTP/1.1 {code} {(code == 200 ? "OK" : "Error")}\r\nContent-Type: application/json\r\nContent-Length: {data.Length}\r\nConnection: close\r\n\r\n");
                await stream.WriteAsync(data, ct);
            }
        }
        catch (Exception e) when (e is IOException or SocketException or OperationCanceledException or JsonException or InvalidOperationException or FormatException) { }
        finally { capacity.Release(); }
    }
    public void Dispose()
    {
        if (Interlocked.Exchange(ref disposed, 1) != 0) return;
        stop.Cancel(); listener.Stop();
        // Existing bounded handlers observe cancellation; never wait on the WPF thread.
    }
}
