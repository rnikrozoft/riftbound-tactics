using Godot;
// Shares the existing merge eligibility signal; no independent gameplay state.
public partial class UpgradeCardFrame:TextureRect
{
 private double _elapsed;private int _frame=-1;
 public override void _Ready(){MouseFilter=MouseFilterEnum.Ignore;TextureFilter=TextureFilterEnum.Nearest;ExpandMode=ExpandModeEnum.IgnoreSize;StretchMode=StretchModeEnum.Scale;SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);OffsetLeft=OffsetTop=-3;OffsetRight=OffsetBottom=3;}
 public override void _Process(double delta){if(!IsVisibleInTree())return;_elapsed+=delta;int frame=(int)(_elapsed*8)%4;if(frame==_frame)return;_frame=frame;Texture=PixelPackUi.Frame("02",new(frame*16,0,16,16));}
 public static void Set(Node parent,bool enabled){if(parent is not Control card)return;var border=card.GetNodeOrNull<UpgradeCardFrame>("UpgradeCardFrame");if(border==null&&enabled){border=new UpgradeCardFrame{Name="UpgradeCardFrame",ZIndex=24};card.AddChild(border);}if(border!=null)border.Visible=enabled;}
}
