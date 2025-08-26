using System.Collections.Generic;

namespace TangySyncClient.Mcdf;

// Tangy payload you apply via IPC.
public sealed class McdfPayload
{
    public string? GlamourerBase64 { get; set; }
    public string? CustomizePlusJson { get; set; }
    public string? HeelsJson { get; set; }
    public string? HonorificJson { get; set; }
    public string? PenumbraCollection { get; set; }
}

// Shape of Mare's header "CharaFileData" blob.
public sealed class MareCharaFileData
{
    public string Description { get; set; } = string.Empty;
    public string GlamourerData { get; set; } = string.Empty; // base64 string
    public string CustomizePlusData { get; set; } = string.Empty; // json
    public string ManipulationData { get; set; } = string.Empty; // not used here
    public List<FileData> Files { get; set; } = new();
    public List<FileSwap> FileSwaps { get; set; } = new();

    public sealed record FileSwap(IEnumerable<string> GamePaths, string FileSwapPath);
    public sealed record FileData(IEnumerable<string> GamePaths, int Length, string Hash);
}

// Full header container in .mcdf:
//  'M' 'C' 'D' 'F'  + byte Version (currently 1)
//  int headerLength + JSON(MareCharaFileData)
//  [ then an LZ4 stream of file bytes matching Files[] ]
public sealed class MareCharaFileHeader
{
    public byte Version { get; set; } = 1;
    public MareCharaFileData CharaFileData { get; set; } = new();
    public string FilePath { get; set; } = string.Empty;
}
