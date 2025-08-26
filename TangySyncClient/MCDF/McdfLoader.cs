using System;
using System.IO;
using System.IO.Compression;
using System.Text;
using System.Text.Json;
using K4os.Compression.LZ4.Legacy;

namespace TangySyncClient.Mcdf;

internal static class McdfLoader
{
    public static McdfPayload? Load(string path)
    {
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
            return null;

        var ext = Path.GetExtension(path).ToLowerInvariant();

        if (ext is ".json")
        {
            // Your simple JSON payload
            var json = File.ReadAllText(path);
            return JsonSerializer.Deserialize<McdfPayload>(json);
        }

        if (ext is ".zip" || ext is ".mcdf")
        {
            // Try Mare .mcdf first (LZ4 stream with 'MCDF' header).
            // If it fails, fall back to ZIP payload.json.
            try
            {
                var p = TryReadMareMcdf(path);
                if (p is not null)
                    return p;
            }
            catch
            {
                // ignore and try zip
            }

            // Plain zip with v1/payload.json or payload.json
            try
            {
                using var fs = File.OpenRead(path);
                using var zip = new ZipArchive(fs, ZipArchiveMode.Read);
                var entry = zip.GetEntry("v1/payload.json") ?? zip.GetEntry("payload.json");
                if (entry is null) return null;
                using var es = entry.Open();
                using var sr = new StreamReader(es, Encoding.UTF8);
                var payloadJson = sr.ReadToEnd();
                return JsonSerializer.Deserialize<McdfPayload>(payloadJson);
            }
            catch
            {
                return null;
            }
        }

        return null;
    }

    // === Mare .mcdf reader (v1) ===
    private static McdfPayload? TryReadMareMcdf(string path)
    {
        using var fs = File.OpenRead(path);
        using var lz = new LZ4Stream(fs, LZ4StreamMode.Decompress, LZ4StreamFlags.HighCompression);
        using var br = new BinaryReader(lz, Encoding.UTF8, leaveOpen: true);

        // Magic
        var m = br.ReadChar();
        var c = br.ReadChar();
        var d = br.ReadChar();
        var f = br.ReadChar();
        if (m != 'M' || c != 'C' || d != 'D' || f != 'F') return null;

        // Version & header
        byte version = br.ReadByte();
        if (version != 1) return null;

        int headerLen = br.ReadInt32();
        var headerBytes = br.ReadBytes(headerLen);
        var headerJson = Encoding.UTF8.GetString(headerBytes);

        var header = new MareCharaFileHeader
        {
            Version = version,
            CharaFileData = JsonSerializer.Deserialize<MareCharaFileData>(headerJson) ?? new MareCharaFileData(),
            FilePath = path
        };

        // We do not need the following LZ4 payload bytes (actual file contents) for your use case,
        // since you only apply appearance via IPCs (Glamourer, Customize+, etc).

        // Translate to your Tangy payload
        return new McdfPayload
        {
            GlamourerBase64 = string.IsNullOrWhiteSpace(header.CharaFileData.GlamourerData) ? null : header.CharaFileData.GlamourerData,
            CustomizePlusJson = string.IsNullOrWhiteSpace(header.CharaFileData.CustomizePlusData) ? null : header.CharaFileData.CustomizePlusData,
            // Heels/Honorific are not present in Mare header; keep null.
            HeelsJson = null,
            HonorificJson = null,
            PenumbraCollection = null
        };
    }
}
