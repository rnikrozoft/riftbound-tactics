using Godot;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

public sealed class DeckMatchRecord
{
    public string MatchId {get;set;}="";
    public string UserId {get;set;}="";
    public string DeckId {get;set;}="";
    public string Result {get;set;}="";
    public long PlayedAt {get;set;}
}
public static class DeckMatchHistory
{
    internal static string? TestPath;
    private static string Path=>TestPath??ProjectSettings.GlobalizePath("user://deck-match-history.json");
    public static string Error {get;private set;}="";
    public static List<DeckMatchRecord> Read()
    {
        Error="";
        try{return File.Exists(Path)?JsonSerializer.Deserialize(File.ReadAllText(Path),GameJsonContext.Default.ListDeckMatchRecord)??new():new();}
        catch(Exception e){Error="Cannot load match statistics: "+e.Message;return new();}
    }
    public static void Record(ReplayGame game)
    {
        if(string.IsNullOrEmpty(game.DeckId)||string.IsNullOrEmpty(game.UserId)||string.IsNullOrEmpty(game.Id)||game.Result is not ("WIN" or "DRAW" or "LOSS"))return;
        var records=Read();if(Error!=""){GD.PushWarning(Error);return;}
        records.RemoveAll(r=>r.MatchId==game.Id&&r.UserId==game.UserId);
        records.Add(new DeckMatchRecord {MatchId=game.Id,UserId=game.UserId,DeckId=game.DeckId,Result=game.Result,PlayedAt=game.PlayedAt});
        try{File.WriteAllText(Path+".tmp",JsonSerializer.Serialize(records,GameJsonContext.Default.ListDeckMatchRecord));File.Move(Path+".tmp",Path,true);}
        catch(Exception e){Error="Cannot save match statistics: "+e.Message;GD.PushWarning(Error);}
    }
    public static DeckMatchRecord[] ForDeck(string userId,string deckId)=>Read().Where(r=>r.UserId==userId&&r.DeckId==deckId&&r.Result is "WIN" or "DRAW" or "LOSS").OrderByDescending(r=>r.PlayedAt).ToArray();
}
