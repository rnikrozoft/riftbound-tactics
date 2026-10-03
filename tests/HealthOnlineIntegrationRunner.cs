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
                    await Action(a,sa,"next"); await Action(b,sb,"next");
                    await Wait(()=>sa.Drafting&&sb.Drafting&&sa.TurnNumber==round,"next preparation");
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
            }
            Check(a.State!.Phase=="game_over"&&b.State!.Phase=="game_over"&&a.State.Winner=="A","Zero health immediately ends match");
            Check(sa.GameOver&&sb.GameOver&&!sa.Drafting&&!sb.Drafting,"No shop after match ends");
            long revision=a.State.Revision; sa.ConfirmTurn(); sb.ConfirmTurn();
            await ToSignal(GetTree().CreateTimer(.3),SceneTreeTimer.SignalName.Timeout);
            Check(a.State.Revision==revision,"Next round button cannot advance terminal match");
            ga.GetParent().QueueFree(); gb.GetParent().QueueFree();
            await ToSignal(GetTree(),SceneTree.SignalName.ProcessFrame);
            GD.Print($"ONLINE HEALTH PASS: {_checks} checks"); GetTree().Quit();
        } catch(Exception e) { GD.PushError(e.ToString()); GetTree().Quit(1); }
    }
}
