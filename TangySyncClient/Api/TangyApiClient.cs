using System;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Tasks;
using TangySyncClient.Config;
using TangySyncClient.Models;

namespace TangySyncClient.Api;

internal sealed class TangyApiClient
{
    private readonly HttpClient _http = new();
    private readonly Configuration _cfg;

    public TangyApiClient(Configuration cfg)
    {
        _cfg = cfg;
    }

    private string Url(string path) =>$"{_cfg.ServerUrl.TrimEnd('/')}{path}";

    //Server routes put in later
    public async Task<bool> PingAsync()
    {
        try
        {
            var resp = await _http.GetAsync(Url("/api/ping"));
            return resp.IsSuccessStatusCode;
        }
        catch
        {
             return false;
        }
    }

    public async Task<string?> StartPairAsync()
    {
        try
        {
            var resp = await _http.PostAsync(Url("/api/pair/start"), null);
            if (!resp.IsSuccessStatusCode) return null;
            var json = await resp.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(json);
            return doc.RootElement.TryGetProperty("code", out var c) ? c.GetString() : null;
        }
        catch
        {
            return null;
        }
    }

    public async Task<string?> FinishPairAsync(string code)
    {
        //Post /api/pair/finish { code} { secret: ".."}
        try
        {
            var body = JsonContent.Create(new { code });
            var resp = await _http.PostAsync(Url("/api/pair/finish"), body);
            if (!resp.IsSuccessStatusCode) return null;
            var json = await resp.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(json);
            return doc.RootElement.TryGetProperty("secret", out var s) ? s.GetString() : null;
        }
        catch
                {
            return null;
        }
    }

    public async Task<bool> UnpairAsync()
    {
        try
        {
            var body = JsonContent.Create(new { secret = _cfg.SecretKey });
            var resp = await _http.PostAsync(Url("/api/pair/revoke"), body);
            return resp.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }
}