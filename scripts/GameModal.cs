using Godot;
using System;

public partial class GameModal : Control
{
    public VBoxContainer Body {get;private set;}=null!;
    public Action? Cancel {get;set;}
    public Tween FadeIn(double seconds)
    {
        var tween=CreateTween().SetIgnoreTimeScale(true).SetParallel().SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.InOut);
        foreach(var node in GetChildren())if(node is Control control&&control.Name!="DefeatEffect"){
            control.Modulate=new Color(1,1,1,0);tween.TweenProperty(control,"modulate:a",1f,seconds);
        }
        return tween;
    }
    public override void _Ready()
    {
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);MouseFilter=MouseFilterEnum.Stop;
        var shade=new ColorRect {Color=new Color(0,0,0,.7f),MouseFilter=MouseFilterEnum.Stop};AddChild(shade);shade.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        var center=new CenterContainer {MouseFilter=MouseFilterEnum.Ignore};AddChild(center);center.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        var panel=new PanelContainer {CustomMinimumSize=new(380,0),MouseFilter=MouseFilterEnum.Stop};panel.AddThemeStyleboxOverride("panel",GameUi.Box(true,24));center.AddChild(panel);
        Body=new VBoxContainer();Body.AddThemeConstantOverride("separation",16);panel.AddChild(Body);
    }
    public override void _UnhandledKeyInput(InputEvent input)
    {
        if(Visible && input is InputEventKey {Keycode:Key.Escape,Pressed:true} && Cancel!=null){Cancel();GetViewport().SetInputAsHandled();}
    }
}
