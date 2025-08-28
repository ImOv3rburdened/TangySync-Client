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
    private ICallGateSubscriber<string>? _get;   // usually: Heels.GetOffset -> string
    private object? _set;                        // dynamic, handles different signatures

    public bool Available { get; private set; }

    public HeelsBridge(IDalamudPluginInterface pi)
    {
        try
        {
            _get = pi.GetIpcSubscriber<string>("Heels.GetOffset");

            // Try common signatures for Heels.OffsetUpdate
            try { _set = pi.GetIpcSubscriber<string>("Heels.OffsetUpdate"); }          // InvokeAction(string)
            catch
            {
                try { _set = pi.GetIpcSubscriber<string, object?>("Heels.OffsetUpdate"); } // InvokeFunc(string) / older variants
                catch { _set = pi.GetIpcSubscriber<object?>("Heels.OffsetUpdate"); }       // no-arg variant, if any
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
            // Try action(string)
            try { d.InvokeAction(offset ?? ""); return true; } catch { }
            // Try func(string)
            try { var _ = d.InvokeFunc(offset ?? ""); return true; } catch { }
            // Try action() with no params
            try { d.InvokeAction(); return true; } catch { }
        }
        catch { }
        return false;
    }
}
