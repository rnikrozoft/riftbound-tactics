using Godot;
using System;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
public partial class UpgradeCardFrameRunner:Node
{
 public override async void _Ready(){try{
  var field=GD.Load<PackedScene>("res://scenes/main.tscn").Instantiate<BattleDisplay>();field.GetNode("OnlineBattle").Free();var battle=field.GetNode<BattleDemo>("BattleDemo");battle.NetworkEnabled=true;battle.AutoStart=true;AddChild(field);var shop=field.GetNode<CardShop>("UI/SafeArea/Content/CardShop");
  var state=JsonSerializer.Deserialize(FileAccess.GetFileAsString("res://tests/combat_fixture.json"),GameJsonContext.Default.OnlineState)!;state.Phase="preparation";state.Players[0]!.UserId="self";shop.ApplyNetworkState(state,"self");
  for(int i=0;i<8;i++)await ToSignal(GetTree(),SceneTree.SignalName.ProcessFrame);
  var views=(System.Collections.Generic.List<ShopCard>)typeof(CardShop).GetField("_shopViews",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance)!.GetValue(shop)!;var card=views.First();UpgradeHint.Set(card,true);var border=card.GetNode<UpgradeCardFrame>("UpgradeCardFrame");
  await ToSignal(GetTree().CreateTimer(.15),SceneTreeTimer.SignalName.Timeout);var texture=border.Texture;await ToSignal(GetTree().CreateTimer(.2),SceneTreeTimer.SignalName.Timeout);if(texture==border.Texture||!card.GetNode<AnimatedSprite2D>("UpgradeHint").Visible)throw new Exception("Border animation or original sparkle missing");
  UpgradeHint.Set(card,false);if(border.Visible||card.GetNode<AnimatedSprite2D>("UpgradeHint").Visible)throw new Exception("Eligibility removal failed");UpgradeHint.Set(card,true);if(card.GetChildren().Count(n=>n.Name=="UpgradeCardFrame")!=1)throw new Exception("Duplicate border");
  if(DisplayServer.GetName()!="headless"){await field.WaitForPhaseTransition();await ToSignal(RenderingServer.Singleton,RenderingServer.SignalName.FramePostDraw);GetViewport().GetTexture().GetImage().SavePng("/tmp/upgrade-card-frame.png");}
  GD.Print("UPGRADE FRAME PASS: animated source asset, original sparkle retained, eligibility toggle and no duplicate effects");GetTree().Quit();
 }catch(Exception e){GD.PushError(e.ToString());GetTree().Quit(1);}}
}
