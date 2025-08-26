using Dalamud.Plugin;
using Dalamud.Plugin.Ipc;
using Dalamud.Plugin.Services;

namespace TangySyncClient.Ipc;

internal sealed class HonorificBridge
{
    private readonly IPluginLog _log;
    private readonly ICallGateSubscriber<string, bool>? _applyJson;

    public bool Available => _applyJson is not null;

    public HonorificBridge(IDalamudPluginInterface pi, IPluginLog log, string gateApplyJson)
    {
        _log = log;
        _applyJson = IpcHelpers.TryGate<string, bool>(pi, log, gateApplyJson);
    }

    public bool TryApply(string json)
    {
        if (_applyJson is null) { _log.Warning("Honorific IPC unavailable."); return false; }
        return _applyJson.InvokeFunc(json);
    }
}
