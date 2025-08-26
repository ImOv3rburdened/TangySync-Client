using Dalamud.Plugin;
using Dalamud.Plugin.Ipc;
using Dalamud.Plugin.Services;

namespace TangySyncClient.Ipc;

internal sealed class PenumbraBridge
{
    private readonly IPluginLog _log;
    private readonly ICallGateSubscriber<string, bool>? _setCollectionLegacy; // Penumbra.SetCollectionForPlayer
    private readonly ICallGateSubscriber<string, bool>? _setCollectionForObject; // Penumbra.SetCollectionForObject

    public bool Available => _setCollectionLegacy is not null || _setCollectionForObject is not null;

    public PenumbraBridge(IDalamudPluginInterface pi, IPluginLog log, string gateSetCollectionLegacy,
        string gateSetCollectionForObject = "Penumbra.SetCollectionForObject")
    {
        _log = log;
        _setCollectionLegacy = IpcHelpers.TryGate<string, bool>(pi, log, gateSetCollectionLegacy);
        _setCollectionForObject = IpcHelpers.TryGate<string, bool>(pi, log, gateSetCollectionForObject);
    }

    public bool TrySetCollection(string collection)
    {
        if (_setCollectionLegacy is not null)
            return _setCollectionLegacy.InvokeFunc(collection);

        if (_setCollectionForObject is not null)
            return _setCollectionForObject.InvokeFunc(collection);

        _log.Warning("Penumbra IPC unavailable.");
        return false;
    }
}
