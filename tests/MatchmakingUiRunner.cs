using Godot;
using System;
using System.Linq;
using System.Threading.Tasks;

public partial class MatchmakingUiRunner : Node
{
 public override void _Ready()=>Callable.From(Run).CallDeferred();
 private async Task Frames(int n=3){for(int i=0;i<n;i++)await ToSignal(GetTree(),SceneTree.SignalName.ProcessFrame);}
 private async Task Wait(Func<bool> f,string message){ulong until=Time.GetTicksMsec()+35000;while(!f()){if(Time.GetTicksMsec()>until)throw new Exception(message);await Frames(1);}}
 private async void Run()
 {
  try
  {
   var lobby=GD.Load<PackedScene>("res://scenes/lobby.tscn").Instantiate<Lobby>();AddChild(lobby);await Frames(10);
   GetViewport().GetTexture().GetImage().SavePng("res://tests/deck-matchmaking-lobby.png");
   lobby.QueueFree();await Frames();
   BattleLaunch.Deck=DeckDefinition.Starter();BattleLaunch.Pending=true;BattleLaunch.Matchmaking=true;
   var main=GD.Load<PackedScene>("res://scenes/main.tscn").Instantiate<Node2D>();AddChild(main);
   var online=main.GetNode<OnlineBattle>("OnlineBattle");
   await Wait(()=>online.State?.Phase=="preparation","matchmaking UI preparation");await Frames(12);
   if(online.State!.Roster.Length!=6)throw new Exception("missing six-seat roster");
   var own=online.State.Players.First(p=>p?.UserId==online.UserId)!;
   var left=main.GetNode<PlayerProfile>(own.Team=="A"?"UI/SafeArea/Content/ProfileA":"UI/SafeArea/Content/ProfileB");
   if(left.AnchorLeft!=0)throw new Exception("own profile must be left");
   var shop=main.GetNode<CardShop>("UI/SafeArea/Content/CardShop");
   int offer=own.Offers[0].Token;online.SendAction("buy",offer);
   await Wait(()=>online.State!.Players.First(p=>p?.UserId==online.UserId)!.Hand.Length==1,"UI purchase");
   int token=online.State!.Players.First(p=>p?.UserId==online.UserId)!.Hand[0].Token;
   online.SendAction("deploy",token,0);
   await Wait(()=>online.State!.Players.First(p=>p?.UserId==online.UserId)!.Units.Length==1,"UI deployment");await Frames(5);
   GetViewport().GetTexture().GetImage().SavePng("res://tests/deck-matchmaking-battle.png");
   online.SendAction("ready");await Wait(()=>online.State?.Phase=="battle","network battle replay");await Frames(10);
   var plan=online.State!.Battle!;if(plan.Events.Length==0)throw new Exception("expected a real combat replay");
   var first=plan.Events[0];
   await Wait(()=>shop.NetworkUnits.TryGetValue(first.Target,out var target)&&target.Health==first.TargetHp,"authoritative replay damage appears in UI");
   main.QueueFree();await Frames(10);GD.Print("MATCHMAKING UI PASS");GetTree().Quit(0);
  }
  catch(Exception e){GD.PushError(e.ToString());GetTree().Quit(1);}
 }
}
