using Godot;
using System;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
public partial class SpecialCardsRunner:Node
{
 private async Task Frames(int n=5){for(int i=0;i<n;i++)await ToSignal(GetTree(),SceneTree.SignalName.ProcessFrame);}
 public override async void _Ready(){try{
  var kinds=CharacterData.SupportKinds.ToArray();if(kinds.Length!=58)throw new Exception("Expected ten support cards");
  foreach(int k in kinds.Where(k=>k!=CharacterData.SupportKind&&!SkillCardVisual.IsSkill(k))){
   var def=CharacterData.Get(k);var node=GD.Load<PackedScene>(def.ScenePath).Instantiate<BattleUnit>();var front=node.GetNode<AnimatedSprite2D>("AnimatedSprite2D").SpriteFrames;var back=GD.Load<SpriteFrames>(def.EnemyAnimationPath);
   if(front.GetFrameCount("idle")!=4||back.GetFrameCount("idle")!=4||((AtlasTexture)front.GetFrameTexture("idle",0)).Region.Position.Y==((AtlasTexture)back.GetFrameTexture("idle",0)).Region.Position.Y)throw new Exception("Direction/frame mismatch "+def.Name);node.Free();
   var plan=new LocalCombat(19).Simulate(new[]{new OnlineCombatUnit{Id="A:hero",Team="A",Kind=460,Stars=1,Slot=3},new OnlineCombatUnit{Id="B:hero",Team="B",Kind=460,Stars=1,Slot=3},new OnlineCombatUnit{Id="A:support",Team="A",Kind=k,Stars=1,Slot=6}});
   if(plan.Events.Any(e=>e.Attacker=="A:support"||e.Target=="A:support"))throw new Exception("Support targeted");
  }
  var field=GD.Load<PackedScene>("res://scenes/main.tscn").Instantiate<BattleDisplay>();field.GetNode("OnlineBattle").Free();var battle=field.GetNode<BattleDemo>("BattleDemo");battle.NetworkEnabled=true;battle.AutoStart=true;AddChild(field);var shop=field.GetNode<CardShop>("UI/SafeArea/Content/CardShop");
  for(int batch=0;batch<2;batch++){
   var state=JsonSerializer.Deserialize(FileAccess.GetFileAsString("res://tests/combat_fixture.json"),GameJsonContext.Default.OnlineState)!;state.Phase="preparation";state.Players[0]!.UserId="self";
   for(int side=0;side<2;side++)state.Players[side]!.Units=new[]{state.Players[side]!.Units.First()}.Concat(kinds.Skip(batch*5).Take(5).Select((k,i)=>new OnlineUnit{Kind=k,Token=900+batch*1000+side*100+i,Stars=1,Slot=6+i})).ToArray();
   shop.ApplyNetworkState(state,"self");await Frames(10);
   foreach(var unit in shop.NetworkUnits.Values.Where(u=>CharacterData.IsSupport(u.CardKind)))if(!unit.Sprite.IsPlaying()||!unit.Visible||unit.ZIndex<5)throw new Exception("Support visibility/animation");
   var animated=shop.NetworkUnits.Values.First(u=>CharacterData.IsSupport(u.CardKind));int firstFrame=animated.Sprite.Frame;await ToSignal(GetTree().CreateTimer(.25),SceneTreeTimer.SignalName.Timeout);if(animated.Sprite.Frame==firstFrame)throw new Exception("Idle frame did not advance");
   foreach(var u in shop.NetworkUnits.Values.Where(u=>CharacterData.IsSupport(u.CardKind))){if(CharacterData.SupportEffectFor(u.CardKind) is "ward" or "seal"){var size=CharacterVisual.AnimationSize(u.Sprite.SpriteFrames,BattleAnimations.Idle);float baseline=Mathf.Min(CharacterVisual.FieldHeight/size.Y,CharacterVisual.FieldWidth/size.X);if(Mathf.Abs(u.Sprite.Scale.X/baseline-1.65f)>.01f)throw new Exception("Tall support size mismatch");}u.ConfigureStars(4,true);if(u.Stars!=1||u.GetChildren().Any(n=>n is Sprite2D star&&star.Name.ToString().StartsWith("Star")&&star.Visible))throw new Exception("Support stars reappeared");}
   if(DisplayServer.GetName()!="headless"){await field.WaitForPhaseTransition();await ToSignal(RenderingServer.Singleton,RenderingServer.SignalName.FramePostDraw);GetViewport().GetTexture().GetImage().SavePng($"/tmp/riftbound-special-cards-{batch}.png");}
  }
  GD.Print("SPECIAL CARDS PASS: ten support cards, nine directional four-frame animations, offline targeting and battlefield rendering");GetTree().Quit();
 }catch(Exception e){GD.PushError(e.ToString());GetTree().Quit(1);}}
}
