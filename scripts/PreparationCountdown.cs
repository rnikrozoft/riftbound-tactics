using Godot;

public partial class PreparationCountdown : Control
{
    public int Seconds { get; private set; } = -1;
    public bool CenterBurst {get;set;}
    private Label _number=null!;
    private CpuParticles2D? _particles;
    private Tween? _pulse;
    public override void _Ready()
    {
        MouseFilter=MouseFilterEnum.Ignore;
        if(CenterBurst) {
            AnchorLeft=AnchorRight=AnchorTop=AnchorBottom=.5f;
            OffsetLeft=OffsetTop=-110;OffsetRight=OffsetBottom=110;
            _particles=new CpuParticles2D {Position=new(110,110),Emitting=false,OneShot=true,Amount=32,Lifetime=.65,Explosiveness=1,Direction=Vector2.Up,Spread=180,Gravity=Vector2.Zero,InitialVelocityMin=100,InitialVelocityMax=220,ScaleAmountMin=.3f,ScaleAmountMax=.6f,Texture=GD.Load<Texture2D>("res://assets/effects/pixel/frames/explosions/stylized_explosion_001/stylized_explosion_001_small_yellow/frame0004.png")};AddChild(_particles);
        }
        _number=new Label {Name="Time",HorizontalAlignment=HorizontalAlignment.Center,VerticalAlignment=VerticalAlignment.Center,MouseFilter=MouseFilterEnum.Ignore};
        GameUi.Label(_number,CenterBurst?120:28);_number.AddThemeColorOverride("font_color",GameUi.Gold);
        AddChild(_number);_number.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);Hide();
    }
    public void UpdateCountdown(int seconds,bool active)
    {
        if(_number==null)return;
        Visible=active && seconds>0 && (!CenterBurst || seconds<=15);
        if(!Visible){Seconds=-1;if(_particles!=null)_particles.Emitting=false;_pulse?.Kill();return;}
        if(Seconds==seconds)return;
        Seconds=seconds;
        _number.Text=CenterBurst?seconds.ToString():$"{seconds:00}s";
        if(CenterBurst){_number.PivotOffset=new(110,110);_number.Scale=Vector2.One*1.3f;_pulse?.Kill();_pulse=CreateTween();_pulse.TweenProperty(_number,"scale",Vector2.One,.35).SetTrans(Tween.TransitionType.Back).SetEase(Tween.EaseType.Out);_particles!.Restart();_particles.Emitting=true;}
        _number.AddThemeColorOverride("font_color",seconds<=10?new Color("ed8d7b"):GameUi.Gold);
    }
}
