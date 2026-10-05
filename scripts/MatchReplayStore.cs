using System.Collections.Generic;
using System.Text.Json;
using System;
using System.Linq;
using System.IO;
using Godot;

public static class MatchReplayStore
{
    public static List<OnlineState> Rounds {get;}=new();
    public static List<OnlineState> Preparations {get;}=new();
    public static string UserId {get;private set;}="";
    public const int Capacity=10;
    public static bool FromHistory {get;private set;}
    public static string Error {get;private set;}="";
    internal static string? TestPath {get;set;}
    private static ReplayGame? _current;
    private static string Path {
        get {if(TestPath!=null)return TestPath;_ = GameAccount.DeviceId;return ProjectSettings.GlobalizePath($"user://replays-{GameAccount.InstanceSlot}.json");}
    }
    public static IReadOnlyList<ReplayGame> History()
    {
        try {
            Error="";
            if(!File.Exists(Path))return Array.Empty<ReplayGame>();
            var history=JsonSerializer.Deserialize(File.ReadAllText(Path),GameJsonContext.Default.ListReplayGame)??new();
            foreach(var game in history){
                game.Rounds=(game.Rounds??new()).Where(s=>HasBattle(s,game.UserId)).ToList();
                game.Preparations=(game.Preparations??new()).Where(s=>HasPreparation(s,game.UserId)).ToList();
            }
            return history.Where(g=>g.Rounds.Count>0||g.Preparations.Count>0).OrderByDescending(g=>g.PlayedAt).Take(Capacity).ToList();
        }catch(Exception e){Error="Cannot read replay history: "+e.Message;return Array.Empty<ReplayGame>();}
    }
    private static void Save()
    {
        if(_current==null||(Rounds.Count==0&&Preparations.Count==0)||FromHistory)return;
        try {
            var history=History().Where(g=>g.Id!=_current.Id).ToList();
            if(!string.IsNullOrEmpty(Error)){GD.PushError(Error);return;}
            _current.Rounds=Rounds.ToList();_current.Preparations=Preparations.ToList();history.Add(_current);
            history=history.OrderByDescending(g=>g.PlayedAt).Take(Capacity).ToList();
            string path=Path;string temp=path+".tmp";
            File.WriteAllText(temp,JsonSerializer.Serialize(history,GameJsonContext.Default.ListReplayGame));File.Move(temp,path,true);Error="";
        }catch(Exception e){Error="Cannot save replay: "+e.Message;GD.PushError(Error);}
    }
    public static void Clear(){Rounds.Clear();Preparations.Clear();UserId="";_current=null;FromHistory=false;}
    public static void Select(ReplayGame game)
    {
        Rounds.Clear();Rounds.AddRange(game.Rounds.Select(Clone));UserId=game.UserId;_current=game;FromHistory=true;
        Preparations.Clear();Preparations.AddRange(game.Preparations.Select(Clone));
    }
    public static OnlineState Clone(OnlineState state)=>JsonSerializer.Deserialize(JsonSerializer.Serialize(state,GameJsonContext.Default.OnlineState),GameJsonContext.Default.OnlineState)!;
    private static bool HasPlayer(OnlineState state,string userId)=>!string.IsNullOrEmpty(userId)&&state.Players.Any(p=>p?.UserId==userId);
    private static bool HasBattle(OnlineState state,string userId)=>state.Round>0&&state.Battle!=null&&HasPlayer(state,userId);
    private static bool HasPreparation(OnlineState state,string userId)=>state.Round>0&&state.Phase=="preparation"&&HasPlayer(state,userId);
    public static void Record(OnlineState state,string userId)
    {
        if(FromHistory||!HasBattle(state,userId)||Rounds.Exists(s=>s.Round==state.Round))return;
        EnsureCurrent(state,userId);
        Rounds.Add(Clone(state));Observe(state,userId);Save();
    }
    private static void EnsureCurrent(OnlineState state,string userId)
    {
        UserId=userId;
        var standing=state.Roster.FirstOrDefault(p=>p.UserId==userId);
        _current??=new ReplayGame {Id=Guid.NewGuid().ToString("N"),UserId=userId,PlayerName=GameAccount.DisplayName,Code=state.Code,PlayedAt=state.ServerMs>0?state.ServerMs:DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),MmrBefore=standing?.Rating??0,Rated=standing!=null&&!standing.Bot};
        _current.FinalHp=standing?.Hp??state.Players.FirstOrDefault(p=>p?.UserId==userId)?.Hp??0;
    }
    public static void RecordPreparation(OnlineState state,string userId)
    {
        if(FromHistory||!HasPreparation(state,userId))return;
        var owner=state.Players.FirstOrDefault(p=>p?.UserId==userId);if(owner==null)return;
        var previous=Preparations.LastOrDefault(s=>s.Round==state.Round);
        // Ignore clock updates and hidden opponent changes; retain every acknowledged
        // change to the owner's shop, hand, formation, coins or ready state.
        var previousOwner=previous?.Players.FirstOrDefault(p=>p?.UserId==userId);
        if(previousOwner!=null&&JsonSerializer.Serialize(previousOwner,GameJsonContext.Default.OnlinePlayer)==JsonSerializer.Serialize(owner,GameJsonContext.Default.OnlinePlayer))return;
        EnsureCurrent(state,userId);var snapshot=Clone(state);snapshot.Battle=null;Preparations.Add(snapshot);Save();
    }
    public static void Observe(OnlineState state,string userId)
    {
        if(_current==null||FromHistory||_current.UserId!=userId)return;
        var player=state.Players.FirstOrDefault(p=>p?.UserId==userId);
        var standing=state.Roster.FirstOrDefault(p=>p.UserId==userId);
        bool finished=state.Phase is "game_over" or "eliminated" || standing?.Hp<=0 || player?.Hp<=0;
        if(!finished)return;
        _current.Result=state.Winner=="DRAW"?"DRAW":state.Roster.Length>0?(state.WinnerId==userId||standing?.Place==1?"WIN":"LOSS"):(state.Winner==player?.Team?"WIN":"LOSS");
        _current.Place=standing?.Place??0;_current.FinalHp=standing?.Hp??player?.Hp??0;
        _current.Rated=standing!=null&&!standing.Bot;_current.MmrPending=state.MmrPending;_current.MmrDelta=state.MmrDelta;Save();
    }
    public static void Leave()
    {
        if(_current==null||FromHistory||_current.Result!="IN PROGRESS")return;
        _current.Result="LEFT MATCH";_current.MmrPending=_current.Rated;Save();
    }
}
