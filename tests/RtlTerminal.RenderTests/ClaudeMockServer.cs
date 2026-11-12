using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;

internal sealed class ClaudeMockServer : IDisposable
{
    internal static string DefaultText => """
سلام! این یک پاسخ آزمایشی برای بررسی Persian و English در RTL Terminal است.

فایل src/app.cs را باز کنید و تابع LoadSettings() را بررسی کنید؛ نتیجه باید status = OK باشد.
English first: please check مسیر C:\Projects\نمونه\app.json and then مقدار timeout را روی 30 بگذارید.
این جمله با فارسی شروع می‌شود، includes English in the middle، و دوباره با فارسی تمام می‌شود.
نسخه v1.1.7 روی .NET 10 اجرا می‌شود و پاسخ HTTP 200 در ساعت 14:30 دریافت شد.

```csharp
var message = "سلام دنیا - Hello world";
Console.WriteLine("نتیجه: Success");
```

برای تست دستور dotnet build را اجرا کنید؛ سپس اگر Error دیدید، فایل logs/output.txt را بخوانید.
Long mixed line: متن فارسی همراه English words و مسیر src/services/SettingsService.cs برای بررسی wrapping و حفظ ترتیب جمله در پنجره کوچک‌تر تکرار می‌شود؛ next step بررسی version 2.1.50 و مقدار count = 42 است.

LOCAL_UI_TEST_OK
""";
    private readonly TcpListener listener = new(IPAddress.Loopback, 0);
    private readonly CancellationTokenSource stop = new();
    private readonly Task accept;
    private readonly string? responseFile;
    public int Requests;
    public string? LastUserText;
    public string Address { get; }
    public ClaudeMockServer(string? responseFile = null)
    {
        this.responseFile = responseFile;
        listener.Start(); Address = "http://127.0.0.1:" + ((IPEndPoint)listener.LocalEndpoint).Port;
        accept = Task.Run(async () =>
        {
            try { while (!stop.IsCancellationRequested) { var client = await listener.AcceptTcpClientAsync(stop.Token); _ = Handle(client); } }
            catch (OperationCanceledException) { } catch (SocketException) when (stop.IsCancellationRequested) { }
        });
    }
    private async Task Handle(TcpClient client)
    {
        using (client)
        try
        {
            using var deadline = CancellationTokenSource.CreateLinkedTokenSource(stop.Token); deadline.CancelAfter(TimeSpan.FromSeconds(20));
            var ct = deadline.Token; var stream = client.GetStream();
            var header = new List<byte>(); var one = new byte[1];
            while (header.Count < 32768 && await stream.ReadAsync(one, ct) > 0)
            {
                header.Add(one[0]);
                if (header.Count >= 4 && header.TakeLast(4).SequenceEqual(new byte[] { 13, 10, 13, 10 })) break;
            }
            var lines = Encoding.ASCII.GetString(header.ToArray()).Split("\r\n");
            var path = lines[0].Split(' ')[1];
            var size = lines.Where(l => l.StartsWith("Content-Length:", StringComparison.OrdinalIgnoreCase)).Select(l => int.Parse(l.Split(':')[1])).FirstOrDefault();
            if (size > 2_000_000) return;
            var body = new byte[size]; await stream.ReadExactlyAsync(body, ct);
            var content = DefaultText;
            if (responseFile != null && System.IO.File.Exists(responseFile) && new System.IO.FileInfo(responseFile).Length <= 65536)
                content = await System.IO.File.ReadAllTextAsync(responseFile, ct);
            object usage = new { input_tokens = 20, output_tokens = 100 };
            var message = new { id = "msg_local_test", type = "message", role = "assistant", model = "claude-sonnet-4-6", content = new[] { new { type = "text", text = content } }, stop_reason = "end_turn", stop_sequence = (string?)null, usage };
            if (path.Contains("count_tokens")) { await Json(new { input_tokens = 20 }); return; }
            if (!path.StartsWith("/v1/messages")) { await Json(new { data = Array.Empty<object>() }); return; }
            Interlocked.Increment(ref Requests);
            using var parsed = JsonDocument.Parse(body);
            if (parsed.RootElement.TryGetProperty("messages", out var messages))
                foreach (var item in messages.EnumerateArray())
                    if (item.TryGetProperty("role", out var role) && role.GetString() == "user" && item.TryGetProperty("content", out var input))
                        LastUserText = input.ValueKind == JsonValueKind.String ? input.GetString()
                            : string.Concat(input.EnumerateArray().Where(block => block.TryGetProperty("type", out var type) && type.GetString() == "text").Select(block => block.GetProperty("text").GetString()));
            if (!parsed.RootElement.TryGetProperty("stream", out var streaming) || streaming.ValueKind != JsonValueKind.True) { await Json(message); return; }
            await Write("HTTP/1.1 200 OK\r\nContent-Type: text/event-stream\r\nConnection: close\r\n\r\n");
            await Event("message_start", new { type = "message_start", message = new { id = "msg_local_test", type = "message", role = "assistant", model = "claude-sonnet-4-6", content = Array.Empty<object>(), stop_reason = (string?)null, stop_sequence = (string?)null, usage } });
            await Event("content_block_start", new { type = "content_block_start", index = 0, content_block = new { type = "text", text = "" } });
            foreach (var chunk in content.EnumerateRunes().Chunk(12))
            {
                await Event("content_block_delta", new { type = "content_block_delta", index = 0, delta = new { type = "text_delta", text = string.Concat(chunk.Select(r => r.ToString())) } });
                await Task.Delay(25, ct);
            }
            await Event("content_block_stop", new { type = "content_block_stop", index = 0 });
            await Event("message_delta", new { type = "message_delta", delta = new { stop_reason = "end_turn", stop_sequence = (string?)null }, usage = new { output_tokens = 100 } });
            await Event("message_stop", new { type = "message_stop" });
            Task Write(string text) => stream.WriteAsync(Encoding.UTF8.GetBytes(text), ct).AsTask();
            Task Event(string name, object value) => Write("event: " + name + "\ndata: " + JsonSerializer.Serialize(value) + "\n\n");
            async Task Json(object value)
            {
                var data = JsonSerializer.SerializeToUtf8Bytes(value);
                await Write("HTTP/1.1 200 OK\r\nContent-Type: application/json\r\nContent-Length: " + data.Length + "\r\nConnection: close\r\n\r\n");
                await stream.WriteAsync(data, ct);
            }
        }
        catch (Exception e) when (e is System.IO.IOException or SocketException or OperationCanceledException) { }
    }
    public void Dispose() { stop.Cancel(); listener.Stop(); accept.GetAwaiter().GetResult(); stop.Dispose(); }
}
