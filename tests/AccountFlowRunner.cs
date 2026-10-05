using Godot;
using System;
using System.Linq;
using System.Threading.Tasks;
using System.Reflection;
using System.Collections.Concurrent;

public partial class AccountFlowRunner : Node
{
    public override void _Ready()=>Callable.From(Run).CallDeferred();
    private async Task Frames(int count=4){for(int i=0;i<count;i++)await ToSignal(GetTree(),SceneTree.SignalName.ProcessFrame);}
    private async Task Wait(Func<bool> condition,string error){ulong deadline=Time.GetTicksMsec()+15000;while(!condition()){if(Time.GetTicksMsec()>deadline)throw new Exception(error);await Frames(1);}}
    private async Task Capture(string name){await Frames(10);await ToSignal(RenderingServer.Singleton,RenderingServer.SignalName.FramePostDraw);GetViewport().GetTexture().GetImage().SavePng("/tmp/riftbound-"+name+".png");}
    private async Task<OnlineBattle> EnterArena()
    {
        BattleLaunch.Deck=DeckDefinition.Starter();BattleLaunch.Pending=true;BattleLaunch.Create=true;BattleLaunch.Matchmaking=false;
        GetTree().ChangeSceneToFile("res://scenes/main.tscn");await Frames();
        var online=GetTree().CurrentScene.GetNode<OnlineBattle>("OnlineBattle");await Wait(()=>online.State!=null,"Arena connection failed");return online;
    }
    private async void Run()
    {
        try {
            GetTree().CurrentScene=null;
            var welcome=GD.Load<PackedScene>("res://scenes/welcome.tscn").Instantiate<Welcome>();AddChild(welcome);await Capture("welcome");
            welcome.LoginGuest();await Wait(()=>welcome.FindChildren("*","",true,false).OfType<GameModal>().Any(m=>m.Visible),"Guest login popup failed");
            var modal=welcome.FindChildren("*","",true,false).OfType<GameModal>().First();var input=modal.Body.GetChildren().OfType<LineEdit>().First();
            input.Text=" ";welcome.ConfirmName();await Frames();if(GetTree().CurrentScene!=null)throw new Exception("Blank name accepted");
            input.Text="Guest Commander";await Capture("guest-name");welcome.ConfirmName();await Wait(()=>GetTree().CurrentScene is Lobby,"Name confirmation did not reach lobby");welcome.QueueFree();await Frames();
            var account=GameAccount.Connection();await account.Connect();var profile=await account.Client.GetAccountAsync(account.Session);if(profile.User.DisplayName!="Guest Commander")throw new Exception("Guest name not persisted");await account.Close();
            var online=await EnterArena();var transport=online.Connection!;online.ShowSurrender();await Capture("surrender");
            var confirmation=online.FindChildren("*","",true,false).OfType<GameModal>().First();confirmation.Cancel!();await Frames();if(!online.Connected || confirmation.Visible || transport.MatchId=="")throw new Exception("Cancel left room");
            online.ShowSurrender();var surrender=confirmation.Body.GetChildren().OfType<HBoxContainer>().First().GetChildren().OfType<TextureButton>().Last();surrender.EmitSignal(BaseButton.SignalName.Pressed);
            await Wait(()=>GetTree().CurrentScene is Lobby,"Surrender did not return to lobby");if(transport.MatchId!="")throw new Exception("Surrender did not leave room");
            online=await EnterArena();transport=online.Connection!;
            online.ShowSurrender();confirmation=online.FindChildren("*","",true,false).OfType<GameModal>().First();
            var finalStates=(ConcurrentQueue<OnlineState>)typeof(OnlineBattle).GetField("_states",BindingFlags.NonPublic|BindingFlags.Instance)!.GetValue(online)!;
            finalStates.Enqueue(new OnlineState {Revision=online.State!.Revision+1,Phase="game_over",WinnerId=online.UserId,Roster=new[]{new OnlineStanding {UserId=online.UserId,Hp=30,Place=1}},Players=online.State.Players});
            await Wait(()=>online.State?.Phase=="game_over","Final result not received");await Frames();
            var lobbyButton=GetTree().CurrentScene.GetNode<TextureButton>("UI/SafeArea/Content/MatchLobbyButton");
            if(confirmation.Visible || !lobbyButton.Visible)throw new Exception("Match end must close surrender dialog and expose lobby exit");
            await Capture("match-end-during-surrender");confirmation.Cancel!();
            await Wait(()=>GetTree().CurrentScene is Lobby,"Cancel racing match end did not return to lobby");if(transport.MatchId!="")throw new Exception("Completed match did not disconnect");
            online=await EnterArena();transport=online.Connection!;
            var states=(ConcurrentQueue<OnlineState>)typeof(OnlineBattle).GetField("_states",BindingFlags.NonPublic|BindingFlags.Instance)!.GetValue(online)!;
            states.Enqueue(new OnlineState {Revision=online.State!.Revision+1,Phase="eliminated",Roster=new[]{new OnlineStanding {UserId=online.UserId,Hp=0}},Players=online.State.Players});
            await Wait(()=>GetTree().CurrentScene is Lobby,"Elimination did not return to lobby");if(transport.MatchId!="")throw new Exception("Elimination did not leave room");
            GD.Print("ACCOUNT FLOW PASS: guest authentication, name validation and server persistence, surrender confirmation/cancel, room leave, HP-zero lobby return");GetTree().Quit();
        }catch(Exception e){GD.PushError(e.ToString());GetTree().Quit(1);}
    }
}
