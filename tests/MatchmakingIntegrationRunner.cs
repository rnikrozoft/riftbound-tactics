using Godot;
using System;
using System.Linq;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading.Tasks;

public partial class MatchmakingIntegrationRunner : Node
{
 private int _checks;
 private readonly List<NakamaConnection> _clients=new();
 private readonly ConcurrentDictionary<string,OnlineState> _states=new();
 private void Check(bool ok,string message){_checks++;if(!ok)throw new Exception(message);}
 private async Task Wait(Func<bool> condition,string message,int timeout=35000)
 {
  ulong end=Time.GetTicksMsec()+(ulong)timeout;
  while(!condition()){if(Time.GetTicksMsec()>end)throw new TimeoutException(message);await ToSignal(GetTree(),SceneTree.SignalName.ProcessFrame);}
 }
 private OnlineState State(NakamaConnection c)=>_states[c.DeviceId];
 private async Task<NakamaConnection> Client()
 {
  var c=new NakamaConnection();_clients.Add(c);c.StateReceived+=s=>_states[c.DeviceId]=s;
  c.ErrorReceived+=e=>GD.Print("SERVER: "+e.Message);await c.Connect();return c;
 }
 public override void _Ready()=>Callable.From(Run).CallDeferred();
 private async void Run()
 {
  try
  {
   // Connect sequentially so the collection itself remains on the scene thread.
   for(int i=0;i<6;i++)await Client();
   var players=_clients.ToArray();
   var rooms=await Task.WhenAll(players.Select(c=>c.FindMatch(DeckDefinition.Starter())));
   Check(rooms.Select(r=>r.MatchId).Distinct().Count()==1,"six humans must share one authoritative match");
   await Wait(()=>players.All(c=>_states.TryGetValue(c.DeviceId,out var s)&&s.Phase=="preparation"),"six preparations");
   var prior=new Dictionary<string,string>();
   foreach(var c in players)
   {
    var s=State(c);Check(s.Roster.Length==6&&!s.Roster.Any(p=>p.Bot),"six humans, no bots");
    Check(s.Roster.All(p=>p.Hp==30&&p.Rating==1000),"server rank and starting health");
    var own=s.Players.First(p=>p?.UserId==c.Session.UserId)!;
    var other=s.Players.First(p=>p?.UserId!=c.Session.UserId)!;
    Check(own.Coins==4&&own.Offers.Length==3,"deck economy initialized once");
    Check(other.Offers.Length==0&&other.Hand.Length==0&&other.Units.Length==0,"private preparation stays hidden");
    prior[c.Session.UserId]=other.UserId;
   }
   var spy=await Client();bool rejected=false;try{await spy.Socket.JoinMatchAsync(rooms[0].MatchId);}catch{rejected=true;}
   Check(rejected,"unreserved user cannot join match");
   foreach(var c in players)await c.Send(new(){Type="ready",Round=1,Sequence=1});
   await Wait(()=>players.All(c=>State(c).Round==2&&State(c).Phase=="preparation"),"automatic next round");
   foreach(var c in players)
   {
    var s=State(c);var own=s.Players.First(p=>p?.UserId==c.Session.UserId)!;var other=s.Players.First(p=>p?.UserId!=c.Session.UserId)!;
    Check(own.Coins==10,"unused coins carry forward plus round income");
    Check(other.UserId!=prior[c.Session.UserId],"avoid consecutive opponent when possible");
    Check(s.Roster.All(p=>p.Hp==30&&p.Place==0),"draw has no damage or elimination");
   }
   var buyer=players[0];var offer=State(buyer).Players.First(p=>p?.UserId==buyer.Session.UserId)!.Offers[0];
   await buyer.Send(new(){Type="buy",Token=offer.Token,Round=2,Sequence=2});
   await Wait(()=>State(buyer).AckSequence>=2,"purchase acknowledgement");
   var card=State(buyer).Players.First(p=>p?.UserId==buyer.Session.UserId)!.Hand.Single();
   await buyer.Send(new(){Type="deploy",Token=card.Token,Slot=0,Round=2,Sequence=3});
   await Wait(()=>State(buyer).AckSequence>=3,"deployment acknowledgement");
   var buyerOpponent=players.Single(c=>c.Session.UserId==State(buyer).Players.First(p=>p?.UserId!=buyer.Session.UserId)!.UserId);
   Check(State(buyerOpponent).Players.First(p=>p?.UserId==buyer.Session.UserId)!.Units.Length==0,"deployed field stays private before battle");
   foreach(var c in players)await c.Send(new(){Type="ready",Round=2,Sequence=c==buyer?4:2});
   await Wait(()=>players.All(c=>State(c).Round==3&&State(c).Phase=="preparation"),"damaging round advances globally");
   Check(players.Select(c=>State(c).DeadlineMs).Distinct().Count()==1,"all six share next preparation deadline");
   Check(State(buyer).DeadlineMs-State(buyer).ServerMs<=60000&&State(buyer).DeadlineMs-State(buyer).ServerMs>58000,"next preparation grants full sixty seconds");
   Check(State(buyer).Roster.Single(p=>p.UserId==buyerOpponent.Session.UserId).Hp==27,"winning shop level plus survivor star damages only paired opponent");
   Check(State(buyer).Players.First(p=>p?.UserId==buyer.Session.UserId)!.Coins==17,"purchase deducted then income carried into next round");
   foreach(var c in players)await c.Close();
   var partialA=await Client();var partialB=await Client();
   var partial=await Task.WhenAll(partialA.FindMatch(DeckDefinition.Starter()),partialB.FindMatch(DeckDefinition.Starter()));
   Check(partial[0].MatchId==partial[1].MatchId,"partial human group shares a match");
   await Wait(()=>_states.TryGetValue(partialA.DeviceId,out var partialState)&&partialState.Phase=="preparation","partial group preparation");
   Check(State(partialA).Roster.Count(p=>p.Bot)==4,"two-human group fills four bot seats");
   await partialA.Close();await partialB.Close();
   var solo=await Client();var room=await solo.FindMatch(DeckDefinition.Starter());
   Check(room.MatchId!="","solo search gets a match after timeout");
   await Wait(()=>_states.TryGetValue(solo.DeviceId,out var s)&&s.Phase=="preparation","solo preparation");
   Check(State(solo).Roster.Length==6&&State(solo).Roster.Count(p=>p.Bot)==5,"solo fallback adds exactly five bots");
   await Wait(()=>State(solo).Players.First(p=>p?.UserId!=solo.Session.UserId)?.Ready==true,"bots finish bounded preparation");
   Check(State(solo).Players.First(p=>p?.UserId==solo.Session.UserId)?.Ready==false,"bots cannot ready the human");
   // Real client projection/replay and profile checks with the solo bot match.
   var main=GD.Load<PackedScene>("res://scenes/main.tscn").Instantiate<Node2D>();
   main.GetNode<BattleDemo>("BattleDemo").AutoStart=false;AddChild(main);
   await ToSignal(GetTree(),SceneTree.SignalName.ProcessFrame);
   var shop=main.GetNode<CardShop>("UI/SafeArea/Content/CardShop");shop.ApplyNetworkState(State(solo),solo.Session.UserId);
   Check(shop.LocalTeam==State(solo).Players.First(p=>p?.UserId==solo.Session.UserId)!.Team,"self perspective follows pairing");
   main.QueueFree();
   await solo.Close();
   var cancelled=await Client();var search=cancelled.FindMatch(DeckDefinition.Starter());await Task.Delay(1000);await cancelled.Close();
   try{await search;throw new Exception("cancelled search unexpectedly joined");}catch(OperationCanceledException){}
   Check(cancelled.MatchId=="","closing search cancels before joining");
   GD.Print($"MATCHMAKING INTEGRATION PASS ({_checks} checks)");
   GetTree().Quit(0);
  }
  catch(Exception e){GD.PushError(e.ToString());foreach(var c in _clients)await c.Close();GetTree().Quit(1);}
 }
}
