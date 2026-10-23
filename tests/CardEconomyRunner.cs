using Godot;
using System;
using System.Linq;
using System.Reflection;
public partial class CardEconomyRunner:Node {
 public override void _Ready()=>Callable.From(Run).CallDeferred();
 private async void Run(){try {
  var field=GD.Load<PackedScene>("res://scenes/main.tscn").Instantiate<BattleDisplay>();var net=field.GetNode("OnlineBattle");field.RemoveChild(net);net.Free();var battle=field.GetNode<BattleDemo>("BattleDemo");battle.AutoStart=false;battle.NetworkEnabled=false;AddChild(field);
  await ToSignal(GetTree(),SceneTree.SignalName.ProcessFrame);
  var shop=field.GetNode<CardShop>("UI/SafeArea/Content/CardShop");
  typeof(CardShop).GetProperty("Drafting")!.SetValue(shop,true);typeof(CardShop).GetProperty("Coins")!.SetValue(shop,100);
  int kind=CharacterData.All.First(c=>c.Enabled).Kind;var name=CardCatalog.Name(kind);
  var pool=(int[])typeof(CardShop).GetField("_remainingCopies",BindingFlags.NonPublic|BindingFlags.Instance)!.GetValue(shop)!;pool[kind]=8;
  shop.Hand.Clear();
  for(int i=1;i<=8;i++) {
   shop.Offers.Clear();shop.Offers.Add(new CardData(800+i,name,CardCatalog.Art(kind),2));
   if(!shop.TakeCard(800+i))throw new Exception("Purchase failed");
   if(shop.Hand.Sum(c=>1<<(c.Stars-1))!=i)throw new Exception("Copies lost during merging");
   if(i==3 && (shop.Hand.Count!=2 || !shop.Hand.Any(c=>c.Stars==1)||!shop.Hand.Any(c=>c.Stars==2)))throw new Exception("Third copy upgraded prematurely");
   if((i==4||i==8)&&(shop.Hand.Count!=1||shop.Hand[0].Stars!=(i==4?3:4)))throw new Exception("Power-of-two merge failed");
  }
  await ToSignal(GetTree(),SceneTree.SignalName.ProcessFrame);
  var card=shop.HandRow.GetChildren().OfType<ShopCard>().First(c=>c.Visible);
  if(card.GetNode<Label>("CardPrice").Visible || !card.GetNode<Control>("HandStars").Visible)throw new Exception("Hand still shows price");
  if(card.GetNode("HandStars").GetChild(1).GetChildCount()!=8)throw new Exception("Wrong hand star count");
  var panel=shop.ZoneB.GetNode<Control>("Surface");
  var top=shop.HandRow.Position.Y-panel.Position.Y;var bottom=panel.Position.Y+panel.Size.Y-(shop.HandRow.Position.Y+card.Size.Y);
  if(Math.Abs(top-bottom)>1)throw new Exception("Hand card margins are unequal");
  int before=shop.Coins;if(!shop.SellCard(shop.Hand[0].Token)||shop.Coins!=before+1)throw new Exception("Sale is not one coin");
  field.Free();GD.Print("CARD ECONOMY PASS: 2/4/8 copy merges, third copy remains one star, four-star hand badge, one-coin resale");GetTree().Quit();
 }catch(Exception e){GD.PushError(e.ToString());GetTree().Quit(1);}}
}
