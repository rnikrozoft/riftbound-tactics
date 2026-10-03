using Godot;

// Only GUI drag payloads cross Godot's Variant boundary. Gameplay uses typed data.
public partial class CardDrag : RefCounted
{
    public CardShop Shop { get; init; } = null!;
    public int Token { get; init; } = -1;
    public bool FromShop { get; init; }
    public BattleUnit? Unit { get; init; }
    public static CardDrag? Read(Variant value) => value.VariantType == Variant.Type.Object ? value.AsGodotObject() as CardDrag : null;
}
public sealed record CardData(int Token, string Name, Texture2D Texture, int Price = 2, int Stars = 1, int Paid = 0, bool Veteran = false)
{
    public int Investment => Paid > 0 ? Paid : Price;
}
public sealed record Deployment(CardData Card, BattleUnit Unit);
