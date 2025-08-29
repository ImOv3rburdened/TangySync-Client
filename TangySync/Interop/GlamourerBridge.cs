using Dalamud.Plugin;
using Dalamud.Plugin.Ipc;

namespace TangySync.Interop;

public interface IGlamourerBridge
{
    bool Available { get; }
    bool TryGetLocalBase64(string playerName, out string data);
    bool TryApplyLocalBase64(string playerName, string base64OrJson);
}

public sealed class GlamourerBridge : IGlamourerBridge
{
    private const uint LockKey = 0x54414E47;
    
    private readonly ICallGateSubscriber<string, uint, (int, string?)> _getByName;
   
    private readonly ICallGateSubscriber<string, string, uint, int> _applyByName;

    public GlamourerBridge(IDalamudPluginInterface pi)
    {
        try
        {
            _getByName = pi.GetIpcSubscriber<string, uint, (int, string?)>("Glamourer.GetStateBase64Name");
            _applyByName = pi.GetIpcSubscriber<string, string, uint, int>("Glamourer.ApplyStateName");
            Available = true;
        }
        catch { Available = false; }
    }

    public bool Available { get; }

    public bool TryGetLocalBase64(string playerName, out string data)
    {
        data = string.Empty;
        if (!Available || string.IsNullOrWhiteSpace(playerName)) return false;
        try
        {
            var (ec, s) = _getByName.InvokeFunc(playerName, LockKey);
            if (ec == 0 && !string.IsNullOrEmpty(s)) { data = s!; return true; }
        }
        catch { }
        return false;
    }

    public bool TryApplyLocalBase64(string playerName, string base64OrJson)
    {
        if (!Available || string.IsNullOrEmpty(playerName) || string.IsNullOrEmpty(base64OrJson)) return false;
        try { return _applyByName.InvokeFunc(base64OrJson, playerName, LockKey) == 0; }
        catch { return false; }
    }
}
