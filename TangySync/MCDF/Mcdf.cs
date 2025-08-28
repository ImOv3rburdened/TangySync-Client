using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.Json;

namespace TangySync.MCDF;

public sealed class McdfData
{
    public string Description { get; set; } = string.Empty;
    public string GlamourerData { get; set; } = string.Empty;
    public string CustomizePlusData { get; set; } = string.Empty;
    public string ManipulationData { get; set; } = string.Empty;
    public List<FileData> Files { get; set; } = new();
    public List<FileSwap> FileSwaps { get; set; } = new();

    public record FileSwap(IEnumerable<string> GamePaths, string FileSwapPath);
    public record FileData(IEnumerable<string> GamePaths, int Length, string Hash);

    public byte[] ToBytes() => Encoding.UTF8.GetBytes(JsonSerializer.Serialize(this));
    public static McdfData FromBytes(byte[] data) => JsonSerializer.Deserialize<McdfData>(Encoding.UTF8.GetString(data))!;
}

public static class McdfCodec
{
    private const byte CurrentVersion = 1;

    public static void Write(string path, McdfData data)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        using var fs = File.Create(path);
        using var bw = new BinaryWriter(fs, Encoding.UTF8, leaveOpen: false);
        bw.Write('M'); bw.Write('C'); bw.Write('D'); bw.Write('F');
        bw.Write(CurrentVersion);
        var payload = data.ToBytes();
        bw.Write(payload.Length);
        bw.Write(payload);
    }

    public static McdfData Read(string path)
    {
        using var fs = File.OpenRead(path);
        using var br = new BinaryReader(fs, Encoding.UTF8, leaveOpen: false);
        var magic = new string(br.ReadChars(4));
        if (magic != "MCDF") throw new InvalidDataException("Not a MCDF file.");
        var ver = br.ReadByte();
        if (ver != 1) throw new InvalidDataException($"Unsupported MCDF version {ver}.");
        var len = br.ReadInt32();
        var payload = br.ReadBytes(len);
        return McdfData.FromBytes(payload);
    }
}
