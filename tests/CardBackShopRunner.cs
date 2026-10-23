using Godot;
using System;
using System.Linq;

public partial class CardBackShopRunner : Node
{
    public override void _Ready()=>Callable.From(Run).CallDeferred();
    private async void Run()
    {
        NakamaConnection? connection=null;
        try {
            connection=new(deviceId:"card-back-display-"+Guid.NewGuid().ToString("N"));
            await connection.Connect(realtime:false);
            GameAccount.RequiresLogin=false;GameAccount.Session=connection.Session;
            await PlayerInventory.Load(connection);
            if(CardBackCatalog.All.Length!=20)throw new Exception("Bootstrap did not provide twenty backs");
            var lobby=GD.Load<PackedScene>("res://scenes/lobby.tscn").Instantiate<Lobby>();AddChild(lobby);
            for(int i=0;i<8;i++)await ToSignal(GetTree(),SceneTree.SignalName.ProcessFrame);
            var rows=lobby.FindChildren("*","HBoxContainer",true,false).OfType<HBoxContainer>()
                .Select(row=>row.GetChildren().OfType<VBoxContainer>().SelectMany(column=>column.GetChildren().OfType<TextureRect>()).ToArray())
                .Where(cards=>cards.Length==3).ToArray();
            if(rows.Length<2)throw new Exception("Lobby hero preview rows are missing");
            foreach(var cards in rows){
                if(cards.Max(c=>c.Size.X)-cards.Min(c=>c.Size.X)>1 || cards.Max(c=>c.Size.Y)-cards.Min(c=>c.Size.Y)>1 || cards.Max(c=>c.GlobalPosition.Y)-cards.Min(c=>c.GlobalPosition.Y)>1)
                    throw new Exception("Hero names changed card dimensions or alignment");
            }
            if(DisplayServer.GetName()!="headless"){
                await ToSignal(RenderingServer.Singleton,RenderingServer.SignalName.FramePostDraw);
                GetViewport().GetTexture().GetImage().SavePng("/tmp/riftbound-equal-lobby-cards.png");
            }
            lobby.Navigate("Shop");
            lobby.FindChild("ShopCardBacksTab",true,false).EmitSignal(BaseButton.SignalName.Pressed);
            for(int i=0;i<8;i++)await ToSignal(GetTree(),SceneTree.SignalName.ProcessFrame);
            var coins=(Label)lobby.FindChild("LobbyWalletCoins",true,false);
            if(coins.Text!=PlayerInventory.Coins.ToString())throw new Exception("Header wallet balance is stale");
            var hero=(TextureButton)lobby.FindChild("ShopCharactersTab",true,false);
            var cardTab=(TextureButton)lobby.FindChild("ShopCardBacksTab",true,false);
            if(!hero.IsVisibleInTree() || hero.GetNode<Label>("Text").Text!="Hero" || cardTab.GetNode<Label>("Text").Text!="Card")throw new Exception("Card and Hero tabs are missing");
            hero.EmitSignal(BaseButton.SignalName.Pressed);
            for(int i=0;i<4;i++)await ToSignal(GetTree(),SceneTree.SignalName.ProcessFrame);
            if(lobby.FindChildren("ShopCardBack_card_back_*","",true,false).Count!=0 || !((Control)lobby.FindChild("ShopGroupFilter",true,false)).IsVisibleInTree())throw new Exception("Hero tab failed to open");
            cardTab.EmitSignal(BaseButton.SignalName.Pressed);
            for(int i=0;i<4;i++)await ToSignal(GetTree(),SceneTree.SignalName.ProcessFrame);
            var buttons=lobby.FindChildren("BuyCardBack_*","",true,false).OfType<TextureButton>().ToArray();
            if(PlayerInventory.Configuration!.Economy.FreeCardBacks){
                if(buttons.Length!=0 || PlayerInventory.State!.Profile.OwnedCharacters.Length!=40 || PlayerInventory.State.Profile.OwnedCardBacks.Length!=20)throw new Exception("Free distribution must unlock all cards and pause shop offers");
                if(!((Control)lobby.FindChild("ShopCardBacksTab",true,false)).IsVisibleInTree() || lobby.FindChildren("ShopCardBack_card_back_*","",true,false).Count!=20)throw new Exception("Shop must display all twenty free backs");
                if(DisplayServer.GetName()!="headless"){
                    await ToSignal(RenderingServer.Singleton,RenderingServer.SignalName.FramePostDraw);
                    GetViewport().GetTexture().GetImage().SavePng("/tmp/riftbound-free-card-back-shop.png");
                }
                lobby.Navigate("Collection");lobby.FindChild("ShopCardBacksTab",true,false).EmitSignal(BaseButton.SignalName.Pressed);
                for(int i=0;i<4;i++)await ToSignal(GetTree(),SceneTree.SignalName.ProcessFrame);
                if(lobby.FindChildren("BuyCardBack_*","",true,false).Count!=0)throw new Exception("Free collection contains purchase buttons");
                var deck=DeckDefinition.Starter();DeckStore.ApplyRemote("",new(){deck},deck.Id);DeckStore.EditId=deck.Id;
                var editor=GD.Load<PackedScene>("res://scenes/deck_builder.tscn").Instantiate<DeckBuilder>();AddChild(editor);editor.SetHero(0,48);
                if(editor.Draft.Validate()!="" || editor.Draft.Heroes[0]!=48 || !editor.SelectCardBack("card_back_19"))throw new Exception("All free groups and backs must be selectable");
                editor.Free();
            }else {
                if(buttons.Length!=19 || buttons.Any(b=>!b.IsVisibleInTree() || !b.Disabled || b.GetNode<Label>("Text").Text!="Buy 100 COINS") || PlayerInventory.State!.Profile.OwnedCardBacks.Length!=1)throw new Exception("Default-only account must display nineteen paid backs");
                if(PlayerInventory.Configuration.Economy.FreeCharacterCards && PlayerInventory.State.Profile.OwnedCharacters.Length!=40)throw new Exception("Character cards must remain free");
            }
            long balance=PlayerInventory.Coins;
            PlayerInventory.State!.Wallet["coins"]=balance+7;
            for(int i=0;i<2;i++)await ToSignal(GetTree(),SceneTree.SignalName.ProcessFrame);
            if(coins.Text!=(balance+7).ToString())throw new Exception("Header did not update after wallet changed");
            PlayerInventory.State.Wallet["coins"]=balance;
            for(int i=0;i<2;i++)await ToSignal(GetTree(),SceneTree.SignalName.ProcessFrame);
            if(DisplayServer.GetName()!="headless"){
                await ToSignal(RenderingServer.Singleton,RenderingServer.SignalName.FramePostDraw);
                GetViewport().GetTexture().GetImage().SavePng("/tmp/riftbound-live-card-back-shop.png");
            }
            GD.Print("LIVE CARD DISTRIBUTION PASS: complete ownership, shop state, collection and deck selection");
            await connection.Close();GetTree().Quit();
        }catch(Exception e){GD.PushError(e.ToString());if(connection!=null)await connection.Close();GetTree().Quit(1);}
    }
}
