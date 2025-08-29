using Dalamud.Interface.Colors;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using Dalamud.Utility;
using ImGuiNET;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Numerics;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using TangySync.Interop;
using TangySync.MCDF;
using TangySync.Services;

namespace TangySync.Ui;

public sealed class MainWindow : IDisposable
{
    private readonly IDalamudPluginInterface _pi;
    private readonly ApiClient _api;
    private readonly ConfigService _cfg;
    private readonly ICondition _cond;
    private readonly IClientState _client;

    //IPC bridges
    private readonly IGlamourerBridge _glam;
    private readonly ICustomizePlusBridge _cplus;
    private readonly IHonorificBridge _honor;
    private readonly IHeelsBridge _heels;
    private readonly IPenumbraBridge _penu;

    //Sync & transfer
    private readonly SyncClient _sync;
    private CancellationTokenSource? _xferCts;
    private int _bps = 512_000;
    private bool _paused;
    private string _status = "Idle";

    //Tabs & state
    private bool _visible;
    private int _tab = 0; // 0 Data, 1 Friends, 2 Sync, 3 Health, 4 Auth

    //Data tab (explorer)
    private string _folder;
    private string _search = "";
    private string _selectedFile = "";
    private string _exportName = "tangysync.mcdf";

    //Friends
    private string _vanity = "";
    private string _friendToAdd = "";
    private List<FriendRow> _friends = new();

    //Health state
    private bool _healthLoading = false;
    private string _healthError = "";
    private string _healthService = "";
    private string _healthVersion = "";
    private string _healthTime = "";
    private string _healthNotes = "";
    private int _healthUsers = 0;
    //key -> true/false; null = not loaded yet (shows yellow)
    private readonly Dictionary<string, bool?> _healthFlags = new(StringComparer.OrdinalIgnoreCase);

    //Convenience
    private string HealthUrl => _cfg.Data.ServerBaseUrl.TrimEnd('/') + "/health.php";


    private record FriendRow(string Vanity, bool Online, string Note);

    public MainWindow(IDalamudPluginInterface pi, ApiClient api, ConfigService cfg, ICondition cond, IClientState client)
    {
        _pi = pi; _api = api; _cfg = cfg; _cond = cond; _client = client;

        _glam = new GlamourerBridge(pi);
        _cplus = new CustomizePlusBridge(pi);
        _honor = new HonorificBridge(pi);
        _heels = new HeelsBridge(pi);
        _penu = new PenumbraBridge(pi);

        _sync = new SyncClient(api, cond);
        _sync.OnStatus += s => _status = s;

        _folder = Directory.Exists(_cfg.Data.LastFolder)
            ? _cfg.Data.LastFolder
            : Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
    }

    public void Toggle() => _visible = !_visible;
    public void Dispose() => _sync.Dispose();

    public void Draw()
    {
        if (!_visible) return;

        ImGui.SetNextWindowSize(new Vector2(900, 610), ImGuiCond.FirstUseEver);
        if (!ImGui.Begin("TangySync", ref _visible, ImGuiWindowFlags.NoCollapse)) { ImGui.End(); return; }

        //Header
        ImGui.TextColored(ImGuiColors.DalamudViolet, "TangySync v0.2.0");
        ImGui.SameLine();
        ImGui.TextDisabled($"| {_status}");

        //Left nav
        ImGui.BeginChild("nav", new Vector2(160, 0), true);
        Nav("Data", 0); Nav("Friends", 1); Nav("Sync", 2); Nav("Health", 3); Nav("Auth", 4);
        ImGui.EndChild(); ImGui.SameLine();

        //Body
        ImGui.BeginChild("content", new Vector2(0, 0), false);
        switch (_tab)
        {
            case 0: DrawData(); break;
            case 1: DrawFriends(); break;
            case 2: DrawSync(); break;
            case 3: DrawHealth(); break;
            case 4: DrawAuth(); break;
        }
        ImGui.EndChild();

        ImGui.End();

        void Nav(string label, int idx)
        {
            var active = _tab == idx;
            if (active) ImGui.PushStyleColor(ImGuiCol.Button, ImGuiColors.HealerGreen);
            if (ImGui.Button(label, new Vector2(-1, 32))) _tab = idx;
            if (active) ImGui.PopStyleColor();
        }
    }

