using Godot;

public partial class ShopZone : Control
{
    public CardShop Shop { get; set; } = null!;
    public override bool _CanDropData(Vector2 position, Variant data) => Shop.CanSell(CardDrag.Read(data));
    public override void _DropData(Vector2 position, Variant data) => Shop.SellDrop(CardDrag.Read(data)!);
    public override void _GuiInput(InputEvent input)
    {
        if (input is InputEventMouseButton { ButtonIndex: MouseButton.Left, Pressed: false } && !GetViewport().GuiIsDragging())
            Shop.CloseDetails();
    }
}
