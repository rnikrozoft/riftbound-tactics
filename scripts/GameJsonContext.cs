using System.Collections.Generic;
using System.Text.Json.Serialization;

[JsonSerializable(typeof(DamageSimulator.EffectEntry[]))]
[JsonSerializable(typeof(string[]))]
[JsonSerializable(typeof(HashSet<string>))]
internal partial class GameJsonContext : JsonSerializerContext { }
