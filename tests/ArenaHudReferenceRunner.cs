using Godot;
using System;
using System.Linq;
using System.Collections.Generic;
public partial class ArenaHudReferenceRunner : Node
{
    private async System.Threading.Tasks.Task Frames(){for(int i=0;i<5;i++)await ToSignal(GetTree(),SceneTree.SignalName.ProcessFrame);}
    public override async void _Ready()
    {
        try {
            var field=GD.Load<PackedScene>("res://scenes/main.tscn").Instantiate<BattleDisplay>();field.GetNode("OnlineBattle").Free();field.GetNode<BattleDemo>("BattleDemo").NetworkEnabled=true;field.GetNode<BattleDemo>("BattleDemo").AutoStart=false;AddChild(field);
            var shop=field.GetNode<CardShop>("UI/SafeArea/Content/CardShop");
            var state=System.Text.Json.JsonSerializer.Deserialize(FileAccess.GetFileAsString("res://tests/combat_fixture.json"),GameJsonContext.Default.OnlineState)!;
            state.Players[0]!.Offers=Enumerable.Range(0,5).Select(i=>new OnlineCard {Kind=460+i,Token=800+i,Stars=1,Price=2}).ToArray();
            state.Phase="preparation";state.Players[0]!.UserId="self";
            state.Roster=Enumerable.Range(0,6).Select(i=>new OnlineStanding {UserId=i==0?"self":"bot"+i,Hp=30-i,Bot=i>0}).ToArray();
            var panel=new PanelContainer {Position=new(16,76)};panel.AddThemeStyleboxOverride("panel",GameUi.Box());field.GetNode("UI/SafeArea/Content").AddChild(panel);
            var roster=new ArenaRoster();panel.AddChild(roster);roster.UpdateRoster(state,"self",new Dictionary<string,string>{{"self","123"}});
            foreach(int count in new[]{0,1,6,10}) {
                state.Players[0]!.Hand=Enumerable.Range(0,count).Select(i=>new OnlineCard {Kind=460+i,Token=900+i,Stars=1,Price=2}).ToArray();
                shop.ApplyNetworkState(state,"self");await Frames();
                var lastOffer=shop.ShopRow.GetChildren().OfType<ShopCard>().Last();
                if(lastOffer.GetGlobalRect().End.X+8>shop.ZoneA.GetNode<Control>("Upgrade").GlobalPosition.X)throw new Exception("Shop cards overlap actions");
                var ready=shop.GetNode<TextureButton>("BattleActions/BattleButton");
                bool disabledBefore=ready.Disabled;
                shop.NetworkPending=true;shop.RefreshNetworkControls();await Frames();
                if(ready.Disabled!=disabledBefore)throw new Exception("Ready flickers during pending card action");
                shop.NetworkPending=false;shop.RefreshNetworkControls();
                shop.UpdateCountdown(40,true);await Frames();
                var timer=ready.GetNode<PreparationCountdown>("PreparationCountdown");
                var caption=ready.GetNode<Label>("Text");
                if(caption.Text!="READY"||timer.Seconds!=40||!ready.GetGlobalRect().Encloses(timer.GetGlobalRect())||timer.GlobalPosition.X<caption.GetGlobalRect().End.X)throw new Exception("Ready countdown layout invalid");
                int empty=shop.HandRow.GetChildren().OfType<Panel>().Count(p=>p.Visible&&p.Name.ToString().StartsWith("EmptyHandSlot"));
                if(empty!=Math.Max(6,count)-count)throw new Exception("Wrong placeholder count: "+count);
                foreach(var node in shop.HandRow.GetChildren().OfType<Control>().Where(c=>c.Visible))
                    if(node.GetGlobalRect().Position.X<shop.HandRow.GlobalPosition.X-1||node.GetGlobalRect().End.X>shop.HandRow.GetGlobalRect().End.X+1)throw new Exception("Hand overflows");
                if(count==0&&DisplayServer.GetName()!="headless") {
                    await field.WaitForPhaseTransition();await ToSignal(RenderingServer.Singleton,RenderingServer.SignalName.FramePostDraw);GetViewport().GetTexture().GetImage().SavePng("/tmp/riftbound-hud-reference.png");
                }
            }
            shop.ShowCardDetails(800);await Frames();
            var preview=shop.Details.GetNode<CardIdlePortrait>("IdlePreview");
            if(!preview.IsVisibleInTree()||preview.IdleFrameCount<2||preview.GetGlobalRect().End.X>=shop.DetailsText.GlobalPosition.X)throw new Exception("Idle preview must occupy the left column");
            if(shop.Details.Size.X<=shop.Details.Size.Y)throw new Exception("Details modal must be landscape");
            var hint=shop.ShopRow.GetChild<ShopCard>(0).GetNodeOrNull<AnimatedSprite2D>("UpgradeHint");
            if(hint==null||!hint.Visible||!hint.IsPlaying())throw new Exception("Upgradeable offer sparkle missing");
            if(DisplayServer.GetName()!="headless"){await ToSignal(RenderingServer.Singleton,RenderingServer.SignalName.FramePostDraw);GetViewport().GetTexture().GetImage().SavePng("/tmp/riftbound-card-details.png");}
            shop.CloseDetails();
            if(roster.GetChildCount()!=6||roster.GetChildren().OfType<PanelContainer>().Count(c=>c.HasThemeStyleboxOverride("panel"))!=6)throw new Exception("Roster rows missing");
            if(panel.Size.X>320)throw new Exception("Roster too wide");
            GD.Print("ARENA HUD REFERENCE PASS: active roster, portraits, hearts, compact width, empty/partial/full hand placeholders, hand limit preserved");GetTree().Quit();
        }catch(Exception e){GD.PushError(e.ToString());GetTree().Quit(1);}
    }
}
