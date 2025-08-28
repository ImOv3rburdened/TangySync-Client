// TangySync/Services/ApiClient.cs
using System;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace TangySync.Services;

public sealed class ApiClient
{
    private static readonly JsonDocument EmptyOk = JsonDocument.Parse("""{"ok":false}""");
    private readonly HttpClient _http;
    private readonly ConfigService _cfg;

    public ApiClient(ConfigService cfg)
    {
        _cfg = cfg;
        // One HttpClient for the life of the plugin
        var handler = new HttpClientHandler
        {
            AllowAutoRedirect = false,
            AutomaticDecompression = System.Net.DecompressionMethods.GZip | System.Net.DecompressionMethods.Deflate
        };
        _http = new HttpClient(handler)
        {
            Timeout = TimeSpan.FromSeconds(8)
        };
    }

    private Uri MakeUri(string path)
    {
        var baseUrl = string.IsNullOrWhiteSpace(_cfg.Data.ServerBaseUrl)
            ? "http://127.0.0.1/"
            : _cfg.Data.ServerBaseUrl;
        baseUrl = baseUrl.TrimEnd('/') + "/";
        path = path.TrimStart('/');
        return new Uri(new Uri(baseUrl), path);
    }

    private HttpRequestMessage Make(HttpMethod m, string path, HttpContent? content = null)
    {
        var req = new HttpRequestMessage(m, MakeUri(path));
        var tok = _cfg.Data.Token;
        if (!string.IsNullOrWhiteSpace(tok))
            req.Headers.Authorization = new("Bearer", tok);
        if (content != null) req.Content = content;
        return req;
    }

    private static (JsonDocument, int) SafeResult(string error, int status = 0)
        => (JsonDocument.Parse($"{{\"ok\":false,\"error\":\"{error.Replace("\"", "\\\"")}\"}}"), status);

    // --------- Core safe ops ---------

    public async Task<(JsonDocument json, int status)> GetJson(string path, CancellationToken ct = default)
    {
        try
        {
            using var res = await _http.SendAsync(Make(HttpMethod.Get, path), ct).ConfigureAwait(false);
            var status = (int)res.StatusCode;
            var stream = await res.Content.ReadAsStreamAsync(ct).ConfigureAwait(false);
            var json = await JsonDocument.ParseAsync(stream, cancellationToken: ct).ConfigureAwait(false);
            return (json, status);
        }
        catch (Exception ex) { return SafeResult(ex.Message); }
    }

    public async Task<(JsonDocument json, int status)> PostJson(string path, object payload, CancellationToken ct = default)
    {
        try
        {
            using var res = await _http.SendAsync(Make(HttpMethod.Post, path, JsonContent.Create(payload)), ct).ConfigureAwait(false);
            var status = (int)res.StatusCode;
            var stream = await res.Content.ReadAsStreamAsync(ct).ConfigureAwait(false);
            var json = await JsonDocument.ParseAsync(stream, cancellationToken: ct).ConfigureAwait(false);
            return (json, status);
        }
        catch (Exception ex) { return SafeResult(ex.Message); }
    }

    public async Task<int> PostStatus(string path, object payload, CancellationToken ct = default)
    {
        try
        {
            using var res = await _http.SendAsync(Make(HttpMethod.Post, path, JsonContent.Create(payload)), ct).ConfigureAwait(false);
            return (int)res.StatusCode;
        }
        catch { return 0; }
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

    // -------- Signaling --------
    public Task<int> SignalOffer(string to, string payload, CancellationToken ct = default)
        => PostStatus("/api/signal/offer", new { to, sdp = payload }, ct);

    public Task<int> SignalAnswer(string to, string payload, CancellationToken ct = default)
        => PostStatus("/api/signal/answer", new { to, sdp = payload }, ct);

    public Task<(JsonDocument, int)> SignalInbox(CancellationToken ct = default)
        => GetJson("/api/signal/inbox", ct);
}
