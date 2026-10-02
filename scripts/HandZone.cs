using Godot;

public partial class HandZone : Control
{
    public CardShop Shop { get; set; } = null!;
    public override void _GuiInput(InputEvent input)
    {
        if (input is InputEventMouseButton { ButtonIndex: MouseButton.Left, Pressed: false } && !GetViewport().GuiIsDragging())
            Shop.CloseDetails();
    }
    public override bool _CanDropData(Vector2 position, Variant value)
    {
        var data = CardDrag.Read(value);
        if (data?.Shop != Shop) return false;
        return data.Unit != null ? Shop.CanReturnUnit(data.Unit) : data.FromShop && Shop.AcceptsToken(data.Token);
    }
    public override void _DropData(Vector2 position, Variant value)
    {
        var data = CardDrag.Read(value)!;
        if (data.Unit != null) Shop.ReturnUnit(data.Unit);
        else Shop.TakeCard(data.Token);
    }
}
