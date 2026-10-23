using Godot;
using System;
using System.Linq;
using System.Threading.Tasks;

public partial class HandOverflowRunner : Node
{
    private void Check(bool condition,string message) { if(!condition)throw new Exception(message); }
    private async Task Frames(int count=5) { for(int i=0;i<count;i++)await ToSignal(GetTree(),SceneTree.SignalName.ProcessFrame); }
    public override void _Ready()=>Callable.From(Run).CallDeferred();
    private async void Run()
    {
        try {
            var field=GD.Load<PackedScene>("res://scenes/main.tscn").Instantiate<Node2D>();
            field.GetNode("OnlineBattle").Free();field.GetNode<BattleDemo>("BattleDemo").AutoStart=false;
            AddChild(field);await Frames();
            var shop=field.GetNode<CardShop>("UI/SafeArea/Content/CardShop");
            var state=System.Text.Json.JsonSerializer.Deserialize(Godot.FileAccess.GetFileAsString("res://tests/combat_fixture.json"),GameJsonContext.Default.OnlineState)!;
            var self=state.Players[0]!;var opponent=state.Players[1]!;
            int kind=CharacterData.SupportKinds.First();state.Phase="preparation";
            self.Coins=100;self.Ready=false;self.Units=Array.Empty<OnlineUnit>();opponent.Units=Array.Empty<OnlineUnit>();
            self.Offers=new[]{new OnlineCard {Token=9999,Kind=kind,Price=CardCatalog.Cost(kind)}};
            float normalCardWidth=0;
            foreach(int count in new[]{10,11,25,40}) {
                self.Hand=Enumerable.Range(0,count).Select(i=>new OnlineCard {Token=2000+i,Kind=CharacterData.SupportKinds.ElementAt(i%CharacterData.SupportKinds.Count()),Generated=i>=10}).ToArray();
                state.Revision++;shop.ApplyNetworkState(state,self.UserId);await Frames();
                await ((BattleDisplay)field).WaitForPhaseTransition();await Frames();
                Check(shop.Hand.Count==count&&shop.PurchasedHandCount==10,"Incoming generated origin/count lost");
                Check(!shop.AcceptsToken(9999),"Eleventh purchased card allowed");
                var cards=shop.HandRow.GetChildren().OfType<ShopCard>().Where(c=>c.Visible).ToArray();
                Check(cards.Length==count,"Overflow cards hidden");
                if(count==10)normalCardWidth=cards[0].Size.X;
                var row=shop.HandRow.GetGlobalRect();
                var surface=shop.ZoneB.GetNode<Panel>("Surface").GetGlobalRect();
                Check(row.Position.X>=surface.Position.X&&row.End.X<=surface.End.X,"Hand overlaps player profiles count="+count+" row="+row+" surface="+surface);
                for(int i=0;i<cards.Length;i++) {
                    var rect=cards[i].GetGlobalRect();
                    Check(rect.Position.X>=row.Position.X-1&&rect.End.X<=row.End.X+1,"Hand extends outside row");
                    Check(cards[i].Size.X>=normalCardWidth-1,"Overflow shrinks cards");
                    if(i>0)Check(cards[i].Position.X>cards[i-1].Position.X&&cards[i].Position.X<cards[i-1].Position.X+cards[i-1].Size.X+5,"Stack ordering invalid");
                }
                foreach(int index in new[]{0,count/2,count-1}) {
                    var card=cards[index];
                    float exposed=index+1<count?cards[index+1].Position.X-card.Position.X:card.Size.X;
                    var point=card.GetGlobalRect().Position+new Vector2(exposed/2,card.Size.Y/2);
                    GetViewport().PushInput(new InputEventMouseMotion {Position=point,GlobalPosition=point},true);await Frames(1);
                    Check(GetViewport().GuiGetHoveredControl()==card,"Covered card intercepted exposed card input");
                    GetViewport().PushInput(new InputEventMouseButton {Position=point,GlobalPosition=point,ButtonIndex=MouseButton.Left,Pressed=true},true);
                    GetViewport().PushInput(new InputEventMouseButton {Position=point,GlobalPosition=point,ButtonIndex=MouseButton.Left,Pressed=false},true);await Frames(1);
                    Check(shop.Details.Visible,"Stacked card cannot open details");shop.CloseDetails();
                }
                if(count>10)Check(shop.HandRow.GetThemeConstant("separation")<0,"Overflow does not overlap");
                cards[0].EmitSignal(Control.SignalName.MouseEntered);Check(cards[0].ZIndex==20,"Hovered card not brought forward");
                cards[0].EmitSignal(Control.SignalName.MouseExited);Check(cards[0].ZIndex==0,"Hover order not restored");
                Check(shop.AcceptsHandToken(cards[0].Token)&&shop.AcceptsHandToken(cards[^1].Token),"Overflow cards cannot be dragged");
                if(count==25&&DisplayServer.GetName()!="headless") {
                    await ToSignal(RenderingServer.Singleton,RenderingServer.SignalName.FramePostDraw);
                    GetViewport().GetTexture().GetImage().SavePng("res://output/hand-overflow"+(MobileLayout.Enabled?"-mobile":"")+".png");
                }
            }
            self.Hand[0].Generated=true;state.Revision++;shop.ApplyNetworkState(state,self.UserId);await Frames();
            Check(shop.PurchasedHandCount==9&&shop.AcceptsToken(9999),"Generated cards blocked available shop capacity");
            int before=shop.Hand.Count;Check(shop.AddGeneratedCard(kind)&&shop.Hand.Count==before+1&&shop.Hand[^1].Generated,"Offline skill cannot overflow");
            self.Hand=self.Hand.Take(3).ToArray();state.Revision++;shop.ApplyNetworkState(state,self.UserId);await Frames();
            Check(shop.HandRow.GetThemeConstant("separation")==4,"Spacing not restored after overflow");
            Check(shop.HandRow.GetChildren().OfType<Panel>().Count(c=>c.Visible)==3,"Empty slots not restored");
            opponent.HandCount=27;state.Phase="battle";state.Revision++;shop.ApplyNetworkState(state,self.UserId);await Frames();
            Check(shop.OpponentHandRow.GetChildren().OfType<TextureRect>().Count(c=>c.Visible)==27,"Opponent overflow truncated");
            GD.Print("HAND OVERFLOW PASS: shop capacity, generated origin, 10/11/25/40 cards, overlap, hover, drag eligibility, recovery, opponent overflow");
            GetTree().Quit();
        } catch(Exception e) {GD.PushError(e.ToString());GetTree().Quit(1);}
    }
}


