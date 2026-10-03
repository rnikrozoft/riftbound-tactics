using Godot;
using System;
using System.Linq;

public partial class DeckBuilder : Control
{
    public DeckDefinition Draft {get;private set;}=null!;
    public int ActiveGroup {get;private set;}
    public int CostFilter {get;private set;}
    public int VisibleCardCount {get;private set;}
    private int _selectedKind=-1,_heroIndex;
    private HBoxContainer _tabs=null!,_filters=null!;
    private GridContainer _catalog=null!;
    private VBoxContainer _details=null!,_list=null!,_summary=null!,_heroChoices=null!;
    private Label _status=null!,_total=null!;
    private LineEdit _search=null!;
    private TextureButton _save=null!;
    public override void _Ready()
    {
        DeckStore.Load();Draft=(DeckStore.Decks.Find(d=>d.Id==DeckStore.EditId)??DeckDefinition.Starter()).Clone();
        if(string.IsNullOrEmpty(DeckStore.EditId)){Draft.Id=Guid.NewGuid().ToString("N");Draft.Name="New expedition";}
        ActiveGroup=Draft.Heroes[0];
        var page=DeckMenuUi.Page(this);
        var header=new HBoxContainer();page.AddChild(header);
        header.AddChild(DeckMenuUi.Button("< LOBBY",()=>GetTree().ChangeSceneToFile("res://scenes/lobby.tscn"),120));
        var name=DeckMenuUi.Input(Draft.Name,"Deck name",360);name.SizeFlagsHorizontal=SizeFlags.ExpandFill;header.AddChild(name);
        name.TextChanged+=text=>{Draft.Name=text;RefreshStatus();};
        _save=DeckMenuUi.Button("SAVE DECK",Save,150);header.AddChild(_save);
        _tabs=new HBoxContainer();page.AddChild(_tabs);
        _heroChoices=new VBoxContainer();page.AddChild(_heroChoices);_heroChoices.Hide();
        var body=new HBoxContainer {SizeFlagsVertical=SizeFlags.ExpandFill};body.AddThemeConstantOverride("separation",12);page.AddChild(body);
        _details=DeckMenuUi.Panel(body,224);
        var center=new VBoxContainer {SizeFlagsHorizontal=SizeFlags.ExpandFill};body.AddChild(center);
        _search=DeckMenuUi.Input("","Search cards...",220);center.AddChild(_search);_search.TextChanged+=_=>RenderCatalog();
        _filters=new HBoxContainer();center.AddChild(_filters);
        var scroll=new ScrollContainer {SizeFlagsVertical=SizeFlags.ExpandFill,SizeFlagsHorizontal=SizeFlags.ExpandFill,HorizontalScrollMode=ScrollContainer.ScrollMode.Disabled};center.AddChild(scroll);
        _catalog=new GridContainer {Columns=3,SizeFlagsHorizontal=SizeFlags.ExpandFill};_catalog.AddThemeConstantOverride("h_separation",10);_catalog.AddThemeConstantOverride("v_separation",10);scroll.AddChild(_catalog);
        var right=DeckMenuUi.Panel(body,308);right.AddChild(DeckMenuUi.Text("MY DECK",22));_total=DeckMenuUi.Text("",15);right.AddChild(_total);
        _summary=new VBoxContainer();right.AddChild(_summary);
        right.AddChild(DeckMenuUi.Text("Click to inspect / drag here to replace",12));
        var listScroll=new ScrollContainer {SizeFlagsVertical=SizeFlags.ExpandFill,HorizontalScrollMode=ScrollContainer.ScrollMode.Disabled};right.AddChild(listScroll);
        _list=new VBoxContainer {SizeFlagsHorizontal=SizeFlags.ExpandFill};listScroll.AddChild(_list);
        _status=DeckMenuUi.Text("",14);page.AddChild(_status);
        SelectCard(CardCatalog.Options(ActiveGroup,2).First());Render();
    }
    public void SetHero(int index,int group)
    {
        if(index<0||index>=3||group<0||group>3||Draft.Heroes.Where((h,i)=>i!=index).Contains(group))return;
        int previous=Draft.Heroes[index];Draft.Cards.RemoveAll(c=>CardCatalog.Group(c.Kind)==previous);Draft.Heroes[index]=group;
        for(int cost=2;cost<=6;cost++)foreach(int kind in CardCatalog.Options(group,cost).Take(2))Draft.Cards.Add(new(){Kind=kind});
        ActiveGroup=group;_heroChoices.Hide();SelectCard(CardCatalog.Options(group,2).First());Render();
    }
    public void SetGroup(int group)
    {
        if(!Draft.Heroes.Append(4).Contains(group))return;
        ActiveGroup=group;CostFilter=0;_heroChoices.Hide();SelectCard(CardCatalog.Options(group,2).First());Render();
    }
    public void Filter(int cost,string search=""){CostFilter=cost;_search.Text=search;RenderFilters();RenderCatalog();}
    private void Render()
    {
        DeckMenuUi.Clear(_tabs);
        for(int i=0;i<3;i++)
        {
            int index=i,group=Draft.Heroes[i];
            var tab=DeckMenuUi.Button((group==ActiveGroup?"> ":"")+CardCatalog.Groups[group],()=>{_heroIndex=index;SetGroup(group);},170,38);_tabs.AddChild(tab);
        }
        _tabs.AddChild(DeckMenuUi.Button((ActiveGroup==4?"> ":"")+"Neutral",()=>SetGroup(4),150));
        _tabs.AddChild(DeckMenuUi.Button("CHANGE HERO",ShowHeroChoices,160));
        RenderFilters();RenderCatalog();RenderDeck();RenderDetails();RefreshStatus();
    }
    private void ShowHeroChoices()
    {
        if(ActiveGroup==4){_status.Text="Neutral is required. Select a hero tab to change its hero.";return;}
        _heroIndex=Array.IndexOf(Draft.Heroes,ActiveGroup);DeckMenuUi.Clear(_heroChoices);_heroChoices.Show();
        var row=new HBoxContainer();_heroChoices.AddChild(row);
        for(int g=0;g<4;g++)
        {
            int group=g;var choice=DeckMenuUi.Button(CardCatalog.Groups[g],()=>SetHero(_heroIndex,group),160);
            choice.Disabled=Draft.Heroes.Contains(g);row.AddChild(choice);
        }
        row.AddChild(DeckMenuUi.Button("CANCEL",()=>_heroChoices.Hide(),100));
        _heroChoices.AddChild(DeckMenuUi.Text("Changing a hero replaces its ten slots with a starter selection.",13));
    }
    private void RenderFilters()
    {
        DeckMenuUi.Clear(_filters);
        foreach(int value in new[]{0,2,3,4,5,6}){int cost=value;_filters.AddChild(DeckMenuUi.Button((CostFilter==cost?"> ":"")+(cost==0?"ALL":cost.ToString()),()=>Filter(cost,_search.Text),cost==0?64:46,28));}
    }
    private void RenderCatalog()
    {
        DeckMenuUi.Clear(_catalog);VisibleCardCount=0;
        foreach(int kind in Enumerable.Range(0,CardCatalog.Count).Where(k=>CardCatalog.Group(k)==ActiveGroup&&(CostFilter==0||CardCatalog.Cost(k)==CostFilter)&&CardCatalog.Name(k).Contains(_search.Text,StringComparison.OrdinalIgnoreCase)))
        {
            VisibleCardCount++;int captured=kind;
            var card=new DeckCatalogCard {Builder=this,Kind=kind,CustomMinimumSize=new(154,192),SizeFlagsHorizontal=SizeFlags.ExpandFill,IgnoreTextureSize=true,StretchMode=TextureButton.StretchModeEnum.Scale};TravelBookUi.StyleButton(card);_catalog.AddChild(card);
            card.Pressed+=()=>SelectCard(captured);
            var content=new VBoxContainer {MouseFilter=MouseFilterEnum.Ignore};card.AddChild(content);content.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);content.OffsetLeft=8;content.OffsetRight=-8;content.OffsetTop=6;content.OffsetBottom=-6;
            var title=DeckMenuUi.Text($"{CardCatalog.Groups[ActiveGroup]} / Cost {CardCatalog.Cost(kind)}",12);content.AddChild(title);
            var art=DeckMenuUi.Art(kind,84,104);art.SizeFlagsHorizontal=SizeFlags.ExpandFill;content.AddChild(art);
            var name=DeckMenuUi.Text(new[]{"Scout","Guard","Sage","Warden"}[CardCatalog.Variant(kind)],16);name.HorizontalAlignment=HorizontalAlignment.Center;content.AddChild(name);
            var selected=Draft.Cards.Find(c=>c.Kind==kind);var state=DeckMenuUi.Text(selected==null?"Click to choose":$"IN DECK  x{selected.Copies}",12);state.HorizontalAlignment=HorizontalAlignment.Center;content.AddChild(state);
            card.TooltipText="Click to inspect. Drag onto a same-cost deck card to replace it.";
        }
        if(VisibleCardCount==0)_catalog.AddChild(DeckMenuUi.Text("No matching cards.",15));
    }
    public void SelectCard(int kind){_selectedKind=kind;RenderDetails();}
    private void RenderDetails()
    {
        if(_details==null||_selectedKind<0)return;
        DeckMenuUi.Clear(_details);int kind=_selectedKind;
        _details.AddChild(DeckMenuUi.Text("CARD DETAILS",18));_details.AddChild(DeckMenuUi.Art(kind,160,190));
        var name=DeckMenuUi.Text(CardCatalog.Name(kind),18);name.AutowrapMode=TextServer.AutowrapMode.WordSmart;name.CustomMinimumSize=new(190,50);_details.AddChild(name);
        _details.AddChild(DeckMenuUi.Text($"Cost {CardCatalog.Cost(kind)}   /   1-star stats\nHP 100   ATK 30   Speed 10",14));
        var description=DeckMenuUi.Text("Basic fighter (mock).\nDuplicate purchases upgrade it, up to 4 stars.",14);description.AutowrapMode=TextServer.AutowrapMode.WordSmart;_details.AddChild(description);
        var entry=Draft.Cards.Find(c=>c.Kind==kind);
        if(entry!=null)
        {
            _details.AddChild(DeckMenuUi.Text($"Included: {entry.Copies} / 4 copies",14));
            var row=new HBoxContainer();_details.AddChild(row);
            var minus=DeckMenuUi.Button("-",()=>ChangeCopies(kind,-1),44,30);minus.Disabled=entry.Copies<=1;row.AddChild(minus);
            var plus=DeckMenuUi.Button("+",()=>ChangeCopies(kind,1),44,30);plus.Disabled=entry.Copies>=4;row.AddChild(plus);
            row.AddChild(DeckMenuUi.Button("REMOVE",()=>RemoveCard(kind),96,30));
        }
        else
        {
            var matches=Draft.Cards.Where(c=>CardCatalog.Group(c.Kind)==CardCatalog.Group(kind)&&CardCatalog.Cost(c.Kind)==CardCatalog.Cost(kind)).ToArray();
            if(matches.Length<2)_details.AddChild(DeckMenuUi.Button("ADD TO DECK",()=>AddCard(kind),190));
            else
            {
                _details.AddChild(DeckMenuUi.Text("Choose a card to replace:",13));
                foreach(var match in matches){int target=match.Kind;_details.AddChild(DeckMenuUi.Button("Replace "+new[]{"Scout","Guard","Sage","Warden"}[CardCatalog.Variant(target)],()=>ReplaceCard(target,kind),190,30));}
            }
        }
    }
    public bool ChangeCopies(int kind,int delta)
    {
        var entry=Draft.Cards.Find(c=>c.Kind==kind);if(entry==null||entry.Copies+delta<1||entry.Copies+delta>4)return false;
        entry.Copies+=delta;Render();return true;
    }
    public void RemoveCard(int kind){Draft.Cards.RemoveAll(c=>c.Kind==kind);Render();}
    public bool CanAdd(int kind)=>kind>=0&&kind<CardCatalog.Count&&Draft.Heroes.Append(4).Contains(CardCatalog.Group(kind))&&!Draft.Cards.Any(c=>c.Kind==kind)&&Draft.Cards.Count(c=>CardCatalog.Group(c.Kind)==CardCatalog.Group(kind)&&CardCatalog.Cost(c.Kind)==CardCatalog.Cost(kind))<2;
    public bool AddCard(int kind)
    {
        if(!CanAdd(kind))return false;
        Draft.Cards.Add(new(){Kind=kind,Copies=1});Render();return true;
    }
    public bool CanReplace(int target,int kind)=>kind>=0&&kind<CardCatalog.Count&&Draft.Cards.Any(c=>c.Kind==target)&&!Draft.Cards.Any(c=>c.Kind==kind)&&CardCatalog.Group(target)==CardCatalog.Group(kind)&&CardCatalog.Cost(target)==CardCatalog.Cost(kind);
    public bool ReplaceCard(int target,int kind)
    {
        if(!CanReplace(target,kind))return false;
        var entry=Draft.Cards.Find(c=>c.Kind==target)!;entry.Kind=kind;entry.Copies=1;_selectedKind=kind;Render();return true;
    }
    private void RenderDeck()
    {
        DeckMenuUi.Clear(_summary);DeckMenuUi.Clear(_list);
        foreach(int group in Draft.Heroes.Append(4))
        {
            int g=group;int count=Draft.Cards.Count(c=>CardCatalog.Group(c.Kind)==g);
            _summary.AddChild(DeckMenuUi.Button((g==ActiveGroup?"> ":"")+CardCatalog.Groups[g]+$"   {count}/10",()=>SetGroup(g),280,27));
        }
        for(int cost=2;cost<=6;cost++)
        {
            _list.AddChild(DeckMenuUi.Text($"Cost {cost}",12));
            var entries=Draft.Cards.Where(c=>CardCatalog.Group(c.Kind)==ActiveGroup&&CardCatalog.Cost(c.Kind)==cost).OrderBy(c=>c.Kind).ToArray();
            foreach(var entry in entries)
            {
                int kind=entry.Kind;var row=new HBoxContainer();_list.AddChild(row);
                var target=new DeckCardDrop {Builder=this,Kind=kind,CustomMinimumSize=new(152,28),SizeFlagsHorizontal=SizeFlags.ExpandFill,IgnoreTextureSize=true,StretchMode=TextureButton.StretchModeEnum.Scale};TravelBookUi.StyleButton(target);row.AddChild(target);target.Pressed+=()=>SelectCard(kind);
                var label=DeckMenuUi.Text(new[]{"Scout","Guard","Sage","Warden"}[CardCatalog.Variant(kind)]+$"  x{entry.Copies}",13);target.AddChild(label);label.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);label.HorizontalAlignment=HorizontalAlignment.Center;label.VerticalAlignment=VerticalAlignment.Center;
                var minus=DeckMenuUi.Button("-",()=>ChangeCopies(kind,-1),30,28);minus.Disabled=entry.Copies<=1;row.AddChild(minus);
                var plus=DeckMenuUi.Button("+",()=>ChangeCopies(kind,1),30,28);plus.Disabled=entry.Copies>=4;row.AddChild(plus);
                row.AddChild(DeckMenuUi.Button("x",()=>RemoveCard(kind),28,28));
            }
            for(int empty=entries.Length;empty<2;empty++)
            {
                int c=cost;var slot=new DeckEmptyDrop {Builder=this,Group=ActiveGroup,Cost=cost,CustomMinimumSize=new(270,28),IgnoreTextureSize=true,StretchMode=TextureButton.StretchModeEnum.Scale};TravelBookUi.StyleButton(slot);_list.AddChild(slot);slot.Pressed+=()=>Filter(c);
                var label=DeckMenuUi.Text("+ Choose cost "+cost,13);slot.AddChild(label);label.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);label.HorizontalAlignment=HorizontalAlignment.Center;
            }
        }
    }
    private void RefreshStatus()
    {
        if(_status==null)return;string error=Draft.Validate();_save.Disabled=error!="";
        _total.Text=$"{Draft.Cards.Count}/40 kinds  /  {Draft.Cards.Sum(c=>c.Copies)} copies";
        _status.Text=error==""?"Ready to save. Choose a card to inspect, or drag it onto a same-cost slot.":error;
        if(error!=""&&Draft.Cards.Count<40)
            foreach(int g in Draft.Heroes.Append(4))for(int c=2;c<=6;c++){int count=Draft.Cards.Count(e=>CardCatalog.Group(e.Kind)==g&&CardCatalog.Cost(e.Kind)==c);if(count<2){_status.Text=$"{CardCatalog.Groups[g]} needs {2-count} more cost-{c} card(s).";return;}}
    }
    public void Save()
    {
        if(Draft.Validate()!="")return;int index=DeckStore.Decks.FindIndex(d=>d.Id==Draft.Id);var deck=Draft.Clone();
        if(index>=0)DeckStore.Decks[index]=deck;else DeckStore.Decks.Add(deck);DeckStore.SelectedId=deck.Id;
        if(!DeckStore.Save()){_status.Text=DeckStore.Error;return;}GetTree().ChangeSceneToFile("res://scenes/lobby.tscn");
    }
}
public partial class DeckCatalogCard : TextureButton
{
    public DeckBuilder Builder {get;set;}=null!;public int Kind {get;set;}
    public override Variant _GetDragData(Vector2 atPosition)
    {
        Builder.SelectCard(Kind);var preview=DeckMenuUi.Art(Kind,84,104);SetDragPreview(preview);return Kind;
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
