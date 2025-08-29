using System;
using System.Buffers.Binary;
using System.IO;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace TangySync.Services;

public static class TcpTransport
{
    // Header: int32 jsonLen, json bytes (UTF8) { name,size,sha256,chunk }
    // Body: repeated [ int32 chunkLen, chunkData ]

    public static async Task SendAsync(NetworkStream net, string path, string sha256, long bytesPerSec, CancellationToken ct)
    {
        var fi = new FileInfo(path);

        //header 
        var header = $"{{\"name\":\"{Escape(fi.Name)}\",\"size\":{fi.Length},\"sha256\":\"{sha256}\",\"chunk\":1048576}}";
        var headerBytes = Encoding.UTF8.GetBytes(header);

        var lenBuf = new byte[4];
        BinaryPrimitives.WriteInt32LittleEndian(lenBuf, headerBytes.Length);
        await net.WriteAsync(lenBuf.AsMemory(0, 4), ct);
        await net.WriteAsync(headerBytes.AsMemory(0, headerBytes.Length), ct);

        //body
        var limiter = new RateLimiter(bytesPerSec);

        //enumerate chunks with cancellation in a standards-compliant way
        var chunker = new Chunker(path);
        await foreach (var item in chunker.WithCancellation(ct))
        {
            var len = item.len;
            var data = item.data;

            BinaryPrimitives.WriteInt32LittleEndian(lenBuf, len);
            await limiter.WaitAsync(len + 4, ct);
            await net.WriteAsync(lenBuf.AsMemory(0, 4), ct);
            await net.WriteAsync(data.AsMemory(0, len), ct);
        }

        //tail: zero length (EOF)
        BinaryPrimitives.WriteInt32LittleEndian(lenBuf, 0);
        await net.WriteAsync(lenBuf.AsMemory(0, 4), ct);
    }

    public static async Task<bool> ReceiveAsync(NetworkStream net, string savePath, long expectedSize, string expectedSha, CancellationToken ct)
    {
        var lenBuf = new byte[4];

        //header
        await net.ReadExactlyAsync(lenBuf.AsMemory(0, 4), ct);
        var hLen = BinaryPrimitives.ReadInt32LittleEndian(lenBuf);
        var hBytes = new byte[hLen];
        await net.ReadExactlyAsync(hBytes.AsMemory(0, hLen), ct);

        var hdr = System.Text.Json.JsonDocument.Parse(hBytes).RootElement;
        var size = hdr.GetProperty("size").GetInt64();
        var sha = hdr.GetProperty("sha256").GetString() ?? "";

        if (expectedSize > 0 && size != expectedSize) return false;
        if (!string.IsNullOrEmpty(expectedSha)) expectedSha = expectedSha.ToUpperInvariant();

        Directory.CreateDirectory(Path.GetDirectoryName(savePath)!);
        await using var fs = File.Create(savePath);
        using var sha256 = SHA256.Create();

        long total = 0;

        //body
        while (true)
        {
            await net.ReadExactlyAsync(lenBuf.AsMemory(0, 4), ct);
            var len = BinaryPrimitives.ReadInt32LittleEndian(lenBuf);
            if (len == 0) break;

            var buf = new byte[len];
            await net.ReadExactlyAsync(buf.AsMemory(0, len), ct);

            await fs.WriteAsync(buf.AsMemory(0, len), ct);
            sha256.TransformBlock(buf, 0, len, null, 0);
            total += len;
        }

        sha256.TransformFinalBlock(Array.Empty<byte>(), 0, 0);
        var got = Convert.ToHexString(sha256.Hash!);

        return total == size && (string.IsNullOrEmpty(expectedSha) || got == expectedSha.ToUpperInvariant());
    }

    private static string Escape(string s) => s.Replace("\\", "\\\\").Replace("\"", "\\\"");
}
