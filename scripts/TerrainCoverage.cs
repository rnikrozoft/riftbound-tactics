using Godot;
using System;

// Extend the actual grass tiles to the viewport edges as the window/camera changes.
public partial class TerrainCoverage : TileMapLayer
{
    private Vector2 _lastSize;
    private Transform2D _lastTransform;
    public override void _Ready()=>CoverViewport();
    public override void _Process(double delta)
    {
        if(IsVisibleInTree() && (_lastSize!=GetViewportRect().Size || _lastTransform!=GetGlobalTransformWithCanvas())) CoverViewport();
    }
    public void CoverViewport()
    {
        _lastSize=GetViewportRect().Size;
        _lastTransform=GetGlobalTransformWithCanvas();
        var inverse=_lastTransform.AffineInverse();
        var corners=new[]{Vector2.Zero,new Vector2(_lastSize.X,0),_lastSize,new Vector2(0,_lastSize.Y)};
        var min=new Vector2I(int.MaxValue,int.MaxValue);var max=new Vector2I(int.MinValue,int.MinValue);
        foreach(var corner in corners) {
            var cell=LocalToMap(inverse*corner);
            min=new(Math.Min(min.X,cell.X),Math.Min(min.Y,cell.Y));
            max=new(Math.Max(max.X,cell.X),Math.Max(max.Y,cell.Y));
        }
        for(int x=min.X-4;x<=max.X+4;x++) for(int y=min.Y-4;y<=max.Y+4;y++) {
            var cell=new Vector2I(x,y);if(GetCellSourceId(cell)>=0)continue;
            uint noise=unchecked((uint)((x*73856093)^(y*19349663)))&65535;
            double patch=Math.Sin(x*.12+y*.06)+Math.Cos(y*.17-x*.03);
            int tile=patch>.2?22:27;
            if(noise%13==0)tile=patch>.2?24:28;
            SetCell(cell,0,new Vector2I(tile%11,tile/11));
        }
    }
}
