using Godot;

public partial class ShopCard : TextureRect
{
    public CardShop Shop { get; set; } = null!;
    public int Token { get; private set; }
    public bool FromShop { get; private set; }
    private bool _canDrag, _dragStarted;
    private Vector2 _pressPosition;
    private readonly TextureRect[] _stars = new TextureRect[4];
    public void Configure(CardData data, bool fromShop, bool draggable)
    {
        Token = data.Token; FromShop = fromShop; _canDrag = draggable;
        for(int i=0;i<4;i++) {
            if(_stars[i] == null) { _stars[i] = new TextureRect { Texture = TravelBookUi.Texture("IconStar01a"), Position=new(5+i*13,5), Size=new(12,12), ExpandMode=ExpandModeEnum.IgnoreSize, StretchMode=StretchModeEnum.KeepAspectCentered, MouseFilter=MouseFilterEnum.Ignore }; AddChild(_stars[i]); }
            _stars[i].Visible = !fromShop && i < data.Stars;
        }
        if (Texture != data.Texture) Texture = data.Texture;
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
        SetDragPreview(preview);
        return new CardDrag { Shop = Shop, Token = Token, FromShop = FromShop };
    }
}
