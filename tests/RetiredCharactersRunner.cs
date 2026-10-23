using Godot;
using System;
using System.Linq;

public partial class RetiredCharactersRunner : Node
{
    public override void _Ready()=>Callable.From(Run).CallDeferred();
    private void Run()
    {
        try {
            if(CardCatalog.HeroGroups.Any(g=>g>=1&&g<=4)||CharacterData.All.Any(c=>c.Enabled&&(c.Group>=1&&c.Group<=4)))throw new Exception("Retired characters selectable");
            var starter=DeckDefinition.Starter();if(starter.Validate()!=""||!starter.Heroes.SequenceEqual(new[]{45,46,47}))throw new Exception("Invalid starter");
            var capped=DeckDefinition.Starter();capped.Cards[0].Copies=8;capped.Cards[1].Copies=2;
            if(capped.Validate()=="")throw new Exception("Eight-copy deck accepted");
            if(!capped.RepairRetiredHeroes() || capped.Cards[0].Copies!=4 || capped.Cards[1].Copies!=2 || capped.Validate()!="" || capped.RepairRetiredHeroes())throw new Exception("Copy limit migration failed");
            if(OS.GetCmdlineUserArgs().Contains("--copy-limit-only")){GD.Print("FOUR COPY LIMIT PASS: rejects eight, clamps to four, preserves smaller counts, valid deck, idempotent migration");GetTree().Quit();return;}
            var legacy=new DeckDefinition {Name="My deck",Heroes=new[]{0,1,2}};
            foreach(int group in legacy.Heroes.Append(4))foreach(int cost in Enumerable.Range(2,5))
                foreach(var c in CharacterData.All.Where(c=>c.Group==group&&c.Cost==cost).Take(2))legacy.Cards.Add(new(){Kind=c.Kind,Copies=2});
            if(!legacy.RepairRetiredHeroes()||legacy.Validate()!=""||legacy.Cards.Any(c=>c.Copies!=2)||legacy.RepairRetiredHeroes())throw new Exception("Legacy migration failed");
            var page=new CollectionShopPage();AddChild(page);page.Open(true);
            var filter=(OptionButton)page.FindChild("ShopGroupFilter",true,false);
            for(int i=0;i<filter.ItemCount;i++)if(filter.GetItemId(i)>=1&&filter.GetItemId(i)<=4)throw new Exception("Retired shop group visible");
            int index=filter.GetItemIndex(46);filter.EmitSignal(OptionButton.SignalName.ItemSelected,index);
            if(filter.GetSelectedId()!=46)throw new Exception("Filtered group index mapped incorrectly");
            page.Free();GD.Print("RETIRED CHARACTERS PASS: choices, starter, old decks, copies, idempotency, shop filters and group selection");GetTree().Quit();
        }catch(Exception e){GD.PushError(e.ToString());GetTree().Quit(1);}
    }
}
