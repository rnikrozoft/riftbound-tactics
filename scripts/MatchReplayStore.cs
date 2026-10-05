using System.Collections.Generic;
using System.Text.Json;

public static class MatchReplayStore
{
    public static List<OnlineState> Rounds {get;}=new();
    public static string UserId {get;private set;}="";
    public static void Clear(){Rounds.Clear();UserId="";}
    public static void Record(OnlineState state,string userId)
    {
        if(state.Battle==null || Rounds.Exists(s=>s.Round==state.Round))return;
        UserId=userId;
        Rounds.Add(JsonSerializer.Deserialize(JsonSerializer.Serialize(state,GameJsonContext.Default.OnlineState),GameJsonContext.Default.OnlineState)!);
    }
}
