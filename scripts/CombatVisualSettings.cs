using Godot;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;

public sealed class CombatVisualChoice
{
    [JsonPropertyName("character")] public string Character {get;set;}="";
    [JsonPropertyName("source_animation")] public string SourceAnimation {get;set;}="attack01";
    [JsonPropertyName("animation")] public string Animation {get;set;}="attack01";
    [JsonPropertyName("effect")] public string Effect {get;set;}="";
    [JsonPropertyName("offset_x")] public float OffsetX {get;set;}
    [JsonPropertyName("offset_y")] public float OffsetY {get;set;}
    [JsonPropertyName("scale")] public float Scale {get;set;}=1;
    [JsonPropertyName("anchor")] public string Anchor {get;set;}="auto";
    public CombatVisualChoice Copy()=>(CombatVisualChoice)MemberwiseClone();
}
public sealed class CombatVisualDocument
{
    [JsonPropertyName("schema_version")] public int SchemaVersion {get;set;}=1;
    [JsonPropertyName("entries")] public List<CombatVisualChoice> Entries {get;set;}=new();
}
// Device overrides layer over packaged defaults. No damage or ability rules are changed.
public static class CombatVisualSettings
{
    public const string SavedPath="user://combat_visuals.json";
    internal static string StoragePath {get;set;}=SavedPath;
    public const string DefaultPath="res://data/combat_visuals.json";
    private static CombatVisualDocument? _defaults,_saved;
    private static HashSet<string>? _effects;
    public static string Slug(int kind)=>CharacterData.Get(kind).ScenePath.GetFile().GetBaseName();
    private static string Key(CombatVisualChoice choice)=>choice.Character+":"+choice.SourceAnimation;
    public static void Reload(){_defaults=null;_saved=null;}
    public static bool Valid(CombatVisualChoice c)
    {
        _effects??=(JsonSerializer.Deserialize(Godot.FileAccess.GetFileAsString("res://data/effect_catalog.json"),GameJsonContext.Default.EffectEntryArray)??Array.Empty<EffectEntry>()).Select(e=>e.Name).ToHashSet();
        return CharacterData.All.Any(d=>Slug(d.Kind)==c.Character)&&
            new[]{"attack01","attack02","attack03"}.Contains(c.SourceAnimation)&&
            new[]{"attack01","attack02","attack03"}.Contains(c.Animation)&&
            (c.Effect==""||_effects.Contains(c.Effect))&&
            float.IsFinite(c.Scale)&&c.Scale>=.1f&&c.Scale<=5&&
            float.IsFinite(c.OffsetX)&&Math.Abs(c.OffsetX)<=200&&float.IsFinite(c.OffsetY)&&Math.Abs(c.OffsetY)<=200&&
            new[]{"auto","body","feet"}.Contains(c.Anchor);
    }
    private static CombatVisualDocument Read(string path)
    {
        if(!Godot.FileAccess.FileExists(path))return new();
        try {
            var doc=JsonSerializer.Deserialize(Godot.FileAccess.GetFileAsString(path),GameJsonContext.Default.CombatVisualDocument);
            if(doc==null||doc.SchemaVersion!=1||doc.Entries==null)throw new JsonException("Unsupported combat visuals document");
            doc.Entries=doc.Entries.Where(c=>c!=null&&Valid(c)).GroupBy(Key).Select(g=>g.Last()).ToList();return doc;
        }catch(Exception e) when(e is JsonException or IOException or ArgumentException){GD.PushWarning("Unable to load combat visuals: "+e.Message);return new();}
    }
    private static void Ensure(){_defaults??=Read(DefaultPath);_saved??=Read(StoragePath);}
    public static CombatVisualChoice? Find(int kind,string animation)
    {
        Ensure();string character=Slug(kind);
        return (_saved!.Entries.LastOrDefault(c=>c.Character==character&&c.SourceAnimation==animation)??
            _defaults!.Entries.LastOrDefault(c=>c.Character==character&&c.SourceAnimation==animation))?.Copy();
    }
    public static CombatVisualDocument Combined()
    {
        Ensure();return new() {Entries=_defaults!.Entries.Concat(_saved!.Entries).GroupBy(Key).Select(g=>g.Last().Copy()).ToList()};
    }
    public static string Serialize(CombatVisualDocument doc)=>JsonSerializer.Serialize(doc,GameJsonContext.Default.CombatVisualDocument);
    public static bool Save(CombatVisualChoice choice,out string error)
    {
        error="";if(!Valid(choice)){error="ค่าที่เลือกไม่ถูกต้อง";return false;}
        Ensure();var next=new CombatVisualDocument {Entries=_saved!.Entries.Where(c=>Key(c)!=Key(choice)).Select(c=>c.Copy()).Append(choice.Copy()).ToList()};
        try {
            AtomicWrite(ProjectSettings.GlobalizePath(StoragePath),Serialize(next));_saved=next;return true;
        }catch(Exception e) when(e is IOException or UnauthorizedAccessException){error=e.Message;return false;}
    }
    public static bool Reset(int kind,string animation,out string error)
    {
        Ensure();error="";var next=new CombatVisualDocument {Entries=_saved!.Entries.Where(c=>c.Character!=Slug(kind)||c.SourceAnimation!=animation).Select(c=>c.Copy()).ToList()};
        try {AtomicWrite(ProjectSettings.GlobalizePath(StoragePath),Serialize(next));_saved=next;return true;}
        catch(Exception e) when(e is IOException or UnauthorizedAccessException){error=e.Message;return false;}
    }
    public static void AtomicWrite(string path,string json)
    {
        string temp=path+".tmp";
        try {File.WriteAllText(temp,json);File.Move(temp,path,true);}
        finally {if(File.Exists(temp))File.Delete(temp);}
    }
    public static string AnimationFor(int kind,string original,SpriteFrames frames)
    {
        var choice=Find(kind,original);
        return choice!=null&&frames.HasAnimation(choice.Animation)?choice.Animation:original;
    }
    public static int RemapFrame(int frame,int originalCount,int displayCount)=>
        Mathf.Clamp(Mathf.RoundToInt(frame*(displayCount-1f)/Math.Max(1,originalCount-1)),0,Math.Max(0,displayCount-1));
}
