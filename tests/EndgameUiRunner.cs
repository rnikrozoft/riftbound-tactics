using Godot;
using System;
using System.Linq;
using System.Reflection;
using System.Collections.Concurrent;
using System.Threading.Tasks;
using System.IO;

public partial class EndgameUiRunner : Node
{
    private int _checks;
    private void Check(bool ok,string message){_checks++;if(!ok)throw new Exception(message);}
    private async Task Wait(Func<bool> ready,int milliseconds=20000){ulong end=Time.GetTicksMsec()+(ulong)milliseconds;while(!ready()){if(Time.GetTicksMsec()>end)throw new Exception("Timed out");await ToSignal(GetTree(),SceneTree.SignalName.ProcessFrame);}}
    public override void _Ready()=>Callable.From(Run).CallDeferred();
    private async void Run()
    {
        OnlineBattle? online=null;
        string testPath=ProjectSettings.GlobalizePath($"user://endgame-test-{Guid.NewGuid():N}.json");MatchReplayStore.TestPath=testPath;
        try {
            BattleLaunch.Pending=false;BattleLaunch.Matchmaking=false;BattleLaunch.Deck=null;
            var field=GD.Load<PackedScene>("res://scenes/main.tscn").Instantiate<BattleDisplay>();AddChild(field);
            online=field.GetNode<OnlineBattle>("OnlineBattle");await Wait(()=>online.Connected);online.CreateRoom();await Wait(()=>online.State!=null);
            var errors=(ConcurrentQueue<OnlineError>)typeof(OnlineBattle).GetField("_errors",BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(online)!;
            errors.Enqueue(new OnlineError {Message="preparation is locked"});
            var result=field.GetNode<Control>("UI/SafeArea/Content/BattleResult");await Wait(()=>result.Visible);
            Check(result.GetNode<Label>("Text").Text=="PREPARATION IS LOCKED","Locked preparation message must appear in centered result popup");
            field.GetNode<BattleDemo>("BattleDemo").SpeedEnabled=true;
            var speed=field.GetNode<BattleSpeedControl>("BattleSpeedControl");speed.CycleSpeed();speed.CycleSpeed();await Wait(()=>Engine.TimeScale==4);
            var states=(ConcurrentQueue<OnlineState>)typeof(OnlineBattle).GetField("_states",BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(online)!;
            states.Enqueue(new OnlineState {Revision=online.State!.Revision+100,Phase="game_over",WinnerId="enemy",Roster=new[]{new OnlineStanding {UserId=online.UserId,Hp=0,Place=6}},Players=online.State.Players});
            await Wait(()=>online.FindChildren("DefeatModal","",true,false).Count>0);
            var modal=online.FindChildren("DefeatModal","",true,false).OfType<GameModal>().Single();var shade=modal.GetChild<Control>(0);
            var buttons=modal.FindChildren("*","",true,false).OfType<TextureButton>().ToArray();
            Check(shade.Modulate.A<.05f&&buttons.All(b=>b.Disabled),"Defeat popup starts invisible and cannot be clicked during fade");
            ulong start=Time.GetTicksMsec();await Wait(()=>Time.GetTicksMsec()-start>=2500,6000);
            Check(shade.Modulate.A>.3f&&shade.Modulate.A<.7f,"Defeat popup must fade gradually at midpoint");
            await Wait(()=>Time.GetTicksMsec()-start>=5200,6000);
            Check(shade.Modulate.A==1&&!buttons.First(b=>b.Name=="DefeatLobbyButton").Disabled,"Lobby button enabled after five-second fade");
            if(DisplayServer.GetName()!="headless"){
                await ToSignal(RenderingServer.Singleton,RenderingServer.SignalName.FramePostDraw);GetViewport().GetTexture().GetImage().SavePng("res://tests/combat-defeat-popup.png");
            }
            await online.Connection!.Close();GD.Print($"PASS endgame UI: {_checks} checks");GetTree().Quit();
        }catch(Exception e){if(online?.Connection!=null)await online.Connection.Close();GD.PushError(e.ToString());GetTree().Quit(1);}
        finally{if(File.Exists(testPath))File.Delete(testPath);MatchReplayStore.TestPath=null;}
    }
}
