using Godot;

public partial class PreparationCountdown : Control
{
    public int Seconds { get; private set; } = -1;
    private Label _number = null!;
    private CpuParticles2D _particles = null!;
    private Tween? _pulse;
    public override void _Ready()
    {
        MouseFilter = MouseFilterEnum.Ignore;
        AnchorLeft = AnchorRight = AnchorTop = AnchorBottom = .5f;
        OffsetLeft = OffsetTop = -110; OffsetRight = OffsetBottom = 110;
        _particles = new CpuParticles2D {
            Position = new(110,110), Emitting = false, OneShot = true, Amount = 32,
            Lifetime = .65, Explosiveness = 1, Direction = Vector2.Up, Spread = 180,
            Gravity = Vector2.Zero, InitialVelocityMin = 100, InitialVelocityMax = 220,
            ScaleAmountMin = .3f, ScaleAmountMax = .6f,
            Texture = GD.Load<Texture2D>("res://assets/Super Pixel Effects Gigapack (Free Version)/PNG/Explosions/stylized_explosion_001/stylized_explosion_001_small_yellow/frame0004.png")
        };
        AddChild(_particles);
        _number = new Label { HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center, MouseFilter = MouseFilterEnum.Ignore,
            PivotOffset = new(110,110) };
        AddChild(_number); _number.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        _number.AddThemeFontSizeOverride("font_size", 120);
        _number.AddThemeColorOverride("font_color", new Color(1,.85f,.4f));
        _number.AddThemeColorOverride("font_outline_color", new Color(.2f,.08f,.03f));
        _number.AddThemeConstantOverride("outline_size", 6);
        Hide();
    }
    public void UpdateCountdown(int seconds, bool active)
    {
        if (_number == null) return;
        bool show = active && seconds > 0 && seconds <= 15;
        Visible = show;
        if (!show) { Seconds = -1; _particles.Emitting = false; _pulse?.Kill(); return; }
        if (Seconds == seconds) return;
        Seconds = seconds; _number.Text = seconds.ToString();
        _pulse?.Kill(); _number.Scale = Vector2.One * 1.3f;
        _pulse = CreateTween(); _pulse.TweenProperty(_number, "scale", Vector2.One, .35).SetTrans(Tween.TransitionType.Back).SetEase(Tween.EaseType.Out);
        _particles.Restart(); _particles.Emitting = true;
    }
}
