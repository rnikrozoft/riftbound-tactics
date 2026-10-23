using Godot;
using System;
using System.Linq;
using System.Reflection;
public partial class FirstPurchaseDropRunner:Node {
public override void _Ready()=>Callable.From(Run).CallDeferred();
private async System.Threading.Tasks.Task Wait(Func<bool> test,string message){ulong end=Time.GetTicksMsec()+40000;while(!test()){if(Time.GetTicksMsec()>end)throw new TimeoutException(message);await ToSignal(GetTree(),SceneTree.SignalName.ProcessFrame);}}
private async void Run(){try {
var field=GD.Load<PackedScene>("res://scenes/main.tscn").Instantiate<BattleDisplay>();var net=field.GetNode("OnlineBattle");field.RemoveChild(net);net.Free();var battle=field.GetNode<BattleDemo>("BattleDemo");battle.AutoStart=false;battle.NetworkEnabled=false;AddChild(field);
await ToSignal(GetTree(),SceneTree.SignalName.ProcessFrame);
var shop=field.GetNode<CardShop>("UI/SafeArea/Content/CardShop");typeof(CardShop).GetProperty("Drafting")!.SetValue(shop,true);typeof(CardShop).GetProperty("Coins")!.SetValue(shop,4);shop.Hand.Clear();shop.Offers.Clear();int kind=CharacterData.All.First(c=>c.Enabled).Kind;shop.Offers.Add(new CardData(901,CardCatalog.Name(kind),CardCatalog.Art(kind),2));((int[])typeof(CardShop).GetField("_remainingCopies",BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(shop)!)[kind]=8;shop.ZoneA.Show();shop.ZoneB.Show();shop.RefreshNetworkControls();
await ToSignal(GetTree(),SceneTree.SignalName.ProcessFrame);
var offer=shop.ShopRow.GetChildren().OfType<ShopCard>().First(c=>c.Visible);var dest=shop.ZoneB.GetGlobalRect().GetCenter();
GD.Print("DROP TARGET "+dest+" ZONE "+shop.ZoneB.GetGlobalRect());
offer.ForceDrag(new CardDrag {Shop=shop,Token=offer.Token,FromShop=true},new Control());
Input.ParseInputEvent(new InputEventMouseMotion {Position=dest,GlobalPosition=dest});
await ToSignal(GetTree(),SceneTree.SignalName.ProcessFrame);
GD.Print("HOVER "+GetViewport().GuiGetHoveredControl()?.GetPath());
Input.ParseInputEvent(new InputEventMouseButton {ButtonIndex=MouseButton.Left,Pressed=false,Position=dest,GlobalPosition=dest});
await ToSignal(GetTree(),SceneTree.SignalName.ProcessFrame);
if(shop.Hand.Count!=1||shop.Coins!=2)throw new Exception("First drop failed: hand="+shop.Hand.Count+" coins="+shop.Coins);
GD.Print("FIRST PURCHASE DROP PASS");
field.QueueFree();await ToSignal(GetTree(),SceneTree.SignalName.ProcessFrame);
var onlineField=GD.Load<PackedScene>("res://scenes/main.tscn").Instantiate<BattleDisplay>();AddChild(onlineField);
var online=onlineField.GetNode<OnlineBattle>("OnlineBattle");var onlineShop=onlineField.GetNode<CardShop>("UI/SafeArea/Content/CardShop");
await Wait(()=>online.Connected,"connect");online.Connection!.ErrorReceived+=e=>GD.Print("PURCHASE ERROR: "+e.Message);
await CharacterData.Refresh(online.Connection);await online.Connection.FindMatch(DeckDefinition.Starter());await Wait(()=>onlineShop.Drafting,"preparation");
await ToSignal(GetTree(),SceneTree.SignalName.ProcessFrame);
var first=onlineShop.ShopRow.GetChildren().OfType<ShopCard>().First(c=>c.Visible);int before=onlineShop.Coins;int token=first.Token;int price=onlineShop.Offers.First(c=>c.Token==token).Price;
var target=onlineShop.ZoneB.GetGlobalRect().GetCenter();first.ForceDrag(new CardDrag {Shop=onlineShop,Token=token,FromShop=true},new Control());
Input.ParseInputEvent(new InputEventMouseMotion {Position=target,GlobalPosition=target});await ToSignal(GetTree(),SceneTree.SignalName.ProcessFrame);
Input.ParseInputEvent(new InputEventMouseButton {ButtonIndex=MouseButton.Left,Pressed=false,Position=target,GlobalPosition=target});
await Wait(()=>onlineShop.Hand.Count==1&&!onlineShop.NetworkPending,"first online purchase");
if(onlineShop.Coins!=before-price||onlineShop.Offers.Any(c=>c.Token==token)||!onlineShop.HandRow.GetChildren().OfType<ShopCard>().Any(c=>c.IsVisibleInTree()))throw new Exception("First online purchase not displayed/deducted");
GD.Print("FIRST ONLINE PURCHASE DROP PASS");await online.Connection.Close();GetTree().Quit();
}catch(Exception e){GD.PushError(e.ToString());GetTree().Quit(1);}}
}
