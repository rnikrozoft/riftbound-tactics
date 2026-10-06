using Godot;
using System;
using System.Linq;
using System.Collections.Generic;
using System.Threading.Tasks;

public partial class BrawlCombatRunner : Node
{
    private int _checks;
    private void Check(bool ok,string message){_checks++;if(!ok)throw new Exception(message);}
    private async Task Frames(int n=2){for(int i=0;i<n;i++)await ToSignal(GetTree(),SceneTree.SignalName.ProcessFrame);}
    private async Task Wait(Func<bool> condition,int ms=6000){ulong end=Time.GetTicksMsec()+(ulong)ms;while(!condition()){if(Time.GetTicksMsec()>end)throw new Exception("Brawl wait timeout");await Frames(1);}}
    public override void _Ready()=>Callable.From(Run).CallDeferred();
    private async void Run()
    {
        try {
            var field=GD.Load<PackedScene>("res://scenes/main.tscn").Instantiate<BattleDisplay>();var online=field.GetNode("OnlineBattle");field.RemoveChild(online);online.Free();
            var battle=field.GetNode<BattleDemo>("BattleDemo");Check(battle.PresentationMode==BattlePresentationMode.TurnBased,"Turn-based is the default presentation");battle.PresentationMode=BattlePresentationMode.Brawl;battle.AutoStart=false;battle.NetworkEnabled=true;AddChild(field);await Frames();
            var shop=field.GetNode<CardShop>("UI/SafeArea/Content/CardShop");
            int kind=CharacterData.All.First(c=>c.ScenePath.EndsWith("/swordsman.tscn")).Kind;
            var state=new OnlineState {Round=1,Phase="preparation",Players=new OnlinePlayer?[]{
                new() {Team="A",UserId="self",Units=new[]{new OnlineUnit {Kind=kind,Token=1,Slot=3},new OnlineUnit {Kind=kind,Token=2,Slot=4}}},
                new() {Team="B",UserId="enemy",Units=new[]{new OnlineUnit {Kind=kind,Token=1,Slot=3},new OnlineUnit {Kind=kind,Token=2,Slot=4}}}}};
            shop.ApplyNetworkState(state,"self");state.Phase="battle";shop.ApplyNetworkState(state,"self");
            var plan=new OnlinePlan {Winner="DRAW",Units=shop.NetworkUnits.Select(p=>new OnlineCombatUnit {Id=p.Key,Kind=kind,Team=p.Value.IsAlly?"A":"B",Slot=p.Value.GridSlot,MaxHp=100,InitialHp=100,Stars=1}).ToArray()};shop.SetReplayPlan(plan);
            var units=shop.NetworkUnits.Values.ToArray();var center=units.Select(u=>u.Position).Aggregate(Vector2.Zero,(a,b)=>a+b)/units.Length;
            foreach(var u in units)u.Position=center+new Vector2(u.IsAlly?-70:70,u.CardToken==1?-20:20);
            var homes=units.ToDictionary(u=>u,u=>u.Position);battle.BeginCombatPresentation(plan);
            Check(battle.BrawlActive&&battle.PresentationMode==BattlePresentationMode.Brawl,"Brawl remains available when explicitly selected");
            await Wait(()=>units.Count(u=>u.Sprite.Animation==BattleAnimations.Walk)>=2);
            await ToSignal(GetTree().CreateTimer(.2),SceneTreeTimer.SignalName.Timeout);
            Check(units.Count(u=>u.Position.DistanceTo(homes[u])>2)>=2,"Multiple units walk concurrently");
            Check(units.All(u=>u.Position.DistanceTo(homes[u])<40),"Movement is continuous walking, not an instant dash");
            await Wait(()=>units.Count(u=>u.Sprite.IsPlaying()&&u.Sprite.Animation.ToString().StartsWith("attack"))>=2);
            Check(units.All(u=>u.Health==100&&u.Armor==0),"Concurrent cosmetic attacks cannot invent damage or consume armor");
            // State updates during battle may not teleport melee participants back to their slots.
            var current=units.ToDictionary(u=>u,u=>u.Position);shop.ApplyNetworkState(state,"self");
            Check(units.All(u=>u.Position.IsEqualApprox(current[u])),"Live state refresh preserves brawl positions");
            shop.SetReplayPlan(plan); // Restore the synthetic 100 HP fixture after catalog stat reconciliation.
            var actor=shop.NetworkUnits["A:1"];var target=shop.NetworkUnits["B:1"];
            var rule=CharacterData.Get(kind).Combat!.Actions.First(a=>a.Animation=="attack03");
            var changes=new List<int>();target.HealthChanged+=(hp,max)=>{if(hp<100&&(changes.Count==0||changes[^1]!=hp))changes.Add(hp);};
            var combat=new OnlineCombatEvent {Attacker=actor.ServerId,Target=target.ServerId,Animation="attack03",Mode="melee",TargetHp=80,
                Hits=rule.HitFrames.Select((frame,i)=>new OnlineCombatHit {Frame=frame,Source=actor.ServerId,Target=target.ServerId,Damage=5,Kind="damage",Changes=new[]{new OnlineCombatChange {Id=target.ServerId,Hp=95-i*5,Slot=target.GridSlot}}}).ToArray()};
            plan.Events=new[]{combat};await battle.PlayServerEvent(actor,target,combat);
            Check(changes.SequenceEqual(new[]{95,90,85,80}),"Four server snapshots apply once and in order");
            Check(actor.Position.DistanceTo(homes[actor])>10,"Melee attacker stays in the scrum instead of returning home");
            if(DisplayServer.GetName()!="headless"){
                await ToSignal(RenderingServer.Singleton,RenderingServer.SignalName.FramePostDraw);GetViewport().GetTexture().GetImage().SavePng("res://tests/combat-brawl.png");
            }
            // Pause freezes the entire scrum, not only the active server attacker.
            var paused=units.ToDictionary(u=>u,u=>(u.Position,u.Sprite.Frame));GetTree().Paused=true;
            ulong until=Time.GetTicksMsec()+150;while(Time.GetTicksMsec()<until)await Frames(1);
            Check(units.All(u=>u.Position.IsEqualApprox(paused[u].Position)&&u.Sprite.Frame==paused[u].Frame),"Replay pause freezes concurrent movement and animation");GetTree().Paused=false;
            battle.EndCombatPresentation(true);Check(!battle.BrawlActive&&units.All(u=>u.Position.IsEqualApprox(homes[u])),"Cancellation restores the formation");
            battle.BeginCombatPresentation(plan);battle.ServerVisualTimeMs=5000;
            var deadline=await Catch(()=>battle.PlayServerEvent(actor,target,combat,5000));
            Check(deadline==null&&target.Health==80,"Deadline cutoff reconciles the authoritative final event");battle.EndCombatPresentation(true);battle.ServerVisualTimeMs=0;
            battle.BeginCombatPresentation(plan);var interrupted=battle.PlayServerEvent(actor,target,combat);battle.EndCombatPresentation(true);
            var canceled=await Catch(()=>interrupted);Check(canceled is OperationCanceledException,"Stopping a brawl cancels pending animation work safely");
            battle.PresentationMode=BattlePresentationMode.TurnBased;battle.BeginCombatPresentation(plan);
            Check(!battle.BrawlActive,"Turn-based presentation remains selectable");
            actor.ApplyCombatState(new OnlineCombatChange {Hp=100,Slot=3});target.ApplyCombatState(new OnlineCombatChange {Hp=100,Slot=3});var origin=actor.Position;
            await battle.PlayServerEvent(actor,target,combat);
            Check(actor.Position.IsEqualApprox(origin)&&target.Health==80,"Original turn-based attack returns home and keeps the same result");
            GD.Print($"PASS brawl combat: {_checks} checks");GetTree().Quit();
        }catch(Exception e){GetTree().Paused=false;GD.PushError(e.ToString());GetTree().Quit(1);}
    }
    private static async Task<Exception?> Catch(Func<Task> run){try{await run();return null;}catch(Exception e){return e;}}
}
