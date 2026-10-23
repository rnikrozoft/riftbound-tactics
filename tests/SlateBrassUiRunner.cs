using Godot;
using System;
using System.Linq;
public partial class SlateBrassUiRunner : Node
{
    private async System.Threading.Tasks.Task Shot(string name)
    {
        for(int i=0;i<4;i++)await ToSignal(GetTree(),SceneTree.SignalName.ProcessFrame);
        if(DisplayServer.GetName()!="headless") {
            await ToSignal(RenderingServer.Singleton,RenderingServer.SignalName.FramePostDraw);
            GetViewport().GetTexture().GetImage().SavePng("/tmp/riftbound-slate-"+name+".png");
        }
    }
    public override async void _Ready()
    {
        try {
            foreach(string path in new[]{"welcome","lobby","deck_builder","combat_simulator_lab"}) {
                var page=GD.Load<PackedScene>("res://scenes/"+path+".tscn").Instantiate<Control>();AddChild(page);
                await Shot(path);
                foreach(var button in page.FindChildren("*","TextureButton",true,false).OfType<TextureButton>()) {
                    if(button.TextureNormal==null)continue;
                    if(button.Material!=null)throw new Exception("Old button shader remains");
                    if(!button.TextureNormal.ResourcePath.StartsWith("res://assets/ui/slate_brass/"))throw new Exception("Button uses old texture: "+button.Name);
                    if(button.TextureHover==null||button.TexturePressed==null||button.TextureDisabled==null)throw new Exception("Missing button states");
                }
                page.Free();
            }
            if(CardCatalog.Art(0).Atlas.ResourcePath!="res://assets/ui/cards/deco/DecoCard.png")throw new Exception("DecoCards changed");
            GD.Print("SLATE BRASS UI PASS: welcome, lobby, deck builder and simulator use new button states, no old shader, DecoCards retained");GetTree().Quit();
        }catch(Exception e){GD.PushError(e.ToString());GetTree().Quit(1);}
    }
}