    //Data (MCDF explorer)
    private void DrawData()
    {
        var gpose = InGpose();
        if (!gpose)
            ImGui.TextColored(new Vector4(1, .8f, .2f, 1), "Actions require GPose. You can browse anytime.");

        ImGui.Separator();
        ImGui.Text("Folder:");
        ImGui.SameLine(); ImGui.SetNextItemWidth(420); ImGui.InputText("##folder", ref _folder, 512);
        ImGui.SameLine();
        if (ImGui.Button("📁")) ImGui.OpenPopup("folder_popup");
        if (ImGui.BeginPopup("folder_popup"))
        {
            if (ImGui.MenuItem("Use Documents"))
                SetFolder(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments));
            if (ImGui.MenuItem("Use Last Saved"))
                SetFolder(_cfg.Data.LastFolder);
            if (ImGui.MenuItem("Paste from Clipboard"))
            {
                var clip = ImGui.GetClipboardText();
                if (!string.IsNullOrWhiteSpace(clip) && Directory.Exists(clip)) SetFolder(clip);
                else _status = "Clipboard path invalid.";
            }
            ImGui.EndPopup();
        }
        ImGui.SameLine();
        if (ImGui.Button("Up"))
        {
            var p = Directory.GetParent(_folder)?.FullName;
            if (!string.IsNullOrEmpty(p)) SetFolder(p);
        }
        ImGui.SameLine();
        ImGui.SetNextItemWidth(200);
        ImGui.InputTextWithHint("##search", "Search .mcdf…", ref _search, 128);

