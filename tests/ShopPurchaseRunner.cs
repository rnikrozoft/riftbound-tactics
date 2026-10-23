using Godot;
using System;
using System.Linq;
using System.Threading.Tasks;

public partial class ShopPurchaseRunner : Node
{
    private int _checks;
    private void Check(bool ok,string message){_checks++;if(!ok)throw new Exception(message);}
    private async Task Frames(){for(int i=0;i<2;i++)await ToSignal(GetTree(),SceneTree.SignalName.ProcessFrame);}
    public override void _Ready()=>Callable.From(Run).CallDeferred();
    private async void Run()
    {
        NakamaConnection? c=null;
        try {
            string device=OS.GetCmdlineUserArgs().Single();PlayerInventory.Reset();GameAccount.RequiresLogin=false;
            c=new(deviceId:device);await c.Connect(realtime:false);GameAccount.Session=c.Session;await PlayerInventory.Load(c);
            Check(!PlayerInventory.AllUnlocked&&PlayerInventory.State!.Profile.OwnedCharacters.Length==30,"Only the three starter characters should be free");
            Check(!CharacterData.All.Where(x=>x.Group==48).Any(x=>PlayerInventory.CanUse(x.Kind)),"Paid group usable before purchase");
            var lobby=GD.Load<PackedScene>("res://scenes/lobby.tscn").Instantiate<Lobby>();AddChild(lobby);lobby.Navigate("Shop");await Frames();
            var filter=(OptionButton)lobby.FindChild("ShopGroupFilter",true,false);int index=filter.GetItemIndex(48);filter.Select(index);filter.EmitSignal(OptionButton.SignalName.ItemSelected,(long)index);await Frames();
            var buy=(TextureButton)lobby.FindChild("BuyCharacterGroupButton",true,false);
            Check(!buy.Disabled&&PlayerInventory.Coins==PlayerInventory.GroupPrice(48),"Funded group must be available in shop at the server price");
            if(DisplayServer.GetName()!="headless"){await ToSignal(RenderingServer.Singleton,RenderingServer.SignalName.FramePostDraw);GetViewport().GetTexture().GetImage().SavePng("/tmp/riftbound-group-shop.png");}
            buy.EmitSignal(BaseButton.SignalName.Pressed);
            ulong until=Time.GetTicksMsec()+8000;while(PlayerInventory.State!.Profile.OwnedCharacters.Length==30&&Time.GetTicksMsec()<until)await Frames();
            Check(PlayerInventory.Coins==0&&PlayerInventory.State!.Profile.OwnedCharacters.Length==40,"Shop button did not atomically buy the complete group");
            Check(CharacterData.All.Where(x=>x.Enabled&&x.Group==48).All(x=>PlayerInventory.CanUse(x.Kind)),"Purchased character still locked");
            await Frames();Check(buy.Disabled,"Owned group must not offer a second purchase");lobby.QueueFree();await Frames();
            DeckStore.EditId="";var editor=GD.Load<PackedScene>("res://scenes/deck_builder.tscn").Instantiate<DeckBuilder>();AddChild(editor);await Frames();editor.SetHero(0,48);
            Check(editor.Draft.Heroes[0]==48&&editor.Draft.Validate()=="","Purchased group cannot replace a starter character in Deck Builder");
            var deck=editor.Draft.Clone();await PlayerInventory.SaveDecks(new(){deck},deck.Id);await PlayerInventory.Load(c);
            Check(DeckStore.Selected!.Heroes[0]==48&&PlayerInventory.State!.Profile.OwnedCharacters.Length==40,"Cloud reload lost purchased group or deck");
            await c.Connect();await c.EnterRoom(true,"",deck);Check(c.MatchId!="","Nakama rejected a deck containing the purchased character");
            GD.Print($"SHOP PURCHASE UI PASS: {_checks} checks");await c.Close();GetTree().Quit();
        }catch(Exception e){GD.PushError(e.ToString());if(c!=null)await c.Close();GetTree().Quit(1);}
    }
}
