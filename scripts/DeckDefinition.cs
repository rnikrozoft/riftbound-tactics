using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using Godot;

public sealed class DeckEntry
{
    [JsonPropertyName("kind")] public int Kind { get; set; }
    [JsonPropertyName("copies")] public int Copies { get; set; } = 4;
}
public sealed class DeckDefinition
{
    [JsonPropertyName("id")] public string Id { get; set; } = Guid.NewGuid().ToString("N");
    [JsonPropertyName("name")] public string Name { get; set; } = "New deck";
    [JsonPropertyName("heroes")] public int[] Heroes { get; set; } = {0,1,2};
    [JsonPropertyName("cards")] public List<DeckEntry> Cards { get; set; } = new();
    public DeckDefinition Clone() => new() { Id=Id, Name=Name, Heroes=(int[])Heroes.Clone(), Cards=Cards.Select(c=>new DeckEntry {Kind=c.Kind,Copies=c.Copies}).ToList() };
    public static DeckDefinition Starter()
    {
        var deck = new DeckDefinition { Name="Starter expedition" };
        foreach (int group in deck.Heroes.Append(4))
            for (int cost=2;cost<=6;cost++)
                foreach (int kind in CardCatalog.Options(group,cost).Take(2)) deck.Cards.Add(new() {Kind=kind});
        return deck;
    }
    public string Validate()
    {
        if (string.IsNullOrWhiteSpace(Name) || Name.Length>40) return "Use a deck name of 1-40 characters.";
        if (Heroes==null || Heroes.Length!=3 || Heroes.Any(h=>h<0 || h>3) || Heroes.Distinct().Count()!=3) return "Select three different heroes.";
        if (Cards==null || Cards.Count!=40) return "Fill all 40 card slots.";
        if (Cards.Any(c=>c==null || c.Kind<0 || c.Kind>=CardCatalog.Count || c.Copies<1 || c.Copies>4) || Cards.Select(c=>c.Kind).Distinct().Count()!=40) return "Each card needs 1-4 copies and a distinct slot.";
        var groups=Heroes.Append(4).ToArray();
        if (Cards.Any(c=>!groups.Contains(CardCatalog.Group(c.Kind)))) return "A card belongs to an unselected hero.";
        foreach(int group in groups) for(int cost=2;cost<=6;cost++)
            if(Cards.Count(c=>CardCatalog.Group(c.Kind)==group && CardCatalog.Cost(c.Kind)==cost)!=2) return "Each group needs two different cards at every cost (2-6).";
        return "";
    }
}
public static class CardCatalog
{
    public const int Count=60;
    public static readonly string[] Groups={"Knight","Ranger","Mage","Guardian","Neutral"};
    public static int Group(int kind) => kind<30 ? kind%6/2 : kind<40 ? 3 : 4;
    public static int Cost(int kind) => kind<30 ? 2+kind/6 : kind<40 ? 2+(kind-30)/2 : 2+(kind-40)/4;
    public static int Variant(int kind) => kind<30 ? kind%2 : kind<40 ? (kind-30)%2 : (kind-40)%4;
    public static string Name(int kind) => $"{Groups[Group(kind)]} {new[]{"Scout","Guard","Sage","Warden"}[Variant(kind)]} {Cost(kind)}";
    public static IEnumerable<int> Options(int group,int cost) => Enumerable.Range(0,Count).Where(k=>Group(k)==group && Cost(k)==cost);
    public static AtlasTexture Art(int kind)
    {
        int[] origins={14,133,250,367,482,611};
        return new() { Atlas=GD.Load<Texture2D>("res://assets/cards/pixelCardAssest_V01.png"), Region=new Rect2(origins[kind%6],4,100,128) };
    }
}
public static class DeckStore
{
    public static List<DeckDefinition> Decks { get; private set; } = new();
    public static string SelectedId { get; set; } = "";
    public static string EditId { get; set; } = "";
    public static string Error { get; private set; } = "";
    private static bool _loaded;
    public static void Load()
    {
        if(_loaded) return;
        _loaded=true;
        if(Godot.FileAccess.FileExists("user://decks.json"))
        {
            try { using var file=Godot.FileAccess.Open("user://decks.json",Godot.FileAccess.ModeFlags.Read); Decks=JsonSerializer.Deserialize(file.GetAsText(),GameJsonContext.Default.ListDeckDefinition) ?? new(); }
            catch(Exception e) { Error="Saved decks could not be read: "+e.Message; }
        }
        if(Decks.Count==0) Decks.Add(DeckDefinition.Starter());
        SelectedId=Decks[0].Id;
        if(Godot.FileAccess.FileExists("user://selected_deck.txt"))
        {
            using var selection=Godot.FileAccess.Open("user://selected_deck.txt",Godot.FileAccess.ModeFlags.Read);
            string id=selection.GetAsText();if(Decks.Any(d=>d.Id==id))SelectedId=id;
        }
    }
    public static bool Save()
    {
        try
        {
            using(var file=Godot.FileAccess.Open("user://decks.json.tmp",Godot.FileAccess.ModeFlags.Write))
            { if(file==null) throw new Exception(Godot.FileAccess.GetOpenError().ToString()); file.StoreString(JsonSerializer.Serialize(Decks,GameJsonContext.Default.ListDeckDefinition)); }
            var result=DirAccess.RenameAbsolute(ProjectSettings.GlobalizePath("user://decks.json.tmp"),ProjectSettings.GlobalizePath("user://decks.json"));
            if(result!=Godot.Error.Ok) throw new Exception(result.ToString());
            using(var selected=Godot.FileAccess.Open("user://selected_deck.txt",Godot.FileAccess.ModeFlags.Write))
            {if(selected==null) throw new Exception(Godot.FileAccess.GetOpenError().ToString());selected.StoreString(SelectedId);}
            Error=""; return true;
        }
        catch(Exception e) { Error="Could not save decks: "+e.Message; return false; }
    }
    public static DeckDefinition? Selected => Decks.Find(d=>d.Id==SelectedId);
}
public static class BattleLaunch
{
    public static bool Matchmaking { get; set; }
    public static DeckDefinition? Deck { get; set; }
    public static bool Pending { get; set; }
    public static bool Create { get; set; }
    public static string Code { get; set; } = "";
}
