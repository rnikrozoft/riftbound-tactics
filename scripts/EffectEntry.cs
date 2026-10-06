using System;
using System.Text.Json.Serialization;

// Shared effect data for combat playback and the combat simulator.
public sealed class EffectEntry
{
    [JsonPropertyName("name")] public string Name { get; set; } = "";
    [JsonPropertyName("category")] public string Category { get; set; } = "";
    [JsonPropertyName("texture")] public string Texture { get; set; } = "";
    [JsonPropertyName("frames")] public int[][] Frames { get; set; } = Array.Empty<int[]>();
}
