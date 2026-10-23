using Godot;
using System;
using System.Threading.Tasks;
public partial class MatchmakingCancelRunner:Node
{
 public override async void _Ready(){try{
  foreach(bool immediate in new[]{true,false}){
   var connection=new NakamaConnection();await connection.Connect();var started=new TaskCompletionSource<bool>();
   var search=connection.FindMatch(DeckDefinition.Starter(),_=>started.TrySetResult(true));
   if(!immediate)await started.Task.WaitAsync(TimeSpan.FromSeconds(8));
   await Task.WhenAll(connection.Close(),connection.Close()).WaitAsync(TimeSpan.FromSeconds(15));
   bool cancelled=false;try{await search;}catch(OperationCanceledException){cancelled=true;}
   if(!cancelled||connection.Socket.IsConnected||connection.MatchId!="")throw new Exception("Search did not cancel/disconnect");
   bool removed=false;try{await connection.Client.RpcAsync(connection.Session,"queue_status","{}");}catch(Nakama.ApiResponseException e){removed=e.Message.Contains("cancelled")||e.Message.Contains("expired");}
   if(!removed)throw new Exception("Server queue still active");
  }
  GD.Print("MATCHMAKING CANCEL PASS: immediate and active search, repeated close, ticket/queue cleanup, socket disconnected, no late join");GetTree().Quit();
 }catch(Exception e){GD.PushError(e.ToString());GetTree().Quit(1);}}
}
