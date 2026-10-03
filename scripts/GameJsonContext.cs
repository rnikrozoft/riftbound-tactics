using System.Collections.Generic;
using System.Text.Json.Serialization;

[JsonSerializable(typeof(DamageSimulator.EffectEntry[]))]
[JsonSerializable(typeof(string[]))]
[JsonSerializable(typeof(HashSet<string>))]
[JsonSerializable(typeof(DeckDefinition))]
[JsonSerializable(typeof(List<DeckDefinition>))]
[JsonSerializable(typeof(OnlineState))]
[JsonSerializable(typeof(OnlineAction))]
[JsonSerializable(typeof(RoomRequest))]
[JsonSerializable(typeof(RoomResponse))]
[JsonSerializable(typeof(ServerClock))]
[JsonSerializable(typeof(OnlineError))]
internal partial class GameJsonContext : JsonSerializerContext { }
