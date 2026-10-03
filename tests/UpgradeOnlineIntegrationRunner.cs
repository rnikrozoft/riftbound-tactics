using Godot;
using System;
using System.Linq;
using System.Threading.Tasks;

public partial class UpgradeOnlineIntegrationRunner : Node
{
    private int _checks;
    private void Check(bool value,string message) { _checks++; if(!value) throw new InvalidOperationException(message); }
    private async Task Wait(Func<bool> test,string message)
    {
        ulong end=Time.GetTicksMsec()+20000;
        while(!test()) { if(Time.GetTicksMsec()>end) throw new TimeoutException(message); await ToSignal(GetTree(),SceneTree.SignalName.ProcessFrame); }
    }
    private Node2D Game(string name)
    {
        var view=new SubViewport { Name=name, Size=new(1280,720) }; AddChild(view);
        var game=GD.Load<PackedScene>("res://scenes/main.tscn").Instantiate<Node2D>(); view.AddChild(game); return game;
    }
    private async Task Action(OnlineBattle net,CardShop shop,string type,int token=0,int slot=0)
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
            await Wait(()=>a.Connected&&b.Connected,"connect"); a.CreateRoom(); await Wait(()=>a.State?.Phase=="waiting","create"); b.JoinRoom(a.State!.Code);
            await Wait(()=>sa.Drafting&&sb.Drafting,"prepare");
            for(int round=2;round<=7;round++) {
                await Action(a,sa,"ready"); await Action(b,sb,"ready"); await Wait(()=>a.State?.Phase=="finished"&&b.State?.Phase=="finished","draw");
                await Action(a,sa,"next"); await Action(b,sb,"next"); await Wait(()=>sa.Drafting&&sb.Drafting&&sa.TurnNumber==round,"income round");
            }
            Check(sa.Coins==84,"Stacked budget from seven rounds");
            string name=sa.Offers[0].Name; int token=sa.Offers[0].Token;
            await Action(a,sa,"buy",token);
            for(int stars=2;stars<=4;stars++) {
                int attempts=0;
                while(!sa.Offers.Any(c=>c.Name==name)) { if(++attempts>35) throw new Exception("No duplicate in pool"); await Action(a,sa,"reroll"); }
                int coins=sa.Coins; int duplicate=sa.Offers.First(c=>c.Name==name).Token;
                await Action(a,sa,"buy",duplicate);
                Check(sa.Coins==coins-2,"Duplicate charges card price once");
                Check(sa.Hand.Count==1&&sa.Deployed.Count==0&&sa.Hand[0].Stars==stars&&sa.Hand[0].Token==token,"Upgraded character returns to hand");
                Check(sa.HandRow.GetChild<ShopCard>(0).HasNode("UpgradeEffect"),"Upgrade effect on returned card");
                await Action(a,sa,"deploy",token,0);
                var unit=sa.Deployed[token].Unit;
                Check(sa.Hand.Count==0&&sa.Deployed.Count==1&&unit.Stars==stars,"Redeploy upgraded character");
                Check(unit.Health==100*stars&&unit.Attack==30*stars&&unit.Speed==10+2*(stars-1),"Client upgraded stats");
                if(stars==2) await Action(a,sa,"ready");
                Check(sa.ReadyForBattle&&!sa.CanReturnUnit(unit),"Automatic upgrade return remains allowed after ready while manual return is blocked");
                Check(a.State!.Players[1]!.Units.Length==0&&b.State!.Players[0]!.Units.Length==0,"Upgrade preserves hidden preparation");
            }
            int saved=sa.Coins;
            await Action(a,sa,"ready"); await Action(b,sb,"ready");
            await Wait(()=>a.State?.Phase=="battle"&&b.State?.Phase=="battle","battle");
            var enemy=sb.NetworkUnits["A:"+token];
            Check(enemy.Stars==4&&enemy.Health==400,"Opponent sees same upgraded unit at battle");
            Check(a.State!.Battle!.Units[0].MaxHp==400&&a.State.Battle.PlayerDamage==6,"Server combat health and player damage include stars");
            await Wait(()=>a.State?.Phase=="finished"&&b.State?.Phase=="finished","finish");
            Check(a.State!.Players[1]!.Hp==24,"Shop two plus surviving four stars");
            await Action(a,sa,"next"); await Action(b,sb,"next"); await Wait(()=>sa.Drafting&&sb.Drafting,"next");
            Check(sa.Deployed[token].Unit.Stars==4&&sa.Deployed[token].Unit.Health==400,"Upgrade persists through round reset");
            await Action(a,sa,"sell",token);
            Check(sa.Deployed.Count==0&&sa.Coins==saved+20+4,"Sell refund half total eight coins");
            ga.GetParent().QueueFree(); gb.GetParent().QueueFree(); await ToSignal(GetTree(),SceneTree.SignalName.ProcessFrame);
            GD.Print($"ONLINE UPGRADE PASS: {_checks} checks"); GetTree().Quit();
        } catch(Exception e) { GD.PushError(e.ToString()); GetTree().Quit(1); }
    }
}
