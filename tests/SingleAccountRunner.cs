using Godot;
using System;
using System.Threading.Tasks;

public partial class SingleAccountRunner : Node
{
    private async Task Wait(Func<bool> condition,string message)
    {
        ulong end=Time.GetTicksMsec()+20000;
        while(!condition()) {
            if(Time.GetTicksMsec()>end)throw new Exception(message);
            await ToSignal(GetTree(),SceneTree.SignalName.ProcessFrame);
        }
    }
    public override void _Ready()=>Callable.From(Run).CallDeferred();
    private async void Run()
    {
        NakamaConnection? first=null,second=null;
        try {
            bool realtime=OS.GetEnvironment("RIFT_SINGLE_ACCOUNT_MODE")!="http";
            string id="rift-single-account-test-"+Guid.NewGuid().ToString("N");
            first=new(deviceId:id);await first.Connect(realtime);
            GameAccount.Session=first.Session;
            bool disconnected=false;first.Disconnected+=()=>disconnected=true;
            string match="";
            if(realtime){await first.EnterRoom(true);match=first.MatchId;}
            GetTree().CurrentScene=null;
            GetTree().ChangeSceneToFile("res://scenes/lobby.tscn");
            // Authentication timestamps have one-second precision.
            await ToSignal(GetTree().CreateTimer(1.2),SceneTreeTimer.SignalName.Timeout);
            second=new(deviceId:id);await second.Connect(realtime);
            if(first.Session.UserId!=second.Session.UserId)throw new Exception("fixture accounts differ");
            if(realtime)await Wait(()=>disconnected,"first socket was not disconnected");
            await Wait(()=>GameAccount.RequiresLogin&&GetTree().CurrentScene is Welcome,"old client did not return to login");
            await Wait(()=>GetNodeOrNull("/root/SessionGuard/SessionReplacedNotice")!=null,"takeover notice missing");
            bool oldRejected=false;
            try {await first.Client.GetAccountAsync(first.Session);}
            catch(Nakama.ApiResponseException e) when(e.StatusCode==401||e.StatusCode==403){oldRejected=true;}
            if(!oldRejected)throw new Exception("old session still authorized");
            bool refreshRejected=false;
            try {await first.Client.SessionRefreshAsync(first.Session);}
            catch(Nakama.ApiResponseException e) when(e.StatusCode==401||e.StatusCode==403){refreshRejected=true;}
            if(!refreshRejected)throw new Exception("old refresh token still authorized");
            await second.Client.GetAccountAsync(second.Session);
            if(realtime) {
                if(!await second.FindActiveMatch()||second.MatchId!=match)throw new Exception("takeover lost active match");
                await second.Rejoin();await second.ForgetActiveMatch();
            }
            GD.Print("SINGLE ACCOUNT PASS ("+(realtime?"socket":"HTTP lobby")+"): old login blocked, notice shown, old refresh rejected, newest login remains valid");
            await first.Close();await second.Close();GetTree().Quit();
        } catch(Exception e) {
            GD.PushError(e.ToString());if(first!=null)await first.Close();if(second!=null)await second.Close();GetTree().Quit(1);
        }
    }
}
