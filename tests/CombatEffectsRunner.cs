using Godot;
using System;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;

public partial class CombatEffectsRunner : Node
{
    private int _checks;
    private void Check(bool ok,string message){_checks++;if(!ok)throw new Exception(message);}
    private async Task Frames(int count=2){for(int i=0;i<count;i++)await ToSignal(GetTree(),SceneTree.SignalName.ProcessFrame);}
    private async Task Capture(string name){if(DisplayServer.GetName()=="headless")return;await ToSignal(RenderingServer.Singleton,RenderingServer.SignalName.FramePostDraw);GetViewport().GetTexture().GetImage().SavePng($"res://tests/{name}.png");}
    public override void _Ready()=>Callable.From(Run).CallDeferred();
    private async void Run()
    {
        try{
            int Kind(string slug)=>CharacterData.All.First(c=>c.ScenePath.EndsWith("/"+slug+".tscn")).Kind;
            string Copy(string slug,int stars=1)=>CharacterData.CombatDescription(Kind(slug),stars);
            Check(Copy("ghostfire",2).Contains("ดาเมจเพิ่ม 5"),"Description must round scaled fire damage like combat");
            Check(Copy("werebear",2).Contains("ได้เกราะ 8"),"Description must use current star armor bonus");
            Check(Copy("slime").Contains("ฟื้นเลือดทันที 2"),"First injury healing occurs immediately");
            Check(Copy("armored_axeman").Contains("2 ดาวขึ้นไป")&&!Copy("armored_axeman",2).Contains("2 ดาวขึ้นไป"),"Area attack star requirement");
            Check(Copy("knight_vanguard").Contains("อีกแถวในแนวเดียว"),"Kill effect must describe actual target area");
            Check(Copy("swordsman").Contains("2 ครั้ง ดาเมจรวม 120%")&&Copy("swordsman").Contains("4 ครั้ง ดาเมจรวม 140%"),"Combo totals must match combat");
            Check(Copy("wizard").Contains("40% เกราะ 0")&&Copy("wizard").Contains("ตัวที่เคยคืนชีพ"),"Revival restrictions");
            Check(!Copy("greatsword_skeleton").Contains("วนซ้ำ")&&!Copy("hellhound").Contains("ท่าปิดฉาก"),"Do not describe animation variations as abilities");
            foreach(var definition in CharacterData.All){
                foreach(int rank in new[]{1,2,3,4}){
                    string copy=CharacterData.CombatDescription(definition.Kind,rank);
                    Check(copy.Length>10&&!new[]{"prototype","prototyle","attack01","attack02","attack03","ข้อเสนอ","Animation"}.Any(term=>copy.Contains(term,StringComparison.OrdinalIgnoreCase)),"Player copy must omit development notes: "+definition.Name);
                }
            }
            var blood=new BloodScreenEffect();AddChild(blood);
            var hit1=new OnlineCombatHit {Frame=1,Changes=new[]{new OnlineCombatChange {Id="A:blood",Hp=80}}};
            var heal=new OnlineCombatHit {Frame=3,Changes=new[]{new OnlineCombatChange {Id="A:blood",Hp=90}}};
            var last=new OnlineCombatHit {Frame=5,Changes=new[]{new OnlineCombatChange {Id="A:blood",Hp=0}}};
            var bloodPlan=new OnlinePlan {Winner="B",Units=new[]{new OnlineCombatUnit {Id="A:blood",Team="A",MaxHp=100,InitialHp=100}},Events=new[]{new OnlineCombatEvent {Hits=new[]{hit1,heal,last}}}};
            blood.Prepare(bloodPlan,"A",true);blood.Preview(hit1,.5f);
            float firstOpacity=.08f+.92f*20/110f;
            Check(blood.Opacity>0&&blood.Opacity<firstOpacity,"Blood must fade during attack before impact");
            blood.Apply(hit1);Check(Mathf.IsEqualApprox(blood.Opacity,firstOpacity),"Blood must reflect precomputed HP loss");
            blood.Apply(heal);Check(Mathf.IsEqualApprox(blood.Opacity,firstOpacity),"Healing must not reverse blood fade");
            blood.Preview(last,.5f);Check(blood.Opacity>firstOpacity&&blood.Opacity<1,"Last strike must fade progressively");
            blood.Apply(last);Check(blood.Opacity==1,"Blood must be full on last HP impact");
            blood.Prepare(bloodPlan,"B",true);blood.Apply(last);Check(blood.Opacity==0,"Winner must have no defeat blood");
            blood.Prepare(bloodPlan,"A",false);blood.Apply(last);Check(blood.Opacity==0,"Surviving round loss must have no elimination blood");
            var primary=new OnlineCombatHit {Frame=5};bloodPlan.Events[0].Hits=new[]{primary,last};
            blood.Prepare(bloodPlan,"A",true);blood.Preview(primary,.5f);Check(blood.Opacity>0&&blood.Opacity<1,"Simultaneous counter must fade before shared impact");
            bloodPlan.Events=new[]{new OnlineCombatEvent {Hits=new[]{hit1,heal}},new OnlineCombatEvent {Hits=new[]{primary,last}}};
            blood.Prepare(bloodPlan,"A",true);blood.Preview(hit1,.5f);blood.Apply(hit1);blood.Apply(heal);
            Check(Mathf.IsEqualApprox(blood.Opacity,firstOpacity),"Last two survivors fade across turns");
            blood.Preview(primary,.5f);Check(blood.Opacity>firstOpacity&&blood.Opacity<1,"Last survivors continue fading");
            blood.Apply(last);Check(blood.Opacity==1,"Final decisive hit must fill blood effect");
            var third=new OnlineCombatHit {Frame=1,Changes=new[]{new OnlineCombatChange {Id="A:third",Hp=0}}};
            bloodPlan.Units=new[]{new OnlineCombatUnit {Id="A:third",Team="A",MaxHp=10},new OnlineCombatUnit {Id="A:blood",Team="A",MaxHp=100},new OnlineCombatUnit {Id="A:other",Team="A",MaxHp=10}};
            var other=new OnlineCombatHit {Frame=8,Changes=new[]{new OnlineCombatChange {Id="A:other",Hp=0}}};
            bloodPlan.Events=new[]{new OnlineCombatEvent {Hits=new[]{third}},new OnlineCombatEvent {Hits=new[]{last,other}}};
            int deaths=0;blood.LastUnitDied+=()=>deaths++;
            blood.Prepare(bloodPlan,"A",true);blood.Preview(third,.5f);Check(blood.Opacity==0,"No blood before two survivors remain");
            blood.Apply(third);ulong fadeUntil=Time.GetTicksMsec()+450;while(Time.GetTicksMsec()<fadeUntil)await Frames(1);
            Check(blood.Opacity>0&&blood.Opacity<=.081f,"Blood starts fading as soon as two survivors remain");
            blood.Apply(last);Check(deaths==0,"Defeat popup must wait for actual last unit");
            blood.Apply(other);Check(deaths==1&&blood.Opacity==1,"Last death triggers popup once with full blood");
            blood.Apply(other);Check(deaths==1,"Duplicate final state must not reopen popup");
            var catchup=new OnlineCombatHit {Frame=9,Changes=new[]{new OnlineCombatChange {Id="A:third",Hp=0},new OnlineCombatChange {Id="A:blood",Hp=0},new OnlineCombatChange {Id="A:other",Hp=0}}};
            bloodPlan.Events[1].Hits=new[]{last,other,catchup};blood.Prepare(bloodPlan,"A",true);blood.Apply(catchup);
            Check(blood.Eliminated&&deaths==2,"Late replay reconciliation must still trigger defeat popup");
            blood.Prepare(new OnlinePlan {Winner="B"},"A",true);
            Check(blood.Opacity==1&&blood.Eliminated&&deaths==3,"Empty final army must show full blood immediately");
            var modal=new GameModal();AddChild(modal);
            var stillBlood=new TextureRect {Name="DefeatEffect",Modulate=new Color(1,1,1,1)};modal.AddChild(stillBlood);
            var popupFade=modal.FadeIn(5);var shade=modal.GetChild<Control>(0);
            Check(shade.Modulate.A==0&&stillBlood.Modulate.A==1,"Popup starts invisible without resetting blood");
            ulong popupStart=Time.GetTicksMsec();while(Time.GetTicksMsec()-popupStart<2500)await Frames(1);
            Check(shade.Modulate.A>.3f&&shade.Modulate.A<.7f,"Popup gradually fades over five seconds");
            await ToSignal(popupFade,Tween.SignalName.Finished);Check(shade.Modulate.A==1,"Popup fully appears at five seconds");modal.QueueFree();
            blood.QueueFree();
            var field=GD.Load<PackedScene>("res://scenes/main.tscn").Instantiate<BattleDisplay>();
            var online=field.GetNode("OnlineBattle");field.RemoveChild(online);online.Free();
            var battle=field.GetNode<BattleDemo>("BattleDemo");battle.NetworkEnabled=true;AddChild(field);await Frames();
            var shop=field.GetNode<CardShop>("UI/SafeArea/Content/CardShop");
            var speed=field.GetNode<BattleSpeedControl>("BattleSpeedControl");
            speed.CycleSpeed();await Frames();
            Check(BattleSpeedControl.SelectedSpeed==2&&Engine.TimeScale==1,"Selected speed must not accelerate preparation");
            battle.SpeedEnabled=true;await Frames();Check(Engine.TimeScale==2,"Combat must enable selected double speed");
            speed.CycleSpeed();await Frames();Check(BattleSpeedControl.SelectedSpeed==4&&Engine.TimeScale==4,"Speed button must cycle to quadruple speed");
            var speedSample=new Node2D();field.AddChild(speedSample);ulong speedStart=Time.GetTicksMsec();
            var fastTween=field.CreateTween();fastTween.TweenProperty(speedSample,"position:x",40f,.4);
            await ToSignal(fastTween,Tween.SignalName.Finished);
            Check(Time.GetTicksMsec()-speedStart<300&&Mathf.IsEqualApprox(speedSample.Position.X,40),"Quadruple speed must shorten real animation duration without losing the endpoint");speedSample.QueueFree();
            speed.CycleSpeed();await Frames();Check(BattleSpeedControl.SelectedSpeed==1&&Engine.TimeScale==1,"Speed cycle must return to normal");battle.SpeedEnabled=false;
            Check(field.GetNode("HeroSlots").GetChildCount()==6,"Six visible hero deployment tiles");
            Check(field.GetNode("EnemyHeroSlots").GetChildCount()==6,"Six opponent deployment tiles");
            var state=JsonSerializer.Deserialize(Godot.FileAccess.GetFileAsString("res://tests/combat_fixture.json"),GameJsonContext.Default.OnlineState)!;
            state.Phase="preparation";shop.ApplyNetworkState(state,"effects-a");state.Phase="battle";shop.ApplyNetworkState(state,"effects-a");shop.SetReplayPlan(state.Battle!);await Frames();
            var plan=state.Battle!;Check(plan.Events.Length>0,"Backend effects plan missing");
            foreach(var unit in shop.NetworkUnits.Values){Check(unit.Attack==unit.Health,"HP = attack");Check(unit.GetNode<Label>("ArmorValue").Text==unit.Armor.ToString(),"Armor label");Check(unit.GetNode<Label>("PowerValue").Text==unit.Health.ToString(),"HP label");var left=unit.GetNode<Label>("ArmorValue");var right=unit.GetNode<Label>("PowerValue");Check(left.Position.X<right.Position.X,"Stat positions");Check(left.GetThemeColor("font_color").B>left.GetThemeColor("font_color").R,"Blue armor");Check(right.GetThemeColor("font_color").R>right.GetThemeColor("font_color").B,"Red HP");}
            var effectCatalog=JsonSerializer.Deserialize(Godot.FileAccess.GetFileAsString("res://data/effect_catalog.json"),GameJsonContext.Default.EffectEntryArray)!;
            foreach(var c in CharacterData.All.GroupBy(c=>c.ScenePath).Select(g=>g.First())){
                for(int attack=1;attack<=3;attack++){
                    string effectName=CharacterImpactEffects.Resolve(c.Kind,$"attack{attack:00}");
                    var entry=effectCatalog.Single(e=>e.Name==effectName);
                    Check(ResourceLoader.Exists(entry.Texture)&&entry.Frames.Length>0,"Missing impact asset: "+c.Name+" "+effectName);
                }
                var unit=GD.Load<PackedScene>(c.ScenePath).Instantiate<BattleUnit>();unit.CardKind=c.Kind;unit.ConfigureStars(1);AddChild(unit);
                foreach(var action in c.Combat!.Actions){Check(unit.Sprite.SpriteFrames.HasAnimation(action.Animation),"Missing alternate attack: "+c.Name);Check(action.HitFrames.Length==action.Hits,"Hit frames: "+c.Name);}
                var idleScale=unit.Sprite.Scale;
                foreach(bool facingLeft in new[]{false,true}){
                    unit.Sprite.FlipH=facingLeft;await Frames(1);
                    var feet=CharacterVisual.GroundAnchor(unit.Sprite);
                    var armor=unit.GetNode<Label>("ArmorValue");var power=unit.GetNode<Label>("PowerValue");
                    Check((armor.Position-feet).IsEqualApprox(new Vector2(-29,-13))&&(power.Position-feet).IsEqualApprox(new Vector2(6,-13)),"Flipped HUD must follow visible feet: "+c.Name);
                    Check(armor.Position.X<power.Position.X,"Facing must never swap armor and power: "+c.Name);
                    var bar=unit.GetNode<ProgressBar>("HealthBar");
                    Check(Mathf.Abs((armor.Position.X+12+power.Position.X+12)/2-(bar.Position.X+bar.Size.X/2))<1,"Stat numbers must stay centered under the body and health bar: "+c.Name);
                    Check(unit.GetNode<Sprite2D>("Star1").Position.IsEqualApprox(feet+new Vector2(0,7)),"Flipped star must stay below visible feet: "+c.Name);
                }
                unit.Sprite.FlipH=false;
                foreach(var name in unit.Sprite.SpriteFrames.GetAnimationNames()){
                unit.Sprite.Play(name);
                for(int f=0;f<unit.Sprite.SpriteFrames.GetFrameCount(name);f++){
                    Check(unit.Sprite.Scale.IsEqualApprox(idleScale),"Character must keep idle scale: "+c.Name+" "+name+" "+f);
                }
                }
                Check(CharacterData.CombatDescription(c.Kind,4).Length>10,"Details missing");unit.QueueFree();
                var local=new LocalCombat(9).Simulate(new[]{new OnlineCombatUnit {Id="A:test",Team="A",Kind=c.Kind,Stars=4,Slot=3},new OnlineCombatUnit {Id="B:test",Team="B",Kind=0,Stars=4,Slot=3}});
                Check(local.Events.Length<=512,"Offline bounded combat");foreach(var e in local.Events)foreach(var h in e.Hits)foreach(var s in h.Changes)Check(s.Hp>=0&&s.Armor>=0,"Offline invalid state");
            }
            await Frames();await Capture("combat-stats");
            if(DisplayServer.GetName()!="headless"){
                var sampleLayer=new CanvasLayer();AddChild(sampleLayer);
                var panel=new Panel {Position=new Vector2(370,20),Size=new Vector2(480,152)};panel.AddThemeStyleboxOverride("panel",GameUi.Box(true));sampleLayer.AddChild(panel);
                for(int i=0;i<4;i++){
                    int kind=CardCatalog.Options(2+i,2).First();
                    var card=new ShopCard {Position=new Vector2(18+i*112,12),Size=new Vector2(100,128),ExpandMode=TextureRect.ExpandModeEnum.IgnoreSize,StretchMode=TextureRect.StretchModeEnum.KeepAspectCentered,Shop=shop};
                    panel.AddChild(card);card.Configure(new CardData(i,CardCatalog.Name(kind),CardCatalog.Art(kind),CardCatalog.Cost(kind),i+1),true,false);
                }
                await Frames();await Capture("combat-card-corners");sampleLayer.QueueFree();
            }
            var first=plan.Events[0];var attacker=shop.NetworkUnits[first.Attacker];var target=shop.NetworkUnits.TryGetValue(first.Target,out var existing)?existing:attacker;
            shop.ShowUnitDetails(attacker);Check(shop.DetailsText.Text.Contains("ARMOR")&&shop.DetailsText.Text.Contains("HP / ATK")&&shop.DetailsText.Text.Contains("ความสามารถ"),"Field details");await Capture("combat-details");shop.CloseDetails();
            await battle.PlayServerEvent(attacker,target,first);
            foreach(var combat in plan.Events.Skip(1))shop.ApplyCombatEvent(combat);
            var final=plan.Events[^1].Hits[^1].Changes;
            foreach(var snapshot in final){if(snapshot.Hp==0&&!shop.NetworkUnits.ContainsKey(snapshot.Id))continue;var unit=shop.NetworkUnits[snapshot.Id];Check(unit.Health==snapshot.Hp&&unit.Armor==snapshot.Armor,"Complete replay reconciliation");}
            // Each animation impact must visibly update the target, rather than collapsing hits.
            var swordsman=shop.NetworkUnits.Values.First(u=>CharacterData.Get(u.CardKind).ScenePath.EndsWith("/swordsman.tscn"));
            var enemy=shop.NetworkUnits.Values.First(u=>!u.IsAlly);swordsman.MaxHealth=100;enemy.MaxHealth=100;
            swordsman.ApplyCombatState(new OnlineCombatChange {Hp=100,Slot=0});enemy.ApplyCombatState(new OnlineCombatChange {Hp=100,Slot=3});
            var hpChanges=new System.Collections.Generic.List<int>();enemy.HealthChanged+=(current,max)=>{if(current<100&&(!hpChanges.Any()||hpChanges[^1]!=current))hpChanges.Add(current);};
            var multi=new OnlineCombatEvent {Attacker=swordsman.ServerId,Target=enemy.ServerId,Mode="ranged",Animation="attack03",TargetHp=80};
            var impactFrames=CharacterData.Get(swordsman.CardKind).Combat!.Actions[2].HitFrames;
            multi.Hits=Enumerable.Range(0,4).Select(i=>new OnlineCombatHit {Frame=impactFrames[i],Source=swordsman.ServerId,Target=enemy.ServerId,Damage=5,Kind="damage",Changes=new[]{new OnlineCombatChange {Id=enemy.ServerId,Hp=95-i*5,Armor=0,Slot=3}}}).ToArray();
            var impacts=new System.Collections.Generic.List<(string Target,string Source,string Effect)>();
            void TrackImpact(Node node){if(node is AnimatedSprite2D effect&&effect.HasMeta("effect_name"))impacts.Add((effect.GetMeta("hit_target").AsString(),effect.GetMeta("hit_source").AsString(),effect.GetMeta("effect_name").AsString()));}
            field.ChildEnteredTree+=TrackImpact;
            await battle.PlayServerEvent(swordsman,enemy,multi);Check(hpChanges.SequenceEqual(new[]{95,90,85,80}),"Four damage impacts must occur in order");
            Check(impacts.Count==4&&impacts.All(p=>p.Target==enemy.ServerId&&p.Source==swordsman.ServerId),"Each combo hit must create exactly one particle on its damage recipient");
            shop.ShowUnitDetails(enemy);Check(shop.DetailsText.Text.Contains("80 / 100"),"Live details must update");await Capture("combat-final-details");
            shop.CloseDetails();
            var origin=swordsman.Position;
            var lethal=new OnlineCombatEvent {Attacker=swordsman.ServerId,Target=enemy.ServerId,Mode="melee",Animation="attack01",TargetHp=80,Hits=new[]{new OnlineCombatHit {Frame=1,Source=enemy.ServerId,Target=swordsman.ServerId,Damage=100,Kind="counter",Changes=new[]{new OnlineCombatChange {Id=swordsman.ServerId,Hp=0,Slot=swordsman.GridSlot}}}}};
            await battle.PlayServerEvent(swordsman,enemy,lethal);
            Check(impacts.Count==5&&impacts[^1].Target==swordsman.ServerId&&impacts[^1].Source==enemy.ServerId&&impacts[^1].Effect==CharacterImpactEffects.Resolve(enemy.CardKind,"attack01"),"Counter particle must use the countering character and hit the attacker");
            field.ChildEnteredTree-=TrackImpact;
            Check(swordsman.IsDead&&swordsman.Position.DistanceTo(origin)>1,"Dead attacker must stay at impact position");
            Check(swordsman.Sprite.Animation==BattleAnimations.Die,"Death animation must replace attack");
            var until=Time.GetTicksMsec()+3000;
            while(swordsman.Visible&&Time.GetTicksMsec()<until)await Frames(1);
            Check(!swordsman.Visible&&swordsman.Modulate.A==0,"Death animation must finish then fade away");
            swordsman.ApplyCombatState(new OnlineCombatChange {Hp=40,Slot=0});
            Check(swordsman.Visible&&swordsman.Modulate.A==1&&swordsman.Sprite.Animation==BattleAnimations.Idle,"Revive must restore visibility");
            Check(CharacterImpactEffects.Play(swordsman,enemy,"attack01","damage",0)==null,"Zero damage must not create an attack particle");
            foreach(string kind in new[]{"heal","revive","curse","summon","armor","poison"}){
                var particle=CharacterImpactEffects.Play(swordsman,enemy,"attack01",kind,kind=="poison"?2:0)!;
                Check(particle.SpriteFrames.GetFrameCount(BattleAnimations.Effect)>0&&particle.GetMeta("effect_name").AsString().StartsWith("spell_"),"Skill needs its own effect: "+kind);
                Check(particle.GetParent()==enemy.GetParent(),"Impact must survive recipient death fade");particle.QueueFree();
            }
            var sampleImpact=CharacterImpactEffects.Play(swordsman,enemy,"attack02","damage",5)!;
            await Frames(4);await Capture("combat-hit-particles");
            int effectFrame=sampleImpact.Frame;GetTree().Paused=true;
            ulong pauseUntil=Time.GetTicksMsec()+200;while(Time.GetTicksMsec()<pauseUntil)await Frames(1);
            Check(sampleImpact.Frame==effectFrame,"Replay pause must freeze impact particles");GetTree().Paused=false;
            ulong effectUntil=Time.GetTicksMsec()+3000;while(GodotObject.IsInstanceValid(sampleImpact)&&Time.GetTicksMsec()<effectUntil)await Frames(1);
            Check(!GodotObject.IsInstanceValid(sampleImpact),"Completed particles must remove themselves");
            swordsman.TakeDamage(1000);swordsman.ResetHealth();await Frames(50);
            Check(swordsman.Visible&&swordsman.Modulate.A==1&&!swordsman.IsDead,"New round must cancel old death fade");
            foreach(var unit in shop.NetworkUnits.Values)unit.ApplyCombatState(new OnlineCombatChange {Hp=unit.Health,Armor=unit.Armor,Slot=unit.GridSlot});
            swordsman.ConfigureStars(4);enemy.ResetHealth();
            swordsman.ApplyCombatState(new OnlineCombatChange {Hp=swordsman.Health,Armor=swordsman.Armor,Slot=swordsman.GridSlot,Fire=3,Poison=2,Stun=true,Curse=true,AntiHeal=true});
            enemy.ApplyCombatState(new OnlineCombatChange {Hp=enemy.Health,Armor=enemy.Armor,Slot=enemy.GridSlot,Fire=3});
            await Frames();var statusPanel=field.GetNode<CombatStatusPanel>("UI/SafeArea/Content/CombatStatusPanel");
            Check(statusPanel.Visible&&statusPanel.StatusCount==5,"Side panel must show unique active status types across both teams");
            Check(statusPanel.FindChildren("fire","",true,false).Count==1,"Repeated burn must have one sidebar row");
            Check(swordsman.GetChildren().OfType<Sprite2D>().Count(s=>s.Name.ToString().StartsWith("Status_")&&s.Visible)==5,"Active statuses must appear as overhead icons");
            Check(swordsman.GetNodeOrNull<Label>("CombatStatus")==null,"Overhead status text must be replaced with icons");
            var stars=swordsman.GetChildren().OfType<Sprite2D>().Where(s=>s.Name.ToString().StartsWith("Star")).ToArray();
            Check(stars.Length==4&&stars.All(s=>s.Visible&&s.Position.Y>0&&s.Scale.X>.4f),"Stars must be enlarged and centered below the feet");
            await Capture("combat-status-icons");
            swordsman.ResetHealth();await Frames();
            Check(statusPanel.Visible&&statusPanel.StatusCount==1,"Status row remains until the last affected character clears it");
            enemy.ResetHealth();await Frames();
            Check(!statusPanel.Visible&&statusPanel.StatusCount==0,"Empty status list must disappear");
            Check(swordsman.GetChildren().OfType<Sprite2D>().Where(s=>s.Name.ToString().StartsWith("Status_")).All(s=>!s.Visible),"Removed buffs must clear overhead icons");
            if(DisplayServer.GetName()!="headless"){
                field.Hide();
                var layer=new CanvasLayer();AddChild(layer);
                var background=new ColorRect {Color=new Color("18232e"),Size=GetViewport().GetVisibleRect().Size};layer.AddChild(background);
                int index=0;
                foreach(var c in CharacterData.All.GroupBy(c=>c.ScenePath).Select(g=>g.First())){
                    var holder=new Node2D {Position=new Vector2(55+(index%10)*100,95+(index/10)*110),Scale=new Vector2(2,2)};layer.AddChild(holder);
                    var actor=GD.Load<PackedScene>(c.ScenePath).Instantiate<BattleUnit>();actor.CardKind=c.Kind;actor.ConfigureStars(1);holder.AddChild(actor);
                    actor.Sprite.Play(BattleAnimations.Idle);
                    var caption=new Label {Text=c.Id,Position=holder.Position+new Vector2(-45,12)};caption.AddThemeFontSizeOverride("font_size",9);layer.AddChild(caption);index++;
                }
                await Frames(12);await Capture("combat-size-audit");
                foreach(var holder in layer.GetChildren().OfType<Node2D>())foreach(var actor in holder.GetChildren().OfType<BattleUnit>())actor.Sprite.FlipH=true;
                await Frames(2);await Capture("combat-flip-alignment");
            }
            GD.Print($"PASS combat effects: {_checks} checks");GetTree().Quit(0);
        }catch(Exception e){GD.PushError(e.ToString());GetTree().Quit(1);}
    }
}
