using Godot;

// A smaller logical viewport increases physical UI size while containers reflow normally.
public partial class MobileLayout : Node
{
    public static bool Enabled=>OS.HasFeature("ios")||OS.HasFeature("android")||OS.GetEnvironment("RIFT_MOBILE_LAYOUT_TEST")=="1";
    public override void _Ready()
    {
        if(!Enabled)return;
        GetWindow().ContentScaleSize=new Vector2I(960,540);
        GetWindow().ContentScaleAspect=Window.ContentScaleAspectEnum.Expand;
        GetViewport().GlobalCanvasTransform=Transform2D.Identity;
    }
}
