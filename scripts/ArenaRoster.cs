using Godot;
using System.Collections.Generic;
using System.Linq;

public partial class ArenaRoster : VBoxContainer
{
    public void UpdateRoster(OnlineState state,string localId,IReadOnlyDictionary<string,string> names)
    {
        DeckMenuUi.Clear(this);AddThemeConstantOverride("separation",3);
        foreach(var pair in state.Roster.Select((player,index)=>(player,index))) {
            var p=pair.player;bool active=p.UserId==localId;
            string name=names.TryGetValue(p.UserId,out var known)&&!string.IsNullOrWhiteSpace(known)?known:active?"YOU":p.Bot?"BOT":"PLAYER";
            var letters=new System.Globalization.StringInfo(name);if(letters.LengthInTextElements>14)name=letters.SubstringByTextElements(0,13)+"…";
            var frame=new PanelContainer {Name="Player_"+pair.index,MouseFilter=MouseFilterEnum.Ignore};AddChild(frame);
            if(active){var style=MenuUi.Box(true,4);style.ContentMarginLeft=style.ContentMarginRight=6;frame.AddThemeStyleboxOverride("panel",style); }
            else frame.AddThemeStyleboxOverride("panel",new StyleBoxFlat {BgColor=new Color(0,0,0,.08f),ContentMarginLeft=6,ContentMarginRight=6,ContentMarginTop=4,ContentMarginBottom=4});
            var row=new HBoxContainer();row.AddThemeConstantOverride("separation",7);frame.AddChild(row);
            var rank=DeckMenuUi.GameplayText($"{pair.index+1:00}",13);rank.CustomMinimumSize=new(22,0);rank.AddThemeColorOverride("font_color",active?GameUi.Teal:GameUi.Text);row.AddChild(rank);
            var portrait=new PlayerAvatar {Name="Portrait",CustomMinimumSize=new(25,30)};portrait.Bind(p.UserId);row.AddChild(portrait);
            var label=DeckMenuUi.GameplayText(name,13);label.AutoTranslateMode=AutoTranslateModeEnum.Disabled;label.CustomMinimumSize=new(42,0);row.AddChild(label);
            row.AddChild(new Control {SizeFlagsHorizontal=SizeFlags.ExpandFill});
            row.AddChild(GameUi.Icon("IconHeart01a",16));
            var hp=DeckMenuUi.GameplayText($"{p.Hp:00} HP"+(p.Place>0?$" / #{p.Place}":""),13);hp.AddThemeColorOverride("font_color",active?GameUi.Teal:GameUi.Text);row.AddChild(hp);
        }
        GetParent<Control>().ResetSize();
        if(GetParent().GetParent() is PanelContainer panel)panel.ResetSize();
    }
}
