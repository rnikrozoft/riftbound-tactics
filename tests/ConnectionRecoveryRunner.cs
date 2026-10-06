using Godot;
using System;
using System.Linq;
using System.Threading.Tasks;

public partial class ConnectionRecoveryRunner : Node
{
    private async Task Frame()=>await ToSignal(GetTree(),SceneTree.SignalName.ProcessFrame);
    private async Task Wait(Func<bool> condition,string message,int timeout=18000)
    {
        ulong until=Time.GetTicksMsec()+(ulong)timeout;
        while(!condition()){if(Time.GetTicksMsec()>until)throw new Exception(message);await Frame();}
    }
    public override void _Ready()=>Callable.From(Run).CallDeferred();
    private async void Run()
    {
        NakamaConnection? first=null,latest=null;
        try{
            GetTree().CurrentScene=null;
            string device="rift-recovery-test-"+Guid.NewGuid().ToString("N");
            first=new(deviceId:device);await first.Connect();
            try {await first.Socket.JoinMatchAsync(Guid.NewGuid().ToString()+".riftbound");throw new Exception("Missing match unexpectedly joined");}
            catch(System.Net.WebSockets.WebSocketException e){if(e.Message!="Match not found")throw new Exception("Unexpected missing-match error: "+e.Message);}

            var room=await first.EnterRoom(true,deck:DeckDefinition.Starter());
            string user=first.Session.UserId;
            long ack=0;first.StateReceived+=state=>System.Threading.Interlocked.Exchange(ref ack,state.AckSequence);
            // Even a rejected game action consumes its sequence, testing transport ordering.
            for(int i=0;i<3;i++)await first.Send(new OnlineAction {Type="invalid-test-action",Sequence=first.NextActionSequence()});
            await Wait(()=>System.Threading.Interlocked.Read(ref ack)>=3,"Pre-reconnect acknowledgement missing");
            var httpOnly=new NakamaConnection(deviceId:device,session:first.Session);
            await httpOnly.Connect(realtime:false);
            if(httpOnly.Socket!=null)throw new Exception("HTTP-only connection opened an extra socket");
            await httpOnly.Close();
            BattleRecovery.Begin(first,DeckDefinition.Starter());
            await Wait(()=>!BattleRecovery.Snapshots.IsEmpty,"Initial match snapshot missing");
            BattleRecovery.Ready=true;GetTree().ChangeSceneToFile("res://scenes/main.tscn");
            await Wait(()=>GetTree().CurrentScene?.GetNodeOrNull<OnlineBattle>("OnlineBattle")?.State!=null,"Gameplay handoff failed");
            var online=GetTree().CurrentScene.GetNode<OnlineBattle>("OnlineBattle");
            if(online.UserId!=user||online.Connection!=first)throw new Exception("Handoff changed identity or transport");
            // Simulate an unexpected transport loss, not a voluntary match leave.
            await first.Socket.CloseAsync();
            await Wait(()=>GetTree().CurrentScene is Lobby,"Disconnect did not return to lobby");
            await Wait(()=>GetTree().CurrentScene?.GetNodeOrNull<OnlineBattle>("OnlineBattle")?.State!=null,"Automatic reconnect did not resume gameplay");
            online=GetTree().CurrentScene.GetNode<OnlineBattle>("OnlineBattle");
            if(online.Connection!.MatchId!=room.MatchId||online.UserId!=user)throw new Exception("Reconnect did not restore the same match/account");
            long next=first.NextActionSequence();
            if(next<=3)throw new Exception("Reconnect reset the command sequence");
            await first.Send(new OnlineAction {Type="invalid-test-action",Sequence=next});
            await Wait(()=>System.Threading.Interlocked.Read(ref ack)>=next,"Post-reconnect action rejected as stale");
            latest=new(deviceId:device);await latest.Connect();
            await Wait(()=>GetTree().CurrentScene is Lobby&&!BattleRecovery.Pending,"Superseded login kept reconnecting");
            if(!GameAccount.RequiresLogin)throw new Exception("Superseded account must require explicit login");
            ((Lobby)GetTree().CurrentScene).Launch(true);await Frame();
            if(GetTree().CurrentScene is not Lobby)throw new Exception("Superseded client bypassed the login lock");
            if(!BattleRecovery.Notice.Contains("elsewhere"))throw new Exception("Superseded session needs a visible login message");
            try{await first.Reconnect();throw new Exception("Older token remains valid");}catch(Nakama.ApiResponseException e) when(e.StatusCode==401||e.StatusCode==403){}
            await latest.EnterRoom(false,room.Code,DeckDefinition.Starter());
            ulong end=Time.GetTicksMsec()+2500;while(Time.GetTicksMsec()<end)await Frame();
            await latest.Client.GetAccountAsync(latest.Session);
            if(GetTree().CurrentScene is not Lobby||BattleRecovery.Pending)throw new Exception("Old client stole the latest login");
            // Size correction and heading removal are visible in the same gameplay scene.
            int kind=CharacterData.All.First(c=>c.ScenePath.EndsWith("/black_knight_a.tscn")).Kind;
            var unit=GD.Load<PackedScene>(CharacterData.Get(kind).ScenePath).Instantiate<BattleUnit>();unit.CardKind=kind;AddChild(unit);
            var idle=CharacterVisual.AnimationSize(unit.Sprite.SpriteFrames,BattleAnimations.Idle);
            float original=Math.Min(CharacterVisual.FieldHeight/idle.Y,CharacterVisual.FieldWidth/idle.X);
            if(Math.Abs(unit.Sprite.Scale.X/original-1.35f)>.01f)throw new Exception("Black Knight body sizing was not corrected");unit.QueueFree();
            if(DisplayServer.GetName()!="headless"){
                ((Control)GetTree().CurrentScene).Hide();
                var preview=GD.Load<PackedScene>("res://scenes/main.tscn").Instantiate<BattleDisplay>();
                preview.GetNode("OnlineBattle").Free();preview.GetNode<BattleDemo>("BattleDemo").AutoStart=false;preview.GetNode<BattleDemo>("BattleDemo").NetworkEnabled=true;AddChild(preview);
                foreach(var existing in preview.GetChildren().OfType<BattleUnit>().ToArray())existing.Free();
                preview.GetNode<Control>("UI/SafeArea/Content/CardShop/ZoneA").Hide();preview.GetNode<Control>("UI/SafeArea/Content/CardShop/ZoneB").Hide();
                var tiles=preview.GetNode<TileMapLayer>("TileMapLayer");
                foreach(var (slug,slot) in new[]{("black_knight_a",1),("black_knight_b",4),("swordsman",0)}){
                    int character=CharacterData.All.First(c=>c.ScenePath.EndsWith("/"+slug+".tscn")).Kind;
                    var actor=GD.Load<PackedScene>(CharacterData.Get(character).ScenePath).Instantiate<BattleUnit>();actor.CardKind=character;preview.AddChild(actor);
                    actor.Position=tiles.MapToLocal(CardShop.DeploymentCells[slot]);actor.ApplyCombatState(new OnlineCombatChange {Hp=actor.MaxHealth,Slot=slot,Stun=slug=="swordsman"});
                }
                await ToSignal(GetTree().CreateTimer(.25),SceneTreeTimer.SignalName.Timeout);
                var panel=preview.FindChildren("*","",true,false).OfType<CombatStatusPanel>().Single();
                if(panel.FindChildren("*","",true,false).OfType<Label>().Any(l=>l.Text=="บัฟและดีบัฟที่มีผล"))throw new Exception("Status heading remains visible");
                await ToSignal(RenderingServer.Singleton,RenderingServer.SignalName.FramePostDraw);GetViewport().GetTexture().GetImage().SavePng("/tmp/riftbound-character-size.png");
            }
            GD.Print("CONNECTION RECOVERY PASS: lobby on loss, same-session reconnect, same-match resume, latest login wins, stale token blocked, no login stealing, body size correction");
            await latest.Close();GetTree().Quit();
        }catch(Exception e){GD.PushError(e.ToString());if(latest!=null)await latest.Close();if(first!=null)await first.Close();GetTree().Quit(1);}
    }
}
