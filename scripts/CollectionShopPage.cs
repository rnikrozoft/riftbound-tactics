using Godot;
using System;
using System.Linq;

public partial class CollectionShopPage : VBoxContainer
{
    private Label _status=null!,_wallet=null!,_title=null!;
    private GridContainer _cards=null!;
    private OptionButton _groupFilter=null!;
    private bool _shop,_busy;
    private int _group;
    private readonly System.Collections.Generic.Dictionary<int,string> _purchaseRequests=new();
    public override void _Ready()
    {
        _title=DeckMenuUi.Text("CHARACTER COLLECTION",26);AddChild(_title);
        _wallet=DeckMenuUi.Text("",16);AddChild(_wallet);
        _status=DeckMenuUi.Text("",14);AddChild(_status);
        AddChild(DeckMenuUi.Button("REFRESH COLLECTION",RefreshCollection,220,32));
        var filters=new HBoxContainer();AddChild(filters);filters.AddChild(DeckMenuUi.Text("CHARACTER",14));
        _groupFilter=new OptionButton {CustomMinimumSize=new(260,36)};filters.AddChild(_groupFilter);
        _groupFilter.ItemSelected+=index=>{_group=(int)index;Render();};
        var scroll=new ScrollContainer {SizeFlagsVertical=SizeFlags.ExpandFill,HorizontalScrollMode=ScrollContainer.ScrollMode.Disabled};AddChild(scroll);
        _cards=new GridContainer {Columns=3,SizeFlagsHorizontal=SizeFlags.ExpandFill};_cards.AddThemeConstantOverride("h_separation",10);_cards.AddThemeConstantOverride("v_separation",10);scroll.AddChild(_cards);
    }
    public void Open(bool shop)
    {
        _shop=shop;Render();
        _status.Text=PlayerInventory.Ready?(PlayerInventory.AllUnlocked?"All characters are available to play during testing. Ownership is saved separately.":"Unlock characters to add them to your decks."):"Login to load your collection and wallet.";
    }
    private void Render()
    {
        _wallet.Text=$"WALLET  /  {PlayerInventory.Coins} COINS     OWNED  /  {PlayerInventory.State?.Profile.OwnedCharacters.Length??0}";
        _title.Text=_shop?"CHARACTER SHOP":"YOUR COLLECTION";
        _groupFilter.Clear();for(int i=0;i<CharacterData.Groups.Length;i++)_groupFilter.AddItem(CharacterData.Groups[i],i);_groupFilter.Select(_group);
        DeckMenuUi.Clear(_cards);
        foreach(var character in CharacterData.All.Where(c=>c.Enabled&&c.Group==_group)){
            int kind=character.Kind;var panel=DeckMenuUi.Panel(_cards,200);panel.AddChild(DeckMenuUi.Art(kind,130,120));var name=DeckMenuUi.Text(character.Name,16);name.AutowrapMode=TextServer.AutowrapMode.WordSmart;panel.AddChild(name);
            bool owned=PlayerInventory.Owns(kind);panel.AddChild(DeckMenuUi.Text(owned?"OWNED":PlayerInventory.AllUnlocked?"FREE TO PLAY / NOT OWNED":"LOCKED",12));
            if(_shop&&!owned){long price=PlayerInventory.Price(kind);var buy=DeckMenuUi.Button($"BUY / {price} COINS",()=>Purchase(kind),180,34);buy.Disabled=_busy||!PlayerInventory.Ready||price<1||PlayerInventory.Coins<price;panel.AddChild(buy);}
        }
    }
    private async void RefreshCollection()
    {
        if(_busy)return;_busy=true;Render();
        NakamaConnection? c=null;
        try {c=GameAccount.Connection();await c.Connect(realtime:false);await PlayerInventory.Load(c);if(IsInsideTree())_status.Text="Collection updated.";}
        catch(Exception e){if(IsInsideTree())_status.Text="Could not refresh: "+e.Message;}
        finally{if(c!=null)await c.Close();_busy=false;if(IsInsideTree())Render();}
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
