using Godot;

// An ambient world overlay below the HUD; it never intercepts clicks or drag/drop.
public partial class ArenaCloudShadows : CanvasLayer
{
    public ShaderMaterial Material {get;private set;}=null!;
    private Vector2 _referenceOrigin,_referenceSize;
    public ColorRect Surface {get;private set;}=null!;
    public override void _Ready()
    {
        Layer=1;
        Material=new ShaderMaterial {Shader=GD.Load<Shader>("res://shaders/arena_cloud_shadows.gdshader")};
        Surface=new ColorRect {Name="CloudShadowSurface",Material=Material,MouseFilter=Control.MouseFilterEnum.Ignore};
        AddChild(Surface);Surface.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.TopLeft);
        var inverse=GetParent<Node2D>().GetGlobalTransformWithCanvas().AffineInverse();
        _referenceOrigin=inverse*Vector2.Zero;
        var size=GetViewport().GetVisibleRect().Size;
        _referenceSize=new Vector2((inverse.X*size.X).Length(),(inverse.Y*size.Y).Length());
        Material.SetShaderParameter("reference_origin",_referenceOrigin);
        Material.SetShaderParameter("reference_size",_referenceSize);
        UpdateShader();
    }
    public override void _Process(double delta)=>UpdateShader();
    private void UpdateShader()
    {
        if(Material==null)return;
        UpdateProjection();
        // Ambient weather keeps its gentle pace when combat playback is accelerated.
        Material.SetShaderParameter("elapsed_seconds",(float)(Time.GetTicksMsec()/1000.0%3600));
    }
    public void UpdateProjection()
    {
        var size=GetViewport().GetVisibleRect().Size;
        Surface.Position=Vector2.Zero;Surface.Size=size;
        Material.SetShaderParameter("viewport_size",size);
        var inverse=GetParent<Node2D>().GetGlobalTransformWithCanvas().AffineInverse();
        Material.SetShaderParameter("screen_to_field_x",inverse.X);
        Material.SetShaderParameter("screen_to_field_y",inverse.Y);
        Material.SetShaderParameter("screen_to_field_origin",inverse.Origin);
    }
}
