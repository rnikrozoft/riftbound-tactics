using Godot;
using System;
using System.Linq;
using System.Text.Json;
public partial class PaladinSkillCardsRunner:Node
{
 public override async void _Ready(){try{
  PaladinSkillRulesChecks.Run();
  if(CharacterData.All.Any(c=>CardCatalog.Art(c.Kind).Atlas.ResourcePath.Contains("ClassicLightCard")))throw new Exception("White frame still assigned");
  var kinds=CharacterData.SupportKinds.Where(SkillCardVisual.IsSkill).ToArray();if(kinds.Length!=48)throw new Exception("Missing skill icons");foreach(int k in kinds){var c=CharacterData.Get(k);if(!ResourceLoader.Exists(c.ScenePath)||!ResourceLoader.Exists(c.PortraitPath)||!CharacterData.IsTrap(k))throw new Exception("Skill card catalog");}
  var field=GD.Load<PackedScene>("res://scenes/main.tscn").Instantiate<BattleDisplay>();field.GetNode("OnlineBattle").Free();var battle=field.GetNode<BattleDemo>("BattleDemo");battle.NetworkEnabled=true;battle.AutoStart=true;AddChild(field);var shop=field.GetNode<CardShop>("UI/SafeArea/Content/CardShop");var state=JsonSerializer.Deserialize(FileAccess.GetFileAsString("res://tests/combat_fixture.json"),GameJsonContext.Default.OnlineState)!;state.Phase="preparation";state.Players[0]!.UserId="self";
  for(int side=0;side<2;side++)state.Players[side]!.Units=new[]{state.Players[side]!.Units.First()}.Concat(new[]{0,11,23,35,47}.Select((n,i)=>new OnlineUnit{Kind=kinds[n],Token=900+side*100+i,Slot=6+i,Stars=1})).ToArray();shop.ApplyNetworkState(state,"self");for(int i=0;i<10;i++)await ToSignal(GetTree(),SceneTree.SignalName.ProcessFrame);
  var units=shop.NetworkUnits.Values.Where(u=>SkillCardVisual.IsSkill(u.CardKind)).ToArray();foreach(var u in units)if(u.Sprite.Scale!=Vector2.One||u.Sprite.FlipH||u.Sprite.SpriteFrames.GetFrameTexture("idle",0).GetSize()!=new Vector2(23,35)||u.Sprite.Material is not ShaderMaterial)throw new Exception("Field must render complete readable card");
  if(!shop.Field.YSortEnabled)throw new Exception("Arena must sort units by depth");
  foreach(var special in units)foreach(var hero in shop.NetworkUnits.Values.Where(u=>!CharacterData.IsSupport(u.CardKind)))if(special.ZIndex!=hero.ZIndex)throw new Exception("Special cards and characters must share a layer for depth sorting on both teams");
  var unit=units.First();float y=unit.Sprite.Position.Y;await ToSignal(GetTree().CreateTimer(.3),SceneTreeTimer.SignalName.Timeout);if(Mathf.Abs(unit.Sprite.Position.Y-y)<.05f)throw new Exception("No floating animation");
  if(DisplayServer.GetName()!="headless"){await field.WaitForPhaseTransition();await ToSignal(RenderingServer.Singleton,RenderingServer.SignalName.FramePostDraw);GetViewport().GetTexture().GetImage().SavePng("/tmp/paladin-skill-cards.png");}
  unit.ApplyCombatState(new OnlineCombatChange{Id=unit.ServerId,Hp=0,Slot=unit.GridSlot});if(!unit.Visible||!((ShaderMaterial)unit.Sprite.Material).GetShaderParameter("ripping").AsBool())throw new Exception("Rip did not start");await ToSignal(GetTree().CreateTimer(.6),SceneTreeTimer.SignalName.Timeout);
  if(DisplayServer.GetName()!="headless"){await ToSignal(RenderingServer.Singleton,RenderingServer.SignalName.FramePostDraw);GetViewport().GetTexture().GetImage().SavePng("/tmp/paladin-skill-card-rip.png");}
  await ToSignal(GetTree().CreateTimer(.8),SceneTreeTimer.SignalName.Timeout);if(unit.Visible)throw new Exception("Used card remained visible");
  GD.Print("PALADIN SKILL CARDS PASS: 48 icons, full card on both teams, float, original rip masks and hide after consumption");GetTree().Quit();
 }catch(Exception e){GD.PushError(e.ToString());GetTree().Quit(1);}}
}
