using System;
using System.Numerics;
using ImGuiNET;
using Dalamud.Plugin.Services;
using Dalamud.Game.ClientState.Conditions;
using TangySyncClient.Config;
using TangySyncClient.Ipc;
using TangySyncClient.Mcdf;

namespace TangySyncClient.Ui;

internal sealed class MainWindow
{
    private readonly Configuration _cfg;
    private readonly IPluginLog _log;
    private readonly ICondition _condition;
    private readonly PenumbraBridge _penumbra;
    private readonly GlamourerBridge _glamourer;
    private readonly CustomizePlusBridge _cplus;
    private readonly HeelsBridge _heels;
    private readonly HonorificBridge _honor;

    private string _mcdfPath = "";
    private string _lastError = "";

    private bool _enableSync;
    private bool _enforceSignature;

    public bool IsOpen;

    public MainWindow(
        Configuration cfg,
        IPluginLog log,
        ICondition condition,
        PenumbraBridge penumbra,
        GlamourerBridge glamourer,
        CustomizePlusBridge cplus,
        HeelsBridge heels,
        HonorificBridge honor)
    {
        _cfg = cfg;
        _log = log;
        _condition = condition;
        _penumbra = penumbra;
        _glamourer = glamourer;
        _cplus = cplus;
        _heels = heels;
        _honor = honor;

        _enableSync = cfg.EnableSync;
        _enforceSignature = cfg.EnforceMcdfSignature;
        _mcdfPath = cfg.LastMcdfPath ?? "";
    }

    public void Draw()
    {
        if (!IsOpen) return;
        ImGui.SetNextWindowSize(new Vector2(700, 420), ImGuiCond.FirstUseEver);
        if (!ImGui.Begin("TangySync", ref IsOpen)) { ImGui.End(); return; }

        if (!_enableSync)
            ImGui.TextColored(new Vector4(1f, .8f, .2f, 1f), "Sync disabled — enable in Config.");

        if (ImGui.BeginTabBar("ts_tabs"))
        {
            if (ImGui.BeginTabItem("Status"))
            {
                DrawStatus();
                ImGui.EndTabItem();
            }

            if (ImGui.BeginTabItem("MCDF"))
            {
                DrawMcdf();
                ImGui.EndTabItem();
            }

            if (ImGui.BeginTabItem("Config"))
            {
                DrawConfig();
                ImGui.EndTabItem();
            }

            ImGui.EndTabBar();
        }

        ImGui.End();
    }

    private void DrawStatus()
    {
        ImGui.Separator();
        ImGui.Text("Integrations:");
        Bullet("Penumbra", _penumbra.Available);
        Bullet("Glamourer", _glamourer.Available);
        Bullet("Customize+", _cplus.Available);
        Bullet("Heels", _heels.Available);
        Bullet("Honorific", _honor.Available);
    }

    private void DrawMcdf()
    {
        var inGpose = IsInGpose();

        ImGui.Text("MCDF File");
        ImGui.InputText("Path", ref _mcdfPath, 260);

        if (!inGpose)
            ImGui.TextColored(new Vector4(1f, .8f, .2f, 1f), "You must be in GPose to apply MCDF.");

        if (!string.IsNullOrEmpty(_lastError))
            ImGui.TextColored(new Vector4(1f, .5f, .5f, 1f), _lastError);

        bool allOk = _glamourer.Available || _cplus.Available || _heels.Available || _honor.Available || _penumbra.Available;

        ImGui.BeginDisabled(!inGpose || string.IsNullOrWhiteSpace(_mcdfPath) || !allOk);
        if (ImGui.Button("Load & Apply"))
        {
            _lastError = "";
            try
            {
                var payload = McdfLoader.Load(_mcdfPath);
                if (payload is null)
                {
                    _lastError = "Could not read MCDF (unsupported file or missing header/payload).";
                }
                else
                {
                    ApplyPayload(payload);
                    _cfg.LastMcdfPath = _mcdfPath;
                    _cfg.Save();
                }
            }
            catch (Exception ex)
            {
                _lastError = $"Apply failed: {ex.Message}";
                _log.Error(ex, "Apply MCDF failed");
            }
        }
        ImGui.EndDisabled();

        ImGui.Spacing();
        ImGui.TextColored(new Vector4(.7f, .7f, .7f, 1f), "Tip: Supports Mare .mcdf (LZ4), .zip (v1/payload.json), or .json.");
    }

