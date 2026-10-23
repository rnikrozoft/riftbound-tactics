using Godot;
using System.Linq;
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
            var card=shop.Offers[0];int price=card.Price;
            typeof(CardShop).GetProperty("Coins")!.SetValue(shop,100);
            var remaining=(int[])typeof(CardShop).GetField("_remainingCopies",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance)!.GetValue(shop)!;
            int kind=CharacterData.All.First(c=>c.Name==card.Name).Kind;remaining[kind]=8;
            shop.Hand.Clear();shop.Offers.Clear();for(int i=0;i<8;i++)shop.Offers.Add(card with {Token=500+i});
            Check(shop.TakeCard(500),"First copy");
            for(int i=0;i<9;i++)shop.Hand.Add(card with {Token=900+i,Name=$"Capacity {i}"});
            Check(shop.TakeCard(501)&&shop.Hand.Count==10&&shop.Hand.Any(c=>c.Token==500&&c.Stars==2),"Full hand pair merge retains token");
            shop.Hand.RemoveAll(c=>c.Token!=500);
            Check(shop.PlaceCardOrUnit(new CardDrag {Shop=shop,Token=500},shop.Tiles.ToGlobal(shop.SlotCenters[0])),"Deploy pair");
            var unit=shop.Deployed[500].Unit;unit.HasBattled=true;
            for(int i=0;i<10;i++)shop.Hand.Add(card with {Token=1000+i,Name=$"Capacity {i}"});
            int before=shop.Coins;
            Check(!shop.TakeCard(502)&&shop.Coins==before&&shop.Deployed.Count==1,"Full hand rejects unmatched tier without spending");
            shop.Hand.Clear();shop.ShowUnitDetails(unit);
            Check(shop.TakeCard(502)&&shop.Deployed[500].Card.Stars==2&&shop.Hand.Count==1&&shop.Hand[0].Stars==1,"Third copy stays one star beside deployed pair");
            Check(shop.TakeCard(503)&&shop.Deployed.Count==0&&shop.Hand.Count==1&&shop.Hand[0].Stars==3&&shop.Hand[0].Veteran,"Fourth copy carries into field pair and preserves veteran");
            Check(!shop.Details.Visible,"Consumed unit details close");
            shop.ShowCardDetails(500);Check(shop.DetailsText.Text.Contains("3 STARS"),"Three-star details at four copies");shop.CloseDetails();
            Check(shop.PlaceCardOrUnit(new CardDrag {Shop=shop,Token=500},shop.Tiles.ToGlobal(shop.SlotCenters[0])),"Redeploy three stars");
            for(int i=504;i<=507;i++)Check(shop.TakeCard(i),"Purchase toward eight copies");
            Check(shop.Deployed.Count==0&&shop.Hand.Count==1&&shop.Hand[0].Stars==4&&shop.Hand[0].Investment==price*8,"Eight-copy four-star upgrade");
            await Frames(12);await Capture("upgrade-field");
            shop.Offers.Add(card with {Token=508});before=shop.Coins;
            Check(!shop.TakeCard(508)&&shop.Coins==before,"Exhausted pool rejects ninth copy without spending");
            Check(shop.PlaceCardOrUnit(new CardDrag {Shop=shop,Token=500},shop.Tiles.ToGlobal(shop.SlotCenters[0])),"Deploy four stars");
            unit=shop.Deployed[500].Unit;var stats=CharacterData.Stats(kind,4);
            Check(unit.Stars==4&&unit.Health==stats.Hp&&unit.Armor==stats.Armor,"Four-star actual combat stats");
            Check(!shop.ReturnUnit(unit),"Veteran cannot manually return");
            shop.SellDrop(new CardDrag {Shop=shop,Unit=unit});Check(shop.Coins==before+1&&shop.Deployed.Count==0,"Four-star field sale returns one coin");
            await ToSignal(GetTree().CreateTimer(1.6),SceneTreeTimer.SignalName.Timeout);
            Check(!shop.HandRow.HasNode("UpgradeEffect"),"No lingering upgrade effect");
            GD.Print($"UPGRADE PASS: {_checks} checks"); GetTree().Quit();
        } catch(Exception e) { GD.PushError(e.ToString()); GetTree().Quit(1); }
    }
}