        ImGui.Separator();
        if (ImGui.BeginTable("tbl", 2, ImGuiTableFlags.SizingStretchProp))
        {
            ImGui.TableSetupColumn("left", ImGuiTableColumnFlags.WidthStretch, 0.55f);
            ImGui.TableSetupColumn("right", ImGuiTableColumnFlags.WidthStretch, 0.45f);
            ImGui.TableNextRow();

            ImGui.TableSetColumnIndex(0);
            DrawExplorer();

            ImGui.TableSetColumnIndex(1);
            DrawDataActions(gpose);

            ImGui.EndTable();
        }
    }

    private void SetFolder(string path)
    {
        if (string.IsNullOrWhiteSpace(path) || !Directory.Exists(path)) return;
        _folder = path;
        _cfg.Data.LastFolder = _folder;
        _cfg.Save();
    }

    private void DrawExplorer()
    {
        ImGui.BeginChild("expl", new Vector2(0, 380), true);

        IEnumerable<string> dirs = Enumerable.Empty<string>();
        IEnumerable<string> files = Enumerable.Empty<string>();
        try
        {
            dirs = Directory.EnumerateDirectories(_folder).OrderBy(Path.GetFileName);
            files = Directory.EnumerateFiles(_folder, "*.mcdf").OrderBy(Path.GetFileName);
        }
        catch (Exception ex) { ImGui.TextColored(ImGuiColors.DalamudRed, ex.Message); }

        foreach (var d in dirs)
            if (ImGui.Selectable("📂 " + Path.GetFileName(d), false))
                SetFolder(d);

        var q = (_search ?? "").Trim();
        if (!string.IsNullOrEmpty(q))
            files = files.Where(f => Path.GetFileName(f).Contains(q, StringComparison.OrdinalIgnoreCase));

        foreach (var f in files)
        {
            var sel = string.Equals(_selectedFile, f, StringComparison.OrdinalIgnoreCase);
            if (ImGui.Selectable("📄 " + Path.GetFileName(f), sel))
                _selectedFile = f;
        }

        ImGui.EndChild();
    }

    private void DrawDataActions(bool gpose)
    {
        ImGui.Text("Selected:"); ImGui.SameLine();
        ImGui.TextDisabled(string.IsNullOrEmpty(_selectedFile) ? "—" : Path.GetFileName(_selectedFile));

        var canImport = gpose && File.Exists(_selectedFile);
        if (!canImport) ImGui.BeginDisabled();
        if (ImGui.Button("Import & Apply (.mcdf)"))
        {
            try { var mcdf = McdfCodec.Read(_selectedFile); ApplyMcdf(mcdf); _status = "Imported and applied."; }
            catch (Exception ex) { _status = "Import failed: " + ex.Message; }
        }
        if (!canImport) ImGui.EndDisabled();

        ImGui.Separator();
        ImGui.Text("Export name:"); ImGui.SameLine();
        ImGui.SetNextItemWidth(240);
        ImGui.InputText("##exp", ref _exportName, 256);
        ImGui.TextDisabled($"Folder: {_folder}");

        var canExport = gpose && Directory.Exists(_folder);
        if (!canExport) ImGui.BeginDisabled();
        if (ImGui.Button("Export Current Look (.mcdf)"))
        {
            try
            {
                var mcdf = CaptureMcdf();
                var outPath = Path.Combine(_folder, EnsureExt(_exportName, ".mcdf"));
                McdfCodec.Write(outPath, mcdf);
                _cfg.Data.LastFolder = _folder; _cfg.Save();
                _status = $"Exported to: {outPath}";
            }
            catch (Exception ex) { _status = "Export failed: " + ex.Message; }
        }
        if (!canExport) ImGui.EndDisabled();

        static string EnsureExt(string name, string ext) => name.EndsWith(ext, StringComparison.OrdinalIgnoreCase) ? name : name + ext;
    }

    private McdfData CaptureMcdf()
    {
        var name = GetPlayerName();
        var mcdf = new McdfData
        {
            Description = $"TangySync MCDF for {name}",
            GlamourerData = _glam.TryGetLocalBase64(name, out var g) ? g : "",
            CustomizePlusData = _cplus.GetActiveProfileBase64(),
            ManipulationData = _penu.GetPlayerMeta(),
        };
        var heels = _heels.GetOffset();
        var title = _honor.GetLocalTitle();
        if (!string.IsNullOrEmpty(heels) || !string.IsNullOrEmpty(title))
        {
            var tail = $"__tangy_meta__|heels:{heels}|title:{title}";
            mcdf.ManipulationData = string.IsNullOrEmpty(mcdf.ManipulationData) ? tail : mcdf.ManipulationData + "\n" + tail;
        }
        return mcdf;
    }

    private void ApplyMcdf(McdfData mcdf)
    {
        _ = _penu.ApplyPlayerMeta(mcdf.ManipulationData);
        _ = _cplus.ApplyProfileBase64(mcdf.CustomizePlusData);
        _ = _glam.TryApplyLocalBase64(GetPlayerName(), mcdf.GlamourerData);

        if (!string.IsNullOrEmpty(mcdf.ManipulationData) && mcdf.ManipulationData.Contains("__tangy_meta__"))
        {
            foreach (var line in mcdf.ManipulationData.Split('\n'))
            {
                if (!line.StartsWith("__tangy_meta__")) continue;
                foreach (var kv in line.Split('|'))
                {
                    if (kv.StartsWith("heels:")) _ = _heels.SetOffset(kv["heels:".Length..]);
                    if (kv.StartsWith("title:")) _ = _honor.SetLocalTitle(kv["title:".Length..]);
                }
            }
        }
    }

    //Friends
    private async void DrawFriends()
    {
        var authed = !string.IsNullOrWhiteSpace(_cfg.Data.Token);
        if (!authed) ImGui.TextColored(ImGuiColors.DalamudYellow, "Paste your token in Auth first.");

        ImGui.BeginDisabled(!authed);

        ImGui.Text("Your vanity:"); ImGui.SameLine();
        ImGui.SetNextItemWidth(220); ImGui.InputText("##vanity", ref _vanity, 64);
        ImGui.SameLine();
        if (ImGui.Button("Save Vanity"))
        {
            var (_, st) = await _api.PostJson("/api/user/vanity", new { vanity = _vanity });
            _status = st == 200 ? "Vanity saved." : $"Failed (HTTP {st}).";
        }

        ImGui.Separator();
        ImGui.Text("Add friend by @vanity:"); ImGui.SameLine();
        ImGui.SetNextItemWidth(220); ImGui.InputText("##addf", ref _friendToAdd, 64);
        ImGui.SameLine();
        if (ImGui.Button("Send Request") && !string.IsNullOrWhiteSpace(_friendToAdd))
        {
            var st = await _api.FriendsRequest(_friendToAdd);
            _status = st == 200 ? "Request sent." : $"Failed (HTTP {st}).";
        }
        ImGui.SameLine();
        if (ImGui.Button("Refresh"))
        {
            _ = Task.Run(LoadFriends);  //runs in background
        }

        ImGui.Separator();
        if (ImGui.BeginTable("friends", 4, ImGuiTableFlags.RowBg | ImGuiTableFlags.SizingStretchProp))
        {
            ImGui.TableSetupColumn("Vanity");
            ImGui.TableSetupColumn("Online");
            ImGui.TableSetupColumn("Note");
            ImGui.TableSetupColumn("Actions", ImGuiTableColumnFlags.WidthFixed, 200);
            ImGui.TableHeadersRow();

            foreach (var f in _friends)
            {
                ImGui.TableNextRow();
                ImGui.TableSetColumnIndex(0); ImGui.TextUnformatted(f.Vanity);
                ImGui.TableSetColumnIndex(1); ImGui.TextColored(f.Online ? ImGuiColors.HealerGreen : ImGuiColors.DalamudGrey, f.Online ? "online" : "offline");
                ImGui.TableSetColumnIndex(2); ImGui.TextDisabled(string.IsNullOrEmpty(f.Note) ? "—" : f.Note);
                ImGui.TableSetColumnIndex(3);
                ImGui.PushID(f.Vanity);
                if (ImGui.Button("Accept")) _ = _api.FriendsAccept(f.Vanity);
                ImGui.SameLine();
                if (ImGui.Button("Remove")) _ = _api.FriendsRemove(f.Vanity);
                ImGui.PopID();
            }

            ImGui.EndTable();
        }

        ImGui.EndDisabled();
    }

    private DateTime _lastFriendsRefresh = DateTime.MinValue;
    private bool _friendsLoading = false;

    private async Task LoadFriends()
    {
        if (_friendsLoading) return;                   //prevent spamming
        if ((DateTime.UtcNow - _lastFriendsRefresh).TotalSeconds < 5) return; //cooldown

        _friendsLoading = true;
        try
        {
            var (json, st) = await _api.FriendsList();
            if (st != 200)
            {
                _status = $"friends http {st}";
                return;
            }

            var list = new List<FriendRow>();
            foreach (var e in json.RootElement.EnumerateArray())
            {
                var vanity = e.TryGetProperty("vanity", out var v) ? v.GetString() ?? "" : "";
                if (string.IsNullOrEmpty(vanity)) continue;
                var online = e.TryGetProperty("online", out var o) && o.GetBoolean();
                var note = e.TryGetProperty("note", out var n) ? n.GetString() ?? "" : "";
                list.Add(new FriendRow(vanity, online, note));
            }
            _friends = list;
            _status = $"Friends loaded: {_friends.Count}";
            _lastFriendsRefresh = DateTime.UtcNow;
        }
        catch (Exception ex)
        {
            _status = "Friends load error: " + ex.Message;
        }
        finally { _friendsLoading = false; }
    }


    //Sync (send/receive)
    private void DrawSync()
    {
        var authed = !string.IsNullOrWhiteSpace(_cfg.Data.Token);
        if (!authed) ImGui.TextColored(ImGuiColors.DalamudYellow, "Paste your token in Auth first.");

        ImGui.BeginDisabled(!authed);

        ImGui.Text("Selected file:");
        ImGui.SameLine();
        ImGui.TextDisabled(string.IsNullOrEmpty(_selectedFile) ? "— (select a .mcdf in Data tab)" : _selectedFile);

        ImGui.SetNextItemWidth(200);
        ImGui.InputText("Send to @vanity", ref _friendToAdd, 64);
        ImGui.SameLine();
        var canSend = File.Exists(_selectedFile);
        if (!canSend) ImGui.BeginDisabled();
        if (ImGui.Button("Send file"))
        {
            _xferCts?.Cancel();
            _xferCts = new CancellationTokenSource();
            _ = _sync.SendFileAsync(_friendToAdd, _selectedFile, _bps, _xferCts.Token);
        }
        if (!canSend) ImGui.EndDisabled();

        ImGui.SameLine();
        if (ImGui.Button(_paused ? "Resume" : "Pause"))
        {
            _paused = !_paused;
            if (_paused) _xferCts?.Cancel();
        }

        ImGui.SameLine(); ImGui.SetNextItemWidth(150);
        if (ImGui.InputInt("Limit (bytes/s)", ref _bps)) if (_bps < 32_000) _bps = 32_000;

        if (ImGui.Button("Receive now (poll inbox)"))
        {
            _ = Task.Run(async () =>
                await _sync.HandleInboxOnceAsync(_folder, CancellationToken.None));
        }

        ImGui.TextDisabled("LAN/port-forward testing. No STUN/ICE yet.");
        ImGui.EndDisabled();
    }

    //Health
    private static void DrawDot(Vector4 color)
    {
        ImGui.PushStyleColor(ImGuiCol.Text, color);
        ImGui.TextUnformatted("●");
        ImGui.PopStyleColor();
    }

    private static void StatusRow(string label, bool? ok)
    {
        ImGui.TableNextRow();
        ImGui.TableSetColumnIndex(0); ImGui.TextUnformatted(label);
        ImGui.TableSetColumnIndex(1);
        if (ok is null) DrawDot(Dalamud.Interface.Colors.ImGuiColors.DalamudYellow);
        else if (ok == true) DrawDot(Dalamud.Interface.Colors.ImGuiColors.HealerGreen);
        else DrawDot(Dalamud.Interface.Colors.ImGuiColors.DalamudRed);
    }

    private async Task FetchHealthAsync()
    {
        if (_healthLoading) return;
        _healthLoading = true;
        _healthError = "";
        try
        {
            using var http = new HttpClient() { Timeout = TimeSpan.FromSeconds(8) };
            var json = await http.GetStringAsync(HealthUrl);
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            _healthService = root.TryGetProperty("service", out var s) ? (s.GetString() ?? "") : "";
            _healthVersion = root.TryGetProperty("version", out var v) ? (v.GetString() ?? "") : "";
            _healthTime = root.TryGetProperty("time", out var t) ? (t.GetString() ?? "") : "";
            _healthUsers = root.TryGetProperty("user_count", out var u) ? u.GetInt32() : 0;
            _healthNotes = root.TryGetProperty("notes", out var n) ? (n.GetString() ?? "") : "";

            //Clear and repopulate flags: treat any top-level bool as a status row,
            //but skip common non-status fields.
            _healthFlags.Clear();
            foreach (var prop in root.EnumerateObject())
            {
                var name = prop.Name;
                if (name.Equals("ok", StringComparison.OrdinalIgnoreCase) ||
                    name.Equals("service", StringComparison.OrdinalIgnoreCase) ||
                    name.Equals("version", StringComparison.OrdinalIgnoreCase) ||
                    name.Equals("time", StringComparison.OrdinalIgnoreCase) ||
                    name.Equals("user_count", StringComparison.OrdinalIgnoreCase) ||
                    name.Equals("notes", StringComparison.OrdinalIgnoreCase))
                    continue;

                if (prop.Value.ValueKind == JsonValueKind.True || prop.Value.ValueKind == JsonValueKind.False)
                    _healthFlags[name] = prop.Value.GetBoolean();
            }

            //If your health.php groups items (e.g., {"api":{"friends":true,"auth":true}}), include those too:
            foreach (var prop in root.EnumerateObject())
            {
                if (prop.Value.ValueKind == JsonValueKind.Object)
                {
                    foreach (var sub in prop.Value.EnumerateObject())
                    {
                        if (sub.Value.ValueKind == JsonValueKind.True || sub.Value.ValueKind == JsonValueKind.False)
                        {
                            var key = $"{prop.Name}.{sub.Name}";
                            _healthFlags[key] = sub.Value.GetBoolean();
                        }
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _healthError = ex.Message;
            //set flags to null so UI shows yellow while user retries
            foreach (var k in _healthFlags.Keys.ToList()) _healthFlags[k] = null;
            if (_healthFlags.Count == 0)
            {
                //provide a couple of standard rows so the table isn’t empty
                _healthFlags["api"] = null;
                _healthFlags["database"] = null;
            }
        }
        finally { _healthLoading = false; }
    }


    private void DrawHealth()
    {
        //Header with basic info
        ImGui.Text($"URL: {HealthUrl}");
        if (!string.IsNullOrEmpty(_healthService) || !string.IsNullOrEmpty(_healthVersion))
        {
            ImGui.SameLine();
            ImGui.TextDisabled($" | {_healthService} v{_healthVersion}");
        }
        if (!string.IsNullOrEmpty(_healthTime))
            ImGui.TextDisabled($"Updated: {_healthTime}");
        if (_healthUsers > 0)
            ImGui.TextColored(ImGuiColors.HealerGreen, $"Active users: {_healthUsers}");
        if (!string.IsNullOrEmpty(_healthNotes))
            ImGui.TextWrapped(_healthNotes);

        //Controls
        if (ImGui.Button(_healthLoading ? "Fetching…" : "Refresh"))
        {
            _ = Task.Run(FetchHealthAsync);
        }
        ImGui.SameLine();
        if (!string.IsNullOrEmpty(_healthError))
            ImGui.TextColored(ImGuiColors.DalamudYellow, _healthError);

        ImGui.Separator();

        //Status table
        if (ImGui.BeginTable("healthTbl", 2, ImGuiTableFlags.RowBg | ImGuiTableFlags.SizingStretchProp))
        {
            ImGui.TableSetupColumn("Check");
            ImGui.TableSetupColumn("Status", ImGuiTableColumnFlags.WidthFixed, 80);
            ImGui.TableHeadersRow();

            //sort keys for stable layout
            foreach (var kv in _healthFlags.OrderBy(k => k.Key, StringComparer.OrdinalIgnoreCase))
                StatusRow(kv.Key, kv.Value);

            ImGui.EndTable();
        }

        //auto-load the first time
        if (_healthFlags.Count == 0 && !_healthLoading)
            _ = Task.Run(FetchHealthAsync);
    }


    //Auth
    private void DrawAuth()
    {
        ImGui.Text("Discord registration / OAuth:");
        if (ImGui.Button("Open Link")) Util.OpenLink(_cfg.Data.DiscordRegisterUrl);

        ImGui.Separator();
        ImGui.Text("Token:");
        var t = _cfg.Data.Token;
        ImGui.InputText("##tok", ref t, 200, ImGuiInputTextFlags.ReadOnly);
        ImGui.SameLine();
        if (ImGui.Button("Clear")) { _cfg.Data.Token = ""; _cfg.Save(); }
    }

    //helpers
    private bool InGpose()
    {
        try
        {
            var t = typeof(Dalamud.Game.ClientState.Conditions.ConditionFlag);
            object val;
            if (Enum.TryParse(t, "InGpose", out val!) || Enum.TryParse(t, "InGPose", out val!) || Enum.TryParse(t, "Gpose", out val!))
                return _cond[(Dalamud.Game.ClientState.Conditions.ConditionFlag)val];
        }
        catch { }
        return false;
    }

    private string GetPlayerName()
    {
        try { return _client?.LocalPlayer?.Name?.TextValue ?? string.Empty; }
        catch { return string.Empty; }
    }
}
