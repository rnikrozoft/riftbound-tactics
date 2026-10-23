using Godot;
using System;
using System.Linq;

public partial class CollectionShopPage : VBoxContainer
{
    private Label _status=null!,_wallet=null!,_title=null!;
    private GridContainer _cards=null!;
    private ScrollContainer _cardScroll=null!;
    private OptionButton _groupFilter=null!;
    private VBoxContainer _details=null!;
    private Control _detailsPanel=null!;
    public int SelectedKind {get;private set;}=-1;
    public int DetailStars {get;private set;}=1;
    private TextureButton _buyGroup=null!;
    private readonly System.Collections.Generic.Dictionary<int,string> _groupRequests=new();
    private bool _shop,_busy,_cardBackMode;
    private bool ShopPaused=>CharactersFree && (PlayerInventory.Configuration?.Economy.FreeCardBacks??false);
    private bool CharactersFree=>PlayerInventory.Configuration?.Economy.FreeCharacterCards??false;
    private HBoxContainer _groupFilters=null!;
    private TextureButton _charactersTab=null!,_backsTab=null!;
    private readonly System.Collections.Generic.Dictionary<string,string> _cardBackRequests=new();
    private int _group;
    private readonly System.Collections.Generic.Dictionary<int,string> _purchaseRequests=new();
    public override void _Ready()
    {
        AddThemeConstantOverride("separation",18);
        var categories=new HBoxContainer {Name="CollectionSubmenu"};AddChild(categories);
        _charactersTab=DeckMenuUi.Button("Characters",()=>{_cardBackMode=false;Render();},136,38);_charactersTab.Name="ShopCharactersTab";categories.AddChild(_charactersTab);
        _backsTab=DeckMenuUi.Button("Card backs",()=>{_cardBackMode=true;Render();},136,38);_backsTab.Name="ShopCardBacksTab";categories.AddChild(_backsTab);
        var heading=new HBoxContainer {Name="CollectionHeading"};AddChild(heading);
        _title=DeckMenuUi.Text("Collection",28);heading.AddChild(_title);
        _wallet=DeckMenuUi.Text("",14);_wallet.Name="CollectionOwnedCount";heading.AddChild(_wallet);
        heading.AddChild(new Control {SizeFlagsHorizontal=SizeFlags.ExpandFill});
        var filters=new HBoxContainer();_groupFilters=filters;heading.AddChild(filters);
        _groupFilter=new OptionButton {Name="ShopGroupFilter",CustomMinimumSize=new(240,MobileLayout.Enabled?60:40)};filters.AddChild(_groupFilter);
        _groupFilter.ItemSelected+=index=>{_group=_groupFilter.GetItemId((int)index);SelectedKind=-1;Render();};
        _status=DeckMenuUi.Text("",14);_status.AutowrapMode=TextServer.AutowrapMode.WordSmart;AddChild(_status);_status.Hide();
        _buyGroup=DeckMenuUi.Button("Unlock group",PurchaseGroup,220,40);_buyGroup.Name="BuyCharacterGroupButton";AddChild(_buyGroup);
        var body=new HBoxContainer {SizeFlagsVertical=SizeFlags.ExpandFill};body.AddThemeConstantOverride("separation",18);AddChild(body);
        var scroll=new ScrollContainer {SizeFlagsHorizontal=SizeFlags.ExpandFill,SizeFlagsVertical=SizeFlags.ExpandFill,HorizontalScrollMode=ScrollContainer.ScrollMode.Disabled};_cardScroll=scroll;body.AddChild(scroll);
        _cards=new GridContainer {Columns=3,SizeFlagsHorizontal=SizeFlags.ExpandFill};_cards.AddThemeConstantOverride("h_separation",12);_cards.AddThemeConstantOverride("v_separation",12);scroll.AddChild(_cards);
        var inspector=DeckMenuUi.Panel(body,MobileLayout.Enabled?280:320);_detailsPanel=inspector.GetParent<Control>();_detailsPanel.Name="CollectionDetails";
        var detailScroll=new ScrollContainer {SizeFlagsVertical=SizeFlags.ExpandFill,HorizontalScrollMode=ScrollContainer.ScrollMode.Disabled};inspector.AddChild(detailScroll);
        _details=new VBoxContainer {SizeFlagsHorizontal=SizeFlags.ExpandFill};detailScroll.AddChild(_details);_detailsPanel.Hide();
    }
    public void Open(bool shop)
    {
        if(shop&&!_shop)_group=CharacterData.All.FirstOrDefault(c=>c.Enabled&&!PlayerInventory.Owns(c.Kind))?.Group??0;
        if(shop&&!_shop)_cardBackMode=true;_shop=shop;if(!shop){SelectedKind=-1;DetailStars=1;}Render();
        _status.Text="";
    }
    public override void _Process(double delta)
    {
        if(_status!=null)_status.Visible=_status.Text!="";
        if(IsVisibleInTree())FitCardColumns();
    }
    private void FitCardColumns()
    {
        if(_cards==null || _cardScroll==null || _cards.GetChildCount()==0)return;
        float tileWidth=_cards.GetChildren().OfType<Control>().Max(c=>c.GetCombinedMinimumSize().X);
        int gap=_cards.GetThemeConstant("h_separation");
        float inspector=_detailsPanel!=null&&_detailsPanel.Visible?_detailsPanel.GetCombinedMinimumSize().X+18:0;
        float viewportWidth=GetViewportRect().Size.X-_cardScroll.GlobalPosition.X-(MobileLayout.Enabled?16:32)-inspector;
        float available=Math.Min(_cardScroll.Size.X,viewportWidth)-_cardScroll.GetVScrollBar().GetCombinedMinimumSize().X;
        int columns=Math.Max(1,(int)Math.Floor((available+gap)/(tileWidth+gap)));
        columns=Math.Min(columns,_cards.GetChildCount());
        if(_cards.Columns!=columns)_cards.Columns=columns;
    }
    private void Render()
    {
        _wallet.Text=$"OWNED / {CharacterData.All.Count(c=>c.Enabled && PlayerInventory.CanUse(c.Kind))}";
        _title.Text=_shop?"Shop":"Collection";
        _wallet.Visible=!_shop;
        _charactersTab.Visible=true;
        _backsTab.Visible=true;
        GameUi.Selected(_charactersTab,!_cardBackMode);GameUi.Selected(_backsTab,_cardBackMode);
        _groupFilters.Visible=!_cardBackMode;_buyGroup.Visible=_shop&&!_cardBackMode;
        if(_cardBackMode){_detailsPanel.Hide();RenderCardBacks();return;}
        _detailsPanel.Visible=SelectedKind>=0;
        var groups=CharacterData.All.Where(c=>c.Enabled).Select(c=>c.Group).Distinct().OrderBy(g=>g).ToArray();
        if(!groups.Contains(_group))_group=groups.First();
        _groupFilter.Clear();foreach(int group in groups)_groupFilter.AddItem(CharacterData.Groups[group],group);_groupFilter.Select(Array.IndexOf(groups,_group));
        var groupCards=CharacterData.All.Where(c=>c.Enabled&&c.Group==_group).ToArray();
        if(!groupCards.Any(c=>c.Kind==SelectedKind)){SelectedKind=groupCards[0].Kind;DetailStars=1;}
        _detailsPanel.Show();RenderDetails();
        int ownedCount=groupCards.Count(c=>PlayerInventory.Owns(c.Kind));long groupPrice=PlayerInventory.GroupPrice(_group);
        _buyGroup.Visible=_shop&&!CharactersFree;
        _buyGroup.GetNode<Label>("Text").Text=ownedCount==groupCards.Length?"CHARACTER OWNED":$"UNLOCK ALL / {groupPrice} COINS";
        _buyGroup.Disabled=_busy||!PlayerInventory.Ready||groupPrice<1||PlayerInventory.Coins<groupPrice;
        
        DeckMenuUi.Clear(_cards);
        foreach(var character in CharacterData.All.Where(c=>c.Enabled&&c.Group==_group)){
            int kind=character.Kind;var panel=DeckMenuUi.Panel(_cards,154);panel.MouseFilter=MouseFilterEnum.Pass;var frame=panel.GetParent<PanelContainer>();frame.SizeFlagsHorizontal=SizeFlags.ExpandFill;frame.Name="CollectionCard_"+kind;frame.SetMeta("kind",kind);frame.MouseDefaultCursorShape=CursorShape.PointingHand;frame.FocusMode=FocusModeEnum.All;
            frame.AddThemeStyleboxOverride("panel",MenuUi.Box(kind==SelectedKind));
            frame.GuiInput+=input=>{if(input is InputEventMouseButton {Pressed:true,ButtonIndex:MouseButton.Left}||input is InputEventKey {Pressed:true,Keycode:Key.Enter or Key.Space})SelectCharacter(kind);};
            var art=CollectionArt(kind);art.GetNode<Control>("ClassEmblem").Hide();panel.AddChild(art);var name=DeckMenuUi.Text(character.Name,16);name.AutowrapMode=TextServer.AutowrapMode.WordSmart;name.HorizontalAlignment=HorizontalAlignment.Center;panel.AddChild(name);
            bool owned=PlayerInventory.Owns(kind);var ownership=DeckMenuUi.Text(owned?"OWNED":PlayerInventory.AllUnlocked?(MobileLayout.Enabled?"FREE TO PLAY":"FREE TO PLAY / NOT OWNED"):"LOCKED",12);ownership.HorizontalAlignment=HorizontalAlignment.Center;panel.AddChild(ownership);
            if(_shop&&!CharactersFree&&!owned){long price=PlayerInventory.Price(kind);var buy=DeckMenuUi.Button($"BUY / {price} COINS",()=>Purchase(kind),180,34);buy.Disabled=_busy||!PlayerInventory.Ready||price<1||PlayerInventory.Coins<price;panel.AddChild(buy);}
        }
    }
    public void ShowCardBacks(){_cardBackMode=true;Render();}
    private void RenderCardBacks()
    {
        DeckMenuUi.Clear(_cards);
        foreach(var back in CardBackCatalog.All){
            string id=back.Id;bool owned=PlayerInventory.OwnsCardBack(id);var panel=DeckMenuUi.Panel(_cards,188);panel.GetParent<Control>().SizeFlagsHorizontal=SizeFlags.ExpandFill;panel.GetParent().Name="ShopCardBack_"+id;
            panel.AddChild(CardBackCatalog.Preview(id,96,128));var name=DeckMenuUi.Text(back.Name,16);name.HorizontalAlignment=HorizontalAlignment.Center;panel.AddChild(name);
            if(owned)panel.AddChild(DeckMenuUi.Text("OWNED",13));
            if(_shop&&!ShopPaused&&!owned){var buy=DeckMenuUi.Button($"Buy {back.Price} COINS",()=>PurchaseCardBack(id),180);buy.Name="BuyCardBack_"+id;buy.Disabled=_busy||!PlayerInventory.Ready||PlayerInventory.Coins<back.Price;panel.AddChild(buy);}
        }
    }
    private async void PurchaseCardBack(string id)
    {
        if(_busy)return;_busy=true;
        if(!_cardBackRequests.TryGetValue(id,out string? request))_cardBackRequests[id]=request=Guid.NewGuid().ToString("N");
        _status.Text="Purchasing card back...";Render();
        try{await PlayerInventory.PurchaseCardBack(id,request);if(!IsInsideTree())return;_cardBackRequests.Remove(id);_status.Text="Card back unlocked. Choose it in Deck → Edit deck.";}
        catch(Exception e){if(IsInsideTree())_status.Text="Purchase failed: "+e.Message;}
        finally{_busy=false;if(IsInsideTree())Render();}
    }
    public void SelectCharacter(int kind)
    {
        if(!CharacterData.Get(kind).Enabled)return;
        SelectedKind=kind;DetailStars=1;_detailsPanel.Show();RenderDetails();FitCardColumns();
        foreach(var frame in _cards.GetChildren().OfType<PanelContainer>())if(frame.HasMeta("kind"))frame.AddThemeStyleboxOverride("panel",MenuUi.Box(frame.GetMeta("kind").AsInt32()==kind));
    }
    public void SetDetailStars(int stars){DetailStars=Math.Clamp(stars,1,4);RenderDetails();}
    private static TextureRect CollectionArt(int kind)
    {
        var size=ShopCard.ShopDisplaySize;
        var art=DeckMenuUi.Art(kind,(int)size.X,(int)size.Y);
        art.Name="CollectionArtwork";
        art.StretchMode=TextureRect.StretchModeEnum.Scale;
        art.SizeFlagsHorizontal=SizeFlags.ShrinkCenter;
        art.SizeFlagsVertical=SizeFlags.ShrinkBegin;
        return art;
    }
    private void RenderDetails()
    {
        if(SelectedKind<0)return;DeckMenuUi.Clear(_details);_details.AddThemeConstantOverride("separation",8);
        var character=CharacterData.Get(SelectedKind);var stats=CharacterData.Stats(SelectedKind,DetailStars);
        var identity=new HBoxContainer();_details.AddChild(identity);
        var art=CollectionArt(SelectedKind);art.GetNode<Control>("ClassEmblem").Hide();art.SizeFlagsHorizontal=SizeFlags.ShrinkBegin;identity.AddChild(art);
        var info=new VBoxContainer {SizeFlagsHorizontal=SizeFlags.ExpandFill,SizeFlagsVertical=SizeFlags.ShrinkCenter};identity.AddChild(info);
        var name=DeckMenuUi.Text(character.Name,22);name.Name="CollectionDetailName";name.AutowrapMode=TextServer.AutowrapMode.WordSmart;info.AddChild(name);
        var group=DeckMenuUi.Text($"{CharacterData.Groups[character.Group]}\nCost {character.Cost}",14);group.AutowrapMode=TextServer.AutowrapMode.WordSmart;info.AddChild(group);
        if(!CharacterData.IsSupport(SelectedKind)){
        var levels=new HBoxContainer();_details.AddChild(levels);
        for(int i=1;i<=4;i++){int tier=i;var button=DeckMenuUi.Button(CharacterData.DisplayStars(i)+"★",()=>SetDetailStars(tier),52,32);levels.AddChild(button);GameUi.Selected(button,DetailStars==i);}
        _details.AddChild(MenuUi.Eyebrow("STATS"));
        _details.AddChild(DeckMenuUi.Text($"HP  {stats.Hp}     ATK  {stats.Attack}\nARMOR  {stats.Armor}",16));
        }
        _details.AddChild(MenuUi.Eyebrow("ATTACK & ABILITIES"));
        var description=DeckMenuUi.Text(CharacterData.CombatDescription(SelectedKind,DetailStars),15);description.Name="CollectionDetailAbilities";description.AutowrapMode=TextServer.AutowrapMode.WordSmart;_details.AddChild(description);
    }
    private async void PurchaseGroup()
    {
        if(_busy)return;int group=_group;_busy=true;
        if(!_groupRequests.TryGetValue(group,out string? request))_groupRequests[group]=request=Guid.NewGuid().ToString("N");
        _status.Text="Unlocking character...";Render();
        try {await PlayerInventory.PurchaseGroup(group,request);if(!IsInsideTree())return;_groupRequests.Remove(group);_status.Text=CharacterData.Groups[group]+" unlocked. You can now select this character in Deck Builder.";}
        catch(Exception e){if(IsInsideTree())_status.Text="Purchase failed: "+e.Message;}
        finally {_busy=false;if(IsInsideTree())Render();}
    }
    private async void Purchase(int kind)
    {
        if(_busy)return;_busy=true;
        if(!_purchaseRequests.TryGetValue(kind,out string? request))_purchaseRequests[kind]=request=Guid.NewGuid().ToString("N");
        _status.Text="Purchasing...";Render();
        try {await PlayerInventory.Purchase(kind,request);if(!IsInsideTree())return;_purchaseRequests.Remove(kind);_status.Text=CharacterData.Get(kind).Name+" added to your collection.";}
        catch(Exception e){if(IsInsideTree())_status.Text="Purchase failed: "+e.Message;}
        finally{_busy=false;if(IsInsideTree())Render();}
    }
}
