using Godot;

public partial class BattleDisplay : Node2D
{
    private Vector2 _arenaOffset;
    public Vector2 ArenaOffset
    {
        get => _arenaOffset;
        set { Position += value - _arenaOffset; _arenaOffset = value; }
    }

    public override void _Ready()
    {
        TravelBookUi.StyleButton(GetNode<TextureButton>("UI/SafeArea/Content/EffectsLabButton"));
        GetNode<TextureButton>("UI/SafeArea/Content/EffectsLabButton").Pressed +=
            () => GetTree().ChangeSceneToFile("res://scenes/damage_simulator.tscn");
        if (OS.GetName() is "Windows" or "Linux" or "macOS")
        {
            var window = GetWindow();
            window.Mode = Window.ModeEnum.Windowed;
            window.Unresizable = true;
            window.MaximizeDisabled = true;
            window.MinSize = window.MaxSize = window.Size = new Vector2I(1280, 720);
        }
    }
}
