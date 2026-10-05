using Godot;

public static class DamageNumber
{
    public static void Play(BattleUnit target,int damage)
    {
        if(damage<=0 || !target.IsInsideTree())return;
        var parent=target.GetParent<Node2D>();
        var effect=new Node2D {Name="DamageNumber",ZIndex=100,TextureFilter=CanvasItem.TextureFilterEnum.Nearest};
        parent.AddChild(effect,true);effect.GlobalPosition=target.ToGlobal(new Vector2(0,-60));
        var number=new Label {Name="Value",Text=$"-{damage}",Position=new(-40,-14),Size=new(80,28),HorizontalAlignment=HorizontalAlignment.Center,VerticalAlignment=VerticalAlignment.Center,MouseFilter=Control.MouseFilterEnum.Ignore,PivotOffset=new(40,14)};
        number.AddThemeFontSizeOverride("font_size",20);
        number.AddThemeColorOverride("font_color",new Color("ffad82"));
        number.AddThemeColorOverride("font_outline_color",new Color("23151b"));number.AddThemeConstantOverride("outline_size",4);
        effect.AddChild(number);number.Scale=Vector2.One*.65f;
        var sparks=new CpuParticles2D {Name="Sparks",OneShot=true,Emitting=false,Amount=8,Lifetime=.35,Explosiveness=1,Direction=Vector2.Up,Spread=100,Gravity=new(0,70),InitialVelocityMin=25,InitialVelocityMax=55,ScaleAmountMin=.08f,ScaleAmountMax=.16f,Color=new Color("ffad82"),Texture=TravelBookUi.Texture("IconStar01a")};effect.AddChild(sparks);sparks.Emitting=true;
        var pulse=effect.CreateTween();pulse.TweenProperty(number,"scale",Vector2.One,.13).SetTrans(Tween.TransitionType.Back).SetEase(Tween.EaseType.Out);
        var rise=effect.CreateTween();rise.TweenProperty(effect,"position",effect.Position+new Vector2((float)GD.RandRange(-6,6),-30),.85).SetTrans(Tween.TransitionType.Quad).SetEase(Tween.EaseType.Out);
        var fade=effect.CreateTween();fade.TweenInterval(.3);fade.TweenProperty(effect,"modulate:a",0f,.55);fade.TweenCallback(Callable.From(effect.QueueFree));
    }
}
