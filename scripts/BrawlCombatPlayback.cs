using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

// Concurrent movement/poses; only the ordered authoritative hit stream can change combat state.
public partial class BrawlCombatPlayback : Node
{
    public bool Active {get;private set;}
    public float WalkSpeed {get;set;}=75;
    private CardShop _shop=null!;
    private BattleDemo _battle=null!;
    private OnlinePlan? _plan;
    private int _generation;
    private readonly Dictionary<BattleUnit,Vector2> _homes=new();
    private readonly Dictionary<BattleUnit,int> _cycles=new();
    private readonly Dictionary<BattleUnit,BattleUnit> _targets=new();
    private readonly HashSet<BattleUnit> _posing=new();
    public void Start(CardShop shop,BattleDemo battle,OnlinePlan plan)
    {
        Stop(true);_shop=shop;_battle=battle;_plan=plan;Active=true;_generation++;SetProcess(true);
        foreach(var unit in shop.NetworkUnits.Values)_homes[unit]=unit.Position;
    }
    public void Stop(bool restoreFormation)
    {
        Active=false;_generation++;SetProcess(false);_targets.Clear();_posing.Clear();
        foreach(var (unit,home) in _homes){
            if(!GodotObject.IsInstanceValid(unit)||unit.IsQueuedForDeletion()||!unit.IsInsideTree())continue;
            if(restoreFormation)unit.Position=home;
            if(!unit.IsDead){unit.Sprite.SpeedScale=1;unit.Sprite.Play(BattleAnimations.Idle);unit.Sprite.FlipH=!unit.IsAlly;}
        }
        _homes.Clear();_cycles.Clear();_plan=null;
    }
    private static bool Valid(BattleUnit unit)=>GodotObject.IsInstanceValid(unit)&&!unit.IsQueuedForDeletion()&&!unit.IsDead;
    private static bool Ranged(BattleUnit unit)=>CharacterData.Get(unit.CardKind).Combat?.Actions.FirstOrDefault(a=>a.Mode is "melee" or "ranged")?.Mode=="ranged";
    public override void _Process(double delta)
    {
        if(!Active)return;
        var living=_shop.NetworkUnits.Values.Where(Valid).ToArray();
        foreach(var unit in living){
            if(!_homes.ContainsKey(unit))_homes[unit]=unit.Position;
            if(_posing.Contains(unit))continue;
            if(unit.Stunned){unit.Sprite.Play(BattleAnimations.Idle);continue;}
            var enemy=_targets.TryGetValue(unit,out var locked)&&Valid(locked)?locked:
                living.Where(u=>u.IsAlly!=unit.IsAlly).OrderBy(u=>unit.Position.DistanceSquaredTo(u.Position)).ThenBy(u=>u.ServerId,StringComparer.Ordinal).FirstOrDefault();
            if(enemy==null){unit.Sprite.Play(BattleAnimations.Idle);continue;}
            unit.Sprite.FlipH=enemy.Position.X<unit.Position.X;
            if(unit.Sprite.Animation==BattleAnimations.Hit&&unit.Sprite.IsPlaying())continue;
            float reach=_targets.ContainsKey(unit)?26:Ranged(unit)?85:26;
            Vector2 difference=enemy.Position-unit.Position;
            if(difference.Length()>reach+2){
                unit.Sprite.Play(unit.Sprite.SpriteFrames.HasAnimation(BattleAnimations.Walk)?BattleAnimations.Walk:unit.Sprite.SpriteFrames.HasAnimation("flying")?new StringName("flying"):BattleAnimations.Idle);
                float travel=Math.Min(WalkSpeed*(float)delta,difference.Length()-reach);
                Vector2 movement=difference.Normalized()*travel;
                // Keep adjacent bodies apart, without pushing or teleporting logical slots.
                foreach(var other in living){if(other==unit||other==enemy)continue;var away=unit.Position-other.Position;float distance=away.Length();if(distance>1&&distance<16)movement+=away/distance*(16-distance)*(float)delta*2;}
                unit.Position+=movement;continue;
            }
            if(unit.Sprite.IsPlaying()&&unit.Sprite.Animation.ToString().StartsWith("attack"))continue;
            var rules=CharacterData.Get(unit.CardKind).Combat;
            string[] animations=rules?.Actions.Select(a=>a.Animation).Distinct().ToArray()??new[]{"attack01"};
            int cycle=_cycles.GetValueOrDefault(unit);_cycles[unit]=cycle+1;
            string animation=CombatVisualSettings.AnimationFor(unit.CardKind,animations[cycle%animations.Length],unit.Sprite.SpriteFrames);
            unit.Sprite.SpeedScale=1;unit.Sprite.Play(animation);unit.Sprite.SetFrameAndProgress(0,0);
        }
    }
    private void Check(int generation,long deadline)
    {
        if(!Active||generation!=_generation||!IsInsideTree()||!ReferenceEquals(_plan,_shop.ReplayPlan)||(deadline>0&&_battle.ServerVisualTimeMs>=deadline))throw new OperationCanceledException();
    }
    private async Task Frame(int generation,long deadline)
    {
        await ToSignal(GetTree(),SceneTree.SignalName.ProcessFrame);Check(generation,deadline);
    }
    public async Task PlayEvent(BattleUnit actor,BattleUnit victim,OnlineCombatEvent combat,long deadline)
    {
        int generation=_generation;Check(generation,deadline);
        string original=combat.Animation.Length>0?combat.Animation:"attack01";
        string animation=CombatVisualSettings.AnimationFor(actor.CardKind,original,actor.Sprite.SpriteFrames);
        if(!actor.Sprite.SpriteFrames.HasAnimation(animation))animation=BattleAnimations.Attack.ToString();
        int originalFrames=actor.Sprite.SpriteFrames.HasAnimation(original)?actor.Sprite.SpriteFrames.GetFrameCount(original):actor.Sprite.SpriteFrames.GetFrameCount(animation);
        bool melee=combat.Mode is "melee" or "execute";
        try {
            if(melee&&actor!=victim&&Valid(actor)&&Valid(victim)){
                _targets[actor]=victim;_targets[victim]=actor;
                float approach=0;
                while(actor.Position.DistanceTo(victim.Position)>29&&Valid(actor)&&Valid(victim)&&approach<4){await Frame(generation,deadline);approach+=(float)GetProcessDeltaTime();}
            }
            _posing.Add(actor);actor.Sprite.SpeedScale=1;actor.Sprite.FlipH=victim.Position.X<actor.Position.X;
            if(!actor.IsDead){actor.Sprite.Play(animation);actor.Sprite.SetFrameAndProgress(0,0);}
            if(combat.Hits.Length==0){
                int frame=actor.Sprite.SpriteFrames.GetFrameCount(animation)/2;
                while(!actor.IsDead&&actor.Sprite.Animation.ToString()==animation&&actor.Sprite.IsPlaying()&&actor.Sprite.Frame<frame)await Frame(generation,deadline);
                Check(generation,deadline);_shop.ApplyCombatEvent(combat);
                if(combat.Damage>0){CharacterImpactEffects.Play(actor,victim,original,"damage",combat.Damage);_battle.NotifyBrawlImpact(actor,victim);}
            }else foreach(var hit in combat.Hits){
                int frame=CombatVisualSettings.RemapFrame(hit.Frame,originalFrames,actor.Sprite.SpriteFrames.GetFrameCount(animation));
                int previous=actor.Sprite.Frame;
                while(!actor.IsDead&&actor.Sprite.Animation.ToString()==animation&&actor.Sprite.IsPlaying()&&actor.Sprite.Frame<frame){
                    _battle.NotifyBrawlProgress(hit,(actor.Sprite.Frame+actor.Sprite.FrameProgress-previous)/Math.Max(1,frame-previous));await Frame(generation,deadline);
                }
                Check(generation,deadline);_shop.ApplyCombatHit(hit);
                if(_shop.NetworkUnits.TryGetValue(hit.Target,out var target)){
                    var source=_shop.NetworkUnits.GetValueOrDefault(hit.Source)??actor;CharacterImpactEffects.Play(source,target,original,hit.Kind,hit.Damage);
                    if(hit.Damage>0)_battle.NotifyBrawlImpact(source,target);
                }
            }
            // Other units keep attacking throughout; nobody returns to a deployment slot.
            while(!actor.IsDead&&actor.Sprite.Animation.ToString()==animation&&actor.Sprite.IsPlaying())await Frame(generation,deadline);
        }finally {
            if(generation==_generation){_posing.Remove(actor);_targets.Remove(actor);_targets.Remove(victim);}
        }
    }
    public override void _ExitTree()=>Stop(false);
}
