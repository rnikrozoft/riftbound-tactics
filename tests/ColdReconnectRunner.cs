using Godot;
using System;
using System.Linq;
using System.Threading.Tasks;

// Run seed and resume in separate Godot processes; only fixture identifiers cross the boundary.
public partial class ColdReconnectRunner : Node
{
    private OnlineState? _state;
    private NakamaConnection? _connection;
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
        NakamaConnection? other=null;
        try {
            string mode=OS.GetEnvironment("RIFT_RECONNECT_PHASE");
            string path=ProjectSettings.GlobalizePath("res://output/cold-reconnect-fixture.txt");
            if(mode=="seed") {
                string device="rift-reconnect-test-"+Guid.NewGuid().ToString("N");
                _connection=new(deviceId:device);await _connection.Connect();
                _connection.StateReceived+=s=>_state=s;
                await _connection.EnterRoom(true);
                other=new();await other.Connect();await other.EnterRoom(false,_connection.Code);
                await Wait(()=>_state?.Phase=="preparation","preparation missing");
                int token=Self().Offers[0].Token;
                await _connection.Send(new OnlineAction {Type="buy",Token=token,Round=_state!.Round,Sequence=_connection.NextActionSequence()});
                await Wait(()=>Self().Hand.Length==1,"buy missing");
                await _connection.Send(new OnlineAction {Type="deploy",Token=token,Slot=2,Round=_state!.Round,Sequence=_connection.NextActionSequence()});
                await Wait(()=>Self().Units.Length==1,"deployment missing");
                System.IO.File.WriteAllLines(path,new[]{device,_connection.MatchId,Self().Coins.ToString(),token.ToString(),_state!.Round.ToString()});
                GD.Print("COLD RECONNECT SEED PASS: "+_connection.MatchId);
            } else if(mode=="ui") {
                var fixture=System.IO.File.ReadAllLines(path);
                // Isolate the fixture account without touching the user's saved account files.
                var flags=System.Reflection.BindingFlags.Static|System.Reflection.BindingFlags.NonPublic;
                typeof(GameAccount).GetField("_deviceId",flags)!.SetValue(null,fixture[0]);
                typeof(GameAccount).GetField("_hadSavedIdentity",flags)!.SetValue(null,true);
                GetTree().CurrentScene=null;
                GetTree().ChangeSceneToFile("res://scenes/welcome.tscn");
                await Wait(()=>GetTree().CurrentScene?.GetNodeOrNull<OnlineBattle>("OnlineBattle")?.Connected==true,"Welcome did not automatically resume gameplay");
                var battle=GetTree().CurrentScene!.GetNode<OnlineBattle>("OnlineBattle");
                await Wait(()=>battle.State!=null,"gameplay snapshot missing");
                var self=battle.State!.Players.First(p=>p?.UserId==battle.UserId)!;
                if(battle.Connection!.MatchId!=fixture[1]||self.Units.Length!=1||self.Units[0].Slot!=2||self.Coins!=int.Parse(fixture[2]))throw new Exception("UI restored incorrect match state");
                await battle.Connection.ForgetActiveMatch();await battle.Connection.Close();
                GD.Print("COLD RECONNECT UI PASS: Welcome -> Lobby recovery -> gameplay automatically, original match and state preserved");
                GetTree().Quit();return;
            } else {
                var fixture=System.IO.File.ReadAllLines(path);
                _connection=new(deviceId:fixture[0]);await _connection.Connect();
                if(!await _connection.FindActiveMatch()||_connection.MatchId!=fixture[1])throw new Exception("original match not discovered");
                _connection.StateReceived+=s=>_state=s;
                await _connection.Rejoin();
                await Wait(()=>_state!=null,"snapshot missing");
                var self=Self();
                if(self.Coins!=int.Parse(fixture[2])||self.Units.Length!=1||self.Units[0].Token!=int.Parse(fixture[3])||self.Units[0].Slot!=2||_state!.Round!=int.Parse(fixture[4]))throw new Exception("authoritative state changed across process restart");
                await _connection.Send(new OnlineAction {Type="lock",Round=_state!.Round,Sequence=_connection.NextActionSequence()});
                await Wait(()=>Self().ShopLocked,"resumed action not accepted");
                await _connection.ForgetActiveMatch();
                if(await _connection.FindActiveMatch())throw new Exception("explicit leave still resumes");
                GD.Print("COLD RECONNECT RESUME PASS: original match, coins, unit, slot, round and new actions verified");
            }
            if(other!=null)await other.Close();
            await _connection.Close();GetTree().Quit();
        } catch(Exception e) {
            GD.PushError(e.ToString());if(other!=null)await other.Close();if(_connection!=null)await _connection.Close();GetTree().Quit(1);
        }
    }
    private OnlinePlayer Self()=>_state!.Players.First(p=>p?.UserId==_connection!.Session.UserId)!;
}
