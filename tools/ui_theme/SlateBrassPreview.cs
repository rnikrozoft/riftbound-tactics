using Godot;
using System;
using System.Linq;
using System.Text.Json;

public partial class SlateBrassPreview : Control
{
    public override async void _Ready()
    {
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        Theme=GD.Load<Theme>("res://assets/ui/slate_brass/slate_brass_theme.tres");
        var background=new ColorRect {Color=new Color("101820"),MouseFilter=MouseFilterEnum.Ignore};AddChild(background);background.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        var box=new VBoxContainer {OffsetLeft=24,OffsetTop=20,AnchorRight=1,AnchorBottom=1,OffsetRight=-24,OffsetBottom=-20};AddChild(box);
        box.AddChild(new Label {Text="SLATE & BRASS / GENERATED UI ASSETS",ThemeTypeVariation="HeaderLarge"});
        box.AddChild(new Label {Text="Preview only. Generated textures do not replace the game's current UI theme."});
        var tabs=new TabContainer {SizeFlagsVertical=SizeFlags.ExpandFill};box.AddChild(tabs);
        using var doc=JsonDocument.Parse(FileAccess.GetFileAsString("res://assets/ui/slate_brass/manifest.json"));
        foreach(string category in new[]{"controls","surfaces","icons"}) {
            var scroll=new ScrollContainer {Name=category,HorizontalScrollMode=ScrollContainer.ScrollMode.Disabled};tabs.AddChild(scroll);
            var grid=new GridContainer {Columns=4,SizeFlagsHorizontal=SizeFlags.ExpandFill};scroll.AddChild(grid);grid.AddThemeConstantOverride("h_separation",16);grid.AddThemeConstantOverride("v_separation",12);
            foreach(var entry in doc.RootElement.GetProperty("assets").EnumerateArray().Where(e=>e.GetProperty("category").GetString()==category)) {
                var cell=new VBoxContainer {SizeFlagsHorizontal=SizeFlags.ExpandFill};grid.AddChild(cell);
                var image=new TextureRect {Texture=GD.Load<Texture2D>(entry.GetProperty("texture").GetString()!),CustomMinimumSize=new(220,96),ExpandMode=TextureRect.ExpandModeEnum.IgnoreSize,StretchMode=TextureRect.StretchModeEnum.KeepAspectCentered,TextureFilter=TextureFilterEnum.Nearest};cell.AddChild(image);
                cell.AddChild(new Label {Text=entry.GetProperty("name").GetString(),HorizontalAlignment=HorizontalAlignment.Center});
            }
        }
        if(OS.GetCmdlineUserArgs().Contains("--capture")) {
            for(int i=0;i<3;i++) {
                tabs.CurrentTab=i;
                for(int f=0;f<4;f++)await ToSignal(GetTree(),SceneTree.SignalName.ProcessFrame);
                await ToSignal(RenderingServer.Singleton,RenderingServer.SignalName.FramePostDraw);
                GetViewport().GetTexture().GetImage().SavePng($"/tmp/slate-brass-{tabs.GetTabTitle(i)}-preview.png");
            }
            GD.Print("SLATE BRASS PREVIEW PASS: all 48 resources loaded and rendered");GetTree().Quit();
        }
    }
}
