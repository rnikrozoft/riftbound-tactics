using Godot;
using System;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

public sealed class CharacterStats
{
    [JsonPropertyName("stars")] public int Stars {get;set;}
    [JsonPropertyName("hp")] public int Hp {get;set;}
    [JsonPropertyName("attack")] public int Attack {get;set;}
    [JsonPropertyName("damage_min")] public int DamageMin {get;set;}
    [JsonPropertyName("damage_max")] public int DamageMax {get;set;}
    [JsonPropertyName("speed")] public int Speed {get;set;}
}
public sealed class CharacterDefinition
{
    [JsonPropertyName("kind")] public int Kind {get;set;}
    [JsonPropertyName("character_id")] public string Id {get;set;}="";
    [JsonPropertyName("name")] public string Name {get;set;}="";
    [JsonPropertyName("group")] public int Group {get;set;}
    [JsonPropertyName("cost")] public int Cost {get;set;}
    [JsonPropertyName("enabled")] public bool Enabled {get;set;}
    [JsonPropertyName("max_copies")] public int MaxCopies {get;set;}=4;
    [JsonPropertyName("role")] public string Role {get;set;}="";
    [JsonPropertyName("tags")] public string Tags {get;set;}="";
    [JsonPropertyName("description")] public string Description {get;set;}="";
    [JsonPropertyName("ability_name")] public string AbilityName {get;set;}="";
    [JsonPropertyName("ability_description")] public string AbilityDescription {get;set;}="";
    [JsonPropertyName("passive_name")] public string PassiveName {get;set;}="";
    [JsonPropertyName("passive_description")] public string PassiveDescription {get;set;}="";
    [JsonPropertyName("scene_path")] public string ScenePath {get;set;}="";
    [JsonPropertyName("enemy_scene_path")] public string EnemyScenePath {get;set;}="";
    [JsonPropertyName("card_art_path")] public string CardArtPath {get;set;}="";
    [JsonPropertyName("card_x")] public int CardX {get;set;}
    [JsonPropertyName("card_y")] public int CardY {get;set;}
    [JsonPropertyName("card_w")] public int CardW {get;set;}
    [JsonPropertyName("card_h")] public int CardH {get;set;}
    [JsonPropertyName("portrait_path")] public string PortraitPath {get;set;}="";
    [JsonPropertyName("portrait_x")] public int PortraitX {get;set;}
    [JsonPropertyName("portrait_y")] public int PortraitY {get;set;}
    [JsonPropertyName("portrait_w")] public int PortraitW {get;set;}
    [JsonPropertyName("portrait_h")] public int PortraitH {get;set;}
    [JsonPropertyName("stats")] public CharacterStats[] Stats {get;set;}=Array.Empty<CharacterStats>();
}
public sealed class CharacterCatalogData
{
    [JsonPropertyName("schema_version")] public int SchemaVersion {get;set;}
    [JsonPropertyName("group_names")] public string[] GroupNames {get;set;}={"Knight","Ranger","Mage","Guardian","Neutral"};
    [JsonPropertyName("characters")] public CharacterDefinition[] Characters {get;set;}=Array.Empty<CharacterDefinition>();
}
public static class CharacterData
{
    private static CharacterCatalogData _catalog=Parse(Godot.FileAccess.GetFileAsString("res://data/characters.json"));
    public static CharacterDefinition[] All=>_catalog.Characters;
    public static string[] Groups=>_catalog.GroupNames;
    public static CharacterDefinition Get(int kind)=>All[kind];
    public static CharacterStats Stats(int kind,int stars)=>Get(kind).Stats[Mathf.Clamp(stars,1,4)-1];
    private static CharacterCatalogData Parse(string json)
    {
        var data=JsonSerializer.Deserialize(json,GameJsonContext.Default.CharacterCatalogData)??throw new Exception("Character catalog is empty.");
        if(data.SchemaVersion!=1 || data.Characters.Length<1 || data.GroupNames.Length<5 || data.GroupNames[4]!="Neutral")throw new Exception("Unsupported character catalog.");
        for(int i=0;i<data.Characters.Length;i++){
            var c=data.Characters[i];if(c.Kind!=i || c.Stats.Length!=4 || c.Group<0 || c.Group>=data.GroupNames.Length)throw new Exception("Character catalog IDs, groups or stats are invalid.");
            foreach(string path in new[]{c.ScenePath,c.EnemyScenePath,c.CardArtPath,c.PortraitPath}.Distinct())
                if(!path.StartsWith("res://") || !ResourceLoader.Exists(path))throw new Exception($"Missing character asset: {path}. Update the game assets first.");
        }
        return data;
    }
    public static async Task Refresh(NakamaConnection connection)
    {
        // Catalog grows with asset packs; use HTTP rather than a realtime socket frame.
        var response=await connection.Client.RpcAsync(connection.Session,"character_catalog","{}");_catalog=Parse(response.Payload);
    }
}
