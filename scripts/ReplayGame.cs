using System.Collections.Generic;

public sealed class ReplayGame
{
    public string Id {get;set;}="";
    public string UserId {get;set;}="";
    public string PlayerName {get;set;}="";
    public string Code {get;set;}="";
    public long PlayedAt {get;set;}
    public string Result {get;set;}="IN PROGRESS";
    public int Place {get;set;}
    public bool Rated {get;set;}
    public bool MmrPending {get;set;}
    public int MmrBefore {get;set;}
    public int MmrDelta {get;set;}
    public int FinalHp {get;set;}
    public List<OnlineState> Rounds {get;set;}=new();
    public List<OnlineState> Preparations {get;set;}=new();
    [System.Text.Json.Serialization.JsonIgnore]
    public string MmrText=>!Rated?"Unrated":MmrPending?"MMR pending":$"MMR {MmrDelta:+0;-0;0}  /  {MmrBefore} → {MmrBefore+MmrDelta}";
}
