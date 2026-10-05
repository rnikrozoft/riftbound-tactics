using Godot;
using System;
using System.Linq;
using System.Reflection;
using System.Collections.Concurrent;
using System.Threading.Tasks;

public partial class DefeatPopupRunner : Node
{
    private async Task Frame()=>await ToSignal(GetTree(),SceneTree.SignalName.ProcessFrame);
    private async Task Wait(Func<bool> condition){ulong end=Time.GetTicksMsec()+20000;while(!condition()){if(Time.GetTicksMsec()>end)throw new Exception("Timeout");await Frame();}}
    public override void _Ready()=>Callable.From(Run).CallDeferred();
    private async void Run()
    {
        try {
            GetTree().CurrentScene=null;
            for(int scenario=0;scenario<2;scenario++) {
                BattleLaunch.Deck=null;BattleLaunch.Pending=false;BattleLaunch.Matchmaking=false;
                GetTree().ChangeSceneToFile("res://scenes/main.tscn");await Frame();await Frame();
                var online=GetTree().CurrentScene.GetNode<OnlineBattle>("OnlineBattle");await Wait(()=>online.Connected);online.CreateRoom();await Wait(()=>online.State!=null);
                var transport=online.Connection!;
                MatchReplayStore.Record(new OnlineState {Round=1,Players=new OnlinePlayer?[]{new(){UserId=online.UserId,Team="A",Units=new[]{new OnlineUnit {Kind=0,Token=1,Slot=0}}},new(){UserId="enemy",Team="B",Units=new[]{new OnlineUnit {Kind=40,Token=2,Slot=0}}}},Battle=new OnlinePlan {Round=1,Winner="B",Events=new[]{new OnlineCombatEvent {Attacker="A:1",Target="B:2",Damage=10,TargetHp=90}}}},online.UserId);
                online.ShowSurrender();
                var queue=(ConcurrentQueue<OnlineState>)typeof(OnlineBattle).GetField("_states",BindingFlags.NonPublic|BindingFlags.Instance)!.GetValue(online)!;
                queue.Enqueue(new OnlineState {Revision=online.State!.Revision+100,Phase="game_over",WinnerId="enemy",Roster=new[]{new OnlineStanding {UserId=online.UserId,Hp=0,Place=2}},Players=online.State.Players});
                await Wait(()=>online.FindChildren("DefeatModal","",true,false).Count>0);
                var modal=online.FindChildren("DefeatModal","",true,false).OfType<GameModal>().Single();
                if(modal.GetNode<TextureRect>("DefeatEffect").Texture==null)throw new Exception("Missing effect");
                var buttons=modal.FindChildren("*","",true,false).OfType<TextureButton>().ToArray();if(buttons.Length!=2)throw new Exception("Expected two buttons");
                if(scenario==0){ulong begin=Time.GetTicksMsec();await ToSignal(RenderingServer.Singleton,RenderingServer.SignalName.FramePostDraw);GetViewport().GetTexture().GetImage().SavePng("/tmp/riftbound-defeat-popup.png");await Wait(()=>GetTree().CurrentScene is Lobby);if(Time.GetTicksMsec()-begin<14500)throw new Exception("Countdown ended early");if(transport.MatchId!="")throw new Exception("Connection leaked");}
                else {
                    buttons.Single(b=>b.Name=="WatchReplayButton").EmitSignal(BaseButton.SignalName.Pressed);
                    await Wait(()=>GetTree().CurrentScene is MatchReplayViewer);
                    await Wait(()=>GetTree().CurrentScene.FindChildren("*","",true,false).OfType<Label>().Any(l=>l.Text=="REPLAY COMPLETE"));
                    var shop=GetTree().CurrentScene.FindChildren("*","",true,false).OfType<CardShop>().Single();if(shop.NetworkUnits["B:2"].Health!=90)throw new Exception("Replay damage not applied");if(transport.MatchId!="")throw new Exception("Replay connection leaked");
                }
            }
            GD.Print("DEFEAT POPUP PASS: two buttons, effect, automatic lobby, offline replay, disconnected room");GetTree().Quit();
        } catch(Exception e){GD.PushError(e.ToString());GetTree().Quit(1);}
    }
}
