using Dalamud.Plugin;
using Dalamud.Plugin.Ipc;
using Dalamud.Plugin.Services;

namespace TangySyncClient.Ipc;

internal sealed class CustomizePlusBridge
{
    private readonly IPluginLog _log;
    private readonly ICallGateSubscriber<string, bool>? _applyProfileJson;

    public bool Available => _applyProfileJson is not null;

    public CustomizePlusBridge(IDalamudPluginInterface pi, IPluginLog log, string gateApplyJson)
    {
        _log = log;
        _applyProfileJson = IpcHelpers.TryGate<string, bool>(pi, log, gateApplyJson);
    }

    public bool TryApply(string json)
    {
        if (_applyProfileJson is null) { _log.Warning("Customize+ IPC unavailable."); return false; }
        return _applyProfileJson.InvokeFunc(json);
    }
}
