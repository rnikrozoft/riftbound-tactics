using Godot;
public static class PixelPackUi
{
 public const string Root="res://assets/ui/Pixel UI pack 4/";
 public static AtlasTexture Frame(string sheet,Rect2 region)=>new(){Atlas=GD.Load<Texture2D>(Root+sheet+".png"),Region=region};
}
public partial class PixelHealthBar:ProgressBar
{
 public float BarHeight {get;set;}=5;
 public override Vector2 _GetMinimumSize()=>Vector2.Zero;
 private readonly Texture2D _full=PixelPackUi.Frame("05",new(0,21,48,6));
 private readonly Texture2D _empty=PixelPackUi.Frame("05",new(240,21,48,6));
 public override void _Ready(){TextureFilter=TextureFilterEnum.Nearest;ShowPercentage=false;AddThemeStyleboxOverride("background",new StyleBoxEmpty());AddThemeStyleboxOverride("fill",new StyleBoxEmpty());ValueChanged+=_=>QueueRedraw();Size=new(Size.X,BarHeight);}
 public override void _Draw(){DrawTextureRect(_empty,new Rect2(Vector2.Zero,Size),false);float ratio=(float)GetAsRatio();if(ratio>0)DrawTextureRectRegion(_full,new Rect2(0,0,Size.X*ratio,Size.Y),new Rect2(0,0,48*ratio,6));}
}
public partial class PixelLoadingIndicator:TextureRect
{
 private double _elapsed;private int _frame=-1;
 public override void _Ready(){TextureFilter=TextureFilterEnum.Nearest;MouseFilter=MouseFilterEnum.Ignore;ExpandMode=ExpandModeEnum.IgnoreSize;StretchMode=StretchModeEnum.KeepAspectCentered;CustomMinimumSize=new(40,40);}
 public override void _Process(double delta){if(!IsVisibleInTree())return;_elapsed+=delta;int frame=(int)(_elapsed*8)%5;if(frame!=_frame){_frame=frame;Texture=PixelPackUi.Frame("03",new(frame*48,48,48,48));}}
}
