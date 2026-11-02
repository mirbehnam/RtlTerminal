using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;

namespace RtlTerminal;

/// <summary>Seekable, bounded ZIP input. Never falls back to a full download.</summary>
public sealed class HttpRangeStream : Stream
{
    private const int BlockSize = 512 * 1024;
    private readonly HttpClient client;
    private readonly Uri address;
    private readonly CancellationToken cancellation;
    private readonly string? etag;
    private readonly Dictionary<long, byte[]> blocks = [];
    private long position, transferred;
    private bool disposed;
    private HttpRangeStream(HttpClient client, Uri address, long length, string? etag, CancellationToken cancellation)
    { this.client = client; this.address = address; Length = length; this.etag = etag; this.cancellation = cancellation; }
    public static async Task<HttpRangeStream> OpenAsync(HttpClient client, string url, CancellationToken ct)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Range = new RangeHeaderValue(0, 0); request.Headers.AcceptEncoding.ParseAdd("identity");
        using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct);
        response.EnsureSuccessStatusCode();
        var range = response.Content.Headers.ContentRange;
        if (response.StatusCode != HttpStatusCode.PartialContent || range is not { From: 0, To: 0, Length: > 0 and <= 220_000_000 } || response.Content.Headers.ContentEncoding.Any(e => e != "identity"))
            throw new IOException("This server or proxy does not support partial font downloads. No full archive was downloaded.");
        await using var input = await response.Content.ReadAsStreamAsync(ct);
        var probe = new byte[2];
        var count = await input.ReadAsync(probe, ct);
        if (count != 1 || await input.ReadAsync(probe, ct) != 0) throw new InvalidDataException("Invalid range response length.");
        // Reuse GitHub's redirected asset URL to avoid a redirect for each block.
        return new(client, response.RequestMessage?.RequestUri ?? new Uri(url), range.Length.Value, response.Headers.ETag is { IsWeak: false } tag ? tag.ToString() : null, ct);
    }
    private async Task<byte[]> BlockAsync(long start)
    {
        cancellation.ThrowIfCancellationRequested();
        if (blocks.TryGetValue(start, out var saved)) return saved;
        var end = Math.Min(Length - 1, start + BlockSize - 1);
        using var request = new HttpRequestMessage(HttpMethod.Get, address);
        request.Headers.Range = new RangeHeaderValue(start, end); request.Headers.AcceptEncoding.ParseAdd("identity");
        if (etag != null) request.Headers.TryAddWithoutValidation("If-Match", etag);
        using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellation);
        response.EnsureSuccessStatusCode();
        var range = response.Content.Headers.ContentRange;
        if (response.StatusCode != HttpStatusCode.PartialContent || range?.From != start || range.To != end || range.Length != Length ||
            response.Content.Headers.ContentEncoding.Any(e => e != "identity") || etag != null && response.Headers.ETag?.ToString() != etag)
            throw new IOException("The font download changed or the server returned an invalid range. Please retry.");
        transferred += end - start + 1;
        if (transferred > 100_000_000) throw new IOException("Partial font download exceeded its safety limit.");
        var bytes = new byte[checked((int)(end - start + 1))];
        await using var input = await response.Content.ReadAsStreamAsync(cancellation);
        await input.ReadExactlyAsync(bytes, cancellation);
        if (await input.ReadAsync(new byte[1], cancellation) != 0) throw new InvalidDataException("Invalid range response length.");
        if (blocks.Count >= 4) blocks.Remove(blocks.Keys.First());
        blocks[start] = bytes; return bytes;
    }
    public override int Read(byte[] buffer, int offset, int count) => ReadAsync(buffer.AsMemory(offset, count), cancellation).AsTask().GetAwaiter().GetResult();
    public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken ct = default)
    {
        ObjectDisposedException.ThrowIf(disposed, this); cancellation.ThrowIfCancellationRequested(); ct.ThrowIfCancellationRequested();
        if (position >= Length || buffer.Length == 0) return 0;
        var start = position / BlockSize * BlockSize; var bytes = await BlockAsync(start);
        var within = (int)(position - start); var count = Math.Min(buffer.Length, bytes.Length - within);
        bytes.AsMemory(within, count).CopyTo(buffer); position += count; return count;
    }
    public override long Seek(long offset, SeekOrigin origin)
    {
        var next = checked(offset + (origin == SeekOrigin.Begin ? 0 : origin == SeekOrigin.Current ? position : origin == SeekOrigin.End ? Length : throw new ArgumentException("Invalid seek origin.")));
        if (next < 0 || next > Length) throw new IOException("ZIP seek is outside the asset.");
        return position = next;
    }
    public override long Length { get; }
    public long BytesTransferred => transferred + 1;
    public override long Position { get => position; set => Seek(value, SeekOrigin.Begin); }
    public override bool CanRead => !disposed;
    public override bool CanSeek => !disposed;
    public override bool CanWrite => false;
    public override void Flush() { }
    public override void SetLength(long value) => throw new NotSupportedException();
    public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    protected override void Dispose(bool disposing) { disposed = true; blocks.Clear(); base.Dispose(disposing); }
}
