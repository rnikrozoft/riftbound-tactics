using Godot;
using System;
using System.Linq;

public partial class TinyPackOnlineRunner : Node
{
    public override void _Ready()=>Callable.From(Run).CallDeferred();
    private async void Run()
    {
        NakamaConnection? connection=null;
        try {
            // Use an isolated test account so the player's active login stays connected.
            connection=new NakamaConnection(GameAccount.ServerHost);
            await connection.Connect(realtime:false);
            await PlayerInventory.Load(connection,forceConfiguration:true);
            if(CharacterData.All.Any(c=>c.MaxCopies>4))throw new Exception("Live catalog exceeds four copies");
            var editor=GD.Load<PackedScene>("res://scenes/deck_builder.tscn").Instantiate<DeckBuilder>();AddChild(editor);
            editor.SetHero(0,48);
            foreach(var pair in new[]{("roster_bat",47),("roster_necromancer",48)}) {
                var card=CharacterData.All.Single(c=>c.Id==pair.Item1);
                if(!card.Enabled||card.Group!=pair.Item2||!PlayerInventory.State!.Profile.OwnedCharacters.Contains(card.Id))throw new Exception("New card not granted by live bootstrap: "+card.Id);
                if(!editor.Draft.Heroes.Contains(card.Group))editor.SetHero(0,card.Group);
                editor.SetGroup(card.Group);
                if(editor.VisibleCardCount!=CharacterData.All.Count(c=>c.Enabled&&c.Group==card.Group))throw new Exception("Live Deck Builder missing eleventh card: "+card.Id);
                if(!editor.FindChildren("*","TextureButton",true,false).OfType<DeckCatalogCard>().Any(c=>c.Kind==card.Kind))throw new Exception("Missing live card widget: "+card.Id);
                var old=editor.Draft.Cards.First(c=>CardCatalog.Group(c.Kind)==card.Group&&CardCatalog.Cost(c.Kind)==card.Cost);
                if(!editor.ReplaceCard(old.Kind,card.Kind)||editor.Draft.Validate()!="")throw new Exception("Live card cannot form valid deck: "+card.Id);
                editor.Draft.Cards.First(c=>c.Kind==card.Kind).Copies=4;
                if(editor.ChangeCopies(card.Kind,1))throw new Exception("Deck Builder allowed fifth copy");
                GD.Print($"LIVE TINY PACK: {card.Name}, group {CardCatalog.Groups[card.Group]}, visible {editor.VisibleCardCount}, owned and usable");
            }
            if(CharacterData.All.Single(c=>c.Id=="roster_burgundy_rogue").Enabled)throw new Exception("Retired Rogue remains enabled");
            editor.SetHero(0,45);editor.SetGroup(45);editor.Filter(3,"Burgundy");
            if(editor.VisibleCardCount!=0)throw new Exception("Retired Rogue remains in Deck Builder");
            GD.Print("ROGUE REMOVAL PASS: live catalog disabled and no Deck Builder card");
            if(CharacterData.All.Single(c=>c.Id=="roster_teal_scout").Enabled)throw new Exception("Teal Scout remains enabled");
            editor.Filter(0,"Teal Scout");
            if(editor.VisibleCardCount!=0)throw new Exception("Teal Scout remains selectable");
            GD.Print("TEAL SCOUT REMOVAL PASS: live catalog disabled and no selectable card");
            editor.QueueFree();await connection.Close();GD.Print("TINY PACK ONLINE PASS: live catalog, ownership, all new Deck Builder widgets and valid replacement decks");GetTree().Quit();
        }catch(Exception e){if(connection!=null)await connection.Close();GD.PushError(e.ToString());GetTree().Quit(1);}
    }
}
