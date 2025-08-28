using Dalamud.Configuration;
using Dalamud.Plugin;
using System;

namespace TangySync.Services;

public sealed class Config : IPluginConfiguration
{
    public int Version { get; set; } = 1;

    public string ServerBaseUrl { get; set; } = "https://tangysync.com";
    public string Token { get; set; } = "";
    public string LastFolder { get; set; } = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
    public string DiscordRegisterUrl { get; set; } = "https://discord.gg/Egxkm9h8Vb";
    public bool ConfirmBeforeApply { get; set; } = true;
}

public sealed class ConfigService
{
    private readonly IDalamudPluginInterface _pi;
    public Config Data { get; private set; }

    private ConfigService(IDalamudPluginInterface pi, Config cfg)
    {
        _pi = pi;
        Data = cfg;
    }

    public static ConfigService Load(IDalamudPluginInterface pi)
    {
        var cfg = pi.GetPluginConfig() as Config ?? new Config();
        return new ConfigService(pi, cfg);
    }

    public void Save() => _pi.SavePluginConfig(Data);
}
