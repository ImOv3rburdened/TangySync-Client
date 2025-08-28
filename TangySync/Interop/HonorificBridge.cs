using Dalamud.Plugin;
using Dalamud.Plugin.Ipc;

namespace TangySync.Interop;

public interface IHonorificBridge
{
    bool Available { get; }
    string GetLocalTitle();
    bool SetLocalTitle(string title);
}

public sealed class HonorificBridge : IHonorificBridge
{
    private ICallGateSubscriber<string>? _getTitle; // "Honorific.GetLocalCharacterTitle" -> string
    private object? _setTitle;                      // dynamic

    public bool Available { get; private set; }

    public HonorificBridge(IDalamudPluginInterface pi)
    {
        try
        {
            _getTitle = pi.GetIpcSubscriber<string>("Honorific.GetLocalCharacterTitle");

            // Try common setter signatures
            try { _setTitle = pi.GetIpcSubscriber<string>("Honorific.SetCharacterTitle"); }            // InvokeAction(string)
            catch
            {
                try { _setTitle = pi.GetIpcSubscriber<string, object?>("Honorific.SetCharacterTitle"); } // InvokeFunc(string)
                catch { _setTitle = pi.GetIpcSubscriber<object?>("Honorific.SetCharacterTitle"); }       // no-arg
            }

            Available = true;
        }
        catch { Available = false; }
    }

    public string GetLocalTitle()
    {
        if (!Available || _getTitle is null) return "";
        try { return _getTitle.InvokeFunc() ?? ""; } catch { return ""; }
    }

    public bool SetLocalTitle(string title)
    {
        if (!Available || _setTitle is null) return false;
        try
        {
            dynamic d = _setTitle;
            try { d.InvokeAction(title ?? ""); return true; } catch { }
            try { var _ = d.InvokeFunc(title ?? ""); return true; } catch { }
            try { d.InvokeAction(); return true; } catch { }
        }
        catch { }
        return false;
    }
}
