using Godot;
using System;
using System.Threading.Tasks;
public partial class ArenaCloudShadowsRunner:Node
{
    private async Task Render(){for(int i=0;i<3;i++)await ToSignal(GetTree(),SceneTree.SignalName.ProcessFrame);await ToSignal(RenderingServer.Singleton,RenderingServer.SignalName.FramePostDraw);}
    public override async void _Ready(){try{
        var field=GD.Load<PackedScene>("res://scenes/main.tscn").Instantiate<BattleDisplay>();field.GetNode("OnlineBattle").Free();field.GetNode<BattleDemo>("BattleDemo").AutoStart=false;AddChild(field);
        var clouds=field.GetNode<ArenaCloudShadows>("CloudShadows");
        if(clouds.Layer>=field.GetNode<CanvasLayer>("UI").Layer||clouds.Surface.MouseFilter!=Control.MouseFilterEnum.Ignore)throw new Exception("Cloud shadows must stay below HUD and allow input");
        field.ProcessMode=ProcessModeEnum.Disabled;
        clouds.SetProcess(false);
        if(clouds.Surface.Size.X<1200||clouds.Surface.Size.Y<700)throw new Exception("Cloud surface does not cover the viewport");
        clouds.Surface.Hide();await Render();
        using var clear=GetViewport().GetTexture().GetImage();clear.SavePng("/tmp/riftbound-cloud-shadows-off.png");clouds.Surface.Show();clouds.Material.SetShaderParameter("elapsed_seconds",0f);await Render();
        using var first=GetViewport().GetTexture().GetImage();first.SavePng("/tmp/riftbound-cloud-shadows-0.png");
        clouds.Material.SetShaderParameter("elapsed_seconds",22f);await Render();
        using var later=GetViewport().GetTexture().GetImage();later.SavePng("/tmp/riftbound-cloud-shadows-22.png");
        int shadowed=0;int dark=0;
        for(int y=0;y<later.GetHeight();y+=8)for(int x=0;x<later.GetWidth();x+=8)if(clear.GetPixel(x,y).R-later.GetPixel(x,y).R>.025f)shadowed++;
        for(int y=0;y<later.GetHeight();y+=8)for(int x=0;x<later.GetWidth();x+=8)if(clear.GetPixel(x,y).R-later.GetPixel(x,y).R>.09f)dark++;
        if(dark<100)throw new Exception("Cloud shapes must remain visible: "+dark);
        if(shadowed<150)throw new Exception("Cloud shadows are not visibly darkening the scene: "+shadowed);
        int changed=0;for(int y=200;y<500;y+=8)for(int x=300;x<1000;x+=8)if(Mathf.Abs(first.GetPixel(x,y).R-later.GetPixel(x,y).R)>.005f)changed++;
        if(changed<50)throw new Exception("Cloud shadows do not drift across the ground");
        var hudLayer=field.GetNode<CanvasLayer>("UI");hudLayer.Hide();
        var backdropLayer=new CanvasLayer {Layer=0};AddChild(backdropLayer);
        var white=new ColorRect {Color=Colors.White,MouseFilter=Control.MouseFilterEnum.Ignore};backdropLayer.AddChild(white);white.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        await Render();using var normalMask=GetViewport().GetTexture().GetImage();
        var originalScale=field.Scale;var originalPosition=field.Position;var beforeTransform=field.GetGlobalTransformWithCanvas();
        var center=GetViewport().GetVisibleRect().Size/2;
        field.Scale*=1.5f;field.Position=center+(field.Position-center)*1.5f;clouds.UpdateProjection();
        await Render();using var zoomMask=GetViewport().GetTexture().GetImage();zoomMask.SavePng("/tmp/riftbound-cloud-shadows-zoom-mask.png");
        var remap=field.GetGlobalTransformWithCanvas()*beforeTransform.AffineInverse();int checkedPixels=0,mismatched=0;
        for(int y=20;y<normalMask.GetHeight()-20;y+=12)for(int x=20;x<normalMask.GetWidth()-20;x+=12){
            var zoomed=remap*new Vector2(x,y);int zx=Mathf.RoundToInt(zoomed.X),zy=Mathf.RoundToInt(zoomed.Y);
            if(zx<2||zy<2||zx>=zoomMask.GetWidth()-2||zy>=zoomMask.GetHeight()-2)continue;
            checkedPixels++;if(Mathf.Abs(normalMask.GetPixel(x,y).R-zoomMask.GetPixel(zx,zy).R)>.04f)mismatched++;
        }
        if(checkedPixels<100||mismatched>checkedPixels*.05f)throw new Exception($"Clouds do not follow field zoom: {mismatched}/{checkedPixels}");
        field.Scale=originalScale;field.Position=originalPosition;clouds.UpdateProjection();backdropLayer.Hide();hudLayer.Show();
        var hudProbe=new ColorRect {Color=Colors.White,MouseFilter=Control.MouseFilterEnum.Ignore};field.GetNode<CanvasLayer>("UI").AddChild(hudProbe);hudProbe.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        await Render();using var hud=GetViewport().GetTexture().GetImage();
        for(int y=0;y<hud.GetHeight();y+=32)for(int x=0;x<hud.GetWidth();x+=32)if(hud.GetPixel(x,y).R<.999f)throw new Exception("Cloud shadows tint the HUD");
        GD.Print("CLOUD SHADOWS PASS: diagonal animated ground shading, follows field zoom, below HUD, input transparent");GetTree().Quit();
    }catch(Exception e){GD.PushError(e.ToString());GetTree().Quit(1);}}
}
