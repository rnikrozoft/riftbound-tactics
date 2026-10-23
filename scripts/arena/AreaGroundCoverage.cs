using Godot;
using System;

// Extend the supplied flat ground tile to viewport edges after camera/size changes.
public partial class AreaGroundCoverage : TileMapLayer
{
    private Vector2 _lastSize;
    private Transform2D _lastTransform;
    public override void _Ready()=>CoverViewport();
    public override void _Process(double delta)
    {
        if(IsVisibleInTree()&&(_lastSize!=GetViewportRect().Size||_lastTransform!=GetGlobalTransformWithCanvas()))CoverViewport();
    }
    private void CoverViewport()
    {
        _lastSize=GetViewportRect().Size;_lastTransform=GetGlobalTransformWithCanvas();
        var inverse=_lastTransform.AffineInverse();
        var min=new Vector2I(int.MaxValue,int.MaxValue);var max=new Vector2I(int.MinValue,int.MinValue);
        foreach(var corner in new[]{Vector2.Zero,new Vector2(_lastSize.X,0),_lastSize,new Vector2(0,_lastSize.Y)})
        {
            var cell=LocalToMap(inverse*corner);
            min=new(Math.Min(min.X,cell.X),Math.Min(min.Y,cell.Y));max=new(Math.Max(max.X,cell.X),Math.Max(max.Y,cell.Y));
        }
        for(int x=min.X-2;x<=max.X+2;x++)for(int y=min.Y-2;y<=max.Y+2;y++)
            if(GetCellSourceId(new Vector2I(x,y))<0)SetCell(new Vector2I(x,y),0,new Vector2I(2,2));
    }
}
