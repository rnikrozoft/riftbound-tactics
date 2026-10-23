using Godot;
using System;
using System.Linq;

public partial class DeckBuilder : Control
{
    public DeckDefinition Draft {get;private set;}=null!;
    public int ActiveGroup {get;private set;}
    public int CostFilter {get;private set;}
    public int VisibleCardCount {get;private set;}
    private bool _saving;
    private int _selectedKind=-1,_heroIndex;
    private HBoxContainer _tabs=null!,_filters=null!;
    private GridContainer _catalog=null!;
    private VBoxContainer _details=null!,_list=null!,_summary=null!;
    private Control _heroChoices=null!;
    private Label _status=null!,_total=null!;
    private LineEdit _search=null!;
    private TextureButton _save=null!;
    private TabContainer _inspectorTabs=null!;
    private TextureRect _cardBackPreview=null!;
    private Label _cardBackName=null!;
    public override void _Ready()
    {
        DeckStore.Load();Draft=(DeckStore.Decks.Find(d=>d.Id==DeckStore.EditId)??DeckDefinition.Starter()).Clone();
        if(string.IsNullOrEmpty(DeckStore.EditId)){Draft.Id=Guid.NewGuid().ToString("N");Draft.Name="New expedition";}
        ActiveGroup=Draft.Heroes[0];
        var page=DeckMenuUi.Page(this);
        var header=new HBoxContainer();page.AddChild(header);
        header.AddChild(DeckMenuUi.Button("← Back",()=>GetTree().ChangeSceneToFile("res://scenes/lobby.tscn"),120));
        var name=DeckMenuUi.Input(Draft.Name,"Deck name",360);name.SizeFlagsHorizontal=SizeFlags.ExpandFill;header.AddChild(name);
        name.TextChanged+=text=>{Draft.Name=text;RefreshStatus();};
        _save=DeckMenuUi.Button("Save deck",Save,150);header.AddChild(_save);GameUi.Primary(_save);
        var backRow=new HBoxContainer {Name="DeckCardBack"};page.AddChild(backRow);
        _cardBackPreview=CardBackCatalog.Preview(Draft.CardBackId,24,32);backRow.AddChild(_cardBackPreview);
        _cardBackName=DeckMenuUi.Text("",14);_cardBackName.SizeFlagsHorizontal=SizeFlags.ExpandFill;backRow.AddChild(_cardBackName);
        var chooseBack=DeckMenuUi.Button("Change card back",ShowCardBackChoices,200);chooseBack.Name="ChooseCardBackButton";backRow.AddChild(chooseBack);
        _tabs=new HBoxContainer();page.AddChild(_tabs);
        _heroChoices=new Control {ZIndex=100,MouseFilter=MouseFilterEnum.Stop};AddChild(_heroChoices);_heroChoices.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);_heroChoices.Hide();
        var body=new HBoxContainer {SizeFlagsVertical=SizeFlags.ExpandFill};body.AddThemeConstantOverride("separation",12);page.AddChild(body);
        var center=new VBoxContainer {SizeFlagsHorizontal=SizeFlags.ExpandFill};body.AddChild(center);
        Container tools=MobileLayout.Enabled?new VBoxContainer():new HBoxContainer();center.AddChild(tools);
        _search=DeckMenuUi.Input("","Search your cards",180);_search.SizeFlagsHorizontal=SizeFlags.ExpandFill;tools.AddChild(_search);_search.TextChanged+=_=>RenderCatalog();
        _filters=new HBoxContainer();tools.AddChild(_filters);
        var scroll=new ScrollContainer {SizeFlagsVertical=SizeFlags.ExpandFill,SizeFlagsHorizontal=SizeFlags.ExpandFill,HorizontalScrollMode=ScrollContainer.ScrollMode.Disabled};center.AddChild(scroll);
        _catalog=new GridContainer {Columns=MobileLayout.Enabled?2:4,SizeFlagsHorizontal=SizeFlags.ExpandFill};_catalog.AddThemeConstantOverride("h_separation",12);_catalog.AddThemeConstantOverride("v_separation",12);scroll.AddChild(_catalog);
        void FitColumns(){int columns=Math.Max(1,(int)((scroll.Size.X-16)/178));if(_catalog.Columns!=columns)_catalog.Columns=columns;}
        scroll.Resized+=FitColumns;
        var right=DeckMenuUi.Panel(body,MobileLayout.Enabled?280:320);right.AddChild(MenuUi.Eyebrow("DECK WORKSPACE"));
        _total=DeckMenuUi.Text("",15);right.AddChild(_total);
        _inspectorTabs=new TabContainer {SizeFlagsVertical=SizeFlags.ExpandFill};right.AddChild(_inspectorTabs);
        var deckTab=new VBoxContainer {Name="Deck"};_inspectorTabs.AddChild(deckTab);
        _summary=new VBoxContainer();deckTab.AddChild(_summary);
        var listScroll=new ScrollContainer {SizeFlagsVertical=SizeFlags.ExpandFill,HorizontalScrollMode=ScrollContainer.ScrollMode.Disabled};deckTab.AddChild(listScroll);
        _list=new VBoxContainer {SizeFlagsHorizontal=SizeFlags.ExpandFill};listScroll.AddChild(_list);
        var detailsScroll=new ScrollContainer {Name="Card details",SizeFlagsVertical=SizeFlags.ExpandFill,HorizontalScrollMode=ScrollContainer.ScrollMode.Disabled};_inspectorTabs.AddChild(detailsScroll);
        _details=new VBoxContainer {SizeFlagsHorizontal=SizeFlags.ExpandFill};detailsScroll.AddChild(_details);
        _status=DeckMenuUi.Text("",14);page.AddChild(_status);
        SelectCard(CardCatalog.Options(ActiveGroup,2).First());Render();_inspectorTabs.CurrentTab=0;
    }
    public void SetHero(int index,int group)
    {
        if(index<0||index>=3||!CardCatalog.HeroGroups.Contains(group)||Draft.Heroes.Where((h,i)=>i!=index).Contains(group)||Enumerable.Range(2,5).Any(cost=>CardCatalog.Options(group,cost).Count(PlayerInventory.CanUse)<2))return;
        int previous=Draft.Heroes[index];Draft.Cards.RemoveAll(c=>CardCatalog.Group(c.Kind)==previous);Draft.Heroes[index]=group;
        for(int cost=2;cost<=6;cost++)foreach(int kind in CardCatalog.Options(group,cost).Where(PlayerInventory.CanUse).Take(2))Draft.Cards.Add(new(){Kind=kind,Copies=CharacterData.Get(kind).MaxCopies});
        ActiveGroup=group;_heroChoices.Hide();SelectCard(CardCatalog.Options(group,2).First());Render();
    }
    public void SetGroup(int group)
    {
        if(group!=4&&!Draft.Heroes.Contains(group))return;
        ActiveGroup=group;CostFilter=0;_heroChoices.Hide();SelectCard(group==4?CharacterData.SupportKinds.First():CardCatalog.Options(group,2).First());Render();
    }
    public void Filter(int cost,string search=""){CostFilter=cost;_search.Text=search;RenderFilters();RenderCatalog();}
    private void Render()
    {
        _cardBackPreview.Texture=CardBackCatalog.Texture(Draft.CardBackId);
        _cardBackName.Text="CARD BACK / "+CardBackCatalog.Find(Draft.CardBackId)?.Name+"  · Applies to all cards";
        DeckMenuUi.Clear(_tabs);
        for(int i=0;i<3;i++)
        {
            int index=i,group=Draft.Heroes[i];
            var tab=DeckMenuUi.Button(CardCatalog.Groups[group],()=>{_heroIndex=index;SetGroup(group);},170,38);_tabs.AddChild(tab);GameUi.Selected(tab,group==ActiveGroup);
        }
        var neutral=DeckMenuUi.Button("Neutral",()=>SetGroup(4),140,38);neutral.Name="NeutralGroupTab";_tabs.AddChild(neutral);GameUi.Selected(neutral,ActiveGroup==4);
        if(ActiveGroup!=4)_tabs.AddChild(DeckMenuUi.Button("CHANGE GROUP",ShowHeroChoices,180));
        RenderFilters();RenderCatalog();RenderDeck();RenderDetails();RefreshStatus();
    }
    private void ShowHeroChoices()
    {
        _heroIndex=Array.IndexOf(Draft.Heroes,ActiveGroup);DeckMenuUi.Clear(_heroChoices);_heroChoices.Show();
        var shade=new ColorRect {Color=new Color(0,0,0,.8f),MouseFilter=MouseFilterEnum.Stop};_heroChoices.AddChild(shade);shade.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        var panel=new PanelContainer {AnchorLeft=.06f,AnchorRight=.94f,AnchorTop=.1f,AnchorBottom=.9f};panel.AddThemeStyleboxOverride("panel",MenuUi.Box(true,14));_heroChoices.AddChild(panel);
        var content=new VBoxContainer();panel.AddChild(content);
        var header=new HBoxContainer();content.AddChild(header);
        var title=DeckMenuUi.Text("SELECT GROUP",20);title.SizeFlagsHorizontal=SizeFlags.ExpandFill;header.AddChild(title);
        header.AddChild(DeckMenuUi.Button("CLOSE",()=>_heroChoices.Hide(),100));
        var scroll=new ScrollContainer {SizeFlagsVertical=SizeFlags.ExpandFill,SizeFlagsHorizontal=SizeFlags.ExpandFill,HorizontalScrollMode=ScrollContainer.ScrollMode.Disabled};content.AddChild(scroll);
        var row=new GridContainer {Columns=GetViewportRect().Size.X<900?2:4,SizeFlagsHorizontal=SizeFlags.ExpandFill};row.AddThemeConstantOverride("h_separation",8);row.AddThemeConstantOverride("v_separation",8);scroll.AddChild(row);
        foreach(int g in CardCatalog.HeroGroups)
        {
            int group=g;var choice=DeckMenuUi.Button(CardCatalog.Groups[g],()=>SetHero(_heroIndex,group),180,64);choice.SizeFlagsHorizontal=SizeFlags.ExpandFill;
            choice.Disabled=Draft.Heroes.Contains(g)||Enumerable.Range(2,5).Any(cost=>CardCatalog.Options(g,cost).Count(PlayerInventory.CanUse)<2);row.AddChild(choice);
            var image=new TextureRect {Texture=CardCatalog.Portrait(CardCatalog.Options(g,2).First()),ExpandMode=TextureRect.ExpandModeEnum.IgnoreSize,StretchMode=TextureRect.StretchModeEnum.KeepAspectCentered,Position=new(6,8),Size=new(50,48),MouseFilter=MouseFilterEnum.Ignore,TextureFilter=TextureFilterEnum.Nearest};choice.AddChild(image);
            var label=choice.GetNode<Label>("Text");label.OffsetLeft=58;GameUi.Label(label,13);label.Text=CardCatalog.Groups[g];label.AutowrapMode=TextServer.AutowrapMode.WordSmart;
        }
    }
    public bool SelectCardBack(string id)
    {
        if(CardBackCatalog.Find(id)==null||!PlayerInventory.OwnsCardBack(id))return false;
        Draft.CardBackId=CardBackCatalog.Normalize(id);_heroChoices.Hide();Render();return true;
    }
    private void ShowCardBackChoices()
    {
        DeckMenuUi.Clear(_heroChoices);_heroChoices.Show();
        var shade=new ColorRect {Color=new Color(0,0,0,.8f),MouseFilter=MouseFilterEnum.Stop};_heroChoices.AddChild(shade);shade.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        var panel=new PanelContainer {AnchorLeft=.06f,AnchorRight=.94f,AnchorTop=.06f,AnchorBottom=.94f};panel.AddThemeStyleboxOverride("panel",MenuUi.Box(true,14));_heroChoices.AddChild(panel);
        var content=new VBoxContainer();panel.AddChild(content);
        var header=new HBoxContainer();content.AddChild(header);var title=DeckMenuUi.Text("Change card back",20);title.SizeFlagsHorizontal=SizeFlags.ExpandFill;header.AddChild(title);header.AddChild(DeckMenuUi.Button("CLOSE",()=>_heroChoices.Hide(),100));
        var scroll=new ScrollContainer {SizeFlagsVertical=SizeFlags.ExpandFill,HorizontalScrollMode=ScrollContainer.ScrollMode.Disabled};content.AddChild(scroll);
        var grid=new GridContainer {Columns=GetViewportRect().Size.X<900?2:5,SizeFlagsHorizontal=SizeFlags.ExpandFill};scroll.AddChild(grid);
        foreach(var back in CardBackCatalog.All){
            string id=back.Id;var box=new VBoxContainer();grid.AddChild(box);box.AddChild(CardBackCatalog.Preview(id,96,128));
            var name=DeckMenuUi.Text(back.Name,13);name.HorizontalAlignment=HorizontalAlignment.Center;box.AddChild(name);
            var button=DeckMenuUi.Button(Draft.CardBackId==id?"SELECTED":PlayerInventory.OwnsCardBack(id)?"USE":"LOCKED / SHOP",()=>SelectCardBack(id),160);
            button.Name="CardBackChoice_"+id;button.Disabled=!PlayerInventory.OwnsCardBack(id);GameUi.Selected(button,Draft.CardBackId==id);box.AddChild(button);
        }
    }
    private void RenderFilters()
    {
        DeckMenuUi.Clear(_filters);
        foreach(int value in new[]{0,2,3,4,5,6}){int cost=value;var filter=DeckMenuUi.Button(cost==0?"ALL":cost.ToString(),()=>Filter(cost,_search.Text),cost==0?64:46,28);_filters.AddChild(filter);GameUi.Selected(filter,CostFilter==cost);}
    }
    private void RenderCatalog()
    {
        DeckMenuUi.Clear(_catalog);VisibleCardCount=0;
        foreach(int kind in Enumerable.Range(0,CardCatalog.Count).Where(k=>CharacterData.Get(k).Enabled&&CardCatalog.Group(k)==ActiveGroup&&(CostFilter==0||CardCatalog.Cost(k)==CostFilter)&&CardCatalog.Name(k).Contains(_search.Text,StringComparison.OrdinalIgnoreCase)))
        {
            VisibleCardCount++;int captured=kind;
            var card=new DeckCatalogCard {Builder=this,Kind=kind,CustomMinimumSize=new(166,MobileLayout.Enabled?244:224),SizeFlagsHorizontal=SizeFlags.ExpandFill,IgnoreTextureSize=true,StretchMode=TextureButton.StretchModeEnum.Scale};MenuUi.Button(card);_catalog.AddChild(card);
            card.Pressed+=()=>SelectCard(captured);
            var content=new VBoxContainer {MouseFilter=MouseFilterEnum.Ignore};card.AddChild(content);content.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);content.OffsetLeft=8;content.OffsetRight=-8;content.OffsetTop=6;content.OffsetBottom=-6;
            var title=DeckMenuUi.Text($"{CardCatalog.Groups[ActiveGroup]} / Cost {CardCatalog.Cost(kind)}",12);title.ClipText=true;title.TextOverrunBehavior=TextServer.OverrunBehavior.TrimEllipsis;content.AddChild(title);
            var art=DeckMenuUi.Art(kind,84,104);art.SizeFlagsHorizontal=SizeFlags.ExpandFill;content.AddChild(art);
            var name=DeckMenuUi.Text(CardCatalog.Name(kind),16);name.AutowrapMode=TextServer.AutowrapMode.WordSmart;name.HorizontalAlignment=HorizontalAlignment.Center;content.AddChild(name);
            var selected=Draft.Cards.Find(c=>c.Kind==kind);var state=DeckMenuUi.Text(!PlayerInventory.CanUse(kind)?"LOCKED":selected==null?"":$"IN DECK  x{selected.Copies}",12);state.HorizontalAlignment=HorizontalAlignment.Center;content.AddChild(state);
            
        }
        if(VisibleCardCount==0)_catalog.AddChild(DeckMenuUi.Text("No matching cards.",15));
    }
    public void SelectCard(int kind){_selectedKind=kind;RenderDetails();if(_inspectorTabs!=null)_inspectorTabs.CurrentTab=1;}
    public void BeginCardDrag(int kind){_selectedKind=kind;RenderDetails();_inspectorTabs.CurrentTab=0;}
    private void RenderDetails()
    {
        if(_details==null||_selectedKind<0)return;
        DeckMenuUi.Clear(_details);int kind=_selectedKind;
        _details.AddChild(DeckMenuUi.Art(kind,120,124));
        var name=DeckMenuUi.Text(CardCatalog.Name(kind),18);name.AutowrapMode=TextServer.AutowrapMode.WordSmart;name.CustomMinimumSize=new(190,28);_details.AddChild(name);
        var stats=CharacterData.Stats(kind,1);var character=CharacterData.Get(kind);
        if(CharacterData.IsSupport(kind))_details.AddChild(DeckMenuUi.Text($"Cost {CardCatalog.Cost(kind)}",14));
        else _details.AddChild(DeckMenuUi.Text($"Cost {CardCatalog.Cost(kind)}   /   1-star stats\nHP / ATK {stats.Hp}   ARMOR {stats.Armor}",14));
        var description=DeckMenuUi.Text(CharacterData.CombatDescription(kind,1),14);description.AutowrapMode=TextServer.AutowrapMode.WordSmart;_details.AddChild(description);
        if(CharacterData.IsSupport(kind))return;
        if(!PlayerInventory.CanUse(kind)){_details.AddChild(DeckMenuUi.Text("Unlock this character in the lobby shop.",14));return;}
        var entry=Draft.Cards.Find(c=>c.Kind==kind);
        if(entry!=null)
        {
            _details.AddChild(DeckMenuUi.Text($"Included: {entry.Copies} / {character.MaxCopies} copies",14));
            var row=new HBoxContainer();_details.AddChild(row);
            var minus=DeckMenuUi.Button("-",()=>ChangeCopies(kind,-1),44,30);minus.Disabled=entry.Copies<=1;row.AddChild(minus);
            var plus=DeckMenuUi.Button("+",()=>ChangeCopies(kind,1),44,30);plus.Disabled=entry.Copies>=CharacterData.Get(kind).MaxCopies;row.AddChild(plus);
            var remove=DeckMenuUi.Button("REMOVE",()=>RemoveCard(kind),96,30);if(MobileLayout.Enabled)_details.AddChild(remove);else row.AddChild(remove);
        }
        else
        {
            var matches=Draft.Cards.Where(c=>CardCatalog.Group(c.Kind)==CardCatalog.Group(kind)&&CardCatalog.Cost(c.Kind)==CardCatalog.Cost(kind)).ToArray();
            if(matches.Length<2)_details.AddChild(DeckMenuUi.Button("ADD TO DECK",()=>AddCard(kind),190));
            else
            {
                foreach(var match in matches){int target=match.Kind;_details.AddChild(DeckMenuUi.Button("Replace "+CardCatalog.Name(target),()=>ReplaceCard(target,kind),190,30));}
            }
        }
    }
    public bool ChangeCopies(int kind,int delta)
    {
        var entry=Draft.Cards.Find(c=>c.Kind==kind);if(entry==null||entry.Copies+delta<1||entry.Copies+delta>CharacterData.Get(kind).MaxCopies)return false;
        entry.Copies+=delta;Render();return true;
    }
    public void RemoveCard(int kind){Draft.Cards.RemoveAll(c=>c.Kind==kind);Render();}
    public bool CanAdd(int kind)=>kind>=0&&kind<CardCatalog.Count&&CharacterData.Get(kind).Enabled&&PlayerInventory.CanUse(kind)&&Draft.Heroes.Contains(CardCatalog.Group(kind))&&!Draft.Cards.Any(c=>c.Kind==kind)&&Draft.Cards.Count(c=>CardCatalog.Group(c.Kind)==CardCatalog.Group(kind)&&CardCatalog.Cost(c.Kind)==CardCatalog.Cost(kind))<2;
    public bool AddCard(int kind)
    {
        if(!CanAdd(kind))return false;
        Draft.Cards.Add(new(){Kind=kind,Copies=1});Render();return true;
    }
    public bool CanReplace(int target,int kind)=>kind>=0&&kind<CardCatalog.Count&&PlayerInventory.CanUse(kind)&&Draft.Cards.Any(c=>c.Kind==target)&&!Draft.Cards.Any(c=>c.Kind==kind)&&CardCatalog.Group(target)==CardCatalog.Group(kind)&&CardCatalog.Cost(target)==CardCatalog.Cost(kind);
    public bool ReplaceCard(int target,int kind)
    {
        if(!CanReplace(target,kind))return false;
        var entry=Draft.Cards.Find(c=>c.Kind==target)!;entry.Kind=kind;entry.Copies=1;_selectedKind=kind;Render();return true;
    }
    private void RenderDeck()
    {
        DeckMenuUi.Clear(_summary);DeckMenuUi.Clear(_list);
        foreach(int group in Draft.Heroes)
        {
            int g=group;int count=Draft.Cards.Count(c=>CardCatalog.Group(c.Kind)==g);
            _summary.AddChild(DeckMenuUi.Button((g==ActiveGroup?"> ":"")+CardCatalog.Groups[g]+$"   {count}/10",()=>SetGroup(g),264,32));
        }
        _summary.AddChild(DeckMenuUi.Button((ActiveGroup==4?"> ":"")+"Neutral",()=>SetGroup(4),264,32));
        if(ActiveGroup==4){foreach(int k in CharacterData.SupportKinds){int kind=k;_list.AddChild(DeckMenuUi.Button(CardCatalog.Name(k),()=>SelectCard(kind),264,36));}return;}
        for(int cost=2;cost<=6;cost++)
        {
            _list.AddChild(DeckMenuUi.Text($"Cost {cost}",12));
            var entries=Draft.Cards.Where(c=>CardCatalog.Group(c.Kind)==ActiveGroup&&CardCatalog.Cost(c.Kind)==cost).OrderBy(c=>c.Kind).ToArray();
            foreach(var entry in entries)
            {
                int kind=entry.Kind;var row=new HBoxContainer();VBoxContainer? mobileEntry=null;
                if(MobileLayout.Enabled){mobileEntry=new VBoxContainer {SizeFlagsHorizontal=SizeFlags.ExpandFill};_list.AddChild(mobileEntry);}else _list.AddChild(row);
                var target=new DeckCardDrop {Builder=this,Kind=kind,CustomMinimumSize=new(152,MobileLayout.Enabled?60:28),SizeFlagsHorizontal=SizeFlags.ExpandFill,IgnoreTextureSize=true,StretchMode=TextureButton.StretchModeEnum.Scale};MenuUi.Button(target);if(mobileEntry!=null){mobileEntry.AddChild(target);mobileEntry.AddChild(row);}else row.AddChild(target);target.Pressed+=()=>SelectCard(kind);
                var label=DeckMenuUi.Text(CardCatalog.Name(kind)+$"  x{entry.Copies}",13);label.ClipText=true;target.AddChild(label);label.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);label.HorizontalAlignment=HorizontalAlignment.Center;label.VerticalAlignment=VerticalAlignment.Center;
                var minus=DeckMenuUi.Button("-",()=>ChangeCopies(kind,-1),30,28);minus.Disabled=entry.Copies<=1;row.AddChild(minus);
                var plus=DeckMenuUi.Button("+",()=>ChangeCopies(kind,1),30,28);plus.Disabled=entry.Copies>=CharacterData.Get(kind).MaxCopies;row.AddChild(plus);
                row.AddChild(DeckMenuUi.Button("x",()=>RemoveCard(kind),28,28));
            }
            for(int empty=entries.Length;empty<2;empty++)
            {
                int c=cost;var slot=new DeckEmptyDrop {Builder=this,Group=ActiveGroup,Cost=cost,CustomMinimumSize=new(MobileLayout.Enabled?220:256,28),IgnoreTextureSize=true,StretchMode=TextureButton.StretchModeEnum.Scale};MenuUi.Button(slot);_list.AddChild(slot);slot.Pressed+=()=>Filter(c);
                var label=DeckMenuUi.Text("+ Choose cost "+cost,13);slot.AddChild(label);label.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);label.HorizontalAlignment=HorizontalAlignment.Center;
            }
        }
    }
    private void RefreshStatus()
    {
        if(_status==null)return;string error=Draft.Validate();if(error==""&&!PlayerInventory.OwnsCardBack(Draft.CardBackId))error="Unlock this card back in Shop before saving.";if(error==""&&Draft.Cards.Any(c=>!PlayerInventory.CanUse(c.Kind)))error="Unlock the characters in this deck before saving.";_save.Disabled=_saving||error!="";
        _total.Text=$"{Draft.Cards.Count}/30 kinds  /  {Draft.Cards.Sum(c=>c.Copies)} copies";
        _status.Text=error==""?"":error;
        if(error!=""&&Draft.Cards.Count<30)
            foreach(int g in Draft.Heroes)for(int c=2;c<=6;c++){int count=Draft.Cards.Count(e=>CardCatalog.Group(e.Kind)==g&&CardCatalog.Cost(e.Kind)==c);if(count<2){_status.Text=$"{CardCatalog.Groups[g]} needs {2-count} more cost-{c} card(s).";return;}}
    }
    public async void Save()
    {
        if(Draft.Validate()!=""||_saving||_save.Disabled)return;
        var decks=DeckStore.Decks.Select(d=>d.Clone()).ToList();int index=decks.FindIndex(d=>d.Id==Draft.Id);var deck=Draft.Clone();
        if(index>=0)decks[index]=deck;else decks.Add(deck);
        _saving=true;_save.Disabled=true;_status.Text="Saving to your account...";
        try {
            if(GameAccount.Session!=null)await PlayerInventory.SaveDecks(decks,deck.Id);
            else {DeckStore.ApplyRemote("",decks,deck.Id);if(!DeckStore.Save())throw new Exception(DeckStore.Error);}
            if(IsInsideTree())GetTree().ChangeSceneToFile("res://scenes/lobby.tscn");
        } catch(Exception e){if(IsInsideTree()){_status.Text="Could not save: "+e.Message;_save.Disabled=false;}}
        finally{_saving=false;}

    }
}
public partial class DeckCatalogCard : TextureButton
{
    public DeckBuilder Builder {get;set;}=null!;public int Kind {get;set;}
    public override Variant _GetDragData(Vector2 atPosition)
    {
        Builder.BeginCardDrag(Kind);var preview=DeckMenuUi.Art(Kind,84,104);SetDragPreview(preview);return Kind;
    }
}
public partial class DeckCardDrop : TextureButton
{
    public DeckBuilder Builder {get;set;}=null!;public int Kind {get;set;}
    public override bool _CanDropData(Vector2 atPosition,Variant data)=>data.VariantType==Variant.Type.Int&&Builder.CanReplace(Kind,data.AsInt32());
    public override void _DropData(Vector2 atPosition,Variant data)=>Builder.ReplaceCard(Kind,data.AsInt32());
}

public partial class DeckEmptyDrop : TextureButton
{
    public DeckBuilder Builder {get;set;}=null!;public int Group {get;set;}public int Cost {get;set;}
    public override bool _CanDropData(Vector2 atPosition,Variant data)
    {
        if(data.VariantType!=Variant.Type.Int)return false;int kind=data.AsInt32();
        return Builder.CanAdd(kind)&&CardCatalog.Group(kind)==Group&&CardCatalog.Cost(kind)==Cost;
    }
    public override void _DropData(Vector2 atPosition,Variant data)=>Builder.AddCard(data.AsInt32());
}
