using Godot;
using System;
using System.Linq;
using System.Threading.Tasks;

public partial class UiReviewRunner : Node
{
    private async Task Frames(int n=5) { for(int i=0;i<n;i++) await ToSignal(GetTree(),SceneTree.SignalName.ProcessFrame); }
    private async Task Capture(string name) { await Frames(10);await ToSignal(RenderingServer.Singleton,RenderingServer.SignalName.FramePostDraw);GetViewport().GetTexture().GetImage().SavePng("/tmp/riftbound-ui-"+name+".png"); }
    public override void _Ready()=>Callable.From(Run).CallDeferred();
    private async void Run()
    {
        try {
            var lobby=GD.Load<PackedScene>("res://scenes/lobby.tscn").Instantiate<Lobby>();AddChild(lobby);await Capture("lobby");
            lobby.Navigate("Decks");await Capture("decks");lobby.Navigate("Shop");await Capture("shop-menu");lobby.Navigate("Achievements");await Capture("achievements");
            lobby.Navigate("Leaderboard");await Frames();var rankings=lobby.FindChild("Rankings",true,false) as LeaderboardPage;
            ulong until=Time.GetTicksMsec()+8000;while(rankings!.Loading && Time.GetTicksMsec()<until)await Frames(1);await Capture("rankings");lobby.QueueFree();await Frames();
            DeckStore.EditId="";var editor=GD.Load<PackedScene>("res://scenes/deck_builder.tscn").Instantiate<DeckBuilder>();AddChild(editor);await Capture("builder");
            editor.SetGroup(4);editor.Filter(4);if(editor.VisibleCardCount!=CardCatalog.Options(4,4).Count())throw new Exception("deck filter");editor.QueueFree();await Frames();
            var field=GD.Load<PackedScene>("res://scenes/main.tscn").Instantiate<BattleDisplay>();var battle=field.GetNode<BattleDemo>("BattleDemo");battle.AutoStart=false;battle.NetworkEnabled=false;battle.ArenaFloatAmplitude=0;
            AddChild(field);var shop=field.GetNode<CardShop>("UI/SafeArea/Content/CardShop");_=shop.PrepareTurn(1);await Capture("preparation");
            var originalPosition=field.Position;var originalShop=shop.ZoneA.GetGlobalRect();
            shop.ShowCardDetails(shop.Offers[0].Token);await Capture("details-modal");
            if(field.Position!=originalPosition || shop.ZoneA.GetGlobalRect()!=originalShop)throw new Exception("Modal moved arena or shop");
            if(shop.Details.GetGlobalRect().GetCenter().DistanceTo(GetViewport().GetVisibleRect().GetCenter())>2)throw new Exception("Modal is not centered");
            Input.ParseInputEvent(new InputEventKey {Keycode=Key.Escape,Pressed=true});await Frames();
            if(shop.Details.Visible)throw new Exception("Escape did not close modal");
            shop.UpdateCountdown(10,true);await Capture("countdown");
            var countdown=shop.GetNode<PreparationCountdown>("CenterCountdown");
            if(!countdown.Visible || countdown.Seconds!=10 || countdown.GetGlobalRect().GetCenter().DistanceTo(GetViewport().GetVisibleRect().GetCenter())>2)throw new Exception("Center countdown missing or misplaced");
            shop.UpdateCountdown(0,false);if(countdown.Visible)throw new Exception("Countdown remained after preparation");
            var offer=shop.Offers.First(c=>c.Price<=shop.Coins);if(!shop.TakeCard(offer.Token)||shop.Hand.Count!=1)throw new Exception("shop purchase");await Capture("hand");
            var coinsBeforeReroll=shop.Coins;var tokenBeforeReroll=shop.Offers[0].Token;
            shop.ToggleShopLock();if(shop.ZoneA.GetNode<TextureButton>("Reroll").Disabled)throw new Exception("Locked shop disabled reroll");
            shop.RerollShop();if(shop.ShopLocked || shop.Coins!=coinsBeforeReroll-CardShop.RerollCost || shop.Offers[0].Token==tokenBeforeReroll)throw new Exception("Reroll did not unlock and refresh shop");
            _=shop.PrepareTurn(2);shop.TakeCard(shop.Offers[0].Token);shop.ToggleShopLock();
            var lockedTokens=shop.Offers.Select(card=>card.Token).ToArray();_=shop.PrepareTurn(3);var addedOffer=shop.Offers[2];_=shop.PrepareTurn(4);
            if(!shop.ShopLocked || !shop.Offers.Take(2).Select(card=>card.Token).SequenceEqual(lockedTokens) || shop.Offers[2].Token==addedOffer.Token || shop.Offers[2].Name==addedOffer.Name)throw new Exception("Filled offer stayed locked across rounds");
            foreach(var card in shop.Offers.ToArray())if(!shop.TakeCard(card.Token))throw new Exception("Empty locked shop purchase failed");
            if(!shop.ShopLocked || shop.Offers.Count!=0)throw new Exception("Lock must remain until next round");
            var coinsBeforeNextRound=shop.Coins;_=shop.PrepareTurn(5);
            if(shop.ShopLocked || shop.Offers.Count!=shop.OfferLimit || shop.Coins!=coinsBeforeNextRound+CardShop.RoundIncome(5))throw new Exception("Empty locked shop did not unlock and refill next round");
            shop.FinishBattle("A",1);battle.ShowServerResult("A");await Capture("result");field.QueueFree();await Frames();
            GD.Print("UI REVIEW PASS: menu navigation, deck filter, shop purchase, centered modal, Escape dismissal, particle countdown, hand, result");GetTree().Quit();
        }catch(Exception e){GD.PushError(e.ToString());GetTree().Quit(1);}
    }
}
