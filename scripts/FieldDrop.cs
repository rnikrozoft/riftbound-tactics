using Godot;

public partial class FieldDrop : Control
{
    public CardShop Shop { get; set; } = null!;
    private Vector2 _pressPosition;
    private bool _dragStarted;
    private CardDrag? _drag;
    private readonly Vector2[][] _polygons = new Vector2[CardShop.FieldLimit][];
    private readonly Vector2[][] _borders = new Vector2[CardShop.FieldLimit][];
    private static readonly Vector2[] Corners = { new(0,-12),new(24,0),new(0,12),new(-24,0) };

    public override void _Ready()
    {
        SetProcess(false);
        for (int i = 0; i < CardShop.FieldLimit; i++) { _polygons[i] = new Vector2[4]; _borders[i] = new Vector2[5]; }
    }
    public override void _Notification(int what)
    {
        if (what == NotificationDragBegin)
        {
            var data = CardDrag.Read(GetViewport().GuiGetDragData());
            _drag = data?.Shop == Shop && (data.Unit != null || !data.FromShop) ? data : null;
            SetProcess(_drag != null);
        }
        else if (what == NotificationDragEnd) { _drag = null; SetProcess(false); QueueRedraw(); }
    }
    public override void _Process(double delta) => QueueRedraw();
    public Vector2 WorldPosition(Vector2 localPosition) =>
        GetViewport().GetCanvasTransform().AffineInverse() * (GetGlobalTransformWithCanvas() * localPosition);
    public override void _Draw()
    {
        if (!Shop.Drafting || _drag == null) return;
        var transform = GetGlobalTransformWithCanvas().AffineInverse() * Shop.Tiles.GetGlobalTransformWithCanvas();
        int hovered = Shop.DeploymentSlot(WorldPosition(GetLocalMousePosition()));
        for (int i = 0; i < CardShop.FieldLimit; i++)
        {
            var center = Shop.SlotCenters[i];
            bool valid = Shop.CanPlace(_drag,Shop.Tiles.ToGlobal(center));
            for (int j = 0; j < 4; j++) _borders[i][j] = _polygons[i][j] = transform * (center + Corners[j]);
            _borders[i][4] = _borders[i][0];
            var color = new Color(1,1,1,valid ? .07f : .03f);
            if (hovered == i) color = valid ? new Color(1,1,1,.24f) : new Color(1,.2f,.2f,.18f);
            DrawColoredPolygon(_polygons[i],color);
            DrawPolyline(_borders[i],new Color(1,1,1,valid ? .4f : .12f),1,false);
        }
    }
    public override void _GuiInput(InputEvent input)
    {
        if (input is not InputEventMouseButton { ButtonIndex: MouseButton.Left } mouse) return;
        if (mouse.Pressed) { _pressPosition = mouse.Position; _dragStarted = false; }
        else if (!_dragStarted && mouse.Position.DistanceTo(_pressPosition) < 8)
        {
            var unit = Shop.PickDetailUnit(WorldPosition(mouse.Position));
            if (unit != null) Shop.ShowUnitDetails(unit); else Shop.CloseDetails();
        }
    }
    public override Variant _GetDragData(Vector2 position)
    {
        var unit = Shop.PickUnit(WorldPosition(position));
        if (unit == null) return default;
        _dragStarted = true;
        var preview = new Control { Position = new(-32,-40), Size = new(64,80) };
        var original = unit.Sprite;
        preview.AddChild(new Sprite2D {
            Texture = original.SpriteFrames.GetFrameTexture(original.Animation,original.Frame), Position = new(32,40),
            Scale = original.Scale * Shop.Camera.Zoom, FlipH = original.FlipH, TextureFilter = TextureFilterEnum.Nearest
        });
        SetDragPreview(preview);
        return new CardDrag { Shop = Shop, Unit = unit };
    }
    public override bool _CanDropData(Vector2 position, Variant value)
    {
        var data = CardDrag.Read(value);
        return data?.Shop == Shop && Shop.CanPlace(data,WorldPosition(position));
    }
    public override void _DropData(Vector2 position, Variant value) => Shop.PlaceCardOrUnit(CardDrag.Read(value)!,WorldPosition(position));
}
