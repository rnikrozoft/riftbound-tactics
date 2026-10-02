using Godot;

public partial class SafeArea : MarginContainer
{
    [Export(PropertyHint.Range, "0,64,1")] public int EdgePadding { get; set; } = 16;
    private bool _mobile;
    private double _elapsed;
    private readonly StringName[] _names = { "margin_left", "margin_top", "margin_right", "margin_bottom" };
    private readonly int[] _margins = { -1, -1, -1, -1 };
    public override void _Ready()
    {
        _mobile = OS.HasFeature("android") || OS.HasFeature("ios");
        SetProcess(_mobile);
        Resized += UpdateSafeArea;
        Callable.From(UpdateSafeArea).CallDeferred();
    }
    public override void _Process(double delta)
    {
        _elapsed += delta;
        if (_elapsed < 0.25) return;
        _elapsed = 0;
        UpdateSafeArea();
    }
    private void UpdateSafeArea()
    {
        var insets = Vector4.Zero;
        if (_mobile)
        {
            Vector2 displaySize = DisplayServer.WindowGetSize();
            var safe = ((Rect2)DisplayServer.GetDisplaySafeArea()).Intersection(new Rect2(Vector2.Zero, displaySize));
            if (displaySize.X > 0 && displaySize.Y > 0 && safe.HasArea())
            {
                var transform = GetViewport().GetScreenTransform() * GetGlobalTransformWithCanvas();
                var local = (transform.AffineInverse() * safe).Intersection(new Rect2(Vector2.Zero, Size));
                insets = new(Mathf.Max(0, local.Position.X), Mathf.Max(0, local.Position.Y),
                    Mathf.Max(0, Size.X - local.End.X), Mathf.Max(0, Size.Y - local.End.Y));
            }
        }
        for (int i = 0; i < 4; i++)
        {
            int margin = Mathf.CeilToInt(insets[i]) + EdgePadding;
            if (_margins[i] == margin) continue;
            _margins[i] = margin;
            AddThemeConstantOverride(_names[i], margin);
        }
    }
}
