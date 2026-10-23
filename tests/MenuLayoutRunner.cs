using Godot;
using System;
using System.Linq;
using System.Collections.Generic;
using System.Threading.Tasks;

public partial class MenuLayoutRunner : Node
{
    private async Task Frames(int count=8){for(int i=0;i<count;i++)await ToSignal(GetTree(),SceneTree.SignalName.ProcessFrame);}
    private async Task Shot(string name){await Frames();if(DisplayServer.GetName()=="headless")return;await ToSignal(RenderingServer.Singleton,RenderingServer.SignalName.FramePostDraw);GetViewport().GetTexture().GetImage().SavePng("res://output/menu-"+name+".png");}
    private void Check(bool pass,string message){if(!pass)throw new Exception(message);}
    public override void _Ready()=>Callable.From(Run).CallDeferred();
    private async void Run()
    {
        try {
            NakamaConnection? connection=null;
            if(OS.GetEnvironment("RIFT_MENU_LIVE_TEST")=="1"){
                connection=new NakamaConnection(GameAccount.ServerHost);await connection.Connect(realtime:false);
                GameAccount.Session=connection.Session;await PlayerInventory.Load(connection,forceConfiguration:true);Check(LocalizationCache.CurrentVersion.Length==64,"Live localization bootstrap did not return a versioned document");
                var firstRpc=await connection.Client.RpcAsync(connection.Session,"localization_bootstrap","{}");
                var firstDoc=System.Text.Json.JsonSerializer.Deserialize(firstRpc.Payload,GameJsonContext.Default.LocalizationResponse)!;
                Check(firstDoc.Document!=null&&firstDoc.Document.Messages.All(p=>p.Length==4),"First localization download must contain source and editable languages");
                var refreshRpc=await connection.Client.RpcAsync(connection.Session,"localization_bootstrap",System.Text.Json.JsonSerializer.Serialize(new LocalizationRequest {CachedVersion=LocalizationCache.CurrentVersion},GameJsonContext.Default.LocalizationRequest));
                var refresh=System.Text.Json.JsonSerializer.Deserialize(refreshRpc.Payload,GameJsonContext.Default.LocalizationResponse)!;
                Check(refresh.Document==null&&refresh.Version==firstDoc.Version,"Unchanged localization must return version only");
                string origin=connection.Client.Scheme+"://"+connection.Client.Host+":"+connection.Client.Port;
                Check(LocalizationCache.Read(origin)?.Version==firstDoc.Version,"Downloaded catalog was not cached on disk");
                GD.Print("LOCALIZATION LIVE PASS: database catalog, full download, version-only refresh and disk cache");
            }
            var decks=new List<DeckDefinition>();
            foreach(string name in new[]{"Vanguard expedition","Ash and iron","The night watch"}){var d=DeckDefinition.Starter();d.Id=Guid.NewGuid().ToString("N");d.Name=name;decks.Add(d);}
            DeckStore.ApplyRemote("",decks,decks[0].Id);
            var scrollingName=new ScrollingDeckName("A very long deck name that must stay on a single line",18) {Size=new Vector2(140,28)};AddChild(scrollingName);
            await Frames();
            var scrollingText=scrollingName.GetChild<Label>(0);
            Check(scrollingName.ClipContents&&scrollingText.AutowrapMode==TextServer.AutowrapMode.Off,"Deck name must clip without wrapping");
            scrollingName._Process(2.5);
            Check(scrollingText.Position.X<0,"Long deck name did not scroll");scrollingName.Free();
            var welcome=GD.Load<PackedScene>("res://scenes/welcome.tscn").Instantiate<Welcome>();AddChild(welcome);await Shot("welcome");
            Check(welcome.FindChild("OfflineCombatSimulatorButton",true,false) is TextureButton,"Offline simulator missing");welcome.Free();
            var lobby=GD.Load<PackedScene>("res://scenes/lobby.tscn").Instantiate<Lobby>();AddChild(lobby);await Shot("lobby");
            Check(lobby.FindChild("DeckHistoryChart",true,false)!=null&&lobby.FindChild("DeckMatchupChart",true,false)==null,"Real match history must replace simulations");
            lobby.FindChild("SettingsButton",true,false).EmitSignal(BaseButton.SignalName.Pressed);await Frames();
            var settings=(SettingsModal)lobby.FindChild("SettingsModal",true,false);
            Check(settings!=null,"Settings button did not open modal");
            Check(((BaseButton)settings!.FindChild("ConnectFacebookButton",true,false)).Disabled,"Unimplemented Facebook connection must stay disabled");
            Check(((BaseButton)settings!.FindChild("DeleteAccountButton",true,false)).Disabled,"Unimplemented account deletion must stay disabled");
            Check(settings!.FindChild("SettingsLanguage",true,false) is OptionButton&&settings!.FindChild("SettingsEffectsVolume",true,false) is HSlider&&settings!.FindChild("SettingsMusicVolume",true,false) is HSlider,"Settings controls missing");
            await Shot("settings");
            string savedLanguage=GamePreferences.Language;
            try{
                settings.FindChild("SettingsLanguage",true,false).EmitSignal(OptionButton.SignalName.ItemSelected,1);await Frames(12);Localization.Refresh(GetTree().Root);await Shot("settings-th");
                settings.Free();lobby.Navigate("Play");await Frames(12);Localization.Refresh(GetTree().Root);await Shot("play-th");
                lobby.Navigate("Collection");await Frames(12);Localization.Refresh(GetTree().Root);await Shot("collection-th");
                GamePreferences.SetLanguage("en");await Frames(12);Localization.Refresh(GetTree().Root);await Shot("collection-en");
            }finally{GamePreferences.SetLanguage(savedLanguage);}

            foreach(string menu in new[]{"Play","Collection","Shop","Replays","Achievements"}){lobby.Navigate(menu);await Shot(menu.ToLowerInvariant());}
            lobby.Navigate("Collection");await Frames();
            var collection=(CollectionShopPage)lobby.FindChild("Collection",true,false);
            collection.FindChild("ShopCharactersTab",true,false).EmitSignal(BaseButton.SignalName.Pressed);await Frames();
            var heading=(Control)collection.FindChild("CollectionHeading",true,false);var submenu=(Control)collection.FindChild("CollectionSubmenu",true,false);
            Check(submenu.GetGlobalRect().End.Y<=heading.GetGlobalRect().Position.Y,"Collection submenu must be above heading");
            Check(collection.FindChild("CollectionOwnedCount",true,false).GetParent()==heading,"Owned count must be beside Collection");
            Check(!collection.FindChildren("*","Label",true,false).OfType<Label>().Any(l=>l.Text=="Refresh"||l.Text.Contains("WALLET")||l.Text.Contains("All 40 character cards")||l.Text.Contains("10/10 OWNED")),"Removed collection text remains");
            Check(collection.SelectedKind>=0&&((Control)collection.FindChild("CollectionDetails",true,false)).Visible,"Collection must select the first card by default");
            Check(!collection.FindChildren("*","Label",true,false).OfType<Label>().Any(l=>l.Text=="CARD DETAILS"||l.Text=="×"),"Collection detail header/close button remains");
            var tile=(Control)collection.FindChildren("*","PanelContainer",true,false).Where(c=>c.HasMeta("kind")).Skip(1).First();int selected=tile.GetMeta("kind").AsInt32();
            foreach(var art in collection.FindChildren("CollectionArtwork","TextureRect",true,false).OfType<TextureRect>())
                Check(art.Size.IsEqualApprox(ShopCard.ShopDisplaySize),"Collection artwork must match gameplay shop card size");
            var click=tile.GetGlobalRect().GetCenter();
            GetViewport().PushInput(new InputEventMouseButton {Position=click,GlobalPosition=click,Pressed=true,ButtonIndex=MouseButton.Left});await Frames();
            GetViewport().PushInput(new InputEventMouseButton {Position=click,GlobalPosition=click,Pressed=false,ButtonIndex=MouseButton.Left});await Frames();
            Check(collection.SelectedKind==selected&&((Control)collection.FindChild("CollectionDetails",true,false)).Visible,"Click did not open character details");
            Check(((Label)collection.FindChild("CollectionDetailName",true,false)).Text==CharacterData.Get(selected).Name,"Wrong details character");
            collection.SetDetailStars(2);Check(collection.DetailStars==2,"Details star selection failed");
            Check(((Label)collection.FindChild("CollectionDetailAbilities",true,false)).Text==CharacterData.CombatDescription(selected,2),"Details ability does not reflect selected stars");
            await Frames();
            Check(((Control)collection.FindChild("CollectionDetails",true,false)).GetGlobalRect().End.X<=GetViewport().GetVisibleRect().Size.X+1,"Collection details overflow the screen");
            await Shot("collection-details");
            if(connection!=null){
                lobby.Navigate("Leaderboard");await Frames();
                var rankings=lobby.FindChild("Rankings",true,false) as LeaderboardPage;
                ulong until=Time.GetTicksMsec()+10000;
                while(rankings!.Loading&&Time.GetTicksMsec()<until)await Frames();
                Check(!rankings.Loading,"Rankings request timed out");
                Check(rankings.TopRecordCount<=100&&rankings.RecordCount==rankings.TopRecordCount+1&&rankings.FindChild("OwnRankingRow",true,false)!=null,"Rankings must show top 100 plus self");
                Check(!rankings.FindChildren("*","TextureButton",true,false).Any(),"Rankings pagination/refresh remains");await Shot("rankings");
            }
            lobby.Free();
            DeckStore.EditId=decks[0].Id;
            var editor=GD.Load<PackedScene>("res://scenes/deck_builder.tscn").Instantiate<DeckBuilder>();AddChild(editor);await Shot("deck-builder");
            editor.SelectCard(CardCatalog.Options(editor.ActiveGroup,3).First());await Shot("card-details");
            Check(editor.Draft.Validate()=="","Deck invalid after layout");
            var kind=editor.Draft.Cards.First(c=>CardCatalog.Group(c.Kind)==editor.ActiveGroup).Kind;
            editor.RemoveCard(kind);Check(!editor.Draft.Cards.Any(c=>c.Kind==kind),"Could not remove a deck card");
            editor.BeginCardDrag(kind);Check(editor.FindChildren("*","TabContainer",true,false).OfType<TabContainer>().Single().CurrentTab==0,"Dragging must reveal deck drop targets");
            var empty=editor.FindChildren("*","TextureButton",true,false).OfType<DeckEmptyDrop>().First(c=>c.Group==editor.ActiveGroup&&c.Cost==CardCatalog.Cost(kind));
            Check(empty._CanDropData(Vector2.Zero,kind),"Card drag rejected by empty slot");empty._DropData(Vector2.Zero,kind);
            Check(editor.Draft.Validate()=="","Drag and drop left an invalid deck");
            editor.FindChild("ChooseCardBackButton",true,false).EmitSignal(BaseButton.SignalName.Pressed);await Shot("card-back-picker");
            var first=editor.FindChildren("*","TextureButton",true,false).OfType<DeckCatalogCard>().First();
            first.EmitSignal(BaseButton.SignalName.Pressed);Check(first.HasMeta("custom_menu"),"Catalog uses old UI skin");editor.Free();
            var lab=GD.Load<PackedScene>("res://scenes/combat_simulator_lab.tscn").Instantiate<CombatSimulatorLab>();AddChild(lab);await Shot("simulator");lab.Free();
            var battle=DeckMenuUi.GameplayButton("Battle action",()=>{});Check(battle.HasMeta("custom_menu")&&battle.GetNodeOrNull<MenuButtonSkin>("MenuSkin")!=null,"Gameplay button is not custom styled");battle.Free();
            if(connection!=null){await connection.Close();GameAccount.Session=null;}
            GD.Print("MENU LAYOUT PASS: all menu pages load, deck remains valid, custom menu and gameplay skins");GetTree().Quit();
        }catch(Exception e){GD.PushError(e.ToString());GetTree().Quit(1);}
    }
}
