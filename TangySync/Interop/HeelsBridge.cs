using Dalamud.Plugin;
using Dalamud.Plugin.Ipc;

namespace TangySync.Interop;

public interface IHeelsBridge
{
    bool Available { get; }
    string GetOffset();
    bool SetOffset(string offset);
}

public sealed class HeelsBridge : IHeelsBridge
{
    private ICallGateSubscriber<string>? _get;
    private object? _set;

    public bool Available { get; private set; }

    public HeelsBridge(IDalamudPluginInterface pi)
    {
        try
        {
            _get = pi.GetIpcSubscriber<string>("Heels.GetOffset");
            
            try { _set = pi.GetIpcSubscriber<string>("Heels.OffsetUpdate"); }
            catch
            {
                try { _set = pi.GetIpcSubscriber<string, object?>("Heels.OffsetUpdate"); }
                catch { _set = pi.GetIpcSubscriber<object?>("Heels.OffsetUpdate"); }
            }

            Available = true;
        }
        catch { Available = false; }
    }

    public string GetOffset()
    {
        if (!Available || _get is null) return "";
        try { return _get.InvokeFunc() ?? ""; } catch { return ""; }
    }

    public bool SetOffset(string offset)
    {
        if (!Available || _set is null) return false;
        try
        {
            dynamic d = _set;
            try { d.InvokeAction(offset ?? ""); return true; } catch { }
            try { var _ = d.InvokeFunc(offset ?? ""); return true; } catch { }
            try { d.InvokeAction(); return true; } catch { }
        }
        catch { }
        return false;
    }
}
