using Godot;
using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Collections.Generic;

public partial class CombatSimulatorRunner : Node
{
    private int _checks;
    private void Check(bool ok,string message){_checks++;if(!ok)throw new Exception(message);}
    private async Task Frames(int count=2){for(int i=0;i<count;i++)await ToSignal(GetTree(),SceneTree.SignalName.ProcessFrame);}
    public override void _Ready()=>Callable.From(Run).CallDeferred();
    private async void Run()
    {
        string testPath=Path.Combine(Path.GetTempPath(),"riftbound-combat-visuals-"+Guid.NewGuid()+".json");
        CombatVisualSettings.StoragePath=testPath;CombatVisualSettings.Reload();
        try {
            Check(GameAccount.Session==null,"Simulator tests run without login");
            var welcome=GD.Load<PackedScene>("res://scenes/welcome.tscn").Instantiate<Welcome>();AddChild(welcome);await Frames();
            Check(welcome.FindChild("OfflineCombatSimulatorButton",true,false) is TextureButton,"Login screen offers offline simulator entry");welcome.QueueFree();
            Check(CombatSimulatorLab.ReturnScene=="res://scenes/welcome.tscn","Offline simulator returns to login screen");
            var lab=GD.Load<PackedScene>("res://scenes/combat_simulator_lab.tscn").Instantiate<CombatSimulatorLab>();AddChild(lab);await Frames();
            Check(lab.Characters.Length==CharacterData.All.Where(c=>c.Enabled).Select(c=>c.ScenePath).Distinct().Count()&&lab.Effects.Length>=192,"Full character/effect catalog");
            var characterPicker=lab.FindChildren("*","OptionButton",true,false).OfType<OptionButton>().First();
            Check(Enumerable.Range(0,characterPicker.ItemCount).Select(characterPicker.GetItemText).Distinct().Count()==lab.Characters.Length,"Character picker shows distinct character names");
            Check(Enumerable.Range(0,characterPicker.ItemCount).Select(characterPicker.GetItemText).SequenceEqual(lab.Characters.Select(c=>c.Name)),"Character picker labels match characters");
            var controls=lab.FindChildren("*","OptionButton",true,false).OfType<OptionButton>().ToArray();
            Check(controls.Length==7,"All simulator selectors available");
            foreach(var control in controls){
                int previous=control.Selected;
                control.EmitSignal(Control.SignalName.GuiInput,new InputEventKey {Keycode=Key.Down,Pressed=true});
                Check(control.Selected==Math.Min(previous+1,control.ItemCount-1),"Closed selector responds to arrow Down");
                control.EmitSignal(Control.SignalName.GuiInput,new InputEventKey {Keycode=Key.Up,Pressed=true});
                Check(control.Selected==previous,"Closed selector responds to arrow Up");
            }
            foreach(var number in lab.FindChildren("*","SpinBox",true,false).OfType<SpinBox>()){
                double previous=number.Value;
                number.GetLineEdit().EmitSignal(Control.SignalName.GuiInput,new InputEventKey {Keycode=Key.Up,Pressed=true});
                Check(Math.Abs(number.Value-previous-number.Step)<.001,"Number arrow Up adds one step");
                number.GetLineEdit().EmitSignal(Control.SignalName.GuiInput,new InputEventKey {Keycode=Key.Down,Pressed=true});
                Check(Math.Abs(number.Value-previous)<.001,"Number arrow Down restores value");
            }
            int uniqueAttacks=0;
            for(int i=0;i<lab.Characters.Length;i++){
                lab.SelectCharacters(i,0);uniqueAttacks+=lab.DisplayAnimations.Length;
                Check(lab.DisplayAnimations.Select(n=>UniqueCharacterAnimations.Signature(lab.Attacker.Sprite.SpriteFrames,n)).Distinct().Count()==lab.DisplayAnimations.Length,"Displayed attacks must have distinct frame sequences: "+lab.Characters[i].Name);
            }
            GD.Print($"Unique attack choices: {uniqueAttacks} (previously {lab.Characters.Length*3})");
            int Index(string slug)=>Array.FindIndex(lab.Characters,c=>c.ScenePath.EndsWith("/"+slug+".tscn"));
            lab.SelectCharacters(Index("skeleton_archer"),Index("black_knight_a"));
            Check(lab.DisplayAnimations.SequenceEqual(new[]{"attack01"}),"Single-sheet character must not offer copied attack02/03");
            var legacy=new CombatVisualChoice {Character="skeleton_archer",SourceAnimation="attack01",Animation="attack03",Effect=""};
            Check(CombatVisualSettings.Save(legacy,out _),"Legacy animation alias remains a valid saved configuration");
            lab.SelectSourceAnimation("attack01");
            Check(lab.Choice.Animation=="attack01"&&!lab.Dirty,"Saved duplicate aliases map to the unique choice without becoming unsaved edits");
            lab.SelectCharacters(Index("archer"),Index("black_knight_a"));Check(lab.DisplayAnimations.Length==2,"Genuine second attack stays selectable");
            lab.SelectCharacters(Index("swordsman"),Index("black_knight_a"));Check(lab.DisplayAnimations.Length==3,"Genuine three-hit-animation choices stay selectable");
            var choice=new CombatVisualChoice {Character="swordsman",TargetCharacter="black_knight_a",SourceAnimation="attack03",Animation="attack02",Effect="spell_poison_001_small_green",OffsetX=7,OffsetY=-12,Scale=1.75f,Anchor="body"};
            lab.SetChoice(choice);Check(!lab.Dirty&&File.Exists(testPath),"Edits automatically persist without Save");
            ((SpinBox)lab.FindChild("EffectOffsetX",true,false)).Value=11;
            CombatVisualSettings.Reload();Check(CombatVisualSettings.Find(lab.Attacker.CardKind,"attack03",lab.Target.CardKind)?.OffsetX==11,"Position controls save immediately without Save");
            lab.SetChoice(choice);
            Check(lab.Attacker.Sprite.SpriteFrames.HasAnimation("attack03"),"Attack variants loaded");
            var picker=lab.FindChild("TargetEffectPicker",true,false) as OptionButton;
            Check(picker!=null,"Effect picker available for keyboard navigation");
            int next=picker!.Selected==1?2:1;picker.EmitSignal(OptionButton.SignalName.ItemFocused,(long)next);
            Check(picker.Selected==next&&lab.Busy,"Arrow navigation automatically starts an attack preview");
            AnimatedSprite2D? previewEffect=null;ulong previewUntil=Time.GetTicksMsec()+2500;
            while(previewEffect==null&&Time.GetTicksMsec()<previewUntil){
                await Frames(1);previewEffect=lab.Target.GetParent().GetChildren().OfType<AnimatedSprite2D>().LastOrDefault(e=>e.HasMeta("effect_name"));
            }
            Check(previewEffect!=null&&previewEffect.IsPlaying(),"Automatic attack displays the selected effect at impact");
            Check(lab.Attacker.Sprite.Animation==choice.Animation&&lab.Target.Sprite.Animation==BattleAnimations.Hit,"A plays the selected attack and B reacts at impact");
            string focused=lab.Choice.Effect;ulong effectUntil=Time.GetTicksMsec()+1000;
            while(GodotObject.IsInstanceValid(previewEffect)&&previewEffect!.Frame==0&&Time.GetTicksMsec()<effectUntil)await Frames(1);
            Check(GodotObject.IsInstanceValid(previewEffect)&&previewEffect!.Frame>0,"Focused effect advances through animation frames");
            picker.EmitSignal(Control.SignalName.GuiInput,new InputEventKey {Keycode=Key.Down,Pressed=true});
            Check(picker.Selected==next+1&&lab.Choice.Effect!=focused,"Closed picker arrows browse the next effect");
            picker.EmitSignal(OptionButton.SignalName.ItemFocused,0L);
            Check(lab.Choice.Effect==""&&!lab.Target.GetParent().GetChildren().OfType<AnimatedSprite2D>().Any(e=>e.HasMeta("effect_name")),"None selection clears the preview immediately");
            lab.SetChoice(choice);
            await lab.PlayAttack();Check(!lab.Busy,"Attack preview completes");
            Check(lab.Attacker.Sprite.Animation==BattleAnimations.Idle&&lab.Target.Sprite.Animation==BattleAnimations.Idle,"Preview returns both units to idle");
            Check(lab.SaveChoice()&&File.Exists(testPath),"Save writes data");Check(!lab.Dirty,"Saved marker");
            CombatVisualSettings.Reload();var reloaded=CombatVisualSettings.Find(lab.Attacker.CardKind,"attack03",lab.Target.CardKind)!;
            Check(reloaded.Animation=="attack02"&&reloaded.Effect==choice.Effect&&reloaded.OffsetX==7&&reloaded.OffsetY==-12&&reloaded.Scale==1.75f&&reloaded.Anchor=="body","All settings survive reload");
            var plain=choice.Copy();plain.Scale=1;plain.OffsetX=plain.OffsetY=0;
            var normal=CharacterImpactEffects.PlayChoice(lab.Attacker,lab.Target,plain)!;
            var saved=CharacterImpactEffects.Play(lab.Attacker,lab.Target,"attack03","damage",5)!;
            Check(saved.GetMeta("effect_name").AsString()==choice.Effect,"Real combat picks the saved particle");
            Check(Mathf.IsEqualApprox(saved.Scale.X/normal.Scale.X,choice.Scale),"Saved size used in real effects");
            var anchor=lab.Target.Position+(CharacterVisual.GroundAnchor(lab.Target.Sprite)+CharacterVisual.HeadAnchor(lab.Target.Sprite))*.5f;
            var expected=anchor+(normal.Position-anchor)*choice.Scale+new Vector2(choice.OffsetX,choice.OffsetY);
            Check(saved.Position.IsEqualApprox(expected),"Offsets and body anchor match the preview");normal.QueueFree();saved.QueueFree();
            Check(CombatVisualSettings.AnimationFor(lab.Attacker.CardKind,"attack03",lab.Attacker.Sprite.SpriteFrames,lab.Target.CardKind)=="attack02","Saved animation used by battle playback");
            int sameCharacter=CharacterData.All.Last(c=>c.ScenePath.EndsWith("/swordsman.tscn")).Kind;
            Check(CombatVisualSettings.Find(sameCharacter,"attack03",lab.Target.CardKind)?.Effect==choice.Effect,"Every price tier shares the character visual");
            Check(CombatVisualSettings.Find(lab.Target.CardKind,"attack03")?.Effect!=choice.Effect,"Other characters unaffected");
            var separate=choice.Copy();separate.SourceAnimation="attack01";separate.Effect="";
            Check(CombatVisualSettings.Save(separate,out _),"Second attack saved independently");
            Check(CombatVisualSettings.Find(lab.Attacker.CardKind,"attack03",lab.Target.CardKind)?.Effect==choice.Effect,"Saving another slot preserves the first");
            Check(CharacterImpactEffects.Play(lab.Attacker,lab.Target,"attack01","damage",5)==null,"Explicitly disabled particle stays disabled");
            string before=File.ReadAllText(testPath);var invalid=choice.Copy();invalid.Scale=float.NaN;
            Check(!CombatVisualSettings.Save(invalid,out _)&&File.ReadAllText(testPath)==before,"Invalid scale cannot replace saved settings");invalid=choice.Copy();invalid.Effect="missing_effect";
            Check(!CombatVisualSettings.Save(invalid,out _)&&File.ReadAllText(testPath)==before,"Unknown effect cannot replace saved settings");
            string export=CombatVisualSettings.Serialize(CombatVisualSettings.Combined());Check(export.Contains("swordsman")&&export.Contains("offset_x"),"Export includes usable saved data");
            Check(CombatVisualSettings.RemapFrame(15,16,8)==7&&CombatVisualSettings.RemapFrame(0,16,8)==0,"Animation remapping retains endpoints");
            var other=choice.Copy();other.TargetCharacter="werebear";other.OffsetX=19;other.Scale=2.5f;
            Check(CombatVisualSettings.Save(other,out _),"Second target saved independently");
            lab.SelectCharacters(Index("swordsman"),Index("werebear"));lab.SelectSourceAnimation("attack03");
            Check(lab.Choice.OffsetX==19&&lab.Choice.Scale==2.5f,"Switching B loads its own settings");
            lab.SelectCharacters(Index("swordsman"),Index("black_knight_a"));lab.SelectSourceAnimation("attack03");
            Check(lab.Choice.OffsetX==7&&lab.Choice.Scale==1.75f,"Original B settings preserved");
            CombatVisualSettings.Reload();
            Check(CombatVisualSettings.Combined().Entries.Count(c=>c.Character=="swordsman"&&c.SourceAnimation=="attack03")==2,"Export and reload retain both targets");
            int loops=0;lab.Attacker.Sprite.AnimationChanged+=()=>{if(lab.Attacker.Sprite.Animation==choice.Animation)loops++;};
            ulong loopUntil=Time.GetTicksMsec()+8000;while(loops<2&&Time.GetTicksMsec()<loopUntil)await Frames(1);
            Check(loops>=2,"Preview automatically repeats attack cycles without pressing Play");
            // Drive actual battle playback with four authoritative damage snapshots.
            lab.Hide();var field=GD.Load<PackedScene>("res://scenes/main.tscn").Instantiate<BattleDisplay>();var online=field.GetNode("OnlineBattle");field.RemoveChild(online);online.Free();
            var battle=field.GetNode<BattleDemo>("BattleDemo");battle.NetworkEnabled=true;AddChild(field);await Frames();
            var shop=field.GetNode<CardShop>("UI/SafeArea/Content/CardShop");
            var state=System.Text.Json.JsonSerializer.Deserialize(Godot.FileAccess.GetFileAsString("res://tests/combat_fixture.json"),GameJsonContext.Default.OnlineState)!;
            state.Phase="preparation";shop.ApplyNetworkState(state,"effects-a");state.Phase="battle";shop.ApplyNetworkState(state,"effects-a");shop.SetReplayPlan(state.Battle!);await Frames();
            var a=shop.NetworkUnits.Values.First(u=>CombatVisualSettings.Slug(u.CardKind)=="swordsman");var b=shop.NetworkUnits.Values.First(u=>!u.IsAlly);var battleChoice=choice.Copy();battleChoice.TargetCharacter=CombatVisualSettings.Slug(b.CardKind);
            Check(CombatVisualSettings.Save(battleChoice,out _),"Fixture target configured independently");a.MaxHealth=b.MaxHealth=100;
            a.ApplyCombatState(new OnlineCombatChange {Hp=100,Slot=0});b.ApplyCombatState(new OnlineCombatChange {Hp=100,Slot=3});
            var received=new List<int>();var particles=new List<string>();string played="";
            b.HealthChanged+=(hp,max)=>{if(hp<100&&(received.Count==0||received[^1]!=hp))received.Add(hp);};
            a.Sprite.AnimationChanged+=()=>{if(a.Sprite.Animation.ToString().StartsWith("attack"))played=a.Sprite.Animation.ToString();};
            field.ChildEnteredTree+=node=>{if(node is AnimatedSprite2D e&&e.HasMeta("effect_name"))particles.Add(e.GetMeta("effect_name").AsString());};
            var rule=CharacterData.Get(a.CardKind).Combat!.Actions.First(r=>r.Animation=="attack03");
            var combat=new OnlineCombatEvent {Attacker=a.ServerId,Target=b.ServerId,Animation="attack03",Mode="ranged",TargetHp=80,
                Hits=rule.HitFrames.Select((frame,i)=>new OnlineCombatHit {Frame=frame,Source=a.ServerId,Target=b.ServerId,Damage=5,Kind="damage",Changes=new[]{new OnlineCombatChange {Id=b.ServerId,Hp=95-i*5,Slot=3}}}).ToArray()};
            await battle.PlayServerEvent(a,b,combat);
            Check(played=="attack02","Authoritative playback actually displays the selected animation");
            Check(received.SequenceEqual(new[]{95,90,85,80}),"Animation replacement preserves four authoritative HP impacts");
            Check(particles.Count==4&&particles.All(p=>p==choice.Effect),"Every real battle impact uses saved particles");field.QueueFree();lab.Show();await Frames();
            // Selecting another character mid-preview cancels safely.
            var preview=lab.PlayAttack();lab.SelectCharacters(Index("archer"),Index("werebear"));await preview;Check(lab.Attacker.CardKind==lab.Characters[Index("archer")].Kind,"Changing character cancels old playback and keeps the new actor");
            lab.SelectCharacters(Index("swordsman"),Index("black_knight_a"));lab.SelectSourceAnimation("attack03");await Frames();
            if(DisplayServer.GetName()!="headless"){
                await ToSignal(RenderingServer.Singleton,RenderingServer.SignalName.FramePostDraw);GetViewport().GetTexture().GetImage().SavePng("res://tests/combat-simulator.png");
            }
            Check(CombatVisualSettings.Reset(lab.Attacker.CardKind,"attack03",out _,lab.Target.CardKind),"Reset removes one override");
            Check(CombatVisualSettings.Find(lab.Attacker.CardKind,"attack03",lab.Target.CardKind)==null&&CombatVisualSettings.Find(lab.Attacker.CardKind,"attack01",lab.Target.CardKind)!=null,"Reset preserves other saved slots");
            File.WriteAllText(testPath,"{ invalid json");CombatVisualSettings.Reload();Check(CombatVisualSettings.Find(lab.Attacker.CardKind,"attack03",lab.Target.CardKind)==null,"Corrupt settings safely fall back");
            GD.Print($"PASS combat simulator: {_checks} checks");GetTree().Quit();
        }catch(Exception e){GD.PushError(e.ToString());GetTree().Quit(1);}
        finally {if(File.Exists(testPath))File.Delete(testPath);CombatVisualSettings.StoragePath=CombatVisualSettings.SavedPath;CombatVisualSettings.Reload();}
    }
}
