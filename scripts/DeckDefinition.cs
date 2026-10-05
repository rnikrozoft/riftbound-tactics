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
                foreach (int kind in CardCatalog.Options(group,cost).Take(2)) deck.Cards.Add(new() {Kind=kind,Copies=CharacterData.Get(kind).MaxCopies});
        return deck;
    }
    public string Validate()
    {
        if (string.IsNullOrWhiteSpace(Name) || Name.Length>40) return "Use a deck name of 1-40 characters.";
        if (Heroes==null || Heroes.Length!=3 || Heroes.Any(h=>!CardCatalog.HeroGroups.Contains(h)) || Heroes.Distinct().Count()!=3) return "Select three different characters.";
        if (Cards==null || Cards.Count!=40) return "Fill all 40 card slots.";
        if (Cards.Any(c=>c==null || c.Kind<0 || c.Kind>=CardCatalog.Count || c.Copies<1 || c.Copies>CharacterData.Get(c.Kind).MaxCopies || !CharacterData.Get(c.Kind).Enabled) || Cards.Select(c=>c.Kind).Distinct().Count()!=40) return "Each card needs 1-4 copies and a distinct slot.";
        var groups=Heroes.Append(4).ToArray();
        if (Cards.Any(c=>!groups.Contains(CardCatalog.Group(c.Kind)))) return "A card belongs to an unselected hero.";
        foreach(int group in groups) for(int cost=2;cost<=6;cost++)
            if(Cards.Count(c=>CardCatalog.Group(c.Kind)==group && CardCatalog.Cost(c.Kind)==cost)!=2) return "Each group needs two different cards at every cost (2-6).";
        return "";
    }
    public bool RepairLegacyNeutralCards()
    {
        if(Heroes==null || Cards==null)return false;
        var replacements=new List<(DeckEntry Entry,int Kind)>();
        var occupied=Cards.Select(c=>c.Kind).ToHashSet();
        foreach(var entry in Cards.Where(c=>c.Kind>=60 && c.Kind<=62)){
            if(Heroes.Contains(CardCatalog.Group(entry.Kind)))continue;
            int replacement=CardCatalog.Options(4,CardCatalog.Cost(entry.Kind)).FirstOrDefault(k=>!occupied.Contains(k),-1);
            if(replacement<0)return false;
            replacements.Add((entry,replacement));occupied.Add(replacement);
        }
        foreach(var replacement in replacements){replacement.Entry.Kind=replacement.Kind;replacement.Entry.Copies=Math.Min(replacement.Entry.Copies,CharacterData.Get(replacement.Kind).MaxCopies);}
        return replacements.Count>0;
    }
}
public static class CardCatalog
{
    public static int Count=>CharacterData.All.Length;
    public static string[] Groups=>CharacterData.Groups;
    public static IEnumerable<int> HeroGroups=>Enumerable.Range(0,Groups.Length).Where(g=>g!=4 && CharacterData.All.Any(c=>c.Enabled && c.Group==g));
    public static int Group(int kind)=>CharacterData.Get(kind).Group;
    public static int Cost(int kind)=>CharacterData.Get(kind).Cost;
    public static string Name(int kind)=>CharacterData.Get(kind).Name;
    public static IEnumerable<int> Options(int group,int cost)=>CharacterData.All.Where(c=>c.Enabled && c.Group==group && c.Cost==cost).Select(c=>c.Kind);
    public static AtlasTexture Art(int kind){var c=CharacterData.Get(kind);return new(){Atlas=GD.Load<Texture2D>(c.CardArtPath),Region=new Rect2(c.CardX,c.CardY,c.CardW,c.CardH)};}
    public static AtlasTexture Portrait(int kind){var c=CharacterData.Get(kind);return CharacterVisual.Portrait(GD.Load<Texture2D>(c.PortraitPath),new Rect2(c.PortraitX,c.PortraitY,c.PortraitW,c.PortraitH));}

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
        bool migrated=false;
        foreach(var deck in Decks)migrated=deck.RepairLegacyNeutralCards()||migrated;
        SelectedId=Decks[0].Id;
        if(Godot.FileAccess.FileExists("user://selected_deck.txt"))
        {
            using var selection=Godot.FileAccess.Open("user://selected_deck.txt",Godot.FileAccess.ModeFlags.Read);
            string id=selection.GetAsText();if(Decks.Any(d=>d.Id==id))SelectedId=id;
        }
        if(migrated){
            if(!Godot.FileAccess.FileExists("user://decks-before-character-groups.json")){
                using var backup=Godot.FileAccess.Open("user://decks-before-character-groups.json",Godot.FileAccess.ModeFlags.Write);
                backup?.StoreBuffer(Godot.FileAccess.GetFileAsBytes("user://decks.json"));
            }
            Save();
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
