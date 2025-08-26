using System;
using System.Collections.Generic;
using Dalamud.Configuration;
using Dalamud.Plugin;
using TangySyncClient.Models;

namespace TangySyncClient.Config;

[Serializable]
public sealed class Configuration : IPluginConfiguration
{
    public int Version { get; set; } = 2;

    // General
    public bool EnableSync { get; set; } = true;
    public bool EnforceMcdfSignature { get; set; } = false;

    // Server (not used for file-only flow, but kept for future)
    public string ServerUrl { get; set; } = "https://tangysync.com";
    public string SecretKey { get; set; } = "";
    public string OwnerNote { get; set; } = "";

    // Last used file
    public string? LastMcdfPath { get; set; }

    // IPC gate names — override these in Config if your local plugins expose different labels
    public string Gate_Glamourer_ApplyBase64 { get; set; } = "Glamourer.ApplyBase64";                  // legacy
    public string Gate_Glamourer_ApplyDesign { get; set; } = "Glamourer.ApplyDesign";                  // new-ish
    public string Gate_Penumbra_SetCollection { get; set; } = "Penumbra.SetCollectionForPlayer";       // legacy
    public string Gate_Penumbra_SetCollectionForObject { get; set; } = "Penumbra.SetCollectionForObject"; // newer
    public string Gate_CustomizePlus_ApplyProfileJson { get; set; } = "CustomizePlus.ApplyProfileJson";
    public string Gate_Heels_ApplyJson { get; set; } = "Heels.ApplyProfileJson";
    public string Gate_Honorific_Set { get; set; } = "Honorific.SetProfileJson";

    // Social (for future tabs)
    public List<FriendRecord> Friends { get; set; } = new();
    public List<SyncshellRecord> Shells { get; set; } = new();

    [NonSerialized] private IDalamudPluginInterface? _pi;
    public void Initialize(IDalamudPluginInterface pi) => _pi = pi;
    public void Save() => _pi?.SavePluginConfig(this);
}
