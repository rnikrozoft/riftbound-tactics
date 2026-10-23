using Godot;
using System.Linq;

public partial class ShopCard : TextureRect
{
    public static Vector2 ShopDisplaySize => new(84, MobileLayout.Enabled ? 94 : 98);
    public CardShop Shop { get; set; } = null!;
    public int Token { get; private set; }
    public bool FromShop { get; private set; }
    private CardData? _data;
    private string _cardName="";
    private bool _canDrag, _dragStarted;
    private Vector2 _pressPosition;
    public static void AddCornerStats(Control card,CardData data)
    {
        int kind=0;for(int i=0;i<CardCatalog.Count;i++)if(CardCatalog.Name(i)==data.Name){kind=i;break;}
        var stats=CharacterData.Stats(kind,data.Stars);
        CornerNumber(card,"CardPrice",data.Price,GameUi.Gold,false,false);
        CornerNumber(card,"CardArmor",stats.Armor,new Color("59c9ff"),false,true);
        CornerNumber(card,"CardPower",(stats.Attack>0?stats.Attack:stats.Hp),new Color("ff5262"),true,true);
        card.GetNode<Label>("CardArmor").Visible=card.GetNode<Label>("CardPower").Visible=!CharacterData.IsSupport(kind);
    }
    private static void CornerNumber(Control card,string name,int value,Color color,bool right,bool bottom)
    {
        var label=card.GetNodeOrNull<Label>(name);
        if(label==null){
            label=new Label {Name=name,MouseFilter=MouseFilterEnum.Ignore,HorizontalAlignment=HorizontalAlignment.Center,VerticalAlignment=VerticalAlignment.Center};
            card.AddChild(label);GameUi.Label(label,13);
            var stat=label;card.Resized+=()=>LayoutStat(card,stat,right,bottom);
            label.AddThemeColorOverride("font_outline_color",new Color("101820"));label.AddThemeConstantOverride("outline_size",3);
            label.AddThemeStyleboxOverride("normal",new StyleBoxEmpty());
            var badge=new TextureRect {Name="StatCircle",ShowBehindParent=true,Texture=GameUi.CardCircle(name=="CardPrice"?1:name=="CardPower"?2:0),ExpandMode=TextureRect.ExpandModeEnum.IgnoreSize,StretchMode=TextureRect.StretchModeEnum.KeepAspectCentered,MouseFilter=MouseFilterEnum.Ignore,TextureFilter=TextureFilterEnum.Nearest};
            label.AddChild(badge);badge.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        }
        LayoutStat(card,label,right,bottom);
        label.AddThemeColorOverride("font_color",color);label.Text=value.ToString();
    }
    private static void LayoutStat(Control card,Label label,bool right,bool bottom)
    {
        int width=card.Size.X>=76?30:24;
        label.AddThemeFontSizeOverride("font_size",card.Size.X>=76?15:13);
        label.ClipText=false;
        var badge=label.GetNodeOrNull<TextureRect>("StatCircle");
        if(badge!=null) {
            int radius=card.Size.X>=76?14:11;
            badge.AnchorLeft=badge.AnchorRight=badge.AnchorTop=badge.AnchorBottom=.5f;
            badge.OffsetLeft=badge.OffsetTop=-radius;badge.OffsetRight=badge.OffsetBottom=radius;
        }
        label.AnchorLeft=label.AnchorRight=right?1:0;label.AnchorTop=label.AnchorBottom=bottom?1:0;
        const int overhang=4;
        label.OffsetLeft=right?-width+overhang:-overhang;label.OffsetRight=right?overhang:width-overhang;
        label.OffsetTop=bottom?-width+overhang:-overhang;label.OffsetBottom=bottom?overhang:width-overhang;
    }
    public void Configure(CardData data, bool fromShop, bool draggable)
    {
        if (Texture != data.Texture) Texture = data.Texture;
        StretchMode=StretchModeEnum.Scale;
        _cardName=data.Name;GameUi.DecorateCard(this,data.Name);
        GetNode<TextureRect>("ClassEmblem").Hide();
        Token = data.Token; FromShop = fromShop; _canDrag = draggable;
        _data=data;AddCornerStats(this,data);
        AddHandStars(this,data,fromShop);
        

    }
    private static int CardCatalogKind(CardData data)=>System.Array.FindIndex(CharacterData.All,c=>c.Name==data.Name);
    public static void AddHandStars(Control card,CardData data,bool fromShop)
    {
        var price=card.GetNode<Label>("CardPrice");price.Visible=fromShop;
        var stars=card.GetNodeOrNull<HBoxContainer>("HandStars");
        if(stars==null){stars=new HBoxContainer {Name="HandStars",MouseFilter=MouseFilterEnum.Ignore};card.AddChild(stars);stars.AnchorLeft=stars.AnchorRight=0;stars.OffsetLeft=-4;stars.OffsetRight=24;stars.OffsetTop=-4;stars.AddThemeConstantOverride("separation",-3);}
        stars.Visible=!fromShop&&!CharacterData.IsSupport(CardCatalogKind(data));
        foreach(var child in stars.GetChildren()){stars.RemoveChild(child);child.Free();}
        var badge=new TextureRect {Texture=new AtlasTexture {Atlas=GD.Load<Texture2D>("res://assets/ui/cards/card_frames.png"),Region=new Rect2(667,305,26,30)},ExpandMode=ExpandModeEnum.IgnoreSize,StretchMode=StretchModeEnum.KeepAspectCentered,CustomMinimumSize=new(24,24),MouseFilter=MouseFilterEnum.Ignore};stars.AddChild(badge);
        var count=new GridContainer {Columns=4,MouseFilter=MouseFilterEnum.Ignore,SizeFlagsVertical=SizeFlags.ShrinkCenter};count.AddThemeConstantOverride("h_separation",0);count.AddThemeConstantOverride("v_separation",0);stars.AddChild(count);
        for(int i=0;i<CharacterData.DisplayStars(data.Stars);i++)count.AddChild(new TextureRect {Texture=new AtlasTexture {Atlas=GD.Load<Texture2D>("res://assets/ui/cards/card_frames.png"),Region=new Rect2(399,238,22,22)},ExpandMode=ExpandModeEnum.IgnoreSize,StretchMode=StretchModeEnum.KeepAspectCentered,CustomMinimumSize=new(12,14),MouseFilter=MouseFilterEnum.Ignore});
    }
    public override bool _CanDropData(Vector2 position, Variant data) => FromShop && Shop.CanSell(CardDrag.Read(data));
    public override void _DropData(Vector2 position, Variant data) => Shop.SellDrop(CardDrag.Read(data)!);
    public override void _GuiInput(InputEvent input)
    {
        if (input is not InputEventMouseButton { ButtonIndex: MouseButton.Left } mouse) return;
        AcceptEvent();
        if (mouse.Pressed) { _pressPosition = mouse.Position; _dragStarted = false; }
        else if (!_dragStarted && mouse.Position.DistanceTo(_pressPosition) < 8) Shop.ShowCardDetails(Token);
    }
    public override Variant _GetDragData(Vector2 position)
    {
        if (!_canDrag || !(FromShop ? Shop.AcceptsToken(Token) : Shop.AcceptsHandToken(Token))) return default;
        _dragStarted = true;
        var preview = new TextureRect {
            Texture = Texture, ExpandMode = ExpandModeEnum.IgnoreSize, StretchMode = StretchModeEnum.Scale,
            Size = Size, Position = -Size * .5f, TextureFilter = TextureFilterEnum.Nearest
        };
        GameUi.DecorateCard(preview,_cardName);preview.GetNode<TextureRect>("ClassEmblem").Hide();
        if(_data!=null){AddCornerStats(preview,_data);AddHandStars(preview,_data,FromShop);}
        SetDragPreview(preview);
        return new CardDrag { Shop = Shop, Token = Token, FromShop = FromShop };
    }
}
