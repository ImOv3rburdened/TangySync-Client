using System;
using Dalamud.Plugin;
using Dalamud.Plugin.Ipc;

namespace TangySync.Interop;

public interface ICustomizePlusBridge
{
    bool Available { get; }
    string GetActiveProfileBase64();
    bool ApplyProfileBase64(string base64OrJson);
}

public sealed class CustomizePlusBridge : ICustomizePlusBridge
{
    private readonly ICallGateSubscriber<ushort, (int, Guid?)> _getActiveProfile;
    private readonly ICallGateSubscriber<Guid, (int, string?)> _getProfileById;
    private readonly ICallGateSubscriber<string, int> _applyToLocal;

    public bool Available { get; }

    public CustomizePlusBridge(IDalamudPluginInterface pi)
    {
        try
        {
            _getActiveProfile = pi.GetIpcSubscriber<ushort, (int, Guid?)>("CustomizePlus.Profile.GetActiveProfileIdOnCharacter");
            _getProfileById = pi.GetIpcSubscriber<Guid, (int, string?)>("CustomizePlus.Profile.GetByUniqueId");
            _applyToLocal = pi.GetIpcSubscriber<string, int>("CustomizePlus.Profile.ApplyProfileToLocalPlayer");
            Available = true;
        }
        catch { Available = false; }
    }

    public string GetActiveProfileBase64()
    {
        if (!Available) return "";
        try
        {
            var (rc, id) = _getActiveProfile.InvokeFunc(0);
            if (rc != 0 || id is null) return "";
            var (rc2, json) = _getProfileById.InvokeFunc(id.Value);
            return rc2 == 0 && !string.IsNullOrEmpty(json) ? json! : "";
        }
        catch { return ""; }
    }

    public bool ApplyProfileBase64(string base64OrJson)
    {
        if (!Available || string.IsNullOrEmpty(base64OrJson)) return false;
        try { return _applyToLocal.InvokeFunc(base64OrJson) == 0; } catch { return false; }
    }
}
