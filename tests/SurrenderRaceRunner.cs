using Godot;
using System;
using System.Linq;
using System.Reflection;
using System.Collections.Concurrent;
using System.Threading.Tasks;

public partial class SurrenderRaceRunner : Node
{
    private async Task Frame()=>await ToSignal(GetTree(),SceneTree.SignalName.ProcessFrame);
    private async Task Wait(Func<bool> condition) {ulong deadline=Time.GetTicksMsec()+15000;while(!condition()){if(Time.GetTicksMsec()>deadline)throw new Exception("Timed out");await Frame();}}
    public override void _Ready()=>Callable.From(Run).CallDeferred();
    private async void Run()
    {
        try {
            GetTree().CurrentScene=null;
            for(int scenario=0;scenario<2;scenario++) {
                // Use a fresh debug guest, preserving the player's account/profile.
                BattleLaunch.Deck=null;BattleLaunch.Pending=false;BattleLaunch.Matchmaking=false;
                GetTree().ChangeSceneToFile("res://scenes/main.tscn");await Frame();await Frame();
                var online=GetTree().CurrentScene.GetNode<OnlineBattle>("OnlineBattle");await Wait(()=>online.Connected);
                online.CreateRoom();await Wait(()=>online.State!=null);var transport=online.Connection!;
                online.ShowSurrender();var modal=online.FindChildren("*","",true,false).OfType<GameModal>().First();
                modal.Cancel!();await Frame();if(modal.Visible || !online.Connected)throw new Exception("Normal cancel must remain in match");online.ShowSurrender();
                var queue=(ConcurrentQueue<OnlineState>)typeof(OnlineBattle).GetField("_states",BindingFlags.NonPublic|BindingFlags.Instance)!.GetValue(online)!;
                queue.Enqueue(new OnlineState {Revision=online.State!.Revision+100,Phase="game_over",WinnerId=online.UserId,Roster=new[]{new OnlineStanding {UserId=online.UserId,Hp=30,Place=1}},Players=online.State.Players});
                await Wait(()=>online.State?.Phase=="game_over");await Frame();
                var user=(await transport.Client.GetUsersAsync(transport.Session,new[]{online.UserId})).Users.First();
                string expected=string.IsNullOrWhiteSpace(user.DisplayName)?user.Username:user.DisplayName;
                var text=new System.Globalization.StringInfo(expected);if(text.LengthInTextElements>14)expected=text.SubstringByTextElements(0,13)+"…";
                var roster=(Label)typeof(OnlineBattle).GetField("_roster",BindingFlags.NonPublic|BindingFlags.Instance)!.GetValue(online)!;
                await Wait(()=>roster.Text.Contains(expected));
                var button=GetTree().CurrentScene.GetNode<TextureButton>("UI/SafeArea/Content/MatchLobbyButton");
                if(modal.Visible || !button.Visible)throw new Exception("Final result must replace surrender dialog with lobby exit");
                if(scenario==0) {await ToSignal(RenderingServer.Singleton,RenderingServer.SignalName.FramePostDraw);GetViewport().GetTexture().GetImage().SavePng("/tmp/riftbound-surrender-match-end.png");modal.Cancel!();}
                else button.EmitSignal(BaseButton.SignalName.Pressed);
                await Wait(()=>GetTree().CurrentScene is Lobby);if(transport.MatchId!="")throw new Exception("Match connection leaked");
            }
            GD.Print("SURRENDER RACE PASS: normal cancel, final result closes modal, late cancel and lobby button both disconnect and return");GetTree().Quit();
        } catch(Exception e) {GD.PushError(e.ToString());GetTree().Quit(1);}
    }
}
