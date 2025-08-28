// TangySync/Interop/PenumbraBridge.cs
using Dalamud.Plugin;
using Dalamud.Plugin.Ipc;

namespace TangySync.Interop;

public interface IPenumbraBridge
{
    bool Available { get; }
    string GetPlayerMeta();
    bool ApplyPlayerMeta(string metaJson);
}

public sealed class PenumbraBridge : IPenumbraBridge
{
    private readonly ICallGateSubscriber<string> _getMeta;
    private readonly ICallGateSubscriber<string, int> _setMeta;

    public PenumbraBridge(IDalamudPluginInterface pi)
    {
        try
        {
            _getMeta = pi.GetIpcSubscriber<string>("Penumbra.GetPlayerMetaManipulations");
            _setMeta = pi.GetIpcSubscriber<string, int>("Penumbra.SetPlayerMetaManipulations");
            Available = true;
        }
        catch { Available = false; }
    }

    public bool Available { get; }
    public string GetPlayerMeta() { if (!Available) return ""; try { return _getMeta.InvokeFunc() ?? ""; } catch { return ""; } }
    public bool ApplyPlayerMeta(string metaJson) { if (!Available || string.IsNullOrEmpty(metaJson)) return false; try { return _setMeta.InvokeFunc(metaJson) == 0; } catch { return false; } }
}
