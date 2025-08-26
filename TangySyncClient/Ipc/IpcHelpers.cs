using System;
using Dalamud.Plugin;
using Dalamud.Plugin.Ipc;
using Dalamud.Plugin.Services;

namespace TangySyncClient.Ipc;

internal static class IpcHelpers
{
    internal static ICallGateSubscriber<TArg, TRes>? TryGate<TArg, TRes>(
        IDalamudPluginInterface pi, IPluginLog log, string gateName)
    {
        try { return pi.GetIpcSubscriber<TArg, TRes>(gateName); }
        catch (Exception ex)
        {
            log.Warning($"IPC gate not available: {gateName} ({ex.Message})");
            return null;
        }
    }

    internal static ICallGateSubscriber<TRes>? TryGate<TRes>(
        IDalamudPluginInterface pi, IPluginLog log, string gateName)
    {
        try { return pi.GetIpcSubscriber<TRes>(gateName); }
        catch (Exception ex)
        {
            log.Warning($"IPC gate not available: {gateName} ({ex.Message})");
            return null;
        }
    }
}
