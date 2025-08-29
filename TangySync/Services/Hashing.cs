using System;
using System.Buffers;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;

namespace TangySync.Services;

public static class Hasher
{
    public static async Task<string> Sha256Async(string path, CancellationToken ct = default)
    {
        await using var fs = File.OpenRead(path);
        using var sha = SHA256.Create();
        var buf = ArrayPool<byte>.Shared.Rent(1 << 20); // 1 MiB
        try
        {
            int r;
            while ((r = await fs.ReadAsync(buf.AsMemory(0, buf.Length), ct)) > 0)
                sha.TransformBlock(buf, 0, r, null, 0);
            sha.TransformFinalBlock(Array.Empty<byte>(), 0, 0);
            return Convert.ToHexString(sha.Hash!);
        }
        finally { ArrayPool<byte>.Shared.Return(buf); }
    }
}

public sealed class Chunker : IAsyncEnumerable<(long index, int len, byte[] data)>
{
    private readonly string _path;
    private readonly int _chunk;
    public Chunker(string path, int chunkSizeBytes = 1 << 20) { _path = path; _chunk = chunkSizeBytes; }

    public async IAsyncEnumerator<(long index, int len, byte[] data)> GetAsyncEnumerator(CancellationToken ct = default)
    {
        long idx = 0;
        await using var fs = File.OpenRead(_path);
        var buf = new byte[_chunk];
        int r;
        while ((r = await fs.ReadAsync(buf.AsMemory(0, _chunk), ct)) > 0)
        {
            var outBuf = new byte[r];
            Buffer.BlockCopy(buf, 0, outBuf, 0, r);
            yield return (idx++, r, outBuf);
        }
    }
}

public sealed class RateLimiter
{
    private readonly long _bytesPerSecond;
    private long _tokens;
    private long _lastTicks;

    public RateLimiter(long bytesPerSecond)
    {
        _bytesPerSecond = Math.Max(32_000, bytesPerSecond);
        _tokens = _bytesPerSecond;
        _lastTicks = Environment.TickCount64;
    }

    public async Task WaitAsync(int bytes, CancellationToken ct)
    {
        while (true)
        {
            var now = Environment.TickCount64;
            var delta = now - _lastTicks;
            _lastTicks = now;
            _tokens = Math.Min(_bytesPerSecond, _tokens + (_bytesPerSecond * delta) / 1000);

            if (_tokens >= bytes) { _tokens -= bytes; return; }
            var needMs = (int)(1000 * (bytes - _tokens) / Math.Max(1, _bytesPerSecond));
            await Task.Delay(Math.Clamp(needMs, 5, 250), ct);
            ct.ThrowIfCancellationRequested();
        }
    }
}
