using Godot;
using System;
using System.Threading.Tasks;

public partial class CharacterUpgradeIntegrationRunner : Node
{
    private int _checks;
    private void Check(bool value,string message) { _checks++; if(!value) throw new InvalidOperationException(message); }
    private async Task Frames(int count=3) { for(int i=0;i<count;i++) await ToSignal(GetTree(),SceneTree.SignalName.ProcessFrame); }
    private async Task Capture(string name)
    {
        if(DisplayServer.GetName()=="headless") return;
        await ToSignal(RenderingServer.Singleton,RenderingServer.SignalName.FramePostDraw);
        GetViewport().GetTexture().GetImage().SavePng($"res://tests/economy-{name}.png");
    }
    public override void _Ready() => Callable.From(Run).CallDeferred();
    private async void Run()
    {
        try {
            var game=GD.Load<PackedScene>("res://scenes/main.tscn").Instantiate<Node2D>();
            var battle=game.GetNode<BattleDemo>("BattleDemo"); battle.NetworkEnabled=false; battle.StartDelay=1000; AddChild(game);
            var shop=game.GetNode<CardShop>("UI/SafeArea/Content/CardShop");
            _=shop.PrepareTurn(1); _=shop.PrepareTurn(2); _=shop.PrepareTurn(3); await Frames();
            var card=shop.Offers[0]; int price=card.Price;
            shop.Offers.Clear(); for(int i=0;i<4;i++) shop.Offers.Add(card with { Token=500+i });
            Check(shop.TakeCard(500),"First copy");
            for(int i=0;i<9;i++) shop.Hand.Add(card with { Token=900+i,Name=$"Test {i}" });
            Check(shop.AcceptsToken(501)&&shop.TakeCard(501),"Upgrade with full hand");
            Check(shop.Hand.Count==10&&shop.Hand[0].Stars==2&&shop.Hand[0].Token==500,"Merge keeps token and hand capacity");
            Check(shop.Hand[0].Investment==price*2,"Stack investment");
            await Frames(12); await Capture("upgrade-hand");
            shop.Hand.RemoveRange(1,9);
            Check(shop.PlaceCardOrUnit(new CardDrag {Shop=shop,Token=500},shop.Tiles.ToGlobal(shop.SlotCenters[0])),"Deploy upgraded card");
            var unit=shop.Deployed[500].Unit;
            Check(unit.Stars==2&&unit.MaxHealth==200&&unit.Health==200&&unit.Attack==60&&unit.Speed==12,"Two-star actual stats");
            for(int i=0;i<10;i++) shop.Hand.Add(card with {Token=1000+i,Name=$"Capacity {i}"});
            int fullCoins=shop.Coins;
            Check(!shop.AcceptsToken(502)&&!shop.TakeCard(502)&&shop.Coins==fullCoins&&shop.Deployed.Count==1,"Full hand blocks automatic return without spending");
            shop.Hand.Clear(); unit.HasBattled=true; shop.ShowUnitDetails(unit);
            Check(shop.TakeCard(502),"Upgrade deployed copy");
            Check(shop.Hand.Count==1&&shop.Deployed.Count==0&&shop.Occupants[0]==null&&shop.Hand[0].Stars==3&&shop.Hand[0].Veteran,"Field upgrade automatically returns to hand");
            Check(!shop.Details.Visible,"Removed unit detail closes");
            shop.ShowCardDetails(500);
            Check(shop.DetailsText.Text.Contains("3 STARS")&&shop.DetailsText.Text.Contains("Attack  90"),"Upgraded hand details"); shop.CloseDetails();
            Check(shop.PlaceCardOrUnit(new CardDrag {Shop=shop,Token=500},shop.Tiles.ToGlobal(shop.SlotCenters[0])),"Redeploy three stars");
            Check(shop.Deployed[500].Unit.HasBattled,"Veteran preserved");
            Check(shop.TakeCard(503),"Fourth copy");
            Check(shop.Hand[0].Stars==4&&shop.Deployed.Count==0,"Fourth copy auto returns again");
            Check(shop.HandRow.GetChild<ShopCard>(0).HasNode("UpgradeEffect"),"Asset animation plays on returned card");
            await Frames(12); await Capture("upgrade-field");
            shop.Offers.Add(card with {Token=504}); int coins=shop.Coins;
            Check(!shop.AcceptsToken(504)&&!shop.TakeCard(504)&&shop.Coins==coins,"Cap rejects fifth without spending");
            Check(shop.PlaceCardOrUnit(new CardDrag {Shop=shop,Token=500},shop.Tiles.ToGlobal(shop.SlotCenters[0])),"Redeploy four stars");
            unit=shop.Deployed[500].Unit;
            Check(unit.Stars==4&&unit.Attack==120&&unit.Speed==16&&unit.MaxHealth==400,"Returned upgrade retains stats");
            Check(!shop.ReturnUnit(unit),"Manual veteran return remains forbidden");
            shop.SellDrop(new CardDrag {Shop=shop,Unit=unit});
            Check(shop.Deployed.Count==0&&shop.Coins==coins+price*2,"Refund half combined purchase cost");
            await ToSignal(GetTree().CreateTimer(1.6),SceneTreeTimer.SignalName.Timeout);
            Check(!shop.HandRow.HasNode("UpgradeEffect"),"No lingering upgrade effect");
            GD.Print($"UPGRADE PASS: {_checks} checks"); GetTree().Quit();
        } catch(Exception e) { GD.PushError(e.ToString()); GetTree().Quit(1); }
    }
}
