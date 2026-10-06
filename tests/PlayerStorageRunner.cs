using Godot;
using System;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;

public partial class PlayerStorageRunner : Node
{
    private int _checks;
    private void Check(bool condition,string message){_checks++;if(!condition)throw new Exception(message);}
    private async Task Frames(int count=3){for(int i=0;i<count;i++)await ToSignal(GetTree(),SceneTree.SignalName.ProcessFrame);}
    private async Task Wait(Func<bool> condition,string message,int timeout=15000){ulong end=Time.GetTicksMsec()+(ulong)timeout;while(!condition()){if(Time.GetTicksMsec()>end)throw new Exception(message);await Frames(1);}}
    public override void _Ready()=>Callable.From(Run).CallDeferred();
    private async void Run()
    {
        NakamaConnection? a=null,b=null;
        try {
            GetTree().CurrentScene=null;PlayerInventory.Reset();GameAccount.RequiresLogin=false;
            a=new(deviceId:"rift-player-storage-ui-"+Guid.NewGuid().ToString("N"));await a.Connect(realtime:false);
            await PlayerInventory.Load(a);
            Check(PlayerInventory.Ready,"account bootstrap did not bind to this user");
            Check(PlayerInventory.AllUnlocked&&PlayerInventory.Coins==0,"temporary free access and explicit zero wallet");
            Check(DeckStore.Decks.Count==1&&DeckStore.Selected!.Cards.Count==40,"server starter deck missing");
            Check(PlayerInventory.State!.Profile.OwnedCharacters.Length==40,"starter ownership missing");
            string version=PlayerInventory.State.ConfigVersion;string profileVersion=PlayerInventory.State.ProfileVersion;
            await PlayerInventory.Load(a);
            Check(PlayerInventory.State.ConfigVersion==version&&PlayerInventory.State.ProfileVersion==profileVersion,"cache bootstrap overwrote collection");
            var lobby=GD.Load<PackedScene>("res://scenes/lobby.tscn").Instantiate<Lobby>();AddChild(lobby);await Frames();
            lobby.Navigate("Collection");Check(lobby.FindChildren("*","",true,false).OfType<CollectionShopPage>().Any(p=>p.Visible),"collection UI missing");
            lobby.Navigate("Shop");await Frames();
            Check(lobby.FindChildren("*","",true,false).OfType<Label>().Any(l=>l.Text.Contains("WALLET")&&l.Text.Contains("0 COINS")),"wallet not shown in lobby shop");
            if(DisplayServer.GetName()!="headless"){
                await ToSignal(RenderingServer.Singleton,RenderingServer.SignalName.FramePostDraw);GetViewport().GetTexture().GetImage().SavePng("/tmp/riftbound-storage-shop.png");
                lobby.Navigate("Collection");await Frames();await ToSignal(RenderingServer.Singleton,RenderingServer.SignalName.FramePostDraw);GetViewport().GetTexture().GetImage().SavePng("/tmp/riftbound-storage-collection.png");
            }
            var initialDeck=DeckStore.Selected!.Clone();
            lobby.DeleteDeck(initialDeck.Id);
            ulong deleteUntil=Time.GetTicksMsec()+8000;while(DeckStore.Decks.Count>0&&Time.GetTicksMsec()<deleteUntil)await Frames(1);
            Check(DeckStore.Decks.Count==0&&DeckStore.Selected==null,"Delete button flow did not remove the last deck");
            await PlayerInventory.Load(a);Check(DeckStore.Decks.Count==0&&PlayerInventory.State.Profile.OwnedCharacters.Length==40,"Deleted deck returned or owned collection changed on reload");
            await PlayerInventory.SaveDecks(new(){initialDeck},initialDeck.Id);
            lobby.QueueFree();await Frames();
            var deck=DeckStore.Selected!.Clone();deck.Name="Cloud expedition";
            await PlayerInventory.SaveDecks(new(){deck},deck.Id);
            Check(DeckStore.Selected!.Name=="Cloud expedition","cloud save did not update deck list");
            await PlayerInventory.Load(a);
            Check(DeckStore.Selected.Name=="Cloud expedition","reload lost server deck");
            b=new(deviceId:"rift-player-storage-ui-"+Guid.NewGuid().ToString("N"));await b.Connect(realtime:false);
            PlayerInventory.Reset();await PlayerInventory.Load(b);
            Check(DeckStore.Selected!.Name!="Cloud expedition"&&PlayerInventory.State.UserId==b.Session.UserId,"another account inherited deck cache");
            GameAccount.Session=a.Session;PlayerInventory.Reset();await PlayerInventory.Load(a);
            Check(DeckStore.Selected!.Name=="Cloud expedition","account switch did not recover its own deck");
            var catalog=await a.Client.RpcAsync(a.Session,"character_catalog","{}");
            var server=JsonSerializer.Deserialize(catalog.Payload,GameJsonContext.Default.CharacterCatalogData)!;
            Check(server.Characters[0].Stats[0].Hp==CharacterData.All[0].Stats[0].Hp,"client stats do not match backend");
            var launchLobby=GD.Load<PackedScene>("res://scenes/lobby.tscn").Instantiate<Lobby>();GetTree().Root.AddChild(launchLobby);GetTree().CurrentScene=launchLobby;await Frames();launchLobby.Launch(true);
            await Wait(()=>GetTree().CurrentScene?.GetNodeOrNull<OnlineBattle>("OnlineBattle")!=null,"Searching scene did not load",10000);await Frames();
            var searching=GetTree().CurrentScene;
            Check(!searching.FindChild("BackButton",true,false).Get("visible").AsBool()&&!searching.FindChild("BattleSpeedButton",true,false).Get("visible").AsBool(),"Surrender/speed appeared while searching");
            await Wait(()=>GetTree().CurrentScene?.GetNodeOrNull<OnlineBattle>("OnlineBattle")?.State?.Phase=="preparation","Cloud deck did not enter gameplay",35000);
            var online=GetTree().CurrentScene.GetNode<OnlineBattle>("OnlineBattle");
            Check(online.UserId==a.Session.UserId&&DeckStore.Selected!.Name=="Cloud expedition","arena launched a different account or deck");
            await Frames();
            Check(GetTree().CurrentScene.FindChild("BackButton",true,false).Get("visible").AsBool()&&GetTree().CurrentScene.FindChild("BattleSpeedButton",true,false).Get("visible").AsBool(),"Surrender/speed missing after entering gameplay");
            var gameplayShop=GetTree().CurrentScene.GetNode<CardShop>("UI/SafeArea/Content/CardShop");
            online.SendAction("buy",gameplayShop.Offers[0].Token);
            await Wait(()=>gameplayShop.Hand.Count>0,"Could not buy a preparation unit",5000);
            online.SendAction("deploy",gameplayShop.Hand[0].Token,0);
            await Wait(()=>gameplayShop.Deployed.Count>0,"Could not deploy a preparation unit",5000);
            Check(gameplayShop.NetworkUnits.Values.All(u=>u.Sprite.IsPlaying()&&u.Sprite.Animation==BattleAnimations.Idle),"Idle did not start in actual preparation");
            await online.Connection!.Close();
            GD.Print($"PLAYER STORAGE UI PASS ({_checks} checks): backend catalog/cache, per-user collection/decks, cloud save, wallet and lobby shop");
            await a.Close();await b.Close();GetTree().Quit();
        }catch(Exception e){GD.PushError(e.ToString());if(a!=null)await a.Close();if(b!=null)await b.Close();GetTree().Quit(1);}
    }
}
