using Godot;
using System.Linq;

public partial class DeckHistoryChart : VBoxContainer
{
    private readonly DeckDefinition _deck;
    public DeckHistoryChart():this(DeckDefinition.Starter()){}
    public DeckHistoryChart(DeckDefinition deck){_deck=deck;AddThemeConstantOverride("separation",10);}
    public override void _Ready()
    {
        var history=DeckMatchHistory.ForDeck(GameAccount.Session?.UserId??"",_deck.Id);
        int wins=history.Count(r=>r.Result=="WIN"),draws=history.Count(r=>r.Result=="DRAW"),losses=history.Count(r=>r.Result=="LOSS");
        AddChild(new MatchupDonut(wins,draws,losses) {Name="MatchHistoryDonut"});
        var summary=DeckMenuUi.Text(history.Length==0?"No completed matches":$"{history.Length} matches played",13);AddChild(summary);
        if(DeckMatchHistory.Error!="")summary.Text="Match statistics unavailable";
        if(history.Length==0)return;
        AddChild(MenuUi.Eyebrow("RECENT MATCHES"));
        var recent=new HBoxContainer();recent.AddThemeConstantOverride("separation",8);AddChild(recent);
        foreach(var game in history.Take(10)){
            var label=DeckMenuUi.Text(game.Result=="WIN"?"W":game.Result=="DRAW"?"D":"L",14);
            label.AddThemeColorOverride("font_color",new Color(game.Result=="WIN"?"6bddc5":game.Result=="DRAW"?"e8ba65":"e17e8f"));recent.AddChild(label);
        }
    }
}
