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
    [JsonPropertyName("card_back_id")] public string CardBackId { get; set; } = CardBackCatalog.DefaultId;
    [JsonPropertyName("id")] public string Id { get; set; } = Guid.NewGuid().ToString("N");
    [JsonPropertyName("name")] public string Name { get; set; } = "New deck";
    [JsonPropertyName("heroes")] public int[] Heroes { get; set; } = CardCatalog.HeroGroups.Take(3).ToArray();
    [JsonPropertyName("cards")] public List<DeckEntry> Cards { get; set; } = new();
    [JsonIgnore] public int MandatoryNeutralKind=>CharacterData.SupportKind;
    public DeckDefinition Clone() => new() { CardBackId=CardBackId, Id=Id, Name=Name, Heroes=(int[])Heroes.Clone(), Cards=Cards.Select(c=>new DeckEntry {Kind=c.Kind,Copies=c.Copies}).ToList() };
    public static DeckDefinition Starter()
    {
        var deck = new DeckDefinition { Name="Starter expedition" };
        foreach (int group in deck.Heroes)
            for (int cost=2;cost<=6;cost++)
                foreach (int kind in CardCatalog.Options(group,cost).Take(2)) deck.Cards.Add(new() {Kind=kind,Copies=CharacterData.Get(kind).MaxCopies});
        return deck;
    }
    public string Validate()
    {
        if(CardBackCatalog.Find(CardBackId)==null)return "Choose a valid card back.";
        if (string.IsNullOrWhiteSpace(Name) || Name.Length>40) return "Use a deck name of 1-40 characters.";
        if (Heroes==null || Heroes.Length!=3 || Heroes.Any(h=>!CardCatalog.HeroGroups.Contains(h)) || Heroes.Distinct().Count()!=3) return "Select three different groups.";
        if (Cards==null || Cards.Count!=30) return "Fill all 30 card slots.";
        if (Cards.Any(c=>c==null || c.Kind<0 || c.Kind>=CardCatalog.Count || c.Copies<1 || c.Copies>CharacterData.Get(c.Kind).MaxCopies || !CharacterData.Get(c.Kind).Enabled) || Cards.Select(c=>c.Kind).Distinct().Count()!=30) return "Each card needs 1-4 copies and a distinct slot.";
        var groups=Heroes.ToArray();
        if (Cards.Any(c=>!groups.Contains(CardCatalog.Group(c.Kind)))) return "A card belongs to an unselected group.";
        foreach(int group in groups) for(int cost=2;cost<=6;cost++)
            if(Cards.Count(c=>CardCatalog.Group(c.Kind)==group && CardCatalog.Cost(c.Kind)==cost)!=2) return "Each group needs two different cards at every cost (2-6).";
        return "";
    }
    public bool RepairRetiredHeroes()
    {
        if(Heroes==null || Cards==null)return false;
        var heroes=(int[])Heroes.Clone();
        var cards=Cards.Select(c=>new DeckEntry {Kind=c.Kind,Copies=c.Copies}).ToList();
        bool changed=cards.RemoveAll(c=>!Heroes.Contains(CardCatalog.Group(c.Kind)))>0;
        foreach(var card in cards)if(card.Copies>CharacterData.Get(card.Kind).MaxCopies){card.Copies=CharacterData.Get(card.Kind).MaxCopies;changed=true;}
        for(int i=0;i<heroes.Length;i++){
            int retired=heroes[i];
            if(CardCatalog.HeroGroups.Contains(retired))continue;
            var oldScene=CharacterData.All.FirstOrDefault(c=>c.Group==retired)?.ScenePath;
            int preferred=CharacterData.All.FirstOrDefault(c=>c.Enabled && c.ScenePath==oldScene)?.Group ?? -1;
            int replacement=CardCatalog.HeroGroups.Prepend(preferred).FirstOrDefault(g=>CardCatalog.HeroGroups.Contains(g) && !heroes.Contains(g),-1);
            if(replacement<0)return false;
            var entries=cards.Where(c=>CardCatalog.Group(c.Kind)==retired).ToArray();
            foreach(int cost in Enumerable.Range(2,5)){
                var old=entries.Where(c=>CardCatalog.Cost(c.Kind)==cost).ToArray();
                var options=CardCatalog.Options(replacement,cost).Take(old.Length).ToArray();
                if(options.Length!=old.Length)return false;
                for(int n=0;n<old.Length;n++){old[n].Kind=options[n];old[n].Copies=Math.Min(old[n].Copies,CharacterData.Get(options[n]).MaxCopies);}
            }
            heroes[i]=replacement;changed=true;
        }
        foreach(var card in cards.Where(c=>!CharacterData.Get(c.Kind).Enabled)){
            var old=CharacterData.Get(card.Kind);
            int replacement=CardCatalog.Options(old.Group,old.Cost).FirstOrDefault(k=>!cards.Any(c=>c.Kind==k),-1);
            if(replacement<0)continue;
            card.Kind=replacement;card.Copies=Math.Min(card.Copies,CharacterData.Get(replacement).MaxCopies);changed=true;
        }
        if(changed){Heroes=heroes;Cards=cards;}
        return changed;
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
    private static readonly Dictionary<string,string> FrameAssignments=LoadFrameAssignments();
    private static Dictionary<string,string> LoadFrameAssignments()
    {
        using var json=JsonDocument.Parse(Godot.FileAccess.GetFileAsString("res://assets/ui/cards/fageltomten/assignments.json"));
        var result=new Dictionary<string,string>(StringComparer.Ordinal);
        foreach(var entry in json.RootElement.GetProperty("assignments").EnumerateObject()){var path=entry.Value.GetString()!;if(!path.Contains("ClassicLightCard.png",StringComparison.Ordinal))result[entry.Name]=path;}
        return result;
    }
    public static AtlasTexture Art(int kind)
    {
        var card=CharacterData.Get(kind);
        // Cosmetic assignments belong to this build, even when a published server
        // catalog still contains the older shared brown frame.
        if(FrameAssignments.TryGetValue(card.Id,out string? assigned))
            return new(){Atlas=GD.Load<Texture2D>(assigned),Region=new Rect2(0,0,23,35)};
        bool approved=!card.CardArtPath.Contains("ClassicLightCard.png",StringComparison.Ordinal)&&(card.CardArtPath.StartsWith("res://assets/ui/cards/deco/",StringComparison.Ordinal)||card.CardArtPath.StartsWith("res://assets/ui/cards/fageltomten/",StringComparison.Ordinal));
        return new(){Atlas=GD.Load<Texture2D>(approved?card.CardArtPath:"res://assets/ui/cards/deco/DecoCard.png"),Region=approved?new Rect2(card.CardX,card.CardY,card.CardW,card.CardH):new Rect2(0,0,23,35)};
    }
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
        if(Decks.Count==0&&!Godot.FileAccess.FileExists("user://decks.json")) Decks.Add(DeckDefinition.Starter());
        bool migrated=false;
        foreach(var deck in Decks){migrated=deck.RepairRetiredHeroes()||migrated;}
        SelectedId=Decks.FirstOrDefault()?.Id??"";
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
    public static void ResetAccount(){_loaded=false;Decks=new();SelectedId="";EditId="";Error="";}
    public static void ApplyRemote(string userId,List<DeckDefinition> decks,string selected)
    {
        Decks=decks;SelectedId=selected;_loaded=true;Error="";
        // Account data is authoritative on Nakama. No shared deck file is written here.
    }
    public static bool Save()
    {
        if(GameAccount.Session!=null){Error="Save this deck to your account using online save.";return false;}
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
