using Godot;
using System;

public partial class ReplayHistoryPage : VBoxContainer
{
    private VBoxContainer _rows=null!;
    private Label _summary=null!;
    public override void _Ready()
    {
        SizeFlagsVertical=SizeFlags.ExpandFill;AddThemeConstantOverride("separation",12);
        AddChild(DeckMenuUi.Text("MATCH REPLAYS",26));
        _summary=DeckMenuUi.Text("",14);AddChild(_summary);
        var scroll=new ScrollContainer {SizeFlagsVertical=SizeFlags.ExpandFill,HorizontalScrollMode=ScrollContainer.ScrollMode.Disabled};AddChild(scroll);
        _rows=new VBoxContainer {SizeFlagsHorizontal=SizeFlags.ExpandFill};_rows.AddThemeConstantOverride("separation",10);scroll.AddChild(_rows);
    }
    public void Open()
    {
        DeckMenuUi.Clear(_rows);var history=MatchReplayStore.History();
        _summary.Text=$"{history.Count} / 10 saved games  /  New games replace the oldest  /  Saved on this device";
        if(!string.IsNullOrEmpty(MatchReplayStore.Error)){_rows.AddChild(DeckMenuUi.Text(MatchReplayStore.Error,14));return;}
        if(history.Count==0){_rows.AddChild(DeckMenuUi.Text("No replays yet. Play a match to save your first replay.",18));return;}
        foreach(var game in history){
            var panel=new PanelContainer();panel.AddThemeStyleboxOverride("panel",GameUi.Box());_rows.AddChild(panel);
            var row=new HBoxContainer();row.AddThemeConstantOverride("separation",16);panel.AddChild(row);
            var info=new VBoxContainer {SizeFlagsHorizontal=SizeFlags.ExpandFill};row.AddChild(info);
            string date=DateTimeOffset.FromUnixTimeMilliseconds(game.PlayedAt).ToLocalTime().ToString("dd MMM yyyy  HH:mm");
            var heading=DeckMenuUi.Text($"{game.Result}   /   {date}",18);heading.AddThemeColorOverride("font_color",game.Result=="WIN"?GameUi.Teal:game.Result=="LOSS"?new Color("ff7883"):GameUi.Gold);info.AddChild(heading);
            var detail=DeckMenuUi.Text($"{game.Rounds.Count} rounds   /   HP {game.FinalHp}{(game.Place>0?$"   /   Place #{game.Place}":"")}   /   {game.MmrText}",14);detail.AutowrapMode=TextServer.AutowrapMode.WordSmart;info.AddChild(detail);
            var selected=game;row.AddChild(DeckMenuUi.Button("WATCH REPLAY",()=>{MatchReplayStore.Select(selected);GetTree().ChangeSceneToFile("res://scenes/match_replay.tscn");},160));
        }
    }
}
