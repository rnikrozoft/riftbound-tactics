using Godot;

public partial class BattleDisplay : Node2D
{
    // Reserved for future trap/effect cards; combat deployment remains six slots.
    public static readonly Vector2I[] SupportCells = {new(37,14),new(37,16),new(37,18),new(37,20),new(37,22)};
    public static readonly Vector2I[] EnemySupportCells = {new(65,14),new(65,16),new(65,18),new(65,20),new(65,22)};
    private Vector2 _arenaOffset;
    public Vector2 ArenaOffset
    {
        get => _arenaOffset;
        set { Position += value - _arenaOffset; _arenaOffset = value; }
    }

    public override void _Ready()
    {
        var tiles=GetNode<TileMapLayer>("TileMapLayer");
        var support=new Node2D {Name="SupportSlots",ZIndex=1};AddChild(support);
        AddSupportSlots(support,tiles,SupportCells);
        var enemySupport=new Node2D {Name="EnemySupportSlots",ZIndex=1};AddChild(enemySupport);
        AddSupportSlots(enemySupport,tiles,EnemySupportCells);
        TravelBookUi.StyleButton(GetNode<TextureButton>("UI/SafeArea/Content/EffectsLabButton"));
        GetNode<TextureButton>("UI/SafeArea/Content/EffectsLabButton").Pressed +=
            () => GetTree().ChangeSceneToFile("res://scenes/damage_simulator.tscn");
        var content=GetNode<Control>("UI/SafeArea/Content");
        var lab=content.GetNode<TextureButton>("EffectsLabButton");
        lab.AnchorTop=lab.AnchorBottom=0;lab.OffsetTop=16;lab.OffsetBottom=48;lab.OffsetLeft=-148;lab.OffsetRight=-16;
        GameUi.Label(lab.GetNode<Label>("Text"),13);lab.GetNode<Label>("Text").Text="EFFECTS LIBRARY";
        var result=content.GetNode<Control>("BattleResult");
        result.AnchorTop=result.AnchorBottom=.5f;result.OffsetTop=-54;result.OffsetBottom=54;result.OffsetLeft=-230;result.OffsetRight=230;
        result.GetNode<TextureRect>("Banner").Hide();GameUi.Backdrop(result,true);GameUi.Label(result.GetNode<Label>("Text"),26);result.GetNode<Label>("Text").OffsetTop=20;
        var heading=new Label {Name="Heading",Text="BATTLE RESULT",AnchorRight=1,OffsetTop=12,OffsetBottom=32,HorizontalAlignment=HorizontalAlignment.Center,MouseFilter=Control.MouseFilterEnum.Ignore};GameUi.Label(heading,12);heading.AddThemeColorOverride("font_color",GameUi.Gold);result.AddChild(heading);
        if (OS.GetName() is "Windows" or "Linux" or "macOS")
        {
            var window = GetWindow();
            window.Unresizable = false;
            window.MaximizeDisabled = false;
            window.MinSize = new Vector2I(640, 360);
            window.MaxSize = Vector2I.Zero;
        }
    }
    private static void AddSupportSlots(Node2D parent,TileMapLayer tiles,Vector2I[] cells)
    {
        foreach(var cell in cells) {
            var slot=new Node2D {Position=tiles.Position+tiles.MapToLocal(cell)};parent.AddChild(slot);
            var points=new[]{new Vector2(-19,0),new Vector2(0,-9),new Vector2(19,0),new Vector2(0,9)};
            slot.AddChild(new Polygon2D {Polygon=points,Color=new Color(.13f,.2f,.23f,.65f)});
            slot.AddChild(new Line2D {Points=points,Closed=true,Width=1,DefaultColor=new Color(.72f,.61f,.4f,.8f)});
        }
    }
}
