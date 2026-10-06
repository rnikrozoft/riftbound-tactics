using System;
using System.Collections.Generic;
using System.Linq;

// Offline simulation emits the same snapshot protocol as the authoritative Go engine.
// Online battles never call this simulator or trust a client-computed result.
public sealed class LocalCombat
{
    // Keep offline/replay timing aligned with the server's presentation budget.
    private static long PresentationMs(OnlineCombatEvent combat,CombatRules? rules)
    {
        int frames=combat.Mode=="stun"?4:rules?.Actions.FirstOrDefault(a=>a.Animation==combat.Animation)?.Frames??8;
        if(combat.Mode!="stun"&&rules!=null){
            if(rules.First?.Animation==combat.Animation)frames=Math.Max(frames,rules.First.Frames);
            if(rules.Finisher==combat.Animation&&rules.FinisherFrames>0)frames=rules.FinisherFrames;
        }
        foreach(var hit in combat.Hits)frames=Math.Max(frames,hit.Frame+1);
        long duration=(Math.Max(1,frames)*1000+11)/12+180;
        if(combat.Mode is "melee" or "execute")duration+=560;
        duration+=combat.Hits.Count(h=>h.Damage>0)*75;
        if(combat.Dead&&combat.Target!=combat.Attacker)duration+=820;
        return duration;
    }
    private sealed class Fighter
    {
        public OnlineCombatUnit Unit=null!;
        public CombatRules? Rules;
        public int Hp,Armor,Side,Turn,Fire,Poison,PoisonTicks,CanAct,Summons;
        public bool Stun,Grace,Curse,AntiHeal,BlockUsed,GuardUsed,HealUsed,LowUsed,KillUsed,ExecuteUsed,Revived,DeathUsed;
    }
    private readonly List<Fighter> _units=new();
    private readonly Random _rng;
    private readonly OnlinePlan _plan=new();
    private readonly List<OnlineCombatHit> _hits=new();
    private readonly HashSet<string> _block=new();
    private int _step;
    private static OnlineCombatUnit CopyUnit(OnlineCombatUnit u)=>new() {Id=u.Id,Team=u.Team,Token=u.Token,Kind=u.Kind,Slot=u.Slot,Stars=u.Stars,MaxHp=u.MaxHp,InitialHp=u.InitialHp,Armor=u.Armor,Summoned=u.Summoned};
    public LocalCombat(int seed=0)=>_rng=new Random(seed);
    private static int Percent(int n,int p)=>(n*p+50)/100;
    private static int Scaled(int n,int stars)=>(int)Math.Round(n*new[]{1,1.5,2,2.5}[Math.Clamp(stars,1,4)-1],MidpointRounding.AwayFromZero);
    private List<Fighter> Alive(int side)=>_units.Where(u=>u.Side==side&&u.Hp>0).ToList();
    private Fighter? Pick(List<Fighter> list)=>list.Count==0?null:list[_rng.Next(list.Count)];
    private void Record(int frame,Fighter source,Fighter target,int damage,string kind)=>_hits.Add(new OnlineCombatHit {
        Frame=frame,Source=source.Unit.Id,Target=target.Unit.Id,Damage=damage,Kind=kind,
        Changes=_units.Select(u=>new OnlineCombatChange {Id=u.Unit.Id,Hp=u.Hp,Armor=u.Armor,Slot=u.Unit.Slot,Fire=u.Fire,Stun=u.Stun,Poison=u.Poison,Curse=u.Curse,AntiHeal=u.AntiHeal}).ToArray()
    });
    private void Status(Fighter u,string status,int stars){if(u.Hp<=0)return;switch(status){case "fire":u.Fire=Scaled(3,stars);break;case "stun":if(!u.Grace)u.Stun=true;break;case "poison":u.Poison=Scaled(2,stars);u.PoisonTicks=2;break;}}
    private void Heal(Fighter source,Fighter u,int n,int frame){if(u.Hp<=0||u.AntiHeal)return;int old=u.Hp;u.Hp=Math.Min(u.Unit.MaxHp,u.Hp+n);if(old!=u.Hp)Record(frame,source,u,0,"heal");}
    private int Damage(Fighter source,Fighter u,int n,int pierce,int frame,string kind)
    {
        if(u.Hp<=0)return 0;int old=u.Hp;
        if(kind is not ("poison" or "execute")&&u.Rules?.BlockFirst==true&&!u.BlockUsed){_block.Add(u.Unit.Id);u.BlockUsed=true;}
        if(kind is not ("poison" or "execute")&&_block.Contains(u.Unit.Id))n=(n+1)/2;
        int direct=n*pierce/100,rest=n-direct,absorbed=Math.Min(u.Armor,rest);u.Armor-=absorbed;u.Hp=Math.Max(0,u.Hp-direct-(rest-absorbed));int lost=old-u.Hp;Record(frame,source,u,n,kind);
        if(u.Hp>0&&u.Rules!=null){
            if(old>u.Hp&&u.Rules.FirstHeal>0&&!u.HealUsed){u.HealUsed=true;Heal(source,u,Scaled(u.Rules.FirstHeal,u.Unit.Stars),frame);}
            if(u.Hp*2<u.Unit.MaxHp&&u.Rules.LowArmor>0&&!u.LowUsed){u.LowUsed=true;u.Armor+=Scaled(u.Rules.LowArmor,u.Unit.Stars);Record(frame,u,u,0,"armor");}
        }
        if(u.Hp==0&&u.Rules?.DeathArmor>0&&!u.DeathUsed){u.DeathUsed=true;var friend=Pick(Alive(u.Side));if(friend!=null){friend.Armor+=Scaled(u.Rules.DeathArmor,u.Unit.Stars);Record(frame,u,friend,0,"armor");}}
        return lost;
    }
    private static bool Neighbor(int a,int b,string shape)
    {
        int ac=a/3,ar=a%3,bc=b/3,br=b%3;
        return shape switch {"sides"=>ac==bc&&Math.Abs(ar-br)==1,"back"=>ac==1&&bc==0&&ar==br,"column"=>ar==br&&ac!=bc,"around"=>a!=b&&Math.Abs(ac-bc)<=1&&Math.Abs(ar-br)<=1,_=>false};
    }
    private List<Fighter> Splash(Fighter target,string shape)=>Alive(target.Side).Where(u=>u!=target&&Neighbor(target.Unit.Slot,u.Unit.Slot,shape)).ToList();
    private int Empty(int side){for(int i=0;i<6;i++)if(!Alive(side).Any(u=>u.Unit.Slot==i))return i;return -1;}
    private void EndAction(Fighter a,int frame){if(a.Hp>0&&a.PoisonTicks>0){Damage(a,a,a.Poison,100,frame,"poison");a.PoisonTicks--;if(a.PoisonTicks==0)a.Poison=0;}}
    private OnlineCombatEvent Action(Fighter a,Fighter target,long at)
    {
        var rule=a.Rules?.Actions[a.Turn%a.Rules.Actions.Length]??new AttackRule {HitFrames=new[]{2}};
        if(a.Turn==0&&a.Rules?.First!=null)rule=a.Rules.First;
        a.Turn++;_hits.Clear();_block.Clear();
        var combat=new OnlineCombatEvent {Index=_plan.Events.Length,Attacker=a.Unit.Id,Target=target.Unit.Id,AtMs=at,Animation=rule.Animation,Mode=rule.Mode};
        if(rule.MinStars>a.Unit.Stars)combat.Animation="attack01";
        if(a.Stun){a.Stun=false;a.Grace=true;combat.Animation="hit";combat.Mode="stun";Record(0,a,a,0,"stun_skip");EndAction(a,0);Record(0,a,a,0,"action_end");return Finish();}
        bool grace=a.Grace,antiHeal=a.AntiHeal,handled=false;int slot=Empty(a.Side);
        switch(rule.Mode){
            case "summon":
                if(slot>=0&&a.Summons<2){var c=CharacterData.All.FirstOrDefault(c=>c.ScenePath.EndsWith("/skeleton.tscn"));if(c!=null){a.Summons++;int hp=Scaled(5,a.Unit.Stars);var u=new Fighter {Unit=new OnlineCombatUnit {Id=$"{a.Unit.Team}:summon:{a.Unit.Token}:{a.Summons}",Team=a.Unit.Team,Kind=c.Kind,Stars=a.Unit.Stars,Slot=slot,MaxHp=hp,Summoned=true},Hp=hp,Side=a.Side,CanAct=_step+2};_units.Add(u);_plan.Units=_plan.Units.Append(CopyUnit(u.Unit)).ToArray();target=u;combat.Target=u.Unit.Id;Record(2,a,u,0,"summon");handled=true;}}break;
            case "revive":
                var dead=_units.Where(u=>u.Side==a.Side&&u.Hp==0&&!u.Unit.Summoned&&!u.Revived).OrderByDescending(u=>u.Unit.MaxHp).FirstOrDefault();
                if(slot>=0&&dead!=null&&a.Summons==0){a.Summons=1;dead.Revived=true;dead.Hp=Math.Max(1,Percent(dead.Unit.MaxHp,40));dead.Armor=0;dead.Unit.Slot=slot;dead.Fire=dead.Poison=dead.PoisonTicks=0;dead.Stun=dead.Curse=dead.AntiHeal=false;dead.CanAct=_step+2;target=dead;combat.Target=dead.Unit.Id;Record(2,a,dead,0,"revive");handled=true;}break;
            case "heal":
                var friends=Alive(a.Side);int lost=friends.Max(u=>u.Unit.MaxHp-u.Hp);target=Pick(friends.Where(u=>u.Unit.MaxHp-u.Hp==lost).ToList())!;combat.Target=target.Unit.Id;Heal(a,target,Scaled(rule.Heal,a.Unit.Stars),2);handled=true;break;
            case "curse":target.Curse=true;Record(2,a,target,0,"curse");handled=true;break;
        }
        if(!handled){
            string mode=rule.Mode is "summon" or "revive"?"melee":rule.Mode;combat.Mode=mode;
            if(rule.SwapBack||rule.TargetBack){var back=Pick(Alive(1-a.Side).Where(u=>u.Unit.Slot<3).ToList());if(back!=null){if(rule.SwapBack){int front=back.Unit.Slot+3;var u=Alive(1-a.Side).FirstOrDefault(u=>u.Unit.Slot==front);if(u!=null)u.Unit.Slot=back.Unit.Slot;back.Unit.Slot=front;Record(0,a,back,0,"move");}target=back;combat.Target=back.Unit.Id;}}
            var guard=Alive(target.Side).FirstOrDefault(u=>u!=target&&u.Rules?.Bodyguard==true&&!u.GuardUsed&&Neighbor(u.Unit.Slot,target.Unit.Slot,"back"));
            if(guard!=null){guard.GuardUsed=true;target=guard;combat.Target=guard.Unit.Id;Record(0,guard,guard,0,"guard");}
            int basis=a.Hp,counter=target.Hp,power=rule.Power,hits=Math.Max(1,rule.Hits),pierce=rule.Pierce;int[] hitFrames=rule.HitFrames;
            if(rule.Mode is "summon" or "revive"){power=100;hits=1;hitFrames=new[]{2};}
            if(a.Curse){power/=2;a.Curse=false;}
            int total=Percent(basis,power);if(a.Turn==1&&a.Rules!=null)total+=Scaled(a.Rules.FirstBonus,a.Unit.Stars);
            bool execute=a.Rules?.Execute>0&&!a.ExecuteUsed&&target.Hp*100<=target.Unit.MaxHp*a.Rules.Execute;
            if(execute){a.ExecuteUsed=true;total=target.Hp;pierce=100;hits=1;hitFrames=new[]{2};combat.Animation="attack03";combat.Mode="execute";}
            else if(a.Rules!=null&&a.Rules.Finisher.Length>0&&total-Math.Min(target.Armor,total*(100-pierce)/100)>=target.Hp){combat.Animation=a.Rules.Finisher;if(a.Rules.FinisherHitFrames.Length>0)hitFrames=a.Rules.FinisherHitFrames;if(a.Rules.FinisherHits>1){hits=a.Rules.FinisherHits;total=Percent(basis,120);if(hitFrames.Length!=hits)hitFrames=new[]{3,7};}}
            if(target.Fire>0){total+=target.Fire;target.Fire=0;}
            var extra=a.Unit.Stars>=rule.MinStars?Splash(target,rule.Splash):new List<Fighter>();int stolen=0,lastFrame=2;
            for(int h=0;h<hits&&target.Hp>0;h++){lastFrame=h<hitFrames.Length?hitFrames[h]:2+h*3;int n=total/hits+(h<total%hits?1:0);stolen+=Damage(a,target,n,pierce,lastFrame,execute?"execute":"damage");combat.Damage+=n;
                if(h==0)foreach(var u in extra){Damage(a,u,Percent(basis,rule.SplashPower),pierce,lastFrame,"splash");Status(u,rule.SplashStatus,a.Unit.Stars);}
            }
            if(mode=="melee"){if(a.Rules?.KillNoCounter==true&&!a.KillUsed&&target.Hp==0)a.KillUsed=true;else{if(a.Fire>0){counter+=a.Fire;a.Fire=0;}Damage(target,a,counter,0,lastFrame,"counter");}}
            Status(target,rule.Status,a.Unit.Stars);if(rule.AntiHeal&&target.Hp>0)target.AntiHeal=true;Record(lastFrame,a,target,0,"status");
            if(a.Hp>0){if(rule.Lifesteal>0)Heal(a,a,Math.Min(stolen,Scaled(rule.Lifesteal,a.Unit.Stars)),lastFrame);if(rule.TeamHeal>0)foreach(var u in Alive(a.Side))Heal(a,u,Scaled(rule.TeamHeal,a.Unit.Stars),lastFrame);}
            if(target.Hp==0&&a.Rules!=null){if(a.Rules.KillStatus.Length>0&&!a.KillUsed){a.KillUsed=true;foreach(var u in Splash(target,a.Rules.KillSplash)){Status(u,a.Rules.KillStatus,a.Unit.Stars);Record(lastFrame,a,u,0,"status");}}if(a.Rules.Suicide&&a.Hp>0)Damage(a,a,a.Hp+a.Armor,100,lastFrame,"suicide");}
        }
        int endFrame=Math.Max(2,rule.Frames-1);if(combat.Animation==a.Rules?.Finisher&&a.Rules!.FinisherFrames>0)endFrame=Math.Max(2,a.Rules.FinisherFrames-1);
        EndAction(a,endFrame);if(grace)a.Grace=false;if(antiHeal)a.AntiHeal=false;Record(endFrame,a,a,0,"action_end");return Finish();
        OnlineCombatEvent Finish(){combat.TargetHp=target.Hp;combat.Dead=target.Hp==0;combat.Hits=_hits.ToArray();return combat;}
    }
    public OnlinePlan Simulate(IEnumerable<OnlineCombatUnit> initial)
    {
        foreach(var u in initial){var stats=CharacterData.Stats(u.Kind,u.Stars);u.MaxHp=stats.Hp;u.InitialHp=stats.Hp;u.Armor=stats.Armor;_units.Add(new Fighter {Unit=u,Rules=CharacterData.Get(u.Kind).Combat,Hp=u.MaxHp,Armor=u.Armor,Side=u.Team=="A"?0:1});}
        _plan.Units=_units.Select(u=>CopyUnit(u.Unit)).ToArray();var events=new List<OnlineCombatEvent>();long at=0;int side=0;
        for(_step=0;_step<512&&Alive(0).Count>0&&Alive(1).Count>0;_step++){
            var a=Pick(Alive(side).Where(u=>u.CanAct<=_step).ToList());if(a!=null){var e=Action(a,Pick(Alive(1-side))!,at);e.Index=events.Count;events.Add(e);at+=PresentationMs(e,a.Rules);}side=1-side;
        }
        _plan.Events=events.ToArray();_plan.EndMs=at;_plan.Winner=Alive(0).Count>0&&Alive(1).Count==0?"A":Alive(1).Count>0&&Alive(0).Count==0?"B":"DRAW";return _plan;
    }
}
