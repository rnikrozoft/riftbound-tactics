using Godot;
using System;
using System.Linq;
using System.Threading.Tasks;
using System.Text.Json;

public partial class DeckIntegrationRunner : Node
{
    private int _checks;
    private void Check(bool value,string message){_checks++;if(!value)throw new Exception(message);}
    private async Task Frames(int count=3){for(int i=0;i<count;i++)await ToSignal(GetTree(),SceneTree.SignalName.ProcessFrame);}
    private async Task Wait(Func<bool> condition,string message)
    {
        ulong end=Time.GetTicksMsec()+20000;
        while(!condition()){if(Time.GetTicksMsec()>end)throw new TimeoutException(message);await Frames(1);}
    }
    private async Task Capture(string name){await Frames();GetViewport().GetTexture().GetImage().SavePng("res://tests/"+name+".png");}
    private static T[] Descendants<T>(Node root) where T:Node => root.GetChildren().SelectMany(n=>(n is T match?new[]{match}:Array.Empty<T>()).Concat(Descendants<T>(n))).ToArray();
    private void Mouse(Vector2 point,bool pressed,bool motion)
    {
        DisplayServer.WarpMouse((Vector2I)point);
        InputEvent input=motion?new InputEventMouseMotion {Position=point,GlobalPosition=point,Relative=new(30,30),ButtonMask=pressed?MouseButtonMask.Left:0}:new InputEventMouseButton {Position=point,GlobalPosition=point,ButtonIndex=MouseButton.Left,Pressed=pressed};
        GetViewport().PushInput(input,true);
    }
    private async Task Drag(Vector2 start,Vector2 end)
    {
        Mouse(start,false,true);await Frames(1);Mouse(start,true,false);await Frames(1);
        Mouse(start+new Vector2(25,25),true,true);await Frames(1);Mouse(end,true,true);await Frames(1);
        Check(GetViewport().GuiIsDragging(),"native deck drag starts");Mouse(end,false,false);await Frames(3);
    }
    public override void _Ready()=>Callable.From(Run).CallDeferred();
    private async void Run()
    {
        try
        {
            DeckStore.Load();var starter=DeckDefinition.Starter();
            Check(starter.Validate()==""&&starter.Cards.Count==40&&starter.Cards.Sum(c=>c.Copies)==160,"40 kinds / 160 copies");
            var invalid=starter.Clone();invalid.Cards[0].Copies=5;Check(invalid.Validate()!="","invalid copies rejected");
            invalid=starter.Clone();invalid.Cards[0].Kind=invalid.Cards[1].Kind;Check(invalid.Validate()!="","duplicate slot rejected");
            // Verify persistence while restoring the user's files and in-memory collection afterward.
            string[] paths={"user://decks.json","user://selected_deck.txt"};
            byte[]?[] backup=paths.Select(path=>Godot.FileAccess.FileExists(path)?Godot.FileAccess.GetFileAsBytes(path):null).ToArray();
            var savedDecks=DeckStore.Decks.ToArray();string savedSelection=DeckStore.SelectedId;
            try
            {
                var persisted=starter.Clone();persisted.Name="Persistence test";persisted.Cards[0].Copies=1;
                DeckStore.Decks.Add(persisted);DeckStore.SelectedId=persisted.Id;
                Check(DeckStore.Save(),"deck file saved");
                using var file=Godot.FileAccess.Open(paths[0],Godot.FileAccess.ModeFlags.Read);
                var loaded=JsonSerializer.Deserialize(file.GetAsText(),GameJsonContext.Default.ListDeckDefinition)!;
                Check(loaded.Any(d=>d.Id==persisted.Id&&d.Cards[0].Copies==1&&d.Validate()==""),"deck definition round trips through user file");
                Check(Godot.FileAccess.GetFileAsString(paths[1])==persisted.Id,"selected deck persists");
            }
            finally
            {
                DeckStore.Decks.Clear();DeckStore.Decks.AddRange(savedDecks);DeckStore.SelectedId=savedSelection;
                for(int i=0;i<paths.Length;i++)
                    if(backup[i]!=null){using var file=Godot.FileAccess.Open(paths[i],Godot.FileAccess.ModeFlags.Write);file.StoreBuffer(backup[i]!);}
                    else DirAccess.RemoveAbsolute(ProjectSettings.GlobalizePath(paths[i]));
            }
            DeckStore.EditId="";
            var editor=GD.Load<PackedScene>("res://scenes/deck_builder.tscn").Instantiate<DeckBuilder>();AddChild(editor);await Frames();
            editor.SetHero(0,3);Check(editor.Draft.Validate()==""&&editor.Draft.Heroes.SequenceEqual(new[]{3,1,2}),"hero replacement fills ten correct slots");
            var neutral=editor.Draft.Cards.First(c=>CardCatalog.Group(c.Kind)==4);int original=neutral.Kind;
            editor.SetGroup(4);
            int replacement=CardCatalog.Options(4,CardCatalog.Cost(original)).First(k=>!editor.Draft.Cards.Any(c=>c.Kind==k));
            Check(editor.ReplaceCard(original,replacement)&&editor.Draft.Validate()=="","explicit same-cost replacement");
            Check(!editor.ReplaceCard(replacement,30),"cross-hero replacement rejected");
            Check(editor.ChangeCopies(replacement,1)&&editor.Draft.Cards.First(c=>c.Kind==replacement).Copies==2,"increase copies");
            Check(editor.ChangeCopies(replacement,-1)&&!editor.ChangeCopies(replacement,-1),"one copy minimum without cycling");
            editor.Filter(4);Check(editor.VisibleCardCount==CardCatalog.Options(4,4).Count(),"cost filter limits neutral catalog");
            editor.Filter(0,"Warden");Check(editor.VisibleCardCount==5,"search matches card names");
            editor.Filter(0);editor.RemoveCard(replacement);Check(editor.Draft.Validate()!="","incomplete deck blocked");
            Check(editor.AddCard(replacement)&&editor.Draft.Validate()=="","fill missing slot with one copy");
            editor.SelectCard(replacement);
            editor.Filter(2);await Frames();
            int dragged=CardCatalog.Options(4,2).First(k=>!editor.Draft.Cards.Any(c=>c.Kind==k));
            int targetKind=editor.Draft.Cards.First(c=>CardCatalog.Group(c.Kind)==4&&CardCatalog.Cost(c.Kind)==2).Kind;
            var source=Descendants<DeckCatalogCard>(editor).First(c=>c.Kind==dragged);
            var target=Descendants<DeckCardDrop>(editor).First(c=>c.Kind==targetKind);
            await Drag(source.GetGlobalRect().GetCenter(),target.GetGlobalRect().GetCenter());
            Check(editor.Draft.Cards.Any(c=>c.Kind==dragged)&&!editor.Draft.Cards.Any(c=>c.Kind==targetKind)&&editor.Draft.Validate()=="","native drop replaces correct slot");
            editor.RemoveCard(dragged);await Frames();
            source=Descendants<DeckCatalogCard>(editor).First(c=>c.Kind==dragged);
            var empty=Descendants<DeckEmptyDrop>(editor).First(c=>c.Cost==2);
            await Drag(source.GetGlobalRect().GetCenter(),empty.GetGlobalRect().GetCenter());
            Check(editor.Draft.Validate()=="","native drop fills empty slot");
            editor.Filter(0);editor.SelectCard(dragged);
            await Capture("deck-builder");editor.QueueFree();await Frames();
            var lobby=GD.Load<PackedScene>("res://scenes/lobby.tscn").Instantiate<Lobby>();AddChild(lobby);await Frames();
            Check(DeckStore.Selected?.Validate()=="","default lobby deck valid");await Capture("deck-lobby");
            string selectedBefore=DeckStore.SelectedId;
            foreach(string menu in new[]{"Decks","Shop","Leaderboard","Achievements"}){lobby.Navigate(menu);await Frames();Check(lobby.ActiveMenu==menu&&DeckStore.SelectedId==selectedBefore,"menu navigation preserves selected deck");}
            await Capture("deck-main-menu");lobby.Navigate("Play");Check(lobby.ActiveMenu=="Play","return to play");lobby.QueueFree();await Frames();
            var deck=new DeckDefinition {Name="Monster expedition",Heroes=new[]{5,6,7}};
            foreach(int group in deck.Heroes.Append(4))for(int cost=2;cost<=6;cost++)
                foreach(int kind in CardCatalog.Options(group,cost).Take(2))deck.Cards.Add(new(){Kind=kind});
            foreach(var c in deck.Cards)c.Copies=1;
            Check(deck.Validate()=="","custom forty-card single-copy deck valid");
            BattleLaunch.Deck=deck;BattleLaunch.Create=true;BattleLaunch.Pending=true;
            var game=GD.Load<PackedScene>("res://scenes/main.tscn").Instantiate<Node2D>();AddChild(game);
            var net=game.GetNode<OnlineBattle>("OnlineBattle");var shop=game.GetNode<CardShop>("UI/SafeArea/Content/CardShop");
            await Wait(()=>net.State?.Phase=="waiting","lobby auto room creation");Check(net.State!.Players[0]!.UserId==net.UserId,"automatic room entry owns A");
            usingOpponent = new NakamaConnection();await usingOpponent.Connect();
            OnlineState? opponentState=null;usingOpponent.StateReceived+=s=>opponentState=s;
            await usingOpponent.EnterRoom(false,net.State.Code,starter);
            await Wait(()=>shop.Drafting&&opponentState?.Phase=="preparation","two different decks prepare");
            Check(net.State!.Players.Select(p=>p!.UserId).Distinct().Count()==2,"two authenticated players");
            Check(net.State.Players[0]!.Offers.All(c=>deck.Cards.Any(d=>d.Kind==c.Kind)),"creator offers from selected deck");
            Check(opponentState!.Players[1]!.Offers.All(c=>starter.Cards.Any(d=>d.Kind==c.Kind)),"joiner has independent selected deck");
            var card=shop.Offers.First(c=>CharacterData.All.Single(d=>d.Name==c.Name).Group!=4);net.SendAction("buy",card.Token);
            await Wait(()=>!shop.NetworkPending&&shop.Hand.Count==1,"purchase single copy");
            Check(shop.Hand[0].Name==CardCatalog.Name(net.State!.Players[0]!.Hand[0].Kind),"client catalog matches server IDs");
            int purchased=net.State.Players[0]!.Hand[0].Kind;
            // Wait for three deterministic round incomes before checking reroll exhaustion.
            for(int round=2;round<=3;round++)
            {
                net.SendAction("ready");await Wait(()=>!shop.NetworkPending,"ready");
                await usingOpponent.Send(new(){Type="ready",Round=round-1,Sequence=round*2});
                await Wait(()=>net.State?.Phase=="finished","empty battle finished");
                net.SendAction("next");await Wait(()=>!shop.NetworkPending,"next");
                await usingOpponent.Send(new(){Type="next",Round=round-1,Sequence=round*2+1});
                await Wait(()=>shop.Drafting&&shop.TurnNumber==round,"next preparation");
            }
            for(int i=0;i<3;i++){net.SendAction("reroll");await Wait(()=>!shop.NetworkPending,"reroll");Check(net.State!.Players[0]!.Offers.All(c=>c.Kind!=purchased&&deck.Cards.Any(d=>d.Kind==c.Kind)),"exhausted and unselected cards excluded");}
            await Capture("deck-battle");
            await usingOpponent.Close();game.QueueFree();await Frames();BattleLaunch.Deck=null;
            GD.Print($"DECK INTEGRATION PASS: {_checks} checks");GetTree().Quit();
        }
        catch(Exception e){if(usingOpponent!=null)await usingOpponent.Close();GD.PushError(e.ToString());GetTree().Quit(1);}
    }
    private NakamaConnection? usingOpponent;
}
