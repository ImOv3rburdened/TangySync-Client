using System;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace TangySync.Services;

public sealed class ApiClient
{
    private readonly HttpClient _http = new() { Timeout = TimeSpan.FromSeconds(15) };
    private readonly ConfigService _cfg;

    public ApiClient(ConfigService cfg) => _cfg = cfg;

    private HttpRequestMessage Make(HttpMethod m, string path, HttpContent? content = null)
    {
        var baseUri = new Uri(_cfg.Data.ServerBaseUrl.TrimEnd('/') + "/");
        var req = new HttpRequestMessage(m, new Uri(baseUri, path.TrimStart('/')));
        if (!string.IsNullOrWhiteSpace(_cfg.Data.Token))
            req.Headers.Authorization = new("Bearer", _cfg.Data.Token);
        if (content != null) req.Content = content;
        return req;
    }

    public async Task<(JsonDocument json, int status)> GetJson(string path, CancellationToken ct = default)
    {
        using var res = await _http.SendAsync(Make(HttpMethod.Get, path), ct);
        var status = (int)res.StatusCode;
        using var stream = await res.Content.ReadAsStreamAsync(ct);
        var json = await JsonDocument.ParseAsync(stream, cancellationToken: ct);
        return (json, status);
    }

    public async Task<(JsonDocument json, int status)> PostJson(string path, object payload, CancellationToken ct = default)
    {
        using var res = await _http.SendAsync(Make(HttpMethod.Post, path, JsonContent.Create(payload)), ct);
        var status = (int)res.StatusCode;
        using var stream = await res.Content.ReadAsStreamAsync(ct);
        var json = await JsonDocument.ParseAsync(stream, cancellationToken: ct);
        return (json, status);
    }

    public async Task<int> PostStatus(string path, object payload, CancellationToken ct = default)
    {
        using var res = await _http.SendAsync(Make(HttpMethod.Post, path, JsonContent.Create(payload)), ct);
        return (int)res.StatusCode;
    }

    // -------- Friends --------
    public Task<(JsonDocument, int)> FriendsList(CancellationToken ct = default)
        => GetJson("/api/friends", ct);

    public Task<int> FriendsRequest(string vanity, CancellationToken ct = default)
        => PostStatus("/api/friends/request", new { vanity }, ct);

    public Task<int> FriendsAccept(string vanity, CancellationToken ct = default)
        => PostStatus("/api/friends/accept", new { vanity }, ct);

    public Task<int> FriendsRemove(string vanity, CancellationToken ct = default)
        => PostStatus("/api/friends/remove", new { vanity }, ct);

    // -------- Presence & health --------
    public Task<(JsonDocument, int)> Health(CancellationToken ct = default)
        => GetJson("/api/health", ct);

    public Task<int> Heartbeat(bool gpose, CancellationToken ct = default)
        => PostStatus("/api/presence/heartbeat", new { gpose }, ct);

    // -------- Signaling (simple REST inbox) --------
    public Task<int> SignalOffer(string to, string payload, CancellationToken ct = default)
        => PostStatus("/api/signal/offer", new { to, sdp = payload }, ct);

    public Task<int> SignalAnswer(string to, string payload, CancellationToken ct = default)
        => PostStatus("/api/signal/answer", new { to, sdp = payload }, ct);

    public Task<(JsonDocument, int)> SignalInbox(CancellationToken ct = default)
        => GetJson("/api/signal/inbox", ct);
}
