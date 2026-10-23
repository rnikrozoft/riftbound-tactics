using Godot;
using Nakama;
using System.Collections.Concurrent;

// Realtime takeover notifications and HTTP-only menu sessions share one logout path.
public partial class SessionGuard : Node
{
    private static readonly ConcurrentQueue<ISession> Replaced=new();
    public static void NotifyReplaced(ISession session)=>Replaced.Enqueue(session);
    private double _untilCheck;
    private bool _checking,_leaving;
    public override void _Process(double delta)
    {
        while(Replaced.TryDequeue(out var session))
            if(ReferenceEquals(session,GameAccount.Session))Logout();
        if(_leaving||GameAccount.RequiresLogin||GameAccount.Session==null)return;
        _untilCheck-=delta;
        if(_untilCheck<=0&&!_checking){_untilCheck=5;CheckSession();}
    }
    private async void CheckSession()
    {
        var session=GameAccount.Session;
        if(session==null)return;
        _checking=true;
        try {await GameAccount.Connection().Client.GetAccountAsync(session);}
        catch(ApiResponseException e) when(e.StatusCode==401||e.StatusCode==403) {
            if(ReferenceEquals(session,GameAccount.Session))NotifyReplaced(session);
        }
        catch(System.Exception) { /* Network outages use normal recovery, never force a login. */ }
        finally {_checking=false;}
    }
    private void Logout()
    {
        if(_leaving)return;
        _leaving=true;GameAccount.RequiresLogin=true;GameAccount.Session=null;
        BattleRecovery.Notice=GamePreferences.Text("Your account was signed in on another device. Please log in again.","บัญชีของคุณมีการเข้าสู่ระบบจากเครื่องอื่น กรุณาเข้าสู่ระบบใหม่");
        // Keep the active-match pointer for the newest login to resume.
        _=BattleRecovery.Cancel();
        GetTree().ChangeSceneToFile("res://scenes/welcome.tscn");
        Callable.From(ShowNotice).CallDeferred();
    }
    private void ShowNotice()
    {
        var layer=new CanvasLayer {Layer=100,Name="SessionReplacedNotice"};AddChild(layer);
        var modal=new GameModal {MenuStyle=true};layer.AddChild(modal);
        modal.Body.AddChild(DeckMenuUi.Text(GamePreferences.Text("Signed in elsewhere","มีการเข้าสู่ระบบจากที่อื่น"),24));
        var text=DeckMenuUi.Text(BattleRecovery.Notice,16);text.AutowrapMode=TextServer.AutowrapMode.WordSmart;modal.Body.AddChild(text);
        void Dismiss(){layer.QueueFree();_leaving=false;}
        modal.Body.AddChild(DeckMenuUi.Button(GamePreferences.Text("OK","ตกลง"),Dismiss,160));modal.Cancel=Dismiss;
    }
}
