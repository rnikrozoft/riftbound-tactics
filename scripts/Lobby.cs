using Godot;
using System;
using System.Linq;
using System.Collections.Generic;

public partial class Lobby : Control
{
    public string ActiveMenu {get;private set;}="Play";
    private readonly Dictionary<string,TextureButton> _navigation=new();
    private VBoxContainer _lobbyPage=null!,_futurePage=null!,_roomPanel=null!;
    private Label _pageTitle=null!,_futureTitle=null!;
    private LeaderboardPage _leaderboard=null!;
    private ReplayHistoryPage _replays=null!;
    public static string InitialMenu {get;set;}="Play";
    private GridContainer _decks=null!;
    private VBoxContainer _preview=null!;
    private Label _status=null!;
    private LineEdit _code=null!;
    private TextureButton _create=null!,_join=null!,_edit=null!;
    public override void _Ready()
    {
        DeckStore.Load();BattleLaunch.Pending=false;BattleLaunch.Deck=null;
        var page=DeckMenuUi.Page(this);
        var brand=new HBoxContainer();brand.AddThemeConstantOverride("separation",12);page.AddChild(brand);
        brand.AddChild(GameUi.Icon("IconEnergy01a",32));
        var logo=DeckMenuUi.Text("RIFTBOUND  /  TACTICS",28);logo.SizeFlagsHorizontal=SizeFlags.ExpandFill;brand.AddChild(logo);
        brand.AddChild(DeckMenuUi.Text(string.IsNullOrEmpty(GameAccount.DisplayName)?"SIX COMMANDERS. ONE SURVIVOR.":GameAccount.DisplayName,13));
        var shell=new HBoxContainer {SizeFlagsVertical=SizeFlags.ExpandFill};shell.AddThemeConstantOverride("separation",18);page.AddChild(shell);
        var navigation=DeckMenuUi.Panel(shell,176);
        navigation.AddChild(DeckMenuUi.Text("COMMAND",14));
        var menus=new[]{"Play","Decks","Shop","Leaderboard","Replays","Achievements"};
        var icons=new[]{"IconPlay01a","IconHome01a","IconCoin01a","IconStar01a","IconPlay01a","IconTick01a"};
        foreach(string menu in menus)
        {
            string destination=menu;var button=DeckMenuUi.Button(menu.ToUpperInvariant(),()=>Navigate(destination),152,46);navigation.AddChild(button);_navigation[menu]=button;
            var icon=GameUi.Icon(icons[Array.IndexOf(menus,menu)],16);
            button.AddChild(icon);icon.Position=new(12,15);icon.Size=new(16,16);button.GetNode<Label>("Text").OffsetLeft=24;GameUi.Label(button.GetNode<Label>("Text"),13);
        }
        navigation.AddChild(DeckMenuUi.Button("ANIMATION LAB",()=>GetTree().ChangeSceneToFile("res://scenes/character_animation_lab.tscn"),152,42));
        var effects=DeckMenuUi.Button("EFFECTS LIBRARY",()=>GetTree().ChangeSceneToFile("res://scenes/damage_simulator.tscn"),152,42);
        effects.Name="EffectsLibraryButton";navigation.AddChild(effects);
        navigation.AddChild(new Control {SizeFlagsVertical=SizeFlags.ExpandFill});
        navigation.AddChild(DeckMenuUi.Text("TACTICAL ARENA\nBUILD / DEPLOY / BATTLE",12));
        var content=new VBoxContainer {SizeFlagsHorizontal=SizeFlags.ExpandFill};shell.AddChild(content);
        _lobbyPage=new VBoxContainer {SizeFlagsVertical=SizeFlags.ExpandFill};_lobbyPage.AddThemeConstantOverride("separation",12);content.AddChild(_lobbyPage);
        var heading=new HBoxContainer();_lobbyPage.AddChild(heading);
        _pageTitle=DeckMenuUi.Text("ENTER THE ARENA",26);_pageTitle.SizeFlagsHorizontal=SizeFlags.ExpandFill;heading.AddChild(_pageTitle);
        heading.AddChild(DeckMenuUi.Button("+ NEW DECK",()=>Edit(""),150));
        _leaderboard=new LeaderboardPage {Name="Rankings"};content.AddChild(_leaderboard);_leaderboard.Hide();
        _replays=new ReplayHistoryPage {Name="Replays"};content.AddChild(_replays);_replays.Hide();
        _futurePage=new VBoxContainer {SizeFlagsVertical=SizeFlags.ExpandFill};content.AddChild(_futurePage);_futurePage.Hide();
        var future=DeckMenuUi.Panel(_futurePage);future.GetParent<Control>().SizeFlagsHorizontal=SizeFlags.ExpandFill;
        _futureTitle=DeckMenuUi.Text("",28);future.AddChild(_futureTitle);
        future.AddChild(DeckMenuUi.Text("COMING SOON",20));
        future.AddChild(DeckMenuUi.Text("More ways to grow your collection are on the way.",16));
        future.AddChild(DeckMenuUi.Button("BACK TO PLAY",()=>Navigate("Play"),180));
        var body=new HBoxContainer {SizeFlagsVertical=SizeFlags.ExpandFill};body.AddThemeConstantOverride("separation",16);_lobbyPage.AddChild(body);
        var collection=new VBoxContainer {SizeFlagsHorizontal=SizeFlags.ExpandFill};body.AddChild(collection);
        collection.AddChild(DeckMenuUi.Text("YOUR STRATEGY STARTS HERE",17));
        collection.AddChild(DeckMenuUi.Text("Choose three characters. Forge a deck. Outlast the arena.",14));
        var scroll=new ScrollContainer {SizeFlagsVertical=SizeFlags.ExpandFill,HorizontalScrollMode=ScrollContainer.ScrollMode.Disabled};collection.AddChild(scroll);
        _decks=new GridContainer {Columns=2,SizeFlagsHorizontal=SizeFlags.ExpandFill};_decks.AddThemeConstantOverride("h_separation",14);_decks.AddThemeConstantOverride("v_separation",14);scroll.AddChild(_decks);
        var arenaPanel=new PanelContainer {CustomMinimumSize=new(0,200)};arenaPanel.AddThemeStyleboxOverride("panel",GameUi.Box());collection.AddChild(arenaPanel);
        var arenaBox=new VBoxContainer();arenaPanel.AddChild(arenaBox);var arenaTitle=DeckMenuUi.Text("THE RUINED ARENA     /     SIX PLAYER BATTLE",12);arenaTitle.AddThemeColorOverride("font_color",GameUi.Gold);arenaBox.AddChild(arenaTitle);
        arenaBox.AddChild(new ArenaPreview {CustomMinimumSize=new(0,152),SizeFlagsVertical=SizeFlags.ExpandFill});
        collection.AddChild(DeckMenuUi.Text("Select a deck to play, or edit it to change cards and copies.",15));
        var side=DeckMenuUi.Panel(body,336);side.AddChild(DeckMenuUi.Text("BATTLE LOADOUT",16));
        _preview=new VBoxContainer();side.AddChild(_preview);
        _edit=DeckMenuUi.Button("EDIT THIS DECK",()=>Edit(DeckStore.SelectedId),304);side.AddChild(_edit);
        var spacer=new Control {SizeFlagsVertical=SizeFlags.ExpandFill};side.AddChild(spacer);
        _roomPanel=new VBoxContainer();side.AddChild(_roomPanel);
        _create=DeckMenuUi.Button("FIND MATCH",()=>Launch(true),304,48);_roomPanel.AddChild(_create);GameUi.Primary(_create);
        _roomPanel.AddChild(DeckMenuUi.Text("6 players / Nearby ranks",15));
        var matchmakingNote=DeckMenuUi.Text("Find opponents near your rank.\nBots fill empty seats after 20 seconds.",14);
        matchmakingNote.AutowrapMode=TextServer.AutowrapMode.WordSmart;_roomPanel.AddChild(matchmakingNote);
        // Kept as private compatibility controls for older test entry points.
        _code=new LineEdit();_join=new TextureButton();_roomPanel.AddChild(_code);_roomPanel.AddChild(_join);_code.Hide();_join.Hide();
        _status=DeckMenuUi.Text(DeckStore.Error,14);page.AddChild(_status);Render();Navigate(InitialMenu);InitialMenu="Play";
    }
    public void Navigate(string menu)
    {
        if(!_navigation.ContainsKey(menu))return;
        ActiveMenu=menu;bool decks=menu=="Decks";bool lobby=menu=="Play"||decks;
        _lobbyPage.Visible=lobby;_futurePage.Visible=!lobby&&menu!="Leaderboard"&&menu!="Replays";_leaderboard.Visible=menu=="Leaderboard";
        _replays.Visible=menu=="Replays";if(menu=="Replays")_replays.Open();
        if(menu=="Leaderboard")_leaderboard.Open();_roomPanel.Visible=!decks;
        _pageTitle.Text=decks?"YOUR DECK COLLECTION":"ENTER THE ARENA";
        _futureTitle.Text=menu.ToUpperInvariant();
        foreach(var entry in _navigation)GameUi.Selected(entry.Value,entry.Key==menu);
    }
    public void SelectDeck(string id)
    {
        if(!DeckStore.Decks.Any(d=>d.Id==id))return;
        DeckStore.SelectedId=id;DeckStore.Save();_status.Text=DeckStore.Error;Render();
    }
    private static HBoxContainer Heroes(DeckDefinition deck,int width,int height)
    {
        var row=new HBoxContainer();row.AddThemeConstantOverride("separation",6);
        foreach(int group in deck.Heroes)
        {
            var hero=new VBoxContainer();row.AddChild(hero);hero.AddChild(DeckMenuUi.Art(CardCatalog.Options(group,2).First(),width,height));
            var name=DeckMenuUi.Text(CardCatalog.Groups[group],12);name.HorizontalAlignment=HorizontalAlignment.Center;hero.AddChild(name);
        }
        return row;
    }
    private void Render()
    {
        DeckMenuUi.Clear(_decks);
        foreach(var deck in DeckStore.Decks)
        {
            var captured=deck;bool selected=deck.Id==DeckStore.SelectedId;
            var button=DeckMenuUi.Button("",()=>SelectDeck(captured.Id),244,242);button.SizeFlagsHorizontal=SizeFlags.ExpandFill;_decks.AddChild(button);
            GameUi.Selected(button,selected);
            var box=new VBoxContainer {MouseFilter=MouseFilterEnum.Ignore};button.AddChild(box);box.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);box.OffsetLeft=12;box.OffsetRight=-12;box.OffsetTop=12;box.OffsetBottom=-12;
            box.AddChild(DeckMenuUi.Text(selected?"ACTIVE LOADOUT":"TACTICAL DECK",12));box.AddChild(Heroes(deck,66,98));
            var name=DeckMenuUi.Text(deck.Name,18);name.AutowrapMode=TextServer.AutowrapMode.WordSmart;box.AddChild(name);
            box.AddChild(DeckMenuUi.Text(deck.Validate()==""?$"Ready  /  {deck.Cards.Count} kinds":"Needs editing",14));
        }
        DeckMenuUi.Clear(_preview);var chosen=DeckStore.Selected;
        if(chosen!=null)
        {
            _preview.AddChild(Heroes(chosen,88,122));
            var name=DeckMenuUi.Text(chosen.Name,24);name.AutowrapMode=TextServer.AutowrapMode.WordSmart;_preview.AddChild(name);
            _preview.AddChild(DeckMenuUi.Text($"NEUTRAL SUPPORT\n{chosen.Cards.Count}/40 kinds  /  {chosen.Cards.Sum(c=>c.Copies)} copies",16));
            var ready=DeckMenuUi.Text(chosen.Validate()==""?"READY FOR BATTLE":chosen.Validate(),15);ready.AutowrapMode=TextServer.AutowrapMode.WordSmart;_preview.AddChild(ready);
        }
        RefreshControls();
    }
    private void RefreshControls()
    {
        var deck=DeckStore.Selected;bool valid=deck!=null&&deck.Validate()=="";
        _create.Disabled=!valid;_join.Disabled=!valid||_code.Text.Length!=6||!_code.Text.All(char.IsDigit);_edit.Disabled=deck==null;
    }
    private void Edit(string id){DeckStore.EditId=id;GetTree().ChangeSceneToFile("res://scenes/deck_builder.tscn");}
    public void Launch(bool create)
    {
        var deck=DeckStore.Selected;if(deck==null||deck.Validate()!="")return;
        if(!create&&(_code.Text.Length!=6||!_code.Text.All(char.IsDigit)))return;
        BattleLaunch.Matchmaking=true;BattleLaunch.Deck=deck.Clone();BattleLaunch.Create=create;BattleLaunch.Code=_code.Text;BattleLaunch.Pending=true;
        GetTree().ChangeSceneToFile("res://scenes/main.tscn");
    }
}
