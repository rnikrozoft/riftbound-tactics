using Godot;
using Nakama;
using System;
using System.Linq;

public partial class LeaderboardPage : VBoxContainer
{
    public bool Loading=>_loading;
    public int RecordCount {get;private set;}
    private NakamaConnection? _connection;
    private VBoxContainer _rows=null!;
    private Label _status=null!,_own=null!;
    private TextureButton _refresh=null!,_next=null!,_previous=null!;
    private string _nextCursor="",_previousCursor="";
    private bool _loading,_exiting;
    public override void _Ready()
    {
        SizeFlagsVertical=SizeFlags.ExpandFill;
        var header=new HBoxContainer();AddChild(header);
        var title=DeckMenuUi.Text("ARENA RANKINGS",26);title.SizeFlagsHorizontal=SizeFlags.ExpandFill;header.AddChild(title);
        _refresh=DeckMenuUi.Button("REFRESH",()=>Load(),120);header.AddChild(_refresh);
        AddChild(DeckMenuUi.Text("Ranked standings use MMR. Placement and opponent ratings determine changes; bots do not affect MMR.",15));
        _own=DeckMenuUi.Text("Your rank: loading...",18);AddChild(_own);_own.AddThemeColorOverride("font_color",GameUi.Gold);
        AddChild(Row("RANK","COMMANDER","RATING","GAMES","",true));
        var scroll=new ScrollContainer {SizeFlagsVertical=SizeFlags.ExpandFill,HorizontalScrollMode=ScrollContainer.ScrollMode.Disabled};AddChild(scroll);
        _rows=new VBoxContainer {SizeFlagsHorizontal=SizeFlags.ExpandFill};scroll.AddChild(_rows);
        var footer=new HBoxContainer();AddChild(footer);
        _previous=DeckMenuUi.Button("< PREVIOUS",()=>Load(_previousCursor),140);footer.AddChild(_previous);
        _next=DeckMenuUi.Button("NEXT >",()=>Load(_nextCursor),140);footer.AddChild(_next);
        _status=DeckMenuUi.Text("",14);footer.AddChild(_status);Controls();
    }
    private PanelContainer Row(string rank,string name,string score,string wins,string uid,bool heading=false)
    {
        var row=new HBoxContainer {CustomMinimumSize=new(0,36)};
        string[] values={rank,name,score,wins};int[] widths={72,270,110,90};
        for(int i=0;i<values.Length;i++)
        {
            var label=DeckMenuUi.Text(values[i],heading?15:i==4?12:16);label.CustomMinimumSize=new(widths[i],0);label.TooltipText=values[i];label.TextOverrunBehavior=TextServer.OverrunBehavior.TrimEllipsis;
            if(i==1)label.SizeFlagsHorizontal=SizeFlags.ExpandFill;label.TooltipText=i==1?uid:values[i];row.AddChild(label);
        }
        var frame=new PanelContainer();frame.AddThemeStyleboxOverride("panel",GameUi.Box(heading,10));frame.AddChild(row);return frame;
    }
    private static string RatedGames(string metadata) { try { using var doc=System.Text.Json.JsonDocument.Parse(metadata);return doc.RootElement.GetProperty("rated_games").ToString(); } catch { return "0"; } }
    public void Open()=>Load();
    public async void Load(string cursor="")
    {
        if(_loading||_exiting)return;_loading=true;Controls();_status.Text="Loading arena rankings...";
        try
        {
            if(_connection==null){_connection=GameAccount.Connection();await _connection.Connect();}
            if(_exiting){if(_connection!=null)await _connection.Close();return;}
            var result=await _connection.Client.ListLeaderboardRecordsAsync(_connection.Session,NakamaConnection.MmrLeaderboardId,new[]{_connection.Session.UserId},limit:50,cursor:cursor);
            if(_exiting)return;
            DeckMenuUi.Clear(_rows);RecordCount=result.Records.Count();
            foreach(var record in result.Records)
            {
                bool mine=record.OwnerId==_connection.Session.UserId;
                string name=string.IsNullOrWhiteSpace(record.Username)?record.OwnerId:record.Username;
                _rows.AddChild(Row("#"+record.Rank,(mine?"YOU / ":"")+name,record.Score,RatedGames(record.Metadata),record.OwnerId));
            }
            var own=result.OwnerRecords.FirstOrDefault(r=>r.OwnerId==_connection.Session.UserId);
            _own.Text=own==null?"You: unranked / starting MMR 1,000":$"You: #{own.Rank} / {own.Score} MMR / {RatedGames(own.Metadata)} rated games";
            _nextCursor=result.NextCursor??"";_previousCursor=result.PrevCursor??"";
            _status.Text=result.Records.Any()?"Scores confirmed by the server.":"No rated players yet. Play against other players to enter the leaderboard.";
        }
        catch(Exception e)
        {
            if(!_exiting){_status.Text="Cannot load leaderboard. Press Refresh to retry.";_own.Text="Leaderboard unavailable";GD.PushWarning("Leaderboard: "+e.Message);}
            if(_connection!=null){await _connection.Close();_connection=null;}
        }
        finally {if(!_exiting){_loading=false;Controls();}}
    }
    private void Controls(){_refresh.Disabled=_loading;_next.Disabled=_loading||string.IsNullOrEmpty(_nextCursor);_previous.Disabled=_loading||string.IsNullOrEmpty(_previousCursor);}
    public override void _ExitTree(){_exiting=true;if(_connection!=null)_= _connection.Close();}
}
