using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Dalamud.Plugin.Services;

namespace TangySync.Services;

/// <summary>
/// Heartbeat + simple TCP transport using /api/signal/* to exchange (ip,port,meta).
/// For LAN/port-forward testing (no STUN/ICE).
/// </summary>
public sealed class SyncClient : IDisposable
{
    private readonly ApiClient _api;
    private readonly ICondition _cond;
    private readonly Timer _hb;
    private volatile bool _hbSending;

    public event Action<string>? OnStatus;

    public SyncClient(ApiClient api, ICondition cond)
    {
        _api = api; _cond = cond;
        _hb = new Timer(async _ => await Heartbeat(), null, TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(20));
    }

    private bool InGpose()
    {
        try
        {
            var t = typeof(Dalamud.Game.ClientState.Conditions.ConditionFlag);
            object v;
            if (Enum.TryParse(t, "InGpose", out v!) || Enum.TryParse(t, "InGPose", out v!) || Enum.TryParse(t, "Gpose", out v!))
                return _cond[(Dalamud.Game.ClientState.Conditions.ConditionFlag)v];
        }
        catch { }
        return false;
    }

    private async Task Heartbeat()
    {
        if (_hbSending) return;
        _hbSending = true;
        try
        {
            var st = await _api.Heartbeat(InGpose());
            OnStatus?.Invoke(st == 200 ? "heartbeat ok" : $"heartbeat http {st}");
        }
        catch (Exception ex) { OnStatus?.Invoke("heartbeat err: " + ex.Message); }
        finally { _hbSending = false; }
    }

    //P2P offer (sender side)
    public async Task<bool> SendFileAsync(string peerVanity, string path, long bytesPerSec, CancellationToken ct, string? ipOverride = null)
    {
        if (!File.Exists(path)) { OnStatus?.Invoke("file not found"); return false; }

        var fi = new FileInfo(path);
        var hash = await Hasher.Sha256Async(path, ct);

        //Start listener on ephemeral port
        var listener = new TcpListener(IPAddress.Any, 0);
        listener.Start();
        var endPoint = (IPEndPoint)listener.LocalEndpoint;
        var port = endPoint.Port;

        //Determine advertised IP
        var hostIp = !string.IsNullOrWhiteSpace(ipOverride) ? IPAddress.Parse(ipOverride) : GetLocalIPv4() ?? IPAddress.Loopback;

        // Create offer payload
        var offer = JsonSerializer.Serialize(new
        {
            type = "tcp-offer",
            ip = hostIp.ToString(),
            port,
            name = fi.Name,
            size = fi.Length,
            sha256 = hash,
            chunk = 1 << 20
        });

        var st = await _api.SignalOffer(peerVanity, offer, ct);
        if (st != 200) { OnStatus?.Invoke($"offer http {st}"); listener.Stop(); return false; }
        OnStatus?.Invoke($"offer sent to @{peerVanity} ({hostIp}:{port})");

        //Accept exactly one receiver and send file
        try
        {
            using var client = await listener.AcceptTcpClientAsync(ct);
            using var net = client.GetStream();

            await TcpTransport.SendAsync(net, path, hash, bytesPerSec, ct);
            OnStatus?.Invoke($"sent {fi.Name} ({fi.Length} bytes)");
            return true;
        }
        catch (Exception ex) { OnStatus?.Invoke("send err: " + ex.Message); return false; }
        finally { listener.Stop(); }
    }

    //P2P receive (poll inbox, connect as client)
    public async Task<int> HandleInboxOnceAsync(string saveDir, CancellationToken ct)
    {
        var (json, st) = await _api.SignalInbox(ct);
        if (st != 200) { OnStatus?.Invoke($"inbox http {st}"); return 0; }
        if (json.RootElement.ValueKind != System.Text.Json.JsonValueKind.Array) return 0;

        var count = 0;
        foreach (var msg in json.RootElement.EnumerateArray())
        {
            var type = msg.GetProperty("type").GetString() ?? "";
            var from = msg.GetProperty("from").GetString() ?? "";
            var sdp = msg.GetProperty("sdp").GetString() ?? "";

            if (type == "offer")
            {
                try
                {
                    var offer = JsonDocument.Parse(sdp).RootElement;
                    if (offer.GetProperty("type").GetString() != "tcp-offer") continue;

                    var ip = offer.GetProperty("ip").GetString()!;
                    var port = offer.GetProperty("port").GetInt32();
                    var name = offer.GetProperty("name").GetString() ?? "file.bin";
                    var size = offer.GetProperty("size").GetInt64();
                    var sha = offer.GetProperty("sha256").GetString() ?? "";

                    Directory.CreateDirectory(saveDir);
                    var savePath = Path.Combine(saveDir, name);

                    using var client = new TcpClient();
                    await client.ConnectAsync(ip, port, ct);
                    using var net = client.GetStream();

                    var ok = await TcpTransport.ReceiveAsync(net, savePath, size, sha, ct);
                    if (ok) { OnStatus?.Invoke($"received {name}"); count++; }
                    else OnStatus?.Invoke($"hash mismatch for {name}");
                }
                catch (Exception ex) { OnStatus?.Invoke("recv err: " + ex.Message); }
            }
        }
        return count;
    }

    private static IPAddress? GetLocalIPv4()
    {
        foreach (var ni in System.Net.NetworkInformation.NetworkInterface.GetAllNetworkInterfaces())
        {
            if (ni.OperationalStatus != System.Net.NetworkInformation.OperationalStatus.Up) continue;
            var ipProps = ni.GetIPProperties();
            foreach (var ua in ipProps.UnicastAddresses)
                if (ua.Address.AddressFamily == AddressFamily.InterNetwork && !IPAddress.IsLoopback(ua.Address))
                    return ua.Address;
        }
        return null;
    }

    public void Dispose() => _hb.Dispose();
}
