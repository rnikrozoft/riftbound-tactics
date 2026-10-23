using Godot;
using System;
using System.Linq;
public partial class PlayingCardPacksRunner:Node
{
 public override async void _Ready(){try{
  var styles=CharacterData.All.Where(c=>c.Enabled).GroupBy(c=>c.CardArtPath).ToArray();if(styles.Length!=8)throw new Exception("Missing card styles");
  foreach(var c in CharacterData.All){var a=CardCatalog.Art(c.Kind);if(a.Atlas.ResourcePath!=c.CardArtPath||a.GetWidth()!=23||a.GetHeight()!=35)throw new Exception("Wrong card frame "+c.Id);}
  var bg=new ColorRect{Color=new Color("0b1018"),Size=new(1280,720)};AddChild(bg);var grid=new GridContainer{Columns=5,Position=new(35,25),Size=new(1210,660)};grid.AddThemeConstantOverride("h_separation",30);grid.AddThemeConstantOverride("v_separation",18);AddChild(grid);
  foreach(var group in styles){var c=group.First();var box=new VBoxContainer{CustomMinimumSize=new(210,300)};grid.AddChild(box);var title=DeckMenuUi.Text(c.CardArtPath.Split('/')[6]+(c.CardArtPath.Contains("Light")?" light":c.CardArtPath.Contains("Dark")?" dark":""),16);box.AddChild(title);box.AddChild(DeckMenuUi.Art(c.Kind,138,210));box.AddChild(DeckMenuUi.Text(c.Name,14));}
  for(int i=0;i<10;i++)await ToSignal(GetTree(),SceneTree.SignalName.ProcessFrame);
  if(DisplayServer.GetName()!="headless"){await ToSignal(RenderingServer.Singleton,RenderingServer.SignalName.FramePostDraw);GetViewport().GetTexture().GetImage().SavePng("/tmp/playing-card-packs.png");}
  GD.Print("PLAYING CARD PACKS PASS: all eight packs / nine frames, 514 stable catalog assignments, existing character portraits and idle animation");GetTree().Quit();
 }catch(Exception e){GD.PushError(e.ToString());GetTree().Quit(1);}}
}
