using Dalamud.Game.Command;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;

namespace TangySync;

public sealed class Plugin : IDalamudPlugin
{
    public string Name => "TangySync";

    private readonly IDalamudPluginInterface _pi;
    private readonly ICommandManager _commands;
    private readonly Ui.MainWindow _ui;

    public Plugin(
        IDalamudPluginInterface pi,
        ICommandManager commands,
        IChatGui chat,
        IClientState client,
        ICondition condition)
    {
        _pi = pi;
        _commands = commands;

        var cfg = Services.ConfigService.Load(pi);
        var api = new Services.ApiClient(cfg);

        _ui = new Ui.MainWindow(pi, api, cfg, condition, client);

        _commands.AddHandler("/tangysync", new CommandInfo((_, __) => _ui.Toggle())
        {
            HelpMessage = "Open TangySync (MCDF import/export, health, auth, sync)."
        });

        _pi.UiBuilder.Draw += _ui.Draw;
        _pi.UiBuilder.OpenConfigUi += () => _ui.Toggle();
        _pi.UiBuilder.OpenMainUi += () => _ui.Toggle();

        chat.Print("[TangySync] v0.2.0 loaded. /tangysync to open.");
    }

    public void Dispose()
    {
        _commands.RemoveHandler("/tangysync");
        _pi.UiBuilder.Draw -= _ui.Draw;
    }
}
