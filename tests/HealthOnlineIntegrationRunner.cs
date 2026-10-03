using Godot;
using System;
using System.Linq;
using System.Threading.Tasks;

public partial class HealthOnlineIntegrationRunner : Node
{
    private int _checks;
    private void Check(bool value, string message) { _checks++; if (!value) throw new InvalidOperationException(message); }
    private async Task Wait(Func<bool> predicate, string message)
    {
        ulong end = Time.GetTicksMsec()+20000;
        while (!predicate()) { if (Time.GetTicksMsec()>end) throw new TimeoutException(message); await ToSignal(GetTree(),SceneTree.SignalName.ProcessFrame); }
    }
    private Node2D Game(string name)
    {
        var viewport = new SubViewport { Name=name, Size=new(1280,720) }; AddChild(viewport);
        var game = GD.Load<PackedScene>("res://scenes/main.tscn").Instantiate<Node2D>(); viewport.AddChild(game); return game;
    }
    private async Task Action(OnlineBattle net, CardShop shop, string type, int token=0, int slot=0)
    {
        net.SendAction(type,token,slot); await Wait(()=>!shop.NetworkPending,type);
    }
    public override void _Ready() => Callable.From(Run).CallDeferred();
    private async void Run()
    {
        try {
            var ga=Game("A"); var gb=Game("B");
            var a=ga.GetNode<OnlineBattle>("OnlineBattle"); var b=gb.GetNode<OnlineBattle>("OnlineBattle");
            var sa=ga.GetNode<CardShop>("UI/SafeArea/Content/CardShop"); var sb=gb.GetNode<CardShop>("UI/SafeArea/Content/CardShop");
            await Wait(()=>a.Connected&&b.Connected,"connect");
            a.CreateRoom(); await Wait(()=>a.State?.Phase=="waiting","create"); b.JoinRoom(a.State!.Code);
            await Wait(()=>sa.Drafting&&sb.Drafting,"preparation");
            Check(a.State!.Players.All(p=>p!.Hp==30),"Both players start at thirty");
            int token=sa.Offers[0].Token;
            await Action(a,sa,"buy",token); await Action(a,sa,"deploy",token,0);
            int previousA=sa.Coins, previousB=sb.Coins;
            int hp=30;
            for (int round=1;round<=10;round++) {
                if (round>1) {
                    await Wait(()=>sa.Drafting&&sb.Drafting&&sa.TurnNumber==round,"automatic next preparation without client acknowledgements");
                    Check(sa.Coins==previousA+CardShop.RoundIncome(round)&&sb.Coins==previousB+CardShop.RoundIncome(round),"Unspent network coins carry forward");
                }
                if(round==2) {
                    var saved=sa.Offers.Select(c=>c.Token).ToArray();
                    await Action(a,sa,"upgrade");
                    Check(sa.ShopLevel==3&&sa.Offers.Select(c=>c.Token).SequenceEqual(saved),"Upgrade preserves every offer");
                    int coins=sa.Coins; await Action(a,sa,"reroll");
                    Check(sa.Coins==coins-2&&sa.Offers.Count==4,"Reroll uses upgraded capacity");
                }
                previousA=sa.Coins; previousB=sb.Coins;
                await Action(a,sa,"ready"); await Action(b,sb,"ready");
                await Wait(()=>a.State?.Phase is "finished" or "game_over"&&b.State?.Phase is "finished" or "game_over","battle finish");
                int damage=sa.ShopLevel+1; hp=Math.Max(0,hp-damage);
                Check(a.State!.Battle!.PlayerDamage==damage,"Winning shop plus surviving star");
                Check(a.State.Players[0]!.Hp==30&&a.State.Players[1]!.Hp==hp&&b.State!.Players[1]!.Hp==hp,"Both clients agree on persistent health");
                Check(sa.Coins==previousA&&sb.Coins==previousB,"Battle never changes coins");
                Check(gb.GetNode<Label>("UI/SafeArea/Content/ProfileB/Coins").Text.Contains($"HP: {hp}/30"),"Profile shows server health");
                if(hp==0) break;
                Check(!sa.ZoneA.Visible&&!sb.ZoneA.Visible,"post-round buttons and text hidden");
                Check(!ga.GetNode<Control>("UI/SafeArea/Content/BattleResult").Visible&&!gb.GetNode<Control>("UI/SafeArea/Content/BattleResult").Visible,"round winner banners hidden");
                var intermediate=await a.Connection!.Client.ListLeaderboardRecordsAsync(a.Connection.Session,NakamaConnection.WinsLeaderboardId,new[]{a.UserId},limit:1);
                Check(!intermediate.OwnerRecords.Any(),"round wins never award leaderboard points");
            }
            Check(a.State!.Phase=="game_over"&&b.State!.Phase=="game_over"&&a.State.Winner=="A","Zero health immediately ends match");
            Check(sa.GameOver&&sb.GameOver&&!sa.Drafting&&!sb.Drafting,"No shop after match ends");
            long revision=a.State.Revision; sa.ConfirmTurn(); sb.ConfirmTurn();
            await ToSignal(GetTree().CreateTimer(.3),SceneTreeTimer.SignalName.Timeout);
            Check(a.State.Revision==revision,"Next round button cannot advance terminal match");
            var points=await a.Connection!.Client.ListLeaderboardRecordsAsync(a.Connection.Session,NakamaConnection.WinsLeaderboardId,new[]{a.UserId,b.UserId},limit:10);
            Check(points.OwnerRecords.Single(r=>r.OwnerId==a.UserId).Score=="1","match win awards one real Nakama point");
            Check(!points.OwnerRecords.Any(r=>r.OwnerId==b.UserId),"loss does not award points");
            bool rejected=false;try{await a.Connection.Client.WriteLeaderboardRecordAsync(a.Connection.Session,NakamaConnection.WinsLeaderboardId,999);}catch{rejected=true;}
            Check(rejected,"client cannot forge authoritative score");
            await ToSignal(GetTree().CreateTimer(2),SceneTreeTimer.SignalName.Timeout);
            points=await a.Connection.Client.ListLeaderboardRecordsAsync(a.Connection.Session,NakamaConnection.WinsLeaderboardId,new[]{a.UserId},limit:1);
            Check(points.OwnerRecords.Single().Score=="1","repeat game-over ticks do not duplicate win");
            var persistent=GameAccount.Connection();await persistent.Connect();string persistentId=persistent.Session.UserId;await persistent.Close();
            var restored=GameAccount.Connection();await restored.Connect();Check(restored.Session.UserId==persistentId,"device account persists across new connections");await restored.Close();
            ga.GetParent().QueueFree(); gb.GetParent().QueueFree();
            await ToSignal(GetTree(),SceneTree.SignalName.ProcessFrame);
            var lobby=GD.Load<PackedScene>("res://scenes/lobby.tscn").Instantiate<Lobby>();AddChild(lobby);lobby.Navigate("Leaderboard");
            var page=lobby.FindChildren("*","",true,false).OfType<LeaderboardPage>().First();
            await Wait(()=>!page.Loading,"leaderboard page load");Check(page.RecordCount>0,"UI displays real leaderboard records");
            await ToSignal(GetTree(),SceneTree.SignalName.ProcessFrame);GetViewport().GetTexture().GetImage().SavePng("res://tests/deck-leaderboard.png");lobby.QueueFree();
            GD.Print($"ONLINE HEALTH PASS: {_checks} checks"); GetTree().Quit();
        } catch(Exception e) { GD.PushError(e.ToString()); GetTree().Quit(1); }
    }
}
