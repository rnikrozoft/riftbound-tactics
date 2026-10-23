using Godot;
using System;
using System.Linq;
using System.Collections.Generic;

public partial class Lobby : Control
{
    private bool _closed,_savingDeck;
    private PixelLoadingIndicator _dataLoader=null!;
    private CollectionShopPage _collectionPage=null!;
    private TextureButton _login=null!;
    public string ActiveMenu {get;private set;}="Play";
    private readonly Dictionary<string,TextureButton> _navigation=new();
    private VBoxContainer _lobbyPage=null!,_futurePage=null!,_roomPanel=null!;
    private Label _futureTitle=null!;
    private LeaderboardPage _leaderboard=null!;
    private ReplayHistoryPage _replays=null!;
    public static string InitialMenu {get;set;}="Play";
    private GridContainer _decks=null!;
    private VBoxContainer _preview=null!;
    private Label _status=null!,_headerCoins=null!,_headerGems=null!,_headerName=null!;
    private LineEdit _code=null!;
    private TextureButton _create=null!,_join=null!,_edit=null!,_delete=null!;
    public override void _Ready()
    {
        GamePreferences.Load();DeckStore.Load();BattleLaunch.Pending=false;BattleLaunch.Deck=null;
        var page=DeckMenuUi.Page(this);
        _dataLoader=new PixelLoadingIndicator {Name="LobbyDataLoadingSpinner",Visible=false,AnchorLeft=.5f,AnchorRight=.5f,OffsetLeft=-20,OffsetRight=20,OffsetTop=12,OffsetBottom=52};AddChild(_dataLoader);
        var brand=new HBoxContainer();brand.AddThemeConstantOverride("separation",12);page.AddChild(brand);
        var settings=DeckMenuUi.Button(GamePreferences.Text("Settings","ตั้งค่า"),OpenSettings,110,42);settings.Name="SettingsButton";brand.AddChild(settings);
        var account=new HBoxContainer {Name="PlayerAccountHeader",SizeFlagsVertical=SizeFlags.ShrinkCenter};account.AddThemeConstantOverride("separation",12);brand.AddChild(account);
        var accountInfo=new VBoxContainer {CustomMinimumSize=new(156,0),SizeFlagsVertical=SizeFlags.ShrinkCenter};accountInfo.AddThemeConstantOverride("separation",2);account.AddChild(accountInfo);
        _headerName=DeckMenuUi.Text(string.IsNullOrWhiteSpace(GameAccount.DisplayName)?"Player":GameAccount.DisplayName,14);_headerName.Name="LobbyPlayerName";_headerName.AutoTranslateMode=AutoTranslateModeEnum.Disabled;_headerName.ClipText=true;_headerName.TextOverrunBehavior=TextServer.OverrunBehavior.TrimEllipsis;_headerName.HorizontalAlignment=HorizontalAlignment.Left;accountInfo.AddChild(_headerName);
        _headerCoins=AccountCurrency(accountInfo,"COINS",PlayerInventory.Coins,"LobbyWalletCoins",GameUi.Gold);
        _headerGems=AccountCurrency(accountInfo,"DIAMONDS",PlayerInventory.State?.Wallet.GetValueOrDefault("gems")??0,"LobbyWalletGems",new Color("6bddc5"));
        var avatar=new PlayerAvatar {Name="LobbyProfileAvatar",CustomMinimumSize=new(72,72)};avatar.Bind(GameAccount.Session?.UserId??"");account.AddChild(avatar);account.MoveChild(avatar,0);
        brand.MoveChild(account,0);var headerSpacer=new Control {SizeFlagsHorizontal=SizeFlags.ExpandFill};brand.AddChild(headerSpacer);brand.MoveChild(headerSpacer,1);
        var navScroll=new ScrollContainer {HorizontalScrollMode=ScrollContainer.ScrollMode.Auto,VerticalScrollMode=ScrollContainer.ScrollMode.Disabled,CustomMinimumSize=new(0,48)};page.AddChild(navScroll);
        var navigation=new HBoxContainer();navigation.AddThemeConstantOverride("separation",8);navScroll.AddChild(navigation);
        var menus=new[]{"Play","Collection","Shop","Leaderboard","Replays","Achievements"};
        foreach(string menu in menus){
            string destination=menu;var button=DeckMenuUi.Button(menu,()=>Navigate(destination),menu=="Achievements"?126:menu=="Leaderboard"?120:96,42);navigation.AddChild(button);_navigation[menu]=button;
        }
        var simulator=DeckMenuUi.Button("Combat Simulator",()=>{CombatSimulatorLab.ReturnScene="res://scenes/lobby.tscn";GetTree().ChangeSceneToFile("res://scenes/combat_simulator_lab.tscn");},168,42);simulator.Name="CombatSimulatorButton";navigation.AddChild(simulator);
        var shell=new HBoxContainer {SizeFlagsVertical=SizeFlags.ExpandFill};page.AddChild(shell);
        var content=new VBoxContainer {SizeFlagsHorizontal=SizeFlags.ExpandFill};shell.AddChild(content);
        _lobbyPage=new VBoxContainer {SizeFlagsVertical=SizeFlags.ExpandFill};_lobbyPage.AddThemeConstantOverride("separation",12);content.AddChild(_lobbyPage);
        var heading=new HBoxContainer {Name="PlaySubmenu"};_lobbyPage.AddChild(heading);
        var newDeck=DeckMenuUi.Button("+ New deck",()=>Edit(""),150);newDeck.Name="NewDeckButton";heading.AddChild(newDeck);
        var areaLabel=DeckMenuUi.Text(GamePreferences.Text("Battle area","พื้นที่ต่อสู้"),14);areaLabel.SizeFlagsVertical=SizeFlags.ShrinkCenter;heading.AddChild(areaLabel);
        var areaPicker=new OptionButton {Name="BattleAreaSelector",CustomMinimumSize=new(190,MobileLayout.Enabled?60:38)};
        areaPicker.AddItem(GamePreferences.Text("Living Garden","สวนมีชีวิต"));areaPicker.AddItem(GamePreferences.Text("Undead Hollow","หุบเขาอันเดด"));
        areaPicker.Selected=GamePreferences.BattleArea=="undead_hollow"?1:0;heading.AddChild(areaPicker);
        areaPicker.ItemSelected+=index=>GamePreferences.SetBattleArea(index==1?"undead_hollow":"living_garden");
        var areaPreview=DeckMenuUi.Button(GamePreferences.Text("Preview area","ดูพื้นที่"),()=>{if(GetNodeOrNull("BattleAreaPreviewModal")==null)AddChild(new BattleAreaPreviewModal {Name="BattleAreaPreviewModal"});},130);
        areaPreview.Name="PreviewBattleAreaButton";heading.AddChild(areaPreview);

        _leaderboard=new LeaderboardPage {Name="Rankings"};content.AddChild(_leaderboard);_leaderboard.Hide();
        _replays=new ReplayHistoryPage {Name="Replays"};content.AddChild(_replays);_replays.Hide();
        _collectionPage=new CollectionShopPage {Name="Collection",SizeFlagsVertical=SizeFlags.ExpandFill};content.AddChild(_collectionPage);_collectionPage.Hide();
        _futurePage=new VBoxContainer {SizeFlagsVertical=SizeFlags.ExpandFill};content.AddChild(_futurePage);_futurePage.Hide();
        var future=DeckMenuUi.Panel(_futurePage);future.GetParent<Control>().SizeFlagsHorizontal=SizeFlags.ExpandFill;
        _futureTitle=DeckMenuUi.Text("",28);future.AddChild(_futureTitle);
        future.AddChild(DeckMenuUi.Text("COMING SOON",20));
        future.AddChild(DeckMenuUi.Text("More ways to grow your collection are on the way.",16));
        future.AddChild(DeckMenuUi.Button("BACK TO PLAY",()=>Navigate("Play"),180));
        future.Alignment=BoxContainer.AlignmentMode.Center;
        foreach(var child in future.GetChildren().OfType<Control>()){child.SizeFlagsHorizontal=SizeFlags.ShrinkCenter;if(child is Label label)label.HorizontalAlignment=HorizontalAlignment.Center;}
        var body=new HBoxContainer {SizeFlagsVertical=SizeFlags.ExpandFill};body.AddThemeConstantOverride("separation",16);_lobbyPage.AddChild(body);
        var collection=new VBoxContainer {SizeFlagsHorizontal=SizeFlags.ExpandFill};body.AddChild(collection);
        var scroll=new ScrollContainer {SizeFlagsVertical=SizeFlags.ExpandFill,HorizontalScrollMode=ScrollContainer.ScrollMode.Disabled};collection.AddChild(scroll);
        _decks=new GridContainer {Columns=MobileLayout.Enabled?1:2,SizeFlagsHorizontal=SizeFlags.ExpandFill};_decks.AddThemeConstantOverride("h_separation",14);_decks.AddThemeConstantOverride("v_separation",14);scroll.AddChild(_decks);
        var side=DeckMenuUi.Panel(body,336);
        var analysisScroll=new ScrollContainer {Name="DeckAnalysisScroll",SizeFlagsVertical=SizeFlags.ExpandFill,HorizontalScrollMode=ScrollContainer.ScrollMode.Disabled};side.AddChild(analysisScroll);
        _preview=new VBoxContainer {SizeFlagsHorizontal=SizeFlags.ExpandFill};analysisScroll.AddChild(_preview);
        _edit=DeckMenuUi.Button("Edit deck",()=>Edit(DeckStore.SelectedId),304);side.AddChild(_edit);
        _delete=DeckMenuUi.Button("Delete deck",ConfirmDeleteDeck,304);_delete.Name="DeleteDeckButton";side.AddChild(_delete);
        side.RemoveChild(_edit);side.RemoveChild(_delete);var edits=new HBoxContainer();side.AddChild(edits);
        _edit.CustomMinimumSize=new(144,MobileLayout.Enabled?60:38);_delete.CustomMinimumSize=new(120,MobileLayout.Enabled?60:38);_edit.SizeFlagsHorizontal=SizeFlags.ExpandFill;_delete.SizeFlagsHorizontal=SizeFlags.ExpandFill;edits.AddChild(_edit);edits.AddChild(_delete);
        side.MoveChild(edits,side.GetChildCount()-1);
        _roomPanel=new VBoxContainer();side.AddChild(_roomPanel);
        _create=DeckMenuUi.Button("Find match",()=>Launch(true),304,48);_roomPanel.AddChild(_create);GameUi.Primary(_create);
        // Kept as private compatibility controls for older test entry points.
        _code=new LineEdit();_join=new TextureButton();_roomPanel.AddChild(_code);_roomPanel.AddChild(_join);_code.Hide();_join.Hide();
        _status=DeckMenuUi.Text(DeckStore.Error,14);page.AddChild(_status);Render();Navigate(InitialMenu);InitialMenu="Play";
        _login=DeckMenuUi.Button("LOGIN AGAIN",()=>{_ = BattleRecovery.Cancel();GameAccount.Session=null;GetTree().ChangeSceneToFile("res://scenes/welcome.tscn");},180);
        if(MobileLayout.Enabled&&_status.Text=="")_status.Hide();
        _status.GetParent().AddChild(_login);_login.Visible=GameAccount.RequiresLogin;
        if(BattleRecovery.Pending)Callable.From(RecoverConnection).CallDeferred();
        else if(BattleRecovery.Notice!="")_status.Text=BattleRecovery.Notice;
    }
    private void OpenSettings()
    {
        if(GetNodeOrNull<SettingsModal>("SettingsModal")!=null)return;
        var modal=new SettingsModal {Name="SettingsModal",MenuStyle=true,FacebookConnected=AvatarProfiles.Connected(GameAccount.Session?.UserId??"")};AddChild(modal);
        modal.LanguageChanged=()=>{
            var button=FindChild("SettingsButton",true,false) as TextureButton;if(button!=null)button.GetNode<Label>("Text").Text=GamePreferences.Text("Settings","ตั้งค่า");
        };
    }
    private static Label AccountCurrency(VBoxContainer parent,string title,long amount,string name,Color color)
    {
        var row=new HBoxContainer();parent.AddChild(row);
        var label=DeckMenuUi.Text(title,11);label.SizeFlagsHorizontal=SizeFlags.ExpandFill;row.AddChild(label);
        var value=DeckMenuUi.Text(amount.ToString("N0"),14);value.Name=name;value.AddThemeColorOverride("font_color",color);row.AddChild(value);return value;
    }
    public override void _Process(double delta)
    {
        if(_dataLoader!=null)_dataLoader.Visible=_savingDeck;
        if(MobileLayout.Enabled&&_status!=null)_status.Visible=_status.Text!="";
        if(_headerCoins!=null){string coins=PlayerInventory.Coins.ToString("N0");if(_headerCoins.Text!=coins)_headerCoins.Text=coins;}
        if(_headerGems!=null)_headerGems.Text=(PlayerInventory.State?.Wallet.GetValueOrDefault("gems")??0).ToString("N0");
        if(_headerName!=null)_headerName.Text=string.IsNullOrWhiteSpace(GameAccount.DisplayName)?"Player":GameAccount.DisplayName;
    }
    private async void RecoverConnection()
    {
        var connection=BattleRecovery.Connection!;
        _create.Disabled=true;
        int attempt=0;
        while(!_closed&&ReferenceEquals(connection,BattleRecovery.Connection)){
            _status.Text=$"Connection lost / Reconnecting ({++attempt})...";
            try{
                await connection.Reconnect();if(_closed)return;
                if(connection.MatchId==""){await BattleRecovery.Cancel();_status.Text="Connected / Your match has ended.";_create.Disabled=false;return;}
                try{await connection.Rejoin(BattleRecovery.Deck);}catch(System.Net.WebSockets.WebSocketException e) when(e.Message=="Match not found" || e.Message=="Player eliminated; this match cannot be rejoined" || e.Message=="Not reserved for this match"){
                    await BattleRecovery.Cancel();_status.Text="Connected / Your previous match is no longer available.";_create.Disabled=false;return;
                }
                // Wait for an authoritative snapshot before passing the socket to gameplay.
                ulong until=Time.GetTicksMsec()+8000;
                while(BattleRecovery.Snapshots.IsEmpty&&Time.GetTicksMsec()<until&&!_closed)await ToSignal(GetTree(),SceneTree.SignalName.ProcessFrame);
                if(_closed)return;
                if(BattleRecovery.Snapshots.IsEmpty)throw new System.TimeoutException();
                BattleRecovery.Ready=true;GetTree().ChangeSceneToFile(BattleAreas.ScenePath);return;
            }catch(Nakama.ApiResponseException e) when(e.StatusCode==401||e.StatusCode==403){
                SessionGuard.NotifyReplaced(connection.Session);return;
            }catch(System.Exception){if(_closed)return;}
            await ToSignal(GetTree().CreateTimer(System.Math.Min(5,attempt+1)),SceneTreeTimer.SignalName.Timeout);
        }
    }
    public override void _ExitTree(){_closed=true;if(BattleRecovery.Pending&&!BattleRecovery.Ready)_ = BattleRecovery.Cancel();}
    public void Navigate(string menu)
    {
        if(menu=="Decks")menu="Play";
        if(!_navigation.ContainsKey(menu))return;
        ActiveMenu=menu;bool lobby=menu=="Play";
        _lobbyPage.Visible=lobby;_futurePage.Visible=!lobby&&menu!="Leaderboard"&&menu!="Replays"&&menu!="Shop"&&menu!="Collection";_leaderboard.Visible=menu=="Leaderboard";
        _replays.Visible=menu=="Replays";if(menu=="Replays")_replays.Open();
        _collectionPage.Visible=menu is "Shop" or "Collection";if(_collectionPage.Visible)_collectionPage.Open(menu=="Shop");
        if(menu=="Leaderboard")_leaderboard.Open();_roomPanel.Visible=true;
        _futureTitle.Text=menu.ToUpperInvariant();
        foreach(var entry in _navigation)GameUi.Selected(entry.Value,entry.Key==menu);
    }
    public async void SelectDeck(string id)
    {
        if(!DeckStore.Decks.Any(d=>d.Id==id))return;
        if(_savingDeck)return;_savingDeck=true;RefreshControls();
        try {if(GameAccount.Session!=null)await PlayerInventory.SaveDecks(DeckStore.Decks.Select(d=>d.Clone()).ToList(),id);else{DeckStore.SelectedId=id;DeckStore.Save();}if(!_closed){_status.Text=DeckStore.Error;Render();}}
        catch(System.Exception e){if(!_closed)_status.Text="Could not select deck: "+e.Message;}
        finally{_savingDeck=false;if(!_closed)RefreshControls();}
    }
    private void ConfirmDeleteDeck()
    {
        var selected=DeckStore.Selected;if(selected==null||_savingDeck)return;
        string id=selected.Id;
        var layer=new CanvasLayer {Layer=30};AddChild(layer);var modal=new GameModal {MenuStyle=true};layer.AddChild(modal);
        modal.Body.AddChild(DeckMenuUi.Text("Delete deck",24));
        var message=DeckMenuUi.Text($"Delete {selected.Name}?\nYour characters will remain in your collection.",16);message.AutowrapMode=TextServer.AutowrapMode.WordSmart;modal.Body.AddChild(message);
        var row=new HBoxContainer();modal.Body.AddChild(row);
        row.AddChild(DeckMenuUi.Button("CANCEL",()=>layer.QueueFree(),156));modal.Cancel=()=>layer.QueueFree();
        row.AddChild(DeckMenuUi.Button("DELETE",()=>{layer.QueueFree();DeleteDeck(id);},156));
    }
    public async void DeleteDeck(string id)
    {
        if(_savingDeck||!DeckStore.Decks.Any(d=>d.Id==id))return;
        _savingDeck=true;RefreshControls();
        var decks=DeckStore.Decks.Where(d=>d.Id!=id).Select(d=>d.Clone()).ToList();
        string selected=DeckStore.SelectedId==id?(decks.FirstOrDefault()?.Id??""):DeckStore.SelectedId;
        try {
            if(GameAccount.Session!=null)await PlayerInventory.SaveDecks(decks,selected);
            else {var previous=DeckStore.Decks.ToList();string previousId=DeckStore.SelectedId;DeckStore.Decks.Clear();DeckStore.Decks.AddRange(decks);DeckStore.SelectedId=selected;if(!DeckStore.Save()){DeckStore.Decks.Clear();DeckStore.Decks.AddRange(previous);DeckStore.SelectedId=previousId;throw new Exception(DeckStore.Error);}}
            if(!_closed){_status.Text="Deck deleted.";Render();}
        }catch(Exception e){if(!_closed){_status.Text="Could not delete deck: "+e.Message;Render();}}
        finally {_savingDeck=false;if(!_closed)RefreshControls();}
    }
    private static HBoxContainer Heroes(DeckDefinition deck,int width,int height)
    {
        var row=new HBoxContainer {Name="DeckGroupColumns"};row.AddThemeConstantOverride("separation",6);
        foreach(int group in deck.Heroes)
        {
            var hero=new VBoxContainer {CustomMinimumSize=new(width,0),SizeFlagsHorizontal=SizeFlags.ExpandFill};row.AddChild(hero);
            var art=DeckMenuUi.Art(CardCatalog.Options(group,2).First(),width,height);
            art.SizeFlagsVertical=SizeFlags.ShrinkBegin;art.GetNode<Control>("ClassEmblem").Hide();hero.AddChild(art);
            var name=new ScrollingDeckName(CardCatalog.Groups[group],12) {Name="DeckGroupName",CenterShortText=true};
            hero.AddChild(name);
        }
        var neutral=new VBoxContainer {Name="NeutralDeckColumn",CustomMinimumSize=new(width,0),SizeFlagsHorizontal=SizeFlags.ExpandFill};row.AddChild(neutral);
        if(CharacterData.SupportKind>=0){var art=DeckMenuUi.Art(CharacterData.SupportKind,width,height);art.GetNode<Control>("ClassEmblem").Hide();neutral.AddChild(art);neutral.AddChild(new ScrollingDeckName("Neutral",12){CenterShortText=true});}
        return row;
    }
    private void Render()
    {
        var newDeck=FindChild("NewDeckButton",true,false) as BaseButton;if(newDeck!=null)newDeck.Disabled=DeckStore.Decks.Count>=5;
        DeckMenuUi.Clear(_decks);
        foreach(var deck in DeckStore.Decks)
        {
            var captured=deck;bool selected=deck.Id==DeckStore.SelectedId;
            var button=DeckMenuUi.Button("",()=>SelectDeck(captured.Id),378,232);button.SizeFlagsHorizontal=SizeFlags.ExpandFill;_decks.AddChild(button);
            GameUi.Selected(button,selected);
            var box=new VBoxContainer {MouseFilter=MouseFilterEnum.Ignore};box.AddThemeConstantOverride("separation",16);button.AddChild(box);box.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);box.OffsetLeft=12;box.OffsetRight=-12;box.OffsetTop=12;box.OffsetBottom=-12;
            var heading=new HBoxContainer {Name="DeckHeading"};heading.AddThemeConstantOverride("separation",10);box.AddChild(heading);
            var name=new ScrollingDeckName(deck.Name,18) {Name="DeckName",TranslateContent=false};heading.AddChild(name);
            var count=DeckMenuUi.Text($"{deck.Cards.Count}/30",14);count.SizeFlagsVertical=SizeFlags.ShrinkCenter;heading.AddChild(count);
            box.AddChild(Heroes(deck,84,124));
        }
        for(int slot=DeckStore.Decks.Count;slot<6;slot++)
        {
            bool locked=slot==5;
            var placeholder=DeckMenuUi.Button(locked?"◇  30 diamonds":"+",()=>Edit(""),378,232);
            placeholder.Name="DeckSlot_"+(slot+1);
            placeholder.SizeFlagsHorizontal=SizeFlags.ExpandFill;
            placeholder.Disabled=locked;
            placeholder.GetNode<Label>("Text").AddThemeFontSizeOverride("font_size",locked?20:36);
            _decks.AddChild(placeholder);
        }
        DeckMenuUi.Clear(_preview);var chosen=DeckStore.Selected;
        if(chosen!=null)
        {
            var identity=new HBoxContainer {Name="SelectedDeckHeader"};identity.AddThemeConstantOverride("separation",12);_preview.AddChild(identity);
            var info=new VBoxContainer {SizeFlagsHorizontal=SizeFlags.ExpandFill};identity.AddChild(info);info.AddChild(MenuUi.Eyebrow("SELECTED DECK"));
            var name=new ScrollingDeckName(chosen.Name,20) {Name="SelectedDeckName",TranslateContent=false};info.AddChild(name);
            var back=CardBackCatalog.Preview(chosen.CardBackId,66,98);back.Name="SelectedDeckCardBack";back.SizeFlagsHorizontal=SizeFlags.ShrinkEnd;back.SizeFlagsVertical=SizeFlags.ShrinkBegin;identity.AddChild(back);
            _preview.AddChild(new DeckHistoryChart(chosen) {Name="DeckHistoryChart"});
        }
        RefreshControls();
    }
    private void RefreshControls()
    {
        var deck=DeckStore.Selected;bool valid=deck!=null&&deck.Validate()==""&&deck.Cards.All(c=>PlayerInventory.CanUse(c.Kind))&&PlayerInventory.OwnsCardBack(deck.CardBackId);
        _create.Disabled=!valid||_savingDeck||BattleRecovery.Pending||GameAccount.RequiresLogin;_join.Disabled=!valid||_code.Text.Length!=6||!_code.Text.All(char.IsDigit);_edit.Disabled=deck==null||_savingDeck;_delete.Disabled=deck==null||_savingDeck;
    }
    private void Edit(string id){if(id==""&&DeckStore.Decks.Count>=5)return;DeckStore.EditId=id;GetTree().ChangeSceneToFile("res://scenes/deck_builder.tscn");}
    public async void Launch(bool create)
    {
        if(_savingDeck||BattleRecovery.Pending||GameAccount.RequiresLogin)return;
        var deck=DeckStore.Selected;if(deck==null||deck.Validate()!=""||deck.Cards.Any(c=>!PlayerInventory.CanUse(c.Kind)))return;
        if(!create&&(_code.Text.Length!=6||!_code.Text.All(char.IsDigit)))return;
        // Recheck the active config before constructing gameplay resources after a server restart.
        if(GameAccount.Session!=null){
            _savingDeck=true;RefreshControls();NakamaConnection? c=null;
            try {c=GameAccount.Connection();await c.Connect(realtime:false);await PlayerInventory.Load(c);if(_closed)return;deck=DeckStore.Selected;if(deck==null||deck.Validate()!=""||deck.Cards.Any(card=>!PlayerInventory.CanUse(card.Kind))){_status.Text="Update your deck with characters available in your collection.";Render();return;}}
            catch(Nakama.ApiResponseException e) when(e.StatusCode==401||e.StatusCode==403){GameAccount.RequiresLogin=true;if(!_closed){_status.Text="Your session ended. Login again to continue.";_login.Show();}return;}
            catch(System.Exception e){if(!_closed)_status.Text="Could not load your account: "+e.Message;return;}
            finally{if(c!=null)await c.Close();_savingDeck=false;if(!_closed)RefreshControls();}
        }
        BattleLaunch.Matchmaking=true;BattleLaunch.Deck=deck.Clone();BattleLaunch.Create=create;BattleLaunch.Code=_code.Text;BattleLaunch.Pending=true;
        GetTree().ChangeSceneToFile(BattleAreas.ScenePath);
    }
}
