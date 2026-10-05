using Godot;
using System;
using System.Linq;
using System.Threading.Tasks;

public partial class UpgradeHintRunner : Node
{
    private async Task Frames() { for(int i=0;i<8;i++)await ToSignal(GetTree(),SceneTree.SignalName.ProcessFrame); }
    private static bool Highlighted(Node node)=>node.GetNodeOrNull<AnimatedSprite2D>("UpgradeHint") is {Visible:true};
    private async Task Capture(string name) { await Frames();if(DisplayServer.GetName()=="headless")return;await ToSignal(RenderingServer.Singleton,RenderingServer.SignalName.FramePostDraw);GetViewport().GetTexture().GetImage().SavePng("/tmp/riftbound-"+name+".png"); }
    public override void _Ready()=>Callable.From(Run).CallDeferred();
    private async void Run()
    {
        try {
            var field=GD.Load<PackedScene>("res://scenes/main.tscn").Instantiate<BattleDisplay>();field.GetNode("OnlineBattle").Free();
            var battle=field.GetNode<BattleDemo>("BattleDemo");battle.NetworkEnabled=true;battle.AutoStart=true;AddChild(field);
            var shop=field.GetNode<CardShop>("UI/SafeArea/Content/CardShop");
            var self=new OnlinePlayer {UserId="self",Team="A",Coins=10,Offers=new[]{new OnlineCard {Kind=60,Token=20},new OnlineCard {Kind=1,Token=21}},Hand=new[]{new OnlineCard {Kind=60,Token=10},new OnlineCard {Kind=61,Token=12}},Units=new[]{new OnlineUnit {Kind=1,Token=11,Slot=0}}};
            var state=new OnlineState {Phase="preparation",Round=1,Players=new OnlinePlayer?[]{self,new OnlinePlayer {UserId="enemy",Team="B",Units=new[]{new OnlineUnit {Kind=1,Token=11,Slot=0}}}}};
            shop.ApplyNetworkState(state,"self");await Frames();
            if(shop.HandRow.GetChildren().OfType<ShopCard>().First().GetNode<TextureRect>("Fighter").Size.Y<=0)throw new Exception("Hand portrait must remain visible under hint");
            if(shop.ShopRow.GetChildren().OfType<ShopCard>().Count(Highlighted)!=2 || shop.HandRow.GetChildren().OfType<ShopCard>().Count(Highlighted)!=1 || !Highlighted(shop.NetworkUnits["A:11"]) || Highlighted(shop.NetworkUnits["B:11"]))throw new Exception("Upgrade hints must match owned hand/field only");
            await Capture("upgrade-hints");
            self.Hand= self.Hand.Reverse().ToArray();shop.ApplyNetworkState(state,"self");await Frames();
            foreach(var card in shop.HandRow.GetChildren().OfType<ShopCard>()) {
                var figure=card.GetNode<TextureRect>("Fighter");
                if(figure.Size.Y<=0 || figure.Texture.GetImage().GetUsedRect().Size.Y<=0)throw new Exception("Reused Orc/Demon hand portraits must remain visible");
            }
            var tiles=field.GetNode<TileMapLayer>("TileMapLayer");
            if(tiles.GetUsedRect().Position.X!=34 || field.GetNode("SupportSlots").GetChildCount()!=5 || BattleDisplay.SupportCells.Any(cell=>tiles.GetCellSourceId(cell)<0))throw new Exception("Expanded arena must contain five rear support slots");
            if(field.GetNode("EnemySupportSlots").GetChildCount()!=5 || BattleDisplay.EnemySupportCells.Any(cell=>tiles.GetCellSourceId(cell)<0))throw new Exception("Enemy must have five rear support slots on arena tiles");
            for(int i=0;i<5;i++)if(BattleDisplay.SupportCells[i].X+BattleDisplay.EnemySupportCells[i].X!=102 || BattleDisplay.SupportCells[i].Y!=BattleDisplay.EnemySupportCells[i].Y)throw new Exception("Opposing support slots must align on the same tile row");
            await Capture("orc-hand-expanded-arena");self.Hand=self.Hand.Reverse().ToArray();
            self.Hand[0].Stars=4;self.Units[0].Stars=4;shop.ApplyNetworkState(state,"self");
            if(shop.ShopRow.GetChildren().OfType<ShopCard>().Any(Highlighted) || Highlighted(shop.NetworkUnits["A:11"]))throw new Exception("Max stars must not highlight");
            self.Hand[0].Stars=1;self.Units[0].Stars=1;self.Offers=Array.Empty<OnlineCard>();shop.ApplyNetworkState(state,"self");
            if(shop.HandRow.GetChildren().OfType<ShopCard>().Any(Highlighted) || Highlighted(shop.NetworkUnits["A:11"]))throw new Exception("Removed offers must clear hints");
            state.Phase="finished";shop.ApplyNetworkState(state,"self");await Frames();
            var result=field.GetNode<Control>("UI/SafeArea/Content/BattleResult");result.Show();
            var next=shop.GetNode<TextureButton>("BattleActions/BattleButton");
            if(Mathf.Abs(next.GetGlobalRect().GetCenter().X-result.GetGlobalRect().GetCenter().X)>1 || next.GetGlobalRect().Position.Y<result.GetGlobalRect().End.Y)throw new Exception("Next Round must sit centered below result");
            await Capture("next-round-centered");
            state.Phase="preparation";shop.ApplyNetworkState(state,"self");await Frames();
            if(next.GetGlobalRect().GetCenter().X<=result.GetGlobalRect().GetCenter().X)throw new Exception("Battle button must return to right side");
            GD.Print("UPGRADE HINT PASS: matching shop/hand/field, enemy exclusion, max stars, cleared offers, centered Next Round, preparation layout");GetTree().Quit();
        } catch(Exception e) {GD.PushError(e.ToString());GetTree().Quit(1);}
    }
}
