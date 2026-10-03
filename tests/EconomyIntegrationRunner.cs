using Godot;
using System;
using System.Linq;
using System.Threading.Tasks;

public partial class EconomyIntegrationRunner : Node
{
    private int _checks;
    private SceneTree _tree = null!;
    private void Check(bool value, string message) { _checks++; if (!value) throw new InvalidOperationException(message); }
    private async Task Frames(int count = 3) { for (int i=0;i<count;i++) await ToSignal(_tree, SceneTree.SignalName.ProcessFrame); }
    private async Task Capture(string name)
    {
        if (DisplayServer.GetName() == "headless") return;
        await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
        GetViewport().GetTexture().GetImage().SavePng("res://tests/" + name + ".png");
    }
    public override void _Ready() => Callable.From(Run).CallDeferred();
    private async void Run()
    {
        _tree = GetTree();
        try
        {
            var game = GD.Load<PackedScene>("res://scenes/main.tscn").Instantiate<Node2D>();
            var battle = game.GetNode<BattleDemo>("BattleDemo"); battle.NetworkEnabled = false; battle.StartDelay = 1000;
            AddChild(game);
            var shop = game.GetNode<CardShop>("UI/SafeArea/Content/CardShop");
            _ = shop.PrepareTurn(1); await Frames();
            Check(shop.Coins == 4 && shop.Offers.Count == 3 && shop.ShopLevel==2 && shop.UpgradeCost==2, "Initial economy");
            var a = game.GetNode<Control>("UI/SafeArea/Content/ProfileA");
            var b = game.GetNode<Control>("UI/SafeArea/Content/ProfileB");
            Check(a.GetGlobalRect().Position.Y > 500 && b.GetGlobalRect().Position.Y > 500, "Bottom profiles");
            var reroll = shop.ZoneA.GetNode<Control>("Reroll");
            Check(reroll.GetGlobalRect().Position.X > shop.ShopRow.GetGlobalRect().End.X && reroll.GetGlobalRect().End.X < 1000, "Shop-relative buttons");
            await Capture("economy-shop");
            shop.ShowCardDetails(shop.Offers[0].Token); await Frames();
            Check(!shop.Details.GetGlobalRect().Intersects(a.GetGlobalRect()) && !shop.Details.GetGlobalRect().Intersects(b.GetGlobalRect()), "Details overlap profiles");
            Check(a.Visible && b.Visible, "Profiles remain visible with details");
            await Capture("economy-details"); shop.CloseDetails();
            int token=shop.Offers[0].Token;
            Check(shop.TakeCard(token) && shop.Coins==2,"Purchase 2 coin");
            Check(shop.SellCard(token) && shop.Coins==3,"Half-price hand refund");
            shop.RerollShop(); Check(shop.Coins == 1 && shop.Offers.Count == 3, "Paid reroll");
            Check(!shop.TakeCard(shop.Offers[0].Token), "Unaffordable buy");
            shop.ToggleShopLock(); var tokens = shop.Offers.Select(c=>c.Token).ToArray();
            shop.RerollShop(); Check(shop.Offers.Select(c=>c.Token).SequenceEqual(tokens), "Locked reroll rejected");
            shop.FinishBattle("B"); Check(shop.Coins == 1, "Loss has no penalty");
            _ = shop.PrepareTurn(2); Check(shop.Coins == 7 && shop.UpgradeCost==0 && shop.Offers.Select(c=>c.Token).SequenceEqual(tokens), "Income and locked offers");
            shop.UpgradeShop(); Check(shop.Coins==7 && shop.ShopLevel==3 && shop.UpgradeCost==8 && shop.Offers.Select(c=>c.Token).SequenceEqual(tokens),"Free upgrade preserves offers");
            shop.UpgradeShop(); Check(shop.ShopLevel==3 && shop.Coins==7,"Unaffordable upgrade");
            token=shop.Offers[0].Token; Check(shop.TakeCard(token) && shop.Coins==5,"Buy locked offer");
            Check(shop.PlaceCardOrUnit(new CardDrag { Shop=shop,Token=token }, shop.Tiles.ToGlobal(shop.SlotCenters[0])),"Deploy");
            shop.SellDrop(new CardDrag { Shop=shop,Unit=shop.Occupants[0] }); Check(shop.Coins==6,"Deployed refund");
            shop.FinishBattle("A"); shop.FinishBattle("A"); Check(shop.Coins==6,"Win does not award coins");
            _ = shop.PrepareTurn(3); Check(shop.Coins==15 && shop.UpgradeCost==6,"Upgrade discount after prior upgrade");
            shop.UpgradeShop(); Check(shop.ShopLevel==4 && shop.Coins==9 && shop.UpgradeCost==10,"Upgrade four");
            shop.FinishBattle("B"); _ = shop.PrepareTurn(4);
            shop.UpgradeShop(); Check(shop.ShopLevel==5 && shop.Coins==13 && shop.UpgradeCost==14,"Upgrade five");
            shop.FinishBattle("B"); _ = shop.PrepareTurn(5);
            shop.UpgradeShop(); Check(shop.ShopLevel==6 && shop.Coins==16 && shop.UpgradeCost==0 && shop.OfferLimit==5,"Level cap");
            shop.UpgradeShop(); Check(shop.Coins==16 && shop.ShopLevel==6,"Cannot upgrade beyond cap");
            shop.ToggleShopLock(); shop.RerollShop(); Check(shop.Coins==14 && shop.Offers.Count==5 && shop.Offers.All(c=>c.Price<=6),"High-level pool");
            await Frames(); await Capture("economy-shop");
            var countdown = shop.GetNode<PreparationCountdown>("PreparationCountdown");
            shop.SetProcess(false);
            shop.UpdateCountdown(16,true); Check(!countdown.Visible,"Countdown hidden above fifteen");
            shop.UpdateCountdown(15,true); Check(countdown.Visible && countdown.Seconds==15,"Countdown starts at fifteen");
            await Frames(12); await Capture("economy-countdown");
            shop.UpdateCountdown(7,true); Check(countdown.Seconds==7,"Countdown follows remaining time");
            shop.UpdateCountdown(0,false); Check(!countdown.Visible,"Countdown hides on battle");

            shop.ShowCardDetails(shop.Offers[0].Token); await Frames(); await Capture("economy-details"); shop.CloseDetails();
            shop.Hand.Clear();
            for (int i=0;i<10;i++) shop.Hand.Add(shop.Offers[0] with { Token=100+i });
            shop.RefreshNetworkControls(); await Frames();
            var hand = shop.HandRow.GetGlobalRect();
            Check(!hand.Intersects(a.GetGlobalRect()) && !hand.Intersects(b.GetGlobalRect()), "Ten cards overlap profiles");
            await Capture("economy-full-hand");
            var carryGame = GD.Load<PackedScene>("res://scenes/main.tscn").Instantiate<Node2D>();
            var carryBattle = carryGame.GetNode<BattleDemo>("BattleDemo"); carryBattle.NetworkEnabled=false; carryBattle.StartDelay=1000;
            AddChild(carryGame);
            var carry = carryGame.GetNode<CardShop>("UI/SafeArea/Content/CardShop");
            _ = carry.PrepareTurn(1); carry.FinishBattle("DRAW"); _ = carry.PrepareTurn(2);
            Check(carry.Coins==10,"Unspent four plus six carries over");
            carry.FinishBattle("B",4); Check(carry.PlayerHpA==24 && carry.PlayerHpB==30,"Offline health damage");
            _ = carry.PrepareTurn(3); Check(carry.Coins==19 && carry.PlayerHpA==24,"Coins stack and health persists");
            carry.FinishBattle("B",30); Check(carry.GameOver && carry.PlayerHpA==0,"Offline elimination");
            int terminalCoins = carry.Coins; _ = carry.PrepareTurn(4); carry.ConfirmTurn();
            Check(carry.Coins==terminalCoins && carry.GameOver,"No round after elimination");
            carryGame.QueueFree();
            GD.Print($"ECONOMY PASS: {_checks} checks"); _tree.Quit();
        }
        catch(Exception exception) { GD.PushError(exception.ToString()); _tree.Quit(1); }
    }
}