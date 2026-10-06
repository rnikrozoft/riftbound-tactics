using System.Collections.Generic;
using System.Text.Json.Serialization;

[JsonSerializable(typeof(PlayerBootstrapResponse))]
[JsonSerializable(typeof(PlayerBootstrapRequest))]
[JsonSerializable(typeof(PlayerDeckSaveRequest))]
[JsonSerializable(typeof(CharacterPurchaseRequest))]
[JsonSerializable(typeof(CatalogCache))]
[JsonSerializable(typeof(GameConfiguration))]
[JsonSerializable(typeof(CombatVisualDocument))]
[JsonSerializable(typeof(EffectEntry[]))]
[JsonSerializable(typeof(string[]))]
[JsonSerializable(typeof(DeckDefinition))]
[JsonSerializable(typeof(List<DeckDefinition>))]
[JsonSerializable(typeof(CharacterCatalogData))]
[JsonSerializable(typeof(OnlineState))]
[JsonSerializable(typeof(List<ReplayGame>))]
[JsonSerializable(typeof(OnlineAction))]
[JsonSerializable(typeof(RoomRequest))]
[JsonSerializable(typeof(RoomResponse))]
[JsonSerializable(typeof(ServerClock))]
[JsonSerializable(typeof(OnlineError))]
internal partial class GameJsonContext : JsonSerializerContext { }
