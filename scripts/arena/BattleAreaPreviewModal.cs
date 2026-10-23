using Godot;

public partial class BattleAreaPreviewModal : GameModal
{
    public override void _Ready()
    {
        base._Ready();Cancel=()=>QueueFree();
        float width=Mathf.Min(840,GetViewportRect().Size.X-80);
        Body.CustomMinimumSize=new(width,0);
        var header=new HBoxContainer();Body.AddChild(header);
        var title=DeckMenuUi.Text(BattleAreas.Name,24);title.SizeFlagsHorizontal=SizeFlags.ExpandFill;header.AddChild(title);
        header.AddChild(DeckMenuUi.Button(GamePreferences.Text("Close","ปิด"),()=>QueueFree(),80));
        var preview=new SubViewportContainer {Name="AreaPreview",Stretch=true,CustomMinimumSize=new(width,Mathf.Max(180,Mathf.Min(width*9/16,GetViewportRect().Size.Y-230))),MouseFilter=MouseFilterEnum.Ignore};Body.AddChild(preview);
        var viewport=new SubViewport {Size=new(900,390),Disable3D=true,RenderTargetUpdateMode=SubViewport.UpdateMode.Always};preview.AddChild(viewport);
        Node2D scenery;
        if(GamePreferences.BattleArea=="undead_hollow")scenery=GD.Load<PackedScene>(BattleAreas.UndeadScenery).Instantiate<Node2D>();
        else {
            scenery=new Node2D {YSortEnabled=true};
            var source=GD.Load<PackedScene>("res://scenes/main.tscn").Instantiate<Node2D>();
            foreach(var child in source.GetChildren())if(child is TileMapLayer || child.Name=="Wildlife") {child.Owner=null;source.RemoveChild(child);scenery.AddChild(child);}
            source.Free();
        }
        viewport.AddChild(scenery);
        var camera=new Camera2D {Position=GamePreferences.BattleArea=="undead_hollow"?new(408,350):new(408,396),Zoom=Vector2.One*(GamePreferences.BattleArea=="undead_hollow"?Mathf.Min(width/1248f,preview.CustomMinimumSize.Y/832f):width/914f)};scenery.AddChild(camera);
        var note=DeckMenuUi.Text(GamePreferences.Text("Selected for your next match","เลือกพื้นที่นี้สำหรับแมตช์ถัดไปแล้ว"),14);Body.AddChild(note);
    }
}

