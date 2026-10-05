using Godot;

// Presentation only: leaves the server clock running normally.
public partial class FinishingFocus : Node
{
    public bool Active { get; private set; }
    public bool ChargePlaying => GodotObject.IsInstanceValid(_charge) && _charge!.IsPlaying();
    private Transform2D _home;
    private Tween? _zoom;
    private AnimatedSprite2D? _charge, _impact;
    private static SpriteFrames? _chargeFrames, _impactFrames;

    private static SpriteFrames LoadFrames(string folder, int count, float fps)
    {
        var frames = new SpriteFrames();
        frames.AddAnimation(BattleAnimations.Effect);
        frames.SetAnimationLoopMode(BattleAnimations.Effect,SpriteFrames.LoopMode.None);
        frames.SetAnimationSpeed(BattleAnimations.Effect,fps);
        for (int i=0;i<count;i++) frames.AddFrame(BattleAnimations.Effect,GD.Load<Texture2D>($"res://assets/effects/pixel/frames/{folder}/frame{i:0000}.png"));
        return frames;
    }

    public Tween Begin(BattleUnit attacker)
    {
        Reset();
        Active = true;
        _home = GetViewport().CanvasTransform;
        _chargeFrames ??= LoadFrames("fantasy_spells/spell_attack_up_001/spell_attack_up_001_small_red",18,30);
        _charge = new AnimatedSprite2D {
            Name="FinisherAttackUpSmall", Position=new(0,-15), ZIndex=25,
            SpriteFrames=_chargeFrames, TextureFilter=CanvasItem.TextureFilterEnum.Nearest
        };
        attacker.AddChild(_charge);
        _charge.Play(BattleAnimations.Effect);
        var focus = attacker.GlobalTransform * new Vector2(0,-17);
        var enlarged = new Transform2D(_home.X * 1.65f, _home.Y * 1.65f, Vector2.Zero);
        enlarged.Origin = GetViewport().GetVisibleRect().Size / 2 - enlarged.BasisXform(focus);
        _zoom = CreateTween().SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.InOut);
        _zoom.TweenMethod(Callable.From<float>(v => GetViewport().CanvasTransform = _home.InterpolateWith(enlarged,v)),0f,1f,.22);
        return _zoom;
    }

    public Tween ZoomOut()
    {
        if (GodotObject.IsInstanceValid(_charge)) _charge!.QueueFree();
        _charge=null;
        _zoom?.Kill();
        var current = GetViewport().CanvasTransform;
        _zoom = CreateTween().SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.InOut);
        _zoom.TweenMethod(Callable.From<float>(v => GetViewport().CanvasTransform = current.InterpolateWith(_home,v)),0f,1f,.22);
        _zoom.TweenCallback(Callable.From(() => GetViewport().CanvasTransform = _home));
        return _zoom;
    }

    public void Impact(BattleUnit target)
    {
        _impactFrames ??= LoadFrames("impacts/symmetrical_impact_001/symmetrical_impact_001_large_yellow",7,15);
        _impact = new AnimatedSprite2D { Name="FinisherSymmetricalImpactLarge", Position=new(0,-15), ZIndex=25, SpriteFrames=_impactFrames };
        target.AddChild(_impact);
        _impact.Play(BattleAnimations.Effect);
    }

    public void Reset()
    {
        _zoom?.Kill(); _zoom = null;
        if (Active && IsInsideTree()) GetViewport().CanvasTransform = _home;
        if (GodotObject.IsInstanceValid(_charge)) _charge!.QueueFree();
        if (GodotObject.IsInstanceValid(_impact)) _impact!.QueueFree();
        _charge = null; _impact = null; Active = false;
    }
    public override void _ExitTree() => Reset();
}
