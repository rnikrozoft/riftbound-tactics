using Godot;
using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;

public partial class ReplayHistoryRunner : Node
{
    private int _checks;
    private void Check(bool ok,string message){_checks++;if(!ok)throw new Exception(message);}
    private async Task Frames(int count=2){for(int i=0;i<count;i++)await ToSignal(GetTree(),SceneTree.SignalName.ProcessFrame);}
    public override void _Ready()=>Callable.From(Run).CallDeferred();
    private async void Run()
    {
        string path=ProjectSettings.GlobalizePath($"user://replays-test-{Guid.NewGuid():N}.json");
        MatchReplayStore.TestPath=path;
        try {
            MatchReplayStore.Clear();
            MatchReplayStore.Record(new OnlineState {Round=1,Battle=new OnlinePlan()},"effects-a");
            MatchReplayStore.RecordPreparation(new OnlineState {Round=1,Phase="preparation"},"effects-a");
            Check(!File.Exists(path)&&MatchReplayStore.Rounds.Count==0&&MatchReplayStore.Preparations.Count==0,"Missing player data must not create a replay");
            var fixture=JsonSerializer.Deserialize(Godot.FileAccess.GetFileAsString("res://tests/combat_fixture.json"),GameJsonContext.Default.OnlineState)!;
            long played=DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            for(int i=0;i<11;i++){
                MatchReplayStore.Clear();var state=MatchReplayStore.Clone(fixture);state.Round=1;state.Code=$"game-{i}";state.ServerMs=played+i*1000;
                state.Roster=new[]{new OnlineStanding {UserId="effects-a",Hp=30,Rating=1000},new OnlineStanding {UserId="effects-b",Hp=30,Rating=1000}};
                state.Phase="preparation";MatchReplayStore.RecordPreparation(state,"effects-a");
                MatchReplayStore.RecordPreparation(state,"effects-a");Check(MatchReplayStore.Preparations.Count==1,"Duplicate preparation broadcasts must be ignored");
                state.ServerMs+=100;state.Players.First(p=>p?.UserId=="effects-a")!.Coins=7;MatchReplayStore.RecordPreparation(state,"effects-a");
                state.ServerMs+=200;state.Players.First(p=>p?.UserId=="effects-a")!.Units[0].Slot=2;MatchReplayStore.RecordPreparation(state,"effects-a");
                Check(MatchReplayStore.Preparations.Count==3,"Shop budget and formation changes must be recorded");state.Phase="battle";
                MatchReplayStore.Record(state,"effects-a");MatchReplayStore.Record(state,"effects-a");
                Check(MatchReplayStore.Rounds.Count==1,"Duplicate broadcasts must not duplicate a round");
                state=MatchReplayStore.Clone(state);state.Round=2;state.Battle!.Round=2;MatchReplayStore.Record(state,"effects-a");
                state.Phase="preparation";MatchReplayStore.RecordPreparation(state,"effects-a");state.Phase="battle";
                Check(MatchReplayStore.Rounds.Count==2,"All rounds belong to same game");
                state.Phase="game_over";state.WinnerId="effects-b";state.Roster[0].Hp=0;state.Roster[0].Place=6;state.MmrPending=true;
                MatchReplayStore.Observe(state,"effects-a");
                Check(MatchReplayStore.History().First().MmrPending,"Pending MMR must be marked");
                state.MmrPending=false;state.MmrDelta=-20;MatchReplayStore.Observe(state,"effects-a");
                var game=MatchReplayStore.History().First();Check(game.MmrDelta==-20&&!game.MmrPending&&game.Result=="LOSS"&&game.Place==6,"Settled server outcome must update same game");
            }
            var history=MatchReplayStore.History();Check(history.Count==10,"Replay capacity is ten games");
            Check(history.First().Code=="game-10"&&history.Last().Code=="game-1","Newest game replaces oldest");
            Check(history.All(g=>g.Rounds.Count==2&&g.UserId=="effects-a"&&g.PlayedAt>0),"Persisted metadata and round plans");
            Check(history.All(g=>g.Preparations.Count==4),"Preparation steps must persist alongside all battles");
            Check(history.First().MmrText.Contains("980"),"MMR before, delta and after");
            MatchReplayStore.Clear();Check(MatchReplayStore.History().Count==10,"History survives clearing active match");
            var latest=MatchReplayStore.History().First();MatchReplayStore.Select(latest);
            Check(MatchReplayStore.FromHistory&&MatchReplayStore.Rounds.Count==2,"Selecting saved game restores rounds and perspective");
            MatchReplayStore.Rounds[0].Round=99;Check(MatchReplayStore.History().First().Rounds[0].Round==1,"Playback must not mutate archive");
            MatchReplayStore.Clear();
            var lobby=GD.Load<PackedScene>("res://scenes/lobby.tscn").Instantiate<Lobby>();AddChild(lobby);lobby.Navigate("Replays");await Frames(3);
            Check(lobby.ActiveMenu=="Replays","Replay menu is reachable from lobby");
            if(DisplayServer.GetName()!="headless"){
                await ToSignal(RenderingServer.Singleton,RenderingServer.SignalName.FramePostDraw);
                GetViewport().GetTexture().GetImage().SavePng("res://tests/combat-replay-history.png");
            }
            lobby.QueueFree();await Frames();
            latest=MatchReplayStore.History().First();
            var plan=latest.Rounds[0].Battle!;var first=plan.Events[0];first.AtMs=0;plan.StartMs=0;plan.Events=new[]{first};
            latest.Rounds[1].Battle!.Events=Array.Empty<OnlineCombatEvent>();
            var finalPreparation=MatchReplayStore.Clone(latest.Preparations.Last());finalPreparation.Round=3;latest.Preparations.Add(finalPreparation);
            MatchReplayStore.Select(latest);var viewer=GD.Load<PackedScene>("res://scenes/match_replay.tscn").Instantiate<MatchReplayViewer>();AddChild(viewer);await Frames();
            var speed=viewer.FindChildren("*","",true,false).OfType<BattleSpeedControl>().Single();speed.CycleSpeed();await Frames();
            long speedClock=viewer.PlaybackClockMs;ulong speedStart=Time.GetTicksMsec();
            while(Time.GetTicksMsec()-speedStart<200)await Frames(1);
            Check(viewer.PlaybackClockMs-speedClock>300,"Double speed must accelerate the replay timeline clock");
            viewer.TogglePlayback();Check(viewer.IsPaused,"Pause control pauses replay");
            long pausedClock=viewer.PlaybackClockMs;var pausedUnit=viewer.FindChildren("*","",true,false).OfType<BattleUnit>().First();var pausedPosition=pausedUnit.Position;int pausedFrame=pausedUnit.Sprite.Frame;
            ulong pausedUntil=Time.GetTicksMsec()+400;while(Time.GetTicksMsec()<pausedUntil)await Frames(1);
            Check(viewer.PlaybackClockMs==pausedClock&&pausedUnit.Position==pausedPosition&&pausedUnit.Sprite.Frame==pausedFrame,"Pause freezes replay time, movement and animation");
            viewer.TogglePlayback();Check(!viewer.IsPaused,"Play resumes paused replay");
            long until=(long)Time.GetTicksMsec()+20000;
            bool reachedNextBattle=false;
            while(viewer.PlaybackStatus!="REPLAY COMPLETE"&&(long)Time.GetTicksMsec()<until){
                if(viewer.SelectedRound==2&&!viewer.ShowingPreparation)reachedNextBattle=true;
                await Frames(1);
            }
            Check(viewer.PlaybackStatus=="REPLAY COMPLETE","Saved replay must play without server connection");
            Check(reachedNextBattle,"Playback must automatically advance from battle through the next round to its battle");
            Check(viewer.SelectedRound==3&&viewer.ShowingPreparation,"Replay ending with preparation must keep the last recorded point selected");
            await Frames();Check(Engine.TimeScale==1,"Replay completion restores normal UI speed");
            Check(!viewer.FindChildren("*","",true,false).OfType<OptionButton>().Any(),"Replay navigation must have no dropdowns");
            var timeline=viewer.FindChildren("*","",true,false).OfType<ReplayTimeline>().Single();
            Check(timeline.PointCount==5,"Timeline must have one preparation point and one battle point per recorded round, regardless of preparation step count");
            Check(Math.Abs(timeline.PointPosition(3,true).X-(timeline.Size.X-46))<1,"Timeline points must span the available panel width");
            Check(viewer.ReplayDrawer.FindChildren("*","",true,false).OfType<TextureButton>().Count()==1,"Drawer has only one Play/Pause button");
            var preparationBackup=MatchReplayStore.Preparations.ToArray();MatchReplayStore.Preparations.Clear();
            var olderTimeline=new ReplayTimeline();AddChild(olderTimeline);olderTimeline.Build(new[]{1,99});
            Check(olderTimeline.PointCount==1,"Older battle-only replay must not show preparation points");olderTimeline.QueueFree();MatchReplayStore.Preparations.AddRange(preparationBackup);
            timeline._GuiInput(new InputEventMouseButton {ButtonIndex=MouseButton.Left,Pressed=true,Position=timeline.PointPosition(1,true)});
            Check(viewer.ShowingPreparation&&viewer.SelectedStep==0,"Clicking preparation must start at the first recorded preparation step");
            Check(viewer.IsPlaying&&!viewer.IsPaused,"Timeline click must immediately play from selected preparation");
            viewer.SelectSection(1,true,1);await Frames();
            Check(viewer.ShowingPreparation&&viewer.SelectedRound==1&&viewer.SelectedStep==1,"Round, phase and preparation step selection");
            var shop=viewer.FindChildren("*","",true,false).OfType<CardShop>().Single();
            Check(shop.Coins==7&&shop.NetworkPending&&shop.ZoneA.Visible,"Read-only preparation must show recorded shop and coins");
            if(DisplayServer.GetName()!="headless"){
                await ToSignal(RenderingServer.Singleton,RenderingServer.SignalName.FramePostDraw);
                GetViewport().GetTexture().GetImage().SavePng("res://tests/combat-replay-preparation.png");
                viewer.ToggleDrawer();ulong foldUntil=Time.GetTicksMsec()+350;while(Time.GetTicksMsec()<foldUntil)await Frames(1);
                await ToSignal(RenderingServer.Singleton,RenderingServer.SignalName.FramePostDraw);
                GetViewport().GetTexture().GetImage().SavePng("res://tests/combat-replay-collapsed.png");
                viewer.ToggleDrawer();ulong openUntil=Time.GetTicksMsec()+350;while(Time.GetTicksMsec()<openUntil)await Frames(1);
            }
            viewer.SelectSection(2,true,0);Check(viewer.SelectedRound==2&&viewer.ShowingPreparation,"Can jump directly to another round preparation");
            timeline._GuiInput(new InputEventMouseButton {ButtonIndex=MouseButton.Left,Pressed=true,Position=timeline.PointPosition(1,false)});
            Check(!viewer.ShowingPreparation&&viewer.SelectedRound==1,"Clicking battle diamond must select battle");
            Check(viewer.IsPlaying&&!viewer.IsPaused,"Timeline click must immediately play selected battle");
            viewer.SelectSection(1,false);viewer.PlaySelected();await Frames();viewer.SelectSection(1,true,2);await Frames(20);
            Check(viewer.ShowingPreparation&&viewer.SelectedStep==2&&viewer.PlaybackStatus.Contains("PREPARATION"),"Switching section cancels old battle playback safely");
            viewer.QueueFree();await Frames();
            Check(Engine.TimeScale==1,"Leaving replay restores normal engine speed");
            GD.Print($"PASS replay history: {_checks} checks");GetTree().Quit(0);
        }catch(Exception e){GD.PushError(e.ToString());GetTree().Quit(1);}
        finally{if(File.Exists(path))File.Delete(path);MatchReplayStore.TestPath=null;}
    }
}
