using Godot;

public partial class BattleDisplay : Node2D
{
    public override void _Ready()
    {
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