    private void DrawConfig()
    {

        ImGui.Text("General");
        ImGui.Checkbox("Enable Sync", ref _enableSync);
        ImGui.Checkbox("Require MCDF signature (placeholder)", ref _enforceSignature);

        ImGui.Separator();
        ImGui.Text("IPC Gate Names (override if your plugins use different labels)");
        string gB64 = _cfg.Gate_Glamourer_ApplyBase64;
        string gDes = _cfg.Gate_Glamourer_ApplyDesign;
        string p1 = _cfg.Gate_Penumbra_SetCollection;
        string p2 = _cfg.Gate_Penumbra_SetCollectionForObject;
        string cP = _cfg.Gate_CustomizePlus_ApplyProfileJson;
        string hls = _cfg.Gate_Heels_ApplyJson;
        string hon = _cfg.Gate_Honorific_Set;

        ImGui.InputText("Glamourer.ApplyBase64", ref gB64, 256);
        ImGui.InputText("Glamourer.ApplyDesign", ref gDes, 256);
        ImGui.InputText("Penumbra.SetCollectionForPlayer", ref p1, 256);
        ImGui.InputText("Penumbra.SetCollectionForObject", ref p2, 256);
        ImGui.InputText("CustomizePlus.ApplyProfileJson", ref cP, 256);
        ImGui.InputText("Heels.ApplyProfileJson", ref hls, 256);
        ImGui.InputText("Honorific.SetProfileJson", ref hon, 256);



        if (ImGui.Button("Save Config"))
        {
            _cfg.EnableSync = _enableSync;
            _cfg.EnforceMcdfSignature = _enforceSignature;

            _cfg.Gate_Glamourer_ApplyBase64 = gB64;
            _cfg.Gate_Glamourer_ApplyDesign = gDes;
            _cfg.Gate_Penumbra_SetCollection = p1;
            _cfg.Gate_Penumbra_SetCollectionForObject = p2;
            _cfg.Gate_CustomizePlus_ApplyProfileJson = cP;
            _cfg.Gate_Heels_ApplyJson = hls;
            _cfg.Gate_Honorific_Set = hon;

            _cfg.Save();
        }
    }

    private bool IsInGpose()
    {
        // Try common spellings that have shown up across builds
        if (Enum.TryParse<ConditionFlag>("InGpose", true, out var f1)) return _condition[f1];
        if (Enum.TryParse<ConditionFlag>("InGPose", true, out var f2)) return _condition[f2];
        if (Enum.TryParse<ConditionFlag>("InGPosing", true, out var f3)) return _condition[f3];

        // If none exist in this build, assume not in GPose.
        return false;
    }

    private void Bullet(string name, bool ok)
    {
        var c = ok ? new Vector4(0.5f, 1f, 0.5f, 1f) : new Vector4(1f, 0.5f, 0.5f, 1f);
        ImGui.Bullet(); ImGui.SameLine(); ImGui.TextColored(c, ok ? $"{name}: OK" : $"{name}: missing");
    }

    private void ApplyPayload(McdfPayload p)
    {
        // Penumbra collection first if present
        if (!string.IsNullOrWhiteSpace(p.PenumbraCollection))
            _penumbra.TrySetCollection(p.PenumbraCollection!);

        if (!string.IsNullOrWhiteSpace(p.GlamourerBase64))
            _glamourer.TryApply(p.GlamourerBase64!);

        if (!string.IsNullOrWhiteSpace(p.CustomizePlusJson))
            _cplus.TryApply(p.CustomizePlusJson!);

        if (!string.IsNullOrWhiteSpace(p.HeelsJson))
            _heels.TryApply(p.HeelsJson!);

        if (!string.IsNullOrWhiteSpace(p.HonorificJson))
            _honor.TryApply(p.HonorificJson!);
    }
}
