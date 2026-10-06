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
            await Wait(()=>GetTree().CurrentScene?.GetNodeOrNull<OnlineBattle>("OnlineBattle")?.State?.Phase=="preparation","Cloud deck did not enter gameplay",35000);
            var online=GetTree().CurrentScene.GetNode<OnlineBattle>("OnlineBattle");
            Check(online.UserId==a.Session.UserId&&DeckStore.Selected!.Name=="Cloud expedition","arena launched a different account or deck");
            await online.Connection!.Close();
            GD.Print($"PLAYER STORAGE UI PASS ({_checks} checks): backend catalog/cache, per-user collection/decks, cloud save, wallet and lobby shop");
            await a.Close();await b.Close();GetTree().Quit();
        }catch(Exception e){GD.PushError(e.ToString());if(a!=null)await a.Close();if(b!=null)await b.Close();GetTree().Quit(1);}
    }
}
