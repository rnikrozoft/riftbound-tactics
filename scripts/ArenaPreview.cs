using Godot;

// Reuses the authored arena tiles and characters as a living menu backdrop.
public partial class ArenaPreview : SubViewportContainer
{
    public override void _Ready()
    {
        Stretch=true;MouseFilter=MouseFilterEnum.Ignore;
        var viewport=new SubViewport {Size=new(640,220),TransparentBg=true,Disable3D=true,RenderTargetUpdateMode=SubViewport.UpdateMode.Always};AddChild(viewport);
        var arena=new Node2D {YSortEnabled=true};viewport.AddChild(arena);
        var source=GD.Load<PackedScene>("res://scenes/main.tscn").Instantiate<Node2D>();
        foreach(var child in source.GetChildren()) {
            if(child is TileMapLayer || child.Name=="Ally_01" || child.Name=="Enemy_01") {
                child.Owner=null;source.RemoveChild(child);arena.AddChild(child);
            }
        }
        source.Free();
        var camera=new Camera2D {Position=new(458,407),Zoom=new(.85f,.85f)};arena.AddChild(camera);
    }
}
