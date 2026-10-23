using Godot;
public partial class PaladinIconContactRunner:Node
{
 public override async void _Ready(){var bg=new ColorRect{Size=new(1280,720),Color=new Color("152030")};AddChild(bg);for(int i=1;i<=48;i++){int x=(i-1)%8,y=(i-1)/8;var icon=new TextureRect{Texture=GD.Load<Texture2D>($"res://assets/free-paladin-pixel-art-skill-icons/PNG/Icon{i}.png"),Position=new(25+x*155,12+y*115),Size=new(76,76),ExpandMode=TextureRect.ExpandModeEnum.IgnoreSize,TextureFilter=CanvasItem.TextureFilterEnum.Nearest};AddChild(icon);AddChild(new Label{Text=i.ToString(),Position=new(105+x*155,35+y*115)});}for(int i=0;i<5;i++)await ToSignal(GetTree(),SceneTree.SignalName.ProcessFrame);await ToSignal(RenderingServer.Singleton,RenderingServer.SignalName.FramePostDraw);GetViewport().GetTexture().GetImage().SavePng("/tmp/paladin-icons.png");GetTree().Quit();}
}
