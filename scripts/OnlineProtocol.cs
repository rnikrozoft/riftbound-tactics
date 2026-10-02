using System;
using System.Text.Json.Serialization;

public sealed class OnlineCard
{
    [JsonPropertyName("token")] public int Token { get; set; }
    [JsonPropertyName("kind")] public int Kind { get; set; }
}
public sealed class OnlineUnit
{
    [JsonPropertyName("token")] public int Token { get; set; }
    [JsonPropertyName("kind")] public int Kind { get; set; }
    [JsonPropertyName("slot")] public int Slot { get; set; }
    [JsonPropertyName("veteran")] public bool Veteran { get; set; }
}
public sealed class OnlinePlayer
{
    [JsonPropertyName("user_id")] public string UserId { get; set; } = "";
    [JsonPropertyName("team")] public string Team { get; set; } = "";
    [JsonPropertyName("connected")] public bool Connected { get; set; }
    [JsonPropertyName("ready")] public bool Ready { get; set; }
    [JsonPropertyName("offers")] public OnlineCard[] Offers { get; set; } = Array.Empty<OnlineCard>();
    [JsonPropertyName("hand")] public OnlineCard[] Hand { get; set; } = Array.Empty<OnlineCard>();
    [JsonPropertyName("hand_count")] public int HandCount { get; set; }
    [JsonPropertyName("units")] public OnlineUnit[] Units { get; set; } = Array.Empty<OnlineUnit>();
}
public sealed class OnlineState
{
    [JsonPropertyName("code")] public string Code { get; set; } = "";
    [JsonPropertyName("phase")] public string Phase { get; set; } = "";
    [JsonPropertyName("round")] public int Round { get; set; }
    [JsonPropertyName("deadline_ms")] public long DeadlineMs { get; set; }
    [JsonPropertyName("server_ms")] public long ServerMs { get; set; }
    [JsonPropertyName("revision")] public long Revision { get; set; }
    [JsonPropertyName("ack_sequence")] public long AckSequence { get; set; }
    [JsonPropertyName("players")] public OnlinePlayer?[] Players { get; set; } = Array.Empty<OnlinePlayer?>();
    [JsonPropertyName("battle")] public OnlinePlan? Battle { get; set; }
}
public sealed class OnlineCombatUnit
{
    [JsonPropertyName("id")] public string Id { get; set; } = "";
    [JsonPropertyName("team")] public string Team { get; set; } = "";
    [JsonPropertyName("token")] public int Token { get; set; }
    [JsonPropertyName("kind")] public int Kind { get; set; }
    [JsonPropertyName("slot")] public int Slot { get; set; }
    [JsonPropertyName("max_hp")] public int MaxHp { get; set; } = 100;
}
public sealed class OnlineCombatEvent
{
    [JsonPropertyName("index")] public int Index { get; set; }
    [JsonPropertyName("attacker")] public string Attacker { get; set; } = "";
    [JsonPropertyName("target")] public string Target { get; set; } = "";
    [JsonPropertyName("damage")] public int Damage { get; set; }
    [JsonPropertyName("target_hp")] public int TargetHp { get; set; }
    [JsonPropertyName("dead")] public bool Dead { get; set; }
    [JsonPropertyName("at_ms")] public long AtMs { get; set; }
}
public sealed class OnlinePlan
{
    [JsonPropertyName("round")] public int Round { get; set; }
    [JsonPropertyName("start_ms")] public long StartMs { get; set; }
    [JsonPropertyName("end_ms")] public long EndMs { get; set; }
    [JsonPropertyName("winner")] public string Winner { get; set; } = "";
    [JsonPropertyName("units")] public OnlineCombatUnit[] Units { get; set; } = Array.Empty<OnlineCombatUnit>();
    [JsonPropertyName("events")] public OnlineCombatEvent[] Events { get; set; } = Array.Empty<OnlineCombatEvent>();
}
public sealed class OnlineAction
{
    [JsonPropertyName("type")] public string Type { get; set; } = "";
    [JsonPropertyName("token")] public int Token { get; set; }
    [JsonPropertyName("slot")] public int Slot { get; set; }
    [JsonPropertyName("round")] public int Round { get; set; }
    [JsonPropertyName("sequence")] public long Sequence { get; set; }
}
public sealed class RoomResponse
{
    [JsonPropertyName("code")] public string Code { get; set; } = "";
    [JsonPropertyName("match_id")] public string MatchId { get; set; } = "";
}
public sealed class RoomRequest
{
    [JsonPropertyName("code")] public string Code { get; set; } = "";
}
public sealed class ServerClock
{
    [JsonPropertyName("server_ms")] public long ServerMs { get; set; }
}
public sealed class OnlineError
{
    [JsonPropertyName("message")] public string Message { get; set; } = "";
    [JsonPropertyName("sequence")] public long Sequence { get; set; }
}
