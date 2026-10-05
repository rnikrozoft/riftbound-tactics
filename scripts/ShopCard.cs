using Godot;

public partial class ShopCard : TextureRect
{
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
        CornerNumber(card,"CardPower",stats.Hp,new Color("ff5262"),true,true);
    }
    private static void CornerNumber(Control card,string name,int value,Color color,bool right,bool bottom)
    {
        var label=card.GetNodeOrNull<Label>(name);
        if(label==null){
            label=new Label {Name=name,MouseFilter=MouseFilterEnum.Ignore,HorizontalAlignment=HorizontalAlignment.Center,VerticalAlignment=VerticalAlignment.Center};
            card.AddChild(label);GameUi.Label(label,13);
            label.AddThemeColorOverride("font_outline_color",new Color("101820"));label.AddThemeConstantOverride("outline_size",3);
            label.AddThemeStyleboxOverride("normal",new StyleBoxFlat {BgColor=new Color("18232fee"),CornerRadiusTopLeft=3,CornerRadiusTopRight=3,CornerRadiusBottomLeft=3,CornerRadiusBottomRight=3});
        }
        label.AnchorLeft=label.AnchorRight=right?1:0;label.AnchorTop=label.AnchorBottom=bottom?1:0;
        label.OffsetLeft=right?-28:4;label.OffsetRight=right?-4:28;label.OffsetTop=bottom?-23:4;label.OffsetBottom=bottom?-3:24;
        label.AddThemeColorOverride("font_color",color);label.Text=value.ToString();
    }
    public void Configure(CardData data, bool fromShop, bool draggable)
    {
        if (Texture != data.Texture) Texture = data.Texture;
        _cardName=data.Name;GameUi.DecorateCard(this,data.Name);
        GetNode<TextureRect>("ClassEmblem").Hide();
        Token = data.Token; FromShop = fromShop; _canDrag = draggable;
        _data=data;AddCornerStats(this,data);
        TooltipText = $"{data.Name} / {data.Stars} stars / Buy {data.Price} coin / Sell {data.Investment / 2} coin";

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
            Texture = Texture, ExpandMode = ExpandModeEnum.IgnoreSize, StretchMode = StretchModeEnum.KeepAspectCentered,
            Size = Size, Position = -Size * .5f, TextureFilter = TextureFilterEnum.Nearest
        };
        GameUi.DecorateCard(preview,_cardName);preview.GetNode<TextureRect>("ClassEmblem").Hide();
        if(_data!=null)AddCornerStats(preview,_data);
        SetDragPreview(preview);
        return new CardDrag { Shop = Shop, Token = Token, FromShop = FromShop };
    }
}
