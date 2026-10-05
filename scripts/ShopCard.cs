using Godot;

public partial class ShopCard : TextureRect
{
    public CardShop Shop { get; set; } = null!;
    public int Token { get; private set; }
    public bool FromShop { get; private set; }
    private Label? _price;
    private string _cardName="";
    private bool _canDrag, _dragStarted;
    private Vector2 _pressPosition;
    private readonly TextureRect[] _stars = new TextureRect[4];
    public void Configure(CardData data, bool fromShop, bool draggable)
    {
        if (Texture != data.Texture) Texture = data.Texture;
        _cardName=data.Name;GameUi.DecorateCard(this,data.Name);
        GetNode<TextureRect>("ClassEmblem").Hide();
        Token = data.Token; FromShop = fromShop; _canDrag = draggable;
        for(int i=0;i<4;i++) {
            if(_stars[i] == null) { _stars[i] = new TextureRect { Name=$"Star{i+1}",Texture = TravelBookUi.Texture("IconStar01a"), Position=new(5+i*13,5), Size=new(12,12), ExpandMode=ExpandModeEnum.IgnoreSize, StretchMode=StretchModeEnum.KeepAspectCentered, MouseFilter=MouseFilterEnum.Ignore }; AddChild(_stars[i]); }
            _stars[i].Visible = i < data.Stars;
        }
        if (_price==null) {
            _price=new Label {AnchorTop=1,AnchorRight=1,AnchorBottom=1,OffsetTop=-20,OffsetLeft=4,OffsetRight=-4,MouseFilter=MouseFilterEnum.Ignore,HorizontalAlignment=HorizontalAlignment.Center};
            GameUi.Label(_price,12);_price.AddThemeColorOverride("font_color",GameUi.Gold);_price.AddThemeColorOverride("font_shadow_color",Colors.Black);_price.AddThemeConstantOverride("shadow_offset_x",1);_price.AddThemeConstantOverride("shadow_offset_y",1);AddChild(_price);
        }
        _price.Text=fromShop?$"{data.Price} GOLD":"";_price.Visible=fromShop;
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
        for(int i=0;i<4;i++)if(_stars[i].Visible){var star=GameUi.Icon("IconStar01a",12);star.Position=new(5+i*13,5);star.Size=new(12,12);preview.AddChild(star);}
        SetDragPreview(preview);
        return new CardDrag { Shop = Shop, Token = Token, FromShop = FromShop };
    }
}
