using Godot;
using System;
using System.Linq;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading.Tasks;

public partial class MmrIntegrationRunner : Node
{
 private readonly List<NakamaConnection> _clients=new();
 private readonly ConcurrentDictionary<string,OnlineState> _states=new();
 private readonly Dictionary<string,long> _sequences=new();
 private int _checks;
 private void Check(bool ok,string message){_checks++;if(!ok)throw new Exception(message);}
 private async Task Wait(Func<bool> f,string message,int timeout=15000){ulong until=Time.GetTicksMsec()+(ulong)timeout;while(!f()){if(Time.GetTicksMsec()>until)throw new TimeoutException(message);await ToSignal(GetTree(),SceneTree.SignalName.ProcessFrame);}}
 private OnlineState State(NakamaConnection c)=>_states[c.DeviceId];
 private OnlinePlayer Own(NakamaConnection c)=>State(c).Players.First(p=>p?.UserId==c.Session.UserId)!;
 private async Task<(int rating,int games)> Account(NakamaConnection c,string payload="{}")
 {
  var rpc=await c.Socket.RpcAsync("mmr_self",payload);using var json=JsonDocument.Parse(rpc.Payload);
  return(json.RootElement.GetProperty("mmr").GetInt32(),json.RootElement.GetProperty("rated_games").GetInt32());
 }
 private async Task Act(NakamaConnection c,string type,int token=0,int slot=0)
 {
  long seq=++_sequences[c.DeviceId];int round=State(c).Round;
  await c.Send(new(){Type=type,Token=token,Slot=slot,Round=round,Sequence=seq});
  await Wait(()=>State(c).AckSequence>=seq,"action acknowledgement: "+type);
 }
 private async Task Deploy(NakamaConnection c)
 {
  while(Own(c).Hand.Length>0&&Own(c).Units.Length<6)
  {
   int slot=Enumerable.Range(0,6).First(s=>!Own(c).Units.Any(u=>u.Slot==s));await Act(c,"deploy",Own(c).Hand[0].Token,slot);
  }
 }
 public override void _Ready()=>Callable.From(Run).CallDeferred();
 private async void Run()
 {
  try
  {
   for(int i=0;i<6;i++)
   {
    var c=new NakamaConnection();_clients.Add(c);_sequences[c.DeviceId]=0;c.StateReceived+=s=>_states[c.DeviceId]=s;
    c.ErrorReceived+=e=>GD.Print("MMR test server: "+e.Message);await c.Connect();var a=await Account(c);
    Check(a.rating==1000&&a.games==0,"new account starts at independent MMR 1000");
   }
   var players=_clients.ToArray();var rooms=await Task.WhenAll(players.Select(c=>c.FindMatch(DeckDefinition.Starter())));
   Check(rooms.Select(r=>r.MatchId).Distinct().Count()==1,"six humans share ranked game");string matchId=rooms[0].MatchId;
   var leader=players[0];await Wait(()=>_states.TryGetValue(leader.DeviceId,out var s)&&s.Phase=="preparation","ranked preparation");
   var checkedLosers=new HashSet<string>();int previousRound=0;
   while(State(leader).Phase!="game_over")
   {
    await Wait(()=>State(leader).Phase=="game_over"||(State(leader).Phase=="preparation"&&State(leader).Round>previousRound),"next ranked round",25000);
    if(State(leader).Phase=="game_over")break;
    previousRound=State(leader).Round;if(previousRound>40)throw new Exception("ranked game did not terminate");
    GD.Print("MMR ranked round "+previousRound);
    await Deploy(leader);
    if(Own(leader).ShopLevel<6&&Own(leader).Coins>=Own(leader).UpgradeCost+2)await Act(leader,"upgrade");
    for(int step=0;step<16;step++)
    {
     var own=Own(leader);
     var offer=own.Offers.FirstOrDefault(c=>c.Price<=own.Coins&&
       (own.Units.Any(u=>u.Kind==c.Kind&&u.Stars<4)||own.Hand.Any(h=>h.Kind==c.Kind&&h.Stars<4)||
        (!own.Units.Any(u=>u.Kind==c.Kind)&&!own.Hand.Any(h=>h.Kind==c.Kind)&&own.Units.Length+own.Hand.Length<6)));
     if(offer!=null){await Act(leader,"buy",offer.Token);await Deploy(leader);continue;}
     if(own.Coins>=4){await Act(leader,"reroll");continue;}break;
    }
    foreach(var c in players)
    {
     if(c==leader)continue;
     if(State(leader).Roster.Single(p=>p.UserId==c.Session.UserId).Hp>0)
     {
      await Wait(()=>State(c).Phase=="preparation"&&State(c).Round==previousRound,"other human preparation");await Act(c,"ready");
     }
    }
    await Act(leader,"ready");
    await Wait(()=>State(leader).Phase=="finished"||State(leader).Phase=="game_over","ranked combat resolves",25000);
    foreach(var c in players)
    {
     if(c==leader||State(leader).Roster.Single(p=>p.UserId==c.Session.UserId).Hp>0||!checkedLosers.Add(c.DeviceId))continue;
     await Wait(()=>State(c).Phase is "eliminated" or "game_over","eliminated final snapshot");
     var a=await Account(c);Check(a.games==1&&a.rating==State(c).Roster.Single(p=>p.UserId==c.Session.UserId).Rating,"MMR saved before elimination kick");
     Check(c.MatchId=="","eliminated transport leaves match");bool rejected=false;try{await c.Socket.JoinMatchAsync(matchId);}catch{rejected=true;}
     Check(rejected,"eliminated account cannot rejoin ranked game");
     var spoof=await Account(c,"{\"mmr\":99999}");Check(spoof==a,"client cannot set own MMR through read API");
    }
   }
   Check(State(leader).WinnerId==leader.Session.UserId,"strong formation wins game");
   int total=0;foreach(var c in players){var a=await Account(c);total+=a.rating;Check(a.games==1,"one rated result per person, including earlier eliminations");}
   var board=await leader.Client.ListLeaderboardRecordsAsync(leader.Session,NakamaConnection.MmrLeaderboardId,players.Select(c=>c.Session.UserId).ToArray(),limit:100);
   foreach(var c in players){var a=await Account(c);var record=board.OwnerRecords.Single(r=>r.OwnerId==c.Session.UserId);Check(int.Parse(record.Score)==a.rating,"MMR leaderboard includes winners and losers at current rating");}
   var scores=board.Records.Select(r=>long.Parse(r.Score)).ToArray();Check(scores.SequenceEqual(scores.OrderByDescending(x=>x)),"leaderboard sorts descending MMR");
   Check(total==6000,"equal-rating six-human placements conserve total MMR");
   var winner=await Account(leader);Check(winner.rating==1016,"first place against equal human MMR earns 16");
   await Task.Delay(2200);Check(await Account(leader)==winner,"repeated game-over ticks do not duplicate MMR");
   foreach(var c in players)await c.Close();
   var reconnect=new NakamaConnection(deviceId:leader.DeviceId);_clients.Add(reconnect);reconnect.StateReceived+=s=>_states[reconnect.DeviceId]=s;await reconnect.Connect();
   Check(await Account(reconnect)==winner,"MMR survives a new authentication session");
   await reconnect.FindMatch(DeckDefinition.Starter());await Wait(()=>State(reconnect).Phase=="preparation","MMR-based solo queue",35000);
   Check(State(reconnect).Roster.Single(p=>p.UserId==reconnect.Session.UserId).Rating==1016,"matchmaking reads MMR, not one-win proxy 1025");
   await reconnect.Close();GD.Print($"MMR INTEGRATION PASS ({_checks} checks)");GetTree().Quit(0);
  }
  catch(Exception e){GD.PushError(e.ToString());foreach(var c in _clients)await c.Close();GetTree().Quit(1);}
 }
}
