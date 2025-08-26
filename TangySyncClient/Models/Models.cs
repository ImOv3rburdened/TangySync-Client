using System;
using System.Collections.Generic;

namespace TangySyncClient.Models;

[Serializable]
public sealed class FriendRecord
{
    public string FriendId { get; set; } = "";
    public string Display { get; set; } = "";
    public bool IsPaired { get; set; } = false;
    public bool IsPaused { get; set; } = false;
}

[Serializable]
public sealed class SyncshellRecord
{
    public string ShellId { get; set; } = "";
    public string Name { get; set; } = "";
    public bool Joined { get; set; } = false;
    public bool Paused { get; set; } = false;
}
