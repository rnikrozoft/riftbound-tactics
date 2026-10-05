using Godot;
using System;
using System.Linq;
using System.Threading.Tasks;

public partial class FinishingFocusRunner : Node
{
    private void Check(bool ok,string message) { if (!ok) throw new Exception(message); }
    public override void _Ready() => Callable.From(Run).CallDeferred();
    private async Task Frame() => await ToSignal(GetTree(),SceneTree.SignalName.ProcessFrame);
    private async void Run()
    {
        try {
            var field = GD.Load<PackedScene>("res://scenes/main.tscn").Instantiate<BattleDisplay>();
            var battle = field.GetNode<BattleDemo>("BattleDemo"); battle.AutoStart=false; battle.ArenaFloatAmplitude=0;
            var online = field.GetNode("OnlineBattle"); field.RemoveChild(online); online.Free();
            AddChild(field); await Frame();
            var units = field.GetChildren().OfType<BattleUnit>().ToArray();
            var attacker = units.First(u=>u.IsAlly); var target = units.First(u=>!u.IsAlly);
            var home = GetViewport().CanvasTransform;
            var focus = battle.GetNode<FinishingFocus>("FinishingFocus");
            await battle.PlayServerEvent(attacker,target,new OnlineCombatEvent {Damage=30,TargetHp=70});
            Check(!focus.Active && GetViewport().CanvasTransform.IsEqualApprox(home),"ordinary attack changed focus");
            bool impactSeen=false;
            battle.Impact += (a,t) => {
                if (!t.IsDead) return;
                impactSeen=true;
                Check(focus.Active,"lethal attack did not focus");
                Check(GetViewport().CanvasTransform.IsEqualApprox(home),"camera must return before attack");
                Check(t.Sprite.SpeedScale==0,"hitstop did not freeze death");
                Check(t.GetNodeOrNull("FinisherSymmetricalImpactLarge") != null,"impact missing");
                Check(a.GetNodeOrNull("FinisherAttackUpSmall") == null,"charge must end before attack");
            };
            var task = battle.PlayServerEvent(attacker,target,new OnlineCombatEvent {Damage=70,TargetHp=0,Dead=true});
            bool chargeSeen=false, zoomSeen=false, deathSeen=false, captured=false;
            var attackerHome=attacker.Position;
            ulong started = Time.GetTicksMsec();
            while(!task.IsCompleted) {
                if (focus.ChargePlaying) {
                    chargeSeen=true;
                    Check(attacker.Position.IsEqualApprox(attackerHome),"attacker moved before charge ended");
                    if (!GetViewport().CanvasTransform.IsEqualApprox(home)) zoomSeen=true;
                }
                if (attacker.Sprite.Animation==BattleAnimations.Walk)
                    Check(GetViewport().CanvasTransform.IsEqualApprox(home),"camera still zoomed during dash");
                if(target.Sprite.Animation==BattleAnimations.Die && target.Sprite.SpeedScale>0) {
                    Check(target.Sprite.SpeedScale==1,"death animation must play at normal speed");
                    deathSeen=true;
                }
                Check(focus.GetNodeOrNull("FinisherSpotlight")==null,"dark overlay must be removed");
                await Frame();
                if (!captured && focus.ChargePlaying && Time.GetTicksMsec()-started>=300 && DisplayServer.GetName() != "headless") {
                    await ToSignal(RenderingServer.Singleton,RenderingServer.SignalName.FramePostDraw);
                    GetViewport().GetTexture().GetImage().SavePng("/tmp/riftbound-finishing-attack-up.png");
                    captured=true;
                }
            }
            await task; await Frame();
            Check(impactSeen && chargeSeen && zoomSeen && deathSeen,"charge, zoom or normal death missing");
            Check(target.IsDead && target.Sprite.SpeedScale==1,"death speed or health not restored");
            Check(!focus.Active && GetViewport().CanvasTransform.IsEqualApprox(home),"camera did not restore");
            Check(target.GetNodeOrNull("FinisherSymmetricalImpactLarge")==null && attacker.GetNodeOrNull("FinisherAttackUpSmall")==null,"effects leaked");
            target.ResetHealth(); target.Sprite.Play(BattleAnimations.Idle);
            battle.ServerVisualTimeMs=1;
            var canceled=battle.PlayServerEvent(attacker,target,new OnlineCombatEvent {Damage=100,TargetHp=0,Dead=true},100);
            await Frame(); battle.ServerVisualTimeMs=100;
            await canceled; await Frame();
            Check(!focus.Active && GetViewport().CanvasTransform.IsEqualApprox(home),"deadline cancellation left camera zoomed");
            GD.Print("FINISHING FOCUS PASS: normal hit, attack-up charge, zoom before dash, impact, normal death, camera return, deadline cleanup");
            GetTree().Quit();
        } catch(Exception e) { GD.PushError(e.ToString()); GetTree().Quit(1); }
    }
}
