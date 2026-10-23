using Godot;

public partial class MenuButtonSkin : Control
{
    public TextureButton Button {get;set;}=null!;
    public bool Primary {get;set;}
    public bool Selected {get;set;}
    private int _state=-1;
    public override void _Process(double delta)
    {
        int state=(Button.Disabled?1:0)+(Button.IsHovered()?2:0)+(Button.IsPressed()?4:0)+(Button.HasFocus()?8:0)+(Selected?16:0)+(Primary?32:0);
        if(state==_state)return;_state=state;QueueRedraw();
    }
    public override void _Draw()
    {
        if(Button==null)return;
        var box=MenuUi.Box(Selected||Button.HasFocus(),0);
        box.CornerRadiusTopLeft=box.CornerRadiusTopRight=box.CornerRadiusBottomLeft=box.CornerRadiusBottomRight=8;
        box.BgColor=Primary?MenuUi.Accent:Selected?new Color("183a3c"):Button.IsHovered()?MenuUi.Raised:MenuUi.Surface;
        if(Button.IsPressed())box.BgColor=box.BgColor.Darkened(.15f);
        if(Button.Disabled){box.BgColor=MenuUi.Raised;box.BorderColor=MenuUi.Line;}
        box.Draw(GetCanvasItem(),new Rect2(Vector2.Zero,Size));
        var textColor=Button.Disabled?MenuUi.Muted:Primary?MenuUi.Background:Selected?MenuUi.Accent:MenuUi.White;
        foreach(var node in Button.GetChildren())if(node is Label label)
        {
            label.Modulate=Colors.White;
            label.AddThemeColorOverride("font_color",textColor);
        }
    }
}
