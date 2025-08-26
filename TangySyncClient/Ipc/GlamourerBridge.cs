using Dalamud.Plugin;
using Dalamud.Plugin.Ipc;
using Dalamud.Plugin.Services;

namespace TangySyncClient.Ipc;

internal sealed class GlamourerBridge
{
    private readonly IPluginLog _log;

    // older: string base64 -> bool
    private readonly ICallGateSubscriber<string, bool>? _applyBase64;
    // newer: (string jsonOrBase64, int flags) -> bool   (we use flags=0)
    private readonly ICallGateSubscriber<string, bool>? _applyDesign; // treat as simple json/base64 apply

    public bool Available => _applyBase64 is not null || _applyDesign is not null;

    public GlamourerBridge(IDalamudPluginInterface pi, IPluginLog log,
        string gateApplyBase64, string gateApplyDesign = "Glamourer.ApplyDesign")
    {
        _log = log;
        _applyBase64 = IpcHelpers.TryGate<string, bool>(pi, log, gateApplyBase64);
        _applyDesign = IpcHelpers.TryGate<string, bool>(pi, log, gateApplyDesign);
    }

    public bool TryApply(string b64OrJson)
    {
        if (_applyBase64 is not null)
            return _applyBase64.InvokeFunc(b64OrJson);
        if (_applyDesign is not null)
            return _applyDesign.InvokeFunc(b64OrJson);
        _log.Warning("Glamourer IPC unavailable.");
        return false;
    }
}
