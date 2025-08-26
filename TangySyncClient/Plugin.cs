using System;
using Dalamud.Game.Command;
using Dalamud.IoC;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using TangySyncClient.Config;
using TangySyncClient.Ipc;
using TangySyncClient.Ui;

namespace TangySyncClient;

public sealed class Plugin : IDalamudPlugin
{
    public string Name => "TangySync";

    [PluginService] internal static IDalamudPluginInterface? Pi { get; private set; }
    [PluginService] internal static ICommandManager? Cmd { get; private set; }
    [PluginService] internal static IPluginLog? Log { get; private set; }
    [PluginService] internal static IChatGui? Chat { get; private set; }
    [PluginService] internal static ICondition? Condition { get; private set; }

    private Configuration _cfg = null!;
    private MainWindow _win = null!;

    private PenumbraBridge _penumbra = null!;
    private GlamourerBridge _glamourer = null!;
    private CustomizePlusBridge _cplus = null!;
    private HeelsBridge _heels = null!;
    private HonorificBridge _honor = null!;

    private CommandInfo? _cmdInfo;
    private Action? _onUiOpenHandler;

    // store dynamic UiBuilder so we can unsubscribe cleanly
    private dynamic? _ui;

    private bool _disposed;

    public Plugin()
    {
        // 1) Config
        _cfg = Pi!.GetPluginConfig() as Configuration ?? new Configuration();
        _cfg.Initialize(Pi!);

        // 2) IPC bridges (gate names from config)
        _penumbra = new PenumbraBridge(Pi!, Log!, _cfg.Gate_Penumbra_SetCollection, _cfg.Gate_Penumbra_SetCollectionForObject);
        _glamourer = new GlamourerBridge(Pi!, Log!, _cfg.Gate_Glamourer_ApplyBase64, _cfg.Gate_Glamourer_ApplyDesign);
        _cplus = new CustomizePlusBridge(Pi!, Log!, _cfg.Gate_CustomizePlus_ApplyProfileJson);
        _heels = new HeelsBridge(Pi!, Log!, _cfg.Gate_Heels_ApplyJson);
        _honor = new HonorificBridge(Pi!, Log!, _cfg.Gate_Honorific_Set);

        // 3) Window
        _win = new MainWindow(_cfg, Log!, Condition!, _penumbra, _glamourer, _cplus, _heels, _honor);

        // 4) UiBuilder hookup (dynamic to support both UiBuilder and IUiBuilder)
        var uiObj = Pi!.UiBuilder;    // strongly-typed on interface, but we don't care which exact type it is
        _ui = (dynamic)uiObj;
        _ui.Draw += (Action)_win.Draw; // event signature is Action
        _onUiOpenHandler = () => _win.IsOpen = true;
        _ui.OpenConfigUi += (Action)_onUiOpenHandler;

        // 5) Command
        _cmdInfo = new CommandInfo(OnCommand) { HelpMessage = "Open TangySync" };
        Cmd!.AddHandler("/tangysync", _cmdInfo);

        Chat?.Print("[TangySync] Loaded.");
    }

    private void OnCommand(string _, string __) => _win.IsOpen = true;

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        if (_cmdInfo is not null)
            Cmd?.RemoveHandler("/tangysync");

        try
        {
            if (_ui is not null)
            {
                _ui.Draw -= (Action)_win.Draw;
                if (_onUiOpenHandler is not null)
                    _ui.OpenConfigUi -= (Action)_onUiOpenHandler;
            }
        }
        catch
        {
            // swallow: some Dalamud builds might throw during unload; safe to ignore here
        }

        Chat?.Print("[TangySync] Unloaded.");
    }
}
