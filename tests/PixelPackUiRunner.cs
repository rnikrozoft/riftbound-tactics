using Godot;
using System;
using System.Threading.Tasks;
public partial class PixelPackUiRunner:Node
{
 public override async void _Ready(){try{
  var layer=new CanvasLayer{Layer=50};AddChild(layer);var background=new ColorRect{Color=new Color("18212b"),Size=new(1000,500),Position=new(80,80)};layer.AddChild(background);
  var title=new Label{Text="PIXEL UI PACK 4  /  Loading · Health · Placement",Position=new(110,105)};layer.AddChild(title);
  var spinner=new PixelLoadingIndicator{Position=new(130,155),Size=new(80,80)};layer.AddChild(spinner);
  for(int i=0;i<6;i++){var bar=new PixelHealthBar{Position=new(260,160+i*45),Size=new(288,36),MaxValue=100,Value=100-i*20};layer.AddChild(bar);}
  for(int i=0;i<4;i++){var rect=new TextureRect{Texture=PixelPackUi.Frame("07",new(i*32,176,32,32)),Position=new(630+i*90,175),Size=new(80,80),ExpandMode=TextureRect.ExpandModeEnum.IgnoreSize,TextureFilter=CanvasItem.TextureFilterEnum.Nearest};layer.AddChild(rect);}
  for(int i=0;i<6;i++)await ToSignal(GetTree(),SceneTree.SignalName.ProcessFrame);var first=spinner.Texture;await ToSignal(GetTree().CreateTimer(.2),SceneTreeTimer.SignalName.Timeout);if(first==spinner.Texture)throw new Exception("Loader not animated");
  if(DisplayServer.GetName()!="headless"){await ToSignal(RenderingServer.Singleton,RenderingServer.SignalName.FramePostDraw);GetViewport().GetTexture().GetImage().SavePng("/tmp/pixel-pack-ui-preview.png");}layer.QueueFree();
  var field=GD.Load<PackedScene>("res://scenes/main.tscn").Instantiate<BattleDisplay>();field.GetNode("OnlineBattle").Free();var battle=field.GetNode<BattleDemo>("BattleDemo");battle.NetworkEnabled=true;battle.AutoStart=true;AddChild(field);var shop=field.GetNode<CardShop>("UI/SafeArea/Content/CardShop");
  var state=System.Text.Json.JsonSerializer.Deserialize(FileAccess.GetFileAsString("res://tests/combat_fixture.json"),GameJsonContext.Default.OnlineState)!;state.Phase="preparation";state.Players[0]!.UserId="self";shop.ApplyNetworkState(state,"self");
  for(int i=0;i<10;i++)await ToSignal(GetTree(),SceneTree.SignalName.ProcessFrame);
  foreach(var unit in shop.NetworkUnits.Values)if(unit.GetNode<ProgressBar>("HealthBar") is not PixelHealthBar)throw new Exception("Hero health not using asset");
  if(shop.FieldZone==null)throw new Exception("Missing placement UI");
  typeof(FieldDrop).GetField("_drag",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance)!.SetValue(shop.FieldZone,new CardDrag{Shop=shop,Unit=System.Linq.Enumerable.First(shop.NetworkUnits.Values,u=>u.IsAlly)});shop.FieldZone.SetProcess(true);shop.FieldZone.QueueRedraw();
  if(DisplayServer.GetName()!="headless"){await field.WaitForPhaseTransition();await ToSignal(RenderingServer.Singleton,RenderingServer.SignalName.FramePostDraw);GetViewport().GetTexture().GetImage().SavePng("/tmp/pixel-pack-ui-battle.png");}
  GD.Print("PIXEL PACK UI PASS: source textures, animated loading, health levels, placement frames, battlefield health integration");GetTree().Quit();
 }catch(Exception e){GD.PushError(e.ToString());GetTree().Quit(1);}}
}
