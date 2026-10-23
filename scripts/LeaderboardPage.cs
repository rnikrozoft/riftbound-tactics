using Godot;
using Nakama;
using System;
using System.Linq;

public partial class LeaderboardPage : VBoxContainer
{
    public bool Loading=>_loading;
    public int RecordCount {get;private set;}
    public int TopRecordCount {get;private set;}
    private NakamaConnection? _connection;
    private VBoxContainer _rows=null!;
    private Label _status=null!,_own=null!;
    private VBoxContainer _self=null!;
    private bool _loading,_exiting;
    private PixelLoadingIndicator _loader=null!;
    public override void _Process(double delta){if(_loader!=null)_loader.Visible=_loading;}
    public override void _Ready()
    {
        SizeFlagsVertical=SizeFlags.ExpandFill;
        var header=new HBoxContainer();AddChild(header);
        _loader=new PixelLoadingIndicator {Name="LeaderboardLoadingSpinner",Visible=false};header.AddChild(_loader);
        var title=DeckMenuUi.Text("Arena rankings",28);title.SizeFlagsHorizontal=SizeFlags.ExpandFill;header.AddChild(title);
        AddThemeConstantOverride("separation",16);
        _own=DeckMenuUi.Text("Your rank: loading...",18);AddChild(_own);_own.AddThemeColorOverride("font_color",GameUi.Gold);
        _self=new VBoxContainer {Name="OwnRanking"};
        AddChild(Row("RANK","COMMANDER","RATING","GAMES","",true));
        var scroll=new ScrollContainer {Name="RankingScroll",SizeFlagsVertical=SizeFlags.ExpandFill,HorizontalScrollMode=ScrollContainer.ScrollMode.Disabled};AddChild(scroll);
        _rows=new VBoxContainer {SizeFlagsHorizontal=SizeFlags.ExpandFill};scroll.AddChild(_rows);
        _status=DeckMenuUi.Text("",14);AddChild(_status);_status.Hide();
        AddChild(_self);
    }
    private PanelContainer Row(string rank,string name,string score,string wins,string uid,bool heading=false)
    {
        var row=new HBoxContainer {CustomMinimumSize=new(0,44)};
        string[] values={rank,name,score,wins};int[] widths={72,180,110,90};
        for(int i=0;i<values.Length;i++)
        {
            var label=DeckMenuUi.Text(values[i],heading?15:i==4?12:16);label.CustomMinimumSize=new(widths[i],0);label.TextOverrunBehavior=TextServer.OverrunBehavior.TrimEllipsis;
            if(i==1){label.SizeFlagsHorizontal=SizeFlags.ExpandFill;if(!heading)label.AutoTranslateMode=AutoTranslateModeEnum.Disabled;}row.AddChild(label);
        }
        var frame=new PanelContainer();frame.AddThemeStyleboxOverride("panel",MenuUi.Box(heading,10));frame.AddChild(row);return frame;
    }
    private static string RatedGames(string metadata) { try { using var doc=System.Text.Json.JsonDocument.Parse(metadata);return doc.RootElement.GetProperty("rated_games").ToString(); } catch { return "0"; } }
    public void Open()=>Load();
    public async void Load(string cursor="")
    {
        if(_loading||_exiting)return;_loading=true;_status.Text="Loading arena rankings...";
        try
        {
            if(_connection==null){_connection=GameAccount.Connection();await _connection.Connect(realtime:false);}
            if(_exiting){if(_connection!=null)await _connection.Close();return;}
            var result=await _connection.Client.ListLeaderboardRecordsAsync(_connection.Session,NakamaConnection.MmrLeaderboardId,new[]{_connection.Session.UserId},limit:100);
            if(_exiting)return;
            DeckMenuUi.Clear(_rows);TopRecordCount=Math.Min(100,result.Records.Count());
            foreach(var record in result.Records.Take(100))
            {
                bool mine=record.OwnerId==_connection.Session.UserId;
                string name=string.IsNullOrWhiteSpace(record.Username)?record.OwnerId:record.Username;
                _rows.AddChild(Row("#"+record.Rank,(mine?"YOU / ":"")+name,record.Score,RatedGames(record.Metadata),record.OwnerId));
            }
            var own=result.OwnerRecords.FirstOrDefault(r=>r.OwnerId==_connection.Session.UserId);
            _own.Text=own==null?"You: unranked / starting MMR 1,000":$"You: #{own.Rank} / {own.Score} MMR / {RatedGames(own.Metadata)} rated games";
            DeckMenuUi.Clear(_self);
            var self=Row(own==null?"—":"#"+own.Rank,string.IsNullOrWhiteSpace(own?.Username??GameAccount.DisplayName)?"YOU":"YOU / "+(own?.Username??GameAccount.DisplayName),own?.Score??"1000",own==null?"0":RatedGames(own.Metadata),_connection.Session.UserId);
            self.Name="OwnRankingRow";self.AddThemeStyleboxOverride("panel",MenuUi.Box(true,10));_self.AddChild(self);
            RecordCount=Math.Min(100,result.Records.Count())+1;
            _status.Text="";_status.Hide();
        }
        catch(Exception e)
        {
            if(!_exiting){_status.Text="Cannot load leaderboard. Reopen this page to retry.";_status.Show();_own.Text="Leaderboard unavailable";GD.PushWarning("Leaderboard: "+e.Message);}
            if(_connection!=null){await _connection.Close();_connection=null;}
        }
        finally {if(!_exiting){_loading=false;}}
    }
    public override void _ExitTree(){_exiting=true;if(_connection!=null)_= _connection.Close();}
}
