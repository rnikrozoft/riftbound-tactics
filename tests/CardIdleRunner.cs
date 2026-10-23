using Godot;
using System;
using System.Linq;

public partial class CardIdleRunner : Node
{
    public override void _Ready()=>Callable.From(Run).CallDeferred();
    private async void Run()
    {
        try {
            foreach(string retiredId in new[]{"roster_burgundy_rogue","roster_teal_scout"}){
                var retiredDeck=DeckDefinition.Starter();
                var retired=CharacterData.All.Single(c=>c.Id==retiredId);
                retiredDeck.Cards.First(c=>CardCatalog.Group(c.Kind)==retired.Group&&CardCatalog.Cost(c.Kind)==retired.Cost).Kind=retired.Kind;
                if(retired.Enabled||!retiredDeck.RepairRetiredHeroes()||retiredDeck.Cards.Any(c=>c.Kind==retired.Kind)||retiredDeck.Validate()!=""||retiredDeck.RepairRetiredHeroes())throw new Exception("Retired deck migration failed: "+retiredId);
            }
            foreach(string id in new[]{"roster_bat","roster_necromancer"}) {
                var definition=CharacterData.All.Single(c=>c.Id==id);
                var deck=DeckDefinition.Starter();
                deck.Heroes=new[]{45,47,48};
                deck.Cards.Clear();
                foreach(int group in deck.Heroes) foreach(int cost in Enumerable.Range(2,5))
                    foreach(int kind in CardCatalog.Options(group,cost).Take(2))deck.Cards.Add(new(){Kind=kind,Copies=1});
                deck.Cards.First(c=>CardCatalog.Group(c.Kind)==definition.Group && CardCatalog.Cost(c.Kind)==definition.Cost).Kind=definition.Kind;
                if(deck.Validate()!="")throw new Exception("New asset cannot replace a deck card: "+id);
                var actor=GD.Load<PackedScene>(definition.ScenePath).Instantiate<BattleUnit>();
                var frames=actor.GetNode<AnimatedSprite2D>("AnimatedSprite2D").SpriteFrames;
                foreach(string animation in new[]{"idle","walk","attack","hit","die"})
                    if(!frames.HasAnimation(animation)||frames.GetFrameCount(animation)<2)throw new Exception("Missing animation: "+id+" "+animation);
                actor.Free();
            }
            var cards=CharacterData.All.Where(c=>c.Enabled).Select(c=>{
                var card=DeckMenuUi.Art(c.Kind,100,128);AddChild(card);card.Size=new(100,128);return (c.Kind,Card:card,Portrait:card.GetNode<CardIdlePortrait>("Fighter"));
            }).ToArray();
            if(cards.Length!=CharacterData.All.Count(c=>c.Enabled) || cards.Any(c=>c.Portrait.IdleFrameCount<2 || c.Portrait.MouseFilter!=Control.MouseFilterEnum.Ignore))throw new Exception("Every active card requires an animated, non-intercepting portrait");
            var first=cards.Select(c=>c.Portrait.Texture).ToArray();
            double until=Time.GetTicksMsec()+600;
            var moved=new bool[cards.Length];
            while(Time.GetTicksMsec()<until){
                await ToSignal(GetTree(),SceneTree.SignalName.ProcessFrame);
                for(int i=0;i<cards.Length;i++)moved[i]|=cards[i].Portrait.Texture!=first[i];
            }
            if(moved.Any(x=>!x))throw new Exception("An idle portrait did not advance");
            foreach(var c in cards){
                var frame=c.Portrait.CurrentFrame;GameUi.DecorateCard(c.Card,c.Kind);
                if(c.Portrait.CurrentFrame!=frame || c.Card.GetChildren().OfType<CardIdlePortrait>().Count()!=1)throw new Exception("Refreshing a card restarted or duplicated idle");
                c.Card.Hide();
            }
            var paused=cards.Select(c=>c.Portrait.CurrentFrame).ToArray();
            until=Time.GetTicksMsec()+200;while(Time.GetTicksMsec()<until)await ToSignal(GetTree(),SceneTree.SignalName.ProcessFrame);
            if(cards.Where((c,i)=>c.Portrait.CurrentFrame!=paused[i]).Any())throw new Exception("Hidden portraits continued processing");
            foreach(var c in cards)c.Card.Free();
            GD.Print("CARD IDLE PASS: all active character assets animate, card refresh preserves playback, hidden cards pause and mouse input passes through");GetTree().Quit();
        }catch(Exception e){GD.PushError(e.ToString());GetTree().Quit(1);}
    }
}
